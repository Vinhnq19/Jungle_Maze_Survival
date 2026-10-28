using JungleBackend.Protocol.Gameplay;
using LiteNetLib.Utils;

namespace GameServer.Simulation;

public sealed class MatchRuntime : IMatchRuntime, IServerClock, IEntityRegistry
{
    private sealed class Entity : IEntity
    {
        public byte NetId { get; init; }
        public string PlayerSessionId { get; set; } = "";
        public string DisplayName { get; init; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public float Radius { get; init; } = PhysicsSimulation.PlayerRadius;
        public bool HasInput;
        public uint InputBudgetTick;
        public int InputBudget;
        public ushort Rotation { get; set; }
        public byte Hp { get; set; } = 100;
        public bool IsAlive => (Flags & PlayerStateFlags.Dead) == 0 && Hp > 0;
        public PlayerStateFlags Flags { get; set; }
        public ushort LastProcessedInputSeq { get; set; }
        public uint LastInputTick { get; set; }
        public bool IsDisconnected { get; set; }
        public uint? DisconnectedAtTick { get; set; }
        public double? DisconnectedAtSecond { get; set; }
        public Guid ResumeToken { get; set; }
        public sbyte MoveX { get; set; }
        public sbyte MoveY { get; set; }
        public int Rank { get; set; }
        public double SurvivalSeconds { get; set; }
        public bool Loaded { get; set; }
        public bool Ready { get; set; }
        public bool ReplayAcked { get; set; }
        public string? ProfileKey { get; set; }
        public uint EliminationTick;
        public byte KillerNetId = 255;
        public EliminationCauseWire EliminationCause = EliminationCauseWire.Unknown;
        public KillcamReplayResult? CapturedKillcam;
        public int ReplayRequestCount;
    }

    public sealed class ReplayFrame
    {
        public uint Tick;
        public float ZoneCenterX, ZoneCenterY, ZoneRadius;
        public (byte NetId, float X, float Y, byte Hp, PlayerStateFlags Flags)[] Players = Array.Empty<(byte, float, float, byte, PlayerStateFlags)>();
    }

    private readonly MapData _map;
    private readonly ZoneSchedule _zoneSchedule;
    private readonly MatchProfile _profile;
    private readonly Dictionary<byte, Entity> _entities = new();
    private readonly Dictionary<string, Entity> _bySession = new(StringComparer.Ordinal);
    private HealthService _health;
    private readonly PhysicsSimulation _physics;
    private ZoneSimulation? _zone;

    // Private replay ring buffer >= 5s (150 ticks; 180 ticks = 6 seconds at 30Hz)
    private readonly ReplayFrame[] _replayRing = new ReplayFrame[180];
    private int _replayIndex;
    private uint _lastRecordedReplayTick = uint.MaxValue;

    private readonly PacketRegistry _packets = new();
    private readonly NetDataWriter _snapshotWriter = new(true, 1024);
    private uint _inGameStartTick;
    private double _phaseTimer;
    private double _matchElapsedSeconds;
    private DateTimeOffset _startUtc;
    private MatchResult? _finalResult;

    public Guid MatchId { get; private set; } = Guid.NewGuid();
    public string RoomId { get; }
    public string RoomCode { get; }
    public MatchPhase Phase { get; private set; } = MatchPhase.Waiting;
    public uint CurrentTick { get; private set; }
    public double DeltaTime { get; private set; } = 1.0 / 30.0;
    public double ElapsedSeconds => _matchElapsedSeconds;
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public IServerClock Clock => this;
    public IHealthService Health => _health;
    public IEntityRegistry Entities => this;
    public MatchProfile Profile => _profile;
    public ZoneSimulation? Zone => _zone;

    public event Action<MatchResult>? MatchCompleted;
    public event Action<string>? PlayerDropped;
    public event Action<string>? GraceExpired;
    public event Action<string, byte, Guid>? ResumeTokenRotated;
    public event Action<byte[]>? SnapshotGenerated;
    public event Action<byte[]>? ReliableBroadcast;

    public MatchRuntime(string roomCode, MapData map, ZoneSchedule zoneSchedule, MatchProfile profile, string? roomId = null)
    {
        RoomCode = roomCode;
        RoomId = roomId ?? Guid.NewGuid().ToString();
        _map = map;
        _zoneSchedule = zoneSchedule;
        _profile = profile;
        _health = new HealthService(this);
        _health.EntityDied += OnEntityDied;
        _physics = new PhysicsSimulation(map, profile.PlayerRadius, profile.PlayerSpeed);
        if (map.Version != profile.Version || zoneSchedule.Version != profile.Version)
            throw new InvalidDataException("Game data versions differ.");

        for (var i = 0; i < _replayRing.Length; i++)
            _replayRing[i] = new ReplayFrame();
    }

