using GameServer.Simulation;
using GameServer.Simulation.Combat;
using JungleBackend.Protocol.Gameplay;

namespace CombatChecks;

/// <summary>Minimal player entity for combat checks. Position, HP and flags are plain fields.</summary>
public sealed class FakeEntity : IEntity
{
    public FakeEntity(byte netId, float x, float y, byte hp = 100)
    {
        NetId = netId;
        X = x;
        Y = y;
        Hp = hp;
    }

    public byte NetId { get; }
    public string PlayerSessionId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float Radius => PhysicsSimulation.PlayerRadius;
    public ushort Rotation { get; set; }
    public byte Hp { get; set; }
    public bool IsAlive => (Flags & PlayerStateFlags.Dead) == 0 && Hp > 0;
    public int Rank { get; set; }
    public double SurvivalSeconds { get; set; }
    public PlayerStateFlags Flags { get; set; }
    public ushort LastProcessedInputSeq { get; set; }
    public uint LastInputTick { get; set; }
    public bool IsDisconnected { get; set; }
    public uint? DisconnectedAtTick { get; set; }
    public Guid ResumeToken { get; set; }
}

public sealed class FakeRegistry : IEntityRegistry
{
    private readonly Dictionary<byte, FakeEntity> _entities = new();

    public FakeEntity Add(FakeEntity entity)
    {
        _entities[entity.NetId] = entity;
        return entity;
    }

    public bool TryGet(byte netId, out IEntity? entity)
    {
        var found = _entities.TryGetValue(netId, out var typed);
        entity = typed;
        return found;
    }

    public bool TryGetBySession(string sessionId, out IEntity? entity)
    {
        entity = null;
        return false;
    }

    public IEnumerable<IEntity> All => _entities.Values;
    public int Count => _entities.Count;
    public int AliveCount => _entities.Values.Count(e => e.IsAlive);
}

/// <summary>Records invulnerability and stun requests instead of owning real effects (Game-10 is not built yet).</summary>
public sealed class FakeEffects : IEffectService
{
    private readonly Dictionary<byte, uint> _invulnerableUntil = new();

    public List<(byte NetId, double Seconds)> Stuns { get; } = new();

    public bool IsInvulnerable(byte netId, uint tick) =>
        _invulnerableUntil.TryGetValue(netId, out var until) && tick < until;

    public void GrantInvulnerability(byte netId, uint tick, double seconds) =>
        _invulnerableUntil[netId] = tick + (uint)Math.Ceiling(seconds * CombatService.TicksPerSecond);

    public void ApplyStun(byte netId, uint tick, double seconds) => Stuns.Add((netId, seconds));
}

/// <summary>Returns a scripted answer and counts calls (ITM-01 is not built yet).</summary>
public sealed class FakePassive : IItemPassive
{
    public FakePassive(bool willSave) => WillSave = willSave;

    public bool WillSave { get; }
    public int Calls { get; private set; }

    public bool TryConsumeLifeSave(byte netId)
    {
        Calls++;
        return WillSave;
    }
}

/// <summary>Obstacles whose name starts with "Crate" are breakable; damage is recorded (ENV-02 is not built yet).</summary>
public sealed class FakeBreakables : IBreakableObstacles
{
    public List<(string Name, int Damage)> Damages { get; } = new();

    public bool IsBreakable(AABB obstacle) => obstacle.Name.StartsWith("Crate", StringComparison.Ordinal);

    public void ApplyDamage(AABB obstacle, int damage, byte attackerNetId) => Damages.Add((obstacle.Name, damage));
}
