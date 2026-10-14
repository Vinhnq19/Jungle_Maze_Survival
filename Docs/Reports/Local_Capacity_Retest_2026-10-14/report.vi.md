# Đo lại sức chứa backend local — 14/10/2026

**Kết quả: đã xác nhận 6 phòng × 10 = 60 phiên qua 180s theo tiêu chí mạng local; 7 phòng × 10 trượt p95 của một phòng. Một phòng 50 phiên cũng đạt 180s, còn 95 phiên trượt. Đã chọn và áp dụng 5 phòng × 10, tổng 50 phiên vào source/config/local.** Đây là mức đã xác nhận trong bài đo, không phải trần phần cứng hay sức chứa gameplay/EC2. Chỉ giữ báo cáo mới nhất này theo cấu trúc bảy phần, kèm JSON/CSV và bằng chứng thô nén.

## 1. Mục đích và phạm vi

Đo lại số phiên trong một phòng và nhiều phòng hoạt động đồng thời, thay vì coi cấu hình 3 phòng/30 phiên là giới hạn phần cứng. Đây là phép đo GameServer/control/UDP ping–pong, tick 30Hz chưa mô phỏng gameplay. Không đo Unity, movement, combat, quái, đạn, snapshot, broadcast giữa người chơi, API tài khoản hoặc MongoDB. Không bật EC2, deploy hay chạy tải AWS.

**Trạng thái hiện tại:** 10 người/phòng, 5 phòng tính cả mặc định, tổng 50 active/pending; mỗi user tối đa 2 phòng tạo thêm. Đã build/recreate API và GameServer local, giữ MongoDB. Source/Compose AWS được cập nhật nhưng chưa deploy EC2. Tại thời điểm chạy 22 lượt đo bên dưới, bản dev còn 3/30; không sửa dữ liệu lịch sử đó thành kết quả đo trên cấu hình mới.

## 2. Những việc thực hiện trước khi kết luận

1. Kiểm tra Docker context là engine local qua `npipe:////./pipe/dockerDesktopLinuxEngine`. Ghi ID/image của API, GameServer và MongoDB dev để đối chiếu sau lượt đo. Giữ các container này chạy nguyên trạng.
2. Vì `RoomLimits` tại thời điểm đo chặn 10/3/30, tạo bản sao chỉ gồm source GameServer/Shared và file build cần thiết trong thư mục tạm `Backend/out/capacity-probe/<run>/source`. Chỉ thay bốn hằng giới hạn trong bản sao; không đổi thuật toán xử lý mạng, protocol v1 hoặc tick. Source snapshot và dữ liệu thô đã được nén vào `evidence.zip`, thư mục tạm đã dọn.
3. Build image và container dùng riêng cho phép đo: RAM 256MiB, không swap, CPU không quota, UDP và HTTP control trên cổng loopback ngẫu nhiên. Không dùng `.env`, DB, phòng hay secret của team.
4. Tạo phòng qua HTTP control bằng secret tạm. Bài một phòng dùng một phòng tạo thêm với 256 slot, phòng mặc định vẫn tồn tại nhưng trống. Bài nhiều phòng dùng cả phòng mặc định, mỗi phòng đúng 10 client. Vì thế cột “phòng hoạt động” không đồng nghĩa số phòng đã đăng ký trong bài một phòng.
5. Bổ sung `NetSmokeClient --room-plan` để một tiến trình phát tải đồng thời vào các phòng. Mỗi phiên vẫn có socket/LiteNetLib manager riêng. File đầu vào có mã phòng riêng tư; report chỉ giữ ID phòng, không giữ mã.
6. Chạy theo mức tăng dần. Mỗi mức warm-up 15 giây rồi đo 60 giây; mức trượt được lặp, thu hẹp vùng đạt/trượt theo bước 5 phiên hoặc 1 phòng, xác nhận ứng viên trong 180 giây. Khi mức tăng trượt lặp, không chạy tiếp các mức cao hơn. Sau khi ứng viên 95/một phòng và 9 phòng trượt lượt dài, chạy xác nhận thấp hơn: một phòng 50, năm phòng × 10, bảy phòng × 10, rồi sáu phòng × 10. Mức 7 trượt nên không chạy xác nhận 8. Không diễn giải danh sách mức dự kiến thành mức đã thử.
7. Thu RTT từng client/phòng, tốc độ phát thật, ping/pong/loss, join/disconnect; CPU, RAM cgroup, tick/drop và room occupancy từ GameServer. Không chạy build/test regression nặng trong cửa sổ đo.
8. Dọn tài nguyên của từng lượt, đối chiếu bản dev/policy. Sau khi chốt 5/50, sửa policy/config/Swagger, build/recreate local và chạy lại bộ kiểm tra đầy đủ. Gộp 16 kiểm tra room-plan vào NetworkChecks, bỏ script Python riêng; nén bằng chứng rồi dọn report/script tạm dư.

