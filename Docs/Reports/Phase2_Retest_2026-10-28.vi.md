# Kiểm thử lại các task Phase 2 đang Done — 28/10/2026

> Mục1–8 là kiểm thử baseline ban đầu; **mục9 là kết quả triển khai mới nhất**.

## 1. Kết luận và nguồn đối chiếu

Đã chạy lại toàn bộ Unity EditMode/PlayMode, backend và các luồng game thật trên source `f5864c17` tại `D:/Unity/Lastaway`, Unity6000.3.15f1. Không sửa code production hoặc trạng thái GDD.

Nguồn: [PROJECT TRACKING](https://docs.google.com/spreadsheets/d/17z41xmXWXdJhTJcziRoNuVw0jJQlEI8PS13_g3kYWUo/edit?gid=377713012), các dòng14–16,26–27,33–34. Có7 task Phase2 Done: SRV-03, NET-02, ZON-01, AUD-01, UI-03, ART-04, ART-05. MOV-02/03 vẫn In Progress; các test của chúng chỉ là hồi quy.

**Không phát hiện test chức năng tự động thất bại sau khi môi trường đúng.** Tuy nhiên Done trong Tracking không đồng nghĩa tất cả phần tích hợp/đánh giá người dùng đã xong: UI-03 và ART-05 có nghiệm thu có điều kiện trong ticket; ART-04 còn phần nhận diện bằng người thật. Không tự mở lại hay đóng thêm task.

## 2. Môi trường và phương pháp

- Backend .NET8; build0 error/0 warning. Unity Editor thật được điều khiển qua MCP HTTP localhost8080, instance Lastaway. Đầu buổi scene Game sạch, chưa Play; cuối buổi trả lại trạng thái này.
- Build Windows mới từ Boot+Game: job `build-805813df77`, succeeded,199,01MiB,0 lỗi/2 warning. Hai warning CS0114 ở OHunt_Trap.Start và CS0414 ở StoneDoor.ID thuộc mã tham khảo JSurviv; đầu buổi còn cảnh báo RHINO.move Legacy và Input Manager deprecated. Không sửa các cảnh báo này trong lượt QA.
- Mỗi bài dùng loopback riêng19777/19781; API15939, sau đó15949 để tách origin Guest, DB `phase2_retest_20261028`, project Docker `jungle-phase2-retest-20261028`. Không dùng secret hoặc DB dev/AWS.
- Docker engine lúc đầu không sẵn sàng; sau khi khởi động, dùng runtime.NET8 có sẵn và mount read-only bản publish mới của source hiện tại vào container. Không lấy binary ứng dụng cũ để thay code đang kiểm. Các log thất bại khởi động môi trường được giữ riêng.
- Test có clock/fake được phân biệt với test socket, Windows player và Editor+player. Không có PC vật lý thứ hai.

## 3. Các kiểm tra mới và kết quả

| Bộ kiểm tra | Kết quả |
| --- | --- |
| Unity EditMode toàn bộ |552/552 PASS,0 skipped |
| Unity PlayMode toàn bộ |106/106 PASS,0 skipped |
| Runtime/movement/zone |84 PASS |
| Guest/ticket/outbox |53 PASS |
| Network/RoomCode/JWT/quota |132 PASS |
| Dual transport/Auto/resume/TLS rejection |53 PASS |
| Gameplay socket thật |8 PASS mỗi lượt Mixed, Udp, Wss;10 phiên và từ chối phiên11 |
| API/Mongo thật |33 PASS |
| Telemetry hồi quy ngoài phạm vi Done |71 PASS sau khi build project riêng |
| Windows2 client không API/DB |PASS2/2 cho UDP, WSS và Auto với UDP không có listener |
| Editor+5 Windows player, main min6 |PASS5/5 phía player; Editor join thật, nhận snapshot/bo và kết quả |
| ESC trong trận thật |Mở/đóng được, timeScale=1; snapshot920→1650, tick2291→3021 trong lúc menu mở |
| Windows2 client với API/Guest/history thật |PASS2/2: movement, zone, HP, result, replay và history đúng MatchId |

EditMode có83 test Audio,118 MatchUi,5 Art04,8 UiArt,45 Movement. PlayMode có15 Audio,41 MatchUi,6 Art04,6 UiArt. Không cộng số theo nhóm vào tổng vì tên nhóm có thể giao nhau.

Các test clock bao phủ năm lịch co bo/warning, hình học, r0 và damage idempotent. Bài socket kiểm snapshot, room capacity, grace/resume và vòng trận tiếp. Bài API/Mongo kiểm JWT thiếu/sai/hết hạn, owner/quota/rate, Guest, ticket, người trùng tên, ghi kết quả idempotent/conflict, PlayerSessionId trùng và quyền lịch sử/leaderboard. Endpoint gửi outbox được cố ý làm lỗi; kết quả trận vẫn hiện, file outbox được giữ; sau đó bật lại pipeline để chạy Unity với history thật.

Lượt đầu Telemetry --no-build chưa có executable, chạy lại build riêng đạt71/71. Audio Python tùy chọn chưa chạy: runtime mặc định thiếu numpy, runtime bundled thiếu soundfile; không cài thêm package chỉ để làm xanh. Điều này không thay kết quả các test audio Unity đã đạt.

## 4. Đối chiếu từng task Done

| Task | Kết luận lượt này | Giới hạn/follow-up |
| --- | --- | --- |
| SRV-03 |PASS phạm vi runtime và demo mạng:2 client, main6, socket10, collision/input/slot/resume, result/replay |Không tuyên bố combat/loot/FOW hay50 phiên gameplay hoàn chỉnh. Replay buffer server không đồng nghĩa killcam đã được cấp cho UI |
| NET-02 |PASS unit và33 API/Mongo, Unity history end-to-end, chơi/result khi không API |AWS và hai thiết bị thật chưa retest. Khôi phục Guest sau mở lại ghi ở dữ liệu lượt reopen |
| ZON-01 |PASS clock5 phase, validation, state qua client thật và HP từ server |Trận test tăng tốc không chứng minh đủ7 phút main; main6 chỉ là smoke, không dùng để đo cân bằng |
| AUD-01 |PASS kỹ thuật: router/catalog/giới hạn source/volume/dedup/chuông và playback tests |Cảm nhận hitmarker/tiếng rồng/phối âm vẫn cần người nghe; không nhận đã nghe qua loa máy |
| UI-03 |PASS thành phần và ESC live; giữ nghiệm thu có điều kiện đã ghi trong ticket |Production Bootstrap dùng UnavailableKillcamReplaySource. Chưa có killcam server5s end-to-end. AC39–43/UI-02 và các reader/input tích hợp phải theo follow-up |
| ART-04 |PASS kỹ thuật: manifest18 weapon+16 equipment, mapping/importer và icon sweep |Nhận diện mù3 người/ký CSV là follow-up người dùng; live inventory/loot thuộc ITM-01 chưa Done |
| ART-05 |PASS kỹ thuật/catalog26 SFX; giữ nghiệm thu có điều kiện |G3 đã cho giữ tạm14 mục JSurviv nhãn chưa kiểm chứng. F1 thay MatchEnd bằng jingles_NES00 đã đóng và PIC đã nghe. F2 còn10 mã ưu tiên và điểm lặp nhạc; không báo chúng đã được nghe/duyệt lại |

Đã đọc phần Nghiệm thu cuối ticket, không chỉ dựa vào báo cáo QA giữa chừng: UI-03 mục8, ART-04 phần bàn giao/nghiệm thu và ART-05 mục8. Các chấp thuận có điều kiện này là quyết định có sẵn của team, không phải quyết định mới của lượt test này.

## 5. Quan sát trực tiếp và những mục chưa kiểm

- Game View thật có6 phiên, tên/HP/bo và HUD; ESC mở ở InGame. Tick/snapshot tiếp tục tăng. Ảnh `editor-esc-live.png` là ảnh mới, không lấy ảnh AWS người dùng gửi để thay cho retest.
- Nhận xét UI không chặn: nhãn nhân vật của demo còn vẽ trên panel ESC; HUD “Bo thu sau”/“Còn sống” vẫn placeholder. Phần binding HUD thật đã là điều kiện bàn giao UI-03; không quy kết catalog icon sai vì điều này.
- Ảnh AWS được cung cấp cho thấy nhiều player qua WSS với tick/zone gần nhau, nhưng ảnh tĩnh không chứng minh reconnect, lưu DB, dedup, đủ5 phase, phần nghe âm thanh hoặc hai PC.
- Lượt đọc HTTPS `/health` AWS hiện timeout sau5s. Chỉ ghi BLOCKED cho lần kiểm tuyến này, không kết luận EC2 hỏng/tắt hay mạng chặn. Không Start/Stop/deploy/load AWS và không biết phiên bản server đang deploy có trùng HEAD hiện tại không.
- Hai PC/LAN/VPN/WAN thực, gameplay combat/loot, stress50 phiên, RTT/loss/jitter MOV-02/03 và playtest nhóm10 người: NOT_TESTED ở lượt này.

## 6. Việc cần làm tiếp

1. Giữ Done theo phạm vi nghiệm thu hiện có; không gọi Phase2 đã tích hợp toàn bộ gameplay. Ưu tiên nối replay thật SRV-03→UI-03/UI-02, rồi kiểm replay đúng quyền/người/phòng và fallback spectator/result.
2. Nối số người sống/countdown bo vào HUD và xử lý lớp nhãn demo trên ESC khi làm UI tích hợp.
3. PIC thực hiện F2 audio và nhận diện icon theo ticket. Giữ nhãn nguồn/license chưa kiểm chứng; không đổi chúng thành đã xác minh.
4. Khi có hai PC và cửa sổ AWS dành riêng, dùng checklist hai PC để xác nhận UDP/WSS/Auto cùng build, nghe âm thanh thực, Guest riêng và resume.

## 7. Bằng chứng và dọn sau test

Dữ liệu máy đọc nằm ở `Phase2_Retest_2026-10-28/`; archive có SHA256 riêng. Chỉ đóng gói whitelist log/kết quả/ảnh; không đưa env, fixture.private, credential Guest/JWT, source publish hay nội dung outbox vào gói. Raw ở `.utmp/phase2-retest-20261028`.

Không commit/push, sửa source hoặc sửa GDD. Khôi phục EditorSettings về baseline trước test, mở scene Game sạch và dừng PlayMode. Server/native client QA được đóng; container fixture được stop, giữ volume/DB test để không xóa dữ liệu. Docker Desktop giữ mở. Chi tiết trạng thái cleanup nằm trong summary.json.
## 8. Bổ sung: đo6 client AWS sau khi người dùng bật server

Ngày28/10, khoảng15:30 UTC+7; IP do người dùng cung cấp. Không dùng kết quả health timeout trước đó để phủ định lượt đo mới này.

- Mở6 Windows player từ build mới của lượt QA. UDP tới IP7777: cả6 timeout trước join, không thu được RTT UDP. HTTPS dùng đúng hostname/certificate với IP hiện tại trả health200.
- DNS máy trả NXDOMAIN. Đã sao lưu hosts và thêm đúng một dòng lease trỏ hostname về IP hiện tại, tối đa10 phút; không bỏ kiểm chứng TLS. Lease đã được tự gỡ lúc08:32:05 UTC, đã kiểm không còn dòng lease trong hosts. Đây là workaround của lượt QA, không phải sửa DNS vĩnh viễn cho người chơi.
- Bài chính:3 client ép WSS và3 Auto; Auto thử UDP rồi timeout và chuyển WSS. Cả6 join/InGame, nhận snapshot, thấy6 người và bo, đạt scenario90 giây/client. Không dùng client/generator đặt trong EC2.

| Client | Cách vào | RTT p50 ms | p95 ms | p99 ms |
| --- | --- | ---: | ---: | ---: |
|1|WSS|266,58|433,62|700,59|
|2|WSS|266,71|533,27|700,26|
|3|WSS|266,69|546,46|733,66|
|4|Auto→WSS|266,67|500,09|633,79|
|5|Auto→WSS|266,63|333,37|667,30|
|6|Auto→WSS|266,76|366,66|601,97|

Input29,99–30Hz; snapshot29,97–30Hz;540 ping,0 pong timeout, không disconnect ngoài dự kiến. Các percentile là thống kê riêng từng client, không phải percentile gộp540 mẫu. Đây là RTT ứng dụng, bao gồm xử lý theo frame; không phải độ trễ một chiều hay thời gian input-to-photon. WSS/TCP có thể che retransmission, nên0 pong timeout không được gọi là0% packet loss UDP.

**Đánh giá:** kết nối và fallback hoạt động, nhưng median≈267ms và đuôi trễ đến734ms còn cao cho combat nhanh. Không kết luận game mượt hoặc EC2 yếu: chưa có số CPU/RAM/tick mới của EC2, CLI admin đã hết phiên. Driver di chuyển ngắn rồi đứng; chưa đo combat liên tục, chưa đủ trận/5 phase/10 người thật. Sender đạt tốc độ yêu cầu, lượt này không bị INVALID vì thiếu tốc độ phát.

**Flow hiện tại:** menu chọn endpoint/RoomCode → lobby main tối thiểu6/countdown15s → loading → server-authoritative movement/aim/collision, bo/HP → kết quả/hạng → chơi lại. Có Guest/history khi API sẵn sàng, HUD/icon/audio và ESC; spectator có adapter state, killcam thật chưa nối replay. Tạo/list/xóa phòng sau JWT hiện test qua Swagger/API; UI-02 chưa hoàn thiện toàn bộ màn. Chưa có vòng chiến đấu/loot/inventory authoritative đầy đủ, hazard/rồng/FOW; không xem controller/reference JSurviv như gameplay multiplayer đã port.

Dữ liệu: [JSON](Phase2_Retest_2026-10-28/aws-latency.json), [CSV](Phase2_Retest_2026-10-28/aws-latency.csv), [bằng chứng](Phase2_Retest_2026-10-28/aws-latency-evidence.zip). Đã đóng toàn bộ6 client test; giữ EC2 đang bật của người dùng, không start/stop/deploy/resize. Không ghi RoomCode vào báo cáo/config/archive. Hai PC vẫn NOT_TESTED.
## 9. Khép tích hợp Done và Local/LAN/Online — kết quả cuối 28/10

Code đã tích hợp trên nhánh `codex/done-closure-20261028`, commit `bbbe831d5eebe75614a9e77ca0dfccc97db0d33d`. Không push, merge main hoặc deploy AWS.

| Gói | Kết quả |
|---|---|
| G1 HUD/audio | HP thật, damage cue không trùng, alive/total và countdown bo từ server; reset khi đổi trận; nhãn demo không đè ESC. |
| G2 Local/menu | Local chạy solo đầy đủ không Docker/mã phòng, không instant win; chết → kết quả local → chơi lại/new MatchId hoặc menu. LAN UDP, Online Radmin UDP/AWS Auto; Nâng cao giữ loopback/profile/CLI cũ. Không gửi thành tích solo lên API. |
| G3 replay | Server đóng dấu death tick/cause, lọc theo phiên/phòng/victim và cửa sổ5s; chunk giới hạn; client kiểm metadata/shape, hủy dữ liệu cũ. Bootstrap dùng nguồn mạng thật. |
| G4 art/audio |19/19 sprite đã có sẵn trong prefab nên không làm lại; nối chuông bo/bị loại vào offline bằng router hiện có. HUD/icon/cue kỹ thuật có test. Các consumer của task chưa Done vẫn được ghi phụ thuộc. |
| G5 bàn giao | Development Windows build mới thành công0error/0warning; `.exe` tự kiểm hai vòng solo PASS. Hướng dẫn Phase1/Phase2, Network Architecture và checklist Radmin hai PC đã cập nhật. |

**Kiểm thử cuối:** EditMode581/581; PlayMode106/106; MatchChecks133; NetworkChecks132; DualTransport53; Guest/Result53; gameplay socket thật9 PASS mỗi tuyến UDP/WSS/Mixed (10 phiên, từ chối11, snapshot/result/resume/chơi lại và nhận replay150frame). Ca replay có killer dùng test damage authoritative trong fixture, không phải combat vũ khí đã triển khai.

Editor scene thật: Game mở offline, HP100, network Disconnected/UDP port0 khi không có container chạy; đưa Hero ngoài bo làm HP về0/Results; replay đổi MatchId và HP100; ESC vẫn tăng elapsed/countdown với timeScale1; rời về MainMenu. Windows runner xác nhận2 vòng, damage, reset, ESC và không mở socket game; process tự thoát. Vòng đủ năm mốc/r0 có test clock thuần; không gọi đây là trận gameplay người thật7 phút.

Một lượt PlayMode trước có hitch ESC62,4ms; lượt riêng và bộ cuối106/106 đạt, không sửa ngưỡng33,4ms. Giữ quan sát này, không tuyên bố loại hết giật trên mọi máy. Development player log khi thoát có cảnh báo cleanup ComputeBuffer của Unity; chưa phân loại được nguồn nên không kết luận không rò toàn ứng dụng.

**Giới hạn:** hai PC/LAN/Radmin/WAN của build mới NOT_TESTED; AWS không bật/deploy/đo lại. UI02 tài khoản/phòng đầy đủ, combat/loot/FOW và các consumer MOV/VFX chưa Done không được kéo vào phạm vi. Backend tạo/list/xóa phòng vẫn yêu cầu JWT; Guest chỉ join bằng mã. Killcam mặc định tắt, zone/unknown killer fallback; cần combat thật để nghiệm thu góc nhìn người chơi chiến đấu.

ART04 nhận diện icon3 người; ART05 nghe10 cue/loop nhạc vẫn MANUAL_REQUIRED.14 mục nguồn chưa kiểm chứng giữ nhãn cũ theo nghiệm thu có điều kiện, không đổi thành đã có giấy phép. GDD không bị đổi status.

Bản chạy: `.utmp/done-closure-20261028/build/Lastaway.exe` (giữ cả thư mục build khi chia sẻ). Bộ chạy lặp: `-offlineTest -offlineReport <path>` trên development build; không thêm flag khi chơi bình thường. Bằng chứng: [zip](Phase2_Retest_2026-10-28/done-closure-evidence.zip), SHA256 đi kèm; summary.json có `doneClosure` và hash toàn cây build. Không đưa env/JWT/Guest credential vào archive.

Đã khôi phục EditorSettings theo baseline, scene Game sạch/EditMode. File kế hoạch và packet kế hoạch tạm được xóa sau nghiệm thu; giữ build, code test tái sử dụng và checkpoint để truy nguyên. Không xóa volume hoặc dữ liệu dev.
