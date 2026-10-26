# 03 — MOV-03: Hiển thị chuyển động của những người chơi khác

Đọc [01 — Kiến thức nền](01_Kien_Thuc_Nen_Netcode.vi.md) mục 5 trước nếu chưa quen *interpolation / extrapolation / jitter*.

## 1. MOV-03 làm được gì

- Nhận snapshot qua `IPacketReceiver` (interface của NetworkManager) → `MatchSession` → event `PlayerStateUpdated`.
- Giữ **buffer theo tick** cho từng người chơi (tối đa 10, NetId 0..9 do server cấp) và vẽ họ **trễ 100 ms** bằng nội suy vị trí + góc quay.
- Bỏ snapshot **trùng / quá cũ**, chèn đúng chỗ gói **đến sai thứ tự**.
- **Ngoại suy tối đa 100 ms** khi thiếu gói, sau đó đứng yên chờ.
- Đo **jitter** (độ lệch nhịp gói tới), số lần ngoại suy, gói trùng/cũ.
- Tạo/xóa view theo **spawn/despawn có NetId**; rời trận, đổi trận, mất kết nối → xóa sạch, **không có ghost entity**.
- Trên **WSS**: gói dồn sau khi nghẽn không bị "phát lại" chậm; buffer có giới hạn nên không tích state cũ.

Không làm: không sửa prediction của mình (MOV-02), không tự cấp NetId, không quyết định state (máu, chết...) — chỉ hiển thị cái server gửi.

## 2. Bản đồ file

| File | Vai trò |
|---|---|
| [SnapshotBuffer.cs](../../Assets/_JungleMaze/Scripts/Application/Player/SnapshotBuffer.cs) | Buffer vòng 32 snapshot của **một** người chơi, luôn sắp theo tick. Chèn, bỏ trùng/cũ, nội suy, ngoại suy có giới hạn, phát hiện dịch chuyển tức thời |
| [RemotePlayerInterpolator.cs](../../Assets/_JungleMaze/Scripts/Application/Player/RemotePlayerInterpolator.cs) | 10 buffer + **đồng hồ render** (ước lượng tick server − 100 ms). Nghe `PlayerStateUpdated`/`PlayerRemoved`. Đo jitter |
| [PlayerPoseSource.cs](../../Assets/_JungleMaze/Scripts/Application/Player/PlayerPoseSource.cs) | Người khác → interpolator. Chính mình → prediction (fallback interpolator khi prediction chưa chạy/đã chết) |
| [RemotePlayerSpawner.cs](../../Assets/_JungleMaze/Scripts/Presentation/Player/RemotePlayerSpawner.cs) | Lấy `PlayerView` từ pool khi có NetId mới, trả về pool khi `PlayerRemoved` |
| [PlayerView.cs](../../Assets/_JungleMaze/Scripts/Presentation/Player/PlayerView.cs) | Mỗi `LateUpdate` hỏi pose source và đặt transform; ẩn sprite khi pose báo đã chết |
| [Domain/Player/PlayerPose.cs](../../Assets/_JungleMaze/Scripts/Domain/Player/PlayerPose.cs) | Struct pose (X, Y, góc, chết) — thứ view cần để vẽ |

## 3. Đồng hồ render — phần khó nhất

Câu hỏi: "bây giờ server đang ở tick mấy?" Client không biết chính xác vì gói tới trễ và không đều. `RemotePlayerInterpolator` ước lượng như sau:

```
localTicks  += dt × 30                         // đồng hồ local, đều đặn
mỗi snapshot MỚI NHẤT tới với tick T:
    sample   = T − localTicks                  // "server đi trước local bao nhiêu"
    offset  += (sample − offset) × 0,05        // lọc trung bình mũ → bỏ dao động jitter
    jitter  += (|sample − offset| − jitter)/16 // giống công thức RFC 3550
renderTick  = localTicks + offset − 3          // 3 tick = 100 ms (DelaySeconds)
```

Ba quy tắc an toàn:

