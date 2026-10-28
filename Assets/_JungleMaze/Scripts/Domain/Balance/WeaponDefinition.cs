namespace JungleMaze.Domain.Balance
{
    /// <summary>Weapon family, matching the "Nhóm" column of GDD Sheet 08 (Weapons).</summary>
    public enum WeaponCategory : byte
    {
        Melee = 0,
        Ranged = 1,
        Thrown = 2,
        Special = 3
    }

    /// <summary>
    /// Stats that apply to some weapon categories but not others (e.g. magazine size only applies to Ranged).
    /// Nullable fields are left unset when the category does not use them.
    /// </summary>
    public struct WeaponStats
    {
        public float Damage;
        /// <summary>Null means "no cooldown" (e.g. single-charge items tracked by Charges instead).</summary>
        public float? CooldownSeconds;
        /// <summary>0 means the effect is centered on the wielder rather than aimed at a distance.</summary>
        public float Range;
        public float? ArcDegrees;
        public float? StunSeconds;
        public float? FireRatePerSecond;
        public float? ProjectileSpeed;
        /// <summary>Pellets per shot for spread weapons (e.g. Shotgun); Damage is per-pellet when set.</summary>
        public int? Pellets;
        public int? MagazineSize;
        public int? ReserveAmmo;
        public float? FuseSeconds;
        public float? AreaWidth;
        public float? AreaHeight;
        public int? Carry;
        public int? Charges;
        /// <summary>Seconds to finish a reload (R). The loader fills the default for Ranged weapons with a magazine.</summary>
        public float? ReloadSeconds;
        /// <summary>Total cone angle in degrees for multi-pellet weapons (e.g. Shotgun). The loader fills the default when Pellets is set.</summary>
        public float? SpreadDegrees;
    }

    /// <summary>
    /// Immutable balance definition for one weapon, loaded from StreamingAssets/JungleMaze/balance/weapons.json
    /// (source: GDD Sheet 08 Weapons). Pure data: combat logic that reads this lives elsewhere.
    /// </summary>
    public sealed class WeaponDefinition
    {
        public WeaponDefinition(string id, string displayName, WeaponCategory category, in WeaponStats stats)
        {
            Id = id;
            DisplayName = displayName;
            Category = category;
            Stats = stats;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public WeaponCategory Category { get; }
        public WeaponStats Stats { get; }

        /// <summary>Returns the first validation error, or null if the definition is usable.</summary>
        public string Validate()
        {
            if (string.IsNullOrWhiteSpace(Id)) return "Weapon id is required.";
            if (string.IsNullOrWhiteSpace(DisplayName)) return $"{Id}: displayName is required.";
            if (Stats.Damage < 0f) return $"{Id}: damage must be >= 0.";
            if (Stats.CooldownSeconds.HasValue && Stats.CooldownSeconds <= 0f) return $"{Id}: cooldownSeconds must be > 0 when set.";
            if (Stats.Range < 0f) return $"{Id}: range must be >= 0.";
            if (Stats.ReloadSeconds.HasValue && Stats.ReloadSeconds <= 0f) return $"{Id}: reloadSeconds must be > 0 when set.";
            if (Stats.SpreadDegrees.HasValue && (Stats.SpreadDegrees <= 0f || Stats.SpreadDegrees > 360f)) return $"{Id}: spreadDegrees must be in (0, 360].";
            return null;
        }

        public override string ToString() => $"WeaponDefinition({Id}, {Category}, dmg={Stats.Damage})";
    }
}