### Giới hạn chỉ dùng trong source snapshot

| Hằng | Source tại thời điểm đo | Bản sao thử nghiệm |
| --- | ---: | ---: |
| PlayersPerRoom | 10 | 256 |
| TotalPlayers | 30 | 256 |
| Rooms, gồm mặc định | 3 | 32 |
| RoomsPerUser | 2 | 31 |

Trong 22 lượt đo, API công khai/Swagger của bản dev không được nới giới hạn. Bài đo provision trực tiếp qua control endpoint nên không đánh giá throughput/rate limit tạo phòng qua Swagger hoặc JWT. Việc áp dụng 5/50 và kiểm tra API được thực hiện sau đó, ghi riêng ở phần 6–7.

## 3. Môi trường và cách tính

| Thành phần | Cấu hình |
| --- | --- |
| Máy phát tải/host | Windows, AMD Ryzen 7 4800H, 16 logical CPU, RAM vật lý 16.558.178.304 byte ≈15,42GiB |
| Docker Desktop | Engine 29.5.3, WSL2 kernel 5.15.167.4, 16 vCPU hiển thị, VM RAM 8.021.950.464 byte ≈7,47GiB |
| Cgroup | v1; đọc memory.usage_in_bytes khoảng mỗi giây |
| GameServer | .NET 8, LiteNetLib 2.1.4; 256MiB memory, memory+swap 256MiB, không CPU quota |
| Client | .NET SDK 8.0.403; một tiến trình console trên Windows, nhiều socket tới Docker loopback |
| Tải | Unreliable Ping/Pong 30Hz mỗi phiên, không payload gameplay; protocol v1, RoomCode |
| Cấu hình phụ | Metrics 1s; TTL phòng trống 3.600s để không thu hồi giữa các lượt; phòng mặc định 10 slot |
| Thời gian | Warm-up 15s; đo thường 60s; xác nhận 180s; chờ Pong cuối tối đa 5,2s |

CPU GameServer là CPU process quy đổi một core: 100% tương ứng một core bận, không phải phần trăm tổng 16 CPU. Giá trị báo cáo là đỉnh cửa sổ metrics khoảng một giây. RAM là đỉnh mẫu cgroup so với 256MiB, không chỉ WorkingSet và không gồm toàn bộ stack/API/Mongo hoặc RAM host.

RTT được tính từ lúc client gửi tới khi ứng dụng client đọc Pong; bao gồm client, transport, Windows/WSL/Docker và lịch poll. Không phải số đo riêng thời gian xử lý GameServer. Percentile dùng nearest rank; cột p95 chung gom mẫu mọi phiên, cột p95 phòng tệ nhất lấy giá trị lớn nhất trong các phòng. Mỗi phòng phải đạt, không dùng trung bình các phòng để che phòng chậm.

### Tiêu chí đạt

| Chỉ số | Điều kiện |
| --- | --- |
| Kết nối | Đủ mọi phiên join, không reject/disconnect ngoài dự kiến |
| Timeout/loss Pong | Dưới 1% số Ping, sau khi đợi Pong cuối |
| RTT | p95 **mỗi phòng** ≤50ms local; p99 ghi nhận nhưng không dùng làm điều kiện đạt |
| Tick | Trung bình 29–31Hz; dropped tick = 0 |
| CPU/RAM | Đỉnh CPU ≤85% một core; RAM cgroup ≤80% giới hạn |
| Telemetry | Có mẫu server trong cửa sổ đo và mẫu RAM |
| Máy phát tải | Gửi ≥28,5Hz/phiên và đo đủ ≥99% thời lượng yêu cầu; không đạt thì không quy kết server yếu |

