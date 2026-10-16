using System.Diagnostics;
using JungleBackend.Protocol;
using LiteNetLib;
using LiteNetLib.Utils;
using NetSmokeClient.Configuration;

namespace NetSmokeClient.Networking;

/// <summary>Mỗi session có socket riêng, tương đương một client thật khi test giới hạn 10 người.</summary>
public sealed class SmokeSession : IDisposable
{
    private const int PongTimeoutMs = 5000;
    private readonly ClientOptions _options;
    private readonly NetManager _manager;
    private readonly NetDataWriter _writer = new();
    private readonly Dictionary<int, long> _sentAt = new();
    private readonly Queue<double> _rttSamples = new();
    private NetPeer? _peer;
    private long _nextPing;
    private int _sequence, _sent, _lost;
    private bool _closing;
    private readonly string _roomCode;
    private readonly string _displayName;
    private readonly string? _accessToken;
    private bool _authSent;
    private readonly long _pingInterval;
    private readonly bool _quiet;
    private bool _sendEnabled = true;
    private readonly Guid _attemptId = new(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
    private WebSocketClientTransport? _webSocket;
    private long _routeStarted, _wsConnectedAt, _lastPong;
    private string _selectedTransport = "Udp";
    private string? _fallbackReason;
    public string? FailureReason { get; private set; }
    public string? PlayerId { get; private set; }
    public bool Joined => PlayerId is not null;
    public int Number { get; }
    public string? RoomLabel { get; }
    public bool ConnectedOnce { get; private set; }
    public bool Finished { get; private set; }
    public bool Failed { get; private set; }
    public int Pongs { get; private set; }

    public SmokeSession(int number, ClientOptions options, string roomCode, string displayName, string? accessToken = null, int pingRate = 1, bool quiet = false, string? roomLabel = null)
    {
        Number = number;
        RoomLabel = roomLabel;
        _options = options;
        _roomCode = roomCode;
        _displayName = displayName;
        _accessToken = accessToken;
        _pingInterval = Stopwatch.Frequency / pingRate;
        _quiet = quiet;
        var listener = new EventBasedNetListener();
        _manager = new NetManager(listener)
        {
            AutoRecycle = true,
            EnableStatistics = true,
            IPv6Enabled = false,
            ReconnectDelay = 500,
            MaxConnectAttempts = (int)Math.Ceiling(options.ConnectTimeoutMs / 500.0),
            DisconnectTimeout = 5000
        };
        listener.PeerConnectedEvent += peer =>
        {
            _peer = peer;
            ConnectedOnce = true;
            if (!_quiet) Console.WriteLine($"[client {Number}] transportConnected=true awaitingJoin=true");
        };
        listener.PeerDisconnectedEvent += (_, info) =>
        {
            if (_closing) return;
            var reason = NetworkProtocol.TryReadStringMessage(info.AdditionalData, NetworkProtocol.JoinRejected, 256, 64, out var text)
                ? text : info.Reason.ToString();
            Fail(reason);
        };
        listener.NetworkReceiveEvent += (_, reader, channel, method) =>
            Receive(reader.GetRemainingBytes(), method == DeliveryMethod.ReliableOrdered,
                method == DeliveryMethod.Unreliable, channel == 0);
        listener.NetworkErrorEvent += (_, error) => { if (!_closing) Fail($"SocketError:{error}"); };
    }

    private void Receive(byte[] packet, bool reliable, bool reliablePong, bool validChannel = true)
    {
        var reader = new NetDataReader(packet);

            if (!validChannel || reader.AvailableBytes == 0) return;
            var type = reader.PeekByte();
            if (reliable && type == NetworkProtocol.AuthRequired)
            {
                if (reader.AvailableBytes != 1 || _options.JoinMode != JoinAuthMode.Jwt || Joined || _authSent
                    || string.IsNullOrWhiteSpace(_accessToken)) { Fail("UnexpectedAuthRequest"); return; }
                Send(NetworkProtocol.WriteAuth(_accessToken).CopyData(), true);
                _authSent = true;
                return;
            }
            if (reliable && type == NetworkProtocol.JoinAccepted)
            {
                if (Joined || !NetworkProtocol.TryReadStringMessage(reader, NetworkProtocol.JoinAccepted, 256, 64, out var id))
                    { Fail("MalformedJoinAccepted"); return; }
                PlayerId = id;
                _lastPong = Stopwatch.GetTimestamp();
                _nextPing = Stopwatch.GetTimestamp();
                if (!_quiet) Console.WriteLine($"[client {Number}] JoinAccepted playerId={PlayerId}");
                return;
            }
            if (reliable && type == NetworkProtocol.JoinRejected)
            {
                Fail(NetworkProtocol.TryReadStringMessage(reader, NetworkProtocol.JoinRejected, 256, 64, out var reason)
                    ? reason : "MalformedJoinRejected");
                return;
            }
            if (!validChannel || !reliablePong
                || !Joined || !NetworkProtocol.TryRead(reader, NetworkProtocol.Pong, out var sequence)
                || !_sentAt.Remove(sequence, out var timestamp)) return;
            // Chỉ tính RTT của sequence đã gửi và chưa timeout; bỏ pong trùng/đến muộn.
            var rtt = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
            Pongs++;
            _lastPong = Stopwatch.GetTimestamp();
            _rttSamples.Enqueue(rtt);
            if (_rttSamples.Count > 16384) _rttSamples.Dequeue();
            if (!_quiet) Console.WriteLine(FormattableString.Invariant($"[client {Number}] Pong sequence={sequence} rttMs={rtt:F2}"));
    }

    public void Start()
    {
        _routeStarted = Stopwatch.GetTimestamp();
        if (_options.Transport == "Wss") { StartWebSocket(); return; }
        if (!_manager.Start()) throw new InvalidOperationException("Không mở được socket client.");
        _peer = _manager.Connect(_options.GameServerHost, _options.GameServerPort,
            NetworkProtocol.WriteConnect(_options.JoinMode, _roomCode, _displayName, (ushort)_options.ProtocolVersion, _attemptId));
    }

    private void StartWebSocket()
    {
        _selectedTransport = "Wss"; _routeStarted = Stopwatch.GetTimestamp();
        _webSocket = new(new Uri(_options.WebSocketUrl!),
            NetworkProtocol.WriteConnect(_options.JoinMode, _roomCode, _displayName, (ushort)_options.ProtocolVersion, _attemptId).CopyData(),_options.WebSocketConnectIp);
    }
    private void Send(byte[] packet, bool reliable)
    {
        if (_webSocket is not null)
        { if (!_webSocket.Send(packet)) Fail("SlowConsumer"); }
        else _peer!.Send(packet, reliable ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable);
    }

    public void Update()
    {
        if (Finished) return;
        if (_webSocket is null) _manager.PollEvents();
        else
        {
            if (_webSocket.Connected && _wsConnectedAt == 0) { ConnectedOnce = true; _wsConnectedAt = Stopwatch.GetTimestamp(); }
            while (_webSocket.TryReceive(out var packet) && !Finished) Receive(packet, true, true);
            if (!Finished && _webSocket.Error is { } error) Fail(error);
        }
        if (Finished) return;
        var now = Stopwatch.GetTimestamp();
        var joinExpired = _webSocket is null
            ? Stopwatch.GetElapsedTime(_routeStarted).TotalMilliseconds >= (_options.Transport == "Auto" ? 5000 : _options.ConnectTimeoutMs)
            : _wsConnectedAt == 0 ? Stopwatch.GetElapsedTime(_routeStarted).TotalSeconds >= 10
            : Stopwatch.GetElapsedTime(_wsConnectedAt).TotalSeconds >= 5;
        if (!Joined && joinExpired)
        {
            Fail(ConnectedOnce ? "JoinTimeout" : "ConnectTimeout");
            return;
        }
        if (Joined && _selectedTransport == "Wss" && _lastPong != 0 && Stopwatch.GetElapsedTime(_lastPong).TotalSeconds >= 5)
        { Fail("ConnectionTimeout"); return; }
        if (_sendEnabled && Joined && (_webSocket?.Connected == true || _peer?.ConnectionState == ConnectionState.Connected) && now >= _nextPing)
        {
            NetworkProtocol.Write(_writer, NetworkProtocol.Ping, ++_sequence);
            _sentAt[_sequence] = now;
            Send(_writer.CopyData(), false);
            _sent++;
            // Absolute schedule avoids accumulating the sleep/poll delay at 30Hz.
            // Never burst to hide a stalled generator; its actual send rate is reported.
            _nextPing += _pingInterval;
            if (_nextPing < now - _pingInterval) _nextPing = now + _pingInterval;
        }
        foreach (var expired in _sentAt.Where(pair =>
            Stopwatch.GetElapsedTime(pair.Value).TotalMilliseconds >= PongTimeoutMs).Select(pair => pair.Key).ToArray())
        {
            _sentAt.Remove(expired);
            _lost++;
            if (!_quiet) Console.WriteLine($"[client {Number}] PongTimeout sequence={expired}");
        }
    }

    private void Fail(string reason)
    {
        if (!Joined && _options.Transport == "Auto" && _selectedTransport == "Udp"
            && (reason is "ConnectTimeout" or "JoinTimeout" or "ConnectionFailed" or "Timeout" || reason.StartsWith("SocketError:")))
        {
            _fallbackReason = reason;
            _closing = true; _manager.Stop(); _closing = false;
            StartWebSocket(); return;
        }
        Failed = true;
        FailureReason = reason;
        Finished = true;
        Console.Error.WriteLine($"[client {Number}] disconnectedOrRejected reason={reason}");
    }

    // A synchronized measurement boundary across all clients, excluding warm-up traffic.
    public void BeginMeasurement()
    {
        _sentAt.Clear(); _rttSamples.Clear(); _sent = _lost = Pongs = 0;
        _nextPing = Stopwatch.GetTimestamp();
    }
    public void StopSending() => _sendEnabled = false;
    public int Outstanding => _sentAt.Count;
    public object Report(double measuredSeconds)
    {
        var sorted = _rttSamples.Order().ToArray();
        double? Percentile(double p) => sorted.Length == 0 ? null : sorted[(int)Math.Ceiling(sorted.Length * p) - 1];
        return new { client = Number, room = RoomLabel, connected = ConnectedOnce, joined = Joined,
            playerId = PlayerId, localUdpPort = _manager.LocalPort,
            transport = _selectedTransport, requestedTransport = _options.Transport, fallbackReason = _fallbackReason,
            webSocketConnectIp = _options.WebSocketConnectIp,
            transportPacketsSent = _manager.Statistics.PacketsSent,
            transportPacketsReceived = _manager.Statistics.PacketsReceived,
            rejected = Failed && !Joined, disconnected = Failed && Joined,
            failed = Failed, reason = FailureReason, sent = _sent, pongs = Pongs, timedOut = _lost,
            outstanding = _sentAt.Count, actualHz = measuredSeconds > 0 ? _sent / measuredSeconds : 0,
            rttP50Ms = Percentile(.5), rttP95Ms = Percentile(.95), rttP99Ms = Percentile(.99), rttMs = _rttSamples.ToArray() };
    }

    public void PrintSummary()
    {
        var sorted = _rttSamples.Order().ToArray();
        var p95 = sorted.Length == 0 ? 0 : sorted[(int)Math.Ceiling(sorted.Length * .95) - 1];
        Console.WriteLine(FormattableString.Invariant(
            $"[client-summary] client={Number} connected={ConnectedOnce} joined={Joined} sent={_sent} pongs={Pongs} timedOut={_lost} outstanding={_sentAt.Count} rttP95Ms={p95:F2} failed={Failed}"));
    }

    public void Dispose()
    {
        _closing = true;
        // Stop gửi disconnect cho server; không tự reconnect để che giấu lỗi mạng/capacity.
        _manager.Stop();
        _webSocket?.Dispose();
        _sentAt.Clear();
    }
}
