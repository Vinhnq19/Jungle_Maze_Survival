namespace GameServer.Simulation.Combat;

/// <summary>Hiệu ứng trạng thái do Game-10 sở hữu. WPN-02 chỉ yêu cầu bất tử/khống chế, không tự ghi effect.</summary>
public interface IEffectService
{
    bool IsInvulnerable(byte netId, uint tick);
    void GrantInvulnerability(byte netId, uint tick, double seconds);
    void ApplyStun(byte netId, uint tick, double seconds);
}

/// <summary>Passive cứu mạng do ITM-01 sở hữu. Trả true nếu item đã được tiêu thụ và cứu người chơi khỏi chết.</summary>
public interface IItemPassive
{
    bool TryConsumeLifeSave(byte netId);
}

/// <summary>Vật phá được (ENV-02). WPN-02 chỉ báo sát thương đã xác nhận; ENV-02 giữ HP vật.</summary>
public interface IBreakableObstacles
{
    bool IsBreakable(AABB obstacle);
    void ApplyDamage(AABB obstacle, int damage, byte attackerNetId);
}

/// <summary>ITM owns weapon instances and ammunition. Combat never creates an online loadout.</summary>
public interface ICombatLoadout
{
    bool CanAttack(byte actor);
    uint EquipmentGeneration(byte actor);
    bool TryGetHeld(byte actor, out JungleBackend.Protocol.Gameplay.CombatWeaponId weapon);
    bool TryGetAmmo(byte actor, JungleBackend.Protocol.Gameplay.CombatWeaponId weapon, out int magazine, out int reserve);
    void WriteAmmo(byte actor, JungleBackend.Protocol.Gameplay.CombatWeaponId weapon, int magazine, int reserve);
    void AttackStarted(byte actor);
    int MitigateDamage(byte actor, int rawDamage);
}

/// <summary>Một lần trúng đã được server xác nhận, dùng để phát event và telemetry.</summary>
public readonly record struct CombatHit(byte TargetNetId, int Damage, bool Killed);
