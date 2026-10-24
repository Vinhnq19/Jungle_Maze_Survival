# Hướng dẫn setup, phát triển và kiểm thử Jungle Maze

Cập nhật **16/10/2026**. Đây là hướng dẫn vận hành chung cho dev, tester và người quản lý AWS. Kiến trúc, packet và trách nhiệm từng module nằm trong [Network_Architecture.vi.md](Network_Architecture.vi.md). Tài liệu nhập assets Unity nằm trong [JSurviv_Integration.vi.md](JSurviv_Integration.vi.md). Tạo nhiều phòng và đo sức chứa: xem mục 14. Quy trình đo backend cũ qua SSM nằm ở mục 14.7; kết nối UDP/WSS thực từ PC và vận hành theo lịch ở mục 16; test PC→EC2 trực tiếp/VPN và rà soát Phase 1 ở mục 15; kết quả thực đo nằm trong báo cáo chung.

Hướng dẫn thao tác nhanh trên server đã triển khai: [Đăng nhập EC2, test Swagger và xem MongoDB bằng Compass](AWS_SSH_Swagger_Compass.vi.md).

## 1. Phạm vi hiện tại và cách chọn môi trường

Dự án phục vụ bài học: **tối đa 10 phiên/phòng, 5 phòng gồm mặc định và tổng 50 phiên active/pending**. Phòng tạo qua API có mã 6 ký tự chữ hoa/số; mỗi user tối đa 2 phòng tạo thêm. Đây là giới hạn cấu hình backend, chưa là bằng chứng sức chứa gameplay. Dev phát triển/debug trên máy mình; AWS là server chung, chỉ bật cho buổi test online đã hẹn. Source/Compose và EC2 đã cập nhật 5/50; đây là hạn mức vận hành, chưa là cam kết sức chứa gameplay.

**Đã có:** API đăng ký/đăng nhập/JWT, MongoDB users, Swagger, LiteNetLib 2.1.4, handshake, giới hạn phòng, ping/pong, khung tick 30Hz, Docker local và triển khai AWS qua HTTPS.

**Chưa có:** Unity client LiteNetLib, đồng bộ nhân vật/gameplay, snapshot/prediction, trận chơi multiplayer hoàn chỉnh. `NetSmokeClient` là chương trình console kiểm tra mạng. `GameServer/Program.cs` có `Tick` chưa mô phỏng gameplay; scene JSurviv vẫn là mã tham khảo Socket.IO, `AutoConnectLegacy` mặc định tắt. Play scene không tự kết nối backend mới.

