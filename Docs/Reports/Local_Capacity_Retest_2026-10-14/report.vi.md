> Cập nhật mới nhất: **Phase2 Local —24/10/2026**. Phần bổ sung ở đầu từng mục; giữ dữ liệu các lượt AWS/capacity cũ để đối chiếu.

# Báo cáo Phase 2 Local và lịch sử kiểm thử mạng

**Phase 2 Local: PASS cho SRV-03, NET-02, ZON-01 và demo Unity UDP. Hai Windows player trên cùng PC hoàn tất trận → kết quả → lịch sử Guest → chơi lại. AWS Phase 2, hai PC vật lý và tải 50 phiên gameplay: NOT_TESTED.**

Kết quả mạng ngày 16/10 là lịch sử: UDP/WSS và Local 10 phiên/300s đã PASS; PC → EC2 WSS dùng IP pin/TLS đúng, Auto và Stop–Start đã qua. DNS/hosts riêng trên PC còn BLOCKED ở lượt đó.

Một báo cáo chung; giữ thư mục cũ để các link đang có hoạt động. Bảng vận hành dùng UTC+7, dữ liệu thô dùng UTC. Lượt Phase 2 không truy cập hoặc bật AWS; trạng thái Stopped bên dưới là bằng chứng tại các lượt kiểm thử trước.

## 1. Mục đích và phạm vi

### Bổ sung Phase2 — 24/10/2026

Lượt này triển khai SRV-03, NET-02, ZON-01 và demo Unity UDP trên **Local**. Hai Windows player thực trên cùng PC hoàn thành trận, thấy nhau di chuyển/HP/bo, xem kết quả, chơi lại và đọc lịch sử Guest. Các phần AWS/co-located phía dưới thuộc những lượt cũ, không được diễn giải thành nghiệm thu Phase2 online.


### Lịch sử Phase 1 — 14–16/10/2026

Kiểm tra tạo phòng bắt buộc login/JWT, giữ phòng mặc định và hạn mức 10 phiên/phòng, 5 phòng gồm mặc định, tổng 50 active/pending, 2 phòng bổ sung/user. Đo LiteNetLib ping/pong 30Hz và tick backend; chưa mô phỏng gameplay.

Theo yêu cầu xử lý mạng chặn, dùng lại **SSH qua Session Manager**: API HTTPS qua tunnel TCP tới Caddy, NetSmokeClient chạy UDP ngay trong EC2. Đây là phép đo backend với generator dùng chung máy. **Chưa xác nhận UDP Internet từ PC/lớp, FPS hoặc trận Unity.** Dữ liệu local 14/10 giữ riêng, không đổi nhãn thành AWS.

### Bổ sung UDP/WSS ngày 16/10/2026

Đợt mới bổ sung UDP chính và WebSocket binary qua WSS 443 làm đường dự phòng, không yêu cầu tester cài VPN. Backend/NetSmokeClient dùng chung phòng, PlayerId và tổng slot; chưa chuyển gameplay Unity. Các kết quả UDP/co-located và Tailscale ở các đoạn cũ là lịch sử, không đại diện tuyến WSS mới.


## 2. Những việc thực hiện trước khi kết luận

### Công việc Phase2 thực hiện trước khi kết luận

1. Tạo nhánh `phase2/match-results-zone`; Antigravity Flash High/effort high triển khai hai gói bằng worktree riêng. Codex review, sửa và kiểm thử trước khi tích hợp; mỗi gói có một vòng giao sửa. Không merge main, push hoặc deploy AWS.
2. Công bố runtime/clock/entity/health và contract kết quả. Runtime độc lập theo phòng, lifecycle đầy đủ, roster/loading, input có MatchId/sequence/tick, timeout input, normalization, sweep/slide cover và bounds. Ping-only không đủ điều kiện bắt đầu.
3. Giữ protocol1/2, thêm v3 cho resume. Sửa slot rỗng bị ghi đè, đăng ký broadcast lặp, grace giữ đúng slot, xoay proof, callback cũ, loading timeout trả slot và bỏ packet trận cũ. UDP/WebSocket dùng cùng runtime.
4. Đưa map/match/bo có version vào Shared/GameData. Thêm sync/verify trước build, manifest từng file và aggregate hash chuẩn hóa BOM/xuống dòng Windows/Linux. Kiểm geometry scene trước LoadingAck; có10 spawn an toàn, cách nhau tối thiểu6 cell.
5. Áp dụng đúng5 đợt bo, cảnh báo, tâm seeded/nested/walkable,5HP/s qua HealthService và r0 toàn map. Tính đồng hạng sau toàn bộ damage tick; không ép kết thúc420s; giữ replay buffer riêng6s.
6. Thêm Guest256-bit, DB chỉ hash; ticket dùng một lần, ràng buộc attempt/phòng; JWT vẫn bắt buộc quản lý phòng. Sửa việc đăng ký ticket tự gắn danh tính: nay chỉ gắn khi phiên chứng minh sở hữu ticket. Sửa default RoomCode phân biệt hoa/thường và giữ identity qua vòng sau.
7. Lưu kết quả nguyên tử theo MatchId trong Mongo standalone, canonical hash không nhập nhằng và giữ đủ độ chính xác; idempotent/conflict, lịch sử theo hồ sơ và public leaderboard đã bỏ thông tin riêng. Sửa biểu diễn Guid BSON và Swagger security OR cho JWT/Guest.
8. Làm outbox ngoài tick: hàng đợi bounded, ghi nguyên tử trên volume, retry độc lập2→30s, quota gồm quarantine, không xóa trước ACK, flush có deadline và metric historyUnavailable riêng. Kiểm API lỗi/DB restart/replay file cũ.
9. Tích hợp MatchSession, input thật, camera overview, nhãn/HP/bo/kết quả/replay/history và resume trong Unity. Tắt controller/weapon probe local khi chơi mạng, giữ view theo server. DPAPI tách origin; first-writer-wins khi hai process khởi động cùng lúc.
10. Lượt Unity đầu chơi được nhưng **FAIL history** vì Hello sớm hơn ticket khi lobby test chỉ0,5s. Đổi thứ tự AttachIdentity → Hello trên cùng channel reliable; chuẩn bị có timeout6s, lỗi API vẫn chơi ẩn danh. Lượt cuối hai client đều xác nhận identitySent/historyLoaded và vào vòng2.
11. Build/test Local, HTTP với Mongo thật, Docker game/API và TLS Caddy. Chuẩn bị compose, secret kết quả riêng, outbox volume và script AWS bổ sung secret mà không ghi đè khóa cũ. Không chạy các script đó trên AWS.
12. Khôi phục side effect URP/ProjectSettings/Editor do Unity build/test, giữ scene Game không dirty. Gói bằng chứng chỉ lấy whitelist; không đưa file.env, JWT, Guest credential, token/SSH/AWS credential vào archive.


