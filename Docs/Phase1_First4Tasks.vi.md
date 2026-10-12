# Hướng dẫn chi tiết bốn task đầu giai đoạn 1

Phạm vi: dự án lớp học, tối đa 10 người một phòng. Theo Tracking đã đọc: INF-01, NET-01, SRV-01 và SRV-02. Bản chỉnh sửa Google Sheets ở lượt trước chưa được xác nhận đã lưu.

Đây là hướng dẫn bạn thực hiện, không phải backend đã được triển khai. Đoạn ghi “giả mã” cần chuyển thành C#. Các lệnh kiểm tra chỉ chạy được sau khi bạn viết phần tương ứng.

## 0. Hệ thống đang xây và nơi làm việc

~~~text
Client thử → API đăng nhập → MongoDB kiểm tra tài khoản
          ← JWT
Client thử → UDP handshake chứa JWT → GameServer xác minh
          ← Accepted hoặc Rejected
GameServer chạy 30 lần mô phỏng mỗi giây
~~~

| Thành phần | Tác dụng | Chạy ở đâu |
| --- | --- | --- |
| Unity | Hiển thị game, nhận thao tác | Unity Editor; nối mạng ở CLI-01 sau này |
| API | Đăng ký, đăng nhập, cấp token | Container api |
| MongoDB | Lưu tài khoản và password hash | Container mongo |
| GameServer | Nhận kết nối, chạy mô phỏng | Container game-server |
| NetSmokeClient | Thử mạng trước khi nối Unity | Console trên máy bạn |

JWT là vé truy cập có chữ ký. API cấp vé, server kiểm tra vé; JWT không tự mã hóa đường truyền.

### 0.1. Mở ứng dụng nào?

1. Mở Docker Desktop, đợi Docker Engine sẵn sàng.
2. Mở VS Code hoặc Visual Studio.
3. Trong editor, mở thư mục D:\Unity\Jungle_Maze_Survival. VS Code dùng File → Open Folder.
4. Mở PowerShell. Trong VS Code: Terminal → New Terminal → chọn PowerShell.
5. Chưa cần sửa scene hay script Unity trong bốn task này.

**Vị trí dự kiến:**

~~~text
D:\Unity\Jungle_Maze_Survival\
  Assets\                        ← code Unity
  Backend\                       ← code server
    global.json
    JungleBackend.sln
    compose.yaml
    .env
    .env.example
    .dockerignore
    README.md
    Api\
      Api.csproj
      Program.cs
      Dockerfile
      Models\User.cs
      Contracts\AuthRequests.cs
      Data\UserRepository.cs
      Auth\TokenService.cs
      Endpoints\AuthEndpoints.cs
    GameServer\
      GameServer.csproj
      Program.cs
      Dockerfile
      Networking\NetworkHost.cs
      Networking\HandshakeValidator.cs
      Simulation\FixedTickLoop.cs
      Diagnostics\ServerMetrics.cs
    NetSmokeClient\
      NetSmokeClient.csproj
      Program.cs
  Docs\
    Phase1_TestResults.md
~~~

Backend nằm ngoài Assets vì Unity tự import C# trong Assets; ASP.NET và GameServer là chương trình riêng. Tên file/class ở đây là đề xuất để phân chia trách nhiệm, tạo đến đâu triển khai đến đó.

**Quy tắc:** lệnh PowerShell dán vào terminal; C#, YAML, Dockerfile lưu bằng editor. Đầu mỗi terminal chạy Get-Location để kiểm tra thư mục. Các lệnh dưới đây chạy ở Backend trừ khi ghi khác.

---

## 1. INF-01 — Local dev và Docker

**Mục đích:** bật API, DB và server bằng một lệnh trên máy bạn. Sau bước này môi trường nền hoạt động, UDP thật sẽ làm ở bước 3.

### 1.1. Kiểm tra công cụ

**Nơi làm:** Docker Desktop và PowerShell tại thư mục bất kỳ.

~~~powershell
dotnet --list-sdks
docker version
docker compose version
docker info
~~~

