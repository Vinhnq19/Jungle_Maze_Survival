using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using JungleBackend.Protocol;

namespace JungleMaze.Infrastructure.Network
{
    /// <summary>
    /// Transport WebSocket không đồng bộ tương thích Unity .NET Standard 2.1 (sử dụng ArraySegment).
    /// Hàng đợi gửi giới hạn 64 packet, hàng đợi nhận giới hạn 1024 packet. Buffer nhận 4KiB binary.
    /// Vòng lặp I/O chạy trong background Task; việc phân phối packet và cập nhật logic diễn ra trên main thread qua Tick.
    /// SendLoop là writer duy nhất ghi vào socket. CloseGracefully gửi ExplicitQuit làm binary cuối cùng rồi đóng <= 1s.
    /// Dispose / Close hoàn toàn non-blocking, không chặn main thread.
    /// </summary>
    internal sealed class UnityWebSocketTransport : IDisposable
    {
        private const int MaxSendQueue = 64;
        private const int MaxReceiveQueue = 1024;
        private const int ReceiveBufferSize = 4096;

        private readonly ClientWebSocket _socket = new ClientWebSocket();
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private readonly CancellationTokenSource _gracefulDeadline = new CancellationTokenSource();
        private readonly ConcurrentQueue<byte[]> _sendQueue = new ConcurrentQueue<byte[]>();
        private readonly ConcurrentQueue<byte[]> _receiveQueue = new ConcurrentQueue<byte[]>();
        private readonly SemaphoreSlim _sendSignal = new SemaphoreSlim(0, int.MaxValue);
        private readonly Task _worker;

        private int _sendCount;
        private int _disposeStarted;
        private int _receiveCount;
        private volatile bool _stopped;
        private volatile bool _closingGracefully;

        public volatile bool Connected;
        public volatile string Error;

        public long PacketsSent;
        public long PacketsReceived;
        public long BytesSent;
        public long BytesReceived;

        public UnityWebSocketTransport(Uri uri, byte[] handshake)
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            if (handshake == null) throw new ArgumentNullException(nameof(handshake));
            _worker = Run(uri, handshake);
        }

        public bool Send(byte[] packet)
        {
            if (_stopped || _stop.IsCancellationRequested || packet == null || _closingGracefully) return false;
            return EnqueueSend(packet);
        }

        private bool EnqueueSend(byte[] packet)
        {
            if (Interlocked.Increment(ref _sendCount) > MaxSendQueue)
            {
                Interlocked.Decrement(ref _sendCount);
                Error = "SlowConsumer";
                return false;
            }
            _sendQueue.Enqueue(packet);
            try { _sendSignal.Release(); }
            catch (ObjectDisposedException) { return false; }
            return true;
        }

        public bool TryReceive(out byte[] packet)
        {
            if (_receiveQueue.TryDequeue(out packet))
            {
                Interlocked.Decrement(ref _receiveCount);
                return true;
            }
            packet = null;
            return false;
        }

        /// <summary>
        /// Đóng kết nối có kiểm soát (graceful): drain toàn bộ packet đã xếp hàng, gửi 1 byte ExplicitQuit
        /// làm frame binary cuối cùng, sau đó đóng socket với thời hạn <= 1 giây. Không block main thread.
        /// </summary>
        public void CloseGracefully()
        {
            if (_stopped || _closingGracefully) return;
            _closingGracefully = true;

            // Enqueue ExplicitQuit làm packet cuối cùng
            EnqueueSend(new byte[] { NetworkProtocol.ExplicitQuit });

            // Giới hạn thời gian kết thúc graceful <= 1s
            _gracefulDeadline.CancelAfter(TimeSpan.FromSeconds(1));

            // Dọn dẹp ngầm sau khi worker kết thúc hoặc hết hạn 1s
            _ = Task.Run(async () =>
            {
                try
                {
                    // A cancellation token alone does not cancel an unrelated await.
                    // Also bound a still-pending ConnectAsync, not just the send loop.
                    await Task.WhenAny(_worker, Task.Delay(1000)).ConfigureAwait(false);
                }
                catch { }
                finally
                {
                    Dispose();
                }
            });
        }