    public bool TryGet(byte netId, out IEntity? entity)
    {
        var found = _entities.TryGetValue(netId, out var e);
        entity = e;
        return found;
    }

    public bool TryGetBySession(string sessionId, out IEntity? entity)
    {
        var found = _bySession.TryGetValue(sessionId, out var e);
        entity = e;
        return found;
    }

    public IEnumerable<IEntity> All => _entities.Values;
    public int Count => _entities.Count;
    public int AliveCount => _entities.Values.Count(e => e.IsAlive);

    public bool TryRegisterPlayer(string sessionId, string displayName, out string reason)
    {
        reason = "";
        if (_bySession.ContainsKey(sessionId))
            return true; // Already registered

        // Freeze roster Loading; late joins Loading/InGame/Ending denied
        if (Phase is MatchPhase.Loading or MatchPhase.InGame or MatchPhase.Ending or MatchPhase.Cleanup)
        {
            reason = "MatchInProgress";
            return false;
        }

        if (_entities.Count >= _profile.MaxPlayers)
        {
            reason = "RoomFull";
            return false;
        }

        // Allocate lowest UNUSED NetId (holes from leaves fill safely without overwriting)
        byte? unusedNetId = null;
        for (byte i = 0; i < _profile.MaxPlayers; i++)
        {
            if (!_entities.ContainsKey(i))
            {
                unusedNetId = i;
                break;
            }
        }

        if (unusedNetId == null)
        {
            reason = "RoomFull";
            return false;
        }

        var netId = unusedNetId.Value;
        var entity = new Entity
        {
            NetId = netId,
            PlayerSessionId = sessionId,
            DisplayName = displayName,
            Radius = _profile.PlayerRadius,
            Hp = _profile.PlayerHp,
            ResumeToken = Guid.NewGuid()
        };

        _entities[netId] = entity;
        _bySession[sessionId] = entity;

        BroadcastRoster();
        return true;
    }

    public bool TryAttachProfile(string sessionId, string profileKey)
    {
        if (Phase is not (MatchPhase.Waiting or MatchPhase.Lobby))
            return false;

        if (_bySession.TryGetValue(sessionId, out var entity))
        {
            if (entity.ProfileKey != null && !string.Equals(entity.ProfileKey, profileKey, StringComparison.Ordinal))
                return false;

            entity.ProfileKey = profileKey;
            return true;
        }

        return false;
    }

    public bool SetPlayerReady(string sessionId)
    {
        if (!_bySession.TryGetValue(sessionId, out var entity)) return false;
        entity.Ready = true;

        BroadcastRoster();

        if (Phase == MatchPhase.Waiting && _entities.Values.Count(e => e.Ready) >= _profile.MinPlayers)
        {
            TransitionTo(MatchPhase.Lobby);
        }
        return true;
    }

    public bool SetPlayerLoaded(string sessionId, Guid matchId)
    {
        if (matchId != MatchId || !_bySession.TryGetValue(sessionId, out var entity))
            return false;

        entity.Loaded = true;
        return true;
    }

    public bool ProcessInput(string sessionId, in MatchInputPacket packet)
    {
        if (Phase != MatchPhase.InGame || packet.MatchId != MatchId)
            return false;

        if (!packet.Input.IsValid)
            return false;

        if (!_bySession.TryGetValue(sessionId, out var entity) || !entity.IsAlive || entity.IsDisconnected)
            return false;

        // Signed differences remain valid across UInt32/UInt16 wrap. Budget bounds burst input.
        var tickDelta = unchecked((int)(packet.Input.Tick - CurrentTick));
        if (tickDelta > 30 || tickDelta < -60) return false;
        if (entity.InputBudgetTick != CurrentTick) { entity.InputBudgetTick = CurrentTick; entity.InputBudget = 0; }
        if (++entity.InputBudget > 2) return false;
        var last = entity.LastProcessedInputSeq;
        var seq = packet.Input.InputSeq;
        if (entity.HasInput && unchecked((short)(seq - last)) <= 0) return false;
        entity.HasInput = true;

        entity.LastProcessedInputSeq = seq;
        entity.LastInputTick = CurrentTick;
        entity.MoveX = packet.Input.MoveX;
        entity.MoveY = packet.Input.MoveY;
        entity.Rotation = packet.Input.Aim;

        return true;
    }