**Kết quả:** Docker có cả phần Client và Server, Compose trả version, có SDK .NET. Máy hiện tại đã phát hiện SDK 8.0.403, 9.0.318, 10.0.401 và Compose; chưa xác nhận Engine đang chạy.

Nếu cannot connect hoặc thiếu phần Server, kiểm tra Docker Desktop trước; chưa cần sửa code.

### 1.2. Tạo solution và ba project

**Nơi làm:** PowerShell. Chạy từng dòng, gặp lỗi thì dừng sửa.

~~~powershell
Set-Location 'D:\Unity\Jungle_Maze_Survival'
New-Item -ItemType Directory -Path Backend -Force
Set-Location '.\Backend'
dotnet new globaljson --sdk-version 8.0.403 --roll-forward latestPatch
dotnet new sln -n JungleBackend
dotnet new web -n Api -o Api -f net8.0
dotnet new console -n GameServer -o GameServer -f net8.0
dotnet new console -n NetSmokeClient -o NetSmokeClient -f net8.0
dotnet sln JungleBackend.sln add Api/Api.csproj GameServer/GameServer.csproj NetSmokeClient/NetSmokeClient.csproj
dotnet build JungleBackend.sln
~~~

| Thành phần/lệnh | Tác dụng |
| --- | --- |
| global.json | Chọn SDK 8 để khớp GDD dù máy có nhiều SDK |
| solution | Gom project để mở/build chung |
| web | API ASP.NET Core tối thiểu |
| console GameServer | Server chạy độc lập Unity |
| console NetSmokeClient | Công cụ thử kết nối |
| sln add | Đưa project vào solution |
| build | Phát hiện lỗi C#/cấu hình trước Docker |

Nếu project đã tồn tại, mở kiểm tra thay vì tạo đè. Máy khác cần SDK thực sự cài khớp global.json. .NET 8 được chọn để khớp Tracking; khi đổi framework cần đổi package và Docker image cùng nhau.

### 1.3. API kết nối DB và có /health

**Nơi làm:** PowerShell tại Backend:

~~~powershell
dotnet add Api/Api.csproj package MongoDB.Driver
~~~

Driver cho phép API giao tiếp MongoDB; Unity không cần driver này. Ghi lại version đã restore trong Api.csproj.

**Nơi sửa code:** Backend/Api/Program.cs. Thay mẫu bằng:

~~~csharp
using MongoDB.Bson;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration["Mongo:ConnectionString"]
    ?? throw new InvalidOperationException("Missing Mongo connection");
var databaseName = builder.Configuration["Mongo:Database"] ?? "jungle_maze";

builder.Services.AddSingleton<IMongoClient>(new MongoClient(connection));
var app = builder.Build();

app.MapGet("/health", async (IMongoClient mongo) =>
{
    try
    {
        await mongo.GetDatabase(databaseName).RunCommandAsync<BsonDocument>(
            new BsonDocument("ping", 1));
        return Results.Ok(new { status = "ok", database = "connected" });
    }
    catch
    {
        return Results.Json(
            new { status = "unhealthy", database = "unavailable" },
            statusCode: 503);
    }
});

app.Run();
~~~

**Tác dụng:** health kiểm tra DB thật, trả 200 khi kết nối được, 503 khi lỗi. MongoClient singleton tái sử dụng connection pool, không tạo một client DB mới mỗi request.

### 1.4. Tạo server stub

**Nơi sửa:** Backend/GameServer/Program.cs:

~~~csharp
Console.WriteLine("GameServer stub started");
await Task.Delay(Timeout.Infinite);
~~~

Stub giữ container chạy để nghiệm thu môi trường. Bước 3 thay bằng UDP/fixed tick.

### 1.5. Dockerfile đóng gói ứng dụng

**Nơi tạo:** Backend/Api/Dockerfile:

~~~dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Api/Api.csproj Api/
RUN dotnet restore Api/Api.csproj
COPY Api/ Api/
RUN dotnet publish Api/Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://0.0.0.0:5000
EXPOSE 5000
ENTRYPOINT ["dotnet", "Api.dll"]
~~~

**Nơi tạo:** Backend/GameServer/Dockerfile:

