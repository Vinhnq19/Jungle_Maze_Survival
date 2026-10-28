# 04 — Hướng dẫn sử dụng & kiểm thử MOV-02 / MOV-03

Cập nhật **25/10/2026**, Unity **6000.3.15f1**. Đọc [README](README.vi.md) để biết thứ tự các tài liệu.

## 1. Chơi thử trong Editor

Không cần cấu hình gì thêm: Bootstrap đã nối prediction và interpolation.

1. Chạy server Local theo [Phase 2 — mục 1](../Phase2_Match_Guest_Zone.vi.md) (`MATCH_PROFILE=test` để 2 người là đủ bắt đầu trận).
2. Mở scene `Boot` → Play. Chọn profile `phase2-local`, nhập mã phòng, tên → vào phòng. Mở thêm một Windows player (hoặc player thứ hai) vào cùng phòng.
3. Khi trận `InGame`: WASD di chuyển, chuột xoay hướng.
4. Bấm **F3** (chỉ Editor/Development build) để mở overlay số đo:

```
MOV-02 ack 112 ms · pending 3 · sửa 41 · snap 0 · lệch avg 0.031/max 0.133
MOV-03 jitter 9.8 ms · trễ 100 ms · ngoại suy 12/2400 · trùng 0 · cũ 0
```

| Số | Ý nghĩa | Bình thường |
|---|---|---|
| `ack` | Từ lúc gửi input tới lúc server xác nhận | ≈ RTT + 0–33 ms |
| `pending` | Input đang chờ ack | ≈ RTT ÷ 33 ms (RTT 100 ms → 3–4) |
| `sửa` / `lệch` | Số lần và độ lớn correction (cell) | Có vài chục khi jitter; `max` ≲ 0,4 |
| `snap` | Số lần phải dịch thẳng | 0 (trừ resume/respawn) |
| `jitter` | Độ lệch nhịp snapshot tới | UDP LAN < 5 ms; Wi-Fi/WAN 5–30 ms |
| `ngoại suy x/y` | Số frame phải đoán vì thiếu snapshot | Nhỏ; tăng khi mất gói |

Dòng panel demo bên trái cũng ghi `Prediction: bật/chờ`.

### Thử cảm giác trễ

Server Local có RTT ≈ 0, nên khó thấy khác biệt. Để cảm nhận, dùng công cụ giả lập mạng (ví dụ *clumsy* trên Windows: lag 50–100 ms, drop 33%) trên cổng 7777/7781. Không có prediction, nhân vật sẽ "ì" đúng bằng RTT; có prediction, nhân vật phản hồi ngay và người khác vẫn mượt.

## 2. Log `[Mov]`

Khi đang trong trận, mỗi 5 giây `MovementTelemetry` ghi một dòng vào Player.log / Console:

```
[Mov] transport=Udp rttP50Ms=48.0 rttP95Ms=61.0 ackRttMs=66.7 predicted=900 pending=3 reconciles=897
      corrections=12 snaps=0 errLast=0.000 errAvg=0.004 errMax=0.133 overflow=0
      | remote jitterMs=3.1 delayMs=100 bufferedMs=92 extrapolated=2/1800 clamped=0 dup=0 old=0
      reordered=0 clockResets=1 spawns=2 despawns=0
```

- Phần trước `|` là MOV-02 (nhân vật của mình), phần sau là MOV-03 (người khác).
- `transport` là đường đang dùng (`Udp`/`Wss`), nên **so UDP và WSS bằng cùng định dạng**.
- Không ghi mã phòng, token hay tên người chơi.
- Đổi chu kỳ: `MovementTelemetry.IntervalSeconds` trong `Bootstrap`.

Lỗi kết nối vẫn dùng thông báo của `DisconnectReasons.Describe` (CLI-01): sai mã → "Mã phòng không đúng.", đầy phòng → "Phòng đã đủ người.", hiện trên menu.

## 3. Test tự động (EditMode) — chạy trước mỗi lần commit

Unity → **Window → General → Test Runner** → tab **EditMode** → Run All (assembly `JungleMaze.Tests.EditMode`).

Hoặc dòng lệnh (đóng Editor đang mở project này trước):

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.15f1\Editor\Unity.exe" -batchmode -nographics `
  -projectPath "D:\Unity\Lastaway" -runTests -testPlatform EditMode `
  -testResults "$env:TEMP\editmode.xml" -logFile "$env:TEMP\editmode.log"
Select-String -Path "$env:TEMP\editmode.log" -Pattern '^\[Mov\]'   # các dòng số đo
```