    public bool ProcessInput(string sessionId, in PlayerInputPacket input) =>
        ProcessInput(sessionId, new MatchInputPacket { MatchId = MatchId, Input = input });

    public bool ProcessLeave(string sessionId, Guid matchId)
    {
        if (matchId != MatchId || !_bySession.TryGetValue(sessionId, out var entity) || !entity.IsAlive)
            return false;

        // Explicit leave eliminates player immediately without waiting for 15s grace
        _health.ApplyDamage(entity.NetId, 255, entity.NetId);
        BroadcastEntityEvent(entity.NetId, MatchEntityEvent.Eliminated);
        BroadcastRoster();
        return true;
    }

    public bool ProcessReplayAck(string sessionId, Guid matchId)
    {
        if (matchId != MatchId || !_bySession.TryGetValue(sessionId, out var entity))
            return false;

        entity.ReplayAcked = true;
        return true;
    }

    public void MarkDisconnected(string sessionId)
    {
        if (!_bySession.TryGetValue(sessionId, out var entity)) return;

        if (Phase == MatchPhase.InGame)
        {
            // Preserve character + slot for 15s grace
            entity.IsDisconnected = true;
            entity.DisconnectedAtTick = CurrentTick;
            entity.DisconnectedAtSecond = _matchElapsedSeconds;
            entity.MoveX = 0;
            entity.MoveY = 0;
        }
        else if (Phase == MatchPhase.Lobby)
        {
            _entities.Remove(entity.NetId);
            _bySession.Remove(sessionId);
            BroadcastEntityEvent(entity.NetId, MatchEntityEvent.Despawn);
            BroadcastRoster();

            // Cancel lobby if below min
            if (_entities.Values.Count(e => e.Ready) < _profile.MinPlayers)
            {
                TransitionTo(MatchPhase.Waiting);
            }
        }
        else
        {
            _entities.Remove(entity.NetId);
            _bySession.Remove(sessionId);
            BroadcastEntityEvent(entity.NetId, MatchEntityEvent.Despawn);
            BroadcastRoster();
        }
    }

    public bool IsValidResumeToken(Guid resumeToken, out IEntity? entity)
    {
        entity = null;
        if (Phase != MatchPhase.InGame || resumeToken == Guid.Empty)
            return false;

        foreach (var e in _entities.Values)
        {
            if (e.IsDisconnected && e.IsAlive && e.ResumeToken == resumeToken)
            {
                if (_matchElapsedSeconds - (e.DisconnectedAtSecond ?? 0.0) < _profile.DisconnectGraceSeconds)
                {
                    entity = e;
                    return true;
                }
            }
        }
        return false;
    }

    public bool TryResume(string sessionId, Guid resumeToken, out byte netId, out Guid newResumeToken)
    {
        netId = 255;
        newResumeToken = Guid.Empty;

        if (!IsValidResumeToken(resumeToken, out var matched) || matched is not Entity entity)
            return false;

        var oldSessionId = entity.PlayerSessionId;
        if (oldSessionId != sessionId)
        {
            _bySession.Remove(oldSessionId);
            entity.PlayerSessionId = sessionId;
            _bySession[sessionId] = entity;
        }

        entity.IsDisconnected = false;
        entity.DisconnectedAtTick = null;
        entity.DisconnectedAtSecond = null;

        // Independent secure random proof ROTATES on every resume
        entity.ResumeToken = Guid.NewGuid();
        netId = entity.NetId;
        newResumeToken = entity.ResumeToken;

        ResumeTokenRotated?.Invoke(sessionId, netId, newResumeToken);
        BroadcastRoster();
        return true;
    }

    // A transport transfer is part of the SAME already-authorized resume attempt.
    // Keep its rotated proof; do not consume/rotate another credential internally.
    internal bool ContinueResumedTransport(string sessionId)
    {
        if (Phase != MatchPhase.InGame || !_bySession.TryGetValue(sessionId, out var e) || !e.IsAlive) return false;
        if (e.IsDisconnected && !IsValidResumeToken(e.ResumeToken, out _)) return false;
        e.IsDisconnected = false; e.DisconnectedAtTick = null; e.DisconnectedAtSecond = null;
        return true;
    }

