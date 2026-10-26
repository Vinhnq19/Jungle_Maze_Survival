# 02 — MOV-02: Kết nối Unity và dự đoán di chuyển nhân vật

Đọc [01 — Kiến thức nền](01_Kien_Thuc_Nen_Netcode.vi.md) trước nếu chưa quen các từ *tick, ack, prediction, reconciliation*.

## 1. MOV-02 làm được gì

- Unity gửi `PlayerInputPacket` (bọc trong `MatchInputPacket`) **30 lần/giây** với `Tick` và `InputSeq` tăng dần.
- Nhân vật của mình **di chuyển ngay khi bấm phím**, không chờ server.
- Mỗi snapshot server tới, client **đối chiếu** theo `LastProcessedInputSeq` (ack) và sửa sai **mượt**, không giật.
- Dự đoán dùng **đúng công thức va chạm của server** với Hard Cover ENV-01, nên không xuyên vật cản (cả vị trí logic lẫn hình vẽ).
- Kết nối: chế độ `Auto` thử UDP tối đa 5 giây, chỉ fallback sang WSS 443 khi **lỗi transport**; sai mã phòng / đầy phòng **báo lỗi luôn, không fallback**. Fallback giữ **cùng AttemptId** nên server không cấp hai slot; đã Joined thì không đổi transport. (Phần này đã có sẵn trong `NetworkClient` từ CLI-01; MOV-02 bổ sung test.)
- Số đo: overlay F3, dòng log `[Mov]` mỗi 5 giây, check tự động cho bài test online.

## 2. Bản đồ file

| File | Lớp | Vai trò |
|---|---|---|
| [MovementSimulation.cs](../../Assets/_JungleMaze/Scripts/Application/Player/MovementSimulation.cs) | Application | Bản sao **1:1** `PhysicsSimulation` của server: sweep hình tròn vs hộp AABB, trượt dọc tường, clamp biên map, đẩy ra khỏi vật cản. Kinematics dùng lại `HeroMotion` (MOV-01) |
| [LocalPrediction.cs](../../Assets/_JungleMaze/Scripts/Application/Player/LocalPrediction.cs) | Application | Lõi MOV-02: hàng chờ input, dự đoán, reconcile, làm mượt correction, đo ack RTT |
| [MatchSession.cs](../../Assets/_JungleMaze/Scripts/Application/Match/MatchSession.cs) | Application | Gửi input 30Hz; gọi `Prediction.RecordInput` sau mỗi lần gửi, `Prediction.Reconcile` khi snapshot của mình tới, `Reset` khi mất kết nối/đổi trận |
| [PlayerPoseSource.cs](../../Assets/_JungleMaze/Scripts/Application/Player/PlayerPoseSource.cs) | Application | Chọn nguồn vẽ: NetId của mình → prediction; còn lại → interpolation (MOV-03) |
| [MovementTelemetry.cs](../../Assets/_JungleMaze/Scripts/Application/Player/MovementTelemetry.cs) | Application | Log `[Mov]` định kỳ (RTT, correction, jitter) |
| [ClientRuntimeData.cs](../../Assets/_JungleMaze/Scripts/Infrastructure/Config/ClientRuntimeData.cs) | Infrastructure | Đọc map + `playerSpeed`/`playerRadius` từ `StreamingAssets/JungleMaze/runtime` sau khi kiểm hash khớp server |
| [MatchDemoView.cs](../../Assets/_JungleMaze/Scripts/Presentation/UI/MatchDemoView.cs) | Presentation | Khi map đã kiểm hash: `Prediction.SetWorld(...)`; ngắm chuột từ vị trí đã dự đoán |
| [PlayerView.cs](../../Assets/_JungleMaze/Scripts/Presentation/Player/PlayerView.cs) | Presentation | Vẽ theo `IPlayerPoseSource` trong `LateUpdate` |
| [Bootstrap.cs](../../Assets/_JungleMaze/Scripts/Bootstrap/Bootstrap.cs) | Bootstrap | Tạo `LocalPrediction`, truyền vào `MatchSession`, đăng ký `PlayerPoseSource`, `MovementTelemetry` |
| [Bootstrap.MovementChecks.cs](../../Assets/_JungleMaze/Scripts/Bootstrap/Bootstrap.MovementChecks.cs) | Bootstrap | Check `mov02.prediction` cho bài test online |

