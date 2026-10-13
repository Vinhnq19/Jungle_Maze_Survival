# Đăng nhập EC2, test Swagger và xem MongoDB bằng Compass

Hướng dẫn dùng server đã triển khai tại `junglemaze.duckdns.org`. Không cần mở Unity, chạy project .NET hay Docker Desktop trên PC để test API và xem DB AWS. EC2 cần chạy Docker và MongoDB. Phần setup/deploy đầy đủ ở [hướng dẫn chính](Phase1_First4Tasks.vi.md).

## 1. Chuẩn bị một lần trên PC

- Có MongoDB Compass và Windows OpenSSH (`ssh` chạy được trong PowerShell).
- Có private key EC2 của chính bạn. Máy Vinh dùng `D:/AWS/junglemaze.pem`.
- Security Group của EC2 cho phép TCP 22 từ Public IP của máy bạn, TCP 80/443 cho API HTTPS. Không mở MongoDB 27017/27018/27019 ra Internet.
- Có username/password bảo vệ Swagger đã nhập khi chạy `configure.sh`.

### Alias SSH

Máy Vinh đã có cấu hình sau trong `C:/Users/Vinh/.ssh/config`. Dev khác tạo `%USERPROFILE%/.ssh/config`, thêm block này và thay đường dẫn key của mình. Giữ cấu hình host khác, lưu file tên `config`, không có đuôi `.txt`.

```sshconfig
Host junglemaze
    HostName junglemaze.duckdns.org
    User ubuntu
    IdentityFile D:/AWS/junglemaze.pem
    IdentitiesOnly yes
    ServerAliveInterval 30
    ServerAliveCountMax 3
```

Kiểm tra cấu hình mà không kết nối server, trong **PowerShell trên PC**:

```powershell
ssh -G junglemaze | Select-String '^(hostname|user|identityfile) '
```

Không chia sẻ private key. Dev được cấp quyền quản trị cần key riêng và public key được admin thêm trên server.

## 2. Bật EC2 và đăng nhập

**AWS Console:** chọn region Sydney `ap-southeast-2` → EC2 → Instances → chọn đúng máy → Instance state → Start instance. Đợi Running và status checks đạt. Sau khi boot, dịch vụ cập nhật DuckDNS về Public IPv4 mới.

**PowerShell trên PC:**

```powershell
ssh junglemaze
```

Khi thấy prompt `ubuntu@ip-...`, bạn đang ở EC2. Lần đầu kết nối, đối chiếu fingerprint với AWS Console → chọn instance → Actions → Monitor and troubleshoot → Get system log, tìm SSH host key fingerprints; đúng mới nhập đầy đủ `yes`. Nếu xuất hiện cảnh báo host key thay đổi, kiểm tra lại instance/fingerprint trước khi sửa known_hosts.

**Trong SSH EC2**, kiểm tra dịch vụ:

```bash
sudo bash /opt/junglemaze/current/scripts/status.sh
sudo docker ps --format 'table {{.Names}}\t{{.Status}}'
```

Mongo và API cần healthy, Caddy và GameServer cần đang chạy. Nếu dịch vụ chưa chạy:

```bash
sudo systemctl start junglemaze
```

Nếu start thất bại, đọc log thay vì chạy deploy lại:

```bash
sudo journalctl -u junglemaze -n 80 --no-pager
```

## 3. Test API bằng Swagger

