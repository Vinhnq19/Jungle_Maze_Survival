using System.Diagnostics;
using GameServer.Configuration;
using GameServer.Diagnostics;
using GameServer.Rooms;
using GameServer.Simulation;
using JungleBackend.Protocol;
using JungleBackend.Protocol.Gameplay;
using JungleBackend.Rooms;
using JungleBackend.Shared;
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
        public string? ProfileKey;
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
        public Guid ResumeMatchId;
    }

    private sealed record RegisteredTicket(
        string Ticket,
        Guid AttemptId,
        string RoomCode,
        string ProfileKey,
        DateTimeOffset ExpiresUtc
    );

    private readonly ServerOptions _options;
    private readonly ServerMetrics _metrics;
    private readonly HandshakeValidator _handshake;
    private readonly Dictionary<IGamePeer, Session> _sessions = new();
    private readonly Dictionary<string, Session> _users = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, Attempt> _attempts = new();
    private readonly Dictionary<string, Session> _heldSessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RegisteredTicket> _tickets = new(StringComparer.Ordinal);
    private readonly HashSet<RoomManager.Room> _hookedRooms = new();
    private readonly PacketRegistry _gameplayRegistry = new();
    private readonly PacketRegistry _frameValidator = new();
    private int _nextId;

    public RoomManager Rooms { get; }
    public RoomCommands Commands { get; } = new();
    public int ReservedPeers => _sessions.Count + _heldSessions.Count;
    public int ActivePeers => _sessions.Values.Count(s => s.PlayerId is not null);
    public int PendingTickets => _tickets.Count;
    public int PendingPeers => ReservedPeers - ActivePeers;

    public SessionHub(ServerOptions options, ServerMetrics metrics)
    {
        _options = options; _metrics = metrics;
        _handshake = new(options); Rooms = new(options);
        // First parse with inert handlers: malformed trailing bytes cannot partially mutate a match.
        _frameValidator.Subscribe<ClientHelloPacket>(_ => { });
        _frameValidator.Subscribe<LoadAckPacket>(_ => { });
        _frameValidator.Subscribe<MatchInputPacket>(_ => { });
        _frameValidator.Subscribe<PlayerLeavePacket>(_ => { });
        _frameValidator.Subscribe<ReplayAckPacket>(_ => { });
        _frameValidator.Subscribe<SnapshotAckPacket>(_ => { });
        _frameValidator.Subscribe<AttachIdentityPacket>(_ => { });
        SetupGameplaySubscriptions();
        Rooms.RoomCreated += HookRoom;
        HookRoomMatches();
    }

    private void HookRoomMatches()
    {
        foreach (var room in Rooms.AllRooms)
        {
            HookRoom(room);
        }
    }

    private void HookRoom(RoomManager.Room room)
    {
        if (!_hookedRooms.Add(room)) return;

        room.Match.SnapshotGenerated += bytes =>
        {
            List<IGamePeer>? slowPeers = null;
            foreach (var session in _sessions.Values)
            {
                if (session.Room == room && session.PlayerId is not null)
                {
                    if (!session.Peer.TrySend(bytes, false, GameplayProtocol.GameplayChannel))
                    {
                        slowPeers ??= new List<IGamePeer>();
                        slowPeers.Add(session.Peer);
                    }
                }
            }

            if (slowPeers != null)
            {
                foreach (var peer in slowPeers)
                {
                    peer.Close("SlowConsumer");
                    Disconnect(peer);
                }
            }
        };

        room.Match.ReliableBroadcast += bytes =>
        {
            List<IGamePeer>? slowPeers = null;
            foreach (var session in _sessions.Values)
            {
                if (session.Room == room && session.PlayerId is not null)
                {
                    if (!session.Peer.TrySend(bytes, true, GameplayProtocol.GameplayChannel))
                    {
                        slowPeers ??= new List<IGamePeer>();
                        slowPeers.Add(session.Peer);
                    }
                }
            }

            if (slowPeers != null)
            {
                foreach (var peer in slowPeers)
                {
                    peer.Close("SlowConsumer");
                    Disconnect(peer);
                }
            }
        };

        room.Match.PlayerDropped += playerId =>
        {
            foreach (var session in _sessions.Values.ToArray())
            {
                if (session.PlayerId == playerId)
                {
                    session.Peer.Close("LoadingTimeout");
                    Disconnect(session.Peer);
                }
            }
            ReleaseHeldSession(playerId);
        };

        room.Match.GraceExpired += playerId =>
        {
            ReleaseHeldSession(playerId);
        };

        room.Match.ResumeTokenRotated += (sessionId, netId, newToken) =>
        {
            foreach (var session in _sessions.Values)
            {
                if (session.Room == room && session.PlayerId == sessionId)
                {
                    var resumePacket = new ResumeTokenPacket
                    {
                        MatchId = room.Match.MatchId,
                        NetId = netId,
                        ResumeToken = newToken
                    };
                    var writer = new NetDataWriter();
                    PacketRegistry.BeginFrame(writer);
                    _gameplayRegistry.Write(writer, ref resumePacket);
                    session.Peer.TrySend(writer.CopyData(), true, GameplayProtocol.GameplayChannel);
                }
            }
        };
    }

    private void SetupGameplaySubscriptions()
    {
        _gameplayRegistry.Subscribe<ClientHelloPacket, Session>((packet, session) =>
        {
            if (session.PlayerId is null) return;
            if (packet.ProtocolVersion != MatchProtocol.Version || !packet.Ready) return;

            // Register entity ONLY on valid ClientHello(ProtocolVersion=MatchProtocol.Version,Ready=true)
            if (!session.Room.Match.TryRegisterPlayer(session.PlayerId, session.Request.DisplayName, out _))
            {
                return;
            }

            if (session.ProfileKey != null)
            {
                session.Room.Match.TryAttachProfile(session.PlayerId, session.ProfileKey);
            }

            session.Room.Match.SetPlayerReady(session.PlayerId);

            // Send initial phase, roster, and resume proof
            SendInitialState(session);
        });

        _gameplayRegistry.Subscribe<LoadAckPacket, Session>((packet, session) =>
        {
            if (session.PlayerId is null || !packet.Ready) return;
            session.Room.Match.SetPlayerLoaded(session.PlayerId, packet.MatchId);
        });

        // Process ONLY match-scoped input; do not trust unscoped PlayerInput
        _gameplayRegistry.Subscribe<MatchInputPacket, Session>((packet, session) =>
        {
            if (session.PlayerId is null) return;
            session.Room.Match.ProcessInput(session.PlayerId, packet);
        });

        _gameplayRegistry.Subscribe<PlayerLeavePacket, Session>((packet, session) =>
        {
            if (session.PlayerId is null) return;
            session.Room.Match.ProcessLeave(session.PlayerId, packet.MatchId);
            ReleaseHeldSession(session.PlayerId);
        });

        _gameplayRegistry.Subscribe<ReplayAckPacket, Session>((packet, session) =>
        {
            if (session.PlayerId is null) return;
            session.Room.Match.ProcessReplayAck(session.PlayerId, packet.MatchId);
        });

        _gameplayRegistry.Subscribe<SnapshotAckPacket, Session>((_, _) => { });

        // Gameplay identity attachment via single-use registered ticket
        _gameplayRegistry.Subscribe<AttachIdentityPacket, Session>((packet, session) =>
        {
            if (string.IsNullOrEmpty(packet.Ticket)) return;
            if (!_tickets.TryGetValue(packet.Ticket, out var ticket)) return;
            if (DateTimeOffset.UtcNow >= ticket.ExpiresUtc || session.Request.AttemptId == Guid.Empty || session.Request.AttemptId != ticket.AttemptId) return;
            if (!string.Equals(ticket.RoomCode, session.Room.Code, StringComparison.Ordinal)) return;
            if (session.Room.Match.Phase is not (MatchPhase.Waiting or MatchPhase.Lobby)) return;
            if (session.ProfileKey != null && !string.Equals(session.ProfileKey, ticket.ProfileKey, StringComparison.Ordinal)) return;
            // Registration is not proof of possession; consume only after the matching gameplay request.
            if (session.PlayerId != null && session.Room.Match.Entities.TryGetBySession(session.PlayerId, out _) && !session.Room.Match.TryAttachProfile(session.PlayerId, ticket.ProfileKey)) return;
            _tickets.Remove(packet.Ticket);
            session.ProfileKey = ticket.ProfileKey;
        });
    }

    private void SendInitialState(Session session)
    {
        var match = session.Room.Match;
        var phase = match.GetPhasePacket();
        var roster = match.GetRosterPacket();

        var writer = new NetDataWriter();
        PacketRegistry.BeginFrame(writer);
        _gameplayRegistry.Write(writer, ref phase);
        _gameplayRegistry.Write(writer, ref roster);

        if (match.Entities.TryGetBySession(session.PlayerId!, out var entity) && entity != null)
        {
            var resume = new ResumeTokenPacket
            {
                MatchId = match.MatchId,
                NetId = entity.NetId,
                ResumeToken = entity.ResumeToken
            };
            _gameplayRegistry.Write(writer, ref resume);
        }

        session.Peer.TrySend(writer.CopyData(), true, GameplayProtocol.GameplayChannel);
    }

    public RoomResult RegisterTicket(RegisterGameTicketCommand command)
    {
        if (string.IsNullOrEmpty(command.Ticket) || command.Ticket.Length > 64)
            return new(400, new RoomError("InvalidTicket"));
        if (command.AttemptId == Guid.Empty)
            return new(400, new RoomError("InvalidAttemptId"));
        if (string.IsNullOrEmpty(command.RoomCode))
            return new(400, new RoomError("InvalidRoomCode"));
        if (string.IsNullOrEmpty(command.ProfileKey) || command.ProfileKey.Length > 80 || command.ProfileKey.Any(char.IsControl))
            return new(400, new RoomError("InvalidProfileKey"));

        var utc = DateTimeOffset.UtcNow;
        if (command.ExpiresUtc <= utc || command.ExpiresUtc > utc.AddSeconds(35)) return new(400, new RoomError("InvalidTicketExpiry"));
        if (_tickets.ContainsKey(command.Ticket)) return new(409, new RoomError("TicketAlreadyRegistered"));
        var room = Rooms.Find(command.RoomCode);
        if (room is null)
            return new(404, new RoomError("RoomNotFound"));

        if (_tickets.Count >= 4096)
        {
            var nowUtc = DateTimeOffset.UtcNow;
            foreach (var key in _tickets.Where(t => t.Value.ExpiresUtc <= nowUtc).Select(t => t.Key).ToArray())
                _tickets.Remove(key);
            if (_tickets.Count >= 4096)
                return new(503, new RoomError("ServerBusy"));
        }

        _tickets[command.Ticket] = new(command.Ticket, command.AttemptId, command.RoomCode, command.ProfileKey, command.ExpiresUtc);

        return new(200, new { status = "registered" });
    }

    public string? Validate(JoinRequest request, string transport)
    {
        var invalid = _handshake.ValidateRequest(request);
        if (invalid is not null) return invalid;
        if (request.Version is (NetworkProtocol.LatestVersion or NetworkProtocol.Version3) && request.AttemptId == Guid.Empty) return "MalformedRequest";
        if (transport == "Wss" && request.Version is not (NetworkProtocol.LatestVersion or NetworkProtocol.Version3)) return "UnsupportedVersion";
        var room = Rooms.Find(request.RoomCode);
        if (room is null) return "InvalidRoomCode";

        // Resume handling: validate alive/unexpired token BEFORE capacity
        if (request.ResumeToken != Guid.Empty)
        {
            if (_attempts.TryGetValue(request.AttemptId, out var resumed))
            {
                // Allow only UDP -> WSS continuation of this exact uncommitted resume.
                // The old proof was already consumed; matching the opaque attempt + full
                // request authorizes transfer of its existing slot, never a fresh resume.
                if (transport != "Wss" || resumed.WssChosen || resumed.Committed || resumed.Current is not { } current ||
                    resumed.ResumeMatchId != room.Match.MatchId || resumed.Request != request ||
                    current.PlayerId is null || !room.Match.Entities.TryGetBySession(current.PlayerId, out var entity) || entity is null || !entity.IsAlive ||
                    room.Match.Phase != MatchPhase.InGame || entity.IsDisconnected && !room.Match.IsValidResumeToken(entity.ResumeToken, out _))
                    return "AttemptUsed";
                return null;
            }
            if (_attempts.Count >= 4096) return "ServerBusy";
            if (room.Match.Phase != MatchPhase.InGame)
                return "InvalidResumeToken";

            if (!room.Match.IsValidResumeToken(request.ResumeToken, out _))
                return "InvalidResumeToken";

            // Valid resume: slot already reserved/held, bypass capacity check
            return null;
        }

        // Freeze roster Loading; late joins Loading/InGame/Ending denied
        if (room.Match.Phase is MatchPhase.Loading or MatchPhase.InGame or MatchPhase.Ending or MatchPhase.Cleanup)
        {
            return "MatchInProgress";
        }

        if (request.AttemptId != Guid.Empty && _attempts.TryGetValue(request.AttemptId, out var previous))
        {
            if (previous.Request.RoomCode != request.RoomCode || previous.Request.DisplayName != request.DisplayName
                || previous.Request.Mode != request.Mode || previous.Committed || previous.WssChosen || transport != "Wss")
                return "AttemptUsed";
            if (previous.Current is not null) return null; // Transfer same slot
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

        // Check if resuming an existing held session
        if (request.ResumeToken != Guid.Empty && room.Match.IsValidResumeToken(request.ResumeToken, out var resumeEntity) && resumeEntity != null)
        {
            if (!_heldSessions.Remove(resumeEntity.PlayerSessionId, out var heldSession))
            { Reject(peer, "InvalidResumeToken"); return false; }
            if (!room.Match.TryResume(heldSession.PlayerId!, request.ResumeToken, out _, out _))
            { _heldSessions[heldSession.PlayerId!] = heldSession; Reject(peer, "InvalidResumeToken"); return false; }
            if (heldSession.Request.AttemptId != Guid.Empty && _attempts.TryGetValue(heldSession.Request.AttemptId, out var oldAttempt))
            { oldAttempt.Current = null; oldAttempt.RetiredAt = Stopwatch.GetTimestamp(); }
            heldSession.Peer = peer;
            heldSession.Request = request;
            heldSession.LastMessage = Stopwatch.GetTimestamp();
            heldSession.ApplicationSeen = false;
            _sessions.Add(peer, heldSession);
            _users[heldSession.PlayerId!] = heldSession;
            _attempts[request.AttemptId] = new(request) { Current = heldSession, PlayerId = heldSession.PlayerId,
                WssChosen = peer.Transport == "Wss", ResumeMatchId = room.Match.MatchId };
            return true;
        }

        Attempt? attempt = null;
        if (request.AttemptId != Guid.Empty)
        {
            if (!_attempts.TryGetValue(request.AttemptId, out attempt))
                _attempts.Add(request.AttemptId, attempt = new(request));
            attempt.WssChosen = peer.Transport == "Wss";
        }
        if (attempt?.Current is { } old)
        {
            if (request.ResumeToken != Guid.Empty)
            {
                if (old.PlayerId is null || !room.Match.ContinueResumedTransport(old.PlayerId)) { Reject(peer, "InvalidResumeToken"); return false; }
                _heldSessions.Remove(old.PlayerId);
            }
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
        if (session.PlayerId is not null)
        {
            Accept(session);

            if (session.Request.ResumeToken != Guid.Empty)
                SendInitialState(session);
            // Do NOT register entity here: entity registration occurs ONLY upon ClientHelloPacket
        }
        else Send(session, new[] { NetworkProtocol.AuthRequired }, true);
    }

    public void Receive(IGamePeer peer, byte[] packet, bool reliable, bool validChannel = true)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        // WSS has no LiteNetLib disconnect metadata. The final explicit quit frame
        // releases the slot immediately after the client's queued PlayerLeave.
        if (peer.Transport == "Wss" && packet.Length == 1 && packet[0] == NetworkProtocol.ExplicitQuit)
        {
            Quit(peer);
            peer.Close("UserRequested");
            return;
        }
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
            Rooms.Activate(session.Room, session.Id);
            Accept(session); return;
        }

        // Gameplay frame check: begins with FrameMarker (0x80)
        if (PacketRegistry.IsFrame(reader))
        {
            if (!validChannel || packet.Length > 4096 || !_frameValidator.TryReadFrame(new NetDataReader(packet)))
            { _metrics.RecordInvalidPacket(); return; }
            session.LastMessage = Stopwatch.GetTimestamp();
            session.ApplicationSeen = true;
            if (session.Request.AttemptId != Guid.Empty) _attempts[session.Request.AttemptId].Committed = true;
            _gameplayRegistry.TryReadFrame(reader, session);
            return;
        }

        if (!validChannel || (peer.Transport == "Udp" && reliable) || !NetworkProtocol.TryRead(reader, NetworkProtocol.Ping, out var sequence))
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

    private void Send(Session session, byte[] packet, bool reliable, byte channel = 0)
    {
        if (!session.Peer.TrySend(packet, reliable, channel)) { session.Peer.Close("SlowConsumer"); Disconnect(session.Peer); }
    }

    public void Reject(IGamePeer peer, string reason)
    {
        peer.Close(reason); Disconnect(peer); RecordReject(reason);
    }

    public void RecordReject(string reason)
    {
        _metrics.RecordRejection(); Console.WriteLine($"[rejected] reason={reason} serverSlots={ReservedPeers}/{(_options.Rooms ?? new()).MaxTotalPlayers}");
    }

    public void Quit(IGamePeer peer)
    {
        if (_sessions.TryGetValue(peer, out var session) && session.PlayerId is not null)
            session.Room.Match.ProcessLeave(session.PlayerId, session.Room.Match.MatchId);
        Disconnect(peer);
    }

    public void Disconnect(IGamePeer peer)
    {
        if (!_sessions.Remove(peer, out var session)) return;

        if (session.PlayerId is not null && session.Room.Match.Phase == MatchPhase.InGame &&
            session.Room.Match.Entities.TryGetBySession(session.PlayerId, out var entity) && entity is { IsAlive: true })
        {
            // Disconnect in game preserves character + slot for 15s grace
            session.Room.Match.MarkDisconnected(session.PlayerId);
            _heldSessions[session.PlayerId] = session;
            if (_users.GetValueOrDefault(session.PlayerId) == session) _users.Remove(session.PlayerId);
            return;
        }

        Rooms.Release(session.Room, session.Id, session.PlayerId);
        if (session.PlayerId is not null && _users.GetValueOrDefault(session.PlayerId) == session) _users.Remove(session.PlayerId);
        if (session.Request.AttemptId != Guid.Empty)
        {
            if (!_attempts.TryGetValue(session.Request.AttemptId, out var attempt)) return;
            attempt.Current = null; attempt.RetiredAt = Stopwatch.GetTimestamp();
        }
    }

    private void ReleaseHeldSession(string playerId)
    {
        if (_heldSessions.Remove(playerId, out var held))
        {
            held.Room.Peers.Remove(held.Id);
            if (held.Request.AttemptId != Guid.Empty && _attempts.TryGetValue(held.Request.AttemptId, out var retired))
            { retired.Current=null; retired.RetiredAt=Stopwatch.GetTimestamp(); }
            if (_users.GetValueOrDefault(playerId) == held) _users.Remove(playerId);
            if (held.Room.Peers.Count == 0) held.Room.EmptySince = DateTimeOffset.UtcNow;
        }
    }

    public void Poll()
    {
        Commands.Drain(Rooms); Rooms.ExpireEmpty();
        _hookedRooms.RemoveWhere(room => Rooms.Find(room.Code) != room);
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

        // Expire dead / finished held sessions
        foreach (var (pid, held) in _heldSessions.ToArray())
        {
            if (!held.Room.Match.Entities.TryGetBySession(pid, out var e) || e is null || !e.IsAlive || held.Room.Match.Phase != MatchPhase.InGame)
            {
                ReleaseHeldSession(pid);
            }
        }

        // Clean expired tickets
        var utcNow = DateTimeOffset.UtcNow;
        foreach (var (tKey, tVal) in _tickets.ToArray())
        {
            if (utcNow > tVal.ExpiresUtc) _tickets.Remove(tKey);
        }
    }

    public void Tick(double dt)
    {
        Rooms.Tick(dt);
    }

    public void CloseAll()
    {
        foreach (var peer in _sessions.Keys.ToArray()) { peer.Close("ServerStopping"); Disconnect(peer); }
        _heldSessions.Clear();
        _attempts.Clear();
        _tickets.Clear();
    }
}
