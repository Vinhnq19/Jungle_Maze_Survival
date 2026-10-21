using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using JungleBackend.Protocol;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Ports;
using JungleMaze.Core.Events;
using JungleMaze.Core.Logging;
using JungleMaze.Core.Ticking;
using JungleMaze.Domain.Network;
using LiteNetLib;
using LiteNetLib.Utils;
using DisconnectReason = JungleMaze.Domain.Network.DisconnectReason;

namespace JungleMaze.Infrastructure.Network
{
    /// <summary>
    /// Client UDP LiteNetLib: handshake protocol v1/v2 ở channel 0, packet gameplay ở channel 1 qua PacketRegistry.
    /// Toàn bộ callback chạy trong Tick() (PollEvents ở main thread, UnsyncedEvents = false).
    /// Không tự reconnect: lỗi mạng/capacity hiện rõ bằng state Failed + DisconnectReason.
    /// </summary>
    public sealed class NetworkClient : INetworkClient, IPacketSender, IPacketReceiver, ITickable, IDisposable
    {
        private const int PingIntervalMs = 1000;
        private const int DisconnectTimeoutMs = 5000;
        private const int MaxIdChars = 64, MaxMessageBytes = 256;

        private readonly IEventBus _bus;
        private readonly PacketRegistry _registry;
        private readonly ILog _log;
        private readonly Func<long> _clock;
        private readonly EventBus _packetBus;
        private readonly HashSet<Type> _registeredPackets = new HashSet<Type>();
        private readonly NetDataWriter _controlWriter = new NetDataWriter(true, 64);
        private readonly NetDataWriter _gameplayWriter = new NetDataWriter(true, 256);
        private readonly RttTracker _rtt = new RttTracker(timestampFrequency: Stopwatch.Frequency);
        private readonly EventBasedNetListener _listener = new EventBasedNetListener();

        private NetManager _manager;
        private NetPeer _peer;
        private ConnectRequest _request;
        private long _connectStarted, _nextPing;
        private int _pingSequence, _invalidPackets;
        private bool _transportConnected, _authSent, _closing, _disposed;

        public NetworkClient(IEventBus bus, PacketRegistry registry, ILog log = null, Func<long> clock = null)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _log = log ?? NullLog.Instance;
            _clock = clock ?? Stopwatch.GetTimestamp;
            _packetBus = new EventBus(_log);
            _listener.PeerConnectedEvent += OnPeerConnected;
            _listener.PeerDisconnectedEvent += OnPeerDisconnected;
            _listener.NetworkReceiveEvent += OnNetworkReceive;
            _listener.NetworkErrorEvent += OnNetworkError;
        }

        public NetworkState State { get; private set; } = NetworkState.Disconnected;
        public DisconnectReason LastReason { get; private set; }
        public string PlayerId { get; private set; }
        public string Endpoint { get; private set; } = "";
        public int LocalPort => _manager?.LocalPort ?? 0;

        public NetworkStats Stats
        {
            get
            {
                var stats = new NetworkStats
                {
                    LastRttMs = _rtt.LastMs,
                    AverageRttMs = _rtt.AverageMs,
                    P95RttMs = _rtt.Percentile(0.95),
                    PeerRttMs = _peer?.RoundTripTime ?? 0,
                    PingsSent = _rtt.Sent,
                    PongsReceived = _rtt.Received,
                    PongsLost = _rtt.Lost,
                    InvalidPackets = _invalidPackets
                };
                var statistics = _manager?.Statistics;
                if (statistics != null)
                {
                    stats.PacketsSent = statistics.PacketsSent;
                    stats.PacketsReceived = statistics.PacketsReceived;
                    stats.BytesSent = statistics.BytesSent;
                    stats.BytesReceived = statistics.BytesReceived;
                }
                return stats;
            }
        }