    public void Tick(double deltaTime)
    {
        DeltaTime = deltaTime;
        CurrentTick++;

        switch (Phase)
        {
            case MatchPhase.Waiting:
                TickWaiting();
                break;
            case MatchPhase.Lobby:
                TickLobby(deltaTime);
                break;
            case MatchPhase.Loading:
                TickLoading(deltaTime);
                break;
            case MatchPhase.InGame:
                TickInGame(deltaTime);
                break;
            case MatchPhase.Ending:
                TickEnding(deltaTime);
                break;
            case MatchPhase.Cleanup:
                TickCleanup();
                break;
        }
    }

    private void TickWaiting()
    {
        if (_entities.Values.Count(e => e.Ready) >= _profile.MinPlayers)
        {
            TransitionTo(MatchPhase.Lobby);
        }
    }

    private void TickLobby(double dt)
    {
        var readyCount = _entities.Values.Count(e => e.Ready);
        if (readyCount < _profile.MinPlayers)
        {
            TransitionTo(MatchPhase.Waiting);
            return;
        }

        _phaseTimer -= dt;
        if (_phaseTimer <= 0.0)
        {
            TransitionTo(MatchPhase.Loading);
        }
    }

    private void TickLoading(double dt)
    {
        _phaseTimer -= dt;

        var allLoaded = _entities.Values.All(e => e.Loaded);
        if (allLoaded || _phaseTimer <= 0.0)
        {
            // Drop missing players who failed to load
            var missing = _entities.Values.Where(e => !e.Loaded).ToArray();
            foreach (var m in missing)
            {
                _entities.Remove(m.NetId);
                _bySession.Remove(m.PlayerSessionId);
                BroadcastEntityEvent(m.NetId, MatchEntityEvent.Despawn);
                PlayerDropped?.Invoke(m.PlayerSessionId);
            }

            if (missing.Length > 0)
            {
                BroadcastRoster();
            }

            if (_entities.Count < _profile.MinPlayers)
            {
                TransitionTo(MatchPhase.Waiting);
            }
            else
            {
                TransitionTo(MatchPhase.InGame);
            }
        }
    }

    private void TickInGame(double dt)
    {
        _matchElapsedSeconds += dt;

        // 1. Process disconnect grace timeout
        foreach (var entity in _entities.Values.Where(e => e.IsAlive && e.IsDisconnected))
        {
            if (_matchElapsedSeconds - (entity.DisconnectedAtSecond ?? 0.0) >= _profile.DisconnectGraceSeconds)
            {
                _health.ApplyDamage(entity.NetId, 255, entity.NetId);
                GraceExpired?.Invoke(entity.PlayerSessionId);
            }
        }

        // 2. Process physics & movement (input stale >= 8 ticks stops)
        var staleTicks = (uint)Math.Ceiling(_profile.InputStaleMs * 30.0 / 1000.0); // 250ms at 30Hz ~ 7.5 ticks -> stale if >= 8 ticks
        foreach (var entity in _entities.Values.Where(e => e.IsAlive))
        {
            var isStale = (CurrentTick - entity.LastInputTick >= staleTicks) || entity.IsDisconnected;
            var mx = isStale ? 0f : (entity.MoveX / 127f);
            var my = isStale ? 0f : (entity.MoveY / 127f);

            var (newX, newY) = _physics.Move(entity.X, entity.Y, mx, my, dt);
            entity.X = newX;
            entity.Y = newY;
        }

        // 3. Zone simulation & zone damage
        if (_zone != null)
        {
            _zone.Update(_matchElapsedSeconds / _profile.ZoneTimeScale, CurrentTick);

            // Whole-tick damage: applied once each second (every 30 ticks)
            if (unchecked(CurrentTick - _inGameStartTick) % 30 == 0)
            {
                foreach (var entity in _entities.Values.Where(e => e.IsAlive))
                {
                    if (_zone.IsOutside(entity.X, entity.Y))
                    {
                        _health.ApplyZoneDamage(entity.NetId, CurrentTick, (byte)_zone.Schedule.DamagePerSecond);
                    }
                }
            }
        }

        // 4. Whole-tick elimination processing & winner check
        var aliveBeforeTick = _entities.Values.Count(e => e.IsAlive);
        var newlyDied = _entities.Values.Where(e => !e.IsAlive && e.Rank == 0).ToArray();

        if (newlyDied.Length > 0)
        {
            EnsureReplayFrameRecorded();
            var sharedRank = aliveBeforeTick + 1; // Same-tick elimination shares rank
            foreach (var dead in newlyDied)
            {
                dead.Rank = sharedRank;
                dead.SurvivalSeconds = _matchElapsedSeconds;
                if (dead.EliminationTick == 0) dead.EliminationTick = CurrentTick;

                CaptureReplayForVictim(dead);
                BroadcastElimination(dead);
                BroadcastEntityEvent(dead.NetId, MatchEntityEvent.Eliminated);
                BroadcastPlayerDiedEvent(dead);
            }
            BroadcastRoster();
        }

        var aliveNow = _entities.Values.Count(e => e.IsAlive);
        if (aliveNow <= 1 && _entities.Count > 1)
        {
            if (aliveNow == 1)
            {
                var winner = _entities.Values.First(e => e.IsAlive);
                winner.Rank = 1;
                winner.SurvivalSeconds = _matchElapsedSeconds;
                EndMatch(MatchEndReason.LastAlive, winner.NetId);
            }
            else // aliveNow == 0: tie
            {
                EndMatch(MatchEndReason.Tie, 255);
            }
        }

        // 5. Store frame in private Replay Ring Buffer
        EnsureReplayFrameRecorded();

        // 6. Broadcast 30Hz snapshot (using SetPlayer to avoid heap allocation)
        BroadcastSnapshot();
    }