Thư mục test: [Tests/EditMode/Movement/](../../Assets/_JungleMaze/Tests/EditMode/Movement/)

| File | Kiểm tra gì |
|---|---|
| `MovementSimulationTests` | Tốc độ 4 cell/s, đường chéo, chặn Hard Cover, trượt, không xuyên cột mỏng khi bước lớn, biên map, không cấp phát |
| `LocalPredictionTests` | Dự đoán ngay; ack bỏ input; server đúng → 0 correction; server lệch → không giật rồi hội tụ; lệch lớn → snap; trùng/cũ bị bỏ; seq quay vòng; chết thì ngừng; **hình không lấn cover**; đo ack RTT; buffer đầy |
| `RemoteInterpolationTests` | Buffer thứ tự/trùng/cũ/giới hạn; nội suy góc cung ngắn; ngoại suy có giới hạn; jitter 20 ms, loss 1/3, gói trùng; **WSS đứng 2 s rồi xả dồn**; resync tiến; despawn/respawn không ghost; MatchSession xóa entity khi mất gói despawn/đổi trận/mất kết nối |
| `MovementNetcodeTests` | **Fake authoritative server** + 2 client thật qua serializer CLI-02, 8 điều kiện mạng (xem mục 4) |
| `NetworkClientTests` (bổ sung) | `Auto` + đầy phòng → không fallback; `Auto` + UDP không tới được → fallback WSS **cùng AttemptId** |

### Fake authoritative server hoạt động thế nào

[MovementNetHarness.cs](../../Assets/_JungleMaze/Tests/EditMode/Movement/MovementNetHarness.cs) gồm ba phần, tất cả chạy bằng **đồng hồ giả lập** (không sleep, kết quả lặp lại được theo `Seed`):

- `SimulatedLink`: một chiều mạng với RTT/2 ± jitter, mất gói, nhân đôi gói; `Reliable = true` mô phỏng WSS (không mất, giữ thứ tự, head-of-line blocking).
- `FakeAuthoritativeServer`: làm đúng luật `MatchRuntime` (30Hz, cửa sổ tick, tối đa 2 input/tick, seq cũ bị bỏ, input cũ > 250 ms thì đứng, cùng physics), gửi `MatchPhasePacket`, `ResumeTokenPacket`, `WorldSnapshotPacket` **đã serialize bằng `PacketRegistry`**. Ghi lịch sử vị trí theo tick để test so "hình client vẽ" với "sự thật server".
- `TestMovementClient`: `MatchSession` + `LocalPrediction` + `RemotePlayerInterpolator` + `PlayerPoseSource` **thật**, chỉ thay `NetworkClient` bằng link giả. Thứ tự mỗi frame giống Unity: nhận gói → `MatchSession.Tick` → `RemotePlayerInterpolator.Tick`.

Kịch bản: client A đâm vào BoulderRock 1,5 s, trượt chéo, sang trái chạm CityWall, đi xuống, zig-zag 2 s rồi dừng; client B đứng xem A.

## 4. Kết quả đo với fake server (25/10/2026)

Unity 6000.3.15f1 batchmode: **EditMode 200/200 PASS** (gồm 49 test movement/transport). Dữ liệu gốc: [evidence/mov-netcode-fake-server.log](evidence/mov-netcode-fake-server.log), [evidence/editmode-results-summary.json](evidence/editmode-results-summary.json).

| Điều kiện mạng | ack RTT | Correction (lần / avg / max, cell) | Snap | Hội tụ khi dừng | B thấy A lệch avg / max | Jitter đo | Ngoại suy |
|---|---|---|---|---|---|---|---|
| UDP RTT 0 | 0 ms | 0 / 0 / 0 | 0 | 0,0000 | 0,000 / 0,000 | 0 ms | 0/481 |
| UDP RTT 50 | 67 ms | 0 / 0 / 0 | 0 | 0,0000 | 0,000 / 0,000 | 0 ms | 0/481 |
| UDP RTT 100 | 100 ms | 0 / 0 / 0 | 0 | 0,0000 | 0,000 / 0,000 | 0 ms | 0/481 |
| UDP RTT 100, jitter 20 | 111 ms | 73 / 0,036 / 0,133 | 0 | 0,0000 | 0,000 / 0,041 | 11,9 ms | 0/481 |
| UDP RTT 50, jitter 20, **loss 1/3** | 55 ms | 60 / 0,053 / 0,400 | 0 | 0,0000 | 0,008 / 0,350 | 9,5 ms | 38/481 |
| UDP RTT 100, jitter 20, **loss 1/3** | 115 ms | 63 / 0,057 / 0,400 | 0 | 0,0000 | 0,003 / 0,150 | 10,9 ms | 35/481 |
| UDP RTT 100, jitter 10, trùng 20% | 96 ms | 67 / 0,032 / 0,267 | 0 | 0,0000 | 0,000 / 0,000 | 8,2 ms | 0/481 |
| WSS RTT 100, jitter 20 | 120 ms | 71 / 0,034 / 0,133 | 0 | 0,0000 | 0,000 / 0,000 | 11,7 ms | 0/481 |

