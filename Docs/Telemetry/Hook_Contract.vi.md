# Hợp đồng gọi telemetry (TEL-01)

Cập nhật 27/10/2026. Tài liệu này nói **ai ghi loại bản ghi nào, gọi ở đâu và trạng thái hiện tại**, cùng với API mà chủ các ticket gameplay dùng để ghi telemetry từ hệ thống của mình (hook H7). Trường và kiểu của từng loại xem [Schema.vi.md](Schema.vi.md). Các thay đổi cần làm trong GameServer của Vinh (H1, H5, H6) nằm riêng trong [GameServer_Hook_Proposal.vi.md](GameServer_Hook_Proposal.vi.md).

## 1. Ba giai đoạn

| Giai đoạn | Ai làm | Nội dung | Cần sửa GameServer? |
| --- | --- | --- | --- |
| **A** | TEL-01 | Schema, sink, bộ quan sát server (chạy trong `TelemetryChecks`), `perf_client`, `perflog`, event giả, báo cáo | Không |
| **B** | Vinh | H1 (gắn sink vào tiến trình), H5, H6 | Có, khoảng 10 dòng, Vinh tự commit |
| **C** | Chủ từng ticket gameplay | H7: một lời gọi `TryWrite` tại điểm quyết định | Có, trong code của chính ticket đó |

## 2. Bảng 18 loại: chủ, điểm gọi, trạng thái

Trạng thái tính tại cuối Phase A:

- **Có:** bộ ghi đã viết và đã kiểm.
- **Chờ H1:** đã chạy trong `TelemetryChecks`; tiến trình GameServer thật chỉ ghi ra khi Vinh gắn H1.
- **Chờ H5/H6:** cần event mới của Vinh.
- **Chờ hệ thống:** loại W, hệ thống gameplay chưa tồn tại. Tới lúc đó chỉ có dữ liệu giả.

| # | `type` | Chủ | Điểm gọi / nguồn tín hiệu | Nguồn | Trạng thái |
| --- | --- | --- | --- | --- | --- |
| 1 | `match_start` | TEL-01 | Frame `MatchPhasePacket` có `Phase = InGame` trong `ReliableBroadcast` | S0 | Chờ H1 |
| 2 | `match_end` | TEL-01 | `MatchResultPacket` trong `ReliableBroadcast` (không đọc `MatchResult` vì có `DisplayName`, `ProfileKey`) | S0 | Chờ H1 |
| 3 | `player_spawn` | TEL-01 | `MatchEntityEvent.Spawn`, roster ngay sau, snapshot đầu. `loadout`: ITM-01 ghi `equip_change` `EQUIP` cùng tick spawn, `loadout` để `[]` | S0 (`loadout` W) | Chờ H1 |
| 4 | `attack` | WPN-02 (đánh/bắn), WPN-03 (ném/đặc biệt) | Ngay khi server chấp nhận một lần đánh: `TelemetryEvent.Attack(...)` | W | Chờ hệ thống |
| 5 | `damage` | TEL-01 (thô); WPN-02 và hệ thống hazard (chi tiết) | Thô: `HealthService.EntityDamaged`. Chi tiết: `TelemetryEvent.DamageDetail(...)` **ngay trước** lời gọi `ApplyDamage` cùng tick | S0 thô, W chi tiết | Thô chờ H1; chi tiết chờ hệ thống |
| 6 | `elimination` | TEL-01 | Ghép `EntityDied` + `GraceExpired` + `MatchEntityEvent.Eliminated` + roster. `killer`/`weapon` lấy từ `damage` cuối; `hidden` lấy từ `hide_span` kết thúc `DEATH` cùng tick | S0 (`killer`, `weapon`, `hidden` W) | Chờ H1 |
| 7 | `cc_span` | WPN-02, WPN-03, ITM-01 | Khi một khống chế **kết thúc**: `TelemetryEvent.CcSpan(...)` | W | Chờ hệ thống |
| 8 | `zone_phase` | TEL-01 | `ZoneStatePacket` trong mọi snapshot, phát khi trạng thái bo đổi | S0 | Chờ H1 |
| 9 | `loot_drop` | ITM-01 (vật chứa, xác), ECONOMIC (airdrop) | Khi vật chứa vỡ hoặc airdrop đáp: `TelemetryEvent.LootDrop(...)` | W | Chờ hệ thống |
| 10 | `item_pickup` | ITM-01 | Khi server xác nhận nhặt: `TelemetryEvent.ItemPickup(...)` | W | Chờ hệ thống |
| 11 | `item_use` | ITM-01 | Khi lượt dùng **kết thúc** (xong, hủy, bị ngắt): `TelemetryEvent.ItemUse(...)` | W | Chờ hệ thống |
| 12 | `equip_change` | ITM-01, WPN-02 | Khi ô trang bị đổi: `TelemetryEvent.EquipChange(...)` | W | Chờ hệ thống |
| 13 | `hide_span` | Hệ thống bụi (Obstacles STT 9, chưa có ticket) | Khi một đợt ẩn **kết thúc**: `TelemetryEvent.HideSpan(...)` | W | Chờ hệ thống |
| 14 | `position_sample` | TEL-01 | Lấy 1 trong `30 / sampleHz` snapshot ở luồng nền. `weapon` lấy từ `equip_change` `WEAPON` gần nhất | S0 (`weapon` W) | Chờ H1 |
| 15 | `session_state` | TEL-01; Vinh (H5) | `RESUMED` ← `ResumeTokenRotated` sau khi đã vào InGame; `GRACE_EXPIRED` ← `GraceExpired`; `LOAD_TIMEOUT` ← `PlayerDropped` trong Loading; `DROPPED`/`LEFT` ← event H5 | S0 + S+ | Chờ H1; `DROPPED`/`LEFT` chờ H5 |
| 16 | `replay_choice` | TEL-01; Vinh (H6) | `REPLAY` ← event H6; `TIMEOUT`/`DISCONNECTED` ← frame phase Cleanup | S+ | Chờ H6 |
| 17 | `perf_server` | TEL-01 (`Tools/Telemetry` `perflog`) | Đọc dòng `[metrics]` của `ServerMetrics` từ log tiến trình | S0 | Có (không cần hook) |
| 18 | `perf_client` | TEL-01 (client, unity-dev) | `PerfClientRecorder` (ITickable) mỗi 5 s trong InGame, chỉ khi chạy với `-telemetry` | C | Có |