    private void TickEnding(double dt)
    {
        _phaseTimer -= dt;
        var allReplayed = _entities.Values.All(e => e.ReplayAcked || e.IsDisconnected);

        if (_phaseTimer <= 0.0 || allReplayed)
        {
            TransitionTo(MatchPhase.Cleanup);
        }
    }

    private void TickCleanup()
    {
        // Clean world and restart in same room
        // Drop disconnected players
        var toRemove = _entities.Values.Where(e => e.IsDisconnected).Select(e => e.PlayerSessionId).ToArray();
        foreach (var pid in toRemove)
        {
            if (_bySession.Remove(pid, out var entity))
            {
                _entities.Remove(entity.NetId);
                BroadcastEntityEvent(entity.NetId, MatchEntityEvent.Despawn);
                PlayerDropped?.Invoke(pid);
            }
        }

        // Preserve connected players and reset their match state
        foreach (var entity in _entities.Values)
        {
            entity.Hp = _profile.PlayerHp;
            entity.Flags = PlayerStateFlags.None;
            entity.Rank = 0;
            entity.SurvivalSeconds = 0;
            entity.LastProcessedInputSeq = 0;
            entity.HasInput = false;
            entity.InputBudget = 0;
            entity.LastInputTick = 0;
            entity.MoveX = 0;
            entity.MoveY = 0;
            entity.Ready = true; // Hello is capability negotiation, not a per-round Ready button.
            entity.Loaded = false;
            entity.ReplayAcked = false;
            entity.ResumeToken = Guid.NewGuid();
            // Preserve verified identity across rounds on the same connection.
        }

        _zone = null;
        _finalResult = null;
        MatchId = Guid.NewGuid(); // Fresh MatchId for next match
        CurrentTick = 0;
        _matchElapsedSeconds = 0;

        _replayIndex = 0;
        _lastRecordedReplayTick = uint.MaxValue;
        for (var i = 0; i < _replayRing.Length; i++)
        {
            _replayRing[i].Tick = 0;
            _replayRing[i].Players = Array.Empty<(byte, float, float, byte, PlayerStateFlags)>();
        }

        foreach (var entity in _entities.Values)
        {
            entity.EliminationTick = 0;
            entity.KillerNetId = 255;
            entity.EliminationCause = EliminationCauseWire.Unknown;
            entity.CapturedKillcam = null;
            entity.ReplayRequestCount = 0;
        }

        // Health dedupe resets per match
        _health = new HealthService(this);
        _health.EntityDied += OnEntityDied;

        TransitionTo(MatchPhase.Waiting);
        BroadcastRoster();
    }

    private void TransitionTo(MatchPhase newPhase)
    {
        Phase = newPhase;

        switch (newPhase)
        {
            case MatchPhase.Waiting:
                _phaseTimer = 0;
                break;
            case MatchPhase.Lobby:
                _phaseTimer = _profile.LobbyDurationSeconds;
                break;
            case MatchPhase.Loading:
                _phaseTimer = _profile.LoadingTimeoutSeconds;
                break;
            case MatchPhase.InGame:
                StartInGame();
                break;
            case MatchPhase.Ending:
                _phaseTimer = _profile.EndingDurationSeconds;
                break;
            case MatchPhase.Cleanup:
                _phaseTimer = 0;
                break;
        }

        // Broadcast phase packet reliably
        var phasePacket = GetPhasePacket();
        var writer = new NetDataWriter();
        PacketRegistry.BeginFrame(writer);
        _packets.Write(writer, ref phasePacket);
        ReliableBroadcast?.Invoke(writer.CopyData());
    }