~~~dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY GameServer/GameServer.csproj GameServer/
RUN dotnet restore GameServer/GameServer.csproj
COPY GameServer/ GameServer/
RUN dotnet publish GameServer/GameServer.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /out .
EXPOSE 7777/udp
ENTRYPOINT ["dotnet", "GameServer.dll"]
~~~

**Giải thích:** SDK image có compiler; runtime image dùng chạy. COPY tính từ build context Backend. API nghe 0.0.0.0 để nhận request qua mạng container. EXPOSE mô tả port; Compose mới publish port ra Windows.

Nếu thêm ProjectReference sau này, Dockerfile phải COPY project được tham chiếu trước restore/publish.

### 1.6. Cấu hình môi trường

**Nơi tạo:** Backend/.env.example:

~~~dotenv
Jwt__Issuer=JungleMaze.Local
Jwt__Audience=JungleMaze.Game
Jwt__SigningKey=REPLACE_WITH_RANDOM_KEY
~~~

Copy thành Backend/.env. Tạo khóa local trong PowerShell:

~~~powershell
$jwtKeyBytes = New-Object byte[] 32
$jwtKeyGenerator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$jwtKeyGenerator.GetBytes($jwtKeyBytes)
$jwtKeyGenerator.Dispose()
[Convert]::ToBase64String($jwtKeyBytes)
~~~

Dán kết quả vào Jwt__SigningKey trong .env. API và server dùng cùng key; không đưa key vào client hoặc ảnh nghiệm thu.

Thêm vào .gitignore ở gốc project, giữ nguyên dòng cũ:

~~~gitignore
Backend/.env
Backend/**/bin/
Backend/**/obj/
~~~

Tạo Backend/.dockerignore:

~~~text
**/bin
**/obj
.env
.git
~~~

**Tác dụng:** tránh commit secret và đưa file build/secret vào Docker build context.

### 1.7. Compose nối các service

**Nơi tạo:** Backend/compose.yaml:

~~~yaml
services:
  mongo:
    image: mongo:8.0
    volumes:
      - mongo-data:/data/db
    healthcheck:
      test: ["CMD", "mongosh", "--quiet", "--eval", "quit(db.adminCommand({ping:1}).ok ? 0 : 1)"]
      interval: 5s
      timeout: 5s
      retries: 12
      start_period: 10s

  api:
    build:
      context: .
      dockerfile: Api/Dockerfile
    env_file:
      - .env
    ports:
      - "127.0.0.1:5000:5000"
    environment:
      Mongo__ConnectionString: mongodb://mongo:27017
      Mongo__Database: jungle_maze
    depends_on:
      mongo:
        condition: service_healthy

  game-server:
    build:
      context: .
      dockerfile: GameServer/Dockerfile
    env_file:
      - .env
    ports:
      - "127.0.0.1:7777:7777/udp"
    environment:
      GAME_PORT: "7777"
      MAX_PLAYERS: "10"
      TICK_RATE: "30"

volumes:
  mongo-data:
~~~

| Cấu hình | Tác dụng |
| --- | --- |
| env_file | Cùng cấu hình JWT cho API và server |
| Mongo__Database | ASP.NET ánh xạ thành Mongo:Database |
| mongodb://mongo:27017 | API tìm DB bằng tên service trong Docker |
| service_healthy | Đợi DB sẵn sàng |
| volume | Giữ DB qua lần thay container |
| 127.0.0.1 ở host | Bốn bước đầu chỉ thử trên máy bạn |
| /udp | Publish UDP; TCP cùng số port không thay thế |
| MAX_PLAYERS/TICK_RATE | Code server phải đọc và áp dụng |

localhost bên trong API container là chính API container, không phải DB. Mongo không cần publish ra Windows.

### 1.8. Bật và kiểm tra

**Nơi chạy:** PowerShell tại Backend:

~~~powershell
docker compose up -d --build
docker compose ps
docker compose logs --tail 50 api game-server
Invoke-RestMethod -Uri 'http://localhost:5000/health'
~~~

