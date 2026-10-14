using System.Diagnostics;
using System.Net;
using GameServer.Configuration;
using GameServer.Diagnostics;
using JungleBackend.Protocol;
using LiteNetLib;
using LiteNetLib.Utils;
using GameServer.Rooms;

namespace GameServer.Networking;

/// <summary>UDP/handshake trên thread PollEvents; slot bao gồm cả pending và active.</summary>
public sealed class NetworkHost : IDisposable
{
    private sealed class Session(NetPeer peer, JoinRequest request, long deadline, string? playerId, RoomManager.Room room)
    {
        public NetPeer Peer { get; } = peer;
        public JoinRequest Request { get; } = request;
        public long Deadline { get; } = deadline;
        public string? PlayerId { get; set; } = playerId;
        public RoomManager.Room Room { get; } = room;
        public bool Active => PlayerId is not null;
    }

    private readonly ServerOptions _options;
    private readonly ServerMetrics _metrics;
    private readonly HandshakeValidator _handshake;
    private readonly NetManager _manager;
    private readonly Dictionary<int, Session> _sessions = new();
    private readonly Dictionary<string, int> _users = new(StringComparer.Ordinal);
    private readonly NetDataWriter _pong = new();
    public int ReservedPeers => _sessions.Count;
    public int ActivePeers => _sessions.Values.Count(session => session.Active);
    public int PendingPeers => ReservedPeers - ActivePeers;
    public RoomManager Rooms { get; }
    public RoomCommands Commands { get; } = new();

    public NetworkHost(ServerOptions options, ServerMetrics metrics)
    {
        _options = options;
        Rooms = new RoomManager(options);
        _metrics = metrics;
        _handshake = new HandshakeValidator(options);
        var listener = new EventBasedNetListener();
        _manager = new NetManager(listener)
        {
            // Library recycle reader sau callback; không Recycle() lần hai.
            AutoRecycle = true, IPv6Enabled = false, DisconnectTimeout = 5000,
            UnconnectedMessagesEnabled = false
        };
        listener.ConnectionRequestEvent += OnConnectionRequest;
        listener.PeerConnectedEvent += OnPeerConnected;
        listener.PeerDisconnectedEvent += (peer, info) =>
        {
            ReleaseSlot(peer.Id);
            Console.WriteLine($"[disconnected] peer={peer.Id} reason={info.Reason} serverSlots={ReservedPeers}/{(_options.Rooms ?? new()).MaxTotalPlayers}");
        };
        listener.NetworkReceiveEvent += OnReceive;
        listener.NetworkErrorEvent += (_, error) => Console.Error.WriteLine($"[network-error] socket={error}");
    }

    public void Start()
    {
        if (!_manager.Start(_options.BindAddress, IPAddress.IPv6Any, _options.Port))
            throw new InvalidOperationException("Không mở được UDP socket; kiểm tra IP bind/cổng đã bị chiếm.");
        Console.WriteLine($"[ready] udp={_options.BindAddress}:{_options.Port} maxPlayers={_options.MaxPlayers} tickRate={_options.TickRate} authMode={_options.AuthMode} protocol={NetworkProtocol.Version}");
    }

    private void OnConnectionRequest(ConnectionRequest request)
    {
        if (!NetworkProtocol.TryReadConnect(request.Data, out var join, out var reason))
        {
            RejectRequest(request, reason);
            return;
        }
        var invalid = _handshake.ValidateRequest(join!);
        if (invalid is not null) { RejectRequest(request, invalid); return; }
        // Tuần tự trên PollEvents: giữ slot ngay khi Accept, trước callback Connected.
        // Tra đúng phòng và giữ slot trước Connected: cả pending/active tính vào phòng và server.
        var room = Rooms.Find(join!.RoomCode);
        if (room is null) { RejectRequest(request, "InvalidRoomCode"); return; }
        var capacityError = Rooms.CapacityError(room);
        if (capacityError is not null) { RejectRequest(request, capacityError); return; }
        var peer = request.Accept();
        if (peer is null) return;
        var playerId = _options.Mode == JoinAuthMode.RoomCode ? Guid.NewGuid().ToString() : null;
        _sessions[peer.Id] = new Session(peer, join!, Stopwatch.GetTimestamp()
            + (long)(_options.AuthTimeoutMs / 1000.0 * Stopwatch.Frequency), playerId, room);
        Rooms.Reserve(room, peer.Id, playerId is not null);
    }

    private void OnPeerConnected(NetPeer peer)
    {
        if (!_sessions.TryGetValue(peer.Id, out var session)) return;
        if (session.Active) AcceptSession(session);
        else
        {
            var writer = new NetDataWriter();
            writer.Put(NetworkProtocol.AuthRequired);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
            Console.WriteLine($"[pending] peer={peer.Id} roomId={session.Room.Id} roomSlots={session.Room.Peers.Count}/{session.Room.MaxPlayers} serverSlots={ReservedPeers}/{(_options.Rooms ?? new()).MaxTotalPlayers}");
        }
    }