## 3. API cho chủ ticket (H7)

Mã nằm ở `Backend/Telemetry/` (namespace `JungleMaze.Telemetry.Server`). Sau H1, phần mã này được biên dịch cùng GameServer.

```csharp
public interface ITelemetrySink
{
    bool Enabled { get; }      // false khi telemetry tắt: có thể bỏ qua việc chuẩn bị tham số
    long Dropped { get; }      // số bản ghi bị bỏ vì hàng đợi đầy
    bool TryWrite(in TelemetryEvent e);   // false khi tắt hoặc đầy; không bao giờ ném lỗi
}

public sealed class NullTelemetrySink : ITelemetrySink   // mặc định: Enabled = false, TryWrite trả false
{
    public static readonly NullTelemetrySink Instance;
}
```

**Lấy sink ở đâu:** mỗi `MatchRuntime` có một sink riêng, do `TelemetryHost.Attach(room)` gắn. Cách nối (H7-prep trong đề xuất cho Vinh): `MatchRuntime` implement `ITelemetryAware` với `ITelemetrySink Telemetry { get; set; }`, mặc định là `NullTelemetrySink.Instance`. Hệ thống gameplay đọc `match.Telemetry` khi khởi tạo. Khi chưa ai gắn, mọi lời gọi rơi vào sink rỗng và không tốn gì.

**Tick và trận do sink đóng dấu.** Lời gọi không truyền `tick`, `matchId` hay `seq`. Sink đọc `match.CurrentTick` lúc nhận; luồng nền đóng dấu `matchId`, `seq` và envelope.

### 3.1. Các factory

Mọi factory là `static` trên `TelemetryEvent` và trả struct. Không có cấp phát nào.

| Loại | Factory |
| --- | --- |
| 4 | `Attack(byte netId, string weaponId, TelemetryAttackKind kind, float x, float y, int ammo = -1)` |
| 5 | `DamageDetail(byte victim, byte attacker, string? weaponId, string? hazardId, int raw, int absorbed, int helmetDur = -1, int armorDur = -1)` |
| 7 | `CcSpan(byte victim, byte source, string byId, TelemetryCcKind kind, uint startTick, uint plannedMs, TelemetryCcEnd end)` |
| 9 | `LootDrop(string containerId, byte byNetId, float x, float y, TelemetryLootItem[] items)` |
| 10 | `ItemPickup(byte netId, string itemId, int qty, TelemetryPickupFrom from, float x, float y)` |
| 11 | `ItemUse(byte netId, string itemId, uint startTick, uint plannedMs, TelemetryUseResult result, byte hpBefore, byte hpAfter)` |
| 12 | `EquipChange(byte netId, TelemetryEquipAction action, TelemetrySlot slot, string itemId, string? fromId)` |
| 13 | `HideSpan(byte netId, uint startTick, string bushId, float x, float y, TelemetryHideEnd end, bool attackedWhileHidden)` |

