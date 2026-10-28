using System.Numerics;
using JungleBackend.Protocol.Gameplay;

namespace GameServer.Simulation.Combat;

/// <summary>
/// Combat authoritative của server: kiểm request đánh/nạp, đạn, cooldown, hit test và sát thương.
/// Không đọc vị trí từ client và không ghi HP trực tiếp, trừ nhánh cứu mạng đã được item xác nhận.
/// </summary>
public sealed class CombatService
{
    public const int TicksPerSecond = 30;
    public const int LifeSaveHp = 50;
    public const double LifeSaveInvulnerabilitySeconds = 2.0;

    private const int WeaponCount = 10;

    private readonly IEntityRegistry _registry;
    private readonly IHealthService _health;
    private readonly IReadOnlyList<AABB> _obstacles;
    private readonly WeaponRules _rules;
    private readonly IEffectService _effects;
    private readonly IItemPassive _passive;
    private readonly IBreakableObstacles _breakables;
    private readonly Dictionary<byte, PlayerCombat> _players = new();

    private sealed class PlayerCombat
    {
        public CombatWeaponId Held = CombatWeaponId.Hand;
        public bool HasSequence;
        public ushort LastSequence;
        public uint NextAttackTick;
        public bool Reloading;
        public CombatWeaponId ReloadingWeapon;
        public uint ReloadEndTick;
        public bool LifeSaveUsed;
        public readonly int[] Magazine = new int[WeaponCount];
        public readonly int[] Reserve = new int[WeaponCount];
    }

