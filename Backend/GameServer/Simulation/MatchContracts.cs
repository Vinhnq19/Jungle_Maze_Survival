using JungleBackend.Protocol.Gameplay;

namespace GameServer.Simulation;

public interface IServerClock
{
    uint CurrentTick { get; }
    double DeltaTime { get; }
    double ElapsedSeconds { get; }
    DateTimeOffset UtcNow { get; }
}

public interface IEntity
{
    byte NetId { get; }
    string PlayerSessionId { get; }
    string DisplayName { get; }
    float X { get; set; }
    float Y { get; set; }
    float Radius { get; }
    ushort Rotation { get; set; }
    byte Hp { get; set; }
    bool IsAlive { get; }
    int Rank { get; set; }
    double SurvivalSeconds { get; set; }
    PlayerStateFlags Flags { get; set; }
    ushort LastProcessedInputSeq { get; set; }
    uint LastInputTick { get; set; }
    bool IsDisconnected { get; set; }
    uint? DisconnectedAtTick { get; set; }
    Guid ResumeToken { get; set; }
}

public interface IEntityRegistry
{
    bool TryGet(byte netId, out IEntity? entity);
    bool TryGetBySession(string sessionId, out IEntity? entity);
    IEnumerable<IEntity> All { get; }
    int Count { get; }
    int AliveCount { get; }
}

public interface IHealthService
{
    bool ApplyZoneDamage(byte netId, uint tick, byte damage = 5);
    bool ApplyDamage(byte netId, byte damage, byte sourceNetId);
    byte GetHp(byte netId);
    bool IsAlive(byte netId);
    event Action<byte, byte, byte>? EntityDamaged;
    event Action<byte, byte>? EntityDied;
}

public sealed record MatchParticipant(
    string PlayerSessionId,
    string DisplayName,
    int Rank,
    int Kills,
    double SurvivalSeconds,
    string? ProfileKey = null
);

public sealed record MatchResult(
    Guid MatchId,
    string RoomId,
    string ConfigVersion,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string Reason,
    IReadOnlyList<MatchParticipant> Participants
);

public interface IMatchRuntime
{
    Guid MatchId { get; }
    string RoomId { get; }
    string RoomCode { get; }
    MatchPhase Phase { get; }
    uint CurrentTick { get; }
    IServerClock Clock { get; }
    IHealthService Health { get; }
    IEntityRegistry Entities { get; }
    MatchProfile Profile { get; }
    void Tick(double deltaTime);
    bool TryRegisterPlayer(string sessionId, string displayName, out string reason);
    bool TryAttachProfile(string sessionId, string profileKey);
    bool SetPlayerReady(string sessionId);
    bool SetPlayerLoaded(string sessionId, Guid matchId);
    bool ProcessInput(string sessionId, in MatchInputPacket packet);
    bool ProcessInput(string sessionId, in PlayerInputPacket input);
    bool ProcessLeave(string sessionId, Guid matchId);
    bool ProcessReplayAck(string sessionId, Guid matchId);
    void MarkDisconnected(string sessionId);
    bool TryResume(string sessionId, Guid resumeToken, out byte netId, out Guid newResumeToken);
    bool IsValidResumeToken(Guid resumeToken, out IEntity? entity);
    MatchPhasePacket GetPhasePacket();
    MatchRosterPacket GetRosterPacket();
    event Action<MatchResult>? MatchCompleted;
    event Action<string>? PlayerDropped;
    event Action<string>? GraceExpired;
    event Action<string, byte, Guid>? ResumeTokenRotated;
    event Action<byte[]>? SnapshotGenerated;
    event Action<byte[]>? ReliableBroadcast;
}