    private void OnReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod method)
    {
        if (!_sessions.TryGetValue(peer.Id, out var session)) return;
        if (!session.Active)
        {
            // Pending chỉ nhận Auth reliable. Input/Ping sớm bị từ chối, không chạy gameplay.
            if (Stopwatch.GetTimestamp() >= session.Deadline) { RejectPeer(session, "AuthTimeout"); return; }
            if (reader.AvailableBytes > NetworkProtocol.MaxAuthBytes) { RejectPeer(session, "PayloadTooLarge"); return; }
            if (channel != 0 || method != DeliveryMethod.ReliableOrdered
                || !NetworkProtocol.TryReadStringMessage(reader, NetworkProtocol.Auth,
                    NetworkProtocol.MaxAuthBytes, NetworkProtocol.MaxTokenChars, out var token))
            {
                RejectPeer(session, "AuthenticationRequired");
                return;
            }
            if (!_handshake.TryValidateToken(token, out var playerId)) { RejectPeer(session, "InvalidToken"); return; }
            // Không await/call API ở đây: validation và reserve user xảy ra trong cùng callback.
            if (_users.ContainsKey(playerId)) { RejectPeer(session, "DuplicateSession"); return; }
            _users[playerId] = peer.Id;
            session.PlayerId = playerId;
            Rooms.Activate(session.Room, peer.Id);
            AcceptSession(session);
            return;
        }
        if (channel != 0 || method != DeliveryMethod.Unreliable
            || !NetworkProtocol.TryRead(reader, NetworkProtocol.Ping, out var sequence))
        {
            // Active packet lỗi chỉ được đếm/bỏ; reader luôn recycle, server tiếp tục chạy.
            _metrics.RecordInvalidPacket();
            return;
        }
        NetworkProtocol.Write(_pong, NetworkProtocol.Pong, sequence);
        peer.Send(_pong, DeliveryMethod.Unreliable);
        _metrics.RecordPing();
    }

    private void AcceptSession(Session session)
    {
        session.Peer.Send(NetworkProtocol.StringMessage(NetworkProtocol.JoinAccepted, session.PlayerId!), DeliveryMethod.ReliableOrdered);
        Console.WriteLine($"[joined] peer={session.Peer.Id} playerId={session.PlayerId} roomId={session.Room.Id} active={ActivePeers} pending={PendingPeers} roomSlots={session.Room.Peers.Count}/{session.Room.MaxPlayers} serverSlots={ReservedPeers}/{(_options.Rooms ?? new()).MaxTotalPlayers}");
    }

    private void RejectRequest(ConnectionRequest request, string reason)
    {
        request.Reject(NetworkProtocol.StringMessage(NetworkProtocol.JoinRejected, reason));
        LogReject(reason);
    }

    private void RejectPeer(Session session, string reason)
    {
        var writer = NetworkProtocol.StringMessage(NetworkProtocol.JoinRejected, reason);
        session.Peer.Send(writer, DeliveryMethod.ReliableOrdered);
        // Kèm reason trong disconnect packet: client vẫn đọc được nếu close tới trước reliable reply.
        session.Peer.Disconnect(writer);
        ReleaseSlot(session.Peer.Id);
        LogReject(reason);
    }

    private void ReleaseSlot(int peerId)
    {
        if (!_sessions.Remove(peerId, out var session)) return;
        Rooms.Release(session.Room, peerId);
        if (session.PlayerId is not null && _users.TryGetValue(session.PlayerId, out var owner) && owner == peerId)
            _users.Remove(session.PlayerId);
    }

    private void LogReject(string reason)
    {
        _metrics.RecordRejection();
        Console.WriteLine($"[rejected] reason={reason} serverSlots={ReservedPeers}/{(_options.Rooms ?? new()).MaxTotalPlayers}");
    }

    public void PollEvents()
    {
        Commands.Drain(Rooms);
        Rooms.ExpireEmpty();
        _manager.PollEvents();
        var now = Stopwatch.GetTimestamp();
        foreach (var session in _sessions.Values.Where(session => !session.Active && now >= session.Deadline).ToArray())
            RejectPeer(session, "AuthTimeout");
    }

    public void Dispose()
    {
        _manager.Stop();
        foreach (var session in _sessions.Values) Rooms.Release(session.Room, session.Peer.Id);
        _sessions.Clear();
        _users.Clear();
        Console.WriteLine("[stopped] UDP socket đã đóng.");
    }
}