### Công việc các lượt Phase 1 trước đây

1. Đọc source/cấu hình và công cụ quản trị. Bật Docker Desktop; AWS CLI hết hạn được đăng nhập lại bằng xác nhận trực tiếp của người dùng. Phát hiện EC2 đã Running trước buổi, đã Stop trong lúc chuẩn bị local.
2. Build solution không lỗi; chạy 225 checks hiện có: 124 network, 50 account, 9 migration secret, 42 API/phòng/Docker. Bổ sung test JWT thật một phút: **55/55 account checks**, gồm năm kiểm tra lifetime/khôi phục, dùng backend và Mongo local riêng.
3. Kiểm thêm migration numeric giữ secret/idempotent/chặn key trùng; RTT từng phòng, tốc độ từng client; khởi tạo harness khi HTTP lỗi; mã thoát Windows; Stop/retry/quyền sở hữu. Client Linux được chạy UDP thật trong container read-only 256MiB/0.75 core trước khi dùng AWS.
4. Thêm test-aws-rooms.ps1 quản vòng đời: Stopped/exclusive, EBS x86_64, CPU Standard, shutdown=stop, DryRun quyền Start/Stop. Watchdog PC được đặt trước Start, timer shutdown EC2 khi SSM Online; finally luôn Stop và xác nhận Stopped. Không nâng instance hoặc tạo EC2/LB/monitoring mới.
5. Thêm AppsOnly vào builder/verifier/deployer. Gói **89,7 MiB** chỉ có API/GameServer, kiểm SHA-256, giữ Mongo 7/Caddy và volume. Sửa upload SSM chậm sang SFTP tiếp tục file tên theo hash; cho phép tái sử dụng đúng release đã cài sau khi đối chiếu manifest.
6. Migration chỉ thêm control secret nếu thiếu và đặt numeric 10/50/5/2, không ghi đè các trường DNS/JWT key/room mặc định/Swagger/DB. Cổng control 7780 không publish. Mongo giữ container ID c45883bfaf88; Caddy giữ image/volume, container được tạo lại khi boot với đường mount release mới.
7. Hai probe UDP từ PC không connected/join, không pong. SG mở UDP 7777, docker-proxy lắng nghe 0.0.0.0:7777, UFW inactive; cửa sổ bắt gói 20s không ghi nhận gói tới port. Chưa xác định chính xác đoạn mạng chặn; không mở thêm cổng hoặc đổi transport gameplay.
8. Theo yêu cầu, chuyển sang GeneratorOnServer. HTTPS vẫn xác minh certificate junglemaze.duckdns.org qua SSH/SSM. Room plan đi qua SSH stdin, không lên argv/log; client/container/file /run được dọn sau bài.
9. AWS đạt **39/39 kiểm tra chức năng/JWT** và tải 10 PASS. Harness sau đó lỗi cleanup: phòng đã xóa bằng JWT mới nhưng record cũ còn giữ JWT ngắn. Đã sửa theo ID phòng, thêm regression, giữ nguyên dữ liệu/lỗi cũ.
10. Lượt bổ sung kiểm hash bằng chứng chức năng/10 PASS và image GameServer trùng, đăng nhập/tạo phòng lại, đo baseline mới rồi chạy 30/50 và xác nhận 50 trong 180s. Không thử >50 và không tìm trần cuối.
11. Cập nhật hướng dẫn mục 14.7 (trước đây đánh số trùng 14.4) và Network Architecture; gộp JSON/CSV/bằng chứng vào báo cáo này. Không sửa Unity.

### Bổ sung sau yêu cầu thông UDP và rà soát Phase 1

12. Đối chiếu bốn task với source, tách JWT quản lý phòng khỏi RoomCode join. Bổ sung bốn biến endpoint NET_MODE/NET_GAME_SERVER_HOST/NET_GAME_SERVER_PORT/NET_API_BASE_URL, ưu tiên env rồi JSON, validate sau merge và không tự đổi tuyến. Thêm 8 regression network: **132/132** network checks; giữ protocol v1.
13. Thêm metadata endpoint/host phát/PlayerId/cổng UDP/transport packet/RTT p50 vào báo cáo client. Tách GAME_PUBLISH_HOST và API_PUBLISH_HOST với fallback PUBLISH_HOST, kiểm Compose loopback/legacy/VPN/private Mongo. Không publish control 7780.
14. Chạy lại suite Local: **132 network, 50 account, 9 migration gốc và các nhóm bổ sung, 42 kiểm tra phòng**. Sau đó chạy stack riêng: down/up giữ volume, login bằng account cũ thành công; có **43 kiểm tra chức năng** và lượt 10 phiên/300s/30Hz. Lượt đầu công cụ dùng ID container cũ sau down/up nên lỗi inspect; đã lấy lại ID, giữ nguyên report lỗi và chạy lại, không tính lỗi công cụ thành server quá tải.
15. Docker Desktop ban đầu tắt được bật lại. Cài Tailscale bằng winget thất bại 0x80072f7d; thử HTTPS bằng Python cũng TLSV1_ALERT_INTERNAL_ERROR. Cả pkgs/dl/login/controlplane.tailscale.com trả 94.140.14.33. Người dùng xác nhận chưa thể đổi hotspot và yêu cầu thử trực tiếp/ghi phần chặn. Chỉ chuẩn bị setup script và cấu hình VPN mẫu; **không cài/enroll EC2** khi PC chưa dùng được.
16. EC2 ban đầu Stopped, sau đó được bật ngoài lượt này. Preflight từ chối can thiệp. Sau khi người dùng xác nhận không ai dùng và cho phép test rồi Stop, tiếp quản máy Running. Lượt đầu tunnel SSM thoát trước UDP; lượt sau HTTPS/JWT qua nhưng lệnh Bash stdin từ Windows bị CRLF/exit 2. Đã ghi các lượt lỗi, tăng thời gian bắt tay/giữ log SSH và chuyển stdin thành UTF-8 bytes LF, thêm regression. Mỗi lượt đều Stop và xác nhận Stopped.
17. Lượt cuối dùng NetSmokeClient trên **PC vật lý**, Public IPv4 hiện tại, quan sát header trên ens5 tại EC2 trong cùng bài thử. Client ghi 10 transport packet gửi, 0 nhận; 0 connected/join/pong. Observer không thấy UDP 7777 tới/đi; listener và Docker publish 0.0.0.0:7777 có mặt. Chưa xác định chính xác PC/firewall/router/ISP/chính sách mạng nào làm mất gói; không đổi SG hoặc mở thêm port không có bằng chứng.
18. AWS cuối đạt **8/8 kiểm tra quản trị/API**: HTTPS certificate/DB health, reuse/register, login, JWT thiếu/sai/sửa chữ ký bị 401, chỉ có mặc định lúc bắt đầu, JWT hợp lệ tạo phòng 10 slot với mã 6 ký tự. Phòng test đã xóa, cleanupErrors rỗng. Không chạy tải WAN/Stop–Start rejoin vì UDP BLOCKED. Đã đóng gói client Windows cần .NET 8 Runtime cho PC thứ hai; chưa có PC thứ hai để nghiệm thu. Không deploy hoặc nâng instance trong đợt này.