Đọc bảng:

- **Không lần nào xuyên Hard Cover** (kiểm từng frame cả vị trí logic lẫn hình vẽ), không snap, không tràn buffer; khi dừng, dự đoán trùng server tới 4 chữ số.
- Correction chỉ xuất hiện khi có jitter/mất gói và bằng bội số một bước tick (0,133 cell) — đúng lý giải ở [02 mục 4.5](02_MOV-02_Prediction.vi.md). Được làm mượt trong 0,1 s nên mắt không thấy giật.
- Gói trùng bị `MatchSession` bỏ trước khi tới interpolator (`dup=0` ở MOV-03, `dupDown=70` ở link).
- WSS: `bufferedMs` ≈ 92 ms (đúng độ trễ 100 ms), không tích state cũ.

## 5. Test với server thật (UDP, WSS, Auto)

Dùng lại khung online của dự án (`OnlineScenarioRunner`) với scenario mới [local2-movement.json](../../Backend/Tests/OnlineReadiness/scenarios/local2-movement.json), bật hai check:

- `mov02.prediction`: FAIL nếu vị trí dự đoán lấn Hard Cover/biên, tràn buffer, có snap, `errMax > 0,5` hoặc `errAvg > 0,05`; PASS (và giữ PASS) sau ≥ 150 lần reconcile trong `InGame`.
- `mov03.interpolation`: FAIL nếu còn buffer cho NetId server đã despawn (ghost) hoặc > 5% frame chạm giới hạn ngoại suy; PASS (và giữ PASS) khi đã thấy người chơi khác ≥ 150 frame trong `InGame`.

Detail của mỗi check chứa nguyên dòng `[Mov]`, nên file evidence JSON giữ luôn số đo RTT/correction/jitter.

**Máy không có Docker** (chỉ cần chơi RoomCode, không lưu lịch sử): chạy GameServer trực tiếp. Máy chỉ có .NET SDK mới hơn 8 thì build từ thư mục **không** nằm dưới `Backend` (để `global.json` không ép SDK 8) và cho phép chạy trên runtime mới:

```powershell
$out = "$env:TEMP\jm-gameserver"
Set-Location $env:TEMP
dotnet build 'D:\Unity\Lastaway\Backend\GameServer\GameServer.csproj' -c Release -o $out
$env:DOTNET_ROLL_FORWARD = 'Major'          # bỏ dòng này nếu máy có runtime .NET 8
$env:ROOM_CODE = 'MA-PHONG-CUA-BAN'; $env:MATCH_PROFILE = 'test'; $env:PUBLIC_HOSTING = 'false'
$env:WS_ENABLED = 'true'; $env:GAME_BIND_ADDRESS = '127.0.0.1'; $env:WS_BIND_ADDRESS = '127.0.0.1'
dotnet "$out\GameServer.dll"                 # đợi dòng [ready] udp=127.0.0.1:7777 và [websocket] enabled port=7781
```

```powershell
# Server Local đang chạy với MATCH_PROFILE=test, WS_ENABLED=true (xem Phase 2 mục 1 hoặc lệnh ở trên)
Set-Location 'D:\Unity\Lastaway\Backend'
$env:NET_ROOM_CODE = Read-Host 'Ma phong'
# Hai client cùng transport (đổi "transport" trong file config: Udp / Wss / Auto)
.\Deploy\test-unity-online.ps1 -Player '<build>\Lastaway.exe' `
  -Config '..\Assets\StreamingAssets\JungleMaze\client.phase2-local.json' `
  -Scenario '.\Tests\OnlineReadiness\scenarios\local2-movement.json' -Clients 2 -OutputDirectory '.\out\mov-LUOT-MOI'
# Trộn transport: một client UDP, một client WSS
.\Deploy\test-unity-online.ps1 ... -ClientConfigs @('<udp.json>', '<wss.json>')
Remove-Item Env:NET_ROOM_CODE
```

