using JungleBackend.Protocol.Gameplay;

namespace GameServer.Simulation;

public sealed class HealthService : IHealthService
{
    private readonly IEntityRegistry _registry;
    private readonly Dictionary<byte, uint> _lastZoneDamageTick = new();

    public event Action<byte, byte, byte>? EntityDamaged;
    public event Action<byte, byte>? EntityDied;

    public HealthService(IEntityRegistry registry)
    {
        _registry = registry;
    }

    public void Reset() => _lastZoneDamageTick.Clear();

    public bool ApplyZoneDamage(byte netId, uint tick, byte damage = 5)
    {
        if (!_registry.TryGet(netId, out var entity) || entity == null || !entity.IsAlive)
            return false;

        // Idempotent: at most once per 30 ticks (1 second at 30Hz)
        if (_lastZoneDamageTick.TryGetValue(netId, out var lastTick))
        {
            if (unchecked((int)(tick - lastTick)) < 30)
                return false;
        }

        _lastZoneDamageTick[netId] = tick;
        return ApplyDamageInternal(entity, damage, 255); // 255 = Zone source
    }

    public bool ApplyDamage(byte netId, byte damage, byte sourceNetId)
    {
        if (!_registry.TryGet(netId, out var entity) || entity == null || !entity.IsAlive)
            return false;

        return ApplyDamageInternal(entity, damage, sourceNetId);
    }

    private bool ApplyDamageInternal(IEntity entity, byte damage, byte sourceNetId)
    {
        if (damage == 0 || !entity.IsAlive) return false;

        var newHp = (byte)Math.Max(0, entity.Hp - damage);
        entity.Hp = newHp;

        EntityDamaged?.Invoke(entity.NetId, damage, sourceNetId);

        if (newHp == 0)
        {
            entity.Flags |= PlayerStateFlags.Dead;
            EntityDied?.Invoke(entity.NetId, sourceNetId);
        }

        return true;
    }

    public byte GetHp(byte netId) =>
        _registry.TryGet(netId, out var entity) && entity != null ? entity.Hp : (byte)0;

    public bool IsAlive(byte netId) =>
        _registry.TryGet(netId, out var entity) && entity != null && entity.IsAlive;
}