Các lượt bị dừng và thời gian của chúng được giữ trong mục 7.

### Bổ sung UDP/WSS ngày 16/10/2026

19. Xác minh CRC, manifest SHA-256 và checksum archive 94 file trước khi đụng dữ liệu; giữ nguyên bằng chứng cũ. Kiểm kê bảy thư mục `.utmp` có thể tái tạo, tổng 814,27MiB. Automatic approval review từ chối xóa đệ quy với “blocked by policy”, nên không dùng công cụ khác để lách. Chạy Docker build-cache prune chỉ phần cũ hơn bảy ngày, thu hồi 0B; giữ bốn container dev đang dừng, Mongo volumes, images và releases rollback.
20. Tách `SessionHub` khỏi adapter LiteNetLib; thêm WebSocket 7781 `/game`, binary ≤4KiB, outbound queue 64/session, event queue giới hạn. I/O bất đồng bộ không chạy trong tick. Hai transport dùng chung RoomManager, JWT pending, timeout, PlayerId và chính sách 10/50/5/2.
21. Giữ UDP v1, thêm v2 với nonce 128 bit cho một lượt kết nối. Chuyển UDP → WSS trước application message đầu tiên giữ một slot/PlayerId; callback UDP cũ không giải phóng phiên mới. Giữ tombstone 30s, từ chối replay hoặc takeover sau commit. Không ghi nonce/code/token vào log.
22. Thêm client Udp/Wss/Auto, env ưu tiên JSON; Auto chờ UDP tối đa 5s rồi WSS 10s connect +5s join. Từ chối nghiệp vụ không fallback; không chuyển giữa trận. Remote bắt buộc WSS với certificate đúng, WS chỉ loopback.
23. Chạy suite: 132 network, 50 account, 42 room, các nhóm migration/evaluator/lifecycle; suite dual cuối cùng 44 checks. Kiểm socket WebSocket thật, UDP v2 thật, Auto khi UDP không có server, mixed 10/11, global cap, callback/replay, fragmented/text/oversize/malformed. Một lượt dual từng thất bại do CloseOutputAsync làm client thấy reset; sửa graceful CloseAsync, thêm kiểm pin IP và từ chối TLS self-signed, lượt cuối 44/44. Giữ log lỗi cũ.
24. Stack Local riêng đạt 43 checks chức năng/restart/account, rồi đo riêng UDP v2 và WebSocket: 15s warm-up +300s, 10 client ×30Hz, server 256MiB. Image bài đo được build trước hai sửa nhỏ graceful close/callback guard; các sửa đó có regression và suite final-image/Caddy riêng. Không gán nhầm fingerprint hai image.
25. Build release apps-only 89,7MiB; kiểm 27 file/checksum, Linux amd64 và không có private config. Dùng image cuối kiểm 35 Caddy/AWS Docker checks, HTTPS certificate CA nội bộ, Swagger Basic auth, API JWT, account persistence và legacy UDP. Caddy chỉ route chính xác `/game`, không áp Swagger Basic auth vào game; cổng 7780/7781/Mongo/API không publish AWS.
26. AWS qua SSM để quản trị/upload, client WSS chạy trên PC thật qua Internet 443. Helper Windows chỉ cập nhật dòng Jungle Maze sau TLS/health thật và backup hosts; không bỏ kiểm TLS. Lượt đầu lỗi SFTP put -a khi file đích chưa tồn tại, chưa deploy; Stop được xác nhận. Sửa phân biệt upload mới/resume. Lượt hai upload chỉ khoảng 40KiB/s, mới truyền 11MiB sau hơn 4 phút, nên chủ động ngắt để Stop, không chờ truyền hết 89,7MiB.
27. Chuẩn bị delta khoảng 0,42MiB khi EC2 tắt: dùng lại tám runtime layer từ hai image cũ được ghim ID, chỉ gửi layer/config mới. Kiểm SHA-256 delta liên kết release đầy đủ; restore local xác nhận từng SHA layer và runtime config trùng. Đóng gói OCI sang Docker legacy có thể đổi manifest/image ID; lưu cả source/restored ID và config hash, không tuyên bố image ID byte-identical. Trên EC2 export đúng base, dựng lại archive, kiểm nội dung rồi mới chạy deploy/checksum chuẩn; thiếu base/layer thì dừng. Gói release đầy đủ vẫn giữ để rollback.
28. Lượt đã deploy join WSS thật, Auto fallback và 10/11 PASS; nhưng bài 300s bị mất cả 10 kết nối tại 167,6s và SSM không lấy được snapshot. Client Local đồng thời vẫn hoàn thành 300s. Người dùng xác nhận có Sleep hoặc mất/đổi mạng; không tính bài ngắt thành PASS, không dùng nó để kết luận server yếu. Stop qua CLI tại 15:11 không xác nhận được, timer EC2 độc lập vẫn hoạt động; truy vấn sau phục hồi lúc 16:01 xác nhận Stopped/InstanceInitiatedShutdown. Giữ nguyên lifecycle stoppedConfirmed=false của lượt cũ và thêm bằng chứng xác nhận sau, không sửa lịch sử.
29. Bổ sung giữ PC khỏi tự idle Sleep trong thời gian test, trả lại khi xong; không chặn Sleep thủ công hoặc đổi mạng. Lượt chạy lại dùng đúng provenance/image đang cài, không upload/deploy/tải 50–100 lần nữa; ngân sách mềm 18 phút, timer độc lập cứng 20 phút để giảm rủi ro tiêu credits khi mất PC. Kiểm Stop–Start yêu cầu đăng ký lại fixture trả 409 và login được, tránh nhầm việc tạo account mới thành DB đã được giữ.
30. Lượt tiếp theo bị hủy ở UAC sửa hosts, chưa phát tải; finally Stop xác nhận. Thay yêu cầu quyền Administrator bằng pin TCP riêng trong client (webSocketConnectIp/NET_WS_CONNECT_IP): SocketsHttpHandler nối tới IPv4 được chỉ định, URI/SNI và certificate chain/hostname vẫn mặc định. Có test TLS không tin cậy bị từ chối. Chạy lại không UAC, không sửa DNS/hosts, không deploy lại; config/report ghi rõ IP pin. Public DNS được hỏi riêng từ EC2; resolver/hosts PC vẫn là hạn chế riêng, không hứa tester ở mạng chặn DNS tự kết nối bằng hostname được.


