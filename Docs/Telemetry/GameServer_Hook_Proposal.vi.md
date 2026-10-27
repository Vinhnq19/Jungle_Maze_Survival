# Đề xuất hook GameServer cho TEL-01 (gửi Vinh duyệt)

Cập nhật 27/10/2026. Người gửi: TEL-01 (PIC V.Huy). Người duyệt: **Vinh** (chủ `Backend/GameServer/**`).

TEL-01 **không sửa** file nào trong `Backend/GameServer/**`. Tài liệu này chỉ là đề xuất: Vinh đọc, chỉnh nếu cần, rồi tự commit. Phase A của TEL-01 (schema, sink, công cụ báo cáo, client) chạy được mà không cần các hook này, nên duyệt chậm không chặn ticket. Các hook chỉ mở khóa phần "nguồn thật" (AC33 trên tiến trình thật, AC36, AC37).

Bối cảnh: [TEL-01](../Tickets/TEL-01.md) mục 2.7 và 4.7, [Hook_Contract.vi.md](Hook_Contract.vi.md), [Schema.vi.md](Schema.vi.md).

## 1. Tóm tắt

| Hook | Sửa ở đâu | Số dòng | Mở khóa | Bắt buộc? |
| --- | --- | --- | --- | --- |
| H1 | `GameServer.csproj`, `Program.cs` | ~5 | Toàn bộ telemetry server trong tiến trình thật (AC33, AC36) | Có, cho Phase B |
| H5 | `MatchRuntime.cs`: 2 event | ~6 | `session_state` `DROPPED`, `LEFT` (AC37) | Nên có |
| H6 | `MatchRuntime.cs`: 1 event | ~3 | `replay_choice` (AC37) | Nên có |
| H7-prep | `MatchRuntime.cs`: implement `ITelemetryAware` | ~2 | Cho WPN-02/ITM-01/WPN-03 ghi telemetry (H7) | Khi chủ ticket đầu tiên cần |
| Tùy chọn | `JungleBackend.sln`; `ApplyDamage` có `cause` | — | Chạy `TelemetryChecks` trong sln; giảm rủi ro R7 | Không |

**H2, H3, H4 không cần sửa GameServer:**

- H2 và H3 nằm trong `MatchTelemetryObserver` của TEL-01. Bộ quan sát đăng ký các event public sẵn có: `ReliableBroadcast`, `SnapshotGenerated`, `MatchCompleted`, `PlayerDropped`, `GraceExpired`, `ResumeTokenRotated`, `Health.EntityDamaged/EntityDied`.
- H4 chỉ đọc log `[metrics]`.

## 2. Bảo đảm cho tick 30 Hz

Mọi handler của TEL-01 trên luồng tick chỉ chép một `struct` vào ring MPSC có giới hạn. Không cấp phát, không khóa, không I/O, không ném lỗi.

- **Ring và luồng ghi:** có hai ring. Ring critical (16 384 ô) giữ vòng đời trận, damage, chết, session; ring bulk (8 192 ô) giữ snapshot. Ring đầy thì bỏ bản ghi và đếm (`droppedQueue` trong `_trailer`), không chặn tick. Giải mã frame, dựng JSON và ghi file chạy ở một `Thread` nền riêng.
- **Dữ liệu được đọc:** handler giữ tham chiếu tới `byte[]` frame mà `MatchRuntime` đã cấp sẵn; TEL-01 không đọc lại `MatchRuntime` ở luồng nền. Bộ ghép **không đọc** `DisplayName`, `ProfileKey`, `RoomCode` hay resume token. Handler `ResumeTokenRotated` bỏ tham số token ngay, không đưa token vào struct.
- **Mặc định tắt:** không đặt `TELEMETRY_PATH` thì `TelemetryHost` không tạo thư mục, không tạo luồng, không đăng ký event nào. Đặt `PUBLIC_HOSTING=true` thì telemetry bị từ chối và in một dòng cảnh báo (G-9).
- **Lỗi đĩa** (thư mục mất, chỉ đọc, đầy): sink tự tắt và in đúng một dòng `[telemetry-error]`. Tick không bị ảnh hưởng.
- **Bằng chứng:** `dotnet run --project Backend/Tests/TelemetryChecks -- --bench` (p99 `TryWrite`, 0 byte cấp phát) và `-- --tick-budget` (so `tickP95Ms` bật/tắt bằng chính `ServerMetrics`). Kết quả nằm ở `Docs/Reports/TEL-01_<ngày>/`.