**Mô phỏng "mạng chặn UDP":** dùng config `transport: "Auto"` nhưng `gameServerPort` trỏ tới cổng UDP không có ai nghe (hoặc chặn UDP 7777 bằng Windows Firewall). Client thử UDP tối đa `min(5000, connectTimeoutMs)` ms rồi sang WSS; evidence ghi `selectedTransport: "Wss"`, `fallbackReason: "ConnectTimeout"`. Server chỉ có 2 người trong roster (cùng AttemptId → không chiếm slot thứ hai).

Kết quả lượt chạy thật trên máy này: xem [mục 7](#7-kết-quả-chạy-với-server-thật).

## 6. Mở rộng / sửa đổi

- **Đổi tốc độ, bán kính, map:** sửa dữ liệu chuẩn `Backend/Shared/GameData` → menu **Jungle Maze → Phase 2 → Sync Runtime Data**. Client tự đọc qua `IClientRuntimeData` sau khi kiểm hash; không hardcode trong code movement.
- **Sửa physics server** (`PhysicsSimulation.cs`): sửa song song `MovementSimulation.cs` + chạy `MovementSimulationTests`, `MovementNetcodeTests`.
- **Thêm kỹ năng di chuyển (dash...)**: input phải đi qua `PlayerInputPacket` → server mô phỏng → **cùng công thức** đưa vào `MovementSimulation` và lưu trong `PendingInput` để replay được. Không cho client tự đặt vị trí.
- **Thêm điều kiện mạng để thử:** thêm một `LinkProfile` trong `MovementNetcodeTests.Profiles()`.
- **Thêm check online:** đăng ký trong [Bootstrap.MovementChecks.cs](../../Assets/_JungleMaze/Scripts/Bootstrap/Bootstrap.MovementChecks.cs) rồi thêm id vào `extraAssertions` của scenario.

## 7. Kết quả chạy với server thật

**25/10/2026**, một PC Windows, loopback. GameServer build từ source hiện tại (`dotnet`, `MATCH_PROFILE=test`, UDP 7777 + WS 7781), hai Windows player Development build từ Boot/Game, scenario `local2-movement` (mỗi client giữ kết nối 20 s). Dữ liệu gốc: [evidence/online-real-server-summary.json](evidence/online-real-server-summary.json).

| Lượt | Client 1 | Client 2 | Kết quả | Correction avg / max | Snap | Jitter đo | Ngoại suy / chạm giới hạn |
|---|---|---|---|---|---|---|---|
| UDP + UDP | Udp | Udp | **PASS 2/2** | 0,000–0,002 / 0,043 | 0 | 5,4–5,5 ms | 0 / 0 |
| WSS + WSS | Wss | Wss | **PASS 2/2** | 0,000–0,001 / 0,043 | 0 | 0,4–1,7 ms | 0 / 0 |
| **Auto (UDP bị chặn)** + UDP | Wss, `fallbackReason=ConnectTimeout` | Udp | **PASS 2/2** | 0,000–0,001 / 0,043 | 0 | 0–16,8 ms | 0 / 0 |
| Auto + WSS | Udp (Auto chọn UDP) | Wss | **PASS 2/2** | 0,000–0,001 / 0,043 | 0 | 0–17 ms | 0 / 0 |

- Input 30,0 Hz, snapshot ≈ 29,9–30,05 Hz, RTT ping p50 ≈ 33 ms (gói ping chỉ được server trả theo nhịp tick 30 Hz nên tối thiểu ≈ 1 tick).
- Log server lượt "UDP bị chặn": client UDP vào trước (`serverSlots=1/50`), client fallback vào bằng `transport=Wss` với `serverSlots=2/50` → **không có slot trùng**, phòng giữ đúng `2/10`.
- Check `mov02.prediction` và `mov03.interpolation` PASS ở cả 8 client: không lấn Hard Cover, không ghost entity, không tràn buffer.
- `ackRttMs` được đo theo frame nên có thể ghi **0** khi RTT nhỏ hơn một frame (loopback WSS); với mạng thật dùng `rttP50Ms`/`rttP95Ms` của ping làm số chính.
- Lượt thử đầu (minConnected 10 s) một client FAIL vì client UDP xong sớm rời phòng làm trận kết thúc trước khi client fallback (vào trễ ~5 s) đủ mẫu. Đã sửa scenario lên 20 s và cho check "chốt" PASS khi đủ mẫu; số đo của client đó vẫn tốt (`snaps=0`, `errMax=0,043`).

**Chưa kiểm:** hai PC vật lý/LAN/WAN thật, AWS, và mạng có loss/jitter thật (đã thay bằng fake server ở mục 4). Khi có hai máy, chạy lại đúng các lệnh ở mục 5 và so dòng `[Mov]`.
