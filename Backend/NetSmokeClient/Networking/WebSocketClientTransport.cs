using System.Net.WebSockets;
using System.Threading.Channels;

namespace NetSmokeClient.Networking;

/// <summary>Async socket worker; the main client loop alone updates measurements/session state.</summary>
internal sealed class WebSocketClientTransport : IDisposable
{
    private readonly ClientWebSocket _socket = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Channel<byte[]> _send = Channel.CreateBounded<byte[]>(64);
    private readonly Channel<byte[]> _received = Channel.CreateBounded<byte[]>(1024);
    private readonly Task _worker;
    public volatile bool Connected;
    public volatile string? Error;
    public WebSocketClientTransport(Uri uri, byte[] handshake, string? connectIp=null) => _worker = Run(uri, handshake,connectIp);
    public bool Send(byte[] packet) => _send.Writer.TryWrite(packet);
    public bool TryReceive(out byte[] packet) => _received.Reader.TryRead(out packet!);
    private async Task Run(Uri uri, byte[] handshake,string? connectIp)
    {
        try
        {
            // Pin only the TCP destination, just like curl --resolve. The URI, SNI,
            // certificate chain and hostname validation remain unchanged. No global hosts edit.
            using var http=connectIp is null?null:new HttpMessageInvoker(new SocketsHttpHandler
            {
                UseProxy=false,
                ConnectCallback=async (context,cancellation)=>
                {
                    var socket=new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,
                        System.Net.Sockets.SocketType.Stream,System.Net.Sockets.ProtocolType.Tcp);
                    try
                    {
                        await socket.ConnectAsync(System.Net.IPAddress.Parse(connectIp),context.DnsEndPoint.Port,cancellation);
                        return new System.Net.Sockets.NetworkStream(socket,ownsSocket:true);
                    }
                    catch { socket.Dispose();throw; }
                }
            });
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            if(http is null)await _socket.ConnectAsync(uri, timeout.Token);
            else await _socket.ConnectAsync(uri,http,timeout.Token);
            Connected = true;
            _send.Writer.TryWrite(handshake);
            var sending = SendLoop(); var receiving = ReceiveLoop();
            await Task.WhenAny(sending, receiving);
            if (!_stop.IsCancellationRequested && Error is null) Error = "WssDisconnected";
            _stop.Cancel(); _socket.Abort();
            await Task.WhenAll(sending, receiving);
            if (!_stop.IsCancellationRequested) Error = "WssDisconnected";
        }
        catch (Exception error) when (error is WebSocketException or OperationCanceledException or IOException or System.Net.Sockets.SocketException)
        { if (!_stop.IsCancellationRequested) Error = Connected ? "WssDisconnected" : "WssConnectFailed"; }
        finally { if (!_stop.IsCancellationRequested && Error is null) Error = "WssDisconnected"; }
    }
    private async Task SendLoop()
    {
        await foreach (var data in _send.Reader.ReadAllAsync(_stop.Token))
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await _socket.SendAsync(data.AsMemory(), WebSocketMessageType.Binary, true, timeout.Token);
        }
    }
    private async Task ReceiveLoop()
    {
        while (!_stop.IsCancellationRequested)
        {
            var data = new byte[4096]; var used = 0;
            ValueWebSocketReceiveResult message;
            do
            {
                message = await _socket.ReceiveAsync(data.AsMemory(used), _stop.Token);
                if (message.MessageType == WebSocketMessageType.Close) { Error = "WssDisconnected"; return; }
                if (message.MessageType != WebSocketMessageType.Binary) { Error = "MalformedMessage"; return; }
                used += message.Count;
                if (used == data.Length && !message.EndOfMessage) { Error = "PayloadTooLarge"; return; }
            } while (!message.EndOfMessage);
            if (!_received.Writer.TryWrite(data[..used])) { Error = "SlowConsumer"; return; }
        }
    }
    public void Dispose()
    {
        _stop.Cancel(); _socket.Abort();
        try { _worker.Wait(TimeSpan.FromSeconds(3)); } catch (AggregateException) { }
        _socket.Dispose(); _stop.Dispose();
    }
}