Quy tắc lớp (asmdef) của dự án vẫn giữ: Application không dùng `UnityEngine`, nên toàn bộ logic MOV-02 test được trong EditMode không cần scene.

## 3. Dòng chảy một frame

Thứ tự Unity chạy mỗi frame (đã cố định bằng `DefaultExecutionOrder`):

```
NetworkManager.Update (order -1000)
  └─ NetworkClient.Tick → nhận gói → MatchSession.Snapshot(...)
        ├─ với NetId của mình: Prediction.Reconcile(snapshot)      ← đối chiếu
        └─ PlayerModel.Apply + event PlayerStateUpdated (MOV-03 nghe)

TickRunner.Update
  ├─ MatchSession.Tick(dt)
  │     ├─ Prediction.Advance(dt)          ← làm mượt offset, tiến đồng hồ
  │     └─ mỗi 1/30 s: tạo PlayerInputPacket(Tick, ++InputSeq)
  │           → gửi qua IPacketSender
  │           → nếu gửi được: Prediction.RecordInput(input)  ← dự đoán ngay
  ├─ RemotePlayerInterpolator.Tick(dt)     (MOV-03)
  └─ MovementTelemetry.Tick(dt)            (log mỗi 5 s)

PlayerView.LateUpdate
  └─ IPlayerPoseSource.TryGetPose(netId) → đặt transform
```

## 4. Đọc code `LocalPrediction` từng bước

### 4.1 Trạng thái bên trong

- `_pending[128]`: **hàng đợi vòng** các input đã gửi nhưng server chưa ack. Mỗi phần tử: `Seq`, hướng đi đã **lượng tử hóa** (`MoveX/127f` — đúng giá trị server nhận), thời điểm gửi.
- `_x, _y`: vị trí **logic** sau input mới nhất. `_prevX, _prevY`: vị trí trước đó một tick.
- `_offsetX, _offsetY`: phần sai số đang được **làm mượt** (cộng vào hình vẽ, giảm dần về 0).
- `_hasAuthority`: đã nhận snapshot gốc đầu tiên chưa. `_dead`: server báo đã chết.

`IsActive = có map && có gốc server && còn sống`. Không active thì `TryGetPose` trả `false` và view tự lấy vị trí từ interpolation.

### 4.2 `RecordInput` — dự đoán ngay

```csharp
var mx = MovementSimulation.Dequantize(input.MoveX);   // đúng số server sẽ dùng
_pending[...] = new PendingInput { Seq = input.InputSeq, MoveX = mx, ... };
(_x, _y) = _world.Move(_x, _y, mx, my, TickSeconds);   // một bước 1/30 s
```

Điểm hay sai: phải dự đoán bằng giá trị **sau lượng tử** (sbyte −127..127), không phải float gốc từ bàn phím, nếu không sẽ lệch dần với server.

### 4.3 `Reconcile` — đối chiếu với server

1. Bỏ snapshot **trùng hoặc cũ hơn** snapshot đã dùng (UDP có thể đảo thứ tự).
2. Snapshot đầu tiên / đang chết → đặt thẳng theo server, xóa hàng chờ.
3. `DropAcknowledged(ack)`: bỏ mọi input có `Seq ≤ ack`. So sánh dùng `(short)(a − b)` để đúng cả khi `ushort` quay vòng 65535 → 1. `ack = 0` nghĩa là "server chưa xử lý input nào" (trận mới/resume) nên giữ nguyên hàng chờ; vì vậy `MatchSession` **không bao giờ gửi seq 0**.
4. Chạy lại các input còn lại từ vị trí server → vị trí dự đoán mới.
5. `error = |dự đoán cũ − dự đoán mới|`:
   - `> 2 cell` (`SnapDistance`): **Snap** — nhảy thẳng (respawn/resume, không phải lỗi mạng bình thường).
   - `> 0,01 cell`: **Correction** — cộng sai số vào `offset` và dời `prev` để hình **đứng yên tại chỗ** trong frame này, rồi `Advance` giảm offset theo hàm mũ với hằng số 0,1 s.

### 4.4 `TryGetPose` — hình để vẽ

```
pose = lerp(prev, cur, alpha) + offset      // alpha = thời gian từ input cuối / 33 ms
pose = world.ResolvePenetration(pose)       // không bao giờ vẽ lấn Hard Cover
rotation = hướng ngắm của input cuối        // xoay phản hồi ngay
```