**Cần thấy:** mongo healthy, api/game-server running; health trả ok/connected; log stub started.

Sửa C# xong chạy lại up -d --build để container nhận DLL mới. File sửa trên Windows không tự cập nhật code đang chạy.

**Kiểm tra volume:**

~~~powershell
docker compose exec mongo mongosh jungle_maze --quiet --eval 'db.inf01_probe.insertOne({ marker: 1 })'
docker compose down
docker compose up -d
docker compose exec mongo mongosh jungle_maze --quiet --eval 'db.inf01_probe.findOne({ marker: 1 })'
~~~

Đợi DB healthy trước lệnh cuối. Phải đọc lại bản ghi marker bằng 1. Không dùng down -v khi muốn giữ volume.

**Done INF-01:** Compose bật được, health kiểm tra DB thật, dữ liệu còn sau restart. Lưu log hoặc ảnh tại Docs/Phase1_TestResults.md.

---

## 2. NET-01 — API đăng ký, đăng nhập và JWT

**Tác dụng:** cấp danh tính đã xác thực cho người chơi. **Nơi làm chính:** Backend/Api. Bước này bạn cần viết các hàm theo luồng dưới đây; không phải chỉ tạo file rỗng.

### 2.1. Thêm package

**Nơi chạy:** PowerShell tại Backend:

~~~powershell
dotnet add Api/Api.csproj package Microsoft.AspNetCore.Authentication.JwtBearer --version "8.0.*"
dotnet list Api/Api.csproj package
~~~

8.0.* chọn dòng ASP.NET Core 8; sau restore có thể pin chính xác version đã nhận. TokenService dùng JwtSecurityTokenHandler; kiểm tra dependency System.IdentityModel.Tokens.Jwt trước khi thêm trực tiếp.

### 2.2. Tạo model và hợp đồng JSON

**Nơi tạo bằng editor:**

| File | Nội dung cần viết | Tác dụng |
| --- | --- | --- |
| Api/Models/User.cs | Id, Username, NormalizedUsername, PasswordHash, CreatedAtUtc | Bản ghi DB |
| Api/Contracts/AuthRequests.cs | RegisterRequest/LoginRequest có username,password; LoginResponse có accessToken,userId,expiresAtUtc | Chốt JSON client/API |
| Api/Data/UserRepository.cs | CreateAsync, FindByNormalizedUsernameAsync, EnsureIndexesAsync | Gom query DB |

Dùng Guid string cho ID ở prototype, UTC cho ngày. Username chuẩn hóa bằng Trim và ToUpperInvariant; giữ tên hiển thị riêng.

EnsureIndexesAsync tạo unique index trên NormalizedUsername khi API khởi động. Duplicate key lúc insert trả 409. Chỉ kiểm tra username tồn tại trước insert không đủ khi hai request đồng thời.

### 2.3. Đăng ký và login

**Nơi viết:** Api/Endpoints/AuthEndpoints.cs. Đăng ký service/endpoint trong Api/Program.cs.

~~~text
Register: kiểm tra request → chuẩn hóa → tạo ID → hash password → insert → 201
Login: chuẩn hóa → tìm user → verify password → tạo JWT → 200
~~~

Dùng PasswordHasher<User> có sẵn:
- HashPassword để lưu hash.
- VerifyHashedPassword để kiểm tra lúc login.
- Không lưu password thô và không trả hash cho client.
- Request thiếu dữ liệu trả 400; username trùng 409; login sai 401.

Trước builder.Build(), đăng ký UserRepository, TokenService và hasher vào DI. DI cung cấp service cho endpoint thay vì endpoint tự tạo mọi dependency.

### 2.4. TokenService và /me

**Nơi viết:** Api/Auth/TokenService.cs. Đọc Jwt:Issuer, Jwt:Audience, Jwt:SigningKey từ cấu hình.

| Claim | Giá trị | Tác dụng |
| --- | --- | --- |
| sub | User ID từ DB | Danh tính |
| iss | JungleMaze.Local | Nguồn cấp |
| aud | JungleMaze.Game | Đối tượng sử dụng |
| exp | UTC hiện tại + 60 phút | Hạn token |
| jti | ID mới | Phân biệt token |
| iat | Thời điểm cấp | Chẩn đoán |

