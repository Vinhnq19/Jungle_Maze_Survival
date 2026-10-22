namespace JungleMaze.Domain.Balance
{
    /// <summary>Equipment family, matching the "Type" column of GDD Sheet 09 (Equipments).</summary>
    public enum EquipmentType : byte
    {
        Helmet = 0,
        Armor = 1,
        Healing = 2,
        Support = 3,
        /// <summary>Only drops from the airdrop supply container ("Thính" in the GDD).</summary>
        Airdrop = 4
    }

    /// <summary>Numeric stats shared by most equipment entries; unused fields stay at their default.</summary>
    public struct EquipmentStats
    {
        /// <summary>Damage reduction, 0..1. Only set for Helmet/Armor.</summary>
        public float? DamageReductionPct;
        /// <summary>Durability points before the item breaks. Only set for Helmet/Armor.</summary>
        public float? DurabilityPoints;
        public float HealAmount;
        public float UseSeconds;
        public int MaxCarry;
    }

    /// <summary>
    /// Immutable balance definition for one equipment item, loaded from
    /// StreamingAssets/JungleMaze/balance/equipments.json (source: GDD Sheet 09 Equipments).
    /// <see cref="EffectDescription"/> is free-text for special behaviour (cleanse, invisibility, self-revive...)
    /// that has no dedicated status-effect system yet; it documents intent without implementing it.
    /// </summary>
    public sealed class EquipmentDefinition
    {
        public EquipmentDefinition(string id, string displayName, EquipmentType type, in EquipmentStats stats,
            string effectDescription)
        {
            Id = id;
            DisplayName = displayName;
            Type = type;
            Stats = stats;
            EffectDescription = effectDescription ?? "";
        }

        public string Id { get; }
        public string DisplayName { get; }
        public EquipmentType Type { get; }
        public EquipmentStats Stats { get; }
        public string EffectDescription { get; }

        /// <summary>Returns the first validation error, or null if the definition is usable.</summary>
        public string Validate()
        {
            if (string.IsNullOrWhiteSpace(Id)) return "Equipment id is required.";
            if (string.IsNullOrWhiteSpace(DisplayName)) return $"{Id}: displayName is required.";
            if (Stats.MaxCarry <= 0) return $"{Id}: maxCarry must be > 0.";
            if (Stats.UseSeconds < 0f) return $"{Id}: useSeconds must be >= 0.";
            if (Stats.DamageReductionPct.HasValue && (Stats.DamageReductionPct < 0f || Stats.DamageReductionPct > 1f))
                return $"{Id}: damageReductionPct must be in [0, 1].";
            return null;
        }

        public override string ToString() => $"EquipmentDefinition({Id}, {Type})";
    }
}