        public void Connect(in ConnectRequest request)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NetworkClient));
            if (State != NetworkState.Disconnected && State != NetworkState.Failed)
            {
                _log.Warn($"[Net] Bỏ qua Connect khi đang {State}.");
                return;
            }
            ResetSession();
            _request = request;
            var config = request.Config;
            if (config == null) { FailBeforeStart(DisconnectReason.InvalidConfig, "config null"); return; }
            Endpoint = config.Endpoint;
            var invalid = config.Validate();
            if (invalid != null) { FailBeforeStart(DisconnectReason.InvalidConfig, invalid); return; }

            var name = request.DisplayName?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > NetworkProtocol.MaxNameChars)
                { FailBeforeStart(DisconnectReason.InvalidDisplayName, "tên 1–32 ký tự"); return; }
            if (string.IsNullOrWhiteSpace(request.RoomCode) || request.RoomCode.Length > NetworkProtocol.MaxRoomChars)
                { FailBeforeStart(DisconnectReason.InvalidRoomCode, "mã phòng 1–64 ký tự"); return; }
            if (config.AuthMode == JoinAuthMode.Jwt && string.IsNullOrWhiteSpace(request.AccessToken))
                { FailBeforeStart(DisconnectReason.AuthenticationRequired, "authMode=Jwt cần đăng nhập trước"); return; }

            NetDataWriter connectData;
            try
            {
                var attemptId = config.ProtocolVersion == NetworkProtocol.LatestVersion ? Guid.NewGuid() : Guid.Empty;
                connectData = NetworkProtocol.WriteConnect(config.AuthMode, request.RoomCode, name,
                    config.ProtocolVersion, attemptId);
            }
            catch (ArgumentException e) { FailBeforeStart(DisconnectReason.InvalidConfig, e.Message); return; }

            _manager = new NetManager(_listener)
            {
                AutoRecycle = true,
                IPv6Enabled = false,
                UnsyncedEvents = false,
                EnableStatistics = true,
                ChannelsCount = GameplayProtocol.ChannelsCount,
                DisconnectTimeout = DisconnectTimeoutMs,
                ReconnectDelay = 500,
                MaxConnectAttempts = Math.Max(1, (int)Math.Ceiling(config.ConnectTimeoutMs / 500.0))
            };
            if (!_manager.Start()) { _manager = null; FailBeforeStart(DisconnectReason.SocketError, "không mở được socket UDP"); return; }

            _connectStarted = _clock();
            SetState(NetworkState.Connecting, DisconnectReason.None);
            _log.Info($"[Net] Connecting {Endpoint} mode={config.Mode} auth={config.AuthMode} v{config.ProtocolVersion}");
            try
            {
                // Hostname (DuckDNS) được resolve trước; LiteNetLib nhận IPEndPoint.
                var address = ResolveIPv4(config.GameServerHost);
                _peer = _manager.Connect(new IPEndPoint(address, config.GameServerPort), connectData);
            }
            catch (Exception e) when (e is SocketException || e is ArgumentException)
            {
                Fail(DisconnectReason.SocketError, "không resolve được host: " + e.Message);
            }
        }

        public void Disconnect()
        {
            if (_manager == null) { if (State == NetworkState.Failed) SetState(NetworkState.Disconnected, LastReason); return; }
            StopTransport();
            SetState(NetworkState.Disconnected, DisconnectReason.UserRequested);
        }

        public void Tick(float deltaTime)
        {
            if (_manager == null) return;
            _manager.PollEvents();
            if (_manager == null) return; // callback có thể đã Fail và dừng transport.

            var now = _clock();
            if (State == NetworkState.Connecting || State == NetworkState.Authenticating)
            {
                if (ElapsedMs(_connectStarted, now) >= _request.Config.ConnectTimeoutMs)
                    Fail(_transportConnected ? DisconnectReason.JoinTimeout : DisconnectReason.ConnectTimeout, null);
                return;
            }
            if (State != NetworkState.Joined) return;
            if (_peer != null && _peer.ConnectionState == ConnectionState.Connected && now >= _nextPing)
            {
                NetworkProtocol.Write(_controlWriter, NetworkProtocol.Ping, ++_pingSequence);
                _peer.Send(_controlWriter, GameplayProtocol.ControlChannel, DeliveryMethod.Unreliable);
                _rtt.OnPingSent(_pingSequence, now);
                // Lịch tuyệt đối để không cộng dồn độ trễ frame; tụt quá xa thì bắt lại, không gửi dồn.
                _nextPing += Stopwatch.Frequency * PingIntervalMs / 1000;
                if (_nextPing < now) _nextPing = now + Stopwatch.Frequency * PingIntervalMs / 1000;
            }
            _rtt.ExpirePending(now);
        }

        public bool Send<T>(ref T packet, PacketDelivery delivery) where T : struct, INetSerializable
        {
            if (State != NetworkState.Joined || _peer == null) return false;
            _registry.WriteFrame(_gameplayWriter, ref packet);
            _peer.Send(_gameplayWriter, GameplayProtocol.GameplayChannel, ToDeliveryMethod(delivery));
            return true;
        }

        public void Subscribe<T>(Action<T> handler) where T : struct, INetSerializable
        {
            if (_registeredPackets.Add(typeof(T))) _registry.Subscribe<T>(PublishPacket);
            _packetBus.Subscribe(handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct, INetSerializable => _packetBus.Unsubscribe(handler);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_manager != null)
            {
                StopTransport();
                SetState(NetworkState.Disconnected, DisconnectReason.UserRequested);
            }
        }

        private void PublishPacket<T>(T packet) where T : struct => _packetBus.Publish(packet);

        private void OnPeerConnected(NetPeer peer)
        {
            if (peer != _peer) return;
            _transportConnected = true;
            _log.Info("[Net] Transport connected, chờ JoinAccepted.");
        }

        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            if (_closing || peer != _peer) return;
            var data = info.AdditionalData;
            if (data != null && data.AvailableBytes > 0
                && NetworkProtocol.TryReadStringMessage(data, NetworkProtocol.JoinRejected, MaxMessageBytes, MaxIdChars, out var text))
            {
                Fail(DisconnectReasons.FromServer(text), text);
                return;
            }
            switch (info.Reason)
            {
                case LiteNetLib.DisconnectReason.RemoteConnectionClose:
                case LiteNetLib.DisconnectReason.DisconnectPeerCalled:
                    Fail(DisconnectReason.ServerClosed, info.Reason.ToString());
                    break;
                case LiteNetLib.DisconnectReason.Timeout:
                case LiteNetLib.DisconnectReason.ConnectionFailed:
                    Fail(State == NetworkState.Joined ? DisconnectReason.ConnectionLost
                        : _transportConnected ? DisconnectReason.JoinTimeout : DisconnectReason.ConnectTimeout, info.Reason.ToString());
                    break;
                case LiteNetLib.DisconnectReason.NetworkUnreachable:
                case LiteNetLib.DisconnectReason.HostUnreachable:
                case LiteNetLib.DisconnectReason.InvalidProtocol:
                case LiteNetLib.DisconnectReason.UnknownHost:
                    Fail(DisconnectReason.SocketError, info.Reason.ToString());
                    break;
                default:
                    Fail(State == NetworkState.Joined ? DisconnectReason.ConnectionLost : DisconnectReason.Unknown, info.Reason.ToString());
                    break;
            }
        }

        private void OnNetworkError(IPEndPoint endPoint, SocketError error)
        {
            if (_closing) return;
            Fail(DisconnectReason.SocketError, error.ToString());
        }

        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod method)
        {
            if (peer != _peer || reader.AvailableBytes == 0) { _invalidPackets++; return; }
            // Unreliable luôn tới channel 0 nên nhận dạng frame gameplay bằng marker, ở mọi channel.
            if (PacketRegistry.IsFrame(reader))
            {
                if (State != NetworkState.Joined || !_registry.TryReadFrame(reader)) _invalidPackets++;
                return;
            }
            if (channel != GameplayProtocol.ControlChannel) { _invalidPackets++; return; }

            var type = reader.PeekByte();
            var reliable = method == DeliveryMethod.ReliableOrdered;
            if (reliable && type == NetworkProtocol.AuthRequired) { HandleAuthRequired(reader); return; }
            if (reliable && type == NetworkProtocol.JoinAccepted) { HandleJoinAccepted(reader); return; }
            if (reliable && type == NetworkProtocol.JoinRejected)
            {
                Fail(NetworkProtocol.TryReadStringMessage(reader, NetworkProtocol.JoinRejected, MaxMessageBytes, MaxIdChars, out var reason)
                    ? DisconnectReasons.FromServer(reason) : DisconnectReason.ProtocolError, reason);
                return;
            }
            if (type == NetworkProtocol.Pong && State == NetworkState.Joined
                && NetworkProtocol.TryRead(reader, NetworkProtocol.Pong, out var sequence))
            {
                // Pong trùng hoặc đến sau timeout không được tính RTT.
                _rtt.OnPongReceived(sequence, _clock());
                return;
            }
            _invalidPackets++;
        }

        private void HandleAuthRequired(NetPacketReader reader)
        {
            if (reader.AvailableBytes != 1 || _request.Config.AuthMode != JoinAuthMode.Jwt || State == NetworkState.Joined
                || _authSent || string.IsNullOrWhiteSpace(_request.AccessToken))
            {
                Fail(DisconnectReason.ProtocolError, "UnexpectedAuthRequest");
                return;
            }
            _peer.Send(NetworkProtocol.WriteAuth(_request.AccessToken), GameplayProtocol.ControlChannel, DeliveryMethod.ReliableOrdered);
            _authSent = true;
            SetState(NetworkState.Authenticating, DisconnectReason.None);
        }

        private void HandleJoinAccepted(NetPacketReader reader)
        {
            if (State == NetworkState.Joined
                || !NetworkProtocol.TryReadStringMessage(reader, NetworkProtocol.JoinAccepted, MaxMessageBytes, MaxIdChars, out var id))
            {
                Fail(DisconnectReason.ProtocolError, "MalformedJoinAccepted");
                return;
            }
            PlayerId = id;
            _nextPing = _clock();
            var joinMs = ElapsedMs(_connectStarted, _nextPing);
            SetState(NetworkState.Joined, DisconnectReason.None);
            _log.Info(string.Format(CultureInfo.InvariantCulture, "[Net] JoinAccepted endpoint={0} joinMs={1:F0}", Endpoint, joinMs));
            _bus.Publish(new JoinedRoom(id));
        }

        private void FailBeforeStart(DisconnectReason reason, string detail)
        {
            _log.Warn($"[Net] Không kết nối: {reason} ({detail})");
            LastReason = reason;
            SetState(NetworkState.Failed, reason);
        }

        private void Fail(DisconnectReason reason, string detail)
        {
            if (State == NetworkState.Failed && _manager == null) return;
            var wasJoined = State == NetworkState.Joined;
            LogSummary(reason, detail);
            StopTransport();
            SetState(NetworkState.Failed, reason);
            if (!wasJoined) PlayerId = null;
        }

        private void SetState(NetworkState next, DisconnectReason reason)
        {
            var previous = State;
            if (next != NetworkState.Joined && next != NetworkState.Connecting && next != NetworkState.Authenticating)
                LastReason = reason;
            if (previous == next && next != NetworkState.Failed) return;
            State = next;
            _bus.Publish(new NetworkStateChanged(previous, next, reason));
        }

        private void StopTransport()
        {
            if (_manager == null) return;
            _closing = true;
            try { _manager.Stop(); } // Stop gửi disconnect cho server để trả slot ngay.
            finally
            {
                _closing = false;
                _manager = null;
                _peer = null;
            }
        }

        private void ResetSession()
        {
            _rtt.Reset();
            _pingSequence = 0;
            _invalidPackets = 0;
            _transportConnected = _authSent = false;
            PlayerId = null;
            LastReason = DisconnectReason.None;
        }

        private void LogSummary(DisconnectReason reason, string detail)
        {
            // Cùng dạng [client-summary] với NetSmokeClient để so số đo; không in room code/token.
            _log.Info(string.Format(CultureInfo.InvariantCulture,
                "[client-summary] endpoint={0} joined={1} reason={2}{3} sent={4} pongs={5} timedOut={6} rttP95Ms={7:F2} rttAvgMs={8:F2}",
                Endpoint, State == NetworkState.Joined, reason, detail == null ? "" : "(" + detail + ")",
                _rtt.Sent, _rtt.Received, _rtt.Lost, _rtt.Percentile(0.95), _rtt.AverageMs));
        }

        private static double ElapsedMs(long from, long to) => (to - from) * 1000.0 / Stopwatch.Frequency;

        private static IPAddress ResolveIPv4(string host)
        {
            if (IPAddress.TryParse(host, out var literal)) return literal;
            foreach (var address in Dns.GetHostAddresses(host))
                if (address.AddressFamily == AddressFamily.InterNetwork) return address;
            throw new ArgumentException($"Host {host} không có địa chỉ IPv4.");
        }

        private static DeliveryMethod ToDeliveryMethod(PacketDelivery delivery)
        {
            switch (delivery)
            {
                case PacketDelivery.Sequenced: return DeliveryMethod.Sequenced;
                case PacketDelivery.ReliableOrdered: return DeliveryMethod.ReliableOrdered;
                default: return DeliveryMethod.Unreliable;
            }
        }
    }
}
