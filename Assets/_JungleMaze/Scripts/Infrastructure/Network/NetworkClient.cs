using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using JungleBackend.Protocol;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Config;
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
    /// Client mạng hỗ trợ UDP LiteNetLib, WebSocket (WSS) và Auto fallback.
    /// Handshake protocol v1/v2/v3, packet gameplay qua PacketRegistry.
    /// Toàn bộ callback và dispatch packet chạy trong Tick() ở main thread.
    /// DNS hostname chạy nền không block main thread; Auto giữ nguyên AttemptId;
    /// Business rejection không bao giờ fallback; Callback thế hệ cũ bị hủy khi chuyển transport;
    /// Disconnect/Dispose gửi ExplicitQuit có kiểm soát (CloseGracefully); Unplanned failure Abort ngay.
    /// </summary>
    public sealed class NetworkClient : INetworkClient, IPacketSender, IPacketReceiver, IMatchConnection, ITickable, IDisposable
    {
        private const int PingIntervalMs = 1000;
        private const int DisconnectTimeoutMs = 5000;
        private const int MaxIdChars = 64, MaxMessageBytes = 256;

        private sealed class DnsResolution
        {
            public readonly int Generation;
            public readonly IPAddress Address;
            public readonly Exception Error;

            public DnsResolution(int generation, IPAddress address, Exception error)
            {
                Generation = generation;
                Address = address;
                Error = error;
            }
        }

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
        private UnityWebSocketTransport _webSocket;
        private ConnectRequest _request;
        private NetDataWriter _udpConnectData;
        private volatile DnsResolution _dnsResult;

        private long _connectStarted, _nextPing, _wsConnectedAt, _lastPong;
        private int _pingSequence, _invalidPackets, _generation, _udpGeneration;
        private bool _transportConnected, _authSent, _closing, _disposed;

        private string _selectedTransport = "Udp";
        private string _fallbackReason;
        private Guid _resumeToken;
        private Guid _resumeForNextConnect;

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

        public Guid AttemptId { get; private set; }
        public ConnectRequest LastRequest => _request;

        public void Resume(Guid proof)
        {
            if (proof == Guid.Empty || (State != NetworkState.Failed && State != NetworkState.Disconnected)) return;
            var old = _request;
            var c = old.Config;
            if (c == null) return;
            _resumeForNextConnect = proof;
            var upgraded = new ClientConfig(c.Mode, c.ApiBaseUrl, c.GameServerHost, c.GameServerPort, c.AuthMode,
                NetworkProtocol.Version3, c.ConnectTimeoutMs, c.Transport, c.Source, c.WebSocketUrl, c.WorldEnabled);
            Connect(new ConnectRequest(upgraded, old.RoomCode, old.DisplayName, old.AccessToken));
        }

        public NetworkStats Stats
        {
            get
            {
                var stats = new NetworkStats
                {
                    LastRttMs = _rtt.LastMs,
                    AverageRttMs = _rtt.AverageMs,
                    P50RttMs = _rtt.Percentile(0.50),
                    P95RttMs = _rtt.Percentile(0.95),
                    P99RttMs = _rtt.Percentile(0.99),
                    PeerRttMs = _peer?.RoundTripTime ?? 0,
                    PingsSent = _rtt.Sent,
                    PongsReceived = _rtt.Received,
                    PongsLost = _rtt.Lost,
                    InvalidPackets = _invalidPackets,
                    Transport = _selectedTransport,
                    FallbackReason = _fallbackReason
                };
                var statistics = _manager?.Statistics;
                if (statistics != null)
                {
                    stats.PacketsSent = statistics.PacketsSent;
                    stats.PacketsReceived = statistics.PacketsReceived;
                    stats.BytesSent = statistics.BytesSent;
                    stats.BytesReceived = statistics.BytesReceived;
                }
                else if (_webSocket != null)
                {
                    stats.PacketsSent = _webSocket.PacketsSent;
                    stats.PacketsReceived = _webSocket.PacketsReceived;
                    stats.BytesSent = _webSocket.BytesSent;
                    stats.BytesReceived = _webSocket.BytesReceived;
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

            AttemptId = config.ProtocolVersion >= NetworkProtocol.LatestVersion ? Guid.NewGuid() : Guid.Empty;
            _resumeToken = _resumeForNextConnect;
            _resumeForNextConnect = Guid.Empty;

            if (config.Transport == "Wss")
            {
                _selectedTransport = "Wss";
                _fallbackReason = null;
                StartWebSocketTransport();
            }
            else
            {
                _selectedTransport = "Udp";
                _fallbackReason = null;
                StartUdpTransport();
            }
        }

        public void Disconnect()
        {
            if (_manager == null && _webSocket == null)
            {
                if (State == NetworkState.Failed) SetState(NetworkState.Disconnected, LastReason);
                return;
            }
            StopTransport(explicitQuit: true, gracefulWebSocket: true);
            SetState(NetworkState.Disconnected, DisconnectReason.UserRequested);
        }

        public void Tick(float deltaTime)
        {
            if (_disposed) return;

            if (_selectedTransport == "Wss")
            {
                TickWebSocket();
                return;
            }

            // Xử lý kết quả DNS chạy nền
            if (_dnsResult != null)
            {
                var res = _dnsResult;
                _dnsResult = null;
                if (res.Generation == _generation && _generation == _udpGeneration && _manager != null
                    && _selectedTransport == "Udp" && State == NetworkState.Connecting)
                {
                    if (res.Error != null || res.Address == null)
                    {
                        if (_request.Config.Transport == "Auto")
                        {
                            FallbackToWebSocket(DisconnectReason.SocketError, "không resolve được host: " + res.Error?.Message);
                            return;
                        }
                        Fail(DisconnectReason.SocketError, "không resolve được host: " + res.Error?.Message);
                        return;
                    }
                    try
                    {
                        _peer = _manager.Connect(new IPEndPoint(res.Address, _request.Config.GameServerPort), _udpConnectData);
                    }
                    catch (Exception e)
                    {
                        if (_request.Config.Transport == "Auto")
                        {
                            FallbackToWebSocket(DisconnectReason.SocketError, e.Message);
                            return;
                        }
                        Fail(DisconnectReason.SocketError, e.Message);
                        return;
                    }
                }
            }

            if (_manager == null) return;
            _manager.PollEvents();
            if (_manager == null) return;

            var now = _clock();
            if (State == NetworkState.Connecting || State == NetworkState.Authenticating)
            {
                var timeoutLimit = (_request.Config.Transport == "Auto")
                    ? Math.Min(5000, _request.Config.ConnectTimeoutMs)
                    : _request.Config.ConnectTimeoutMs;

                if (ElapsedMs(_connectStarted, now) >= timeoutLimit)
                {
                    if (_request.Config.Transport == "Auto")
                    {
                        FallbackToWebSocket(DisconnectReason.ConnectTimeout, "UDP connect/DNS timeout");
                        return;
                    }
                    Fail(_transportConnected ? DisconnectReason.JoinTimeout : DisconnectReason.ConnectTimeout, null);
                }
                return;
            }
            if (State != NetworkState.Joined) return;
            if (_peer != null && _peer.ConnectionState == ConnectionState.Connected && now >= _nextPing)
            {
                NetworkProtocol.Write(_controlWriter, NetworkProtocol.Ping, ++_pingSequence);
                _peer.Send(_controlWriter, GameplayProtocol.ControlChannel, DeliveryMethod.Unreliable);
                _rtt.OnPingSent(_pingSequence, now);
                _nextPing += Stopwatch.Frequency * PingIntervalMs / 1000;
                if (_nextPing < now) _nextPing = now + Stopwatch.Frequency * PingIntervalMs / 1000;
            }
            _rtt.ExpirePending(now);
        }

        public bool Send<T>(ref T packet, PacketDelivery delivery) where T : struct, INetSerializable
        {
            if (State != NetworkState.Joined) return false;
            _registry.WriteFrame(_gameplayWriter, ref packet);

            if (_selectedTransport == "Wss" && _webSocket != null)
            {
                return _webSocket.Send(_gameplayWriter.CopyData());
            }
            if (_peer != null)
            {
                _peer.Send(_gameplayWriter, GameplayProtocol.GameplayChannel, ToDeliveryMethod(delivery));
                return true;
            }
            return false;
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
            StopTransport(explicitQuit: true, gracefulWebSocket: true);
            if (State != NetworkState.Disconnected)
                SetState(NetworkState.Disconnected, DisconnectReason.UserRequested);
        }

        private void PublishPacket<T>(T packet) where T : struct => _packetBus.Publish(packet);

        private void StartUdpTransport()
        {
            _generation++;
            _udpGeneration = _generation;
            var currentGen = _generation;
            _dnsResult = null;

            try
            {
                _udpConnectData = NetworkProtocol.WriteConnect(_request.Config.AuthMode, _request.RoomCode,
                    _request.DisplayName.Trim(), _request.Config.ProtocolVersion, AttemptId, _resumeToken);
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
                MaxConnectAttempts = Math.Max(1, (int)Math.Ceiling(_request.Config.ConnectTimeoutMs / 500.0))
            };
            if (!_manager.Start())
            {
                _manager = null;
                if (_request.Config.Transport == "Auto")
                {
                    FallbackToWebSocket(DisconnectReason.SocketError, "không mở được socket UDP");
                    return;
                }
                FailBeforeStart(DisconnectReason.SocketError, "không mở được socket UDP");
                return;
            }

            _connectStarted = _clock();
            SetState(NetworkState.Connecting, DisconnectReason.None);
            _log.Info($"[Net] Connecting {Endpoint} mode={_request.Config.Mode} auth={_request.Config.AuthMode} v{_request.Config.ProtocolVersion} transport=Udp");

            var host = _request.Config.GameServerHost;
            if (IPAddress.TryParse(host, out var literal))
            {
                try
                {
                    _peer = _manager.Connect(new IPEndPoint(literal, _request.Config.GameServerPort), _udpConnectData);
                }
                catch (Exception e)
                {
                    if (_request.Config.Transport == "Auto")
                    {
                        FallbackToWebSocket(DisconnectReason.SocketError, e.Message);
                        return;
                    }
                    Fail(DisconnectReason.SocketError, e.Message);
                }
            }
            else
            {
                // DNS resolve chạy nền không block main thread
                Task.Run(async () =>
                {
                    try
                    {
                        var addresses = await Dns.GetHostAddressesAsync(host).ConfigureAwait(false);
                        IPAddress resolved = null;
                        for (var i = 0; i < addresses.Length; i++)
                        {
                            if (addresses[i].AddressFamily == AddressFamily.InterNetwork)
                            {
                                resolved = addresses[i];
                                break;
                            }
                        }
                        if (resolved == null) throw new SocketException((int)SocketError.HostNotFound);
                        _dnsResult = new DnsResolution(currentGen, resolved, null);
                    }
                    catch (Exception ex)
                    {
                        _dnsResult = new DnsResolution(currentGen, null, ex);
                    }
                });
            }
        }

        private void StartWebSocketTransport()
        {
            Endpoint = _request.Config.WebSocketUrl;
            _generation++;
            byte[] handshake;
            try
            {
                handshake = NetworkProtocol.WriteConnect(_request.Config.AuthMode, _request.RoomCode,
                    _request.DisplayName.Trim(), _request.Config.ProtocolVersion, AttemptId, _resumeToken).CopyData();
            }
            catch (ArgumentException e) { FailBeforeStart(DisconnectReason.InvalidConfig, e.Message); return; }

            _connectStarted = _clock();
            _wsConnectedAt = 0;
            _lastPong = 0;
            _selectedTransport = "Wss";
            SetState(NetworkState.Connecting, DisconnectReason.None);
            _log.Info($"[Net] Connecting WebSocket {_request.Config.WebSocketUrl} auth={_request.Config.AuthMode} v{_request.Config.ProtocolVersion}");

            try
            {
                _webSocket = new UnityWebSocketTransport(new Uri(_request.Config.WebSocketUrl), handshake);
            }
            catch (Exception e)
            {
                Fail(DisconnectReason.SocketError, "Lỗi khởi tạo WebSocket: " + e.Message);
            }
        }

        private void FallbackToWebSocket(DisconnectReason reason, string detail)
        {
            if (State == NetworkState.Joined || _request.Config?.Transport != "Auto" || _selectedTransport != "Udp")
            {
                Fail(reason, detail);
                return;
            }

            _fallbackReason = reason.ToString();
            // Không ghi log secret AttemptId
            _log.Info($"[Net] UDP không kết nối được ({reason}: {detail}), fallback sang WSS.");
            _generation++;
            StopUdpTransport(explicitQuit: false);
            _authSent = false;
            _transportConnected = false;
            _selectedTransport = "Wss";
            StartWebSocketTransport();
        }

        private void TickWebSocket()
        {
            if (_webSocket == null) return;

            if (_webSocket.Connected && _wsConnectedAt == 0)
            {
                _wsConnectedAt = _clock();
                _transportConnected = true;
                _log.Info("[Net] WebSocket connected, chờ JoinAccepted.");
            }

            var now = _clock();

            var dispatched = 0;
            while (dispatched < 256 && _webSocket.TryReceive(out var packet))
            {
                dispatched++;
                ProcessWebSocketPacket(packet);
                if (State == NetworkState.Failed || State == NetworkState.Disconnected) return;
            }

            if (_webSocket.Error != null)
            {
                var err = _webSocket.Error;
                DisconnectReason reason;
                if (err == "WssConnectFailed") reason = DisconnectReason.ConnectTimeout;
                else if (err == "MalformedMessage" || err == "PayloadTooLarge") reason = DisconnectReason.ProtocolError;
                else if (State == NetworkState.Joined) reason = DisconnectReason.ConnectionLost;
                else reason = DisconnectReason.ConnectTimeout;

                Fail(reason, err);
                return;
            }

            if (State == NetworkState.Connecting || State == NetworkState.Authenticating)
            {
                if (_wsConnectedAt == 0)
                {
                    if (ElapsedMs(_connectStarted, now) >= 10000)
                    {
                        Fail(DisconnectReason.ConnectTimeout, "WSS connect timeout");
                        return;
                    }
                }
                else
                {
                    if (ElapsedMs(_wsConnectedAt, now) >= 5000)
                    {
                        Fail(DisconnectReason.JoinTimeout, "WSS join timeout");
                        return;
                    }
                }
                return;
            }

            if (State != NetworkState.Joined) return;

            if (_lastPong != 0 && ElapsedMs(_lastPong, now) >= 5000)
            {
                Fail(DisconnectReason.ConnectionLost, "WSS pong timeout");
                return;
            }

            if (_webSocket.Connected && now >= _nextPing)
            {
                NetworkProtocol.Write(_controlWriter, NetworkProtocol.Ping, ++_pingSequence);
                if (!_webSocket.Send(_controlWriter.CopyData()))
                {
                    Fail(DisconnectReason.ConnectionLost, "WSS send failed");
                    return;
                }
                _rtt.OnPingSent(_pingSequence, now);
                _nextPing += Stopwatch.Frequency * PingIntervalMs / 1000;
                if (_nextPing < now) _nextPing = now + Stopwatch.Frequency * PingIntervalMs / 1000;
            }
            _rtt.ExpirePending(now);
        }

        private void ProcessWebSocketPacket(byte[] packet)
        {
            if (packet == null || packet.Length == 0) { _invalidPackets++; return; }
            var reader = new NetDataReader(packet);

            if (PacketRegistry.IsFrame(reader))
            {
                if (packet.Length > 4096 || State != NetworkState.Joined || !_registry.TryReadFrame(reader))
                    _invalidPackets++;
                return;
            }

            var type = reader.PeekByte();
            if (type == NetworkProtocol.AuthRequired)
            {
                HandleAuthRequired(reader);
                return;
            }
            if (type == NetworkProtocol.JoinAccepted)
            {
                HandleJoinAccepted(reader);
                return;
            }
            if (type == NetworkProtocol.JoinRejected)
            {
                var rejectedReason = NetworkProtocol.TryReadStringMessage(reader, NetworkProtocol.JoinRejected, MaxMessageBytes, MaxIdChars, out var reason)
                    ? DisconnectReasons.FromServer(reason)
                    : DisconnectReason.ProtocolError;
                Fail(rejectedReason, reason);
                return;
            }
            if (type == NetworkProtocol.Pong && State == NetworkState.Joined
                && NetworkProtocol.TryRead(reader, NetworkProtocol.Pong, out var sequence))
            {
                _lastPong = _clock();
                _rtt.OnPongReceived(sequence, _lastPong);
                return;
            }

            _invalidPackets++;
        }

        private void OnPeerConnected(NetPeer peer)
        {
            if (_closing || _manager == null || _generation != _udpGeneration || peer != _peer || _selectedTransport != "Udp") return;
            _transportConnected = true;
            _log.Info("[Net] UDP connected, chờ JoinAccepted.");
        }

        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            if (_closing || _manager == null || _generation != _udpGeneration || peer != _peer || _selectedTransport != "Udp") return;
            var data = info.AdditionalData;
            if (data != null && data.AvailableBytes > 0
                && NetworkProtocol.TryReadStringMessage(data, NetworkProtocol.JoinRejected, MaxMessageBytes, MaxIdChars, out var text))
            {
                Fail(DisconnectReasons.FromServer(text), text);
                return;
            }

            if (State != NetworkState.Joined && _request.Config?.Transport == "Auto")
            {
                FallbackToWebSocket(DisconnectReason.ConnectTimeout, info.Reason.ToString());
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
            if (_closing || _manager == null || _generation != _udpGeneration || _selectedTransport != "Udp") return;
            if (State != NetworkState.Joined && _request.Config?.Transport == "Auto")
            {
                FallbackToWebSocket(DisconnectReason.SocketError, error.ToString());
                return;
            }
            Fail(DisconnectReason.SocketError, error.ToString());
        }

        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod method)
        {
            if (_closing || _manager == null || _generation != _udpGeneration || peer != _peer || reader.AvailableBytes == 0 || _selectedTransport != "Udp")
            {
                _invalidPackets++;
                return;
            }
            if (PacketRegistry.IsFrame(reader))
            {
                if (reader.AvailableBytes > 4096 || (method == DeliveryMethod.ReliableOrdered && channel != GameplayProtocol.GameplayChannel) || State != NetworkState.Joined || !_registry.TryReadFrame(reader)) _invalidPackets++;
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
                _rtt.OnPongReceived(sequence, _clock());
                return;
            }
            _invalidPackets++;
        }

        private void HandleAuthRequired(NetDataReader reader)
        {
            if (reader.AvailableBytes != 1 || _request.Config.AuthMode != JoinAuthMode.Jwt || State == NetworkState.Joined
                || _authSent || string.IsNullOrWhiteSpace(_request.AccessToken))
            {
                Fail(DisconnectReason.ProtocolError, "UnexpectedAuthRequest");
                return;
            }
            var authPacket = NetworkProtocol.WriteAuth(_request.AccessToken);
            if (_selectedTransport == "Wss" && _webSocket != null)
            {
                _webSocket.Send(authPacket.CopyData());
            }
            else if (_peer != null)
            {
                _peer.Send(authPacket, GameplayProtocol.ControlChannel, DeliveryMethod.ReliableOrdered);
            }
            _authSent = true;
            SetState(NetworkState.Authenticating, DisconnectReason.None);
        }

        private void HandleJoinAccepted(NetDataReader reader)
        {
            if (State == NetworkState.Joined
                || !NetworkProtocol.TryReadStringMessage(reader, NetworkProtocol.JoinAccepted, MaxMessageBytes, MaxIdChars, out var id))
            {
                Fail(DisconnectReason.ProtocolError, "MalformedJoinAccepted");
                return;
            }
            PlayerId = id;
            _lastPong = _clock();
            _nextPing = _clock();
            var joinMs = ElapsedMs(_connectStarted, _nextPing);
            SetState(NetworkState.Joined, DisconnectReason.None);
            _log.Info(string.Format(CultureInfo.InvariantCulture, "[Net] JoinAccepted endpoint={0} transport={1} joinMs={2:F0}", Endpoint, _selectedTransport, joinMs));
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
            if (State == NetworkState.Failed && _manager == null && _webSocket == null) return;
            var wasJoined = State == NetworkState.Joined;
            LogSummary(reason, detail);
            // Lỗi ngoài ý muốn: abort ngay lập tức, không gửi ExplicitQuit
            StopTransport(explicitQuit: false, gracefulWebSocket: false);
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

        private void StopTransport(bool explicitQuit = false, bool gracefulWebSocket = false)
        {
            _closing = true;
            _generation++;
            try
            {
                StopUdpTransport(explicitQuit);
                StopWebSocketTransport(gracefulWebSocket);
            }
            finally
            {
                _closing = false;
            }
        }

        private void StopUdpTransport(bool explicitQuit = false)
        {
            if (_manager == null) return;
            var mgr = _manager;
            var peer = _peer;
            _manager = null;
            _peer = null;
            _udpConnectData = null;
            try
            {
                if (explicitQuit && peer != null) peer.Disconnect(new byte[] { NetworkProtocol.ExplicitQuit });
                mgr.Stop();
            }
            catch { }
        }

        private void StopWebSocketTransport(bool graceful = false)
        {
            if (_webSocket == null) return;
            var ws = _webSocket;
            _webSocket = null;
            try
            {
                if (graceful) ws.CloseGracefully();
                else ws.Abort();
            }
            catch { }
        }

        private void ResetSession()
        {
            _rtt.Reset();
            _pingSequence = 0;
            _invalidPackets = 0;
            _transportConnected = _authSent = false;
            PlayerId = null;
            LastReason = DisconnectReason.None;
            _selectedTransport = "Udp";
            _fallbackReason = null;
            _wsConnectedAt = 0;
            _lastPong = 0;
            _dnsResult = null;
        }

        private void LogSummary(DisconnectReason reason, string detail)
        {
            _log.Info(string.Format(CultureInfo.InvariantCulture,
                "[client-summary] endpoint={0} transport={1} fallbackReason={2} joined={3} reason={4}{5} sent={6} pongs={7} timedOut={8} rttP50Ms={9:F2} rttP95Ms={10:F2} rttP99Ms={11:F2} rttAvgMs={12:F2}",
                Endpoint, _selectedTransport, _fallbackReason ?? "none", State == NetworkState.Joined, reason,
                detail == null ? "" : "(" + detail + ")",
                _rtt.Sent, _rtt.Received, _rtt.Lost, _rtt.Percentile(0.50), _rtt.Percentile(0.95), _rtt.Percentile(0.99), _rtt.AverageMs));
        }

        private static double ElapsedMs(long from, long to) => (to - from) * 1000.0 / Stopwatch.Frequency;

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