        public void Abort()
        {
            if (_stopped) return;
            _stopped = true;
            try { _stop.Cancel(); } catch (ObjectDisposedException) { }
            try { _socket.Abort(); } catch { }
            try { _sendSignal.Release(); } catch { }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposeStarted, 1) != 0) return;
            Abort();
            _ = Task.Run(async () =>
            {
                try { await _worker.ConfigureAwait(false); } catch { }
                try { _socket.Dispose(); } catch { }
                try { _stop.Dispose(); } catch { }
                try { _gracefulDeadline.Dispose(); } catch { }
                try { _sendSignal.Dispose(); } catch { }
            });
        }

        private async Task Run(Uri uri, byte[] handshake)
        {
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(10));
                    await _socket.ConnectAsync(uri, timeout.Token).ConfigureAwait(false);
                }
                Connected = true;

                // Gửi handshake ban đầu
                EnqueueSend(handshake);

                var sending = SendLoop();
                var receiving = ReceiveLoop();
                await Task.WhenAny(sending, receiving).ConfigureAwait(false);

                if (!_stop.IsCancellationRequested && Error == null) Error = "WssDisconnected";
                _stop.Cancel();
                try { _socket.Abort(); } catch { }
                await Task.WhenAll(sending, receiving).ConfigureAwait(false);
                if (!_stop.IsCancellationRequested && Error == null) Error = "WssDisconnected";
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or IOException or SocketException)
            {
                if (!_stop.IsCancellationRequested && Error == null)
                    Error = Connected ? "WssDisconnected" : "WssConnectFailed";
            }
            catch (Exception)
            {
                if (!_stop.IsCancellationRequested && Error == null)
                    Error = Connected ? "WssDisconnected" : "WssConnectFailed";
            }
            finally
            {
                if (!_stop.IsCancellationRequested && Error == null) Error = "WssDisconnected";
            }
        }

        private async Task SendLoop()
        {
            while (!_stop.IsCancellationRequested)
            {
                // Khi đóng graceful và hàng đợi đã gửi hết
                if (_closingGracefully && _sendQueue.IsEmpty)
                {
                    try
                    {
                        using (var closeTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(500)))
                        {
                            if (_socket.State == WebSocketState.Open)
                                await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Quit", closeTimeout.Token).ConfigureAwait(false);
                        }
                    }
                    catch { }
                    break;
                }

                using (var waitCts = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token, _gracefulDeadline.Token))
                {
                    try
                    {
                        await _sendSignal.WaitAsync(waitCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }

                if (_sendQueue.TryDequeue(out var data))
                {
                    Interlocked.Decrement(ref _sendCount);
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token, _gracefulDeadline.Token))
                    {
                        timeout.CancelAfter(TimeSpan.FromSeconds(1));
                        await _socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, timeout.Token).ConfigureAwait(false);
                    }
                    Interlocked.Increment(ref PacketsSent);
                    Interlocked.Add(ref BytesSent, data.Length);
                }
            }
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[ReceiveBufferSize];
            while (!_stop.IsCancellationRequested)
            {
                var used = 0;
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer, used, buffer.Length - used), _stop.Token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Error = "WssDisconnected";
                        return;
                    }
                    if (result.MessageType != WebSocketMessageType.Binary)
                    {
                        Error = "MalformedMessage";
                        return;
                    }
                    used += result.Count;
                    if (used == buffer.Length && !result.EndOfMessage)
                    {
                        Error = "PayloadTooLarge";
                        return;
                    }
                } while (!result.EndOfMessage);

                var packet = new byte[used];
                Buffer.BlockCopy(buffer, 0, packet, 0, used);
                Interlocked.Increment(ref PacketsReceived);
                Interlocked.Add(ref BytesReceived, used);

                if (Interlocked.Increment(ref _receiveCount) > MaxReceiveQueue)
                {
                    Interlocked.Decrement(ref _receiveCount);
                    Error = "SlowConsumer";
                    return;
                }
                _receiveQueue.Enqueue(packet);
            }
        }
    }
}
