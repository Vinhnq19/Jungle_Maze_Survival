# Tài liệu hvu — Netcode di chuyển (MOV-02, MOV-03)

Bộ tài liệu cho người **mới vào dự án**, chưa có kinh nghiệm làm game nhiều người chơi. Đọc theo thứ tự:

| # | Tài liệu | Đọc khi |
|---|---|---|
| 1 | [Kiến thức nền netcode](01_Kien_Thuc_Nen_Netcode.vi.md) | Chưa hiểu *tick, ack, prediction, interpolation, jitter* là gì |
| 2 | [MOV-02 — Dự đoán di chuyển nhân vật của mình](02_MOV-02_Prediction.vi.md) | Sửa cảm giác điều khiển, physics, gửi input |
| 3 | [MOV-03 — Hiển thị người chơi khác](03_MOV-03_Interpolation.vi.md) | Sửa cách vẽ người khác, spawn/despawn |
| 4 | [Hướng dẫn sử dụng & kiểm thử](04_Su_Dung_Va_Kiem_Thu.vi.md) | Chạy game, đọc overlay/log, chạy test, thêm tính năng |

## Tóm tắt một phút

```
           ┌──────────── Unity client ─────────────┐
 phím/chuột│ MatchSession ──input 30Hz──────────────┼──► Server (authoritative)
           │   │ RecordInput                        │      mô phỏng 30Hz
           │   ▼                                    │        │
           │ LocalPrediction (MOV-02)               │        │ WorldSnapshot 30Hz
           │   ▲ Reconcile(ack)  ◄──────────────────┼────────┘
           │   │                                    │
           │ RemotePlayerInterpolator (MOV-03) ◄────┤ PlayerStateUpdated
           │   │                                    │
           │ PlayerPoseSource: mình→prediction,     │
           │                   người khác→nội suy   │
           │   ▼                                    │
           │ PlayerView (LateUpdate) vẽ             │
           └────────────────────────────────────────┘
```

- **Nhân vật của mình** di chuyển ngay khi bấm (prediction), server vẫn là trọng tài; sai lệch được sửa mượt (reconciliation).
- **Người khác** được vẽ trễ 100 ms giữa hai snapshot (interpolation), ngoại suy tối đa 100 ms khi thiếu gói.
- Toàn bộ logic nằm ở lớp **Application** (C# thuần, không `UnityEngine`) nên test được trong EditMode với **fake authoritative server** giả lập RTT/jitter/mất gói/trùng gói/WSS.

## Đối chiếu tiêu chí "Hoàn thành khi" (25/10/2026)

| Tiêu chí | Bằng chứng | Trạng thái |
|---|---|---|
| MOV-02.1 Fake server: di chuyển/correction ổn định, không xuyên Hard Cover; có test input trễ, snapshot trùng/mất gói | `MovementNetcodeTests` 8 điều kiện mạng, `LocalPredictionTests`, `MovementSimulationTests` — [04 mục 4](04_Su_Dung_Va_Kiem_Thu.vi.md#4-kết-quả-đo-với-fake-server-25102026) | Đạt |
| MOV-02.2 Server thật: hai client chạy UDP và WSS/Auto; chặn UDP vẫn join qua WSS, không slot trùng | 4 lượt `local2-movement` PASS 2/2; log server `serverSlots=2/50` — [04 mục 7](04_Su_Dung_Va_Kiem_Thu.vi.md#7-kết-quả-chạy-với-server-thật) | Đạt trên một PC (loopback); hai PC vật lý chưa kiểm |
| MOV-02.3 Log RTT/correction ở 50/100 ms RTT, jitter 20 ms, loss 1/3; sai mã/đầy phòng báo lỗi đúng | Dòng `[Mov]` trong [evidence/mov-netcode-fake-server.log](evidence/mov-netcode-fake-server.log); `WrongRoomCode`, `RoomFull`, `Auto_WhenRoomFull_DoesNotFallback` | Đạt |
| MOV-03.1 Test fake snapshot: jitter, loss, packet trùng, mất spawn/despawn; không tích state cũ trên WSS | `RemoteInterpolationTests` (gồm WSS đứng 2 s rồi xả dồn) | Đạt |
| MOV-03.2 Tích hợp thật: hai client nhìn nhau di chuyển/xoay đúng, không ghost sau đổi phòng/rời trận | Check `mov03.interpolation` PASS 8/8 client; test MatchSession đổi trận/mất kết nối xóa entity | Đạt (ảnh chụp trong thư mục output của launcher) |
| MOV-03.3 Đo jitter/correction trên UDP và WSS theo cùng bộ thử MOV-02 | Cùng `MovementNetcodeTests` + cùng scenario online, cùng định dạng `[Mov]` | Đạt |

Test tự động: **EditMode 200/200, PlayMode 5/5 PASS** (Unity 6000.3.15f1 batchmode).

## Tài liệu liên quan của dự án

- [Client Developer Guide](../Client_Developer_Guide.vi.md) — cấu trúc client, cách chạy chung.
- [Network Architecture](../Network_Architecture.vi.md) — contract server/client.
- [Phase 2 — Match/Guest/Zone](../Phase2_Match_Guest_Zone.vi.md) — chạy server Local, bài test online.
- [Unity Foundation Acceptance](../Unity_Foundation_Acceptance.vi.md) — MOV-01, ENV-01, CLI-02.
