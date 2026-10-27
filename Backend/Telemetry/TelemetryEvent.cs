#nullable enable
using System;

namespace JungleMaze.Telemetry.Server;

/// <summary>Loại tín hiệu trong ring. 1–31: tín hiệu thô do observer chép từ MatchRuntime; 32+: H7 của chủ ticket gameplay.</summary>
public enum TelemetryKind : byte
{
    None = 0,
    Frame = 1,          // ReliableBroadcast: Ref = byte[] frame có sẵn
    Snapshot = 2,       // SnapshotGenerated: Ref = byte[] frame có sẵn (bulk)
    Damaged = 3,        // A = victim, B = source, C = hpAfter, I0 = damage
    Died = 4,           // A = victim, B = source
    GraceExpired = 5,   // Ref = sessionId
    PlayerDropped = 6,  // Ref = sessionId
    ResumeRotated = 7,  // A = netId, Ref = sessionId (token bị bỏ ngay ở handler)
    Disconnected = 8,   // H5: Ref = sessionId
    Left = 9,           // H5: Ref = sessionId
    ReplayAcked = 10,   // H6: Ref = sessionId

    Attack = 32,
    DamageDetail = 33,
    CcSpan = 34,
    LootDrop = 35,
    ItemPickup = 36,
    ItemUse = 37,
    EquipChange = 38,
    HideSpan = 39,
}

// Giá trị và thứ tự trùng `enums` trong Docs/Telemetry/telemetry.schema.v1.json.
public enum TelemetryAttackKind : byte { Melee, Shot, Throw, Special }
public enum TelemetryCcKind : byte { Stun, Root, Reverse }
public enum TelemetryCcEnd : byte { Expired, Cleansed, Death, MatchEnd }
public enum TelemetryPickupFrom : byte { Container, Floor, Corpse, Airdrop }
public enum TelemetryUseResult : byte { Done, Cancelled, Interrupted }
public enum TelemetryEquipAction : byte { Equip, Unequip, Drop, Break, Swap }
public enum TelemetrySlot : byte { Weapon, Helmet, Armor }
public enum TelemetryHideEnd : byte { Exit, Death, MatchEnd }

/// <summary>Một món trong loot_drop. Mảng chứa nó không được sửa sau khi gọi TryWrite.</summary>
public readonly struct TelemetryLootItem
{
    public readonly string Id;
    public readonly int Qty;
    public TelemetryLootItem(string id, int qty) { Id = id; Qty = qty; }
}

/// <summary>
/// Bản ghi thô đi qua ring: struct không cấp phát. Ref/Ref2 chỉ trỏ tới object có sẵn
/// (byte[] frame của MatchRuntime, chuỗi ID từ config, sessionId). Tick và Utc do sink đóng dấu.
/// </summary>
public struct TelemetryEvent
{
    public const byte NoNetId = 255;

    public TelemetryKind Kind;
    public byte A, B, C, D;
    public uint Tick;
    public long Utc;
    public int I0, I1, I2, I3;
    public float F0, F1;
    public object? Ref;
    public object? Ref2;

    /// <summary>Loại 4. ammo = -1 khi vũ khí không dùng đạn.</summary>
    public static TelemetryEvent Attack(byte netId, string weaponId, TelemetryAttackKind kind, float x, float y, int ammo = -1) =>
        new() { Kind = TelemetryKind.Attack, A = netId, B = (byte)kind, F0 = x, F1 = y, I0 = ammo, Ref = weaponId };

    /// <summary>Loại 5 (chi tiết). Gọi ngay trước ApplyDamage cùng tick; attacker = 255 khi không có.</summary>
    public static TelemetryEvent DamageDetail(byte victim, byte attacker, string? weaponId, string? hazardId,
        int raw, int absorbed, int helmetDur = -1, int armorDur = -1) =>
        new()
        {
            Kind = TelemetryKind.DamageDetail, A = victim, B = attacker, I0 = raw, I1 = absorbed,
            I2 = helmetDur, I3 = armorDur, Ref = weaponId, Ref2 = hazardId
        };

    /// <summary>Loại 7. Gọi khi khống chế kết thúc; endTick = tick lúc gọi.</summary>
    public static TelemetryEvent CcSpan(byte victim, byte source, string byId, TelemetryCcKind kind, uint startTick,
        uint plannedMs, TelemetryCcEnd end) =>
        new()
        {
            Kind = TelemetryKind.CcSpan, A = victim, B = source, C = (byte)kind, D = (byte)end,
            I0 = unchecked((int)startTick), I1 = unchecked((int)plannedMs), Ref = byId
        };

    /// <summary>Loại 9. byNetId = 255 khi không có người phá (airdrop).</summary>
    public static TelemetryEvent LootDrop(string containerId, byte byNetId, float x, float y, TelemetryLootItem[] items) =>
        new() { Kind = TelemetryKind.LootDrop, A = byNetId, F0 = x, F1 = y, Ref = containerId, Ref2 = items };

    /// <summary>Loại 10.</summary>
    public static TelemetryEvent ItemPickup(byte netId, string itemId, int qty, TelemetryPickupFrom from, float x, float y) =>
        new() { Kind = TelemetryKind.ItemPickup, A = netId, B = (byte)from, I0 = qty, F0 = x, F1 = y, Ref = itemId };

    /// <summary>Loại 11. Gọi khi lượt dùng kết thúc; endTick = tick lúc gọi.</summary>
    public static TelemetryEvent ItemUse(byte netId, string itemId, uint startTick, uint plannedMs, TelemetryUseResult result,
        byte hpBefore, byte hpAfter) =>
        new()
        {
            Kind = TelemetryKind.ItemUse, A = netId, B = (byte)result, C = hpBefore, D = hpAfter,
            I0 = unchecked((int)startTick), I1 = unchecked((int)plannedMs), Ref = itemId
        };

    /// <summary>Loại 12. fromId = null khi ô trước đó trống.</summary>
    public static TelemetryEvent EquipChange(byte netId, TelemetryEquipAction action, TelemetrySlot slot, string itemId, string? fromId) =>
        new() { Kind = TelemetryKind.EquipChange, A = netId, B = (byte)action, C = (byte)slot, Ref = itemId, Ref2 = fromId };

    /// <summary>Loại 13. Gọi khi đợt ẩn kết thúc; endTick = tick lúc gọi.</summary>
    public static TelemetryEvent HideSpan(byte netId, uint startTick, string bushId, float x, float y, TelemetryHideEnd end,
        bool attackedWhileHidden) =>
        new()
        {
            Kind = TelemetryKind.HideSpan, A = netId, B = (byte)end, C = attackedWhileHidden ? (byte)1 : (byte)0,
            I0 = unchecked((int)startTick), F0 = x, F1 = y, Ref = bushId
        };
}