**Quy ước tham số:**

- **Giá trị "không có":** `byte` = 255 nghĩa là không có (`attacker`, `source`, `byNetId`); `int` = −1 nghĩa là `null` (`ammo`, `helmetDur`, `armorDur`).
- **Tick kết thúc** của `CcSpan`, `ItemUse`, `HideSpan` là tick lúc gọi. Phải gọi đúng lúc khoảng đó kết thúc.
- **`TelemetryLootItem`** là `readonly struct (string Id, int Qty)`. Mảng `items` tối đa 8 phần tử và **không được sửa** sau khi gọi, vì luồng nền đọc nó sau. Nên dùng mảng tạo một lần khi sinh nội dung vật chứa.
- **Enum:** `TelemetryAttackKind`, `TelemetryCcKind`, `TelemetryCcEnd`, `TelemetryPickupFrom`, `TelemetryUseResult`, `TelemetryEquipAction`, `TelemetrySlot`, `TelemetryHideEnd` có đúng các giá trị của `enums` trong schema, cùng thứ tự.

### 3.2. Quy tắc bắt buộc ở điểm gọi

Mục 2.7 của spec và review sẽ kiểm các quy tắc này.

1. **Không cấp phát trên đường gọi.** Chuỗi ID phải là tham chiếu sẵn có lấy từ config (ví dụ `weapon.Id`). Không nội suy, không `ToString()`, không `new` mảng cho mỗi lần gọi.
2. **Không I/O, không khóa, không chờ.** `TryWrite` chỉ chép struct vào ring có giới hạn.
3. **Không xử lý giá trị trả về `false`.** Hàng đầy thì bỏ bản ghi và đếm. Không thử lại, không ghi log trên luồng tick.
4. **Không đưa PII vào struct.** Không tên hiển thị, `RoomCode`, token hay mã tài khoản; định danh người chơi chỉ là `netId`.
5. **Gọi tại điểm server quyết định**, không gọi ở nơi client yêu cầu. Lần đánh bị từ chối thì không ghi `attack`.
6. **`DamageDetail` phải gọi ngay trước `ApplyDamage`** gây ra nó, trong cùng tick. Bộ ghép ghép chi tiết với `EntityDamaged(victim, source)` kế tiếp, nên mỗi lần trúng chỉ ra **một** bản ghi `damage`. Có chi tiết mà không có `EntityDamaged` theo sau (ví dụ giáp chặn hết) thì vẫn ghi `damage` với `applied = 0`.
7. **Không gọi cho loại do TEL-01 tự suy ra** (1, 2, 3, 6, 8, 14, 15, 16), kể cả khi hệ thống của bạn biết sự kiện đó. Bộ quan sát đã có, gọi thêm sẽ tạo bản trùng.

### 3.3. Ví dụ (WPN-02)

```csharp
// Trong hệ thống chiến đấu, sau khi server xác nhận cú đánh hợp lệ:
_telemetry.TryWrite(TelemetryEvent.Attack(attacker.NetId, weapon.Id, TelemetryAttackKind.Melee, attacker.X, attacker.Y));

foreach (var target in hits)
{
    _telemetry.TryWrite(TelemetryEvent.DamageDetail(target.NetId, attacker.NetId, weapon.Id, null,
        raw: weapon.Damage, absorbed: blocked, helmetDur: helmet?.Durability ?? -1, armorDur: armor?.Durability ?? -1));
    _health.ApplyDamage(target.NetId, (byte)(weapon.Damage - blocked), attacker.NetId);
}
```

## 4. Kiểm sau khi gắn H7

1. Chạy `dotnet run --project Backend/Tests/TelemetryChecks`: phải xanh.
2. Chạy một trận cục bộ với `TELEMETRY_PATH` đặt, rồi `python -I Tools/Telemetry/telemetry.py validate --strict <file>` trên file server: 0 lỗi.
3. Chạy `report`: cột độ phủ (`coverage.csv`) của loại vừa gắn chuyển từ `fake-only` sang `real` (AC39).
4. Ghi vào ticket của mình: loại nào đã gắn, ở hàm nào.