Telemetry server chỉ lấy cửa sổ nằm trong giai đoạn đo, loại chuyển tiếp warm-up. Không có CPU quota tương đương một EC2 micro/small: cùng giới hạn RAM container không làm hai môi trường tương đương.

## 4. Các lượt đo và kết quả

Đã chạy **22 lượt**, gồm **34 phút cửa sổ đo** và 15s warm-up mỗi lượt, trên bốn container thử nghiệm nối tiếp. Mức lớn nhất đã đo là **100 phiên**, tối đa **10 phòng hoạt động**. Các mức 150/200/256 phiên và 15/20/25 phòng trong kế hoạch công cụ không được chạy do đã trượt ở mức trước.

Trong toàn bộ cửa sổ đo: **4.319.700 Ping và đủ 4.319.700 Pong**, không timeout/outstanding còn lại, không reject/disconnect ngoài dự kiến và không dropped tick. Mọi lượt gửi hợp lệ; tốc độ thấp nhất ở cả tổng/phiên là **29,933Hz**, vượt yêu cầu 28,5Hz. Tick trung bình từng lượt **29,987–30,017Hz**. Đỉnh CPU GameServer **16% một core**, đỉnh RAM cgroup **51,17MiB ≈19,99% của 256MiB**. CPU client cao nhất tính theo trung bình cả cửa sổ là khoảng **26,92% một core**; không tương đương đỉnh CPU một giây của server.

Tất cả các lượt FAIL dưới đây chỉ trượt tiêu chí **p95 mỗi phòng ≤50ms**. Các tiêu chí join, loss, tick, CPU/RAM và telemetry còn lại đều đạt. p99 không phải tiêu chí đạt nhưng được ghi để quan sát các đợt tăng độ trễ.

`measure`: tăng tải ban đầu; `repeat`: lặp mức trượt; `refine`: thu hẹp bằng lượt 60s; `confirm`: ứng viên cao nhất ngắn hạn; `confirm-only`: đo dài cố định ở mức thấp hơn. Một lượt 60s PASS không thay thế xác nhận dài. RAM giữa các lượt không tăng đều theo số phiên vì process có lịch sử cấp phát/GC khác nhau và mỗi probe khởi động container mới.

| Lượt | Phòng hoạt động | Phiên | Đo (s) | Kết quả | RTT p95/p99 (ms) | p95 phòng tệ nhất | Hz/phiên | CPU core % | RAM MiB |
| --- | ---: | ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: |
| single-10-measure | 1 | 10 | 60 | PASS | 46.96/47.86 | 46.96 | 29.99 | 5.97 | 36.01 |
| single-30-measure | 1 | 30 | 60 | PASS | 46.99/60.64 | 46.99 | 29.99 | 8.00 | 36.17 |
| single-50-measure | 1 | 50 | 60 | PASS | 47.26/61.01 | 47.26 | 29.99 | 7.00 | 37.17 |
| single-100-measure | 1 | 100 | 60 | FAIL | 60.43/96.22 | 60.43 | 30.00 | 12.99 | 40.20 |
| single-100-repeat | 1 | 100 | 60 | FAIL | 60.88/96.07 | 60.88 | 30.00 | 13.00 | 41.30 |
| single-75-refine | 1 | 75 | 60 | PASS | 48.28/61.60 | 48.28 | 29.93 | 12.93 | 43.25 |
| single-85-refine | 1 | 85 | 60 | PASS | 47.92/61.10 | 47.92 | 30.00 | 11.00 | 43.89 |
| single-90-refine | 1 | 90 | 60 | PASS | 47.21/60.67 | 47.21 | 29.99 | 10.00 | 44.98 |
| single-95-refine | 1 | 95 | 60 | PASS | 47.26/60.98 | 47.26 | 30.00 | 11.00 | 47.17 |
| single-95-confirm | 1 | 95 | 180 | FAIL | 59.92/81.98 | 59.92 | 30.00 | 13.99 | 47.32 |
| multi-3-measure | 3 | 30 | 60 | PASS | 46.68/60.29 | 46.72 | 30.00 | 6.00 | 47.95 |
| multi-5-measure | 5 | 50 | 60 | PASS | 47.65/61.17 | 47.82 | 29.99 | 7.00 | 47.80 |
| multi-10-measure | 10 | 100 | 60 | FAIL | 59.84/62.84 | 60.04 | 29.99 | 12.98 | 49.00 |
| multi-10-repeat | 10 | 100 | 60 | FAIL | 61.74/141.64 | 62.08 | 29.99 | 15.00 | 49.73 |
| multi-7-refine | 7 | 70 | 60 | PASS | 47.16/60.97 | 47.33 | 30.00 | 8.00 | 49.99 |
| multi-8-refine | 8 | 80 | 60 | PASS | 47.23/60.70 | 47.43 | 30.00 | 9.00 | 49.94 |
| multi-9-refine | 9 | 90 | 60 | PASS | 47.33/60.97 | 47.77 | 30.00 | 11.00 | 50.58 |
| multi-9-confirm | 9 | 90 | 180 | FAIL | 60.31/217.75 | 60.50 | 30.00 | 16.00 | 51.17 |
| single-50-confirm-only | 1 | 50 | 180 | PASS | 47.34/61.03 | 47.34 | 30.00 | 9.99 | 34.86 |
| multi-5-confirm-only | 5 | 50 | 180 | PASS | 47.26/60.97 | 47.43 | 30.00 | 7.00 | 37.96 |
| multi-7-confirm-only | 7 | 70 | 180 | FAIL | 48.27/61.79 | 55.71 | 30.00 | 12.00 | 35.87 |
| multi-6-confirm-only | 6 | 60 | 180 | PASS | 48.03/61.66 | 48.21 | 30.00 | 10.94 | 38.92 |

