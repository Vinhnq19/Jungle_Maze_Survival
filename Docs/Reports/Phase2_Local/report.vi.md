> Tiến độ sẵn sàng online (25/10/2026): **95% tổng thể; code và nghiệm thu một PC đã hoàn tất**. Local UDP/WebSocket/Auto, AWS6 client/300s, fallback WSS, JWT và Stop–Start đều đạt; EC2 đã Stopped. 5% còn lại là nghiệm thu hai PC và tuyến VPN/public tự host thực tế, có checklist riêng. Phần trăm là tiến độ công việc, không phải điểm hiệu năng hoặc bảo đảm game hoàn chỉnh. Kết quả Local24/10 được giữ làm lịch sử.

# Báo cáo Phase 2 — Local24/10 và mở rộng online25/10/2026

Các đoạn “lượt Local24/10” và số liệu cũ dưới đây là mốc lịch sử; phần bổ sung25/10 ghi riêng kết quả của bản transport/test online mới.

**Mốc24/10 — SRV-03, NET-02, ZON-01 và demo Unity UDP: PASS trong phạm vi Local.** Khi đó hai Windows player trên cùng PC hoàn tất trận, kết quả, lịch sử Guest và chơi lại; AWS Phase2 chưa kiểm. Lượt25/10 bổ sung AWS/UDP/WSS/Auto như bảng mới ở mục4. Hai PC vật lý và tải50 phiên gameplay vẫn **NOT_TESTED**.

[Hướng dẫn chạy và test](../../Phase2_Match_Guest_Zone.vi.md). Các lượt ping/pong và AWS Phase 1 giữ tại [báo cáo lịch sử](../Local_Capacity_Retest_2026-10-14/report.vi.md).

## 1. Mục đích và phạm vi

Mốc24/10 triển khai SRV-03, NET-02, ZON-01 và demo Unity UDP trên Local. Lượt25/10 bổ sung Unity WSS/Auto, bộ test dùng lại cho feature mới, cấu hình tự host và nghiệm thu từ một PC tới EC2. Hai Windows player Local hoàn tất trận; AWS dùng sáu Windows player thật chạy300s và bài Stop–Start riêng. Số liệu ping/co-located Phase1 vẫn nằm trong báo cáo lịch sử, không thay cho bằng chứng gameplay online mới.

## 2. Những việc thực hiện trước khi kết luận

**Mở rộng online25/10, sau mốc Local24/10:**

- Giữ nhánh và toàn bộ thay đổi có sẵn. Antigravity Flash High/effort high làm transport và scaffold host/test trong workspace được chia phạm vi. Codex review, sửa sâu phần queue, graceful quit, DNS generation, schema/assertion, cleanup và guard AWS; sau reboot không mở worker trùng. Hai vòng sửa đã dùng hết, phần còn lại do Codex hoàn thiện.
- Unity dùng chung handshake/gameplay cho UDP và WebSocket binary; Auto chỉ fallback trước join, không vượt lỗi nghiệp vụ. Thêm chọn transport trong config/env/CLI, thống kê tuyến/RTT và profile Local/LAN/VPN/selfhost/AWS; HTTPS bắt buộc cho credential từ xa.
- Sửa resume có UDP đã được nhận nhưng mất ACK trước fallback: cùng attempt tiếp tục trên WSS, giữ một slot/PlayerId và chỉ xoay proof một lần; callback UDP cũ không thu hồi slot mới. Thêm test chống takeover lần hai và WSS explicit quit.
- Làm runner scenario JSON có version, assertion/driver mở rộng, bằng chứng từ Windows player thật và ảnh. Thiếu tốc độ phát là INVALID, thiếu assertion/khác scenario/crash không được PASS. Fingerprint bao gồm toàn bộ data/runtime build, dùng thứ tự ordinal giống nhau trên PowerShell5/7.
- Tạo compose.host, cấu hình secret riêng, Caddy public/VPN và preflight render env thật; firewall helper chỉ mở đúng cổng. Chưa đăng nhập/cấu hình VPN/router thay người vận hành. Có checklist hai PC và cách thêm test combat/inventory sau này.
- AWS dùng release checksum, giữ Mongo/Caddy/volume/key; bổ sung secret kết quả theo cách không ghi đè. Có bộ thu RAM giới hạn thời gian, kiểm tick/CPU/OOM/restart, watchdog độc lập, giữ PC không tự Sleep và Stop trong finally.
- Lượt AWS đầu triển khai healthy nhưng test bị BLOCKED vì hosts còn ghim IP cũ. Đã Stop, sao lưu hosts và xóa đúng dòng JungleMaze. Lượt sau verify release đã cài để bỏ upload/deploy; normal DNS phải khớp Public IPv4 trước khi chạy Unity. Giữ bằng chứng lượt lỗi, không sửa thành PASS.