HS256 và key chung giữa API/server là lựa chọn cho prototype local đóng của bài lớp học, không phải hệ thống auth public hoàn chỉnh.

**Trong Program.cs:**
1. AddAuthentication + AddJwtBearer.
2. Bật kiểm tra signing key, issuer, audience, lifetime; giới hạn thuật toán đã chốt.
3. MapInboundClaims=false để đọc sub nhất quán.
4. AddAuthorization.
5. Sau Build gọi UseAuthentication rồi UseAuthorization.
6. Register/login không yêu cầu token.
7. GET /me có RequireAuthorization, lấy sub từ principal đã xác thực.

TokenService tạo vé. Middleware kiểm tra vé cho HTTP. UDP cần validator riêng ở bước 4.

### 2.5. Thử API

**Nơi chạy:** PowerShell tại Backend, sau khi viết xong logic:

~~~powershell
docker compose up -d --build api
$authPayload = @{ username = 'player01'; password = 'ClassDemo_123!' } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri 'http://localhost:5000/auth/register' -ContentType 'application/json' -Body $authPayload
$loginResult = Invoke-RestMethod -Method Post -Uri 'http://localhost:5000/auth/login' -ContentType 'application/json' -Body $authPayload
Invoke-RestMethod -Uri 'http://localhost:5000/me' -Headers @{ Authorization = "Bearer $($loginResult.accessToken)" }
~~~

Response phải có accessToken theo hợp đồng. Token giữ trong biến, không in toàn bộ vào log. HTTP ở đây chỉ localhost và tài khoản demo, chưa gửi password qua LAN.

**Done NET-01:** register mới 201; trùng username 409; login đúng 200; sai password 401; /me đúng user. Thiếu/giả/hết hạn token đều 401. Restart container vẫn login được. Để thử expiry, tạm TTL ngắn rồi phục hồi 60 phút.

---

## 3. SRV-01 — LiteNetLib và fixed tick 30 Hz

**Tác dụng:** nhận UDP và chạy mô phỏng theo nhịp cố định. **Nơi làm:** Backend/GameServer và NetSmokeClient; chưa sửa Unity.

### 3.1. Package giống nhau

**Nơi chạy:** PowerShell tại Backend:

~~~powershell
dotnet add GameServer/GameServer.csproj package LiteNetLib --version 2.1.4
dotnet add NetSmokeClient/NetSmokeClient.csproj package LiteNetLib --version 2.1.4
dotnet build JungleBackend.sln
~~~

2.1.4 là version xác minh trên NuGet khi viết hướng dẫn. Nếu thay, thay cả hai và đọc mẫu API đúng version.

### 3.2. File và trách nhiệm

| File trong GameServer | Việc cần viết |
| --- | --- |
| Program.cs | Đọc GAME_PORT,MAX_PLAYERS,TICK_RATE; khởi tạo; xử lý dừng |
| Networking/NetworkHost.cs | Socket, connect/disconnect/receive, ping/pong |
| Simulation/FixedTickLoop.cs | Đồng hồ, accumulator, tick counter, catch-up |
| Diagnostics/ServerMetrics.cs | Gom số đo, báo cáo định kỳ |

NetSmokeClient/Program.cs chứa client thử. Đọc cấu hình bằng Environment.GetEnvironmentVariable, parse/kiểm tra trước khi chạy. Environment trong Compose không tự áp giới hạn nếu code bỏ qua.

Khi thêm gameplay sau này, receive đưa input vào hàng đợi để Tick xử lý; không đặt toàn bộ gameplay trong callback mạng.

### 3.3. Viết NetworkHost

Dùng mẫu chính thức LiteNetLib ở cuối tài liệu. API cần dùng:
- EventBasedNetListener nhận sự kiện.
- NetManager.Start(port) mở socket.
- ConnectionRequestEvent accept/reject.
- PeerConnectedEvent/PeerDisconnectedEvent theo dõi peer.
- NetworkReceiveEvent đọc packet.
- PollEvents đưa sự kiện tới code.
- NetDataWriter và peer.Send gửi packet.
- Stop đóng socket.

