namespace GameServer.Simulation.Combat;

/// <summary>
/// Placeholder collaborators used until Game-10, ITM-01 and ENV-02 provide real ones.
/// They grant no effects, no life save and no breakable damage, so the server behaves as before for those systems.
/// </summary>
public static class CombatDefaults
{
    public static IEffectService Effects { get; } = new NoEffects();
    public static IItemPassive Passive { get; } = new NoPassive();
    public static IBreakableObstacles Breakables { get; } = new NoBreakables();

    private sealed class NoEffects : IEffectService
    {
        public bool IsInvulnerable(byte netId, uint tick) => false;
        public void GrantInvulnerability(byte netId, uint tick, double seconds) { }
        public void ApplyStun(byte netId, uint tick, double seconds) { }
    }

    private sealed class NoPassive : IItemPassive
    {
        public bool TryConsumeLifeSave(byte netId) => false;
    }

    private sealed class NoBreakables : IBreakableObstacles
    {
        public bool IsBreakable(AABB obstacle) => false;
        public void ApplyDamage(AABB obstacle, int damage, byte attackerNetId) { }
    }
}
