using JungleBackend.Protocol.Gameplay;
using JungleMaze.Domain.Balance;

namespace JungleMaze.App.Combat
{
    /// <summary>
    /// Runtime view of one of the ten client weapons, built from its balance definition. Read-only for consumers;
    /// <see cref="WeaponController"/> changes magazine, reserve and cooldown.
    /// </summary>
    public sealed class CombatWeaponSlot
    {
        internal CombatWeaponSlot(CombatWeaponId identity, WeaponDefinition definition)
        {
            Identity = identity;
            Definition = definition;
            if (definition == null) return;

            var stats = definition.Stats;
            Category = definition.Category;
            Range = stats.Range;
            ArcDegrees = stats.ArcDegrees ?? 0f;
            StunSeconds = stats.StunSeconds ?? 0f;
            Pellets = stats.Pellets ?? 1;
            SpreadDegrees = stats.SpreadDegrees ?? 0f;
            ProjectileSpeed = stats.ProjectileSpeed ?? 0f;
            MagazineSize = stats.MagazineSize ?? 0;
            Magazine = MagazineSize;
            Reserve = stats.ReserveAmmo ?? 0;
            ReloadSeconds = stats.ReloadSeconds ?? 0f;

            if (Category == WeaponCategory.Ranged)
                CadenceSeconds = stats.FireRatePerSecond.HasValue && stats.FireRatePerSecond > 0f ? 1f / stats.FireRatePerSecond.Value : 0f;
            else
                CadenceSeconds = stats.CooldownSeconds ?? 0f;

            Available = (Category == WeaponCategory.Melee || Category == WeaponCategory.Ranged)
                        && CadenceSeconds > 0f
                        && (Category != WeaponCategory.Ranged || (MagazineSize > 0 && ReloadSeconds > 0f));
        }

        /// <summary>Wire id of this weapon.</summary>
        public CombatWeaponId Identity { get; }
        /// <summary>Null when balance has no entry for this weapon.</summary>
        public WeaponDefinition Definition { get; }
        /// <summary>False when balance is missing this weapon or its cadence/magazine/reload values are unusable.</summary>
        public bool Available { get; }
        public WeaponCategory Category { get; }
        public bool IsRanged => Category == WeaponCategory.Ranged;
        public float Range { get; }
        public float ArcDegrees { get; }
        public float StunSeconds { get; }
        public int Pellets { get; }
        public float SpreadDegrees { get; }
        public float ProjectileSpeed { get; }
        /// <summary>Seconds between shots (ranged, from fireRatePerSecond) or between swings (melee, from cooldownSeconds).</summary>
        public float CadenceSeconds { get; }
        public float ReloadSeconds { get; }
        public int MagazineSize { get; }
        public int Magazine { get; internal set; }
        /// <summary>Client-tracked until the server sends reserve (see WPN-01 open questions).</summary>
        public int Reserve { get; internal set; }
        public float CooldownRemaining { get; internal set; }
    }
}
