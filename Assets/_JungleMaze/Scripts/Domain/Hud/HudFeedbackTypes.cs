namespace JungleMaze.Domain.Hud
{
    /// <summary>Semantic effect color and ordering.</summary>
    public enum EffectPolarity : byte { Harmful, Beneficial }
    /// <summary>Display rules, independent of gameplay effect simulation.</summary>
    public static class EffectKinds
    {
        public static EffectPolarity PolarityOf(EffectKind kind) => kind == EffectKind.Invisible || kind == EffectKind.CcImmune || kind == EffectKind.Adrenaline ? EffectPolarity.Beneficial : EffectPolarity.Harmful;
        public static bool ShowsAsChip(EffectKind kind) => kind != EffectKind.None && kind != EffectKind.Adrenaline;
    }
    /// <summary>Source-authoritative item action phase.</summary>
    public enum ItemUsePhase : byte { Idle, Using, Cancelled, Completed }
    /// <summary>Immutable snapshot of a source-owned item action.</summary>
    public readonly struct ItemUseStatus
    {
        public readonly ItemUsePhase Phase;
        public readonly string ItemId, DisplayName;
        public readonly int SlotIndex;
        public readonly float TotalSeconds, RemainingSeconds;
        public readonly bool Cancellable;
        public ItemUseStatus(ItemUsePhase phase, string id, string name, int slot, float total, float remaining, bool cancellable)
        { Phase = phase; ItemId = id; DisplayName = name; SlotIndex = slot; TotalSeconds = total; RemainingSeconds = remaining; Cancellable = cancellable; }
    }
    /// <summary>One accepted pickup, with display name resolved by the data owner.</summary>
    public readonly struct ItemPickup
    {
        public readonly string ItemId, DisplayName;
        public readonly int Count;
        public ItemPickup(string id, string name, int count) { ItemId = id; DisplayName = name; Count = count; }
    }
    /// <summary>Only Hero hits produce a hit marker.</summary>
    public enum HitTargetKind : byte { Hero, Other }
    /// <summary>One confirmed hit from combat.</summary>
    public readonly struct HitDealt { public readonly HitTargetKind Target; public HitDealt(HitTargetKind target) { Target = target; } }
    /// <summary>Direction is optional and never inferred by the HUD.</summary>
    public readonly struct DamageTaken
    {
        public readonly bool HasDirection;
        public readonly float DirectionDegrees;
        public DamageTaken(bool hasDirection, float directionDegrees) { HasDirection = hasDirection; DirectionDegrees = directionDegrees; }
    }
    /// <summary>Supported source-owned warnings.</summary>
    public enum DangerKind : byte { FusedBomb, Trap }
    /// <summary>Warning source coordinates use world cells, not a cached relative vector.</summary>
    public readonly struct DangerWarning
    {
        public readonly int Id;
        public readonly DangerKind Kind;
        public readonly float TotalSeconds, RemainingSeconds, SourceX, SourceY;
        public readonly bool HasSource;
        public bool IsTimed => TotalSeconds > 0f;
        public DangerWarning(int id, DangerKind kind, float total, float remaining, bool source, float x, float y)
        { Id = id; Kind = kind; TotalSeconds = total; RemainingSeconds = remaining; HasSource = source; SourceX = x; SourceY = y; }
    }
    /// <summary>Reason for a warning list change.</summary>
    public enum DangerChangeType : byte { Added, Refreshed, Removed, Expired }
    /// <summary>One warning change; readers retain snapshots for expiry animations.</summary>
    public readonly struct DangerChange
    {
        public readonly int Id;
        public readonly DangerChangeType Type;
        public DangerChange(int id, DangerChangeType type) { Id = id; Type = type; }
    }
}