`lerp(prev, cur)` giúp hình mượt ở 60–144 FPS dù vị trí logic chỉ đổi 30 lần/giây.

### 4.5 Vì sao vẫn có correction nhỏ dù mạng tốt?

Server chỉ giữ **input mới nhất** của mỗi người (không xếp hàng). Khi jitter làm hai input tới trong cùng một tick, server dùng input sau cho tick đó và input trước coi như "nhập" vào tick sau. Tổng quãng đường vẫn đúng nhưng **tạm lệch một bước** (≈0,13 cell) rồi tự khớp ở snapshot kế. Làm mượt 0,1 s che hoàn toàn việc này. Số liệu thực đo ở [04 — Hướng dẫn sử dụng & kiểm thử](04_Su_Dung_Va_Kiem_Thu.vi.md).

## 5. `MovementSimulation` — vì sao phải "chép" physics của server

Prediction chỉ đúng khi client tính **giống hệt** server. Nếu client dùng `Rigidbody2D` của Unity còn server dùng thuật toán sweep riêng, mỗi lần chạm tường hai bên sẽ dừng ở vị trí hơi khác nhau → correction liên tục.

Vì vậy `MovementSimulation` chép từng bước của `Backend/GameServer/Simulation/PhysicsSimulation.cs`:

1. Chuẩn hóa đường chéo, nhân tốc độ × dt (`HeroMotion.ComputeMoveDelta` — chung với MOV-01).
2. Tối đa 2 lần sweep: tìm thời điểm chạm sớm nhất với biên map hoặc Hard Cover (hộp nở thêm bán kính, góc hộp xử lý như hình tròn). Dừng sát điểm chạm, phần còn lại chiếu lên mặt tường để **trượt**.
3. Clamp vào map, `ResolvePenetration` đẩy ra nếu lấn.

Khác biệt duy nhất: bản client không cấp phát bộ nhớ (server tạo mảng 4 góc mỗi lần chạm). Có test `Move_DoesNotAllocate`.

> **Quy tắc bảo trì:** ai sửa `PhysicsSimulation.cs` ở server **phải sửa `MovementSimulation.cs`** và chạy lại `MovementSimulationTests`. Nếu không, prediction sẽ correction liên tục khi chạm vật cản.

Map, tốc độ và bán kính lấy từ `IClientRuntimeData` **sau khi** `Verify(version, hash)` khớp với `MatchPhasePacket` của server — client không tự đoán map.

## 6. Nối vào scene

`MatchDemoView.Update`:

- Pha `Loading`: kiểm hash + geometry scene → `UsePredictionWorld()` → `ConfirmLoaded()`.
- Vào thẳng `InGame` (resume sau mất mạng, bỏ qua Loading): vẫn kiểm hash rồi `UsePredictionWorld()`, không gửi LoadAck.
- Ngắm chuột từ pose đã dự đoán (`IPlayerPoseSource`) để hướng chuột khớp với nhân vật người chơi đang nhìn thấy.

Trong demo mạng, `HeroController` (MOV-01) vẫn bị tắt như trước và chỉ làm "proxy" cho camera; nhân vật của mình được vẽ bằng `PlayerView` lấy pose từ prediction. Không bật lại `HeroController` khi chơi online — hai nơi cùng ghi một transform sẽ giật.

## 7. Lỗi thường gặp

| Triệu chứng | Nguyên nhân thường gặp | Cách kiểm |
|---|---|---|
| Overlay F3 ghi `Prediction: chờ` mãi | Chưa `SetWorld` (hash map lệch server) hoặc chưa có NetId/snapshot | Xem dòng lỗi dữ liệu trên panel demo; chạy công cụ đồng bộ runtime data |
| `sửa` (corrections) tăng rất nhanh khi đi sát tường | Physics client lệch server | So `MovementSimulation` với `PhysicsSimulation`; chạy `MovementSimulationTests` |
| `snap` > 0 khi không respawn | Server dịch nhân vật (resume, zone đẩy...) hoặc mất gói rất lâu | Xem log `[Mov]` và log server |
| `pending` tăng tới 128, `overflow` > 0 | Server không ack input (sai MatchId, bị chặn budget, mất kết nối một chiều) | Log server `ProcessInput`, overlay `Packets out/in` |

Đọc tiếp: [03 — MOV-03 Interpolation](03_MOV-03_Interpolation.vi.md).
