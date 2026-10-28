# Phase 2: chạy trận, Guest, Safe Zone và lịch sử

Cập nhật **28/10/2026**. Hướng dẫn setup và kiểm thử **SRV-03, NET-02, ZON-01**. Xem [báo cáo nghiệm thu Phase 2](Reports/Phase2_Local/report.vi.md) để đọc kết quả, giới hạn và bằng chứng. Setup nền và vận hành AWS nằm trong [hướng dẫn Phase 1](Phase1_First4Tasks.vi.md).


Phần này áp dụng **SRV-03 / NET-02 / ZON-01** trên source Phase 2. AWS đang chạy bản trước không tự có tính năng mới; đợt này không deploy hoặc bật EC2. Xem [Network Architecture](Network_Architecture.vi.md) để biết contract và quyền truy cập.

## 0. Chọn Local, LAN hoặc Online

- **Local → Chơi Solo (Offline)**: vào Game ngay; không bật Docker/API, không nhập mã phòng. Một người chơi với di chuyển/cover/bo/HP → chết → kết quả luyện tập → chơi lại. Không tự thắng khi chỉ có một người và không ghi lịch sử online. Mở Game rồi Play trong Editor cũng chạy đường này, trừ runner mạng có chủ đích.
- **LAN**: máy host chạy GameServer; đặt `GAME_PUBLISH_HOST` trong Backend/.env thành IPv4 LAN của host, giữ `GAME_BIND_ADDRESS=0.0.0.0`, bật lại `docker compose up -d --build game-server`. Firewall host cho UDP7777 trên mạng riêng. Client nhập đúng IP, RoomCode và tên. Mongo/control vẫn private. RoomCode không đòi đăng nhập.
- **Online → Radmin/VPN**: cả hai PC cài Radmin VPN và vào cùng mạng VPN. Máy host publish UDP7777 vào IP Radmin của chính nó qua `GAME_PUBLISH_HOST`; client nhập IP đó, tên và mã phòng. Cho phép UDP7777 ở firewall đúng interface; không mở Mongo/control ra VPN. Từ xa chỉ cấu hình API khi đã có HTTPS hợp lệ; bỏ API vẫn chơi được, không có lịch sử online.
- **Online → AWS**: hostname `junglemaze.duckdns.org`, Auto UDP rồi WSS qua443; không dùng IP thay hostname cho TLS. Chọn mode không tự bật EC2. Admin bật máy, kiểm DNS/TLS và đặt giờ Stop theo tài liệu Phase1 trước khi mời tester.

**Nâng cao** giữ profile, host/port, RoomCode, API và nút vào backend loopback để test mạng local như trước. `phase2-local` là **backend Docker loopback**, khác nút Local offline. Profile/CLI cũ giữ tương thích. F3 bật thông tin chẩn đoán; ESC chỉnh âm lượng/rời trận. Offline vẫn chạy bo khi ESC mở.

Muốn tạo/list/xóa phòng: Swagger → register/login → Authorize JWT → `/api/rooms`; Guest không được quản lý phòng. Giao diện tài khoản/phòng đầy đủ UI-02 không nằm trong lần khép Done này.

Killcam dùng replay server theo chính phiên/phòng, tối đa5s (150frame), dữ liệu chỉ có nạn nhân và kẻ hạ. Mặc định vẫn tắt theo ticket; bật asset MatchOverlaySet hoặc `-killcam` ở development build để kiểm. Chết do bo/không rõ killer hiển thị fallback, không giả lập combat. Chưa có combat vũ khí authoritative để nghiệm thu killcam người thật.

## 1. Chuẩn bị backend Docker loopback lần đầu