    private void StartInGame()
    {
        // KEEP SAME MatchId from lobby/loading through result!
        _startUtc = DateTimeOffset.UtcNow;
        _matchElapsedSeconds = 0;
        _inGameStartTick = CurrentTick;

        // Initialize zone
        var seed = MatchId.GetHashCode();
        _zone = new ZoneSimulation(_zoneSchedule, seed, 2.0f, _map);

        // Spawn players at pair-distance >= 6 safe points
        var spawnList = _map.SpawnPoints;
        var netIdCounter = 0;
        foreach (var entity in _entities.Values)
        {
            var spawn = spawnList[netIdCounter % spawnList.Count];
            entity.X = spawn.X;
            entity.Y = spawn.Y;
            entity.Hp = _profile.PlayerHp;
            entity.Flags = PlayerStateFlags.None;
            entity.LastInputTick = CurrentTick;
            entity.LastProcessedInputSeq = 0;
            entity.HasInput = false;
            entity.InputBudget = 0;
            entity.Rank = 0;

            // Rotate resume token on game start and notify session hub
            entity.ResumeToken = Guid.NewGuid();
            ResumeTokenRotated?.Invoke(entity.PlayerSessionId, entity.NetId, entity.ResumeToken);

            BroadcastEntityEvent(entity.NetId, MatchEntityEvent.Spawn);
            netIdCounter++;
        }

        BroadcastRoster();
    }

    private void EndMatch(MatchEndReason reason, byte winnerNetId)
    {
        var participants = _entities.Values.Select(e => new MatchParticipant(
            e.PlayerSessionId,
            e.DisplayName,
            e.Rank == 0 ? (reason == MatchEndReason.LastAlive && e.NetId == winnerNetId ? 1 : 2) : e.Rank,
            0, // Kills = 0 without combat
            e.SurvivalSeconds,
            e.ProfileKey
        )).ToList().AsReadOnly();

        // RoomId not secret RoomCode in persisted result
        _finalResult = new MatchResult(
            MatchId,
            RoomId,
            _profile.Version,
            _startUtc,
            DateTimeOffset.UtcNow,
            reason.ToString(),
            participants
        );

        // Fire outside tick sink for persistence
        MatchCompleted?.Invoke(_finalResult);

        // Send ResultPacket with full participant states
        var participantStates = _entities.Values.Select(e => new MatchParticipantState
        {
            NetId = e.NetId,
            PlayerSessionId = e.PlayerSessionId,
            DisplayName = e.DisplayName,
            Rank = (byte)(e.Rank == 0 ? (reason == MatchEndReason.LastAlive && e.NetId == winnerNetId ? 1 : 2) : e.Rank),
            Kills = 0,
            SurvivalTicks = (uint)(e.SurvivalSeconds * 30)
        }).ToArray();

        var resultPacket = new MatchResultPacket
        {
            MatchId = MatchId,
            Reason = reason,
            WinnerNetId = winnerNetId,
            ParticipantCount = (byte)participantStates.Length,
            Participants = participantStates
        };

        var writer = new NetDataWriter();
        PacketRegistry.BeginFrame(writer);
        _packets.Write(writer, ref resultPacket);
        ReliableBroadcast?.Invoke(writer.CopyData());

        TransitionTo(MatchPhase.Ending);
    }

    private void RecordReplayFrame()
    {
        var frame = _replayRing[_replayIndex % _replayRing.Length];
        frame.Tick = CurrentTick;
        frame.ZoneCenterX = _zone?.CurrentCenter.X ?? 25f;
        frame.ZoneCenterY = _zone?.CurrentCenter.Y ?? 25f;
        frame.ZoneRadius = _zone?.CurrentRadius ?? 25f;

        if (frame.Players.Length != _entities.Count)
            frame.Players = new (byte, float, float, byte, PlayerStateFlags)[_entities.Count];

        var i = 0;
        foreach (var e in _entities.Values)
        {
            frame.Players[i++] = (e.NetId, e.X, e.Y, e.Hp, e.Flags);
        }
        _replayIndex++;
    }

