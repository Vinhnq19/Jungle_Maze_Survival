using System;
using JungleMaze.Domain.Balance;

namespace JungleMaze.Domain.Hud
{
    /// <summary>A worn helmet or armor piece with its name and durability already resolved from balance data.</summary>
    public readonly struct GearStatus
    {
        public readonly string ItemId;
        public readonly string DisplayName;
        public readonly float Durability;
        /// <summary>durabilityPoints of the item; 0 when unknown.</summary>
        public readonly float MaxDurability;

        public GearStatus(string itemId, string displayName, float durability, float maxDurability)
        {
            ItemId = string.IsNullOrEmpty(itemId) ? null : itemId;
            DisplayName = ItemId == null ? null : displayName;
            Durability = ItemId == null ? 0f : durability;
            MaxDurability = ItemId == null ? 0f : maxDurability;
        }

        public bool IsWorn => ItemId != null;

        public bool Equals(in GearStatus other) => ItemId == other.ItemId && DisplayName == other.DisplayName
                                                   && Durability == other.Durability && MaxDurability == other.MaxDurability;
    }

    /// <summary>The weapon currently held. <see cref="Definition"/> is null for an id missing from balance data.</summary>
    public readonly struct HeldWeaponStatus
    {
        public readonly string ItemId;
        public readonly string DisplayName;
        public readonly WeaponDefinition Definition;

        public HeldWeaponStatus(string itemId, string displayName, WeaponDefinition definition)
        {
            ItemId = itemId;
            DisplayName = displayName;
            Definition = definition;
        }

        public bool Equals(in HeldWeaponStatus other) =>
            ItemId == other.ItemId && DisplayName == other.DisplayName && ReferenceEquals(Definition, other.Definition);
    }

    /// <summary>Ammo of the held weapon: rounds in the magazine and in reserve.</summary>
    public readonly struct AmmoStatus
    {
        public readonly int InMagazine;
        public readonly int Reserve;

        public AmmoStatus(int inMagazine, int reserve)
        {
            InMagazine = inMagazine;
            Reserve = reserve;
        }
    }

    /// <summary>Hero position in world units (1 unit = 1 cell) and facing in degrees (0 = +X, counter-clockwise).</summary>
    public readonly struct HeroPose
    {
        public readonly float X, Y, RotationDegrees;

        public HeroPose(float x, float y, float rotationDegrees)
        {
            X = x;
            Y = y;
            RotationDegrees = rotationDegrees;
        }
    }

    /// <summary>Values shown under the minimap: zone countdown (ZON-01) and players alive (SRV-03).</summary>
    public readonly struct MatchStatusInfo
    {
        public readonly bool HasZoneCountdown;
        public readonly float ZoneSecondsRemaining;
        public readonly bool HasAlive;
        public readonly int Alive;
        /// <summary>Players at match start (the "Còn sống" denominator), sent by the source, not maxPlayers.</summary>
        public readonly int Total;

        public MatchStatusInfo(bool hasZoneCountdown, float zoneSecondsRemaining, bool hasAlive, int alive, int total)
        {
            HasZoneCountdown = hasZoneCountdown;
            ZoneSecondsRemaining = zoneSecondsRemaining;
            HasAlive = hasAlive;
            Alive = alive;
            Total = total;
        }
    }

    /// <summary>One quick-bar slot with its display name resolved. Empty when <see cref="ItemId"/> is null.</summary>
    public readonly struct InventorySlot
    {
        public readonly string ItemId;
        public readonly string DisplayName;
        public readonly int Count;

        public InventorySlot(string itemId, string displayName, int count)
        {
            var empty = string.IsNullOrEmpty(itemId) || count <= 0;
            ItemId = empty ? null : itemId;
            DisplayName = empty ? null : displayName;
            Count = empty ? 0 : count;
        }

        public bool IsEmpty => ItemId == null;

        public bool Equals(in InventorySlot other) =>
            ItemId == other.ItemId && DisplayName == other.DisplayName && Count == other.Count;
    }

    /// <summary>Kind of a timed status effect. One entry per kind. UI-01-P2 adds CC/buff kinds without changing ports.</summary>
    public enum EffectKind : byte
    {
        None = 0,
        Adrenaline = 1,
        Poison = 2,
        Slow = 3,
        Invisible = 4,
        Stun = 5,
        Root = 6,
        Reverse = 7,
        CcImmune = 8
    }

    /// <summary>A status effect currently on the local player.</summary>
    public readonly struct ActiveEffect
    {
        public readonly EffectKind Kind;
        /// <summary>Full duration; 0 means untimed (stays until removed).</summary>
        public readonly float DurationSeconds;
        public readonly float RemainingSeconds;

        public ActiveEffect(EffectKind kind, float durationSeconds, float remainingSeconds)
        {
            Kind = kind;
            DurationSeconds = durationSeconds;
            RemainingSeconds = remainingSeconds;
        }

        public bool IsTimed => DurationSeconds > 0f;
    }

    /// <summary>Which parts of the HUD state changed in one write. Pose has no flag: it is polled every frame.</summary>
    [Flags]
    public enum HudStateChange
    {
        None = 0,
        Hp = 1 << 0,
        MaxHp = 1 << 1,
        Armor = 1 << 2,
        Helmet = 1 << 3,
        HeldWeapon = 1 << 4,
        Ammo = 1 << 5,
        ZoneCountdown = 1 << 6,
        AliveCount = 1 << 7,
        All = Hp | MaxHp | Armor | Helmet | HeldWeapon | Ammo | ZoneCountdown | AliveCount
    }

    /// <summary>Which parts of the inventory state changed in one write.</summary>
    [Flags]
    public enum InventoryChange
    {
        None = 0,
        Slots = 1 << 0,
        ActiveSlot = 1 << 1,
        All = Slots | ActiveSlot
    }

    public enum EffectChangeType : byte
    {
        Added = 0,
        Refreshed = 1,
        Removed = 2
    }

    /// <summary>One change of the active effect list.</summary>
    public readonly struct EffectChange
    {
        public readonly EffectKind Kind;
        public readonly EffectChangeType Type;

        public EffectChange(EffectKind kind, EffectChangeType type)
        {
            Kind = kind;
            Type = type;
        }
    }
}