## 3. H1: gắn telemetry vào tiến trình

**`Backend/GameServer/GameServer.csproj`:** nhúng mã nguồn telemetry, giống cách đang nhúng `Shared/Protocol`.

```diff
     <Compile Include="../Shared/Protocol/**/*.cs" LinkBase="Protocol" />
+    <Compile Include="../Telemetry/**/*.cs" LinkBase="Telemetry" />
```

**`Backend/GameServer/Program.cs`:**

```diff
 using GameServer.Rooms;
+using JungleMaze.Telemetry.Server;
 ...
     outbox.Start();
+    using var telemetry = TelemetryHost.FromEnvironment();

     void HookRoomMatch(RoomManager.Room room)
     {
         room.Match.MatchCompleted += result => outbox.TryEnqueue(result);
+        telemetry.Attach(room);
     }
 ...
         using var outboxDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
         await outbox.StopAsync(outboxDeadline.Token);
+        telemetry.Dispose(); // flush tối đa 3 s, ghi _trailer; gọi lại lần nữa ở using là no-op
     }
```

- **Phạm vi:** `Attach` gắn một `MatchTelemetryObserver` cho mỗi phòng; phòng tạo sau qua `RoomCreated` cũng được gắn. Bộ quan sát tự gắn lại `Health` khi `HealthService` được tạo mới ở Cleanup (rủi ro R3).
- **Biến môi trường:** tất cả là tùy chọn và chỉ có tác dụng khi có `TELEMETRY_PATH`.

  | Biến | Mặc định | Ý nghĩa |
  | --- | --- | --- |
  | `TELEMETRY_PATH` | không đặt = tắt | Thư mục gốc; file nằm ở `<path>/<yyyy-MM-dd>/<matchId>.server.<wid>.jsonl` |
  | `TELEMETRY_SAMPLE_HZ` | 1 | Tần suất `position_sample`, kẹp trong 0,5–2 |
  | `TELEMETRY_MAX_FILE_MIB` | 8 | Trần mỗi file |
  | `TELEMETRY_MAX_TOTAL_MIB` | 256 | Trần tổng của tiến trình |

- **Sau khi H1 vào:** TEL-01 làm commit B1, xóa dòng nhúng `../../Telemetry/**/*.cs` trong `Backend/Tests/TelemetryChecks/TelemetryChecks.csproj`. Lúc đó các kiểu đã có trong `GameServer.dll`; giữ dòng này sẽ báo trùng kiểu. **H1 và B1 nên vào cùng ngày** để `TelemetryChecks` không bị vỡ build ở giữa.
- **Kiểm sau H1:**
  1. `Backend/Tests/run_all.cmd` vẫn xanh khi không đặt `TELEMETRY_PATH` (AC27).
  2. Chạy GameServer với `TELEMETRY_PATH` cùng `NetSmokeClient`, rồi chạy `validate --strict` trên file sinh ra.

## 4. H5: tín hiệu mất kết nối và rời trận

Hiện `MarkDisconnected` chỉ đặt cờ, không phát ra ngoài; snapshot không mang cờ này. `ProcessLeave` cũng không có event riêng. Đề xuất cho `Backend/GameServer/Simulation/MatchRuntime.cs`:

```diff
     public event Action<string>? GraceExpired;
+    public event Action<string>? PlayerDisconnected; // InGame: kết nối -> mất kết nối, bắt đầu grace
+    public event Action<string>? PlayerLeft;         // ProcessLeave thành công
 ...
     public bool ProcessLeave(string sessionId, Guid matchId)
     {
         ...
         BroadcastEntityEvent(entity.NetId, MatchEntityEvent.Eliminated);
         BroadcastRoster();
+        PlayerLeft?.Invoke(sessionId);
         return true;
     }
 ...
         if (Phase == MatchPhase.InGame)
         {
+            var wasConnected = !entity.IsDisconnected;
             // Preserve character + slot for 15s grace
             entity.IsDisconnected = true;
             ...
             entity.MoveY = 0;
+            if (wasConnected) PlayerDisconnected?.Invoke(sessionId);
         }
```