## 3. Môi trường và cách tính

### Môi trường Phase2

- Source được nghiệm thu: `6a854d2e58a24c4b6536a3cb69dbef17de557c60`, nhánh feature; target .NET8, SDK local `10.0.401`, Unity6000.3.15f1/URP, `Microsoft Windows 11 Home Single Language`.
- CPU: AMD Ryzen 7 4800H with Radeon Graphics         . Local Docker riêng: game256MiB, API384MiB, Mongo fixture có volume riêng. Cổng host14777/14781/14500; production local mặc định vẫn7777/7781/5000. Control chỉ trong network.
- Profile main: min6/max10, lobby15s/loading30s/grace15s/ending10s. Profile test: min2, timer/bo tăng tốc; các kết quả demo không chứng minh thời lượng main theo thời gian thực.
- Docker smoke10 phiên đo input/snapshot trong5s rồi đợi kết quả; không phải bài tìm sức chứa cuối hoặc benchmark combat.


### Môi trường AWS tại lượt cũ

| Thành phần | Cấu hình thực tế |
| --- | --- |
| EC2 | t3.small, ap-southeast-2, 2 vCPU, CPU credits Standard |
| OS | Linux 7.0.0-1014-aws, x86_64, RAM OS thấy 1906.0MiB |
| Storage | gp3 20GiB mã hóa; trước deploy còn khoảng 11GB filesystem |
| GameServer | 256MiB, tick 30Hz, RoomCode public; 10/50/5/2 |
| Generator | Một process trong container EC2, 256MiB/0.75 core, UDP loopback qua host 7777, dùng chung tài nguyên với server |
| API/control | HTTPS + JWT qua SSM; HTTP control private giữa API/GameServer |
| Release | aws-rooms-20261016 |
| Archive SHA-256 | 51f3cb8a571b2315076d18559e79c9ce8c410d54630ae92ebf1b654c0c6f3b5a |
| GameServer image | sha256:edd96d3c3b0c44b66cb3efb95890bdd80bcdf63200abd6e28e77a6f9552004b5 |
| Baseline p95 | Lượt chức năng 30.3493ms; lượt bổ sung 30.5476ms |

Baseline: 1 client, warm-up 5s, đo 30s. Đây là baseline trong EC2. Một nhãn console cũ còn ghi “Internet baseline”; metadata generatorOnServer=true và topology thực tế xác định đúng phạm vi, không phải RTT Việt Nam–Sydney.

Tải 10/30/50: warm-up 10s, đo 45/45/60s; xác nhận warm-up 10s + đo 180s. Mức 10 và 30/50 thuộc **hai lần boot riêng**, không ghép thành chuỗi không restart. Đã đo baseline riêng và đối chiếu image; mỗi lượt vẫn login/tạo phòng bằng JWT.

Tiêu chí: đủ join, không disconnect ngoài dự kiến; timeout <1%; p95 **từng phòng** ≤ baseline +50ms; tick 29–31Hz, không dropped tick/OOM/restart; CPU server ≤85% một core; RAM cgroup ≤80% của 256MiB. Từng client phải phát ≥28.5Hz, nếu không là INVALID_GENERATOR. CPU là peak cửa sổ metrics 5s, RAM cgroup lấy khoảng 1s. Chỉ dùng cửa sổ metrics nằm trọn thời gian đo; RTT nearest-rank, loại warm-up và đợi pong cuối.

Generator dùng chung EC2 có thể cạnh tranh CPU/RAM; không so thẳng với phát tải từ máy độc lập.

### Bổ sung UDP/WSS ngày 16/10/2026

Local: Windows/Docker Desktop, GameServer 256MiB, .NET 8/LiteNetLib 2.1.4. AWS giữ t3.small/CPU Standard/Mongo 7/Caddy volumes, không thêm instance/LB. Client AWS chạy trên PC vật lý; WSS dùng TLS public hostname, không đi trong tunnel SSM. RTT Local xét ≤50ms p95; RTT Sydney/WSS chỉ báo thực đo. Các tiêu chí khác: đủ join, không disconnect ngoài dự kiến, pong timeout <1%, từng client ≥28,5Hz, tick 29–31Hz, không drop/OOM/restart, CPU ≤85% một core, RAM ≤80% giới hạn. Full per-client samples và image/source hash nằm trong JSON/archive.


## 4. Các lượt đo và kết quả

### Kết quả Phase2 mới nhất

| Hạng mục | Trạng thái | Bằng chứng |
| --- | --- | --- |
| Runtime/movement/zone | PASS84 | backend-integration.log |
| Outbox/ticket unit | PASS52 | backend-integration.log |
| Hồi quy UDP/JWT/phòng | PASS132 | backend-integration.log |
| Dual transport/Auto/TLS rejection | PASS44 | merged-dual.log |
| Socket gameplay UDP, WebSocket, mixed | PASS8 mỗi lượt | merged-gameplay-*.json |
| HTTP/Mongo thật: JWT thiếu/sai/hết hạn, owner/quota/rate, Guest/account ticket/history, idempotence/conflict | PASS33 | api-integration.json |
| Docker10 phiên gameplay, chặn phiên11, input≥28,5Hz/client và snapshot coherent | PASS5 | docker-gameplay-Mixed.json |
| Docker HTTP/restart | PASS8 +3 | docker-http.json, docker-restart.json |
| DB outage/recovery | PASS:503 rồi200, dữ liệu còn | db-restart.json |
| HTTPS Caddy: health, internal block, Swagger auth, exact game route | PASS6 | caddy-checks.json |
| Unity EditMode/PlayMode | PASS 137/5 | unity-editmode.json, unity-playmode.json |
| Windows build | PASS, 0 error/0 warning | unity-build.json |
| Hai Unity player: view/move/bo/HP/result/replay/Guest/history | PASS cả hai;900 snapshot/client; vòng2 | accepted-A/B.json và ảnh |
| Hai PC vật lý / AWS Phase2 /50 phiên gameplay | NOT_TESTED | Không có lượt tương ứng |

Lượt history FAIL trước sửa được giữ trong final-A/B.json; kết luận dùng accepted-A/B.json sau sửa. Không đổi một lượt lỗi thành PASS.


### Chức năng/JWT AWS