Các bước1–12 dưới đây mô tả lượt Local24/10 trước khi có bản mở rộng:

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

## 3. Môi trường và cách tính

**Bổ sung25/10:** baseline `f7be6c1e9b412d5996ee0ef97df35138e8fa766e` cộng thay đổi chưa commit trên cùng feature branch. Codex tiếp quản phần việc Antigravity bị ngắt do reboot sau hai vòng sửa; không coi trạng thái worker là nghiệm thu. GameServer .NET8, Unity6000.3.15f1, một PC Windows; fixture Docker riêng game256MiB/API384MiB/Mongo512MiB, cổng19777/19781/15939. Mã/secret fixture không đưa vào bằng chứng.

Rehearsal main giữ min6/lobby15s và dữ liệu thật. Bài Local2 dùng profile test tăng tốc để xem trọn vòng trận/history/replay. Hai loại bài có mục đích khác nhau, không dùng Local2 để chứng minh lịch bo main theo thời gian thực.

- Source được nghiệm thu: `6a854d2e58a24c4b6536a3cb69dbef17de557c60`, nhánh feature; target .NET8, SDK local `10.0.401`, Unity6000.3.15f1/URP, `Microsoft Windows 11 Home Single Language`.
- CPU: AMD Ryzen 7 4800H with Radeon Graphics         . Local Docker riêng: game256MiB, API384MiB, Mongo fixture có volume riêng. Cổng host14777/14781/14500; production local mặc định vẫn7777/7781/5000. Control chỉ trong network.
- Profile main: min6/max10, lobby15s/loading30s/grace15s/ending10s. Profile test: min2, timer/bo tăng tốc; các kết quả demo không chứng minh thời lượng main theo thời gian thực.
- Docker smoke10 phiên đo input/snapshot trong5s rồi đợi kết quả; không phải bài tìm sức chứa cuối hoặc benchmark combat.

## 4. Các lượt đo và kết quả

### Bổ sung25/10 — Unity transport và khung kiểm thử online

| Hạng mục | Kết quả thực | Phạm vi |
| --- | --- | --- |
| Unity EditMode / PlayMode | PASS154 / PASS5 | Editor thực, không dùng dotnet thay Unity |
| Windows build | Succeeded,0 lỗi/1 warning | Unity cảnh báo uncompiled post-processing/importing; player từ chính build này đã chạy các lượt dưới |
| Dual transport backend | PASS53 | Gồm resume UDP→WSS cùng attempt và quit trả slot |
| Gameplay10 phiên Mixed | PASS8 | Socket thật; từ chối phiên11, resume, snapshot, kết quả và replay |
| Công cụ launcher/config | PASS31 | Có ca zero-rate INVALID, crash/sai scenario/thiếu evidence FAIL |
| Unity Local UDP,2 player | PASS2/2 | Trọn trận/HP/bo/result/replay/history, input/snapshot xấp xỉ30Hz |
| Unity Local WebSocket,2 player | PASS2/2 | Cùng các assertion gameplay và lịch sử |
| Unity Local Auto, UDP không có listener | PASS2/2, chọn WSS | Không sửa firewall; fallback có lý do |
| Main6 rehearsal hỗn hợp | PASS6/6 | 3UDP+3WSS; input≈30Hz, snapshot≈29,9Hz; mỗi client kết nối30s |
| Local đồng thời AWS | PASS1/1 | Client Local join/pong trong lúc6 player nối EC2; không coi là bài gameplay Local hai người |
| Auto fallback từ PC đến WSS AWS | PASS1/1 | UDP7779 không có listener, cùng WSS443; ConnectTimeout→Wss, RTT p50≈200ms, p99≈233ms |
| AWS main6, UDP/WSS từ PC | PASS6/6 trong300s | 3WSS+3UDP, result/replay/history; input29,994–29,996Hz, snapshot29,968–29,987Hz |
| AWS số đo server khi có người | PASS | 60 mẫu active, tick30,002Hz, CPU đỉnh16,38% một core, RAM lấy mẫu đỉnh42,56%/256MiB, dropped0; không OOM/restart |
| Stop–Start, account/JWT và Unity join lại | PASS lượt reconnect riêng | Cùng account trả409 khi đăng ký lại, login200, tạo phòng201, player join/pong; normal DNS/TLS hợp lệ sau đổi IP |
| LAN/VPN/public selfhost, hai PC vật lý | NOT_TESTED | Static config/preflight không chứng minh tuyến từ xa; xem checklist |