1. Bật Docker Desktop, dùng Linux containers. Mở PowerShell **tại `Backend`**, không chạy compose từ `C:\Users\...`.
2. Giữ `.env` riêng của máy và Mongo volume hiện có. Tạo từ `.env.example` nếu chưa có, theo [mục 3 của hướng dẫn Phase 1](Phase1_First4Tasks.vi.md). Không chép secret AWS sang Local.
3. Kiểm tra `ROOM_CODE`, `MAX_PLAYERS=10`, `MAX_ROOM_PLAYERS=10`, `MAX_ROOMS=5`, `MAX_TOTAL_PLAYERS=50`, `MAX_ROOMS_PER_USER=2`. Main dùng `MATCH_PROFILE=main`; thử hai client dùng `MATCH_PROFILE=test` và `PUBLIC_HOSTING=false`.
4. Muốn tạo phòng/liên kết Guest cần `ROOM_CONTROL_TOKEN` tối thiểu32 byte và API trỏ đúng room-control nội bộ. Muốn lưu lịch sử cần **secret riêng** `MATCH_RESULT_TOKEN` tối thiểu32 byte, giống nhau giữa API/GameServer; không dùng JWT key hoặc control token thay thế. Có thể tạo mỗi secret bằng PowerShell: `$b = New-Object byte[] 32; $rng = [Security.Cryptography.RandomNumberGenerator]::Create(); $rng.GetBytes($b); $rng.Dispose(); [Convert]::ToBase64String($b)`, rồi điền vào `.env` bằng VS Code. Không commit file này.
5. Giữ `MATCH_OUTBOX_PATH` trong volume của game-server và `MATCH_RESULTS_API_URL` trỏ API nội bộ. Compose chịu trách nhiệm tên service/cổng; client không gọi tuyến ghi kết quả.
6. Khởi động cả hệ thống:

```powershell
Set-Location 'D:\Unity\Lastaway\Backend'
docker compose --profile account up -d --build
docker compose ps
docker compose logs --tail 40 game-server api
```

Đợi Mongo healthy, `/health` của API trả thành công, GameServer có `[ready]`. Nếu báo lịch sử bị tắt/secret thiếu thì vẫn chơi RoomCode được, nhưng chưa nghiệm thu lưu lịch sử. Không dùng `docker compose down -v`: lệnh đó xóa dữ liệu.

## 2. Chạy demo Unity hai client

1. Mở project Unity **6000.3.15f1**, đợi hết compile. Trong menu **Jungle Maze → Phase 2 → Sync Runtime Data** đồng bộ map/luật/bo từ `Backend/Shared/GameData`. Dùng Verify Runtime Data để kiểm; không sửa bản sao StreamingAssets thay nguồn chuẩn.
2. Trong Nâng cao của menu kết nối scene Boot, chọn profile **phase2-local**, đặt host/port theo compose và nhập `ROOM_CODE` trong `.env`. Protocol v3 dùng resume; RoomCode không bắt đăng nhập. Nếu muốn lưu Guest, `apiBaseUrl` phải trỏ API loopback đang chạy. API từ xa phải HTTPS.
3. Build hai scene `Boot` và `Game` thành Windows player. Chạy một player + Unity Editor, hoặc hai player. Dùng hai tên dễ phân biệt. Cả hai dùng chung endpoint và mã phòng.
4. Với `MATCH_PROFILE=test`, hai người đủ bắt đầu; lobby 0,5s, loading tối đa 1s, disconnect grace 1s, kết quả 0,5s và lịch bo chạy ở 4% thời gian gốc. Với `main`, cần ít nhất 6 người, lobby 15s, loading tối đa 30s, grace 15s, kết quả 10s và bo đúng lịch GDD. **Không dùng test profile để đo thời lượng GDD hoặc đưa public.**
5. Quan sát roster/countdown/loading, hai nhân vật có nhãn tên/HP, bo đỏ hiện tại và bo vàng kế tiếp. WASD gửi input thật; server tính va chạm/speed/HP. Góc nhìn toàn map dành cho demo. Local HeroController bị tắt khi chơi mạng, tránh hai mô phỏng ghi vị trí.
6. Ra ngoài bo để thấy HP giảm5/s; đợi thắng/hòa, xem hạng và nhấn chơi lại. Cùng tick bị loại đồng hạng. Bo về0 vẫn gây damage toàn map;420s không phải timeout cưỡng bức.
7. Rời phòng bằng nút để loại ngay. Mất mạng đột ngột: nhân vật đứng yên và còn nhận damage; nút khôi phục chỉ thành công khi còn sống và trong grace từ lúc server phát hiện mất kết nối: 15s với `main`, 1s với `test`. Resume giữ PlayerId/slot và đổi proof; không tạo nhân vật thứ hai.
8. Nút lịch sử gọi API cho hồ sơ đã xác minh. Lần đầu tự tạo Guest khi API có sẵn; Windows lưu credential bằng DPAPI, tách theo origin Local/AWS. Đóng/mở lại client giữ hồ sơ. API mất kết nối vẫn xem kết quả trực tiếp; lịch sử báo không khả dụng. Không tự gộp tài khoản theo tên.

