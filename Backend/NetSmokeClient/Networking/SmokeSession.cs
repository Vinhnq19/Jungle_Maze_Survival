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
    private readonly long _started = Stopwatch.GetTimestamp();
    private NetPeer? _peer;
    private long _nextPing;
    private int _sequence, _sent, _lost;
    private bool _closing;
    private readonly string _roomCode;
    private readonly string _displayName;
    private readonly string? _accessToken;
    private bool _authSent;
    public string? PlayerId { get; private set; }
    public bool Joined => PlayerId is not null;
    public int Number { get; }
    public bool ConnectedOnce { get; private set; }
    public bool Finished { get; private set; }
    public bool Failed { get; private set; }
    public int Pongs { get; private set; }

    public SmokeSession(int number, ClientOptions options, string roomCode, string displayName, string? accessToken = null)
    {
        Number = number;
        _options = options;
        _roomCode = roomCode;
        _displayName = displayName;
        _accessToken = accessToken;
        var listener = new EventBasedNetListener();
        _manager = new NetManager(listener)
        {
            AutoRecycle = true,
            IPv6Enabled = false,
            ReconnectDelay = 500,
            MaxConnectAttempts = (int)Math.Ceiling(options.ConnectTimeoutMs / 500.0),
            DisconnectTimeout = 5000
        };
        listener.PeerConnectedEvent += peer =>
        {
            _peer = peer;
            ConnectedOnce = true;
            Console.WriteLine($"[client {Number}] transportConnected=true awaitingJoin=true");
        };
        listener.PeerDisconnectedEvent += (_, info) =>
        {
            if (_closing) return;
            var reason = NetworkProtocol.TryReadStringMessage(info.AdditionalData, NetworkProtocol.JoinRejected, 256, 64, out var text)
                ? text : info.Reason.ToString();
            Fail(reason);
        };
        listener.NetworkReceiveEvent += (_, reader, channel, method) =>
        {
            if (channel != 0 || reader.AvailableBytes == 0) return;
            var type = reader.PeekByte();
            if (method == DeliveryMethod.ReliableOrdered && type == NetworkProtocol.AuthRequired)
            {
                if (reader.AvailableBytes != 1 || _options.JoinMode != JoinAuthMode.Jwt || Joined || _authSent
                    || string.IsNullOrWhiteSpace(_accessToken)) { Fail("UnexpectedAuthRequest"); return; }
                _peer!.Send(NetworkProtocol.WriteAuth(_accessToken), DeliveryMethod.ReliableOrdered);
                _authSent = true;
                return;
            }
            if (method == DeliveryMethod.ReliableOrdered && type == NetworkProtocol.JoinAccepted)
            {
                if (Joined || !NetworkProtocol.TryReadStringMessage(reader, NetworkProtocol.JoinAccepted, 256, 64, out var id))
                    { Fail("MalformedJoinAccepted"); return; }
                PlayerId = id;
                _nextPing = Stopwatch.GetTimestamp();
                Console.WriteLine($"[client {Number}] JoinAccepted playerId={PlayerId}");
                return;
            }
            if (method == DeliveryMethod.ReliableOrdered && type == NetworkProtocol.JoinRejected)
            {
                Fail(NetworkProtocol.TryReadStringMessage(reader, NetworkProtocol.JoinRejected, 256, 64, out var reason)
                    ? reason : "MalformedJoinRejected");
                return;
            }
            if (channel != 0 || method != DeliveryMethod.Unreliable
                || !Joined || !NetworkProtocol.TryRead(reader, NetworkProtocol.Pong, out var sequence)
                || !_sentAt.Remove(sequence, out var timestamp)) return;
            // Chỉ tính RTT của sequence đã gửi và chưa timeout; bỏ pong trùng/đến muộn.
            var rtt = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
            Pongs++;
            _rttSamples.Enqueue(rtt);
            if (_rttSamples.Count > 1024) _rttSamples.Dequeue();
            Console.WriteLine(FormattableString.Invariant($"[client {Number}] Pong sequence={sequence} rttMs={rtt:F2}"));
        };
        listener.NetworkErrorEvent += (_, error) => Fail($"SocketError:{error}");
    }

    public void Start()
    {
        if (!_manager.Start()) throw new InvalidOperationException("Không mở được socket client.");
        _peer = _manager.Connect(_options.GameServerHost, _options.GameServerPort,
            NetworkProtocol.WriteConnect(_options.JoinMode, _roomCode, _displayName));
    }

    public void Update()
    {
        if (Finished) return;
        _manager.PollEvents();
        if (Finished) return;
        var now = Stopwatch.GetTimestamp();
        if (!Joined && Stopwatch.GetElapsedTime(_started).TotalMilliseconds >= _options.ConnectTimeoutMs)
        {
            Fail(ConnectedOnce ? "JoinTimeout" : "ConnectTimeout");
            return;
        }
        if (Joined && _peer?.ConnectionState == ConnectionState.Connected && now >= _nextPing)
        {
            NetworkProtocol.Write(_writer, NetworkProtocol.Ping, ++_sequence);
            _sentAt[_sequence] = now;
            _peer.Send(_writer, DeliveryMethod.Unreliable);
            _sent++;
            _nextPing = now + Stopwatch.Frequency;
        }
        foreach (var expired in _sentAt.Where(pair =>
            Stopwatch.GetElapsedTime(pair.Value).TotalMilliseconds >= PongTimeoutMs).Select(pair => pair.Key).ToArray())
        {
            _sentAt.Remove(expired);
            _lost++;
            Console.WriteLine($"[client {Number}] PongTimeout sequence={expired}");
        }
    }

    private void Fail(string reason)
    {
        Failed = true;
        Finished = true;
        Console.Error.WriteLine($"[client {Number}] disconnectedOrRejected reason={reason}");
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
        _sentAt.Clear();
    }
}