### Mức đã xác nhận và vùng chưa kết luận

| Câu hỏi | Kết luận từ dữ liệu |
| --- | --- |
| Tổng phiên đã xác nhận dài cao nhất | 60 phiên, chia 6 phòng × 10, RTT p95 phòng chậm nhất 48,21ms, p99 gộp 61,66ms, CPU đỉnh 10,94% một core, RAM 38,92MiB |
| Mức nhiều phòng kế tiếp | 7 phòng × 10 trượt 180s vì một phòng p95 55,71ms; không khẳng định 70 phiên chắc chắn không thể tối ưu để đạt |
| Một phòng | Xác nhận 50 phiên ở 180s; 95 phiên trượt 180s. Các mức 75/85/90 chỉ đo 60s, chưa xác định trần dài hạn giữa 50 và 95 |
| 8 phòng × 10 | Chỉ đạt lượt 60s; chưa xác nhận 180s, không nội suy là đạt từ mức 9 hoặc 6 |
| 100 phiên | Join đủ cả bố cục một phòng và 10 phòng, nhưng trượt độ trễ ở hai lượt 60s của mỗi bố cục |
| Trần phần cứng/gameplay | Chưa tìm ra; bài này không ép CPU/RAM tới cạn và chưa có mô phỏng gameplay |

“Cao nhất đã xác nhận” là trong những trường hợp đã đo, không phải bảo đảm mọi tổ hợp ít người hơn đều đạt hoặc kết quả sẽ lặp y hệt trên máy khác. Chưa đo đầy đủ mọi số người/phòng, cấu hình transport, soak nhiều giờ hoặc nhiều máy phát tải.

## 5. Diễn giải giới hạn

“Join được” chỉ chứng minh nhận kết nối/slot. “Đạt bài đo” còn yêu cầu đủ tốc độ gửi, độ trễ, loss, tick và tài nguyên theo bảng trên. Không dùng số cấu hình 256 phiên/32 phòng hoặc số client join thành công để tuyên bố sức chứa ổn định.

Lượt xác nhận **7 phòng × 10** minh họa vì sao phải đo từng phòng: RTT p95 gộp khoảng **48,27ms** đạt 50ms, nhưng phòng chậm nhất **55,71ms** nên toàn lượt vẫn trượt. Không lấy p95 gộp để kết luận mọi phòng đều đạt. Lượt **9 phòng × 10** có p99 gộp khoảng **217,75ms**, dù không mất Pong; việc nhận đủ gói không loại trừ các đợt tăng độ trễ.