    private void BroadcastSnapshot()
    {
        var snapshot = new WorldSnapshotPacket
        {
            MatchId = MatchId,
            ServerTick = CurrentTick,
            PlayerCount = (byte)_entities.Count
        };

        var i = 0;
        foreach (var e in _entities.Values)
        {
            snapshot.SetPlayer(i++, new PlayerStatePacket
            {
                Tick = CurrentTick,
                NetId = e.NetId,
                X = e.X,
                Y = e.Y,
                Rotation = e.Rotation,
                LastProcessedInputSeq = e.LastProcessedInputSeq,
                Hp = e.Hp,
                Flags = e.Flags
            });
        }

        var writer = _snapshotWriter;
        PacketRegistry.BeginFrame(writer);
        _packets.Write(writer, ref snapshot);

        if (_zone != null)
        {
            var zonePacket = _zone.GetStatePacket(CurrentTick);
            zonePacket.MatchId = MatchId;
            zonePacket.ShrinkStartTick = _inGameStartTick + (uint)(zonePacket.ShrinkStartTick * _profile.ZoneTimeScale);
            zonePacket.ShrinkEndTick = _inGameStartTick + (uint)(zonePacket.ShrinkEndTick * _profile.ZoneTimeScale);
            _packets.Write(writer, ref zonePacket);
        }

        SnapshotGenerated?.Invoke(writer.CopyData());
    }

    public MatchPhasePacket GetPhasePacket() => new()
    {
        MatchId = MatchId,
        Phase = Phase,
        ConfigVersion = _profile.Version,
        ConfigHash = _profile.ConfigHash,
        ServerTick = CurrentTick,
        PhaseStartTick = CurrentTick,
        PhaseEndTick = CurrentTick + (uint)(_phaseTimer * 30),
        PlayerCount = (byte)_entities.Count,
        MinPlayers = (byte)_profile.MinPlayers,
        MaxPlayers = (byte)_profile.MaxPlayers
    };

    public MatchRosterPacket GetRosterPacket() => new()
    {
        MatchId = MatchId,
        Players = _entities.Values.Select(e => new MatchParticipantState
        {
            NetId = e.NetId,
            PlayerSessionId = e.PlayerSessionId,
            DisplayName = e.DisplayName,
            Rank = (byte)e.Rank,
            Kills = 0,
            SurvivalTicks = (uint)(e.SurvivalSeconds * 30)
        }).ToArray()
    };

    private void BroadcastRoster()
    {
        var roster = GetRosterPacket();
        var writer = new NetDataWriter();
        PacketRegistry.BeginFrame(writer);
        _packets.Write(writer, ref roster);
        ReliableBroadcast?.Invoke(writer.CopyData());
    }

    private void BroadcastEntityEvent(byte netId, MatchEntityEvent kind)
    {
        var evt = new MatchEntityEventPacket
        {
            MatchId = MatchId,
            NetId = netId,
            Kind = kind
        };
        var writer = new NetDataWriter();
        PacketRegistry.BeginFrame(writer);
        _packets.Write(writer, ref evt);
        ReliableBroadcast?.Invoke(writer.CopyData());
    }
    private void OnEntityDied(byte victimNetId, byte killerNetId)
    {
        if (_entities.TryGetValue(victimNetId, out var victim))
        {
            victim.EliminationTick = CurrentTick;
            if (killerNetId == 255)
            {
                victim.KillerNetId = 255;
                victim.EliminationCause = EliminationCauseWire.Zone;
            }
            else if (killerNetId == victimNetId)
            {
                victim.KillerNetId = 255;
                victim.EliminationCause = EliminationCauseWire.LeftMatch;
            }
            else
            {
                victim.KillerNetId = killerNetId;
                victim.EliminationCause = EliminationCauseWire.Player;
            }
        }
    }

    private void EnsureReplayFrameRecorded()
    {
        if (_lastRecordedReplayTick != CurrentTick)
        {
            RecordReplayFrame();
            _lastRecordedReplayTick = CurrentTick;
        }
    }