| Nội dung | Kết quả |
| --- | --- |
| JWT thiếu / sai định dạng / sửa chữ ký | POST trả 401, không tạo phòng |
| JWT thật lifetime 1 phút | Tạo được trước expiry; exp +35s trả 401, không tạo phòng |
| Khôi phục | Token cấp lại 60 phút, giữ signing key |
| Owner | Lấy từ sub, bỏ qua ownerId giả; user khác không thấy phòng/mã |
| Xóa | Chủ xóa phòng trống 204; khác chủ 403; mặc định/đang có người 409 |
| Sức chứa | Max 10; đề nghị 11 trả 400; client thứ 11 bị RoomFull |
| Quota | 2 phòng thêm/user; tổng 5 gồm mặc định; phòng thứ 6 bị RoomLimitReached |
| Rate limit | Request thứ 6/phút/user trả 429 |
| Cấu hình | RoomCode public, 7780 private, 256MiB, policy 10/50/5/2 |
| Lượt bổ sung | Tạo đủ 4 phòng bằng JWT hợp lệ, kiểm đúng image và policy |

TTL 10 phút, JWT pending và ServerFull khi tổng server thấp hơn tổng slot phòng được kiểm local; không chờ 10 phút trên AWS. Các client thứ 11 bị từ chối có chủ ý nằm trong file riêng, không tính là lỗi performance.

### Performance trong EC2

| Lượt | Phòng hoạt động | Joined | Đo s | Kết quả | RTT p95/p99 ms | p95 phòng chậm nhất | Hz/client thấp nhất | Tick Hz | CPU peak % core | RAM peak % |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| load-10 | 1 | 10/10 | 45 | PASS | 30.28/34.63 | 30.28 | 30.00 | 29.99 | 6.20 | 38.75 |
| load-30 | 3 | 30/30 | 45 | PASS | 30.53/36.07 | 30.67 | 30.00 | 30.02 | 7.80 | 37.88 |
| load-50 | 5 | 50/50 | 60 | PASS | 31.70/36.59 | 31.85 | 30.03 | 30.01 | 8.40 | 38.33 |
| confirm-50 | 5 | 50/50 | 180 | PASS | 30.86/39.67 | 30.96 | 30.00 | 30.00 | 10.97 | 39.33 |

Tổng cửa sổ performance: **414,107 ping**, 0 mất/timeout, 0 dropped tick. Peak CPU 10.97% một core; RAM 39.33% = 100.69MiB; client chậm nhất 30.00Hz.

**5 phòng ×10 đạt lượt xác nhận 180s**, không OOM/restart trong các cửa sổ đánh giá. P95 được xét từng phòng, không chỉ toàn bộ sample gộp.

### Đối chiếu local 14/10

Giữ 22 lượt, 4.319.700 ping/pong, không loss/drop. Source snapshot local nới policy để đo: một phòng 50, năm phòng ×10 và sáu phòng ×10 đạt 180s; bảy phòng ×10 trượt p95 một phòng 55,71ms; một phòng 95/chín phòng ×10 cũng trượt lượt dài. Đây là số local cũ, không phải cấu hình/trần EC2. API hiện tại chỉ cho 10 người/phòng.

### Bài ổn định Local Phase 1 mới nhất

Stack Docker/DB/secret riêng, không dùng DB dev hoặc AWS. Một phòng 10 phiên từ PC, warm-up 15s, đo **300s**, 30 ping/s/phiên; đây là 10 socket trên một PC, không phải 10 người/máy.

| Chỉ số | Kết quả |
| --- | --- |
| Joined / lỗi client | 10/10; 0 lỗi |
| Ping/pong / timeout | 90.000 /90.000; 0 mất/timeout |
| Tốc độ từng client thấp nhất | 29,9996Hz, đạt ≥28,5Hz |
| RTT p50 /p95 /p99 | 30,7685 /46,8849 /60,4903ms |
| Tick trung bình /dropped tick | 30,0029Hz /0 |
| CPU peak một core /RAM cgroup | 7,0% /14,57% của 256MiB (~37,29MiB) |
| Khởi động lại và dữ liệu account | Down/up giữ volume, login lại được |
| Kết quả | PASS theo tiêu chí Local; không suy ra FPS/gameplay |

Bài Local ban đầu lỗi ID container trong công cụ, chưa có stage 300s; giữ riêng trong JSON và archive. Không gộp thành một lượt PASS không gián đoạn.

### Đường thực PC → EC2 và trạng thái bốn task

Bài UDP cuối khoảng **13:28 ngày 16/10 (UTC+7)** dùng PC DESKTOP-JDQDIQH → 16.176.205.217:7777, không phát tải trong EC2. IP là địa chỉ tại lượt đó, **không dùng lại sau Stop–Start**. Observer được arm trước khi chạy client; đồng hồ UTC hai host có độ lệch nhỏ, vì vậy đối chiếu cả thứ tự chạy/cửa sổ chồng lấp, không dùng timestamp từ hai máy để suy ra độ trễ một chiều.

| Phần kiểm tra | Trạng thái / bằng chứng |
| --- | --- |
| Public UDP từ PC | **BLOCKED**: transportSent=10, received=0, ConnectTimeout; observer ens5 incoming/outgoing=0 trong cửa sổ 12s |
| Bind/Docker | UDP 0.0.0.0:7777, docker-proxy và publish đúng; DOCKER-USER không có rule chặn bổ sung; kiểm lại SG mở UDP 7777, NACL allow và route Internet Gateway khi EC2 đã Stopped |
| Tailscale | **BLOCKED** ở PC: DNS chung 94.140.14.33 và lỗi HTTPS; chưa có direct/DERP để đo |
| API HTTPS/JWT AWS | **PASS 8/8** qua quản trị SSM/TLS đúng certificate, không phải bằng chứng UDP public |
| INF-01 | Local setup/restart/data/env PASS; hai PC LAN/VPN, chạy đồng thời Local/AWS và Stop–Start rejoin còn NOT_TESTED |
| NET-01 | API/JWT/tạo phòng PASS; JWT thật hết hạn đã kiểm trong lượt AWS trước và suite Local hiện tại, không đổi TTL AWS lần này; không mở lại UI Compass |
| SRV-01 | Local 10/300s PASS; AWS co-located 5×10/180s cũ PASS; PC→AWS 10/300s **NOT_TESTED** |
| SRV-02 | Local version/payload/mã sai/capacity/pending/timeout/trả slot PASS; handshake/reconnect qua WAN/VPN **NOT_TESTED** |
| INF-02 /Unity | 10 máy/người và gameplay **NOT_TESTED**, chưa có client Unity LiteNetLib |

Máy đang dùng không chuyển mạng theo lựa chọn của người dùng, nên dừng ở bằng chứng BLOCKED, không chạy lại generator trong EC2 để thay thế. Chưa đủ cơ sở đánh Done toàn bộ Phase 1 cho cả Local và Online.

### Bổ sung UDP/WSS ngày 16/10/2026

