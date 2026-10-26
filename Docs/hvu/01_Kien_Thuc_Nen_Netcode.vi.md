# 01 — Kiến thức nền: vì sao cần prediction và interpolation

Tài liệu này dành cho người **chưa từng làm game nhiều người chơi**. Đọc xong bạn sẽ hiểu các từ hay gặp trong code MOV-02/MOV-03: *authoritative server, tick, input sequence, ack, snapshot, prediction, reconciliation, interpolation, extrapolation, RTT, jitter, packet loss*. Chưa cần mở code.

Mục lục bộ tài liệu: [README](README.vi.md).

---

## 1. Server là "trọng tài" (authoritative server)

Trong Lastaway, **server quyết định mọi thứ**: vị trí, máu, ai thắng. Client (Unity) **chỉ gửi ý định** của người chơi — "tôi đang bấm W và D, chuột chỉ hướng 45°" — chứ **không bao giờ gửi tọa độ**.

Lý do: nếu client tự báo "tôi đang ở (10, 20)", người gian lận chỉ cần sửa số là xuyên tường. Khi server tự tính vị trí từ input, client có hack cũng không đi nhanh hơn hay xuyên Hard Cover được.

```
 Client (Unity)                         Server (GameServer .NET)
 ───────────────                        ────────────────────────
 Bàn phím/chuột                          
     │  PlayerInputPacket (30 lần/giây)   
     └──────────────────────────────────► áp input, mô phỏng vật lý
                                           (va chạm Hard Cover, biên map)
     ◄──────────────────────────────────┘
        WorldSnapshotPacket (30 lần/giây): vị trí, hướng, máu của mọi người
```

## 2. Tick — nhịp tim của server

Server không chạy liên tục mà chạy theo **tick**: 30 tick mỗi giây (`GameplayProtocol.TickRate = 30`), mỗi tick dài 1/30 ≈ 33,3 ms. Mỗi tick server:

1. Lấy input mới nhất của từng người chơi.
2. Di chuyển mỗi nhân vật một bước (`PhysicsSimulation.Move`, 4 cell/giây → 0,133 cell mỗi tick).
3. Gửi một **snapshot** (ảnh chụp trạng thái) cho tất cả client.

Client cũng gửi input đúng 30 lần/giây (không phụ thuộc FPS màn hình).

## 3. Độ trễ mạng (RTT), jitter và mất gói

| Thuật ngữ | Nghĩa đơn giản | Ví dụ |
|---|---|---|
| **RTT** (round-trip time) | Thời gian gói đi từ client tới server **và quay về** | RTT 100 ms: bấm phím, 100 ms sau mới thấy server trả lời |
| **Jitter** | Độ trễ **không đều**: gói này 45 ms, gói sau 65 ms | Jitter 20 ms: mỗi gói lệch ±20 ms so với trung bình |
| **Packet loss** | Gói biến mất giữa đường (UDP không gửi lại) | Loss 1/3: cứ 3 gói mất 1 |
| **Gói trùng** | Một gói tới hai lần | Router gửi lại |
| **Đảo thứ tự** | Gói gửi sau lại tới trước | Do jitter lớn hơn khoảng cách giữa hai gói |

Hai đường truyền trong dự án:

- **UDP** (LiteNetLib): nhanh, nhưng có thể mất/đảo/trùng gói. Dùng mặc định.
- **WSS** (WebSocket trên cổng 443, chạy trên TCP): không mất, không đảo, nhưng khi mạng nghẽn thì gói **dồn lại** rồi tới một loạt (head-of-line blocking). Dùng khi mạng chặn UDP.

## 4. Vấn đề 1 — nhân vật của mình bị "ì" (giải bằng MOV-02 Prediction)

Nếu chỉ vẽ nhân vật theo snapshot của server, với RTT 100 ms thì **bấm phím xong 100 ms sau nhân vật mới nhúc nhích**. Cảm giác như điều khiển xe tải.

