using System.Diagnostics;
using GameServer.Configuration;
using GameServer.Diagnostics;
using GameServer.Rooms;
using JungleBackend.Protocol;
using LiteNetLib.Utils;

namespace GameServer.Networking;

/// <summary>Single writer: toàn bộ hàm trong lớp này chạy trên vòng PollEvents/tick.
/// Socket async chỉ enqueue công việc; không khóa hoặc await I/O trong lớp này.</summary>
public sealed class SessionHub
{
    private sealed class Session(int id, IGamePeer peer, JoinRequest request, RoomManager.Room room, string? playerId)
    {
        public int Id = id;
        public IGamePeer Peer = peer;
        public JoinRequest Request = request;
        public RoomManager.Room Room = room;
        public string? PlayerId = playerId;
        public long LastMessage = Stopwatch.GetTimestamp();
        public bool ApplicationSeen;
    }
    private sealed class Attempt(JoinRequest request)
    {
        public JoinRequest Request = request;
        public Session? Current;
        public string? PlayerId;
        public bool WssChosen;
        public bool Committed;
        public long RetiredAt;
    }
    private readonly ServerOptions _options;
    private readonly ServerMetrics _metrics;
    private readonly HandshakeValidator _handshake;
    private readonly Dictionary<IGamePeer, Session> _sessions = new();
    private readonly Dictionary<string, Session> _users = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, Attempt> _attempts = new();
    private int _nextId;
    public RoomManager Rooms { get; }
    public RoomCommands Commands { get; } = new();
    public int ReservedPeers => _sessions.Count;
    public int ActivePeers => _sessions.Values.Count(s => s.PlayerId is not null);
    public int PendingPeers => ReservedPeers - ActivePeers;
    public SessionHub(ServerOptions options, ServerMetrics metrics)
    {
        _options = options; _metrics = metrics;
        _handshake = new(options); Rooms = new(options);
    }
    public string? Validate(JoinRequest request, string transport)
    {
        var invalid = _handshake.ValidateRequest(request);
        if (invalid is not null) return invalid;
        if (request.Version == NetworkProtocol.LatestVersion && request.AttemptId == Guid.Empty) return "MalformedRequest";
        if (transport == "Wss" && request.Version != NetworkProtocol.LatestVersion) return "UnsupportedVersion";
        var room = Rooms.Find(request.RoomCode);
        if (room is null) return "InvalidRoomCode";
        if (request.AttemptId != Guid.Empty && _attempts.TryGetValue(request.AttemptId, out var previous))
        {
            // Unpredictable nonce binds ONLY one initial connection attempt, not account ownership.
            // A retired UDP attempt can finish via WSS; an active application cannot be taken over.
            if (previous.Request.RoomCode != request.RoomCode || previous.Request.DisplayName != request.DisplayName
                || previous.Request.Mode != request.Mode || previous.Committed || previous.WssChosen || transport != "Wss")
                return "AttemptUsed";
            if (previous.Current is not null) return null; // Transfer the same slot, even if room is full.
        }
        if (request.AttemptId != Guid.Empty && !_attempts.ContainsKey(request.AttemptId) && _attempts.Count >= 4096)
            return "ServerBusy";
        return Rooms.CapacityError(room);
    }
    public bool Join(JoinRequest request, IGamePeer peer)
    {
        var invalid = Validate(request, peer.Transport);
        if (invalid is not null) { Reject(peer, invalid); return false; }
        var room = Rooms.Find(request.RoomCode)!;
        Attempt? attempt = null;
        if (request.AttemptId != Guid.Empty)
        {
            if (!_attempts.TryGetValue(request.AttemptId, out attempt))
                _attempts.Add(request.AttemptId, attempt = new(request));
            attempt.WssChosen = peer.Transport == "Wss";
        }
        if (attempt?.Current is { } old)
        {
            // Detach the old route FIRST. Its delayed disconnect/packets cannot release the new route.
            _sessions.Remove(old.Peer);
            var previousPeer = old.Peer;
            old.Peer = peer;
            old.LastMessage = Stopwatch.GetTimestamp();
            _sessions.Add(peer, old);
            previousPeer.Close("TransportSwitched");
            return true;
        }
        var playerId = attempt?.PlayerId ?? (_options.Mode == JoinAuthMode.RoomCode ? Guid.NewGuid().ToString() : null);
        var session = new Session(++_nextId, peer, request, room, playerId);
        _sessions.Add(peer, session);
        Rooms.Reserve(room, session.Id, playerId is not null);
        if (attempt is not null) { attempt.Current = session; attempt.PlayerId = playerId; }
        if (playerId is not null && _options.Mode == JoinAuthMode.Jwt) _users[playerId] = session;
        return true;
    }
    public void Connected(IGamePeer peer)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        if (session.PlayerId is not null) Accept(session);
        else Send(session, new[] { NetworkProtocol.AuthRequired }, true);
    }
    public void Receive(IGamePeer peer, byte[] packet, bool reliable, bool validChannel = true)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var reader = new NetDataReader(packet);
        if (session.PlayerId is null)
        {
            if (Stopwatch.GetElapsedTime(session.LastMessage).TotalMilliseconds >= _options.AuthTimeoutMs) { Reject(peer, "AuthTimeout"); return; }
            if (packet.Length > NetworkProtocol.MaxAuthBytes) { Reject(peer, "PayloadTooLarge"); return; }
            if (!validChannel || !reliable || !NetworkProtocol.TryReadStringMessage(reader, NetworkProtocol.Auth,
                NetworkProtocol.MaxAuthBytes, NetworkProtocol.MaxTokenChars, out var token))
            { Reject(peer, "AuthenticationRequired"); return; }
            if (!_handshake.TryValidateToken(token, out var playerId)) { Reject(peer, "InvalidToken"); return; }
            if (_users.ContainsKey(playerId)) { Reject(peer, "DuplicateSession"); return; }
            _users.Add(playerId, session); session.PlayerId = playerId;
            if (session.Request.AttemptId != Guid.Empty) _attempts[session.Request.AttemptId].PlayerId = playerId;
            Rooms.Activate(session.Room, session.Id); Accept(session); return;
        }
        if (!validChannel || peer.Transport == "Udp" && reliable || !NetworkProtocol.TryRead(reader, NetworkProtocol.Ping, out var sequence))
        { _metrics.RecordInvalidPacket(); return; }
        session.LastMessage = Stopwatch.GetTimestamp(); session.ApplicationSeen = true;
        if (session.Request.AttemptId != Guid.Empty) _attempts[session.Request.AttemptId].Committed = true;
        var pong = new NetDataWriter(); NetworkProtocol.Write(pong, NetworkProtocol.Pong, sequence);
        Send(session, pong.CopyData(), false); _metrics.RecordPing();
    }
    private void Accept(Session session)
    {
        Send(session, NetworkProtocol.StringMessage(NetworkProtocol.JoinAccepted, session.PlayerId!).CopyData(), true);
        Console.WriteLine($"[joined] playerId={session.PlayerId} roomId={session.Room.Id} transport={session.Peer.Transport} serverSlots={ReservedPeers}/{(_options.Rooms ?? new()).MaxTotalPlayers}");
    }
    private void Send(Session session, byte[] packet, bool reliable)
    {
        if (!session.Peer.TrySend(packet, reliable)) { session.Peer.Close("SlowConsumer"); Disconnect(session.Peer); }
    }
    public void Reject(IGamePeer peer, string reason)
    {
        peer.Close(reason); Disconnect(peer); RecordReject(reason);
    }
    public void RecordReject(string reason)
    {
        _metrics.RecordRejection(); Console.WriteLine($"[rejected] reason={reason} serverSlots={ReservedPeers}/{(_options.Rooms ?? new()).MaxTotalPlayers}");
    }
    public void Disconnect(IGamePeer peer)
    {
        if (!_sessions.Remove(peer, out var session)) return;
        Rooms.Release(session.Room, session.Id);
        if (session.PlayerId is not null && _users.GetValueOrDefault(session.PlayerId) == session) _users.Remove(session.PlayerId);
        if (session.Request.AttemptId != Guid.Empty)
        {
            var attempt = _attempts[session.Request.AttemptId];
            attempt.Current = null; attempt.RetiredAt = Stopwatch.GetTimestamp();
        }
    }
    public void Poll()
    {
        Commands.Drain(Rooms); Rooms.ExpireEmpty();
        var now = Stopwatch.GetTimestamp();
        foreach (var session in _sessions.Values.ToArray())
        {
            if (!session.Peer.IsOpen) { Disconnect(session.Peer); continue; }
            if (session.PlayerId is null && Stopwatch.GetElapsedTime(session.LastMessage, now).TotalMilliseconds >= _options.AuthTimeoutMs)
                Reject(session.Peer, "AuthTimeout");
            else if (session.Peer.Transport == "Wss" && Stopwatch.GetElapsedTime(session.LastMessage, now).TotalSeconds >= 5)
                Reject(session.Peer, "ConnectionTimeout");
        }
        foreach (var id in _attempts.Where(a => a.Value.Current is null && Stopwatch.GetElapsedTime(a.Value.RetiredAt, now).TotalSeconds >= 30).Select(a => a.Key).ToArray())
            _attempts.Remove(id);
    }
    public void CloseAll()
    {
        foreach (var peer in _sessions.Keys.ToArray()) { peer.Close("ServerStopping"); Disconnect(peer); }
        _attempts.Clear();
    }
}