- **Chữ ký:** `Action<string>` (sessionId), giống `PlayerDropped` và `GraceExpired`.
- **Cách TEL-01 đăng ký:** tìm event theo **tên** bằng reflection, **một lần lúc `Attach`**, không chạy trên luồng tick. Vì vậy TEL-01 không cần commit thêm khi H5 vào. Vinh chỉ cần giữ đúng tên và chữ ký; đổi tên thì báo TEL-01.
- **Tùy chọn:** truyền thêm transport (`Udp`/`Wss`) để điền `session_state.transport`. Chưa có thì trường này ghi `null`.

## 5. H6: tín hiệu chọn chơi lại

```diff
+    public event Action<string>? ReplayAcked;
 ...
     public bool ProcessReplayAck(string sessionId, Guid matchId)
     {
         if (matchId != MatchId || !_bySession.TryGetValue(sessionId, out var entity))
             return false;

+        var first = !entity.ReplayAcked;
         entity.ReplayAcked = true;
+        if (first) ReplayAcked?.Invoke(sessionId);
         return true;
     }
```

TEL-01 tự suy ra `TIMEOUT` và `DISCONNECTED` khi hết Ending, từ frame phase Cleanup và các event H5. Cách đăng ký giống H5: theo tên, lúc `Attach`.

## 6. H7-prep: cho hệ thống gameplay ghi telemetry

Khi WPN-02, ITM-01, WPN-03 hay hệ thống bụi cần ghi `attack`, `damage` chi tiết, `cc_span`, loot…, họ cần một sink gắn với trận. Đề xuất cho `MatchRuntime.cs`:

```diff
-public sealed class MatchRuntime : IMatchRuntime, IServerClock, IEntityRegistry
+public sealed class MatchRuntime : IMatchRuntime, IServerClock, IEntityRegistry, JungleMaze.Telemetry.Server.ITelemetryAware
 {
+    public JungleMaze.Telemetry.Server.ITelemetrySink Telemetry { get; set; } = JungleMaze.Telemetry.Server.NullTelemetrySink.Instance;
```

- **Cách gắn sink:** `TelemetryHost.Attach` kiểm `room.Match is ITelemetryAware` và gán sink của phòng. Chưa có H7-prep thì phép kiểm này trả false và không có gì xảy ra.
- **Phụ thuộc:** H7-prep cần H1 trước, vì `ITelemetryAware` nằm trong mã telemetry được nhúng.
- **Thời điểm:** làm khi chủ ticket gameplay đầu tiên cần, không cần ngay. Quy tắc gọi xem [Hook_Contract.vi.md](Hook_Contract.vi.md) mục 3.2.

## 7. Tùy chọn

- **Thêm `Backend/Tests/TelemetryChecks/TelemetryChecks.csproj` vào `JungleBackend.sln`** và một dòng trong `Backend/Tests/run_all.cmd`. TEL-01 không tự thêm vì hai file này dùng chung.
- **Truyền nguyên nhân rõ ràng vào `ApplyDamage`.** Hiện chết do mất kết nối và rời trận dùng nguồn = chính nạn nhân, bo dùng 255. TEL-01 phân loại bằng `GraceExpired` cùng tick (rủi ro R7). Một tham số `cause` sẽ bỏ được suy luận này. Không gấp.

## 8. Ô duyệt

Trạng thái mỗi hook điền một trong: Duyệt, Duyệt có sửa (ghi rõ), Không duyệt (ghi lý do).

| Hook | Trạng thái | Commit | Ghi chú của Vinh |
| --- | --- | --- | --- |
| H1 | Chờ duyệt | | |
| H5 | Chờ duyệt | | |
| H6 | Chờ duyệt | | |
| H7-prep | Chờ duyệt | | |
| Tùy chọn (sln, cause) | Chờ duyệt | | |

Ký duyệt: ____________ (Vinh) · Ngày: ____________