    public CombatService(
        IEntityRegistry registry,
        IHealthService health,
        IReadOnlyList<AABB> obstacles,
        WeaponRules rules,
        IEffectService effects,
        IItemPassive passive,
        IBreakableObstacles breakables)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _health = health ?? throw new ArgumentNullException(nameof(health));
        _obstacles = obstacles ?? throw new ArgumentNullException(nameof(obstacles));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _effects = effects ?? throw new ArgumentNullException(nameof(effects));
        _passive = passive ?? throw new ArgumentNullException(nameof(passive));
        _breakables = breakables ?? throw new ArgumentNullException(nameof(breakables));
    }

    /// <summary>Đặt vũ khí đang cầm. ITM-01 gọi khi đổi trang bị; request chỉ được dùng đúng vũ khí đang cầm.</summary>
    public bool SetHeldWeapon(byte netId, CombatWeaponId weapon)
    {
        if (!_rules.TryGet(weapon, out _)) return false;
        GetOrCreate(netId).Held = weapon;
        return true;
    }

    /// <summary>Xóa toàn bộ đạn, cooldown và passive đã dùng khi vào trận mới.</summary>
    public void Reset() => _players.Clear();

    /// <summary>Hoàn tất các lần nạp đã đến hạn. Gọi mỗi tick trước khi xử lý request.</summary>
    public void Tick(uint tick)
    {
        foreach (var player in _players.Values)
        {
            if (player.Reloading && tick >= player.ReloadEndTick)
                FinishReload(player);
        }
    }

    public int GetMagazine(byte netId, CombatWeaponId weapon) =>
        _players.TryGetValue(netId, out var player) ? player.Magazine[(int)weapon] : 0;

    public int GetReserve(byte netId, CombatWeaponId weapon) =>
        _players.TryGetValue(netId, out var player) ? player.Reserve[(int)weapon] : 0;

    /// <summary>
    /// Xử lý một AttackRequest. Các CombatHit đã xác nhận được thêm vào <paramref name="hits"/>; caller chịu trách nhiệm xóa list trước khi gọi.
    /// </summary>
    public AttackResultPacket TryAttack(byte netId, in AttackRequestPacket request, uint tick, List<CombatHit> hits)
    {
        var player = GetOrCreate(netId);
        var result = new AttackResultPacket { RequestSeq = request.RequestSeq, Weapon = request.Weapon, Tick = tick, NetId = netId };

        if (!AcceptSequence(player, request.RequestSeq))
        {
            result.Outcome = CombatOutcome.Duplicate;
            result.Magazine = (byte)player.Magazine[(int)request.Weapon];
            return result;
        }

        Tick(tick);
        result.Magazine = (byte)player.Magazine[(int)request.Weapon];

        if (!_registry.TryGet(netId, out var attacker) || attacker == null || !attacker.IsAlive)
        {
            result.Outcome = CombatOutcome.Dead;
            return result;
        }

        if (request.Weapon != player.Held || !_rules.TryGet(request.Weapon, out var rule))
        {
            result.Outcome = CombatOutcome.InvalidWeapon;
            return result;
        }

        if (player.Reloading)
        {
            result.Outcome = CombatOutcome.Reloading;
            return result;
        }

        if (tick < player.NextAttackTick)
        {
            result.Outcome = CombatOutcome.Cooldown;
            return result;
        }

        if (rule.IsRanged)
        {
            if (player.Magazine[(int)rule.Id] <= 0)
            {
                result.Outcome = CombatOutcome.NoAmmo;
                return result;
            }

            player.Magazine[(int)rule.Id]--;
            result.Magazine = (byte)player.Magazine[(int)rule.Id];
        }

        player.NextAttackTick = tick + SecondsToTicks(rule.CooldownSeconds);

        var aim = CombatGeometry.AimDirection(request.Aim);
        var origin = new Vector2(attacker.X, attacker.Y);
        if (rule.IsRanged)
            ResolveRanged(attacker, rule, origin, aim, tick, hits);
        else
            ResolveMelee(attacker, rule, origin, aim, tick, hits);

        result.Outcome = CombatOutcome.Accepted;
        return result;
    }

    /// <summary>Xử lý một ReloadRequest cho súng đang cầm. Vũ khí cận chiến và tay không bị từ chối.</summary>
    public ReloadResultPacket TryReload(byte netId, in ReloadRequestPacket request, uint tick)
    {
        var player = GetOrCreate(netId);
        var result = new ReloadResultPacket { RequestSeq = request.RequestSeq, Weapon = request.Weapon, Tick = tick, NetId = netId };

        if (!AcceptSequence(player, request.RequestSeq))
        {
            result.Outcome = CombatOutcome.Duplicate;
            return result;
        }

        Tick(tick);
        result.Magazine = (byte)player.Magazine[(int)request.Weapon];

        if (!_registry.TryGet(netId, out var reloader) || reloader == null || !reloader.IsAlive)
        {
            result.Outcome = CombatOutcome.Dead;
            return result;
        }

        if (request.Weapon != player.Held || !_rules.TryGet(request.Weapon, out var rule) || !rule.IsRanged)
        {
            result.Outcome = CombatOutcome.InvalidWeapon;
            return result;
        }

        if (player.Reloading)
        {
            result.Outcome = CombatOutcome.Reloading;
            result.ReloadEndTick = player.ReloadEndTick;
            return result;
        }

        if (player.Magazine[(int)rule.Id] >= rule.MagazineSize)
        {
            result.Outcome = CombatOutcome.MagazineFull;
            return result;
        }

        if (player.Reserve[(int)rule.Id] <= 0)
        {
            result.Outcome = CombatOutcome.NoReserve;
            return result;
        }

        player.Reloading = true;
        player.ReloadingWeapon = rule.Id;
        player.ReloadEndTick = tick + SecondsToTicks(rule.ReloadSeconds);
        result.Outcome = CombatOutcome.Accepted;
        result.ReloadEndTick = player.ReloadEndTick;
        return result;
    }

    private void ResolveMelee(IEntity attacker, WeaponRule rule, Vector2 origin, Vector2 aim, uint tick, List<CombatHit> hits)
    {
        foreach (var target in _registry.All)
        {
            if (target.NetId == attacker.NetId || !target.IsAlive) continue;

            var targetCenter = new Vector2(target.X, target.Y);
            if (!CombatGeometry.InMeleeArc(origin, aim, rule.Range, rule.ArcDegrees, targetCenter, target.Radius)) continue;
            if (IsBlocked(origin, targetCenter, null)) continue;

            ApplyHit(attacker.NetId, target, rule.Damage, rule, tick, hits);
        }

        if (!rule.IsBreakableAxe) return;

        foreach (var obstacle in _obstacles)
        {
            if (!_breakables.IsBreakable(obstacle)) continue;

            var nearest = Vector2.Clamp(origin, new Vector2(obstacle.MinX, obstacle.MinY), new Vector2(obstacle.MaxX, obstacle.MaxY));
            if (!CombatGeometry.InMeleeArc(origin, aim, rule.Range, rule.ArcDegrees, nearest, 0f)) continue;
            if (IsBlocked(origin, nearest, obstacle)) continue;

            var damage = (int)MathF.Round(rule.Damage * WeaponRules.AxeBreakableMultiplier);
            _breakables.ApplyDamage(obstacle, damage, attacker.NetId);
        }
    }

    private void ResolveRanged(IEntity attacker, WeaponRule rule, Vector2 origin, Vector2 aim, uint tick, List<CombatHit> hits)
    {
        var pellets = rule.Pellets;
        var halfSpread = rule.SpreadDegrees * 0.5f;

        for (var i = 0; i < pellets; i++)
        {
            var offset = pellets == 1 ? 0f : -halfSpread + rule.SpreadDegrees * i / (pellets - 1);
            var dir = RotateDegrees(aim, offset);
            FireRay(attacker, rule, origin, dir, tick, hits);
        }
    }

    private void FireRay(IEntity attacker, WeaponRule rule, Vector2 origin, Vector2 dir, uint tick, List<CombatHit> hits)
    {
        var obstacleDistance = float.MaxValue;
        foreach (var obstacle in _obstacles)
        {
            if (CombatGeometry.RayHitsAabb(origin, dir, rule.Range, obstacle, out var d) && d < obstacleDistance)
                obstacleDistance = d;
        }

        IEntity? nearestTarget = null;
        var targetDistance = float.MaxValue;
        foreach (var target in _registry.All)
        {
            if (target.NetId == attacker.NetId || !target.IsAlive) continue;

            var center = new Vector2(target.X, target.Y);
            if (CombatGeometry.RayHitsCircle(origin, dir, rule.Range, center, target.Radius, out var d) && d < targetDistance)
            {
                targetDistance = d;
                nearestTarget = target;
            }
        }

        if (nearestTarget != null && targetDistance <= obstacleDistance)
            ApplyHit(attacker.NetId, nearestTarget, rule.Damage, rule, tick, hits);
    }

    private void ApplyHit(byte attackerNetId, IEntity target, int damage, WeaponRule rule, uint tick, List<CombatHit> hits)
    {
        if (damage <= 0 || !target.IsAlive) return;
        if (_effects.IsInvulnerable(target.NetId, tick)) return;

        var player = GetOrCreate(target.NetId);
        var clamped = (byte)Math.Min(damage, byte.MaxValue);
        var lethal = clamped >= target.Hp;

        if (lethal && !player.LifeSaveUsed && _passive.TryConsumeLifeSave(target.NetId))
        {
            player.LifeSaveUsed = true;
            target.Hp = LifeSaveHp;
            _effects.GrantInvulnerability(target.NetId, tick, LifeSaveInvulnerabilitySeconds);
            hits.Add(new CombatHit(target.NetId, 0, false));
        }
        else
        {
            _health.ApplyDamage(target.NetId, clamped, attackerNetId);
            hits.Add(new CombatHit(target.NetId, clamped, !target.IsAlive));
        }

        if (rule.StunSeconds > 0.0 && target.IsAlive)
            _effects.ApplyStun(target.NetId, tick, rule.StunSeconds);
    }

    private bool IsBlocked(Vector2 from, Vector2 to, AABB? exclude)
    {
        foreach (var obstacle in _obstacles)
        {
            if (exclude.HasValue && IsSameObstacle(exclude.Value, obstacle)) continue;
            if (CombatGeometry.SegmentIntersectsAabb(from, to, obstacle, out _)) return true;
        }

        return false;
    }

    private static bool IsSameObstacle(AABB a, AABB b) =>
        a.Name == b.Name && a.MinX == b.MinX && a.MaxX == b.MaxX && a.MinY == b.MinY && a.MaxY == b.MaxY;

    private static bool AcceptSequence(PlayerCombat player, ushort sequence)
    {
        if (player.HasSequence && unchecked((short)(sequence - player.LastSequence)) <= 0) return false;
        player.HasSequence = true;
        player.LastSequence = sequence;
        return true;
    }

    private void FinishReload(PlayerCombat player)
    {
        var index = (int)player.ReloadingWeapon;
        var rule = _rules.TryGet(player.ReloadingWeapon, out var found) ? found : null;
        if (rule != null)
        {
            var needed = rule.MagazineSize - player.Magazine[index];
            var moved = Math.Min(needed, player.Reserve[index]);
            player.Magazine[index] += moved;
            player.Reserve[index] -= moved;
        }

        player.Reloading = false;
    }

    private PlayerCombat GetOrCreate(byte netId)
    {
        if (_players.TryGetValue(netId, out var existing)) return existing;

        var created = new PlayerCombat();
        foreach (CombatWeaponId weapon in Enum.GetValues<CombatWeaponId>())
        {
            if (!_rules.TryGet(weapon, out var rule) || !rule.IsRanged) continue;
            created.Magazine[(int)weapon] = rule.MagazineSize;
            created.Reserve[(int)weapon] = rule.ReserveAmmo;
        }

        _players[netId] = created;
        return created;
    }

    private static uint SecondsToTicks(double seconds) => (uint)Math.Ceiling(seconds * TicksPerSecond - 1e-6);

    private static Vector2 RotateDegrees(Vector2 dir, float degrees)
    {
        var radians = degrees * MathF.PI / 180f;
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        return new Vector2(dir.X * cos - dir.Y * sin, dir.X * sin + dir.Y * cos);
    }
}