Lượt main6 lần1 cũng PASS; lần2 xác nhận lại sau khi sửa fingerprint PowerShell5/7. Không có thay đổi Unity code giữa rehearsal lần2 và AWS. Đơn vị RTT WAN là mili giây, không áp tiêu chí50ms Local cho Sydney.

AWS main6 ghi tổng1802 ping,0 pong timeout. WSS p50≈332–333ms, p95≈400ms, p99≈433ms; UDP p50≈300ms, p95≈334–367ms, p99≈367ms. Đây là RTT ứng dụng qua tuyến PC→Sydney và chu kỳ xử lý player, không phải RTT ICMP hay cam kết cảm giác input. Trong lượt này có thêm một client Local và một probe Auto ngắn ở phòng AWS riêng; không dùng số liệu để công bố sức chứa tối đa50 người.

Sau bài300s, kiểm Stop–Start lần đầu bị BLOCKED ở HTTP tool vì cache DNS trong tiến trình PowerShell5. Kết quả gameplay300s không bị đổi thành FAIL hoặc PASS cho restart: chúng là hai hạng mục riêng. Đã sửa DNS refresh của công cụ mà không pinIP/bỏ TLS, và chạy lại bài reconnect ngắn riêng; kết quả cuối nằm trong lifecycle của lượt `aws-unity-reconnect`.

### Mốc Local24/10 được giữ nguyên

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

## 5. Diễn giải và giới hạn

**Bổ sung25/10:** Unity Windows/Editor nay có UDP, WebSocket và Auto. Local thực đã qua cả ba tuyến; rehearsal main6 có UDP/WSS/Auto cùng phòng. WSS là đường dự phòng khi kết nối UDP thất bại trước join, vẫn yêu cầu DNS/TLS/443 hoạt động. Sau join không tự chuyển transport. Prediction/interpolation, combat và test hai PC vẫn là phạm vi riêng. Kết quả cũ “Unity chỉ UDP” trong đoạn lịch sử bên dưới chỉ áp dụng bản24/10.

Đây là gameplay tối thiểu có movement/collision và bo, chưa có combat vũ khí/inventory/hazard/FOW đầy đủ; kills bằng0. Unity chỉ nghiệm thu UDP; WSS/Auto/prediction/reconciliation/interpolation đầy đủ còn thuộc MOV-02/03. Chưa có killcam UI-03 hoặc toàn bộ UI-02. Hai process cùng Windows có thể dùng chung Guest DPAPI; kiểm hai Guest cùng tên nhưng credential khác dùng bài HTTP riêng. Chưa kết luận sức chứa gameplay50 người, độ mượt WAN hoặc độ trễ Sydney từ lượt Local này.

## 6. Phương án chọn và lý do

**Kết luận mở rộng25/10:** dùng UDP chính, WSS443 dự phòng bằng Auto trong Unity; giữ10 người/phòng,5 phòng/tổng50 slot và JWT quản lý phòng. Phương án đã chạy được demo mạng từ PC tới EC2, kể cả khi cố ý làm UDP thất bại. WSS không cần tester cài VPN hoặc có quyền AWS. VPN/private và public tự host có cấu hình riêng, secret/DB riêng cùng checklist, nhưng chưa được nghiệm thu hai thiết bị.

Sẵn sàng đưa cho dev tiếp tục tích hợp và tổ chức buổi test hai máy. Chưa kết luận đủ50 phiên gameplay hoặc cảm giác game hành động mượt với RTT≈300–433ms. Cần MOV-02/03 và đo lại khi có combat/inventory/snapshot lớn. “Hoàn tất một PC” không thay cho kiểm LAN/router/VPN từ PC thứ hai.

Kết luận dưới đây là mốc Local24/10:

**Chọn giữ10 người/phòng,5 phòng/tổng50 slot; đưa SRV-03, NET-02, ZON-01 sang tích hợp Local với demo UDP.** Lý do: đúng phạm vi lớp học, backend và Unity đã hoàn tất vòng trận với Guest/history, có test slot/transport/persistence, giữ các contract để dev combat/UI tiếp tục. Đây là hạn mức vận hành, không phải công bố benchmark tối đa mới.