Nếu chỉ RTT trượt trong khi CPU/RAM/tick/loss vẫn đạt, kết luận là đường ping/pong local chưa đạt tiêu chí độ trễ tại mức đó. Chưa đủ căn cứ quy nguyên nhân cho CPU server hoặc coi đó là trần phần cứng. Cần đo nhiều máy phát tải, phân tích transport/poll/scheduling, lặp ở điều kiện máy khác và chạy soak dài hơn nếu cần tìm nguyên nhân.

Số đo còn phụ thuộc thời điểm và điều kiện máy phát tải. Không so một kết quả cũ với kết quả này để tuyên bố code nhanh lên theo tỷ lệ cụ thể; báo cáo cũ đã được bỏ để chỉ giữ một bản kết quả mới nhất.

Khi thêm gameplay, mỗi phòng cần world/entity riêng, xử lý input/collision/combat và gửi snapshot tới các client. Bài ping/pong hiện tại không có các chi phí đó; phải đo lại trước khi cam kết số người chơi thực hoặc chuyển kết quả sang EC2/Internet.

## 6. Phương án chọn và triển khai

**Đã áp dụng cho backend local: 10 người/phòng, tối đa 5 phòng gồm 1 mặc định + 4 phòng tạo thêm, tổng 50 active/pending.** Giữ quota 2 phòng tạo thêm/user, TTL phòng trống 600s và mã phòng mới 6 ký tự. Cần ít nhất hai user để tạo đủ bốn phòng ngoài mặc định; quota của một user không phải số phòng tối đa của server.

Lý do:

1. Tổng 50 phiên đã đạt cả một phòng và năm phòng trong lượt 180s, với RTT p95 khoảng 47,3ms; không chọn chỉ từ một lượt 60s. Bài chia năm phòng đo trực tiếp phương án đề xuất.
2. Giữ 10 người/phòng đúng mục tiêu lớp học. Chưa có cơ sở nâng một phòng lên 50–100 người khi chưa đo gameplay và gửi trạng thái giữa người chơi.
3. Năm phòng cho dev nhiều nhóm test riêng hơn ba phòng, thấp hơn mức sáu phòng/tổng 60 đã xác nhận. Không dùng mức cao nhất làm mặc định; bảy phòng đã có một phòng trượt, vùng 90–100 phiên còn kém ổn định hơn. Không coi chênh lệch 10 slot là lượng dự phòng hiệu năng gameplay cố định.
4. Có thể tăng tiếp sau khi đo Unity/gameplay thật và thời gian dài hơn. Số phiên chưa sử dụng không thể quy đổi thành một tỷ lệ dự phòng CPU cho quái, đạn hoặc combat.

**Với AWS:** source/Compose đã chuẩn bị policy 5/50 nhưng chưa build release/deploy EC2 hoặc chạy tải AWS. Không suy ra cấu hình runtime hiện tại trên EC2. Khi triển khai sau này, đo từ một phòng 10 người thực trên Internet trước, chọn tiêu chí RTT theo môi trường đích rồi mới mở tải nhiều phòng. Có thể cấu hình giới hạn thấp hơn trần 5/50 cho buổi test đầu; bài này không xác nhận EC2 chịu được 50 hoặc 80 người.

**Khi bắt đầu gameplay:** nghiệm thu trước một phòng 10 người, sau đó nhiều phòng có world/quái/đạn/snapshot thực. Chưa cam kết chơi mượt 50 người chỉ từ ping/pong. Nên chạy soak dài hơn và dùng nhiều máy phát tải độc lập khi đánh giá phát hành.

Các thay đổi triển khai:

| Hạng mục | Kết quả |
| --- | --- |
| RoomLimits dùng chung | TotalPlayers=50, Rooms=5; PlayersPerRoom=10, RoomsPerUser=2 giữ nguyên |
| Validation và Swagger | Từ chối cấu hình tổng >50/phòng >5; mô tả POST và Swagger ghi đúng 5/50, maxPlayers tối đa 10 |
| Cấu hình | `.env.example`, defaults Compose local/AWS và hai khóa số trong `.env` local cập nhật 5/50; không ghi đè secret |
| Runtime local | Build/recreate API/GameServer, `/health` trả ok/database connected; UDP client join/Pong thành công; Mongo giữ nguyên ID/image |
| Kiểm thử chức năng | Năm phòng đồng thời đủ 10 active, 50 client có Pong; chủ phòng khác chưa có phòng cũng bị từ chối tạo phòng thứ sáu |
| Dọn file | Bỏ report cũ, script tổng hợp dùng một lần và script kiểm tra room-plan tách riêng; giữ một report này và bằng chứng nén |