Bước này local có thể dùng key cố định tạm để thử UDP, bước 4 phải thay bằng JWT.

Chốt packet:
~~~text
Ping: byte messageType=1, int sequence
Pong: byte messageType=2, int sequence
~~~

Pong giữ sequence để client ghép thời điểm gửi/nhận. Dùng Unreliable cho ping để thấy packet có thể mất. Timeout không đồng nghĩa server crash.

Kiểm tra đủ byte trước đọc; recycle reader theo cấu hình, tránh double-recycle nếu bật tự recycle. Trước accept kiểm tra số peer; đủ 10 thì reject.

### 3.4. Fixed tick: cách viết và vì sao

**Nơi viết:** Simulation/FixedTickLoop.cs. 30 Hz là 30 lần mô phỏng/giây, delta=1/30 giây=33,333 ms; không phải FPS Unity.

**Giả mã cần chuyển thành C#:**
~~~text
previous = Stopwatch hiện tại
accumulator = 0

while server chạy:
    now = Stopwatch hiện tại
    accumulator += now - previous
    previous = now
    PollEvents()
    steps = 0

    while accumulator >= 1/30 giây và steps < 5:
        Tick(delta=1/30)
        đo thời gian thực thi
        accumulator -= 1/30 giây
        tăng tick counter và steps

    nếu backlog vẫn quá lớn:
        ghi overload, áp chính sách giới hạn backlog cho prototype
        không coi phần mô phỏng bỏ qua là thành công

    chờ ngắn nếu chưa tới tick kế tiếp
~~~

Stopwatch đo thời gian trôi thật; accumulator giữ phần dư; fixed delta giúp gameplay nhất quán. Giới hạn catch-up tránh vòng lặp vô tận. Chờ ngắn giảm idle CPU, nhưng không coi Sleep(33) là lịch tick chính xác.

Đo riêng thời gian Tick và toàn vòng gồm callback mạng. Callback chậm cũng làm trễ tick dù Tick rất nhanh.

### 3.5. Viết client thử

**Nơi viết:** NetSmokeClient/Program.cs:
1. Đọc args host/port, mặc định 127.0.0.1:7777.
2. Tạo listener/NetManager, Connect với key tạm.
3. PollEvents thường xuyên.
4. Sau connected, mỗi giây Ping; lưu Stopwatch theo sequence.
5. Nhận Pong tính RTT.
6. Báo timeout/reject/disconnect.
7. Ctrl+C đóng NetManager.

**Terminal A tại Backend:**
~~~powershell
docker compose up -d --build game-server
docker compose logs -f game-server
~~~

**Terminal B tại Backend:**
~~~powershell
dotnet run --project NetSmokeClient/NetSmokeClient.csproj -- 127.0.0.1 7777
~~~

Args chỉ hoạt động khi bạn đã viết phần đọc args. Ctrl+C ở logs chỉ ngừng xem, container vẫn chạy; ở client dừng client.

Thử 10 instance bằng 10 terminal hoặc chế độ nhiều client trong công cụ. Peer thứ 11 phải reject.

### 3.6. Đo nghiệm thu

**Nơi xem:** ServerMetrics/log và Docker:
~~~powershell
docker stats --no-stream
~~~

Lấy nhiều mẫu trong 5 phút. Docker CPU theo core có thể khác tỷ lệ tổng trong Task Manager; ghi cách đo và cấu hình máy.

Log mỗi vài giây: peers, ticks/s, tick p95/p99, full-loop time, overrun. p95/p99 là mức 95%/99% mẫu không vượt. Không log từng tick.

**Done SRV-01:** ping/pong hoạt động; nhịp gần 30 Hz ổn định; 10 peer và reject peer 11; idle CPU dưới 5% theo cách đo ghi lại. Đo tick time so ngân sách 33,3 ms. Đây chưa phải benchmark gameplay.

---

## 4. SRV-02 — Handshake xác thực JWT