Đợt này hoàn thành trên Local; không tự nâng cấu hình hoặc deploy AWS. Trước nghiệm thu chơi online cần release riêng và hai PC thật; sau khi có combat/snapshot phức tạp phải đo lại hiệu năng.

## 7. Bằng chứng, chi phí và chạy lại

**Bổ sung25/10:** [bằng chứng online](online-evidence-20261025.zip), [checksum](online-evidence-20261025.zip.sha256), [metadata](online-evidence-20261025.json), [CSV từng client](online-checks-20261025.csv). Archive mới tách lượt BLOCKED khỏi lượt PASS, giữ archive Local24/10 bên dưới. Không đưa env, credential, mã phòng hoặc proof vào gói.

- Release đã cài: `aws-online-20261025`; SHA256 bundle `dd63041999bd1219d2baab5cccfb73703a4ff0f7e2ab7751523177c75309815b`. Gói đầy đủ apps-only nằm tại `Backend/out/online-20261025/release/release.tar.gz`; Mongo/Caddy và volume hiện có được giữ. Không dùng delta vì base layers không trùng, không giảm được đáng kể dung lượng.
- Windows player mới nhất: `.utmp/online-20261024/build/Lastaway.exe`, copy **toàn bộ thư mục build** khi đưa cho PC2. Fingerprint gồm data/runtime: `abd33580d4630a892af4f9e10e58dfbf5540eff04a5d8488893713c3ab1b239e`. Không chỉ so hash.exe vì exe Unity là launcher stub.
- EC2 cuối cùng xác nhận **Stopped** bằng AWS API; giữ loại t3.small/CPU Standard, không tạo thêm máy. Các lượt chỉ dùng cửa sổ có watchdog và luôn Stop. Thời gian/các lần Start–Stop nằm trong lifecycle; không suy ra hóa đơn chính xác từ thời gian test. EBS vẫn có chi phí lưu trữ sau Stop.
- Fixture Docker của lượt này đã stop, giữ volume để không mất dữ liệu. Không prune cache/image/volume hay xóa dev container. Khôi phục8 snapshot Editor/URP/ProjectSettings/manifest về baseline trước test; scene Game sạch, giữ thay đổi có sẵn của user. Không commit/push/merge thay user.
- Một lệnh xóa `aws-auto-fallback.log` tạm bị automatic approval review từ chối với “blocked by policy”; file giữ ngoài Git/archive, đã giới hạn ACL cho current-user, không thử cách xóa khác. Các log/report nghiệm thu không chứa credential. Việc dọn tạm này còn chờ thao tác thủ công.

Các gạch đầu dòng tiếp theo là bằng chứng/cleanup của lượt Local24/10, không mô tả lượt AWS25/10:

- [Archive](phase2-evidence.zip), [SHA256](phase2-evidence.zip.sha256), [CSV kiểm tra](phase2-checks.csv); metadata nằm trong [report.json](report.json). Archive đã kiểm CRC; SHA256 `8c6a937defa0453a17cfd867b63ccee385340687a92f889c1f9aa277e5ac11ad`.
- [Hướng dẫn Phase 2](../../Phase2_Match_Guest_Zone.vi.md), [Network Architecture](../../Network_Architecture.vi.md), [Client guide](../../Client_Developer_Guide.vi.md).
- Giữ một Windows build mới nhất tại `.utmp/phase2/build/Lastaway.exe`. Khi chạy thủ công chọn profile phase2-local và RoomCode của môi trường; không bật cờ phase2Smoke khi chơi bình thường.
- Không gọi AWS/Start EC2/deploy trong lượt này. EBS của môi trường AWS vẫn có thể phát sinh chi phí riêng. Tài nguyên Docker fixture được đóng sau khi thu bằng chứng; không xóa volume/dev container có sẵn.
- Bộ kiểm tra/code đã commit trên feature; không push hoặc merge main. Archive lịch sử cũ giữ nguyên.
- Dọn hai worktree đã tích hợp bằng công cụ orchestrator, thu hồi **1.136.711.793 byte (~1,06 GiB)**. Giữ bản Windows mới nhất và các volume. Lệnh dọn thêm staging/file fixture trong `.utmp/phase2` bị automatic approval review từ chối với “blocked by policy”; nhóm này được giữ nguyên, không dùng cách xóa khác. Xem cleanup-final.json trong archive.
