# Jungle Backend

Backend .NET 8 phục vụ dự án lớp học: tối đa 10 phiên/phòng, 5 phòng gồm mặc định, tổng server 50 phiên. Phòng tạo qua API JWT dùng mã join 6 ký tự chữ hoa/số; mỗi user tối đa 2 phòng bổ sung. Đây là hạn mức cấu hình, chưa phải sức chứa gameplay. Dev/debug dùng Docker local; AWS là server chung chỉ bật cho buổi test online. Client hiện là NetSmokeClient console; Unity LiteNetLib và gameplay multiplayer còn cần triển khai. Bản nhiều phòng chưa deploy AWS.

- [Hướng dẫn setup, phát triển và kiểm thử chung](../Docs/Phase1_First4Tasks.vi.md): setup lần đầu, chạy lại hằng ngày, Docker, API/Compass, AWS SSH/deploy/Start-Stop, credits, kiểm thử và giao client cho dev/tester.
- [Kiến trúc mạng đã thực hiện](../Docs/Network_Architecture.vi.md): project/module, session/identity, packet, tick và luồng triển khai.
- [Thực hành AWS: SSH, Swagger và Compass](../Docs/AWS_SSH_Swagger_Compass.vi.md): đăng nhập bằng alias `junglemaze`, test account API và xem DB qua SSH tunnel.

Local RoomCode chỉ cần game-server; account/Jwt dùng thêm profile account với MongoDB 8.0. AWS public chỉ RoomCode, API HTTPS qua Caddy, MongoDB 7.0.43 đã khóa trong release builder. Lấy source đầy đủ gồm Shared/global.json/Directory.Build.props; mỗi dev tạo .env riêng.

Các hướng dẫn tài khoản/UDP tách riêng trước đây đã được gộp vào hai file này. Tra mục 3–4 cho dev local, mục 6 cho admin AWS lần đầu, mục 7 cho mỗi buổi test và kiểm soát credits; mục 10 giải thích phần Unity chưa được nối.

Tạo phòng/đo tải: **mục 14** trong hướng dẫn chung. `Tools/Enable-RoomControl.ps1` thêm secret local; `Tools/room_benchmark.py` tự tạo/dọn stack benchmark Docker riêng và xuất Markdown/JSON/CSV. Không chạy tải hoặc deploy AWS tự động.

Đo vượt chính sách trên local: `Tools/local_capacity_probe.py` build bản sao GameServer riêng, đo một phòng tăng số phiên và nhiều phòng × 10 phiên bằng `NetSmokeClient --room-plan`. Công cụ không đổi `.env`/trần 10/5/50 của bản dev, không dùng API/MongoDB hoặc AWS. Cách chạy và cleanup ở mục 14.5; [báo cáo mới nhất](../Docs/Reports/Local_Capacity_Retest_2026-10-14/report.vi.md) giữ kết quả, phương án đã áp dụng, JSON/CSV và bằng chứng thô nén. Kiểm tra room-plan đã gộp trong `Tests/NetworkChecks`, không cần script Python riêng.