**Trình duyệt trên PC:** mở [Swagger AWS](https://junglemaze.duckdns.org/swagger).

Trình duyệt hỏi username/password của Swagger: dùng thông tin đã tạo trong `configure.sh`. Đây là Basic auth bảo vệ trang, khác tài khoản người chơi và JWT ở nút Authorize.

### 3.1. Kiểm tra sức khỏe

Mở GET `/health` → Try it out → Execute. Mong đợi HTTP 200:

```json
{
  "status": "ok",
  "database": "connected"
}
```

Cũng có thể mở trực tiếp [health](https://junglemaze.duckdns.org/health), không cần tài khoản Swagger. HTTP 503 nghĩa là chưa kết nối DB thành công.

### 3.2. Đăng ký

POST `/api/auth/register` → Try it out → thay body:

```json
{
  "username": "player01",
  "password": "TestGame123!"
}
```

Execute → 201 là thành công. Username dài 3–32 ký tự sau trim, password 8–128 ký tự. 409 nghĩa tên đã tồn tại: dùng tên khác hoặc chuyển sang login. 400 nghĩa body không hợp lệ. Register không tự đăng nhập.

### 3.3. Đăng nhập và lấy JWT

POST `/api/auth/login` → Try it out → dùng username/password vừa đăng ký → Execute.

HTTP 200 trả `accessToken`, `userId`, `expiresAtUtc`. Copy **giá trị accessToken**, không lấy dấu ngoặc kép. HTTP 401 nghĩa thông tin đăng nhập sai.

### 3.4. Xem thông tin người chơi

Nhấn Authorize ở đầu trang → dán token → Authorize → Close. Chỉ dán token, Swagger tự thêm tiền tố Bearer.

GET `/api/me` → Try it out → Execute. Mong đợi 200 và thông tin account. Nếu 401, login để lấy token mới rồi Authorize lại. Có thể chọn Logout trong Authorize và gọi lại `/api/me` để xác nhận thiếu token bị từ chối.

Register/login dùng chung giới hạn 60 request/phút toàn server AWS. Nếu 429, ngừng gửi và chờ sang cửa sổ giới hạn tiếp theo. Không chạy stress test đăng ký trên server lớp.

Đây là test account API. Public GameServer dùng RoomCode; JWT Swagger không dùng để join UDP public. Test UDP thực hiện riêng bằng NetSmokeClient.

## 4. Xem MongoDB AWS bằng Compass

DB AWS khác DB local. Account đăng ký qua Swagger AWS được lưu trong DB `jungle_maze` trên EC2.

### 4.1. Mở tunnel tự lấy IP Mongo

Mở **một cửa sổ PowerShell mới trên PC**, không chạy đoạn này trong SSH Ubuntu. Chạy cả block:

```powershell
$mongoIpLines = @(ssh junglemaze "sudo docker inspect --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' junglemaze-aws-mongo-1")
if ($LASTEXITCODE -ne 0) {
    throw 'Khong lay duoc IP Mongo. Kiem tra SSH va container Mongo tren EC2.'
}
$mongoIp = ($mongoIpLines -join '').Trim()
$parsedMongoIp = $null
if (-not [System.Net.IPAddress]::TryParse($mongoIp, [ref]$parsedMongoIp)) {
    throw 'IP Mongo khong hop le. Kiem tra container dang chay.'
}
ssh junglemaze -N -o ExitOnForwardFailure=yes -L "127.0.0.1:27019:${mongoIp}:27017"
```

Block tự lấy IP container rồi mở tunnel. Terminal đứng yên không có prompt là bình thường. **Giữ cửa sổ này mở trong lúc dùng Compass.** Tunnel chuyển kết nối từ cổng 27019 trên PC tới Mongo private 27017 qua SSH; không cần mở cổng DB trong Security Group.

### 4.2. Kết nối Compass

Compass → Add new connection → nhập:

```text
mongodb://127.0.0.1:27019
```

Lưu tên **Jungle Maze AWS** → Connect → `jungle_maze` → `users` → Documents. Nhấn Refresh sau khi đăng ký qua Swagger.

- Filter `{}`: xem toàn bộ users.
- Filter `{ "NormalizedUsername": "PLAYER01" }`: tìm account mẫu.
- Project `{ "PasswordHash": 0 }`: ẩn hash khi xem/chụp màn hình.
- Sort `{ "CreatedAtUtc": -1 }`: xem account mới nhất trước.

Tạo tài khoản bằng API để giữ đúng validation và hash mật khẩu. Không chỉnh/xóa trực tiếp dữ liệu dùng chung nếu chỉ đang kiểm tra. Chưa đăng ký account trên AWS thì collection có thể rỗng.

| Kết nối | URI | Điều kiện |
| --- | --- | --- |
| AWS | `mongodb://127.0.0.1:27019` | EC2 + Mongo chạy, SSH tunnel còn mở |
| Local | `mongodb://127.0.0.1:27018` | Mongo local trên Docker Desktop chạy |

Nếu recreate Mongo, IP container có thể đổi: Ctrl+C tunnel cũ, chạy lại block để lấy IP mới. Sau Stop/Start EC2 cũng mở lại tunnel.

## 5. Kết thúc buổi test

1. Ngắt kết nối Compass.
2. Ctrl+C trong cửa sổ tunnel để đóng tunnel.
3. Trong cửa sổ SSH, gõ `exit` để thoát.
4. Xác nhận không còn ai dùng server, rồi AWS Console → Instance state → Stop instance → Stop. Đợi trạng thái Stopped.

Đóng trình duyệt, SSH, Unity hoặc dừng container **không dừng EC2**. EC2 Running tiếp tục phát sinh usage kể cả không có người chơi. Stop giữ dữ liệu trong volume/EBS, nhưng EBS vẫn phát sinh usage lưu trữ. Theo dõi credits và hạn Free Plan trong Billing; test/debug thường ngày trên local để tiết kiệm.

## 6. Xử lý lỗi thường gặp

| Lỗi | Cách kiểm tra / xử lý |
| --- | --- |
| SSH timeout | EC2 Running, Public IP đúng, SG TCP 22 cho phép Public IP hiện tại của bạn. Mạng đổi thì rule My IP có thể cần cập nhật. |
| DuckDNS chưa trỏ đúng | Đợi dịch vụ DNS cập nhật. Trong PowerShell dùng `Resolve-DnsName junglemaze.duckdns.org`; có thể truy cập tạm `ssh -o HostName=PUBLIC_IPV4 junglemaze` sau khi thay PUBLIC_IPV4 thật. |
| Unprotected private key / bad permissions | Quyền file key quá rộng. Máy Vinh đã sửa chỉ Vinh được đọc. Nếu tái xuất hiện, kiểm tra ACL và chỉ cấp đọc cho tài khoản sở hữu; không chia sẻ key. |
| Permission denied (publickey) | Kiểm tra đúng key của instance và user ubuntu. Alias không thay thế key hợp lệ. |
| Swagger hỏi mật khẩu liên tục | Dùng tài khoản bảo vệ Swagger trong configure.sh, không dùng account player. |
| Swagger 502 hoặc health 503 | Kiểm tra status.sh và logs API/Mongo/Caddy trên EC2. |
| Compass connection refused | Tunnel phải còn mở, đúng URI 27019, Mongo phải chạy. Kiểm tra lỗi trong terminal tunnel. |
| Tunnel báo address already in use | Cổng 27019 đang có tunnel/app khác. Dùng tunnel đang hoạt động hoặc Ctrl+C đúng cửa sổ cũ trước khi mở lại. |
| Compass không thấy account | Kiểm tra đang kết nối AWS 27019, Refresh, và đã register qua endpoint AWS. Account local không tự chuyển lên AWS. |

Khi cần hỗ trợ, gửi lỗi/status đã che secrets. Không gửi private key, DuckDNS token, JWT, mật khẩu hay toàn bộ file `.env.aws`.