| Môi trường / transport | Phiên / giây | Kết quả | Hz thấp nhất | RTT p50/p95/p99 (ms) | Tick Hz | CPU peak / RAM peak |
| --- | --- | --- | --- | --- | --- | --- |
| Local / Udp | 10 / 300 | PASS | 30.00 | 30.73/46.92/60.35 | 30.00 | 11.00% / 13.66% |
| Local / Wss | 10 / 300 | PASS | 29.99 | 15.38/16.17/31.31 | 30.00 | 16.91% / 19.07% |
| PC → EC2 / Wss | 10 / 300 | PASS | 30.00 | 404.06/591.35/774.27 | 30.00 | 9.40% / 42.43% |

| Hạng mục | Trạng thái |
| --- | --- |
| localUdpV1 | PASS |
| localUdpV2 | PASS |
| localWebSocket | PASS |
| localAutoBlockedUdp | PASS |
| mixedRoomCapacity | PASS |
| jwtRoomOwnershipQuotaRateLimit | PASS |
| localAccountPersistsRestart | PASS |
| publicWss | PASS |
| publicAuto | PASS |
| publicUdp | PASS |
| localAwsConcurrent | PASS |
| awsStopStartRejoin | PASS |
| twoPhysicalPcs | NOT_TESTED |
| unityMultiplayerGameplay | NOT_TESTED |
| temporaryFileCleanup | BLOCKED |
| ec2FinalStopped | PASS |
| publicDnsFromEc2 | PASS |
| pcSystemHostname | BLOCKED |
| pinnedTlsRejectsUntrustedCertificate | PASS |

Auto thực tế: {"transport": "Udp", "fallbackReason": null}.

Lỗi harness được giữ: Process failed: sftp (1).

Lỗi harness được giữ: Process failed: sftp (-1).

Lỗi harness được giữ: AWS test incomplete; inspect test/report.json..

Lỗi harness được giữ: This command cannot be run due to the error: The operation was canceled by the user..


## 5. Diễn giải và giới hạn

### Giới hạn Phase2

Đây là gameplay tối thiểu có movement/collision và bo, chưa có combat vũ khí/inventory/hazard/FOW đầy đủ; kills bằng0. Unity chỉ nghiệm thu UDP; WSS/Auto/prediction/reconciliation/interpolation đầy đủ còn thuộc MOV-02/03. Chưa có killcam UI-03 hoặc toàn bộ UI-02. Hai process cùng Windows có thể dùng chung Guest DPAPI; kiểm hai Guest cùng tên nhưng credential khác dùng bài HTTP riêng. Chưa kết luận sức chứa gameplay50 người, độ mượt WAN hoặc độ trễ Sydney từ lượt Local này.


### Giới hạn của bài ping/pong cũ

Đã xác nhận **ít nhất 50 phiên mạng, chia 5 phòng**, trên t3.small trong bài ping/pong 30Hz co-located. Không thử cao hơn, không tìm giới hạn cuối và không diễn giải thành 50 người chơi Unity mượt.

Đường quản trị SSM/HTTPS API hoạt động; UDP public từ PC chưa thông. Chạy generator trong EC2 kiểm được backend khi mạng chặn, không sửa kết nối game từ lớp. Cần mạng cho UDP hoặc thiết kế transport/overlay riêng để nghiệm thu chơi online từ PC.

Tick chưa mô phỏng world/combat/snapshot. Một máy phát tải không thay cho nhiều máy vật lý, FPS, WAN loss, reconnect hoặc nhiều trận gameplay độc lập. Phải đo lại khi có movement/combat và Unity client.

Lỗi cleanup ở lượt security là lỗi công cụ, không phải server FAIL tải. Dữ liệu gốc giữ trạng thái incomplete; lượt bổ sung tham chiếu hash, chạy lại baseline, cleanup cuối không lỗi. Không sửa kết quả cũ thành PASS hoàn chỉnh.

### Bổ sung UDP/WSS ngày 16/10/2026

WSS là đường TCP/TLS có thứ tự; mất gói có thể gây chờ các message sau, nên bài ping/pong đạt không chứng minh movement/combat mượt. Lượt WAN đủ 300s có RTT p50 khoảng 404ms, p95 khoảng 591ms, p99 khoảng 774ms: kết nối ổn định nhưng độ trễ này có thể thấy rõ trong game real-time. Không tuyên bố đã chơi mượt; cần client Unity, prediction/interpolation và đo gameplay/mạng thực tiếp theo. Chưa đo 50 phiên WSS và chưa nghiệm thu hai PC vật lý/10 người thật. Account identity JWT dùng quản lý phòng; RoomCode join nhận PlayerId phiên, không gắn trực tiếp account. Nếu mạng chặn cả HTTPS/Upgrade tới IP thì kết nối vẫn BLOCKED. Đợt cuối pin TCP riêng trong client, không sửa hosts; DNS/hosts PC vẫn cần xử lý riêng khi dùng trình duyệt/hostname mặc định.


## 6. Phương án chọn và lý do

### Kết luận phương án Phase2

**Chọn giữ10 người/phòng,5 phòng/tổng50 slot; đưa SRV-03, NET-02, ZON-01 sang tích hợp Local với demo UDP.** Lý do: đúng phạm vi lớp học, backend và Unity đã hoàn tất vòng trận với Guest/history, có test slot/transport/persistence, giữ các contract để dev combat/UI tiếp tục. Đây là hạn mức vận hành, không phải công bố benchmark tối đa mới.

Đợt này hoàn thành trên Local; không tự nâng cấu hình hoặc deploy AWS. Trước nghiệm thu chơi online cần release riêng và hai PC thật; sau khi có combat/snapshot phức tạp phải đo lại hiệu năng.


### Quyết định trước Phase 2

Giữ **10 người/phòng, 5 phòng gồm mặc định, tổng 50 active/pending; mỗi user tối đa 2 phòng thêm**, mã tạo thêm 6 ký tự. Chưa thay đổi chính sách sau bài kết nối mới; việc còn thiếu là đường mạng PC→EC2 và nghiệm thu hai PC thật. Tạo/list/xóa cần login và JWT hợp lệ; join UDP public vẫn RoomCode.

Phù hợp lớp khoảng 10 người và cho các nhóm/dev test song song; local và phép đo backend EC2 đã có bằng chứng 5×10. Không tăng lên 6 phòng/60 từ source snapshot local vì chưa đo AWS tương ứng và gameplay sẽ phát sinh tải mới. 50 là hạn mức backend chọn hiện tại, không phải lời hứa sức chứa gameplay.

Khi test gameplay đầu tiên, bắt đầu 1 phòng/10 máy thật trên mạng UDP hoạt động; đo movement/combat/snapshot/FPS/RTT WAN rồi mới nghiệm thu nhiều phòng. Không tự nâng instance/Unlimited để làm bài đạt.

### Bổ sung UDP/WSS ngày 16/10/2026