Kiểm tra năm phòng × 10 sau triển khai là lượt chức năng **6 giây**, không phải một lượt đo hiệu năng 180s mới. Bảng 22 lượt ở phần 4 vẫn là dữ liệu đo trước khi áp dụng policy; không thay số liệu hoặc fingerprint để coi như vừa đo lại dài trên image mới.

## 7. Bằng chứng, kiểm tra và chạy lại

- [Dữ liệu JSON](report.json): môi trường, source/image fingerprint, mọi tiêu chí, số đo server và từng phòng của các lượt.
- [Bảng CSV](report.csv): mọi mức đạt/trượt và thời lượng; không bỏ các lượt trượt hoặc lặp lại.
- [Bằng chứng nén](evidence.zip): JSON RTT từng client, log server, source snapshot và log kiểm tra triển khai; có manifest SHA256 bên trong.
- [Cách chạy công cụ, file riêng tư và cleanup](../../Phase1_First4Tasks.vi.md#145-thử-vượt-chính-sách-trên-bản-gameserver-local-riêng).
- Source công cụ: `Backend/Tools/local_capacity_probe.py`, `Backend/Tools/room_benchmark.py`, `Backend/NetSmokeClient/Configuration/RoomLoadTarget.cs`; kiểm tra input đã gộp trong `Backend/Tests/NetworkChecks/Program.cs`.

Kết quả kiểm tra cuối:

- Build `JungleBackend.sln`: **0 warning, 0 error**.
- **124 network checks** qua: gồm 16 room-plan checks đã gộp; kiểm tương thích client, RoomCode/JWT, pending/timeout/trả slot, policy 5/50, từ chối phòng thứ sáu và HTTP control. Các case room-plan hợp lệ/không hợp lệ dừng trước khi mở session mạng.
- **50 account/API checks**, **9 kiểm tra migration secret** và **42 kiểm tra Docker/API phòng** qua. Tổng **225 điểm kiểm tra**, không cộng trùng 16 case room-plan.
- Audit độc lập **22 lượt** với JSON client thô: đối chiếu số join/sent/Pong/loss, tính lại p95/p99 và p95 từng phòng, kiểm tốc độ từng client và hash toàn bộ source snapshot. Không sửa số đo gốc.
- Bốn lượt đo ban đầu ghi `productionPolicyUnchanged=true`, `devContainersUnchanged=true`, `cleanupErrors=[]` tại thời điểm đo. Sau khi áp dụng policy, API/GameServer được recreate có chủ đích; xác minh riêng `mongoContainerUnchanged=true`. Không deploy AWS hoặc xóa volume/DB của team.

Trong `evidence.zip`: `runs/<runId>/` chứa JSON/log thô của bốn lượt, `source/` lưu một bản snapshot dùng chung; `implementation/` chứa log build/core suite/local runtime và kết quả Docker/API sau áp dụng 5/50. Đã kiểm CRC toàn archive và SHA256 của **94 file bằng chứng** theo `manifest.sha256.json`. JSON báo cáo có hash archive, metadata của mọi lượt và xác minh runtime mới. Đã dọn thư mục raw/report/script tạm ngoài archive để không giữ nhiều bản trùng.

Các lượt đo dùng cùng fingerprint source GameServer `7b98b2145812ab251eb55c9e6b940b1e6520d3461199d06aeebdc1149bce3b72`, HEAD `fd2b2e9a887d1cd87644165de62b73ee96f38042`. Đây là fingerprint **bản đo trước khi áp dụng 5/50**, không phải hash source hiện tại. Giữa lượt dò và lượt xác nhận thấp hơn chỉ bổ sung guard input `null` của room-plan trên client; hash binary từng đợt được giữ trong JSON. Workspace có thay đổi chưa commit nên commit HEAD một mình không định danh đầy đủ bản đo.
