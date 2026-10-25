# Kiểm thử bằng hai PC thật

Chỉ điền PASS khi đã dùng **hai thiết bị vật lý**. Hai cửa sổ, hai process hoặc máy ảo trên một PC không thay thế bài này. Report do launcher tạo luôn ghi `physicalDeviceCount=1`, `physicalTwoPc=NOT_TESTED`; giữ nguyên chúng và thêm biên bản đối chiếu hai máy bên dưới.

## 1. Chuẩn bị chung

- Cả hai PC Windows dùng cùng bản build, hash dữ liệu và profile trỏ cùng backend. Server host có thể nằm trên PC1 hoặc EC2.
- Bật Run In Background, tránh Sleep/đổi Wi-Fi trong bài đo. Ghi giờ UTC và thông tin mạng; không cần đặt điều kiện đồng hồ lệch dưới 50ms để đo RTT.
- Dùng tên khác nhau cho lần đầu. Chia sẻ RoomCode; không chia sẻ JWT, Guest credential, file DPAPI hoặc khóa AWS.
- Tạo phòng qua Swagger bằng tài khoản đã login/JWT hợp lệ. Guest chỉ join/chơi/xem lịch sử của mình.

## 2. Chọn đúng bài theo tuyến

| Tuyến | Cấu hình và cách thực hiện | Điều bắt buộc kiểm trên PC2 |
| --- | --- | --- |
| LAN riêng | PC1 chạy Docker Local, publish UDP vào IP LAN bằng GAME_PUBLISH_HOST; mở firewall UDP đúng mạng. Dùng profile server test để đủ2 người. PC2 dùng IP LAN, transport Udp, protocol3. | JoinAccepted/pong và gameplay thật từ PC2. HTTP API LAN không được dùng cho Guest; có thể chơi ẩn danh, lịch sử báo không khả dụng. Muốn đủ Guest/history dùng HTTPS. |
| VPN Tailscale | Hai máy đăng nhập cùng tailnet. Host dùng compose.host profile vpn, proxy loopback8080 sau Tailscale Serve HTTPS. UDP dùng IP Tailscale; API/WSS dùng hostname HTTPS ts.net của **cùng host**. Private test có thể dùng min2. | UDP trực tiếp/relay bằng tailscale ping/status, rồi ép Wss và Auto. HTTPS phải có certificate hợp lệ. Không mở8080 ra mạng; không bật Funnel. |
| Tự host public | Hostname riêng, khác junglemaze.duckdns.org; TCP80/443 và UDP7777 forward về PC1. Public dùng main. PC2 phải ở mạng ngoài, như hotspot, để tránh chỉ kiểm hairpin của router. | DNS, HTTPS, Unity WSS và UDP từ mạng ngoài. CGNAT hoặc mạng chặn toàn bộ tuyến được ghi BLOCKED. |
| AWS | Hai PC dùng hostname AWS, HTTPS/WSS443 và UDP7777. Giữ main min6. Admin đặt thời lượng/tự Stop trước khi mời tester. | Game đi Internet trực tiếp, không dùng SSH/SSM tunnel để thay kết quả gameplay. |

Public/AWS cần **ít nhất6 gameplay client**. Với hai PC, chạy3 client trên mỗi máy hoặc hai người thật cộng4 client kiểm thử. Không dùng kịch bản2 người để đòi main bắt đầu, không hạ luật main.

## 3. Thao tác trên mỗi PC

1. Mở PowerShell tại thư mục gốc repo; với gói player-only thì dùng đường dẫn tương ứng đến script/config đã bàn giao.
2. Kiểm HTTPS: `curl.exe --fail --show-error https://TEN-HOST/health`. Không dùng `-k`. GET `/game` bằng HTTP có thể trả400 vì thiếu WebSocket Upgrade; chỉ client join/pong thật mới chứng minh WSS hoạt động.
3. Nhập mã bằng `$env:NET_ROOM_CODE = Read-Host 'Ma phong'`. Profile JSON không chứa mã phòng, password hoặc token.
4. LAN/VPN private test: trên **mỗi máy** chạy1 client với cùng scenario `local2-accelerated.json`:

```powershell
.\Backend\Deploy\test-unity-online.ps1 -Player 'D:\Game\Lastaway.exe' -Config 'D:\Game\client.vpn.json' -Scenario '.\Backend\Tests\OnlineReadiness\scenarios\local2-accelerated.json' -Clients 1 -OutputDirectory '.\Backend\out\two-pc-PC1-LUOT-MOI'
```

PC2 dùng tên output riêng. Profile/client config phải đi cùng đúng server `test`, HTTPS API hoạt động nếu scenario yêu cầu history.

5. Public/AWS main: đổi scenario thành `main6-smoke.json`, `-Clients 3` trên mỗi máy; bắt đầu hai bên gần nhau, trong lobby15s. Nếu không kịp đủ6 thì để countdown hủy rồi thử lại đúng luật.
6. Chạy lần lượt: ép Udp, ép Wss, Auto. Muốn chứng minh Auto fallback, dùng profile thử có **cổng UDP không có listener**, giữ WSS/API đúng host; không tắt toàn bộ firewall. Report phải ghi selectedTransport Wss và fallbackReason.
7. `Test-NetConnection -Port7777` chỉ thử TCP, không dùng kết quả đó để kết luận UDP. Dùng client UDP thật và quan sát gói/header trên host nếu cần.

## 4. Đối chiếu giữa hai máy

- Cùng RoomId/MatchId; hai phía thấy vị trí, cover collision, bo và HP nhất quán.
- Mỗi client gửi ít nhất28,5 input/s trong đoạn còn sống; lưu RTT p50/p95/p99 thực, không áp ngưỡng Local cho WAN.
- Không mất kết nối ngoài dự kiến; pong timeout dưới1%; đối chiếu tick/CPU/RAM server.
- Có kết quả hợp lệ: một người thắng **hoặc hòa/đồng hạng khi chết cùng tick**. Không ép phải luôn có một winner.
- Replay sang MatchId mới, view cũ được dọn; history chứa đúng trận mới sau outbox ACK.
- Thử hai Guest cùng tên: PlayerSessionId khác nhau; lịch sử không trộn hai hồ sơ trên hai PC. Đóng/mở client để kiểm khôi phục Guest cục bộ.
- Rời phòng chủ động trả slot ngay. Với resume, giữ app đang chạy, làm gián đoạn mạng rồi nối lại trong grace15s(main)/1s(test) tính từ lúc server phát hiện; proof ở RAM không tự khôi phục sau khi kill/mở lại app.
- Mã sai, phòng đầy và late join bị từ chối; Auto không dùng đường khác để vượt giới hạn.

## 5. Biên bản nghiệm thu riêng

Ghi ngày/UTC, tên hai PC, bản build/hash, mạng mỗi bên, host, transport, direct/relay, MatchId, file report của từng máy và kết luận PASS/FAIL/BLOCKED/INVALID. Không chép credential vào biên bản.

Hai report một-PC giữ nguyên NOT_TESTED. Chỉ biên bản có bằng chứng hai thiết bị mới đánh dấu bài hai-PC đã đạt. Xong buổi tự host thì stop đúng stack; AWS phải Stop và xác nhận Stopped, không chỉ đóng SSH hoặc game.
