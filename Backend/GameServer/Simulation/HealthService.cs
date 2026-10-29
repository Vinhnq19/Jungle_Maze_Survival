using JungleBackend.Protocol.Gameplay;

namespace GameServer.Simulation;

public sealed class HealthService : IHealthService
{
    public Func<byte, byte, bool>? DamageInterceptor { get; set; }
    private readonly byte _maxHp;
    private readonly IEntityRegistry _registry;
    private readonly Dictionary<byte, uint> _lastZoneDamageTick = new();

    public event Action<byte, byte, byte>? EntityDamaged;
    public event Action<byte, byte>? EntityDied;

    public HealthService(IEntityRegistry registry, byte maxHp = 100)
    {
        _registry = registry;
        if (maxHp == 0) throw new ArgumentException("Max HP must be positive.");
        _maxHp = maxHp;
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

    public bool Eliminate(byte netId)
    {
        if (!_registry.TryGet(netId, out var entity) || entity == null || !entity.IsAlive) return false;
        return ApplyDamageInternal(entity, byte.MaxValue, netId, true);
    }

    private bool ApplyDamageInternal(IEntity entity, byte damage, byte sourceNetId, bool forced = false)
    {
        if (damage == 0 || !entity.IsAlive) return false;
        if (!forced && DamageInterceptor?.Invoke(entity.NetId, damage) == true) return false;

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

    public void Heal(byte netId, byte amount)
    {
        if (_registry.TryGet(netId, out var entity) && entity != null && entity.IsAlive)
            entity.Hp = (byte)Math.Min(_maxHp, entity.Hp + amount);
    }

    public void Rescue(byte netId, byte hp)
    {
        if (_registry.TryGet(netId, out var entity) && entity != null && entity.IsAlive)
            entity.Hp = (byte)Math.Clamp((int)hp, 1, _maxHp);
    }
}