**Tác dụng:** thay key tạm bằng danh tính đã kiểm tra. **Phụ thuộc:** NET-01 và SRV-01.

### 4.1. Tạo validator ở server

**Nơi tạo:** GameServer/Networking/HandshakeValidator.cs.

Console server không có middleware HTTP. Cần package System.IdentityModel.Tokens.Jwt. Tại Backend:
~~~powershell
dotnet list Api/Api.csproj package --include-transitive
~~~

Đọc version package đó từ output, thêm cùng version vào GameServer.csproj rồi restore. Không dán placeholder version vào lệnh.

Đọc environment Jwt__Issuer, Jwt__Audience, Jwt__SigningKey do cùng .env cung cấp.

Validate(token) cần kiểm tra signature, algorithm, issuer, audience và expiry; RequireExpirationTime/RequireSignedTokens; ClockSkew rõ ràng, đề xuất 30 giây. MapInboundClaims=false, lấy sub không rỗng. Catch lỗi xác thực, trả reason thay vì crash.

ReadJwtToken chỉ đọc nội dung, không xác minh chữ ký.

### 4.2. Chốt handshake client/server

**Nơi sửa:** NetworkHost.cs và NetSmokeClient/Program.cs:
~~~text
ushort protocolVersion = 1
string accessToken = JWT từ API
~~~

Hai bên ghi/đọc cùng thứ tự. Đề xuất tổng payload <=4096 byte, token <=2048 ký tự; đo token thật và kiểm tra giới hạn LiteNetLib/MTU.

Nếu JWT không vừa connection request, dùng peer pending có timeout/giới hạn, gửi Auth packet ReliableOrdered rồi mới chuyển authenticated. Pending không được gửi gameplay input. Transport connected không đồng nghĩa authenticated.

### 4.3. Accept/reject và mapping phiên

**Nơi viết:** NetworkHost.cs, xử lý connection request/auth:
~~~text
Kích thước → parse → version → Validate JWT → sub
→ phiên trùng → sức chứa → accept/map peer-user
~~~

Lưu dictionary peerId→userId và userId→peerId. Tối đa 10 authenticated; cùng user không tạo phiên thứ hai; disconnect dọn cả hai mapping.

Chỉ Accept sau kiểm tra; với pending chỉ gửi AuthAccepted khi toàn bộ kiểm tra đạt.

Log reason: InvalidToken, TokenExpired, VersionMismatch, DuplicateSession, RoomFull, MalformedHandshake. Không log token/password.

Xác minh bằng key sẵn có ở server, ngoài hàm Tick. Không gọi API/DB đồng bộ trên từng packet/tick.

### 4.4. Client login trước UDP

**Nơi sửa:** NetSmokeClient/Program.cs:
1. Nhập username/password demo bằng console, không dùng command-line args.
2. HttpClient POST localhost:5000/auth/login.
3. API lỗi thì không nối UDP.
4. Deserialize accessToken/userId, giữ token trong bộ nhớ.
5. NetDataWriter ghi version rồi token.
6. Connect bằng handshake đã chốt.
7. Chờ Accepted/AuthAccepted rồi ping.
8. Giữ vòng PollEvents và xử lý reject.

HTTP chỉ localhost ở bốn bước này; chưa mở password ra LAN.

### 4.5. Chạy và kiểm tra

**Nơi chạy:** Backend:
~~~powershell
docker compose up -d --build api game-server
docker compose logs -f game-server
~~~

Terminal khác:
~~~powershell
dotnet run --project NetSmokeClient/NetSmokeClient.csproj -- 127.0.0.1 7777
~~~

Client hỏi tài khoản sau khi bạn viết phần nhập console.

| Ca | Cách tạo | Kết quả |
| --- | --- | --- |
| Hợp lệ | Login user demo | Accepted đúng ID |
| Thiếu token | Test chuỗi rỗng | Reject |
| Giả token | Sửa ký tự phần chữ ký | Reject |
| Hết hạn | TTL ngắn, chờ qua exp+ClockSkew | Reject |
| Sai issuer/audience | Token test cấu hình khác | Reject |
| Sai version | Gửi 99 | Reject |
| Gói lỗi | Thiếu trường/quá giới hạn | Reject, server không crash |
| Trùng user | Hai client cùng user | Client thứ hai reject |
| Đủ phòng | 10 user khác nhau, thêm user 11 | RoomFull |
| Disconnect | Đóng client, thử user đó lại | Mapping cũ được dọn |