| Bạn cần làm gì? | Môi trường / quyền cần có | Đọc phần nào? |
| --- | --- | --- |
| Dev mới tự chạy backend và test một client | PC riêng, Docker Desktop, SDK .NET 8 | [Setup local](#3-thiết-lập-local-lần-đầu) |
| Dev quay lại làm việc sau khi bật máy | Local; AWS có thể giữ Stopped | [Chạy local hằng ngày](#4-chạy-local-những-lần-sau) |
| Tester thử kết nối từ nhà/lớp | Client + endpoint + mã phòng; không cần SSH | [Buổi test AWS](#7-bật-và-dừng-aws-cho-mỗi-buổi-test) |
| Admin tạo/triển khai server AWS lần đầu | AWS Console, key SSH riêng, DuckDNS token | [Setup AWS](#6-thiết-lập-aws-lần-đầu) |
| Admin cập nhật backend hoặc cứu dữ liệu | Release mới, SSH, backup | [Cập nhật và backup](#11-cập-nhật-aws-backup-và-restore) |
| Dev kiểm tra UI/animation hoặc chuẩn bị multiplayer Unity | Unity 6000.3.15f1 / URP | [Phát triển và test game](#10-phát-triển-và-test-game-trong-unity) |

Hai nhánh auth không tự thay thế nhau:

- **RoomCode:** nhập mã phòng → server cấp Guid PlayerId → JoinAccepted → Ping/Pong. Không cần account/API cho việc join.
- **Jwt:** nhập mã phòng và account → API login → AuthRequired → gửi JWT → server xác minh sub → JoinAccepted. Dùng cho Local hoặc VPN phù hợp. Cả mã phòng và JWT phải đúng.
- **AWS public chỉ RoomCode.** API account AWS vẫn test qua HTTPS, nhưng token account chưa được dùng cho UDP public vì UDP hiện chưa mã hóa. `PUBLIC_HOSTING=true` chặn `AUTH_MODE=Jwt` ở GameServer.

## 2. Chuẩn bị và quy ước nơi chạy lệnh

| Máy / vai trò | Cần cài hoặc nhận | Tác dụng |
| --- | --- | --- |
| Dev backend trên Windows | SDK .NET 8.0.403 hoặc patch mới hơn cùng nhánh 8.0.4xx; Docker Desktop với Linux containers | Build client/debug; chạy server và Mongo local |
| Dev Unity | Unity **6000.3.15f1**, package URP của project; bộ công cụ backend ở trên khi test mạng | Phát triển client game |
| Tester chạy client từ source | SDK phù hợp + thư mục Backend đầy đủ | Chạy NetSmokeClient |
| Tester nhận client self-contained | Cả thư mục output publish | Không cần SDK, Docker hoặc SSH |
| Người xem DB | MongoDB Compass | Đọc users Local hoặc qua SSH tunnel AWS |
| Người chạy test tự động | Python **3.11+**, Docker Desktop, SDK .NET 8 | Test core và kiểm tra release bằng standard library |
| EC2 | Ubuntu **24.04 x86_64**, Docker/Compose do bootstrap cài | Chạy image đã build trên PC; không build SDK trên EC2 |

Mỗi dev lấy source mới nhất, bao gồm `Backend/Shared`, `Backend/global.json` và `Backend/Directory.Build.props`. Không copy `.env` hoặc private key của người khác. `global.json` chọn SDK 8.0.403/latestPatch; chỉ có SDK 9/10 hoặc 8.0.1xx chưa đáp ứng cấu hình này.

Trong hướng dẫn:

- **PC / PowerShell tại Backend:** terminal nằm ở thư mục `Backend` của checkout của bạn. Ví dụ máy chủ dự án dùng `D:/Unity/Jungle_Maze_Survival/Backend`; dev khác thay đường dẫn theo checkout của mình.
- **SSH EC2:** terminal đã đăng nhập bằng user `ubuntu`, thường có prompt `ubuntu@ip-...`. Chỉ chạy lệnh Bash/Linux tại đây.
- **AWS Console:** thao tác trên trình duyệt, region Sydney `ap-southeast-2`.
- Copy các lệnh trong code block; không copy phần prompt `PS ...>` hoặc `ubuntu@...$` từ log.

**PC, kiểm tra công cụ:**

```powershell
Set-Location 'D:/Unity/Jungle_Maze_Survival/Backend'
dotnet --version
docker version
docker compose version
dotnet build JungleBackend.sln
```

Docker Desktop phải mở và Engine ready. `dotnet --version` cần chọn 8.0.4xx khi đứng ở Backend. Package đã khai báo trong csproj; không cần chạy lại `dotnet new` hoặc `dotnet add package` theo hướng dẫn cũ.

## 3. Thiết lập local lần đầu

### 3.1. Tạo cấu hình riêng một lần

**PC / PowerShell tại Backend:**

```powershell
if (-not (Test-Path -LiteralPath '.env')) {
    Copy-Item -LiteralPath '.env.example' -Destination '.env'
}
```

Mở `.env` bằng editor. Đặt `ROOM_CODE` thành mã riêng; giữ `AUTH_MODE=RoomCode`, `PUBLISH_HOST=127.0.0.1`, `GAME_BIND_ADDRESS=0.0.0.0`, `GAME_PORT=7777`, `MAX_PLAYERS=10`, `TICK_RATE=30`.

`.env` chỉ ở máy host, đã ignore Git. Mã phòng là quyền vào buổi test, không dùng password tài khoản làm mã phòng. Với RoomCode chỉ chạy game-server, signing key mẫu chưa được dùng; nếu bật API/account phải tạo key thật theo mục 5.

### 3.2. Build và bật GameServer local

```powershell
docker compose up -d --build game-server
docker compose ps
docker compose logs --tail 20 game-server
```

Cần thấy game-server Up và log `[ready] ... maxPlayers=10 tickRate=30 authMode=RoomCode protocol=1`. Lần đầu Docker tải image SDK/runtime và restore dependency. Server bind trong container `0.0.0.0:7777`; Windows publish mặc định `127.0.0.1:7777/udp`.

RoomCode không cần Mongo/API chạy. Docker Desktop → Containers → nhóm Compose thường tên `backend` → game-server → Logs cũng xem được log. `docker compose logs -f game-server` theo dõi liên tục; Ctrl+C chỉ thoát xem log.

### 3.3. Test client local ở terminal PC thứ hai

```powershell
dotnet run --project NetSmokeClient/NetSmokeClient.csproj -- --config NetSmokeClient/client.local.json --name Dev01 --duration 30
```

Nhập mã phòng trong `.env`. Ký tự không hiện khi gõ là bình thường. Cần thấy JoinAccepted, Pong và summary `requested=1 connected=1 joined=1 failed=0`.

`transportConnected=true` mới là kết nối transport; **JoinAccepted + Pong** mới xác nhận server cho vào phòng và trao đổi UDP được. Nếu lỗi, dùng mục 12 trước khi thử AWS.

## 4. Chạy local những lần sau

### 4.1. Bắt đầu một buổi phát triển

1. Mở Docker Desktop, đợi Engine ready; mở đúng source và terminal tại Backend.
2. Chạy `docker compose up -d game-server` để bật lại bằng image đã có; kiểm tra log ready.
3. Chạy client local ở mục 3.3. Nếu cần account/API, dùng mục 5.1 trước.
4. Với phần Unity không phụ thuộc mạng, mở scene kiểm thử tương ứng. Xem mục 10 về giới hạn hiện tại.

Docker local **không có restart policy tự bật sau boot**. Qua Docker Desktop có thể Start game-server; nhánh account: Start Mongo → đợi healthy → Start API → kiểm tra /health → Start game-server. Nếu container chưa tạo hoặc config/source đổi, dùng Compose.

### 4.2. Lệnh nào dùng khi sửa gì?

| Thay đổi / tình huống | Thao tác tại PC / Backend |
| --- | --- |
| Bật lại, source/config không đổi | `docker compose up -d game-server` |
| Sửa C# GameServer hoặc Shared chạy trong Docker | `docker compose up -d --build game-server` |
| Sửa API chạy Docker | `docker compose --profile account up -d --build api` |
| Sửa `.env` / port / mode | Recreate service liên quan, ví dụ `docker compose up -d --force-recreate game-server`; account dùng `--profile account` |
| Sửa NetSmokeClient source | `dotnet run` build lại client trên PC |
| Sửa JSON endpoint client | Chạy lại client; không cần build |
| Sửa Unity asset/script client | Play/build lại Unity theo tính năng; server chỉ rebuild khi code server đổi |
| Pull source mới | Build solution; rebuild image của service có code/dependency thay đổi |

Nút Restart container dùng image/env cũ, không thay cho build hoặc recreate. Nếu API/game cùng dùng JWT và đổi key, recreate cả hai với cùng cấu hình.

### 4.3. Kết thúc buổi local

Dừng client bằng Ctrl+C hoặc đợi duration kết thúc. Sau đó:

```powershell
docker compose stop game-server
```

Nếu đã bật account:

```powershell
docker compose --profile account stop
```

Stop giữ named volume Mongo. `docker compose down` xóa container/network nhưng giữ named volumes; `down -v` xóa volume và có thể mất account. Giữ project name/volume cũ khi dùng lại dữ liệu. Trên máy riêng của mỗi dev, dữ liệu Local độc lập với AWS.

## 5. API account, Jwt và xem dữ liệu local

### 5.1. Bật API/Mongo để test tài khoản

API luôn cần signing key hợp lệ, kể cả game-server đang RoomCode. Tạo key trên **PC / PowerShell**, dùng được với Windows PowerShell 5.1:

```powershell
$jwtBytes = New-Object byte[] 48
$jwtGenerator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$jwtGenerator.GetBytes($jwtBytes)
$jwtGenerator.Dispose()
[Convert]::ToBase64String($jwtBytes)
```

Dán kết quả vào `Jwt__SigningKey` trong `.env` bằng editor; giữ key ổn định qua lần bật máy. Đây là secret của server, không gửi cho tester hoặc chụp màn hình. `Jwt__Issuer=JungleMaze.Local`, `Jwt__Audience=JungleMaze.Game`, expiry mặc định 60 phút.

```powershell
docker compose --profile account up -d --build
docker compose --profile account ps
```

Mongo cần healthy; API cần trả `{"status":"ok","database":"connected"}` tại `http://127.0.0.1:5000/health`. Compose Local chỉ có healthcheck của Mongo; API Up chưa thay thế GET /health. Game có thể giữ RoomCode, còn API được test độc lập.

### 5.2. Swagger / Postman

Local Docker: `http://127.0.0.1:5000/swagger`. AWS: `https://junglemaze.duckdns.org/swagger`; browser hỏi **Swagger admin** đã đặt khi deploy. Đó là Basic auth mở UI, khác account game và JWT Authorize.

| Bước | Thao tác | Kết quả |
| --- | --- | --- |
| 1 | POST /api/auth/register, nhập account demo riêng | 201, id/username/createdAtUtc; không trả hash |
| 2 | Đăng ký lại tên cùng hoa/thường | 409; thiếu/sai DTO → 400 |
| 3 | POST /api/auth/login bằng account vừa tạo | 200, accessToken/userId/expiresAtUtc; sai credentials → 401 |
| 4 | Authorize, dán riêng accessToken | Swagger tự thêm Bearer |
| 5 | GET /api/me | 200 đúng user; bỏ token/sửa token → 401 |

Tên đăng ký sau Trim dài 3–32 ký tự, tra cứu không phân biệt hoa/thường. Password đăng ký 8–128 ký tự; password login bắt buộc và tối đa 128 ký tự. Password giữ khoảng trắng và chỉ lưu hash có salt. Reload Swagger cần Authorize lại; register không tự login/join UDP.

Postman dùng cùng endpoint và JSON; /api/me chọn Bearer Token. Có thể import `/swagger/v1/swagger.json`; bản AWS cần Basic auth admin để tải tài liệu này. Không nhập signing key vào Postman/client.

AWS public: register/login dùng chung **60 request/phút toàn server**, vượt trả 429; body tối đa 16KB (Content-Length quá lớn trả 413). /health và /api/me không tiêu hạn mức auth. Local mặc định không áp dụng limiter này. Tránh burst đăng ký/login trên server lớp; để test tự động các ca giới hạn trên Docker local.

### 5.3. Nếu cần test Jwt join local

Sửa `.env` `AUTH_MODE=Jwt`; API và game-server dùng cùng issuer/audience/key. Sửa `NetSmokeClient/client.local.json` `authMode` thành `Jwt`, giữ apiBaseUrl Local. Recreate game-server bằng lệnh account và chạy client: nhập mã phòng, username, password → API login → UDP Auth → JoinAccepted/Pong.

Khi quay về RoomCode, đổi cả server và JSON client cho khớp. Token lỗi không fallback. Một JWT account chỉ giữ một phiên trong cùng GameServer; muốn 10 phiên Jwt cần 10 account khác nhau, không dùng một account với `--clients 10`.

### 5.4. Compass local

Start Mongo local, mở Compass → Add new connection → `mongodb://127.0.0.1:27018` → lưu tên Jungle Maze Local. Vào `jungle_maze → users → Documents`, Filter `{}`, Refresh sau khi register.

Có thể đặt Project `{ "PasswordHash": 0 }`, Sort `{ "CreatedAtUtc": -1 }`, hoặc Filter `{ "NormalizedUsername": "PLAYER01" }`. Project chỉ ẩn hash trên màn hình. Dữ liệu có `_id` (Guid string), Username, NormalizedUsername, PasswordHash, CreatedAtUtc. Tạo user qua API để giữ validation/hash/index đúng.

Mongo trong container dùng 27017, Windows mặc định dùng 27018. Nếu cổng đã bị chiếm, sửa MONGO_PORT rồi recreate Mongo, cập nhật URI Compass/API trực tiếp. Mongo Local chỉ publish loopback; account giữ trong volume thường tên `backend_mongo-data`.

## 6. Thiết lập AWS lần đầu

Chỉ admin cần làm phần này. Dev/tester dùng server đã có đi thẳng mục 7. **AWS phục vụ test chung; dựng backend và chạy test tự động trên PC trước khi bật EC2.**

### 6.1. Cấu hình hạ tầng bằng AWS Console

| Mục | Cấu hình đang dùng / cần xác nhận |
| --- | --- |
| Account / region | AWS (new), giữ Free Plan; Sydney ap-southeast-2; kiểm tra credits/thời hạn trong Billing |
| EC2 | Một instance Ubuntu 24.04 x86_64, t3.small 2 vCPU/2GB; CPU credit mode Standard |
| Storage | Root EBS gp3 20GB, mã hóa; bật termination protection |
| Network | Default VPC, subnet public có route Internet Gateway, auto-assign public IPv4 |
| SSH | Key pair riêng, user ubuntu; TCP 22 từ IP public của admin được phép |
| Public dịch vụ | TCP 80/443 và UDP 7777 từ Anywhere IPv4 |
| Nội bộ | Không mở API 5000 hoặc Mongo 27017/27018 public |
| DNS | DuckDNS hostname junglemaze.duckdns.org; token admin giữ riêng |

Xác nhận t3.small được console cho phép/eligible đối với **tài khoản của bạn**. Free Tier mới dùng credits, không áp bảng micro 750 giờ/tháng cho mọi tài khoản. AWS project của account hiện gán Sydney; giữ Free Plan, không activate advanced features để đổi region. Không tạo thêm Elastic IP, NAT Gateway, Load Balancer, RDS hoặc Route53 cho buổi test lớp. Xem [EC2 Free Tier](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/ec2-free-tier-usage.html) và [AWS project regions](https://docs.aws.amazon.com/accounts/latest/reference/project-regions.html).

Trước khi SSH: EC2 Running, status checks đạt, key pair đúng, TCP 22 My IP khớp mạng PC hiện tại. Đổi Wi-Fi/hotspot có thể phải cập nhật nguồn TCP 22. Bật MFA tài khoản AWS; không gửi password console/private key cho tester.

### 6.2. Tạo release trên PC

**PC / PowerShell tại Backend**, Docker Desktop đang chạy:

```powershell
dotnet build JungleBackend.sln
powershell -NoProfile -ExecutionPolicy Bypass -File Deploy/build-aws-release.ps1 -Domain junglemaze.duckdns.org
python -X utf8 Tests/verify_aws_release.py out/aws/release.tar.gz
```

Nếu Python không có trong PATH, dùng đường dẫn Python 3.11+ trên máy như mục 9. Builder build API/game Linux AMD64, lấy MongoDB **7.0.43** và Caddy 2, tag bằng release ID mới, đóng bốn image cùng scripts/config public/docs. Nó tạo:

- `Backend/out/aws/release.tar.gz` và `release.tar.gz.sha256` để upload.
- Trong gói: `manifest.json` với image IDs/version, `release.env` public, `SHA256SUMS`, `images.tar.gz`, client AWS JSON, hai tài liệu chính và hồ sơ nhập/kiểm chứng JSurviv để các liên kết trong docs dùng được. Assets Unity không nằm trong gói server.

Mỗi release có ID riêng; không dùng cùng ID cho nội dung khác. Không dùng `-SkipBuild` nếu chưa chủ động chuẩn bị đúng cả bốn image/tag; đó là tùy chọn tái đóng gói đã kiểm thử. Gói không chứa `.env`, token, SSH key hoặc dữ liệu Mongo. Thay tài liệu/Unity client không bắt buộc deploy lại server; backend/protocol đổi mới cần release tương ứng.

### 6.3. SSH lần đầu và upload

**PC / PowerShell tại Backend**. Các prompt sau yêu cầu giá trị thật; nhập đường dẫn key không có dấu ngoặc kép bao quanh:

```powershell
$ec2Key = Read-Host 'Duong dan tuyet doi file .pem cua ban'
$ec2Ip = Read-Host 'Public IPv4 hien tai cua EC2 trong Console'
if (-not (Test-Path -LiteralPath $ec2Key)) { throw 'Khong tim thay file key; sua duong dan.' }
if (-not $ec2Ip) { throw 'Can nhap Public IPv4 EC2.' }
ssh -i $ec2Key ("ubuntu@" + $ec2Ip)
```

Dùng Public IPv4 hiện tại, không dùng IP private `172.31...`, không để dấu `< >`. Hostname DuckDNS dùng được sau khi DNS đã cập nhật đúng IP.

Nếu hỏi `Are you sure you want to continue connecting (yes/no/[fingerprint])?`: AWS → instance → **Actions → Monitor and troubleshoot → Get system log** → tìm `BEGIN SSH HOST KEY FINGERPRINTS`; so với fingerprint cùng loại khóa đang hiển thị. Khớp thì gõ đầy đủ `yes` và Enter. Không bỏ kiểm tra host key. [Hướng dẫn AWS](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/connection-prereqs-general.html).

Thấy `ubuntu@ip-...` là đã vào EC2. Chạy `exit` để trở lại PowerShell PC, rồi upload bằng **cùng terminal PC** vẫn có hai biến trên:

```powershell
scp -i $ec2Key .\out\aws\release.tar.gz .\out\aws\release.tar.gz.sha256 ("ubuntu@" + $ec2Ip + ":~/")
ssh -i $ec2Key ("ubuntu@" + $ec2Ip)
```

Nếu mở terminal PC mới, nhập lại ec2Key/ec2Ip trước. SCP chạy ở PC; đường dẫn `out/aws` không tồn tại trong phiên SSH trước khi upload. Không nhầm fingerprint host với fingerprint key pair/private key.

### 6.4. Bootstrap và deploy lần đầu trên EC2

**SSH EC2:**

```bash
cd ~
sha256sum -c release.tar.gz.sha256
release_dir="$(mktemp -d "$HOME/junglemaze-release.XXXXXX")"
tar -xzf release.tar.gz -C "$release_dir"
cd "$release_dir"
sha256sum -c SHA256SUMS
sudo bash scripts/bootstrap.sh
sudo bash scripts/deploy.sh "$PWD"
```

Hai lượt checksum phải OK trước khi chạy script. Bootstrap kiểm tra Ubuntu 24.04/x86_64, cài Docker/Compose từ repo Docker, Python/curl/OpenSSL và swap 1GB. Không cài SDK .NET hay compile trên EC2. Deploy load image, kiểm tra guard volume, cài `/opt/junglemaze/releases/<release-id>`, giữ Compose project **junglemaze-aws**, tạo systemd/DuckDNS timer và khởi động services.

Nếu `.env.aws` chưa có, deploy hỏi cấu hình:

| Prompt | Nhập / tác dụng |
| --- | --- |
| DuckDNS hostname | Enter giữ junglemaze.duckdns.org, hoặc hostname thuộc tài khoản admin |
| DuckDNS token | Token trên tài khoản DuckDNS, nhập ẩn |
| Email for HTTPS certificate | Email thật cho ACME |
| Swagger admin username | Enter giữ teacher hoặc đặt riêng |
| Swagger password / repeat | 12–64 ký tự ASCII, nhập ẩn; chỉ lưu bcrypt hash |
| Room code | 8–64 ký tự chữ/số/_/-; Enter để tạo ngẫu nhiên |

JWT signing key được tự tạo. Config private ở `/opt/junglemaze/config/.env.aws`, quyền 0600; code/release tách khỏi config. Mã phòng cần gửi riêng cho người test:

```bash
sudo grep '^ROOM_CODE=' /opt/junglemaze/config/.env.aws
```

Giữ nguyên token/key/password qua các lần bật máy. `configure.sh` chỉ tạo lần đầu và từ chối ghi đè config đã có. Server/client JSON không nhận secrets từ file này. DB AWS mới, không tự copy DB Local.

### 6.5. Kiểm tra trước khi cho team vào

```bash
sudo bash /opt/junglemaze/current/scripts/status.sh
sudo systemctl status junglemaze --no-pager
sudo systemctl list-timers junglemaze-dns.timer --no-pager
```

Mongo/API phải healthy; game-server Up và log ready; Caddy Up. Systemd `active (exited)` là bình thường với service Type=oneshot; các tiến trình thật nằm trong Docker. Không coi game-server/Caddy Up là đã test UDP/TLS.

**PC / browser:** mở `https://junglemaze.duckdns.org/health`, phải có certificate hợp lệ và `status=ok, database=connected`. Sau đó test Swagger theo mục 5.2 và UDP theo mục 7.2. Caddy cấp/renew ACME qua DNS đúng và TCP 80/443; deploy thành công chưa tự chứng minh certificate public đã sẵn sàng.

### 6.6. MongoDB và kernel EC2

Bản AWS hiện khóa MongoDB **7.0.43**. Release đầu dùng 8.0.32 bị chặn trên kernel **7.0.0-1014-aws**; Docker local dùng kernel WSL 5.15 nên không bắt lỗi đó. Container dùng kernel host. [MongoDB SERVER-125742](https://jira.mongodb.org/browse/SERVER-125742) xác nhận nhánh7.0 không bị lỗi allocator này; kernel Ubuntu có fix nhận diện riêng ở [SERVER-131779](https://jira.mongodb.org/browse/SERVER-131779).

Nếu chuyển từ release 8/unknown sang7, `check-mongo-volume.sh` chỉ cho phép volume trống hoàn toàn. Volume có file thì dừng trước đổi current/service, cần migration có backup trên host tương thích. Không xóa file để vượt guard hoặc cho MongoDB 7 đọc data files của MongoDB 8. Cập nhật7→7 dùng lại volume; rollback khác major bị chặn. **Local Compose và core test vẫn dùng mongo:8.0 theo code**, chỉ AWS builder khóa7.0.43.

Nếu đang có release đầu bị lỗi, upload gói mới, giải nén vào thư mục mới như mục 6.4 rồi chạy **deploy**; bootstrap/configure không cần làm lại trên EC2 đã setup. Hướng dẫn thường ngày ở mục 7 không chạy lại bước này.

## 7. Bật và dừng AWS cho mỗi buổi test

### 7.1. Admin bật server đã cài

1. **AWS Console:** xem Billing/credits, thống nhất thời gian test với team; EC2 → chọn instance → Instance state → **Start instance** nếu đang Stopped.
2. Đợi Running/status checks. Systemd đã enable sẽ bật Docker services và cập nhật DuckDNS lúc boot, timer retry mỗi phút.
3. Mở HTTPS /health. Nếu cần SSH, dùng Public IPv4 mới hoặc hostname sau khi DNS cập nhật; key vẫn là key của instance.
4. Kiểm tra `status.sh`; nếu đã Stop app thủ công khi EC2 vẫn Running, dùng `sudo systemctl start junglemaze` rồi kiểm tra lại.
5. Chỉ cho tester vào khi /health đúng, game ready, room code đúng. Không chạy lại bootstrap/deploy để bật server cũ.

Nếu chưa sẵn sàng, xem mục 12. IP/DNS có thể cần chờ; không tắt xác minh certificate. Khi bật lại, TLS data/config Mongo vẫn nằm trên EBS/named volumes, không đăng ký lại account chỉ vì restart.

### 7.2. Tester test online từ PC

Tester nhận source Backend hoặc client self-contained, hostname/cổng, mã phòng, release/protocol và lịch test. Tester không cần Docker, AWS account, key SSH hoặc DuckDNS token.

**PC / PowerShell tại Backend:**

```powershell
dotnet run --project NetSmokeClient/NetSmokeClient.csproj -- --config NetSmokeClient/client.aws.json --name Tester01 --duration 30
```

Nhập mã phòng AWS. Cần JoinAccepted/Pong, summary joined=1/failed=0. AWS config là Online, `junglemaze.duckdns.org:7777`, RoomCode, protocol 1. Chạy ở mạng nhà/lớp/hotspot để ghi RTT và phát hiện mạng chặn UDP. HTTPS health thành công không thay cho bài UDP này; `Test-NetConnection -Port 7777` chỉ kiểm tra TCP.

Sau bài một client, dùng bài 10/11 ở mục 8. Lệnh `--duration` dừng **client**, không dừng AWS. Không bật một EC2 riêng cho mỗi tester.

### 7.3. Kết thúc buổi: dừng app và Stop EC2

1. Tester dừng client hoặc đợi duration kết thúc; admin lưu kết quả và backup khi cần.
2. **SSH EC2:**

```bash
sudo systemctl stop junglemaze
```

3. **AWS Console:** EC2 → instance → **Instance state → Stop instance → Stop** thông thường. Đợi trạng thái **Stopped** và ghi nhận đã dừng. Terminate có thể xóa root EBS; không dùng khi còn cần dữ liệu.

Nếu mất SSH, vẫn dùng Stop thông thường từ Console để AWS thực hiện shutdown. Chỉ thoát SSH/đóng VS Code/dừng Docker chưa dừng compute của EC2 đang Running. [AWS Stop/Start](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/Stop_Start.html).

### 7.4. Những khoản tiêu hao credits và cách hạn chế

| Khoản của phương án này | Running | Stopped |
| --- | --- | --- |
| Compute t3.small | Tính theo thời gian chạy, kể cả idle/app lỗi | Ngừng compute |
| IPv4 public tự cấp | Phát sinh phí IP được cấp | AWS giải phóng IP khi Stop |
| EBS gp3 20GB | Phí dung lượng đã cấp | Vẫn tính để giữ OS/config/DB |
| Transfer | Có thể tính phần usage ngoài allowance | Instance không truyền dữ liệu khi Stopped |

Credits bù phí usage đủ điều kiện. Stop EC2 giảm phần lớn usage nhưng EBS vẫn tồn tại. Giữ CPU **Standard** theo kế hoạch; CPU credits T3 là quota burst khác credits USD trong Billing. [EC2 lifecycle](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/ec2-instance-lifecycle.html), [IPv4](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/using-instance-addressing.html), [T3 modes](https://aws.amazon.com/ec2/instance-types/t3/).

Trước/sau buổi test vào **Billing and Cost Management → Home / Free Tier / Credits**, xem plan, số dư và hạn. **Free Plan kết thúc sau 6 tháng hoặc khi hết credits**, điều kiện đến trước; mất quyền truy cập khi account đóng. Paid Plan có thể tính phí vượt credits. Credits có hạn riêng 12 tháng, không kéo dài Free Plan 6 tháng. [AWS plans](https://docs.aws.amazon.com/awsaccountbilling/latest/aboutv2/free-tier-plans.html), [credits expiry](https://aws.amazon.com/free/free-tier-faqs/).

Quy tắc vận hành của team:

- Dev/debug, core tests, kiểm tra limiter và build release trên PC; chỉ bật AWS để nghiệm thu từ xa.
- Chuẩn bị client/source/mã phòng trước khi Start; test một client 30 giây trước khi test10 phiên300 giây. Dừng theo lịch, không để server qua đêm vì quên.
- Sau buổi, một admin xác nhận **Stopped**; đóng cửa sổ client hoặc thấy website ngừng chưa chứng minh EC2 đã dừng.
- Không tạo thêm instance/IP/dịch vụ trả phí cho bài lớp. Ngưỡng nội bộ: còn<10 USD hoặc còn<7 ngày tới hạn thì backup và dừng để xem lại kế hoạch, không tự nâng Paid Plan.
- Định kỳ tải backup về PC; stopped disk vẫn tiêu usage và account hết hạn có thể mất quyền truy cập. Không đổi policy/xóa volume để giảm phí nếu chưa có bản sao dữ liệu cần giữ.

## 8. Bài kiểm thử và ghi kết quả

### 8.1. Một client và ca lỗi

Dùng JSON đúng môi trường. `--duration 30` nghĩa đo 30 giây sau warm-up (mặc định warm-up 0); bỏ duration/đặt 0 chạy tới Ctrl+C. `--clients` nhận 1–256. Ping mặc định 1Hz; `--ping-rate 30` cho benchmark, `--quiet` giảm log. Tên hiển thị 1–32 ký tự sau Trim; mã phòng client/server 1–64 ký tự, AWS configure yêu cầu 8–64.

| Bài | Kết quả cần ghi |
| --- | --- |
| Đúng room/mode/protocol | JoinAccepted + Pong, joined=1/failed=0 |
| Sai room | InvalidRoomCode; không giữ slot |
| Mode client khác server | AuthModeMismatch; không fallback |
| JSON protocolVersion sai | Client báo config trước connect; test tự động kiểm tra server reject |
| Login Jwt sai password | HTTP 401, dừng trước UDP |
| JWT giả/hết hạn/sai key/issuer/audience | InvalidToken |
| Cùng JWT account đang nối, thêm phiên | DuplicateSession; phiên đầu giữ nguyên |
| Dừng client rồi join lại | Trả slot; RoomCode Guid mới, Jwt cùng sub |
| Dừng host/mất mạng | Timeout/disconnect; không tự reconnect |

Packet thiếu/thừa/fragmentation, pending chưa Auth và auth deadline được test tự động; không sửa code sản phẩm để gửi byte thử bằng tay. Client exit 0 khi tất cả phiên đã join, có Pong và không lỗi; exit 1 cho reject/không có Pong, kể cả bài cố ý thử thứ 11; hủy nhập/login có thể trả 130.

### 8.2. RoomCode 10/11 phiên

Giới hạn **phòng mặc định** là **10 phiên đang kết nối cùng lúc**, không phải tổng 10 lượt join từ khi server bật. Phòng tạo thêm có sức chứa riêng; tổng mọi phòng còn chịu `MAX_TOTAL_PLAYERS`. `--duration 30` kết thúc thì client disconnect và trả slot; chạy lại liên tục vẫn join được là đúng. RoomCode cấp ID mới cho mỗi phiên, không khóa theo tên hiển thị hay IP. Khi mất mạng đột ngột, slot được trả sau timeout.

Phòng phải trống trước khi đo. **PC / Backend**, ví dụ AWS (Local thay file config thành client.local.json):

```powershell
dotnet run --project NetSmokeClient/NetSmokeClient.csproj -- --config NetSmokeClient/client.aws.json --name Capacity --clients 10 --duration 300
```

Nhập mã phòng; cần joined=10/failed=0 và server active=10/pending=0. Trong lúc đó, terminal khác chạy bài một client: **RoomFull**, exit 1 là đúng. Khi client đã dừng/trả slot, chạy lại bài một client cần thành công.

Ví dụ **Local**, mở hai terminal PowerShell tại Backend. Terminal A giữ đủ 10 phiên đến khi nhấn Ctrl+C:

```powershell
dotnet run --project NetSmokeClient/NetSmokeClient.csproj -- --config NetSmokeClient/client.local.json --name HoldRoom --clients 10 --duration 0
```

Đợi đủ 10 dòng `JoinAccepted` và có `Pong`. Giữ terminal A chạy, terminal B thử thêm một phiên:

```powershell
dotnet run --project NetSmokeClient/NetSmokeClient.csproj -- --config NetSmokeClient/client.local.json --name ExtraPlayer --duration 10
```

Kỳ vọng terminal B: `disconnectedOrRejected reason=RoomFull`, summary `joined=0 failed=1`. Trong Docker Desktop, Logs của game-server có `[rejected] reason=RoomFull` và `serverSlots=10/50` theo mặc định. Có thể chạy B lại khi A còn chạy: vẫn phải RoomFull. Sau Ctrl+C ở A, chạy B lại phải join/Pong thành công. Đảm bảo hai terminal dùng cùng config/endpoint và mã phòng.

Có thể thử burst 11 với `--clients 11 --duration 15`: phòng trống thì joined=10/failed=1. Các console đang mở đều chiếm slot; một tool 10 phiên không phải 10 người chơi thật. Sau smoke test mới tổ chức 10 máy thực, mỗi máy tên riêng, ghi OS/build/mạng và endpoint.

### 8.3. Số đo cần ghi

| Số đo | Ý nghĩa / đọc đúng |
| --- | --- |
| joined/failed, sent/pongs/timedOut/outstanding | Kết quả client; outstanding cuối lượt chưa tự coi là mất |
| rttMs / rttP95Ms | Mạng + scheduling hai đầu; không phải FPS |
| active/pending/peers | Tổng slot active+pending mọi phòng, tối đa 50; xem roomSlots cho từng phòng |
| ticksPerSecond | Gần 30 theo cửa sổ; tick server không phải render FPS |
| tickP95Ms/p99, loopP95Ms/p99 | Tick và poll+tick work; so budget 33.33ms |
| cpuOneCorePercent | CPU process chuẩn một core; 100% = một core bận, có thể>100% |
| tickOverruns/droppedTicks | Quá ngân sách/bỏ backlog; nếu>0 ghi tình trạng quá tải |

Đo idle riêng và 10 client riêng khoảng 5 phút; mục tiêu idle CPU <5% theo chuẩn một core là mục tiêu cần đo, không tự ghi đạt. AWS xem `status.sh`, game logs và Docker stats; Local xem Docker Desktop. Ghi cách tính CPU khi so Task Manager/Docker Stats.

Tick hiện rỗng, ping khoảng 1 lần/giây/client; bài này chưa đại diện combat/FPS/full match. Không cam kết độ mượt từ một Pong hoặc từ việc dùng AWS; Sydney→Việt Nam phải đo RTT thực tế.

## 9. Test tự động và gửi client cho tester

### 9.1. Core suite trên PC

Docker Desktop ready, SDK .NET 8, Python 3.11+. Lần đầu cần image Mongo local/core test:

```powershell
docker pull mongo:8.0
.\Tests\run_all.cmd
```

Script build solution, chạy NetworkChecks và auth_smoke; dừng nếu fail. Nếu `python` không ở PATH, đặt `PYTHON_EXE` trỏ tới Python thật, ví dụ hỏi đường dẫn:

```powershell
$env:PYTHON_EXE = Read-Host 'Duong dan executable Python 3.11+ tren may'
.\Tests\run_all.cmd
```

Suite dùng process/container/DB/port riêng, không đọc `.env` hay DB team; logs trong `.utmp`. Mongo core test vẫn 8.0; nếu host kernel bị lỗi nhánh 8 thì xem mục 12, không xóa DB để sửa. Chạy riêng: `dotnet run --project Tests/NetworkChecks/NetworkChecks.csproj`, `python -X utf8 Tests/auth_smoke.py` sau build solution.

### 9.2. AWS images và guard, chạy trên Docker PC

Sau build release, lấy **Release ID** builder đã in, rồi:

```powershell
$releaseId = Read-Host 'Release ID vua build'
python -X utf8 Tests/aws_smoke.py --release-id $releaseId
python -X utf8 Tests/verify_aws_release.py out/aws/release.tar.gz
```

aws_smoke chạy bốn services trong project/volumes riêng, port loopback ngẫu nhiên; TLS test dùng internal CA có xác minh certificate, không sửa Caddy ACME của release. Nó test HTTPS/Swagger/413/429/JWT/DB backup-restore/persist và 10 UDP client. Không chạy suite này trên EC2 đang phục vụ lớp.

Guard test cần Docker CLI Linux và MongoDB 7 image; chỉ dùng volume synthetic:

```powershell
docker pull docker:29-cli
docker pull mongo:7.0.43
python -X utf8 Tests/aws_mongo_guard.py
```

### 9.3. Tester không có SDK

**PC admin/dev tại Backend:**

```powershell
dotnet publish NetSmokeClient/NetSmokeClient.csproj -c Release -r win-x64 --self-contained true -o out/NetSmokeClient
```

Gửi **cả thư mục** `out/NetSmokeClient`, không gửi `.env` hay key. Bốn file client.*.json được copy tự động. Tester mở terminal trong thư mục nhận:

```powershell
.\NetSmokeClient.exe --config .\client.aws.json --name Tester02 --duration 30
```

Local chọn client.local.json; LAN/VPN sửa IP mẫu rồi chọn file tương ứng. Endpoint JSON không chứa room/password/token; nhập mã phòng khi chạy. Đây là console smoke client, chưa phải build game Unity.

## 10. Phát triển và test game trong Unity

### 10.1. Việc dev làm hiện tại

Mở project bằng Unity 6000.3.15f1/URP; tham khảo [hướng dẫn nhập JSurviv](JSurviv_Integration.vi.md). Test riêng UI, animation/effect/config và logic không phụ thuộc server trong scene kiểm thử phù hợp. `AutoConnectLegacy=false` giữ chế độ tham khảo; không bật Socket.IO cũ để thử AWS LiteNetLib. Không coi scene tham khảo là demo gameplay offline hoàn chỉnh.

Nếu sửa backend, test local trước; nếu chỉ sửa Unity client, Play/build lại phần client. Khi mạng Unity chưa được nối, NetSmokeClient là công cụ nghiệm thu backend.

### 10.2. Mốc cần triển khai tiếp

1. Unity network client LiteNetLib: đọc endpoint, nhập room, join/disconnect, lỗi và PlayerId; khớp protocol backend.
2. Hai client Unity (Editor + Windows build): server quản lý spawn/despawn, nhận input, mô phỏng vị trí và phát trạng thái; thấy nhau di chuyển.
3. Thêm combat/HP/item/inventory/round state, snapshot/interpolation/prediction và đo jitter/loss/FPS theo GDD.
4. Hai máy qua AWS rồi 10 máy thật. Gửi cả thư mục build game; tester không cần Unity Editor/SDK/SSH.

Đây là định hướng, **chưa có menu chọn endpoint/UnityNetClient trong code hiện tại**. Endpoint dự kiến: Local 127.0.0.1:7777, LAN IP host, AWS junglemaze.duckdns.org:7777. Backend là .NET server độc lập, chưa chạy Unity physics/scene ở server; cần thiết kế phần mô phỏng phù hợp trước khi đồng bộ combat. Shared/protocol đổi thì cập nhật cả server/client cùng lượt; server chưa hỗ trợ nhiều version song song.

## 11. Cập nhật AWS, backup và restore

### 11.1. Release backend mới

Dev test Local/core suite trước, admin build release trên PC theo mục 6.2. Giữ một admin triển khai trong buổi, báo tester dừng vì update có thể ngắt phiên. Upload hai file bằng SCP theo mục 6.3, SSH EC2:

```bash
cd ~
sha256sum -c release.tar.gz.sha256
release_dir="$(mktemp -d "$HOME/junglemaze-release.XXXXXX")"
tar -xzf release.tar.gz -C "$release_dir"
cd "$release_dir"
sha256sum -c SHA256SUMS
sudo bash scripts/deploy.sh "$PWD"
sudo bash /opt/junglemaze/current/scripts/status.sh
```

Mỗi bundle dùng thư mục giải nén mới. Deploy giữ config/key/volume và trỏ current sang release mới; không gọi configure nếu config đã có. Giữ lại image/release trước để rollback; không prune image/volume tùy tiện.

Bản cài `/opt/junglemaze/releases/...` đã bỏ `images.tar.gz` để giảm bản sao, nên **không chạy deploy từ current** hoặc checksum toàn bộ bản cài như bundle. Deploy phải nhận thư mục giải nén đầy đủ có images.tar.gz/SHA256SUMS. Sau update kiểm tra /health, Swagger và UDP; kết thúc vẫn Stop EC2.

### 11.2. Backup DB và tải về PC

**SSH EC2:**

```bash
sudo bash /opt/junglemaze/current/scripts/backup.sh
```

Backup mongodump chỉ DB `jungle_maze`; file private `/opt/junglemaze/backups/users-...archive.gz`. Chỉ file thành công mới được đặt tên cuối. Copy đúng đường dẫn script đã in để ubuntu có quyền tải:

```bash
read -r -p 'Duong dan backup day du vua tao: ' backup_path
sudo install -m 600 -o ubuntu -g ubuntu "$backup_path" /home/ubuntu/junglemaze-backup.archive.gz
```

**PC / Backend** (ec2Key/ec2Ip đã đặt trong terminal PC):

```powershell
scp -i $ec2Key ("ubuntu@" + $ec2Ip + ":~/junglemaze-backup.archive.gz") .\out\aws\
```

Backup chứa user/hash: giữ riêng, không commit/chia sẻ cho tester. Backup nằm cùng EBS chưa chống được mất account/volume, cần bản trên PC. DB dump không chứa private config/JWT signing key/DuckDNS token/Caddy cert; admin lưu secrets an toàn riêng để phục hồi. Backup Mongo không giữ game state hiện tại vì server chỉ có session trong RAM.

### 11.3. Restore và rollback

Restore chỉ dành cho admin đã có backup đúng. Upload backup vào EC2 rồi **SSH EC2**:

```bash
read -r -p 'Duong dan archive backup can restore: ' backup_path
sudo bash /opt/junglemaze/current/scripts/restore.sh "$backup_path"
```

Nhập `RESTORE` vì script `--drop` các collection trong archive thuộc jungle_maze; API/Caddy tạm dừng và được bật lại khi script kết thúc. Game RoomCode vẫn độc lập account; test API/Compass lại và kiểm tra lỗi trước khi cho lớp dùng.

Rollback: `sudo bash /opt/junglemaze/current/scripts/rollback.sh`. Cần previous release và image cũ còn trong Docker, cùng major MongoDB; rollback giữ DB/schema/config, không hoàn tác dữ liệu. No previous release hoặc khác major thì bị chặn. Mọi migration DB cần kế hoạch riêng, không dùng rollback code để ép hạ database.

### 11.4. Compass AWS qua SSH tunnel

**SSH EC2:** lấy IP Mongo nội bộ (không in env):

```bash
sudo docker inspect --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' junglemaze-aws-mongo-1
```

**PC**, terminal riêng, dùng key/IP EC2 đã biết; nhập IP Mongo vừa lấy:

```powershell
$mongoIp = Read-Host 'IP container Mongo vua lay tu EC2'
ssh -i $ec2Key -N -L ("127.0.0.1:27019:" + $mongoIp + ":27017") ("ubuntu@" + $ec2Ip)
```

Giữ terminal mở. Compass URI `mongodb://127.0.0.1:27019` → jungle_maze/users. Local là 27018, AWS tunnel là 27019. Recreate Mongo có thể đổi IP nội bộ: lấy lại và mở lại tunnel. Không mở Mongo port trong SG.

### 11.5. Dev khác và quyền vận hành

Mỗi dev có Local backend/DB/config riêng. Tester chỉ nhận build/config public/room và lịch; phòng mặc định server chung có 10 slot. Phòng bổ sung cần bản mở rộng đã được admin triển khai và xác nhận mức tải. Chỉ admin được SSH/deploy/backup/Stop-Start AWS; cấp key riêng cho admin bổ sung, không dùng chung private key hay account AWS. Khi cấp SSH thêm, admin phải cho phép đúng IP của dev trong rule TCP 22 và thêm public key của họ vào authorized_keys theo quy trình của team. Quyền sudo user ubuntu là quyền quản trị rộng, nên chỉ cấp cho người cần vận hành.

## 12. Chẩn đoán và debug

### 12.1. Lệnh lấy lỗi trên EC2

```bash
sudo journalctl -u junglemaze.service -b -n 80 --no-pager -o cat
sudo docker ps -a --filter label=com.docker.compose.project=junglemaze-aws --format 'table {{.Names}}\t{{.Status}}'
sudo docker logs --tail 80 junglemaze-aws-mongo-1
sudo docker logs --tail 80 junglemaze-aws-api-1
sudo docker logs --tail 80 junglemaze-aws-game-server-1
sudo docker logs --tail 80 junglemaze-aws-caddy-1
sudo journalctl -u junglemaze-dns -n 20 --no-pager
```

Chọn log của service lỗi; không dump `.env.aws`, docker inspect toàn bộ env hoặc bật Bash `-x` để chia sẻ. Mongo unhealthy sẽ làm API/Caddy chờ; cần đọc Mongo log trước. Dừng EC2 nếu kết thúc buổi debug để tránh quên Running.

### 12.2. Bảng lỗi thường gặp

| Hiện tượng | Kiểm tra / sửa |
| --- | --- |
| Could not resolve hostname `<>` | Nhập Public IPv4 thật bằng prompt ở mục 6.3; không để placeholder |
| Host key verification failed sau câu hỏi yes/no | So fingerprint host rồi gõ yes; nếu báo key changed thì xác minh instance/DNS trước, không tắt kiểm tra hoặc xóa known_hosts mù |
| SSH timeout / Permission denied | EC2 Running, Public IP mới, TCP 22 / My IP, user ubuntu, key pair đúng; file key tồn tại/quyền riêng |
| Checksum FAIL | Upload lại gói/sha cùng phiên bản vào thư mục mới; dừng trước chạy script |
| Release ID already exists with different contents | Build ID mới; không sửa/ghi đè release cũ |
| Mongo Restarting(1), log kernel 6.19 | AWS dùng release MongoDB 7.0.43; xem mục 6.6 và guard, không xóa DB |
| Refusing MongoDB 8/unknown→7 | Volume không trống; giữ data, cần migration riêng |
| /health lỗi hoặc Mongo/API unhealthy | Container/log, config/key, Mongo index/connection; health pings DB |
| HTTPS certificate lỗi | DuckDNS/IP mới, TCP 80/443, Caddy log; không bỏ kiểm certificate |
| Swagger 401 trước mở UI AWS | Nhập admin Basic auth; /api/me 401 cần Bearer JWT account |
| API 429 / body 413 | Hạn mức 60/phút chung auth / body 16KB; sửa payload/chờ cửa sổ, test burst ở Local |
| Local được, LAN/AWS UDP timeout | JSONhost/port, mappingUDP, SG/firewall; TCP health không chứng minhUDP |
| RoomFull / DuplicateSession | Phiên cũ/pending hoặc cùngJWTsub; dừng/chờ cleanup rồi thử |
| InvalidToken | Key/issuer/audience/HS256/lifetime/đồng hồ; không fallback RoomCode |
| Compass thiếu user | Đúng DB/port Local-vs-AWS, filter {}, refresh; dữ liệu hai môi trường khác nhau |
| CPU/tick backlog | Đọc p95/p99/overrun/drop, CPU/RAM; record tải thực, không tự đánh đạt |

### 12.3. MSB4062 do C# Dev Kit

C# Dev Kit 11.3.30 chèn task `WriteProjectDataSliceTask` cần runtime 10 vào build SDK 8. `Backend/Directory.Build.props` đặt `EnableProjectDataOnBuild=false` cho build thường; giữ file này khi clone. Máy có SDK 10 vẫn dùng SDK 8 theo global.json.

Nếu chưa cập nhật file props, dùng tạm trên **PC / Backend**:

```powershell
dotnet build NetSmokeClient/NetSmokeClient.csproj -p:EnableProjectDataOnBuild=false
dotnet NetSmokeClient/bin/Debug/net8.0/NetSmokeClient.dll --config NetSmokeClient/client.aws.json --name Tester01 --duration 30
```

Chỉ chạy DLL sau build thành công. Có thể đối chiếu PowerShell từ Start ngoài VS Code để tránh môi trường extension. Không sửa target framework/DLL extension để giải quyết bài test này.

### 12.4. Debug backend trực tiếp bằng .NET trên PC

`.NET run` trực tiếp **không tự đọc Backend/.env**. Docker mới nạp env_file. Giữ terminal riêng cho server và client, để nhập secret bằng console tương tác.

GameServer RoomCode: dừng container game để tránh chiếm port; **PC / Backend**:

```powershell
docker compose stop game-server
$env:GAME_BIND_ADDRESS = '127.0.0.1'
$env:GAME_PORT = '7777'
$env:MAX_PLAYERS = '10'
$env:TICK_RATE = '30'
$env:AUTH_MODE = 'RoomCode'
$env:PUBLIC_HOSTING = 'false'
$env:ROOM_CODE = [System.Net.NetworkCredential]::new('', (Read-Host 'Room code local' -AsSecureString)).Password
dotnet run --project GameServer/GameServer.csproj
foreach ($settingName in @('ROOM_CODE', 'GAME_BIND_ADDRESS', 'GAME_PORT', 'MAX_PLAYERS', 'TICK_RATE', 'AUTH_MODE', 'PUBLIC_HOSTING')) {
    Remove-Item -LiteralPath ("Env:" + $settingName) -ErrorAction SilentlyContinue
}
```

Ctrl+C dừng server; lệnh cleanup bên dưới dotnet run xóa các biến vừa đặt để chúng không override `.env` khi quay về Docker. Có thể đóng terminal debug rồi mở terminal PC mới. Jwt debug còn cần issuer/audience/key giống API. Docker quay lại bằng `docker compose up -d game-server` từ môi trường sạch.

API: bật Mongo, dừng API container để tránh nhầm endpoint, cấp môi trường debug:

```powershell
docker compose --profile account up -d mongo
docker compose --profile account stop api
$env:Mongo__ConnectionString = 'mongodb://127.0.0.1:27018'
$env:Mongo__Database = 'jungle_maze'
$env:Jwt__Issuer = 'JungleMaze.Local'
$env:Jwt__Audience = 'JungleMaze.Game'
$env:PUBLIC_HOSTING = 'false'
$env:Jwt__SigningKey = [System.Net.NetworkCredential]::new('', (Read-Host 'Signing key local trong .env' -AsSecureString)).Password
dotnet run --project Api/Api.csproj --launch-profile http
foreach ($settingName in @('Jwt__SigningKey', 'Jwt__Issuer', 'Jwt__Audience', 'Mongo__ConnectionString', 'Mongo__Database', 'PUBLIC_HOSTING')) {
    Remove-Item -LiteralPath ("Env:" + $settingName) -ErrorAction SilentlyContinue
}
```

Profile http dùng `http://localhost:5204/swagger`; nếu client Jwt login API này, apiBaseUrl phải dùng cổng 5204. Mongo container hostname `mongo` chỉ dùng trong Docker; API Windows dùng loopback 27018. Compass vẫn đọc cùng DB Local.

### 12.5. LAN hoặc VPN, nhánh tùy chọn

LAN RoomCode: host lấy IPv4 Wi-Fi/Ethernet thật, đổi PUBLISH_HOST trong `.env` thành IP đó, giữ bind container 0.0.0.0, recreate game-server. Tester sửa client.lan.json tới IP/cổng host. Admin Windows mở UDP 7777 cho LocalSubnet trên profile Private bằng firewall; không tắt firewall. Wi-Fi trường có thể client isolation hoặc chặn UDP.

Ví dụ **PowerShell Run as administrator trên host LAN**, khi GAME_PORT mặc định 7777 và mạng được phép dùng profile Private:

```powershell
$lanHostIp = Read-Host 'IPv4 Wi-Fi/Ethernet cua host LAN'
New-NetFirewallRule -DisplayName 'Jungle Maze LAN UDP 7777' -Direction Inbound -Action Allow -Protocol UDP -LocalPort 7777 -LocalAddress $lanHostIp -RemoteAddress LocalSubnet -Profile Private
```

Nếu host publish cổng khác, thay LocalPort cho khớp. Mạng trường Public cần chính sách/rule được quản trị cho phép, không tự đổi profile để vượt hạn chế. Tester chạy bài mục 3.3 với `client.lan.json` sau khi sửa IP/cổng. Muốn Jwt trong LAN, client cần API HTTPS hoặc đường VPN được cấu hình; chỉ mở cổng HTTP LAN chưa đủ.

VPN: chỉ khi team chọn nhánh này, cấu hình quyền VPN/firewall trước; IP Tailscale 100.64.0.0/10 trong client.online.json không tự bật VPN. Client Jwt cho phép HTTP qua IP này khi mode Online, nhưng admin phải xác nhận VPN thực tế. API và UDP cần quyền tới đúng cổng; Mongo vẫn loopback. Kiểm tra điều kiện gói/quyền sharing hiện hành tại [Tailscale sharing](https://tailscale.com/docs/features/sharing) trước buổi lớp. AWS public là nhánh online chính của dự án, không cần VPN cho tester.

## 13. Cấu hình tham chiếu và kết quả đã biết

### 13.1. Server và endpoint

| Trường | Local / Docker | AWS |
| --- | --- | --- |
| Config private | Backend/.env, từng máy | /opt/junglemaze/config/.env.aws, root-only |
| Game bind / host port | 0.0.0.0:7777 trong container; PUBLISH_HOST:GAME_PORT trên Windows | 0.0.0.0:7777/udp public |
| API | PUBLISH_HOST:API_PORT, mặc định 127.0.0.1:5000 | HTTPS 443 qua Caddy; API 5000 private |
| Mongo | MongoDB 8.0, host loopback MONGO_PORT mặc định 27018 | MongoDB 7.0.43 private 27017; Compass qua SSH 27019 |
| Auth mode / PUBLIC_HOSTING | RoomCode/Jwt; mặc định false | RoomCode/true |
| MAX_PLAYERS / TICK_RATE | 1–10 / bắt buộc 30 | 10 / 30 |
| ROOM_CODE | 1–64 ký tự không trống | Configure 8–64 chữ/số/_/- |
| AUTH_TIMEOUT_MS | 500–30000, mặc định 5000, pending Jwt | RoomCode không dùng pending Jwt |
| METRICS_INTERVAL_SECONDS | 1–60, mặc định 5 | 5 |
| Jwt issuer/audience/key | Key ≥32 UTF-8 byte khi API/Jwt bật | API key riêng; game RoomCode không nhận JWT key |
| Jwt__ExpiryMinutes | 1–1440, mặc định 60 | API mặc định 60 |
| Swagger | SWAGGER_ENABLED trong Local Compose | Bật API, proxy Basic auth UI/JSON |

Client JSON có `mode` Local/LAN/Online, `gameServerHost` (IP/hostname không có scheme/port), `gameServerPort` 1–65535, `authMode` RoomCode/Jwt, `protocolVersion` 1, `connectTimeoutMs` 500–60000. apiBaseUrl chỉ cần với Jwt: localhost HTTP, hoặc HTTPS hợp lệ, hoặc mode Online/Tailscale đã có VPN. Sửa JSON không cần build; không thêm secret hay trường lạ vì parser từ chối unknown field.

### 13.2. Bằng chứng và nghiệm thu còn lại

Ghi nhận trong phiên ngày 12/10/2026: build solution 0 warning/error; **69 network checks +48 API/e2e checks** đã qua trên Local. Images AWS MongoDB 7 đã qua **35 AWS Docker checks +8 volume guard checks**; gói release được kiểm checksum/file/images/không private config. Đã tái hiện và sửa MSB4062 bằng target C# Dev Kit thật.

Admin báo đã triển khai bản MongoDB 7 lên EC2; **HTTPS /health đã được gọi thành công, status=ok, database=connected**, ở thời điểm kiểm tra trong phiên. Đây là ghi nhận lịch sử, không khẳng định server đang Running khi bạn đọc. Chưa có log nghiệm thu UDP online 10 máy, tải gameplay hoặc FPS Unity; phải ghi kết quả khi thực hiện.

| ID Tracking | Có trong code | Team cần ghi kết quả thực |
| --- | --- | --- |
| INF-01 | Docker/config Local và AWS, release/HTTPS/volume/systemd | Endpoint thật, boot/Stop-Start, UDP hai máy, dữ liệu tồn tại |
| NET-01 | Register/login/JWT/hash/index/Swagger | Account API từ xa nếu dùng, Compass đúng DB |
| SRV-01 | LiteNetLib/30Hz/metrics/10 slot/ping-pong | Idle / 10 client / 5 phút, RTT/CPU/RAM/p95/p99/drop |
| SRV-02 | Version/room/mode/identity/auth deadline/duplicate/cleanup | Reject, mất mạng, trả slot, protocol client-server khớp |

Cập nhật 16/10: Local 132 network +50 account checks và 10 phiên/300s PASS; API AWS 8/8 PASS; đường UDP PC→EC2 và prerequisites VPN BLOCKED. Các mục hai PC, đồng thời Local/AWS và rejoin sau Stop–Start chưa nghiệm thu; xem mục 15 và báo cáo chung.

Mỗi lượt ghi ngày/giờ, OS/CPU/RAM, source/release ID, endpoint/authmode, số client, đường mạng, logs đã che secrets và kết quả. Giữ đúng bốn ID này; không đánh Done phần gameplay chỉ vì smoke test qua. Mốc tiếp theo là nối Unity và đồng bộ hai nhân vật như mục 10.

## 14. Tạo nhiều phòng và đo sức chứa backend

### 14.1. Bật chức năng tạo phòng trên local

Thực hiện trong PowerShell tại **Backend**, sau khi tạo `.env` ở mục 3 và thiết lập `Jwt__SigningKey` random hợp lệ theo mục 5.1 (API cần key này kể cả game đang RoomCode). Script dưới đây thêm secret điều khiển phòng vào `.env` nếu thiếu/trống, giữ nguyên secret và cấu hình đang có. Secret này khác JWT key và mã join; không gửi cho tester.

```powershell
Set-Location 'D:\Unity\Jungle_Maze_Survival\Backend'
powershell -ExecutionPolicy Bypass -File Tools/Enable-RoomControl.ps1
docker compose --profile account up -d --build
docker compose ps
docker compose logs --tail 30 game-server api
```

Mở `http://localhost:5000/swagger`. Nếu đổi `API_PORT`, dùng cổng tương ứng. Trong Swagger:

1. `POST /api/auth/register`: tạo account nếu chưa có.
2. `POST /api/auth/login`: lấy `accessToken` → **Authorize** → dán token, không thêm chữ Bearer.
3. `POST /api/rooms` → Try it out, nhập ví dụ:

```json
{ "name": "Team A", "maxPlayers": 10 }
```

Response **201** có `id`, `name`, `maxPlayers`, `roomCode`, `activePlayers`, `pendingPlayers`. `roomCode` phòng tạo thêm có đúng 6 ký tự chữ hoa/số, ví dụ `K7M2Q9`; nhập đúng chữ hoa, không có I/O/0/1. Chia sẻ mã cho người cần vào phòng; không chia sẻ JWT/control secret. Chủ phòng lấy từ JWT `sub`, không nhập user ID. Bỏ `maxPlayers` thì dùng 10; yêu cầu 11 trở lên trả 400.

4. `GET /api/rooms`: xem phòng mặc định và phòng của mình. Mã phòng mặc định không xuất hiện; lấy từ người vận hành. Mã phòng tạo thêm chỉ trả về cho chủ phòng; không có danh sách phòng của người khác.
5. `DELETE /api/rooms/{id}`: nhập ID phòng của mình. Chỉ xóa được khi không còn active/pending; thành công **204**. Không xóa được phòng mặc định.

API HTTP dùng JWT để quản lý phòng; UDP vẫn dùng chế độ auth chung của server. Khi `AUTH_MODE=RoomCode`, người chơi chỉ cần mã join, không cần gửi JWT qua UDP. Nếu cố ý test `AUTH_MODE=Jwt` local/VPN, mỗi phiên cần account riêng; quota tạo phòng không giới hạn số phiên RoomCode của chủ phòng.

| Kết quả/lỗi | Ý nghĩa và cách xử lý |
| --- | --- |
| 400 | Tên sau trim 1–48 ký tự; capacity 1–10 và không vượt tổng server |
| 401 | Thiếu/JWT sai hoặc hết hạn; login và Authorize lại |
| 403 NotRoomOwner | Bạn không phải chủ phòng |
| 404 RoomNotFound | Phòng không tồn tại, đã xóa/thu hồi hoặc server vừa restart |
| 409 UserRoomLimitReached | Mỗi user tối đa 2 phòng bổ sung; xóa phòng trống trước |
| 409 RoomLimitReached | Tổng server tối đa 5 phòng tính cả phòng mặc định |
| 409 RoomNotEmpty / DefaultRoomProtected | Ngắt client, chờ slot được trả; phòng mặc định không xóa |
| 429 | Tối đa 5 POST/phút/user, kể cả yêu cầu validation/quota lỗi; đợi cửa sổ mới |
| 503 | Chưa có ROOM_CONTROL_TOKEN, secret hai bên không khớp, server/control chưa sẵn sàng hoặc queue quá tải |

Cập nhật `.env` cũ theo phương án: `MAX_ROOM_PLAYERS=10`, `MAX_TOTAL_PLAYERS=50`, `MAX_ROOMS=5`, `MAX_ROOMS_PER_USER=2`. Có thể giảm các giá trị cho buổi test nhỏ; code từ chối cấu hình vượt các trần này. Mã phòng mặc định trong `ROOM_CODE` được giữ nguyên, không bắt buộc 6 ký tự. Không có control secret thì UDP phòng mặc định vẫn hoạt động; API phòng trả 503. Sau khi đổi cấu hình, recreate API/game-server; phòng tạm cần tạo lại. TCP 7780 chỉ ở Docker network, không publish và không mở Security Group.

### 14.2. Join phòng tạo thêm bằng NetSmokeClient

Mở terminal khác tại Backend. Dùng cùng JSON endpoint với server; khi chương trình hỏi mã phòng, nhập **roomCode của phòng vừa tạo**, không nhập `ROOM_CODE` mặc định:

```powershell
dotnet run --project NetSmokeClient/NetSmokeClient.csproj -- --config NetSmokeClient/client.local.json --name TeamA --clients 10 --duration 30
```

Thử thêm một phiên khi phòng đang đủ 10: nhận `RoomFull`. Phòng khác còn chỗ vẫn join được, trừ khi tổng active+pending mọi phòng chạm `MAX_TOTAL_PLAYERS` thì nhận `ServerFull`. Khi cả phòng và server đều đầy ưu tiên `RoomFull`. Join lại sau khi client dừng là hành vi đúng vì slot được trả.

Phòng tạo thêm chỉ ở RAM, không có collection rooms trong Compass. Restart/recreate GameServer mất các phòng này; phòng mặc định được tạo lại từ `.env`. Phòng tạo thêm trống liên tục 600 giây tự thu hồi; có người hoặc pending thì không bị thu hồi, rời hết sẽ bắt đầu đếm lại. API/Mongo restart riêng không làm mất phòng nếu GameServer vẫn chạy.

### 14.3. Cấu hình sức chứa

| Biến | Mặc định | Tác dụng |
| --- | ---: | --- |
| MAX_PLAYERS | 10 | Phòng mặc định, cho phép 1–10; giữ tương thích bản trước |
| MAX_ROOM_PLAYERS | 10 | Sức chứa tối đa được yêu cầu cho mỗi phòng tạo thêm, 1–10 |
| MAX_TOTAL_PLAYERS | 50 | Tổng phiên active+pending mọi phòng, 1–50 |
| MAX_ROOMS | 5 | Số phòng tính cả mặc định, 1–5 |
| MAX_ROOMS_PER_USER | 2 | Số phòng tạo thêm của một account, 1–2 |
| ROOM_EMPTY_TTL_SECONDS | 600 | Thời gian phòng tạo thêm trống trước thu hồi |
| ROOM_CONTROL_TOKEN | trống | Secret random ít nhất 32 UTF-8 byte; trống tắt HTTP control |
| ROOM_CONTROL_BIND_ADDRESS / PORT | Compose: 0.0.0.0 / 7780 | Bind nội bộ container; chạy .NET trực tiếp mặc định 127.0.0.1 |
| RoomControl__BaseUrl | Compose: http://game-server:7780 | Địa chỉ API gọi GameServer, không phải địa chỉ client |

Tổng server được kiểm theo **phiên đồng thời thực tế**, gồm pending. Năm phòng 10 slot cho phép tối đa 50 phiên khi dùng các giá trị mặc định; có thể giảm giới hạn cho buổi test nhỏ. Một user chỉ tạo được hai phòng bổ sung, cần ít nhất hai user để tạo đủ bốn phòng ngoài mặc định. Các trần 10/5/50 là chính sách vận hành, chưa chứng minh gameplay năm phòng đầy sẽ đạt hiệu năng yêu cầu.

### 14.4. Chạy bài đo local riêng

Chỉ giữ [báo cáo mới nhất](Reports/Local_Capacity_Retest_2026-10-14/report.vi.md), cùng JSON/CSV và bằng chứng thô nén. Báo cáo gồm kết quả một/nhiều phòng, phương án 5/50 đã áp dụng và kiểm tra triển khai. Không diễn giải các lượt này thành sức chứa gameplay/EC2.

Docker Desktop Linux engine, SDK .NET 8, Python 3.11+ phải hoạt động. Core chức năng trước, sau đó benchmark:

```powershell
dotnet build JungleBackend.sln
dotnet run --project Tests/NetworkChecks/NetworkChecks.csproj
python -X utf8 Tests/auth_smoke.py
python -X utf8 Tools/room_benchmark.py
```

Nếu Python không ở PATH, dùng đường dẫn Python của máy như mục 9. Harness tự build image và tạo Compose project `room-bench-<timestamp>-<random>`: Mongo/DB mới, API mới, GameServer mới; cổng loopback ngẫu nhiên. Không đọc `.env` dev, không dùng DB/phòng dev, không bật hoặc gọi AWS. GameServer giới hạn **256MiB**, không swap cho benchmark. Mongo 768MiB/API 384MiB nằm riêng; đây là Docker Desktop trên máy dev, không mô phỏng CPU/đường truyền EC2.

Harness hiện kiểm API/quota/quyền sở hữu, mã 6 ký tự, ngưỡng 10/11 và chức năng **5 phòng × 10 phiên** đồng thời trong lượt ngắn 6 giây; phòng thứ sáu bị từ chối. Bài hiệu năng một phòng chạy **5 → 10** phiên; mỗi mức warm-up **15 giây**, đo **60 giây** ở **30Hz**, xác nhận ứng viên **180 giây**. Client chờ tối đa 5.2 giây cho Pong cuối. Nếu máy phát tải gửi dưới 28.5Hz/client (95% của 30Hz), đánh dấu **INVALID_GENERATOR** và dừng. Muốn dò vượt policy dùng công cụ ở mục 14.5.

Điều kiện đạt: tất cả join, không disconnect ngoài dự kiến, timeout <1%, RTT p95 local ≤50ms, tick trung bình 29–31Hz, dropped tick=0, CPU cao nhất theo cửa sổ đo ≤85% một core, RAM cgroup lấy mẫu ≤80% giới hạn, đủ telemetry. CPU process không chia cho số core. RAM cgroup (hỗ trợ v1/v2) có thể lớn hơn WorkingSet process. Ngưỡng RTT 50ms là ngưỡng **local**; khi đo Sydney cần chốt tiêu chí RTT riêng trước.

Kết quả tại `Backend/out/room-bench/<run>/report.md`, kèm `report.json`, `report.csv`, log và JSON từng client. Có thông tin môi trường, commit, fingerprint source, image ID/giới hạn container, tiêu chí đạt/trượt. Harness đóng client, xóa phòng do nó tạo và `down --volumes` **đúng stack riêng**, kể cả khi lỗi/Ctrl+C. Tắt cưỡng bức process/PC có thể ngăn cleanup: tìm project có đúng tên run rồi dọn bằng Compose của run, không chạy lệnh xóa mọi container/volume.

`--functional-only` chạy kiểm thử phòng, ngưỡng 10/11 và 5 × 10 phiên ngắn; chưa là xác nhận hiệu năng nhiều phòng. `--levels` chỉ nhận mức tăng dần 1–10; `--seconds`, `--warmup`, `--confirm-seconds` dành cho bài tự chọn, báo cáo phải ghi đúng tham số. Tick hiện chưa có gameplay; phải đo lại khi thêm movement/combat/snapshot và khi xác nhận nhiều phòng trên môi trường đích.

### 14.5. Thử vượt chính sách trên bản GameServer local riêng

`Tools/local_capacity_probe.py` dùng để tìm vùng tải đạt tiêu chí, không sửa giới hạn vận hành 10/5/50. Công cụ sao chép riêng GameServer/Shared vào `out/capacity-probe`, chỉ nới bốn hằng `RoomLimits` trong bản sao lên 256 người/phòng, tổng 256 phiên, 32 phòng và 31 phòng/user. API/Swagger của dự án giữ nguyên giới hạn 10/5/50.

Chạy từ **Backend** khi Docker Desktop Linux engine đã mở:

```powershell
python -X utf8 Tools/local_capacity_probe.py
```

Một tiến trình NetSmokeClient phát tải đồng thời vào các phòng. Hai nhóm đo lần lượt là một phòng tăng số phiên và nhiều phòng mỗi phòng 10 phiên. Trần công cụ là 256 phiên một phòng hoặc 25 phòng × 10; đây là phạm vi có thể thử, không phải kết quả đã xác nhận. Mỗi mức warm-up 15s/đo 60s, mức trượt được lặp lại, thu hẹp vùng đạt/trượt rồi xác nhận ứng viên 180s. Dừng tăng ở mức trượt lặp hoặc khi máy phát tải không đủ tốc độ; các mức sau đó không được coi là đã đo. Ngưỡng mỗi phòng là RTT p95 ≤50ms, cùng các tiêu chí CPU/RAM/tick/loss ở mục 14.4.

Công cụ dùng Docker context `desktop-linux`, chỉ chấp nhận socket local; tạo container RAM 256MiB không swap, không hạn CPU, cổng loopback ngẫu nhiên. Nó không đọc `.env` của team, không chạy API/MongoDB, không gọi AWS và không đo luồng đăng nhập. TCP control chỉ mở trên loopback của stack thử nghiệm. Phòng được tạo qua control endpoint nội bộ bằng secret tạm.

Kết quả ở `Backend/out/capacity-probe/<run>/report.md`, `.json`, `.csv`, kèm log và JSON từng lượt. JSON có số liệu từng phòng/client, tốc độ gửi thật, telemetry và hash source snapshot. Cuối lượt, công cụ đóng client, xóa phòng, dỡ container/network và image của chính lượt đó, xóa file mã phòng/secret tạm, kiểm lại policy và ID/image của ba container dev. Không dùng lệnh prune hoặc xóa volume của team. Nếu cưỡng bức tắt PC, cần tự kiểm tra cleanup đúng project của lượt.

Chạy riêng một nhóm hoặc thay mức thử, ví dụ:

```powershell
python -X utf8 Tools/local_capacity_probe.py --skip-single --room-levels 3,5,10
python -X utf8 Tools/local_capacity_probe.py --confirm-only --single-levels 50 --room-levels 5
dotnet build JungleBackend.sln
dotnet run --project Tests/NetworkChecks/NetworkChecks.csproj
```

`--confirm-only` chỉ chạy lượt dài 180s ở một mức chỉ định cho mỗi nhóm, không dò thêm; dùng để xác nhận phương án thấp hơn khi ứng viên cao trượt ở lượt dài. Ví dụ trên đo một phòng 50 phiên và năm phòng × 10. Có thể thêm `--skip-single` hoặc `--skip-multi` để chỉ đo một nhóm.

NetSmokeClient có thêm `--room-plan <private.json>` cho công cụ này: mảng `{roomId, roomCode, clients}`, tổng 1–256 client, tối đa 32 phòng khác ID. Không dùng kèm `--clients` hoặc Jwt. Không commit/chia sẻ file chứa mã; report chỉ giữ ID phòng. Với dev test bình thường, tiếp tục dùng cách nhập mã ở mục 14.2.

Báo cáo mới nhất: [Local 14/10 và AWS 16/10/2026](Reports/Local_Capacity_Retest_2026-10-14/report.vi.md). AWS đã xác nhận 5 phòng ×10 phiên trong 180s với generator cùng EC2 qua SSM, không loss/drop; UDP từ PC chưa thông. Local cũ dùng source snapshot riêng, không thay thế phép đo AWS. Phương án 10 người/phòng, 5 phòng/tổng 50, 2 phòng bổ sung/user đã áp dụng local và EC2. Kết quả không xác nhận sức chứa gameplay; không tự tăng giới hạn sau bài đo.

### 14.6. Chuẩn bị AWS, chỉ thực hiện sau khi chốt lượt xác nhận

Đợt này không tự tạo release mới, bật EC2, deploy hoặc chạy tải AWS. Sau báo cáo local, người quản lý chọn số phiên, thời gian và tiêu chí RTT Internet. Giữ phòng mặc định và buổi test dev riêng; phòng benchmark là phòng khác nhưng vẫn dùng chung tài nguyên EC2, nên cần lịch trống.

Khi đã được chọn triển khai, builder mục 6.2 sẽ đóng code/Compose/scripts mới. `configure.sh` mới tạo control secret cho lần cài mới; `deploy.sh` mới gọi `enable-room-control.sh` để **chỉ thêm key còn thiếu**, giữ nguyên account/DNS/JWT/ROOM_CODE và volume hiện có. Không cần mở cổng 7780. Caddy giữ HTTPS và bảo vệ Swagger như cũ.

Công cụ chuẩn bị chạy cùng tải trên server có sẵn:

```powershell
# Chỉ chạy SAU khi có lịch và mức tải AWS được thống nhất.
# Cần Docker context quản trị từ xa đã cấu hình, client.aws.json đúng endpoint,
# BENCH_ACCESS_TOKEN là JWT account riêng cho benchmark (không đặt trong argv/file commit).
python -X utf8 Tools/room_benchmark.py --remote --docker-context junglemaze-admin --api-url https://junglemaze.duckdns.org --client-config NetSmokeClient/client.aws.json --server-container junglemaze-aws-game-server-1 --levels 10 --seconds 60 --confirm-seconds 180
```

`junglemaze-admin` là tên context cần admin tự cấu hình trước, không được tạo tự động bởi harness. Nhánh remote tạo/xóa **phòng của lượt đo**, đọc cgroup/log qua Docker context; không build/up/down/restart remote. Nó cần quyền admin Docker để đọc telemetry, không dành cho tester thông thường. Harness vẫn áp dụng RTT 50ms và các ngưỡng chuẩn để so sánh; báo cáo remote có thể trượt RTT do đường truyền, không suy ra lỗi xử lý server. Không chạy lệnh ví dụ trước khi chốt tải, chi phí và cách truy cập quản trị.

Kết thúc: kiểm tra client đã đóng, phòng benchmark được xóa; admin **Stop instance** trong Console nếu hết buổi test. Đóng Swagger/SSH/client hoặc dừng container không dừng EC2, EBS vẫn có chi phí khi instance Stopped. Không nâng instance hoặc đổi gói AWS tự động để làm bài đo đạt.

### 14.7. Buổi test phòng AWS có tự dừng (16/10/2026)

Đây là công cụ dành cho admin; yêu cầu phiên CLI junglemaze-admin còn hạn và alias SSH junglemaze đã dùng SSM. Chỉ chạy khi máy dành riêng cho bài đo và đang Stopped. Script từ chối chạy trên instance đang phục vụ buổi khác. Không đổi instance hoặc bật T3 Unlimited; shutdown của EC2 phải là Stop. Đọc kết quả mới nhất ở [báo cáo chung](Reports/Local_Capacity_Retest_2026-10-14/report.vi.md).

1. Trên PC, bật Docker Desktop, build solution và chạy Tests/run_all.cmd từ Backend. Test JWT hết hạn thật có thể chạy thêm bằng Python: Tests/auth_smoke.py --short-lifetime; đợi khoảng 95 giây để vượt expiry và clock skew.
2. Tạo gói cài đặt nhẹ bằng PowerShell tại thư mục gốc repo:

    powershell -NoProfile -ExecutionPolicy Bypass -File Backend/Deploy/build-aws-release.ps1 -AppsOnly -OutputDirectory Backend/out/aws-room-test

   Gói này chỉ có image API/GameServer. Mongo 7 và Caddy phải tồn tại trên EC2; giữ volume và các secret cũ. Migration chỉ thêm control secret nếu thiếu và đặt giới hạn phòng 10/50/5/2. Không thay dữ liệu Mongo.
3. Chạy kiểm tra cấu hình trước, thêm -PreflightOnly vào lệnh sau. Khi đạt, chạy lại bỏ cờ đó để bắt đầu buổi đo:

    powershell -NoProfile -ExecutionPolicy Bypass -File Backend/Deploy/test-aws-rooms.ps1 -Python python -Release D:/Unity/Jungle_Maze_Survival/Backend/out/aws-room-test/release.tar.gz -OutputDirectory D:/Unity/Jungle_Maze_Survival/Backend/out/aws-room-test/session-NEW

   Thay đường dẫn repo/Python nếu máy khác; mỗi lượt dùng output mới. Thông tin đăng nhập ba fixture account được lưu riêng tại LocalAppData/JungleMaze/aws-room-fixtures.json với ACL chỉ user hiện tại. Không gửi file này, token hoặc mã phòng lên Git/báo cáo.
4. Script kiểm tra Stopped, shutdown=stop, CPU Standard và quyền Start/Stop; bật máy, đợi SSM tối đa 3 phút, đặt timer tắt EC2 độc lập rồi upload qua SSH/SSM. Kết nối HTTPS dùng IP hiện tại nhưng vẫn kiểm chứng hostname/certificate, không sửa hosts hoặc bỏ TLS.
5. Kiểm tra tạo phòng bắt buộc JWT: thiếu/sai/sửa chữ ký/hết hạn phải 401; chủ lấy từ sub, quyền xóa, quota, giới hạn 5 phòng, rate limit. API tạm cấp token 1 phút, khôi phục 60 phút ngay sau login; GameServer giữ nguyên RoomCode. Override nằm trong /run và có timer khôi phục riêng. Không gửi JWT qua UDP public.
6. Đo baseline 1 client (Internet nếu generator trên PC; loopback EC2 nếu -GeneratorOnServer) (warm-up 5 giây, đo 30), rồi 10/30/50 client (warm-up 10, đo 45/45/60 giây), xác nhận mức đạt cao nhất 180 giây. Mỗi phòng tối đa 10; phòng mặc định vẫn tồn tại. Lượt cố ý join client thứ 11 được ghi riêng. Trượt thì dừng tăng tải.

Ngưỡng: p95 **từng phòng** ≤ baseline + 50ms; tick 29–31Hz; không dropped tick/OOM/restart/disconnect; timeout <1%; CPU ≤85% một core; RAM cgroup ≤80% của 256MiB. Mỗi client phải phát ≥28.5Hz; dưới ngưỡng là generator không hợp lệ. RTT Internet không dùng ngưỡng tuyệt đối 50ms của local.

Không bắt đầu lượt không thể xong trước phút 28. Cuối buổi hoặc lỗi: đóng client, xóa phòng của bài đo, khôi phục JWT, rồi StopInstances và xác nhận Stopped. Có watchdog PC và timer EC2 mốc 30 phút; nếu công cụ báo STOP NOT CONFIRMED phải kiểm tra Console ngay. Đóng terminal không phải cách dừng EC2.

Ngân sách là thời gian Running, không phải cam kết 0đ: compute và public IPv4 trong lúc bật, EBS vẫn giữ phí khi tắt. Không tạo thêm EC2/load balancer/CloudWatch agent. Thống kê JSON/CSV, RTT và telemetry được giữ trong output; báo cáo không chứa credentials. Kết quả ping/pong không chứng minh sức chứa movement/combat hay 50 máy vật lý.

**Khi mạng PC chặn UDP:** thêm -GeneratorOnServer vào script PowerShell. Trước khi bật EC2, chạy tại thư mục gốc:

    dotnet publish Backend/NetSmokeClient/NetSmokeClient.csproj -c Release -r linux-x64 --self-contained false -o Backend/out/aws-room-test/smoke-client
    tar -czf Backend/out/aws-room-test/smoke-client.tar.gz -C Backend/out/aws-room-test/smoke-client .

Chế độ này mở tunnel SSH/SSM TCP từ loopback PC tới Caddy 443 để gọi API, vẫn xác minh certificate junglemaze.duckdns.org. Client chạy container tạm ngay trên EC2, UDP qua loopback EC2, RAM 256MiB và CPU tối đa 0.75 core; không mở thêm port hoặc dịch vụ cloud. Plan chứa mã phòng đi qua SSH stdin, nằm trong thư mục /run riêng rồi được xóa. API vẫn bắt JWT như bình thường.

Kết quả chỉ đánh giá backend trên EC2 với generator dùng chung máy; CPU/credit của generator có ảnh hưởng. **Không dùng số RTT này để kết luận Internet từ lớp đã thông hoặc game chơi từ PC được.** SSH/Session Manager không chuyển tiếp UDP game; cần mạng cho phép UDP hoặc giải pháp mạng riêng nếu muốn client Unity từ xa.

Nếu đúng gói release đã cài, thêm -ReuseDeployedRelease để bỏ upload/deploy lại. Script đối chiếu SHA-256 manifest trên EC2 trước khi cho chạy; không dùng cờ này để bỏ qua phiên bản khác. Upload bình thường dùng SFTP tiếp tục file có tên theo SHA-256, giới hạn 8 phút; nội dung được kiểm hash trước giải nén. Nếu lỗi, Stop vẫn được thực hiện.

Nếu một lượt đã đạt toàn bộ kiểm tra JWT/chức năng và tải 10 nhưng công cụ bị gián đoạn sau đó, có thể dùng -CapacityAfter kèm đường dẫn report.json của lượt ấy, cùng -GeneratorOnServer. Script kiểm chứng các kiểm tra bảo mật bắt buộc, PASS 10 phiên và image GameServer trùng khớp; ghi hash bằng chứng cũ. Nó đăng nhập/tạo phòng lại bằng JWT hợp lệ, đo baseline mới rồi chạy 30/50 và lượt xác nhận. Đây là hai lượt riêng, phải giữ cả dữ liệu cũ; cờ này không bỏ cơ chế JWT của API hoặc thay thế một bài bảo mật bị FAIL.

## 15. Nghiệm thu Phase 1: Local và PC → EC2

Mục này giữ quy trình chẩn đoán UDP/LAN/VPN. Luồng triển khai hiện tại và nghiệm thu UDP/WSS Auto ở **mục 16**; tester không bắt buộc cài VPN. Trạng thái các lượt cũ trong mục này không thay kết quả WSS mới ở báo cáo chung.

### 15.1. Bốn task và cách hiểu thống nhất

| Task | Phạm vi cần thực hiện và kiểm tra |
| --- | --- |
| INF-01 — Dev Local & Docker | GameServer/API/Mongo dùng Compose riêng Local/AWS. Container bind 0.0.0.0, host publish theo môi trường. Test boot, restart, DB còn dữ liệu và hai máy LAN/VPN; nhóm 10 máy thật thuộc INF-02. |
| NET-01 — REST API & JWT | Account API ASP.NET Core 8 cấp JWT ký với account ID ở sub. Tạo/list/xóa phòng cần JWT. Local dùng loopback; remote qua HTTPS hoặc VPN mã hóa đã xác minh. |
| SRV-01 — GameServer & tick | LiteNetLib UDP 7777, fixed tick 30Hz/PollEvents, không chờ API/DB đồng bộ trong tick. Giới hạn 10/phòng, tổng 50/5 phòng; kiểm 10 phiên/300s và metrics. |
| SRV-02 — Handshake | Luồng chính Local/Online/VPN dùng RoomCode, server cấp PlayerId phiên; kiểm version/payload/capacity, timeout và trả slot. JWT UDP là nhánh tùy chọn Local/VPN, không tự bật khi NET-01 có API account. |

Account ID trong JWT **khác** PlayerId ngẫu nhiên của phiên RoomCode. Có login để tạo phòng không có nghĩa mọi người join phòng phải login. Không đưa JWT vào UDP public; giữ guard PUBLIC_HOSTING và wire protocol v1.

### 15.2. Chọn endpoint bằng biến môi trường

NetSmokeClient lấy `NET_MODE`, `NET_GAME_SERVER_HOST`, `NET_GAME_SERVER_PORT`, `NET_API_BASE_URL` **ưu tiên hơn JSON** rồi validate. Giá trị sai bị từ chối; không tự fallback sang môi trường khác. Biến trong `.env` của Compose không tự truyền sang terminal client.

**PC / PowerShell tại Backend**, ví dụ chọn VPN sau khi đã có IP EC2 thật:

```powershell
$env:NET_GAME_SERVER_HOST = Read-Host 'IPv4 Tailscale cua EC2'
dotnet run --project NetSmokeClient/NetSmokeClient.csproj -- --config NetSmokeClient/client.aws-vpn.json --name Tester01 --duration 30
Remove-Item Env:NET_GAME_SERVER_HOST
```

`client.aws-vpn.json` chứa **IP mẫu**, phải override. Local dùng `client.local.json`; LAN dùng `client.lan.json`; AWS trực tiếp dùng `client.aws.json`. Có thể dùng Public IPv4 hiện tại thay hostname để tách lỗi DNS, nhưng API vẫn phải xác minh HTTPS hostname/certificate. Báo cáo client ghi endpoint thực, host phát tải, PlayerId, cổng socket, số transport packet gửi/nhận và RTT p50/p95/p99; không ghi mã phòng/JWT.

Muốn đổi môi trường, xóa override cũ trước:

```powershell
Remove-Item Env:NET_MODE,Env:NET_GAME_SERVER_HOST,Env:NET_GAME_SERVER_PORT,Env:NET_API_BASE_URL -ErrorAction SilentlyContinue
```

### 15.3. Kiểm Local trước, không tiêu credits EC2

Đứng tại **Backend**, Docker Desktop đang chạy, có Python 3 và .NET SDK 8:

```powershell
.\Tests\run_all.cmd
python -X utf8 Tools/room_benchmark.py --phase1-local --levels 10 --output out/phase1-local-MOI
```

Thay `MOI` bằng nhãn lượt mới; công cụ không ghi đè thư mục đã tồn tại. `--phase1-local` dùng stack/DB/secret/cổng ngẫu nhiên riêng; tạo account, down/up **giữ volume**, kiểm login còn hoạt động, chạy các kiểm tra phòng và một lượt 10 phiên/300s/30Hz. Cuối bài chỉ xóa stack/volume test do công cụ tạo, không dọn DB dev. Khi Docker tạo lại container, công cụ lấy lại ID mới trước khi đọc metrics.

### 15.4. Dùng hai PC thật qua LAN/VPN

Hiện chưa có PC thứ hai để nghiệm thu. Khi có, trên **PC host** đặt `GAME_PUBLISH_HOST` bằng IPv4 LAN/VPN của host trong `.env`; giữ `API_PUBLISH_HOST=127.0.0.1` và `GAME_BIND_ADDRESS=0.0.0.0`. Tạo lại game-server bằng `docker compose up -d --build game-server`. `PUBLISH_HOST` vẫn được hỗ trợ làm fallback cho cấu hình cũ.

Chỉ cho firewall nhận UDP đúng cổng và đúng máy/subnet thử nghiệm. Trên **PC thứ hai**, lấy NetSmokeClient và cấu hình LAN/VPN, đặt host IP, nhập RoomCode rồi chạy 30s. Không cần cấp AWS account/key SSH hoặc mật khẩu DB cho tester. Đổi IP/port qua config/env, không sửa code. API loopback không truy cập được từ PC kia là đúng; muốn account remote phải có HTTPS/VPN và cấu hình publish API có chủ đích.

Ghi tên hai máy, địa chỉ mạng, tuyến LAN/VPN, joined/pong, disconnect/rejoin. Hai process trên một PC không thay thế bước này; bài 10 session không thay cho 10 người/máy.

### 15.5. Test PC → EC2 có giới hạn thời gian

Admin chuẩn bị CLI còn đăng nhập, alias `ssh junglemaze` qua SSM hoạt động và Release NetSmokeClient đã build. Với EC2 **Stopped**, chạy tại Backend:

```powershell
dotnet build JungleBackend.sln -c Release
.\Deploy\test-aws-rooms.ps1 -Phase1Connectivity -PreflightOnly -OutputDirectory out/phase1-preflight-MOI
.\Deploy\test-aws-rooms.ps1 -Phase1Connectivity -OutputDirectory out/phase1-aws-MOI
```

Truyền `-Python 'duong-dan-python.exe'` nếu Python không nằm trong PATH. Chế độ này **không deploy, không phát UDP trong EC2 và không đo 50–100 phiên**. HTTPS quản trị đi qua tunnel SSM, vẫn kiểm certificate gốc. Công cụ kiểm JWT API, tạo phòng test, chạy client thật trên PC và quan sát **header** UDP tại EC2 trong cùng cửa sổ; không lưu payload/mã phòng.

Nếu EC2 đang Running, mặc định từ chối. Chỉ sau khi người vận hành xác nhận không ai dùng và cho phép Stop cuối buổi, thêm `-UseRunningInstance`. Báo cáo tách lượt tiếp quản máy Running khỏi lượt tự Start; thời gian Running trước đó vẫn có chi phí.

Khi thông đường: kiểm phòng mặc định 10/11, phòng tạo thêm, mã sai, trả slot; đo 10 phiên/300s/30Hz. Muốn đồng thời Local và AWS, thêm `-LocalConfig NetSmokeClient/client.local.json` và đặt `PHASE1_LOCAL_ROOM_CODE` trong process bằng mã Local riêng, không đưa lên argv/report. Nếu thiếu, mục đồng thời để NOT_TESTED.

Sau PASS, công cụ Stop–Start cùng EC2, lấy IP mới và kiểm join lại; timer được đặt lại sau reboot. Có mục tiêu 20 phút và watchdog tuyệt đối 30 phút. Nếu kết nối BLOCKED, bỏ bài tải và Stop–Start để tiết kiệm; không coi đây là PASS. Cuối mọi lượt luôn dọn client/phòng test, Stop EC2 và xác nhận Stopped. Đọc `lifecycle.json`, `test/report.json`, logs/header; lỗi hoặc mục chưa chạy được giữ nguyên.

### 15.6. VPN Tailscale dự phòng

Chuẩn bị trên PC trong lúc EC2 tắt:

```powershell
.\Deploy\setup-phase1-vpn.ps1 -Target PC
```

Người dùng tự xác nhận UAC và đăng nhập trình duyệt; dùng cùng tailnet cá nhân của mình. Khi PC đã đăng nhập, bật EC2 cho lượt setup có lịch, rồi trong terminal PC thứ hai chạy:

```powershell
.\Deploy\setup-phase1-vpn.ps1 -Target EC2
```

Script dùng SSH/SSM cài host Tailscale từ nguồn chính thức, không Start/resize EC2, không bật exit node/subnet route, không nhận auth key trên argv. Mở URL đăng nhập EC2 bằng cùng tài khoản/tailnet, chạy lại để lấy IPv4. Tự Stop EC2 nếu chỉ setup thủ công; script setup không sở hữu vòng đời server. Không chạy setup song song với bài đo khi chưa hoàn tất đăng nhập.

Trong Tailscale admin → Access controls, cấp đúng PC tới EC2, ví dụ dưới là các **IP mẫu** phải thay:

```json
{"grants":[{"src":["100.80.1.10"],"dst":["100.80.1.20"],"ip":["udp:7777","tcp:443"]}]}
```

Giữ các rule quản trị cần thiết; rule allow-all hiện hữu vẫn cấp quyền rộng và phải được thu hẹp có chủ đích để đạt giới hạn trên. Kiểm tra policy cả allow UDP 7777/HTTPS 443 và deny Mongo 27017/control 7780. Không ghi đè policy tailnet đang có bằng nguyên ví dụ. [Cú pháp grants](https://tailscale.com/docs/reference/syntax/grants).

Sau enrollment, thêm `-VpnIp 'IPv4-EC2-Tailscale'` cho `-Phase1Connectivity`. Công cụ vẫn thử Public IPv4 trước; chỉ thử VPN nếu PC Running và IP thuộc peer đang Online. Ghi `tailscale ping` để phân biệt direct/relay và đo lại RTT; kết nối overlay thành công chưa chứng minh đường UDP public đã thông. [Relay/firewall](https://tailscale.com/docs/reference/faq/firewall-ports).

Ngày 16/10 mạng hiện tại trả `94.140.14.33` cho cả pkgs/dl/login/controlplane.tailscale.com và HTTPS handshake thất bại; winget/Python tải đều không được. Người dùng chưa thể chuyển hotspot. Vì vậy không cài/enroll EC2 vô ích; VPN hiện **BLOCKED**, không hứa relay vượt mọi mạng. Dùng mạng được phép hoặc nhờ quản trị cho phép các endpoint chính thức, không tắt kiểm tra TLS hay sửa DNS để vượt chính sách.

### 15.7. Cách ghi kết quả

PASS/FAIL cho tiêu chí đã chạy; BLOCKED cho đường mạng không tới được; NOT_TESTED cho hai PC, kiểm thử đồng thời hoặc Stop–Start chưa thực hiện. Đủ join, không disconnect ngoài dự kiến, timeout <1%, từng client phát ≥28.5Hz, tick trung bình 29–31Hz, không dropped tick; ghi thêm CPU/RAM/RTT. Không áp RTT Local ≤50ms cho Sydney/VPN; không kết luận Unity mượt từ ping/pong. Kết quả mới nhất bổ sung vào [báo cáo chung](Reports/Local_Capacity_Retest_2026-10-14/report.vi.md), không sửa nhãn bài co-located cũ thành PC→EC2.

## 16. Kết nối UDP/WSS và mở server cho buổi chơi

### 16.1. Phạm vi hiện tại

Backend + NetSmokeClient hỗ trợ UDP chính và WSS dự phòng, dùng chung room/slot. Unity JSurviv chưa chuyển sang backend này, vì vậy hoàn thành bài này chưa phải có trận multiplayer. Giữ 10 người/phòng, 5 phòng gồm mặc định, 50 phiên tổng, 2 phòng thêm/user. Đăng nhập/JWT hợp lệ mới tạo/list/xóa phòng; join dùng RoomCode.

### 16.2. Chạy Local

Tại **Backend**, tạo .env theo mục 3 rồi bật Docker Desktop:

```powershell
docker compose --profile account up -d --build
```

Giữ WS_ENABLED=true, WS_PUBLISH_HOST=127.0.0.1, WS_PORT=7781. Client:

```powershell
dotnet run --project NetSmokeClient/NetSmokeClient.csproj -- --config NetSmokeClient/client.local-auto.json --name Dev01 --duration 30
```

Mặc định Auto chọn UDP nếu vào được. Ép kiểm WebSocket trong đúng terminal:

```powershell
$env:NET_TRANSPORT='Wss'
dotnet run --project NetSmokeClient/NetSmokeClient.csproj -- --config NetSmokeClient/client.local-auto.json --name Dev01 --duration 30
Remove-Item Env:NET_TRANSPORT
```

NET_WS_URL có thể override endpoint. NET_WS_CONNECT_IP là tùy chọn pin IPv4 TCP cho đúng lượt test, vẫn dùng hostname/certificate của WebSocketUrl; không tự đổi tuyến, không bỏ TLS và không sửa DNS máy. Chỉ ws loopback hoặc wss remote có certificate đúng; ws pin cũng chỉ được trỏ loopback. Profile LAN v2 vẫn UDP. Config UDP cũ v1 tiếp tục chạy; không chỉ đặt NET_TRANSPORT=Auto lên config v1 vì phải đổi sang profile v2.

`client.lan-auto.json` là mẫu cho PC thứ hai: thay IP 192.168.1.10 và hostname `junglemaze-lan.example.com` bằng địa chỉ của **cùng một server LAN**. Hostname ví dụ chưa có DNS/certificate thực. Cần reverse proxy TLS tin cậy trên LAN chuyển `/game` tới 7781 trước khi dùng Auto; không trỏ WSS dự phòng sang AWS khi UDP đang thử Local. Nếu chưa thiết lập TLS LAN, dùng client.lan-v2.json/Udp; bài hai PC vẫn NOT_TESTED.

### 16.3. Test tự động trước AWS

```powershell
.\Tests\run_all.cmd
python -X utf8 Tools/room_benchmark.py --dual-local --levels 10 --output out/dual-local-MOI
```

Dùng tên output chưa tồn tại. Bài đầu kiểm API/JWT/room/quota và UDP v1/v2/WebSocket/fallback. Bài thứ hai dùng stack/DB/cổng/secret riêng, kiểm down/up giữ account và đo riêng UDP v2 rồi WebSocket, mỗi lượt 10 phiên/300s/30Hz trong 256MiB. Dọn đúng stack test khi xong; không xóa DB dev.

### 16.4. Deploy và nghiệm thu AWS có tự dừng

Chuẩn bị khi EC2 đang Stopped:

```powershell
dotnet build JungleBackend.sln -c Release
.\Deploy\build-aws-release.ps1 -AppsOnly -ReleaseId aws-dual-MOI -OutputDirectory out/aws-dual-MOI
python -X utf8 Tests/verify_aws_release.py out/aws-dual-MOI/release.tar.gz --apps-only
.\Deploy\test-aws-rooms.ps1 -DualTransport -Release out/aws-dual-MOI/release.tar.gz -OutputDirectory out/aws-dual-session-MOI
```

Thay MOI bằng nhãn hợp lệ chữ thường/số/gạch ngang mới; thêm -Python 'duong-dan-python.exe' nếu cần. CLI junglemaze-admin phải còn đăng nhập, alias ssh junglemaze dùng SSM. Script kiểm stopped/exclusive, CPU Standard, shutdown=stop, arm watchdog PC/EC2 30 phút và mục tiêu test 20 phút. HTTPS public từ PC được thử trước upload. Có lỗi thì Stop và xác nhận Stopped.

Bài AWS tự động hiện **không yêu cầu UAC hoặc sửa hosts**: API và client WSS được pin tới Public IPv4 hiện tại, giữ URI/SNI/chứng chỉ đúng `junglemaze.duckdns.org`. Report ghi webSocketConnectIp để tách kết quả transport khỏi kiểm thử DNS hệ thống. WSS đi PC→EC2 public 443, không đi trong SSM. Người chơi có DNS bình thường dùng profile hostname mặc định, không cần pin. Nếu mạng chặn cả HTTPS/WSS tới IP thì vẫn BLOCKED.

Nếu trình duyệt Swagger trên PC bị dòng hosts ghim IP cũ, việc sửa riêng dòng này vẫn cần quyền Windows: `update-pc-hosts.ps1` kiểm TLS/health, backup tại LocalAppData/JungleMaze rồi cập nhật hostname. Công cụ test không tự yêu cầu quyền này nữa. Pin trong NetSmokeClient không sửa trình duyệt hoặc resolver toàn máy; báo trạng thái DNS riêng và xử lý qua quản trị mạng khi cần.

Deploy apps-only giữ image/volume Mongo/Caddy, tạo lại Caddy để mount config release mới có /game; không đổi secret/JWT/default room. Sau deploy: login/JWT security → phòng riêng → ép WSS → Auto → default 10/11 → đo WSS 10 phiên/300s, đồng thời một Local Docker riêng → Stop–Start và join WSS lại nếu còn ngân sách. Không đo lại 50–100 phiên hoặc phát client trong EC2 để thay kết quả WAN.

**SSM upload chậm:** chuẩn bị delta khi EC2 còn tắt, dùng hai base image có cùng ID đã xác minh trên cả Local và EC2 (có thể là bộ base gốc giữ lại, không nhất thiết release đang chạy):

```powershell
python Tools/aws_image_delta.py build --release out/aws-dual-MOI/release.tar.gz --base junglemaze-api:RELEASE-CU junglemaze-game:RELEASE-CU --output out/aws-dual-MOI/delta.tar.gz
python Tools/aws_image_delta.py verify --release out/aws-dual-MOI/release.tar.gz --archive out/aws-dual-MOI/delta.tar.gz
.\Deploy\test-aws-rooms.ps1 -DualTransport -Release out/aws-dual-MOI/release.tar.gz -DeltaRelease out/aws-dual-MOI/delta.tar.gz -OutputDirectory out/aws-dual-session-MOI
```

RELEASE-CU là tag base thật đã triển khai và còn cùng ID trên hai máy, không dùng nguyên placeholder. Bộ đã dùng trong lần này là aws-rooms-20261016; giữ các image base đó nếu muốn tái sử dụng delta. Sau đóng gói legacy, ID Local/EC2 có thể khác nhau dù layer/config giống; khi đó dùng bộ base gốc cùng ID hoặc gói đầy đủ, không bỏ kiểm tra pin. Delta ghim base image ID và SHA layer, không tải runtime lại; thiếu base thì fail và Stop. Việc đóng gói lại có thể đổi manifest/image ID dù filesystem/runtime config giống nhau; provenance ghi cả hai. Khi chạy lại đúng release đã cài bằng delta, thêm `-ReuseDeployedRelease -DeltaRelease ...`: công cụ kiểm provenance của bundle đầy đủ và image ID thực trên EC2 trước khi bỏ qua upload/deploy. Giữ gói đầy đủ và release trước để rollback.

`-BudgetMinutes` là ngân sách mềm 8–20 phút; `-HardStopMinutes` là watchdog/timer cứng 10–30 phút và phải lớn hơn ngân sách mềm. Ví dụ lượt không cần upload: `-BudgetMinutes 18 -HardStopMinutes 20`. Không giảm thời gian đo 300s để báo đạt. Script giữ PC khỏi tự idle Sleep và trả lại khi kết thúc; vẫn phải tránh Sleep thủ công, đóng máy hoặc đổi Wi-Fi trong lúc test. Nếu mất mạng khiến CLI Stop không tới AWS, timer EC2 độc lập vẫn tắt máy theo giờ cứng; xác nhận Stopped lại sau khi đường quản trị phục hồi.

Chỉ cần kiểm tra lại sau một Stop đã xác nhận mà không đo tải/deploy: dùng `test-aws-rooms.ps1 -DualTransport -WssReconnectOnly -OutputDirectory out/wss-rejoin-MOI`. Nhánh này Start có timer, xác nhận fixture account cũ còn trong Mongo, login/JWT/tạo phòng và một WSS client thật rồi finally Stop. Không thay thế bài đo 10 phiên/300s.

### 16.5. Bật theo lịch khi team muốn test/chơi

Admin, tại Backend:

```powershell
.\Deploy\open-aws-session.ps1 -Minutes 60
```

Thời lượng bắt buộc, 5–240 phút, tính từ lúc chuẩn bị Start nên bao gồm boot; không bật khi server đang có buổi khác. Công cụ đặt timer Stop trên EC2 và watchdog PC, cập nhật DuckDNS; thông báo giờ kết thúc UTC+7. Kiểm /health, DNS và một client Auto trước khi mời team. Nếu PC admin ghim hosts cũ, chạy helper update-pc-hosts bằng quyền Administrator với Public IPv4 vừa hiện. Tester không cần AWS account/SSH/VPN.

Người tạo phòng mở HTTPS Swagger, register/login → Authorize bằng accessToken → POST /api/rooms → chia sẻ RoomCode riêng. Tester dùng client.aws-auto.json:

```powershell
dotnet run --project NetSmokeClient/NetSmokeClient.csproj -- --config NetSmokeClient/client.aws-auto.json --name Tester01 --duration 30
```

Đã join thì giữ transport đã chọn. Bị ngắt giữa phiên thì chạy lại để tạo lượt kết nối mới; chưa có khôi phục nhân vật/trận. Hết buổi có thể Stop sớm trong Console; đợi Stopped. Đóng app/SSH không Stop EC2. EBS vẫn có usage lưu trữ, không hứa 0đ. Stop–Start giữ account nhưng mất phòng thêm/session trong RAM.

### 16.6. Đọc kết quả và giới hạn

Report client ghi transport, fallbackReason, endpoint, playerId, RTT/loss/rate; không ghi mã phòng, JWT hoặc attempt nonce. Có joined/Pong thật mới tính vào kết quả. Mã sai/phòng đầy không thử đường khác. UDP public BLOCKED nhưng Auto/WSS PASS là hai trạng thái khác nhau, phải ghi cả hai. Hai process không thay thế hai PC vật lý; bài ping/pong không chứng minh FPS/movement/combat.

Tài liệu này và Network Architecture là hướng dẫn chuẩn; số liệu thực, timeline Start/Stop, cleanup bị chặn và archive checksum nằm trong một báo cáo chung hiện có.

## 17. Phase 2: chạy trận, Guest, Safe Zone và lịch sử

Phần này áp dụng **SRV-03 / NET-02 / ZON-01** trên source Phase 2. AWS đang chạy bản trước không tự có tính năng mới; đợt này không deploy hoặc bật EC2. Xem [Network Architecture](Network_Architecture.vi.md) để biết contract và quyền truy cập.

### 17.1. Chuẩn bị Local lần đầu

1. Bật Docker Desktop, dùng Linux containers. Mở PowerShell **tại `Backend`**, không chạy compose từ `C:\Users\...`.
2. Giữ `.env` riêng của máy và Mongo volume hiện có. Tạo từ `.env.example` nếu chưa có, theo mục 3. Không chép secret AWS sang Local.
3. Kiểm tra `ROOM_CODE`, `MAX_PLAYERS=10`, `MAX_ROOM_PLAYERS=10`, `MAX_ROOMS=5`, `MAX_TOTAL_PLAYERS=50`, `MAX_ROOMS_PER_USER=2`. Main dùng `MATCH_PROFILE=main`; thử hai client dùng `MATCH_PROFILE=test` và `PUBLIC_HOSTING=false`.
4. Muốn tạo phòng/liên kết Guest cần `ROOM_CONTROL_TOKEN` tối thiểu32 byte và API trỏ đúng room-control nội bộ. Muốn lưu lịch sử cần **secret riêng** `MATCH_RESULT_TOKEN` tối thiểu32 byte, giống nhau giữa API/GameServer; không dùng JWT key hoặc control token thay thế. Có thể tạo mỗi secret bằng PowerShell: `$b = New-Object byte[] 32; $rng = [Security.Cryptography.RandomNumberGenerator]::Create(); $rng.GetBytes($b); $rng.Dispose(); [Convert]::ToBase64String($b)`, rồi điền vào `.env` bằng VS Code. Không commit file này.
5. Giữ `MATCH_OUTBOX_PATH` trong volume của game-server và `MATCH_RESULTS_API_URL` trỏ API nội bộ. Compose chịu trách nhiệm tên service/cổng; client không gọi tuyến ghi kết quả.
6. Khởi động cả hệ thống:

```powershell
Set-Location 'D:\Unity\Jungle_Maze_Survival\Backend'
docker compose --profile account up -d --build
docker compose ps
docker compose logs --tail 40 game-server api
```

Đợi Mongo healthy, `/health` của API trả thành công, GameServer có `[ready]`. Nếu báo lịch sử bị tắt/secret thiếu thì vẫn chơi RoomCode được, nhưng chưa nghiệm thu lưu lịch sử. Không dùng `docker compose down -v`: lệnh đó xóa dữ liệu.

### 17.2. Chạy demo Unity hai client

1. Mở project Unity **6000.3.15f1**, đợi hết compile. Trong menu **Jungle Maze → Phase 2 → Sync Runtime Data** đồng bộ map/luật/bo từ `Backend/Shared/GameData`. Dùng Verify Runtime Data để kiểm; không sửa bản sao StreamingAssets thay nguồn chuẩn.
2. Trong menu kết nối của scene Boot, chọn profile **phase2-local**, đặt host/port theo compose và nhập `ROOM_CODE` trong `.env`. Protocol v3 dùng resume; RoomCode không bắt đăng nhập. Nếu muốn lưu Guest, `apiBaseUrl` phải trỏ API loopback đang chạy. API từ xa phải HTTPS.
3. Build hai scene `Boot` và `Game` thành Windows player. Chạy một player + Unity Editor, hoặc hai player. Dùng hai tên dễ phân biệt. Cả hai dùng chung endpoint và mã phòng.
4. Với `MATCH_PROFILE=test`, hai người đủ bắt đầu và bo/timer được tăng tốc để kiểm vòng trận. Với `main`, cần ít nhất6 người, lobby15s, loading tối đa30s và bo đúng lịch GDD. **Không dùng test profile để đo thời lượng GDD hoặc đưa public.**
5. Quan sát roster/countdown/loading, hai nhân vật có nhãn tên/HP, bo đỏ hiện tại và bo vàng kế tiếp. WASD gửi input thật; server tính va chạm/speed/HP. Góc nhìn toàn map dành cho demo. Local HeroController bị tắt khi chơi mạng, tránh hai mô phỏng ghi vị trí.
6. Ra ngoài bo để thấy HP giảm5/s; đợi thắng/hòa, xem hạng và nhấn chơi lại. Cùng tick bị loại đồng hạng. Bo về0 vẫn gây damage toàn map;420s không phải timeout cưỡng bức.
7. Rời phòng bằng nút để loại ngay. Mất mạng đột ngột: nhân vật đứng yên và còn nhận damage; nút khôi phục chỉ thành công trong15s grace từ lúc server phát hiện mất kết nối, khi còn sống. Resume giữ PlayerId/slot và đổi proof; không tạo nhân vật thứ hai.
8. Nút lịch sử gọi API cho hồ sơ đã xác minh. Lần đầu tự tạo Guest khi API có sẵn; Windows lưu credential bằng DPAPI, tách theo origin Local/AWS. Đóng/mở lại client giữ hồ sơ. API mất kết nối vẫn xem kết quả trực tiếp; lịch sử báo không khả dụng. Không tự gộp tài khoản theo tên.

Unity hiện nghiệm thu UDP. WSS/Auto trên Unity và prediction/interpolation đầy đủ còn thuộc MOV-02/03; JSurviv Socket.IO cũ không phải client demo này. Chưa có combat vũ khí/inventory/hazard; kills bằng0.

### 17.3. Kiểm API qua Swagger

Mở Swagger Local như mục 5. Các endpoint phòng vẫn cần register/login và Authorize JWT account. Guest credential **không** thay JWT để tạo/list/xóa phòng.

| Endpoint | Cách thử |
| --- | --- |
| `POST /api/guests` | Không cần account; nhận Guest ID và credential, giữ riêng và không chia sẻ |
| `POST /api/game-tickets` | JWT hoặc `X-Guest-Credential`; body attemptId + roomCode của kết nối, ticket dùng một lần/TTL ngắn |
| `GET /api/me/matches` | JWT hoặc Guest credential đúng; phân trang, chỉ lịch sử của hồ sơ đó |
| `GET /api/matches/{matchId}/leaderboard` | Đọc kết quả authoritative đã lưu, không có credential/ProfileKey |
| `/internal/match-results` | Không thử bằng JWT player; tuyến chỉ GameServer qua mạng nội bộ + secret riêng |

Client demo tự xử lý attempt/ticket; không nhập PlayerId tùy ý để nhận lịch sử người khác. Hai người cùng tên vẫn là hai hồ sơ nếu dùng credential khác. Kết quả cần kết thúc trận và outbox được API xác nhận trước khi có trong Mongo/history.

### 17.4. Kiểm thử tự động không tiêu credits

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

### 17.5. Những lần chạy sau, dừng và cập nhật

Bật Docker Desktop → mở đúng thư mục Backend → `docker compose --profile account up -d` → kiểm `/health`/logs → chạy client. Khi sửa code backend thêm `--build`; khi sửa dữ liệu chuẩn, Sync Runtime Data và build client lại. Khi đổi `MATCH_PROFILE` hoặc biến môi trường, chạy compose up để recreate service; restart đơn thuần không nạp biến mới.

Xong buổi Local: đóng client, `docker compose --profile account stop`. Lần bật lại Mongo và outbox trên volume vẫn còn; phòng bổ sung, session và trận trong RAM mất. Không xóa outbox chưa gửi được để dọn dung lượng. Khi API trở lại, outbox retry ngoài tick, cùng MatchId không cộng kết quả lần nữa.

AWS chỉ chuẩn bị compose/Caddy/secret mới, chưa tự deploy. Khi triển khai sau này phải giữ JWT key, RoomCode, Mongo volume và cấu hình cũ; thêm secret kết quả/outbox volume, không publish internal port. Dùng quy trình checksum/rollback và Stop EC2 ở mục7/11/16. EBS vẫn có thể tiêu credits khi EC2 đã Stopped. Hai PC thật và lượt online Phase 2 cần nghiệm thu riêng; hai process trên cùng PC không thay thế điều đó.
