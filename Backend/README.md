# Jungle Backend

Backend .NET 8 phục vụ dự án lớp học, một phòng tối đa 10 phiên. Dev/debug dùng Docker local; AWS là server chung chỉ bật cho buổi test online. Client hiện là NetSmokeClient console; Unity LiteNetLib và gameplay multiplayer còn cần triển khai.

- [Hướng dẫn setup, phát triển và kiểm thử chung](../Docs/Phase1_First4Tasks.vi.md): setup lần đầu, chạy lại hằng ngày, Docker, API/Compass, AWS SSH/deploy/Start-Stop, credits, kiểm thử và giao client cho dev/tester.
- [Kiến trúc mạng đã thực hiện](../Docs/Network_Architecture.vi.md): project/module, session/identity, packet, tick và luồng triển khai.

Local RoomCode chỉ cần game-server; account/Jwt dùng thêm profile account với MongoDB 8.0. AWS public chỉ RoomCode, API HTTPS qua Caddy, MongoDB 7.0.43 đã khóa trong release builder. Lấy source đầy đủ gồm Shared/global.json/Directory.Build.props; mỗi dev tạo .env riêng.

Các hướng dẫn tài khoản/UDP tách riêng trước đây đã được gộp vào hai file này. Tra mục 3–4 cho dev local, mục 6 cho admin AWS lần đầu, mục 7 cho mỗi buổi test và kiểm soát credits; mục 10 giải thích phần Unity chưa được nối.
