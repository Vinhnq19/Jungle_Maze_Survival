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

/// <summary>Một lần trúng đã được server xác nhận, dùng để phát event và telemetry.</summary>
public readonly record struct CombatHit(byte TargetNetId, int Damage, bool Killed);