**Client-side prediction** (dự đoán phía client): khi gửi input đi, client **tự mô phỏng luôn** bằng đúng công thức của server, và vẽ nhân vật ở vị trí dự đoán. Người chơi thấy phản hồi tức thì.

Nhưng dự đoán có thể sai (gói mất, server bị chặn bởi thứ client chưa biết...). Nên cần **reconciliation** (đối chiếu):

```
Input:     #1  #2  #3  #4  #5        ← client gửi, đánh số thứ tự (InputSeq)
Client:    dự đoán ngay #1..#5 → vị trí P5

Snapshot server tới: "tôi đã xử lý tới input #3 (ack = 3), bạn đang ở S3"

Reconcile:
  1. Bỏ #1, #2, #3 khỏi hàng chờ (server đã xác nhận).
  2. Lấy S3 của server làm gốc (server luôn đúng).
  3. Chạy lại #4, #5 lên S3  → P5' (dự đoán mới).
  4. Nếu P5' ≠ P5 → có "correction" (sai số). Kéo hình về từ từ trong ~0,1 s
     để mắt không thấy giật; lệch quá 2 cell (hồi sinh/resume) thì nhảy thẳng.
```

`LastProcessedInputSeq` trong `PlayerStatePacket` chính là **ack** ở trên.

## 5. Vấn đề 2 — người chơi khác bị giật (giải bằng MOV-03 Interpolation)

Snapshot tới 30 lần/giây, màn hình vẽ 60+ lần/giây, và gói tới không đều (jitter, mất gói). Nếu đặt thẳng người chơi khác theo snapshot mới nhất, họ sẽ **nhảy cóc** và **khựng** mỗi khi mất gói.

**Snapshot interpolation**: client **cố tình vẽ người khác chậm 100 ms** so với snapshot mới nhất. Nhờ vậy, tại thời điểm vẽ gần như luôn có **hai snapshot kẹp hai bên**, chỉ việc nội suy tuyến tính ở giữa:

```
tick:      100      101      102      103      104(mới nhất)
            ●────────●────────●────────●────────●
                          ▲
                   renderTick = 104 − 3 = 101,4
                   → vẽ ở 40% đường từ snapshot 101 tới 102
```

- Mất gói 102? Vẫn còn 101 và 103 để nội suy qua lỗ hổng.
- Không còn snapshot nào mới hơn renderTick (mất nhiều gói liền)? **Ngoại suy** (extrapolation) theo vận tốc cuối, nhưng **tối đa 100 ms** rồi đứng yên chờ — đoán xa hơn dễ sai và kéo nhân vật xuyên tường.
- Đánh đổi: người khác luôn hiện **trễ ~100 ms** so với server. Đây là cách làm chuẩn của đa số game hành động.

## 6. Ai làm gì (ranh giới task)

| Task | Sở hữu | Không làm |
|---|---|---|
| **MOV-01** | Điều khiển Hero local: WASD chuẩn hóa đường chéo, aim mượt (`HeroMotion`, `HeroController`) | Không biết mạng |
| **ENV-01** | Grid map, Hard Cover, layer `HardCover` | — |
| **CLI-01** | Transport UDP/WSS/Auto, handshake, `NetworkClient` | Không có logic gameplay |
| **CLI-02** | Định dạng gói (`PlayerInputPacket`, `WorldSnapshotPacket`...), `PacketRegistry` | Không gửi/nhận |
| **MOV-02** | Gửi input theo tick/seq, **dự đoán** nhân vật của mình, **reconcile** theo ack/snapshot | Không vẽ người khác |
| **MOV-03** | **Nội suy** người chơi khác, spawn/despawn view theo NetId | Không sửa dự đoán của mình, không cấp NetId, không quyết định state |
| **SRV-03** | Server thật: xác thực input, mô phỏng authoritative | — |

Đọc tiếp: [02 — MOV-02 Prediction](02_MOV-02_Prediction.vi.md).