Unity hiện có UDP/WebSocket/Auto; xem các mục online bên dưới và báo cáo để phân biệt kết quả Local với AWS. Prediction/interpolation đầy đủ còn thuộc MOV-02/03; JSurviv Socket.IO cũ không phải client demo này. Chưa có combat vũ khí/inventory/hazard; kills bằng0.

## 3. Kiểm API qua Swagger

Mở [Swagger Local](http://127.0.0.1:5000/swagger/index.html), theo [mục 5 của hướng dẫn Phase 1](Phase1_First4Tasks.vi.md). Các endpoint phòng vẫn cần register/login và Authorize JWT account. Guest credential **không** thay JWT để tạo/list/xóa phòng.

| Endpoint | Cách thử |
| --- | --- |
| `POST /api/guests` | Không cần account; nhận Guest ID và credential, giữ riêng và không chia sẻ |
| `POST /api/game-tickets` | JWT hoặc `X-Guest-Credential`; body attemptId + roomCode của kết nối, ticket dùng một lần/TTL ngắn |
| `GET /api/me/matches` | JWT hoặc Guest credential đúng; phân trang, chỉ lịch sử của hồ sơ đó |
| `GET /api/matches/{matchId}/leaderboard` | Đọc kết quả authoritative đã lưu, không có credential/ProfileKey |
| `/internal/match-results` | Không thử bằng JWT player; tuyến chỉ GameServer qua mạng nội bộ + secret riêng |

Client demo tự xử lý attempt/ticket; không nhập PlayerId tùy ý để nhận lịch sử người khác. Hai người cùng tên vẫn là hai hồ sơ nếu dùng credential khác. Kết quả cần kết thúc trận và outbox được API xác nhận trước khi có trong Mongo/history.

## 4. Kiểm thử tự động không tiêu credits

Đứng tại `Backend`:

```powershell
dotnet build JungleBackend.sln
dotnet run --project Tests/MatchChecks/MatchChecks.csproj
dotnet run --project Tests/MatchResultChecks/MatchResultChecks.csproj
dotnet run --project Tests/NetworkChecks/NetworkChecks.csproj
dotnet run --project Tests/DualTransportChecks/DualTransportChecks.csproj
dotnet run --project Tests/GameplayNetworkChecks/GameplayNetworkChecks.csproj -- Mixed
dotnet run --project Tests/GameplayNetworkChecks/GameplayNetworkChecks.csproj -- Udp
dotnet run --project Tests/GameplayNetworkChecks/GameplayNetworkChecks.csproj -- Wss
```

GameplayNetworkChecks tự mở server/port loopback riêng, không dùng phòng đang phục vụ dev; thử10 phiên, phiên11, snapshot/bo, resume, kết quả và vòng tiếp theo. Có thể thêm đường dẫn JSON ở cuối lệnh để lưu bằng chứng. Đây là kiểm chức năng gameplay tối thiểu, không phải benchmark sức chứa cuối.

Unity: Test Runner → EditMode `JungleMaze.Tests.EditMode` và PlayMode `JungleMaze.Tests.PlayMode`. Build tự chặn dữ liệu không khớp nguồn. Có acceptance runner opt-in trong Windows player: `-clientConfig <json> -room <code> -name <name> -phase2Smoke <output.json>`; chạy hai player với profile server `test`. Runner gửi input thử, xác nhận view/bo/HP/kết quả/chơi lại rồi thoát; không bật cờ này khi chơi bình thường.

Để đo hiệu năng, đợi Unity build/shader compilation hoàn tất, tránh Sleep và chạy ứng dụng nặng đồng thời. Ghi dropped tick/CPU/RAM và tốc độ phát thực; không đổi một lượt đo không hợp lệ thành PASS.

## 5. Những lần chạy sau, dừng và cập nhật

Bật Docker Desktop → mở đúng thư mục Backend → `docker compose --profile account up -d` → kiểm `/health`/logs → chạy client. Khi sửa code backend thêm `--build`; khi sửa dữ liệu chuẩn, Sync Runtime Data và build client lại. Khi đổi `MATCH_PROFILE` hoặc biến môi trường, chạy compose up để recreate service; restart đơn thuần không nạp biến mới.

Xong buổi Local: đóng client, `docker compose --profile account stop`. Lần bật lại Mongo và outbox trên volume vẫn còn; phòng bổ sung, session và trận trong RAM mất. Không xóa outbox chưa gửi được để dọn dung lượng. Khi API trở lại, outbox retry ngoài tick, cùng MatchId không cộng kết quả lần nữa.

AWS chỉ chuẩn bị compose/Caddy/secret mới, chưa tự deploy. Khi triển khai sau này phải giữ JWT key, RoomCode, Mongo volume và cấu hình cũ; thêm secret kết quả/outbox volume, không publish internal port. Dùng quy trình checksum/rollback và Stop EC2 trong [hướng dẫn Phase 1, mục 7/11/16](Phase1_First4Tasks.vi.md). EBS vẫn có thể tiêu credits khi EC2 đã Stopped. Hai PC thật và lượt online Phase 2 cần nghiệm thu riêng; hai process trên cùng PC không thay thế điều đó.

## 6. Chọn môi trường online

Phần mở rộng ngày **25/10/2026** bổ sung Unity WSS/Auto và bộ test dùng lại. Kết quả thực tế cập nhật trong [báo cáo Phase 2](Reports/Phase2_Local/report.vi.md); có cấu hình không đồng nghĩa đã nghiệm thu tuyến mạng đó.

| Profile Unity | Host game / API | Việc phải cấu hình |
| --- | --- | --- |
| `phase2-local` | Loopback: UDP7777, API5000, WS7781 | Docker Local, RoomCode trong Backend/.env |
| `phase2-lan` | IP LAN của máy host, UDP | Thay IP mẫu; API để null thì chơi ẩn danh, không có lịch sử online |
| `phase2-vpn` | IP Tailscale cho UDP; hostname ts.net cho HTTPS/WSS | Thay IP/hostname mẫu, cùng tailnet, Tailscale Serve HTTPS |
| `phase2-selfhost` | Hostname riêng cho UDP/HTTPS/WSS | Thay YOUR-SELFHOST, DNS, router forwarding và TLS |
| `phase2-aws` | junglemaze.duckdns.org | EC2 phải được deploy release mới; game/API/WSS cùng backend |

Các profile nằm tại `Assets/StreamingAssets/JungleMaze/client.phase2-*.json`, đều dùng protocol3. Các địa chỉ LAN/VPN/selfhost mẫu phải thay bằng địa chỉ thật. Không đổi hostname tự host sang domain của AWS.

Auto thử UDP tối đa5s, rồi WSS tối đa10s kết nối +5s join. RoomFull, ServerFull, sai RoomCode, MatchInProgress hoặc sai version không làm client thử transport khác. Sau join không tự đổi tuyến; mất mạng dùng Resume trong grace. F3 trong Editor/Development build cho biết transport/RTT/fallback, menu cũng hiện transport cấu hình.

API từ xa cần HTTPS, kể cả VPN. WS không TLS chỉ dùng loopback Local. Không dùng certificate callback cho phép mọi chứng chỉ, `curl -k`, hoặc chép Guest credential/JWT vào JSON cấu hình. `NET_TRANSPORT` và `NET_WS_URL` có thể override transport/URL; CLI có `-transport` và `-webSocketUrl`. Bỏ biến override sau khi test để không ảnh hưởng buổi sau.

### 6.1. Tự host qua VPN

Đứng tại `Backend`, bật Docker Desktop. Dùng config riêng, không sửa secret của `.env` Local hoặc `.env.aws`:

```powershell
.\Deploy\Host\configure-host.ps1 -Mode vpn
```

Script hỏi mật khẩu Swagger, tạo ba secret riêng và `.env.host.vpn` được bảo vệ bằng ACL Windows. Không ghi đè file đã có. Cấu hình ban đầu là `MATCH_PROFILE=main`; muốn thử2 người trong VPN riêng thì mở file và đổi thành `test`.

1. Cài/đăng nhập Tailscale bằng `Deploy/setup-phase1-vpn.ps1 -Target PC`, hoặc cài ứng dụng chính thức. Đăng nhập trực tiếp trong trình duyệt. Mạng chặn trang cài/đăng nhập thì ghi BLOCKED và đổi mạng được phép; không coi VPN luôn hoạt động.
2. Chạy preflight rồi mở stack:

```powershell
.\Deploy\Host\preflight-host.ps1 -Mode vpn -EnvFile .\Deploy\Host\.env.host.vpn
docker compose -f compose.host.yaml --env-file Deploy/Host/.env.host.vpn --profile vpn up -d --build
```

3. Trong PowerShell Administrator, thực hiện thủ công `Deploy/Host/setup-firewall.ps1 -Action Enable -Mode Vpn`. Chỉ cho UDP từ mạng Tailscale; không mở8080 ra mạng.
4. Chạy Tailscale Serve HTTPS chuyển vào `http://127.0.0.1:8080`, theo hướng dẫn CLI `tailscale serve --bg http://127.0.0.1:8080`. Xác nhận bật HTTPS trong tailnet khi được hỏi. Không bật Funnel.
5. Lấy IP bằng `tailscale ip -4` và hostname HTTPS mà Serve báo; điền cả hai vào profile `phase2-vpn`. Game UDP và HTTPS phải về cùng host. Tester cũng đăng nhập tailnet.
6. Xong buổi: `docker compose -f compose.host.yaml --env-file Deploy/Host/.env.host.vpn --profile vpn stop`. Tắt Serve bằng `tailscale serve reset` nếu không còn dùng cấu hình Serve này. Chỉ reset cấu hình thuộc host test, không đụng thiết bị khác.

Stack `junglemaze-host` có volume riêng, không dùng DB dev/AWS. Nếu UDP7777 đang bị stack dev chiếm, stop đúng service dev khi không ai dùng, hoặc chọn `HOST_GAME_UDP_PORT` khác và đổi client tương ứng. Không chạy hai stack publish cùng cổng trên một host.

### 6.2. Tự host public

1. Kiểm tra có IP public và quyền cấu hình router. CGNAT/double NAT chưa xử lý thì tuyến public chưa sẵn sàng; dùng VPN hoặc AWS. Không dùng kết nối từ chính host để khẳng định người ngoài vào được.
2. Tạo hostname riêng và trỏ tới public IP của host. Giữ domain `junglemaze.duckdns.org` cho AWS.
3. Chạy `Deploy/Host/configure-host.ps1 -Mode public -Domain TEN-HOST-THAT -AcmeEmail EMAIL-THAT`. File riêng được tạo, giữ profile main.
4. Router forward TCP80/443 và UDP7777 về máy host; chạy firewall helper thủ công với `-Mode Public`. Chưa có cách đọc WAN/router từ repo để tự xác nhận các bước này.
5. Preflight với mode public rồi chạy compose.host như VPN, đổi env/profile thành public. Caddy cấp TLS, `/game` chuyển đến GameServer; Swagger có basic auth. API/Mongo/control/WS nội bộ không publish trực tiếp.
6. Dùng PC ở mạng ngoài để kiểm HTTPS và game thật. Chỉ `/health`200 chưa chứng minh WSS/UDP hoặc gameplay. Xem checklist hai PC ở mục9.

## 7. Chạy test tự động bằng player thật

`Deploy/test-unity-online.ps1` dùng được khi các feature tiếp theo được phát triển. Nó không tạo NPC server, không ép phase hoặc tự ghi HP/kết quả. Mỗi player thật gửi input kiểm thử, nhận state server và xuất bằng chứng.

Đứng tại `Backend`, dùng build Windows đã tạo từ Boot/Game. `-Player` là đường dẫn .exe, không phải project Unity. Giữ thư mục `_Data`, DLL và MonoBleedingEdge đi cùng. Không sửa code để đổi endpoint; dùng `-Config`.

```powershell
$env:NET_ROOM_CODE = Read-Host 'Ma phong cua server dang test'
.\Deploy\test-unity-online.ps1 -Player '..\.utmp\online-20261024\build\Lastaway.exe' -Config '..\Assets\StreamingAssets\JungleMaze\client.phase2-local.json' -Scenario '.\Tests\OnlineReadiness\scenarios\local2-accelerated.json' -Clients 2 -OutputDirectory '.\out\test-LUOT-MOI'
Remove-Item Env:NET_ROOM_CODE
```

Thay LUOT-MOI bằng nhãn chưa tồn tại. Công cụ từ chối ghi đè evidence cũ; mã phòng chỉ truyền qua env của process. Chỉ dọn process đúng PID và thời điểm khởi chạy mà công cụ đã tạo, không kill Editor/game của người khác.

| Scenario | Server cần dùng | Điều xác nhận |
| --- | --- | --- |
| `local2-accelerated` / `local2-udp` / `local2-wss` | Private `test`,2 người | Movement, bo, HP, kết quả, replay và đúng MatchId trong history |
| `main6-preflight` | `main`,6 client | Giữ kết nối30s, input/snapshot đủ tốc độ; dùng để kiểm PC phát tải trước AWS |
| `main6-smoke` | `main`,6 client | Kết nối ít nhất300s, đủ vòng trận/kết quả/replay/history; deadline600s, không tăng tốc luật |
| `connection` | Bất kỳ,1 client | Join/pong; không coi là hoàn thành gameplay |

Tên scenario không ép transport. Chọn transport trong config hoặc override, trước lần chạy tương ứng. `-ClientConfigs` có thể nhận một đường dẫn config cho mỗi client để thử hỗn hợp UDP/WSS/Auto cùng backend.

Mỗi lượt có report.json, summary.csv, JSON/log và ảnh từ từng player. PASS chỉ khi đúng scenario, đủ quan sát và process không crash. Thiếu tốc độ input yêu cầu là INVALID; kết nối bị chặn là BLOCKED. Các báo cáo một-PC luôn giữ `physicalTwoPc=NOT_TESTED`. Fingerprint bao gồm player DLL/data để lượt rehearse cũ không được dùng cho build đã thay đổi.

Kiểm công cụ báo cáo bằng `powershell -NoProfile -File Tests/OnlineReadiness/Test-StaticOnlineReadiness.ps1` từ Backend; nếu đứng tại repo root, thêm `Backend/` trước `Tests/`. Các process giả ở bài này chỉ kiểm launcher, không thay cho player Unity hoặc chứng minh gameplay.

## 8. Thêm test khi có combat, inventory hoặc feature mới

1. Tạo JSON scenario mới dựa trên schema; thay các điều kiện thời gian/số người/builtin flag. Không sửa luật server để làm scenario PASS. Parser từ chối field không hỗ trợ.
2. Assertion feature cần **implementation thật**. Tạo file partial `Bootstrap.FeatureTests.cs` trong assembly Bootstrap, triển khai `partial void ConfigureOnlineChecks()`. Hook chỉ gọi khi có `-onlineTest`; chơi bình thường không chạy driver.
3. Trong hook, đăng ký `OnlineTestExtensions.RegisterCheck("combat.server-damage", context => ...)`. Trả `OnlineCheckResult("WAIT")` khi chưa quan sát, PASS khi nhận state/event authoritative đúng, FAIL khi phát hiện vi phạm. Đưa tên vào `extraAssertions` của JSON. Không có check tương ứng thì runner FAIL, không bỏ qua.
4. Muốn điều khiển hành động mới, triển khai `IOnlineTestDriver.Apply(in OnlineTestContext)` và đăng ký bằng `RegisterDriver`. Dùng service/packet feature đã được server hỗ trợ; context có Match, Network, History, Sender và elapsed. Không tự sửa snapshot/HP hoặc ghi điểm từ client.
5. Driver mặc định chỉ gửi movement ngắn rồi đứng yên. JSON dạng `extensions.combat.requireKills` không tự tạo được bài combat và bị từ chối; phải tích hợp driver/check rồi build lại.
6. Chạy test đơn vị feature trước, sau đó scenario Local hai client, sáu-client rehearsal và mới đến online. File schema/evidence có version; nếu thay contract phải cập nhật cả C#, module đánh giá và các ca negative launcher.

## 9. Các bài bắt buộc hai máy thật

Làm theo [checklist hai PC](../Backend/Tests/OnlineReadiness/PHYSICAL_TWO_PC_CHECKLIST.md). Các phần một-PC không thay thế được:

- LAN: PC2 vào IP LAN của PC1, firewall và layout giống nhau.
- VPN: hai thiết bị riêng trong tailnet, kiểm direct/relay, UDP và HTTPS/WSS.
- Public tự host: PC2 ở mạng ngoài, xác nhận DNS/router/CGNAT và game thật.
- AWS: hai PC, tốt nhất khác mạng; so vị trí/bo/HP/kết quả, Guest riêng, reconnect và replay.

Private test có thể mỗi máy1 client. Public/AWS main cần ít nhất6 phiên gameplay: ví dụ mỗi máy3 client. Lưu report mỗi máy và biên bản đối chiếu; không đổi report một-PC thành hai-PC PASS.

## 10. AWS có kiểm soát credits

Build/verify release và hoàn thành `main6-preflight` khi EC2 còn tắt. Công cụ kiểm fingerprint build để tránh dùng kết quả rehearsal không còn tương ứng.

```powershell
.\Deploy\test-aws-rooms.ps1 -DualTransport -UnityGameplay -Release 'out\RELEASE\release.tar.gz' -UnityPlayer '..\.utmp\online-20261024\build\Lastaway.exe' -UnityScenario 'Tests\OnlineReadiness\scenarios\main6-smoke.json' -UnityRehearsalReport 'out\LOCAL-REHEARSAL\report.json' -OutputDirectory 'out\AWS-LUOT-MOI' -BudgetMinutes 20 -HardStopMinutes 30
```

Đây là lệnh có quyền Start/deploy/test/Stop EC2, chỉ chạy khi admin đã dành riêng máy. `-PreflightOnly` chỉ kiểm trước; không bật máy. Delta release là lựa chọn khi đã verify base image/provenance theo Phase1; không bỏ checksum để upload nhanh.

Lượt AWS dùng Unity thực từ PC, JWT để tạo phòng, Guest qua HTTPS, config protocol3/Auto. Không dùng generator trong EC2 để thay bằng chứng WAN. Bài main giữ nguyên ngưỡng6 và lịch bo. Nếu còn thời gian, Stop–Start và kiểm account/join lại; nếu không, ghi NOT_TESTED.

Mục tiêu20 phút gồm boot/deploy/test; watchdog30 phút. Thiếu thời gian thì dừng, không rút ngắn bài đo rồi báo PASS. Công cụ đóng client, xóa phòng test và Stop trong finally; vẫn phải xác nhận `stoppedConfirmed=true`. Tắt máy PC hoặc đóng SSH không dừng EC2. EBS còn chi phí lưu trữ sau Stop. Những account/Guest/kết quả test đã lưu vẫn ở DB; không tự xóa lịch sử hoặc volume để dọn dung lượng.

## 11. Kiểm tra lặp lại Windows offline (development build)

Bản build development có runner opt-in, không chạy trong buổi chơi bình thường:

```powershell
Set-Location 'D:\Unity\Lastaway'
& '.\.utmp\done-closure-20261028\build\Lastaway.exe' -offlineTest -offlineReport "$PWD\.utmp\offline-player.json" -logFile "$PWD\.utmp\offline-player.log"
```

Runner dùng scene/physics/UI thật, đặt nhân vật ngoài bo để kiểm damage (không giả lập bắn/combat), hoàn thành hai vòng, kiểm MatchId/HP reset, ESC giữ timer, trở về menu và không mở socket game. Kết quả JSON phải có `passed=true`, `rounds=2`; timeout90s là FAIL. Đóng hết Docker vẫn chạy được. Không dùng kết quả này thay nghiệm thu hai PC, input người thật hoặc gameplay multiplayer.

Không truyền `-offlineTest` khi chơi bình thường. Chạy lại sau khi sửa flow/bo/HUD để kiểm hồi quy; nếu tự build sang thư mục khác thì thay đường dẫn exe ở lệnh trên.