**Done SRV-02:** login→JWT→UDP authenticated hoạt động; ca lỗi reject; không còn đường gameplay bằng key tạm. Dọn phiên cơ bản chưa phải reconnect giữ HP/inventory trong trận, sẽ làm sau khi có match state.

---

## 5. Khi nào chuyển sang LAN?

Hoàn thành local trước, sau đó CLI-01 và thử máy thứ hai.

**Máy server:** PowerShell ipconfig lấy IPv4 của mạng lớp; đổi host mapping UDP từ 127.0.0.1:7777:7777/udp thành 7777:7777/udp; recreate container; cấu hình firewall UDP 7777 cho mạng lớp/private.

**Máy client:** dùng IP máy server, không localhost. localhost là chính máy đang chạy client.

Đăng nhập từ máy khác cần API HTTPS và chứng chỉ đúng hostname/IP được máy đó tin cậy; không chỉ mở API HTTP chứa password ra LAN. JWT UDP không tự mã hóa; khi triển khai LAN cần chốt bảo vệ đường truyền hoặc ticket vào trận ngắn hạn. Đây là phần chuyển LAN, chưa được hoàn thành chỉ bằng code local.

Nếu Wi-Fi lớp cách ly client/chặn UDP, cần hỗ trợ mạng từ quản trị viên.

## 6. Lỗi thường gặp và nơi kiểm tra

| Lỗi | Nơi xem | Hướng xử lý |
| --- | --- | --- |
| Docker không chạy | Docker Desktop/docker info | Engine ready |
| Không tìm Compose | Get-Location | Chuyển Backend |
| API không thấy DB | Compose/api logs | Hostname mongo, DB healthy |
| Health 503 | Mongo logs/config | Kiểm tra connection string |
| Port đã dùng | Docker Desktop/tiến trình local | Dừng bản trùng hoặc đổi host port |
| Code mới chưa chạy | API/server logs | up -d --build |
| JWT API đúng, UDP sai | .env/validator/token service | Key/issuer/audience/algorithm/clock thống nhất |
| UDP không nhận | Mapping/host/PollEvents | Đúng /udp, port, host và poll |
| CPU cao | Fixed loop/log | Tránh busy loop/log từng tick |
| Docker lỗi sau ProjectReference | Dockerfile | COPY project tham chiếu trước restore |

## 7. Ghi kết quả và cập nhật Tracking

Tạo Docs/Phase1_TestResults.md trong editor:
~~~markdown
| Task | Ngày | Máy | Kiểm tra | Kết quả | Log/commit |
| --- | --- | --- | --- | --- | --- |
| INF-01 | | | Compose + health + volume | | |
| NET-01 | | | Register/login + JWT lỗi | | |
| SRV-01 | | | UDP + 30 Hz + 10 client | | |
| SRV-02 | | | Handshake + reject cases | | |
~~~

Trong 02. GDD Tracking tìm task theo ID. Bắt đầu thì In Progress nếu dropdown có; chỉ Done sau nghiệm thu. Không thêm status ngoài validation.

Có thể mỗi buổi một task; chưa đạt thì sửa trước khi chuyển bước. Cuối giai đoạn ghi Backend/README.md: lệnh bật, port, cách chạy client thử để bạn học làm lại.

## Tài liệu chính thức

- [Docker Compose startup/health check](https://docs.docker.com/compose/how-tos/startup-order/)
- [MongoDB C# driver](https://www.mongodb.com/docs/drivers/csharp/current/connect/mongoclient/)
- [ASP.NET Core JWT](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0)
- [LiteNetLib server/client](https://github.com/RevenantX/LiteNetLib)
- [LiteNetLib 2.1.4 NuGet](https://www.nuget.org/packages/LiteNetLib/2.1.4)