1. **Không bao giờ lùi**: nếu `renderTick` mới nhỏ hơn cũ thì giữ nguyên (đứng chờ) thay vì vẽ giật ngược.
2. **Resync khi lệch lớn** (> 15 tick = 0,5 s, ví dụ đường WSS hết nghẽn, đổi mạng): đo lại offset và **chỉ nhảy tới**, không tua lùi để phát lại state cũ.
3. **Timeline mới**: khi không còn ai trong buffer (đổi trận, reconnect) hoặc tick lùi hẳn > 1 s, đồng hồ đo lại từ đầu.

## 4. Lấy mẫu (`SnapshotBuffer.TrySample`)

```
renderTick nằm trước snapshot cũ nhất  → vẽ đúng snapshot cũ nhất (mới spawn)
renderTick nằm giữa A và B             → nội suy: X, Y tuyến tính; góc theo cung NGẮN
                                         (350° → 10° đi qua 0°, không quay ngược 340°)
                                         trạng thái chết lấy theo snapshot đã "xảy ra"
A→B cách nhau > 3 cell                 → coi là dịch chuyển tức thời (respawn): không kéo lê
renderTick vượt snapshot mới nhất      → ngoại suy theo vận tốc 2 snapshot cuối,
                                         tối đa 3 tick (100 ms) rồi đứng yên.
                                         Không ngoại suy người đã chết.
```

`Tick()` mỗi frame cắt bỏ snapshot không còn dùng (chỉ giữ một snapshot ≤ renderTick làm mốc). Buffer đầy 32 thì bỏ cái cũ nhất.

## 5. Spawn / despawn và chống ghost

Nguồn sự thật về "ai đang có mặt" là **snapshot của server**, không phải gói spawn/despawn riêng lẻ:

- `MatchSession.Snapshot`: NetId nào **không còn** trong snapshot → `PlayerRemoved` (vậy nên **mất gói despawn** vẫn xóa đúng).
- `MatchEntityEventPacket` Despawn (reliable) → `PlayerRemoved` sớm hơn.
- Đổi `MatchId`, mất kết nối → `ClearPlayers()` → `PlayerRemoved` cho tất cả.

`RemotePlayerInterpolator.Remove(netId)` xóa buffer, `RemotePlayerSpawner` trả view về pool. Lần spawn sau (rejoin/trận mới) bắt đầu buffer **rỗng**, nên view đặt thẳng tại vị trí mới — không trượt từ vị trí cũ qua nửa map.

## 6. Tinh chỉnh

| Tham số | Mặc định | Tăng thì | Giảm thì |
|---|---|---|---|
| `DelaySeconds` | 0,1 s | Chịu jitter/mất gói tốt hơn, nhưng người khác hiện trễ hơn | Phản hồi nhanh hơn, dễ phải ngoại suy |
| `MaxExtrapolationSeconds` | 0,1 s | Ít "khựng" khi mất gói liên tiếp, dễ đoán sai (xuyên tường tạm thời) | An toàn hơn, dễ thấy đứng hình |
| `SnapshotBuffer.Capacity` | 32 (~1 s) | Không cần | Phải ≥ (delay + jitter) × 30 |
| `TeleportDistance` | 3 cell | — | Có thể nhầm chạy nhanh thành dịch chuyển |

Quy tắc kinh nghiệm: `DelaySeconds ≥ 2 × khoảng cách tick + jitter lớn nhất` (33 ms × 2 + 20 ms ≈ 90 ms → 100 ms).

## 7. Lỗi thường gặp

| Triệu chứng | Nguyên nhân | Cách kiểm |
|---|---|---|
| Người khác **khựng** định kỳ | Thiếu snapshot (mất gói/nghẽn) quá giới hạn ngoại suy | F3: `ngoại suy x/y` cao; log `[Mov]` `clamped` > 0 |
| Người khác **giật lùi** | Không nên xảy ra (đồng hồ chỉ tiến) | Chạy `RemoteInterpolationTests`; báo lỗi kèm log |
| Còn hình người đã rời trận | Ghost: view không được trả pool | Check online `mov03.interpolation` sẽ FAIL kèm NetId |
| Trên WSS người khác **chậm dần** so với thực | Đồng hồ không resync | `bufferedMs` trong log phải ≈ 100 ms; > 250 ms là lỗi |

Đọc tiếp: [04 — Hướng dẫn sử dụng & kiểm thử](04_Su_Dung_Va_Kiem_Thu.vi.md).