Chọn **10 người/phòng, 5 phòng gồm mặc định, tổng 50 active/pending, 2 phòng thêm/user**, dùng **UDP v2 chính + WSS dự phòng Auto**. Lý do: giữ tương thích UDP v1, không cần tester cài VPN, dùng chung slot nên fallback không vượt sức chứa; JWT bảo vệ quản lý phòng. Giữ giới hạn vận hành hiện tại; chỉ tuyên bố mức/transport đã đo trong bảng, không suy ra 50 người chơi gameplay. Admin mở buổi có thời lượng bằng open-aws-session.ps1; tester chỉ nhận client, endpoint và mã phòng.


## 7. Bằng chứng, chi phí và chạy lại

### Bằng chứng Phase2 và bàn giao

- [Archive](phase2-evidence.zip), [SHA256](phase2-evidence.zip.sha256), [CSV kiểm tra](phase2-checks.csv); metadata cũng nằm trong report.json → phase2. Archive đã kiểm CRC; SHA256 `8c6a937defa0453a17cfd867b63ccee385340687a92f889c1f9aa277e5ac11ad`.
- [Hướng dẫn chung mục17](../../Phase1_First4Tasks.vi.md#17-phase-2-chạy-trận-guest-safe-zone-và-lịch-sử), [Network Architecture](../../Network_Architecture.vi.md), [Client guide](../../Client_Developer_Guide.vi.md).
- Giữ một Windows build mới nhất tại `.utmp/phase2/build/Lastaway.exe`. Khi chạy thủ công chọn profile phase2-local và RoomCode của môi trường; không bật cờ phase2Smoke khi chơi bình thường.
- Không gọi AWS/Start EC2/deploy trong lượt này. EBS của môi trường AWS vẫn có thể phát sinh chi phí riêng. Tài nguyên Docker fixture được đóng sau khi thu bằng chứng; không xóa volume/dev container có sẵn.
- Bộ kiểm tra/code đã commit trên feature; không push hoặc merge main. Archive lịch sử cũ giữ nguyên.
- Dọn hai worktree đã tích hợp bằng công cụ orchestrator, thu hồi **1.136.711.793 byte (~1,06 GiB)**. Giữ bản Windows mới nhất và các volume. Lệnh dọn thêm staging/file fixture trong `.utmp/phase2` bị automatic approval review từ chối với “blocked by policy”; nhóm này được giữ nguyên, không dùng cách xóa khác. Xem cleanup-final.json trong archive.


### Toàn bộ các lần bật có kiểm soát

| Lượt | Start request UTC+7 | Stop request UTC+7 | Kết quả | Xác nhận |
| --- | --- | --- | --- | --- |
| lượt | 11:05:18 | 11:10:35 | Timed out: scp | Stopped |
| lượt-final | 11:12:34 | 11:13:08 | Process failed: ssh () | Stopped |
| lượt-run | 11:14:25 | 11:18:58 | AWS test incomplete; inspect test/report.json. | Stopped |
| lượt-ssm | 11:22:47 | 11:29:31 | AWS test incomplete; inspect test/report.json. | Stopped |
| lượt-capacity | 11:31:18 | 11:39:57 | Hoàn tất | Stopped |

Có thêm preflight lỗi ACL Windows trước Start, không bật EC2. Các sửa lỗi: upload timeout → SFTP resume; Docker remote cần sudo/mã thoát Windows; biến http của harness; record cleanup JWT ngắn. Các lượt lỗi đều Stop, không Terminate.

Tổng từ Start request tới Stop request của **5 lần bật: 25.77 phút**. Khoảng này vượt mốc mục tiêu 25 phút khoảng 46 giây do các lượt sửa lỗi. Đây là thời gian vòng vận hành có phần pending, không phải Running chốt hóa đơn. Usage của EC2 đã bật trước lúc chuẩn bị không nằm trong số này; bảng không loại các lần thất bại.

Compute = giờ Running AWS ghi nhận × đơn giá t3.small Sydney của tài khoản; credits còn phụ thuộc ưu đãi/usage ledger. Không suy ra 0đ hoặc số credits còn lại từ test. AWS tính theo giây, tối thiểu 60s/lần Start; compute ngừng khi Stopping thường, EBS vẫn tính lưu trữ. [AWS lifecycle](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/ec2-instance-lifecycle.html).

IPv4 public niêm yết USD 0.005/giờ; riêng thành phần này theo khoảng vận hành trên xấp xỉ **USD 0.0021**, chưa gồm compute/EBS/transfer, chưa trừ ưu đãi. [AWS VPC pricing](https://aws.amazon.com/vpc/pricing/). CPU Standard không dùng dư Unlimited; CPU credits khác promotional credits. [AWS Standard mode](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/burstable-performance-instances-standard-mode.html).

Lượt cuối: client đóng, phòng test xóa, collector dừng, cleanupErrors rỗng, API cấp token 60 phút. Giữ 3 fixture account để tái sử dụng; mật khẩu ở file ngoài repo, DACL chỉ user Windows hiện tại. EC2 **Stopped đã xác nhận**; EBS/Mongo/Caddy data và cấu hình được giữ. Timer 30 phút dành cho harness; bật EC2 thông thường vẫn cần admin tự Stop.

### Các lượt bổ sung PC → EC2 và chi phí

| Lượt | Tiếp quản/Start UTC+7 | Stop request UTC+7 | Kết quả | Xác nhận |
| --- | --- | --- | --- | --- |
| aws — tiếp quản Running sau xác nhận | 13:21:30 | 13:22:15 | Tunnel SSM thoát trước UDP | Stopped 13:22:43 |
| aws-rerun | 13:24:18 | 13:25:17 | HTTPS/JWT qua, Bash stdin CRLF lỗi | Stopped 13:25:46 |
| aws-probe | 13:27:22 | 13:28:42 | API 8/8; UDP trực tiếp BLOCKED; VPN prerequisites BLOCKED | Stopped 13:29:21 |

Tổng khoảng **tiếp quản hoặc Start request → Stop request** của ba lượt mới: **3,06 phút**; chưa cộng vào bảng năm lượt trước thành một lượt chạy liên tục. Lượt tiếp quản đã được bật từ 13:16:29, khoảng **5,03 phút trước khi công cụ nhận quyền quản lý**; khoảng Running đó vẫn có usage, không được coi là miễn phí. Đây là timeline vận hành, không phải ledger Running AWS. Không tạo thêm EC2/resize/deploy; không chạy tải WAN không thể tới được hoặc lặp 50–100 phiên.

Local stack/volume test đã được dọn, Docker không còn container test chạy. Phòng AWS mới tạo đã xóa, client/SSH tunnel/watchdog PC của bài đã đóng, EC2 **Stopped xác nhận lại**. EBS/data vẫn được giữ và có thể tiếp tục tiêu credits lưu trữ. Đây là kết luận lịch sử của lượt UDP-only; hướng hiện tại dùng WSS Auto ở phần bổ sung, Tailscale không bắt buộc. Hai PC thật vẫn cần riêng cho nghiệm thu LAN/VPN.

### File và truy vết

- [report.json](report.json): local cũ, AWS security gốc, stage/lỗi/dọn dẹp, lượt bổ sung, source/image fingerprint và lifecycle.
- [report.csv](report.csv): giữ 22 dòng Local cũ +4 AWS co-located; thêm Local 10/300s và probe PC→EC2 BLOCKED, có environment để tránh diễn giải lẫn nhau.
- [evidence.zip](evidence.zip): archive mới chứa dữ liệu còn phục hồi của lượt AWS cũ, Local/PC→EC2 mới, các lượt lỗi, timeline đã bỏ account ID và source công cụ; không kèm image/release archive hoặc credentials.
- Archive trước đợt UDP/WSS có **94 file**; archive hiện tại có **287 file**, SHA-256 ở [evidence.zip.sha256](evidence.zip.sha256), metadata trong report.json. Kiểm CRC và SHA-256 từng file theo manifest trong archive.
- Khi rà soát lượt mới, cả evidence.zip và evidence.zip.sha256 đã mô tả ở lần trước đều không có trên đĩa. Giữ metadata/hash lịch sử trong priorEvidenceArchive20261016; dựng archive mới từ raw còn trong Backend/out và số liệu JSON/CSV còn giữ. Không tuyên bố phục hồi byte-identical hoặc kiểm lại CRC archive cũ không có; raw local 14/10 cũng chỉ còn phần đã lưu trong báo cáo.
- Không có JWT/password/room plan/.env/PEM trong gói; đã so với mật khẩu fixture thật. Bỏ AWS account ID khỏi lifecycle public. Lượt bổ sung tham chiếu đúng hash bằng chứng trước.

Chạy lại theo [mục 14.4 hướng dẫn](../../Phase1_First4Tasks.vi.md) và [Network Architecture](../../Network_Architecture.vi.md). GeneratorOnServer dùng SSM/co-located khi mạng chặn UDP; ReuseDeployedRelease chỉ khi manifest trùng; CapacityAfter không dùng cho bài bảo mật FAIL. Chuẩn bị build/test/client trước Start và xác nhận Stopped cuối buổi.

Hướng dẫn mới: [mục 15 Phase 1](../../Phase1_First4Tasks.vi.md#15-nghiệm-thu-phase-1-local-và-pc--ec2). Gói chuyển PC thứ hai: `Backend/out/phase1-20261016/client-win.zip` (không chứa room code, cần .NET 8 Runtime).

### Bổ sung UDP/WSS ngày 16/10/2026

| Lượt | Start request UTC+7 | Stop request UTC+7 | Stopped xác nhận |
| --- | --- | --- | --- |
| WSS 1 | 14:44:46 | 14:46:02 | True |
| WSS 2 | 14:47:05 | 14:53:24 | True |
| WSS 3 | 15:01:14 | 15:11:13 | False |
| WSS 4 | 16:04:05 | 16:06:42 | True |
| WSS 5 | 16:10:39 | 16:20:35 | True |

Các lượt đầu dùng watchdog/timer 30 phút; lượt cuối giảm còn 20 phút cứng, 18 phút mềm. Mục tiêu hoàn tất toàn đợt trong một cửa sổ 20 phút đã không đạt vì upload chậm, gián đoạn PC/mạng và UAC. Không gộp nhiều lần Start thành một lượt 20 phút. Start request → Stop request không phải hóa đơn chính xác; riêng lượt mất đường quản trị, Stop request không chứng minh máy đã dừng, phải xem xác nhận phục hồi/timer. EBS còn usage sau Stop. Không resize, không T3 Unlimited, không tạo LB/EC2 mới, không chạy lại 50–100 phiên.

Gói client mới nhất: `Backend/out/wss-20261016/NetSmokeClient-latest-win-x64.zip`, Windows x64 cần .NET 8 Runtime, có profile Auto; không chứa code phòng/credentials. Release/checksum giữ tại `Backend/out/wss-20261016/release/` cho triển khai/rollback. Hướng dẫn chuẩn mới ở mục 16 Phase1_First4Tasks.vi.md và Network Architecture.

Archive bằng chứng giữ các file cũ, thêm raw Local/WSS/AWS/lượt lỗi, timeline bỏ account ID, source và checks. Không kèm images.tar.gz/.env/PEM/password/JWT. Manifest/checksum/CRC được kiểm lại. Cleanup nhóm file tạm cũ hiện BLOCKED: giữ nguyên 814,27MiB, nhóm này xóa 0B. Đã xóa riêng bản images.tar.gz dựng lại trùng của đợt mới: 99.965.719 byte (~95,33MiB), sau khi kiểm đường dẫn tuyệt đối, giữ gói release gốc. Docker prune cũ hơn bảy ngày thu hồi 0B, dev/DB/rollback được giữ.

### Bổ sung nghiệm thu nền tảng Unity — 23/10/2026

Hoàn thiện CLI-02/CLI-03/CLI-04/MOV-01/ENV-01 trong phạm vi serializer, base pattern, config, movement local và map/collision. Unity 6000.3.15f1: **125/125 EditMode + 5/5 PlayMode PASS**, gồm serializer 0 GC sau warm-up, thay JSON ảnh hưởng đạn thử, collider config sau Awake, Hero đi chéo 4 cell/s, aim khi lệch tâm camera, map 50×50/48 PPU, bốn biên và cả bốn Hard Cover chặn Hero/đạn quét nhanh. Có kiểm camera đứng yên/clamp và tái sử dụng projectile trong pool.

Đã join GameServer thật trên loopback bằng endpoint tạm trong RAM, quan sát Game View và bắn trúng cover; không phát sinh lỗi Console. Không dùng kết quả này thay phép đo AWS/WSS/capacity ở trên; không bật/deploy/chạy tải EC2. Server local thử đã dừng. Trải nghiệm giữ WASD kéo dài bằng thao tác tay chưa xác nhận riêng do công cụ chỉ gửi lần nhấn ngắn; chuyển động được đo bằng Unity Physics2D qua cùng kinematics của controller.

Chi tiết, cách chạy lại và ranh giới MOV-02/combat/FOW: [Unity Foundation Acceptance](../../Unity_Foundation_Acceptance.vi.md). Dữ liệu mới: [unity-foundation-tests.json](unity-foundation-tests.json), nằm **ngoài** archive bằng chứng backend cũ; không đổi checksum hay nhãn các kết quả cũ. Map hiện là layout nghiệm thu bốn loại cover, chưa phải mê cung hoàn chỉnh/art cuối. Đạn/VFX thử chạy local, không gây damage hoặc đồng bộ nhiều người.