    private void CaptureReplayForVictim(Entity victim)
    {
        var endTick = victim.EliminationTick != 0 ? victim.EliminationTick : CurrentTick;
        var killerNetId = victim.KillerNetId;

        // Zone or unknown killer fallback: NoData
        if (killerNetId == 255 || killerNetId == victim.NetId || !_entities.ContainsKey(killerNetId))
        {
            victim.CapturedKillcam = new KillcamReplayResult(
                MatchId,
                (byte)4, // NoData
                victim.NetId,
                255,
                endTick,
                Array.Empty<KillcamFramePayload>()
            );
            return;
        }

        // Check window duration: must have at least 150 ticks (5 seconds at 30Hz)
        const int requiredFrames = 150;
        if (CurrentTick < _inGameStartTick || (CurrentTick - _inGameStartTick + 1) < requiredFrames)
        {
            victim.CapturedKillcam = new KillcamReplayResult(
                MatchId,
                (byte)7, // TooShort
                victim.NetId,
                killerNetId,
                endTick,
                Array.Empty<KillcamFramePayload>()
            );
            return;
        }

        var startTick = endTick - requiredFrames + 1;
        var frames = new KillcamFramePayload[requiredFrames];
        var ringLen = _replayRing.Length;

        for (var i = 0; i < requiredFrames; i++)
        {
            var targetTick = startTick + (uint)i;
            ReplayFrame? matchedFrame = null;

            for (var r = 0; r < ringLen; r++)
            {
                if (_replayRing[r].Tick == targetTick)
                {
                    matchedFrame = _replayRing[r];
                    break;
                }
            }

            if (matchedFrame != null)
            {
                var payload = new KillcamFramePayload
                {
                    ZoneX = matchedFrame.ZoneCenterX,
                    ZoneY = matchedFrame.ZoneCenterY,
                    ZoneRadius = matchedFrame.ZoneRadius
                };

                for (var p = 0; p < matchedFrame.Players.Length; p++)
                {
                    var player = matchedFrame.Players[p];
                    if (player.NetId == victim.NetId)
                    {
                        payload.VictimX = player.X;
                        payload.VictimY = player.Y;
                        payload.VictimHp = player.Hp;
                        payload.VictimFlags = (byte)player.Flags;
                    }
                    else if (player.NetId == killerNetId)
                    {
                        payload.KillerX = player.X;
                        payload.KillerY = player.Y;
                        payload.KillerHp = player.Hp;
                        payload.KillerFlags = (byte)player.Flags;
                        payload.KillerPresent = true;
                    }
                }

                frames[i] = payload;
            }
            else
            {
                victim.CapturedKillcam = new KillcamReplayResult(
                    MatchId,
                    (byte)7, // TooShort
                    victim.NetId,
                    killerNetId,
                    endTick,
                    Array.Empty<KillcamFramePayload>()
                );
                return;
            }
        }

        victim.CapturedKillcam = new KillcamReplayResult(
            MatchId,
            0, // Ok
            victim.NetId,
            killerNetId,
            endTick,
            frames
        );
    }

    public bool TryGetKillcamReplay(string sessionId, Guid matchId, byte victimNetId, out KillcamReplayResult? result)
    {
        result = null;
        if (matchId != MatchId) return false;
        if (!_bySession.TryGetValue(sessionId, out var entity) || entity == null) return false;
        if (entity.NetId != victimNetId) return false;
        if (entity.IsAlive) return false;
        if (entity.ReplayRequestCount >= 3) return false;

        entity.ReplayRequestCount++;

        if (entity.CapturedKillcam == null)
        {
            result = new KillcamReplayResult(
                MatchId,
                (byte)4, // NoData
                entity.NetId,
                255,
                entity.EliminationTick != 0 ? entity.EliminationTick : CurrentTick,
                Array.Empty<KillcamFramePayload>()
            );
            return true;
        }

        result = entity.CapturedKillcam;
        return true;
    }

    private void BroadcastElimination(Entity dead)
    {
        var packet = new MatchEliminationEventPacket
        {
            Version = MatchEliminationEventPacket.CurrentVersion,
            MatchId = MatchId,
            Tick = dead.EliminationTick,
            VictimNetId = dead.NetId,
            KillerNetId = dead.KillerNetId,
            Cause = dead.EliminationCause
        };
        var writer = new NetDataWriter();
        PacketRegistry.BeginFrame(writer);
        _packets.Write(writer, ref packet);
        ReliableBroadcast?.Invoke(writer.CopyData());
    }

    private void BroadcastPlayerDiedEvent(Entity dead)
    {
        var evt = new EventPacket
        {
            Tick = dead.EliminationTick,
            EventType = GameEventType.PlayerDied,
            SourceNetId = dead.KillerNetId != 255 ? dead.KillerNetId : EventPacket.NoTarget,
            TargetNetId = dead.NetId,
            Param1 = 0,
            Param2 = 0
        };
        var writer = new NetDataWriter();
        PacketRegistry.BeginFrame(writer);
        _packets.Write(writer, ref evt);
        ReliableBroadcast?.Invoke(writer.CopyData());
    }
}
