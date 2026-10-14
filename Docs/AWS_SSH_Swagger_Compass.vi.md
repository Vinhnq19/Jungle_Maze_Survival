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

Nếu mạng không cho SSH trực tiếp, dùng [quy trình xử lý mạng chặn ở mục 7](#7-xử-lý-khi-mạng-chặn-truy-cập-ec2), gồm terminal trên trình duyệt và Compass qua Session Manager.

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
 j

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

| Kết nối | URI                           | Điều kiện                           |
| --------- | ----------------------------- | -------------------------------------- |
| AWS       | `mongodb://127.0.0.1:27019` | EC2 + Mongo chạy, SSH tunnel còn mở |
| Local     | `mongodb://127.0.0.1:27018` | Mongo local trên Docker Desktop chạy |

Nếu recreate Mongo, IP container có thể đổi: Ctrl+C tunnel cũ, chạy lại block để lấy IP mới. Sau Stop/Start EC2 cũng mở lại tunnel.

## 5. Kết thúc buổi test

1. Ngắt kết nối Compass.
2. Ctrl+C trong cửa sổ tunnel để đóng tunnel.
3. Trong cửa sổ SSH, gõ `exit` để thoát.
4. Xác nhận không còn ai dùng server, rồi AWS Console → Instance state → Stop instance → Stop. Đợi trạng thái Stopped.

Đóng trình duyệt, SSH, Unity hoặc dừng container **không dừng EC2**. EC2 Running tiếp tục phát sinh usage kể cả không có người chơi. Stop giữ dữ liệu trong volume/EBS, nhưng EBS vẫn phát sinh usage lưu trữ. Theo dõi credits và hạn Free Plan trong Billing; test/debug thường ngày trên local để tiết kiệm.

## 6. Xử lý lỗi thường gặp

| Lỗi                                      | Cách kiểm tra / xử lý                                                                                                                                                                            |
| ----------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| SSH timeout                               | EC2 Running, Public IP đúng, SG TCP 22 cho phép Public IP hiện tại của bạn. Mạng đổi thì rule My IP có thể cần cập nhật.                                                             |
| DuckDNS chưa trỏ đúng                 | Đợi dịch vụ DNS cập nhật. Trong PowerShell dùng`Resolve-DnsName junglemaze.duckdns.org`; có thể truy cập tạm `ssh -o HostName=PUBLIC_IPV4 junglemaze` sau khi thay PUBLIC_IPV4 thật. |
| Unprotected private key / bad permissions | Quyền file key quá rộng. Máy Vinh đã sửa chỉ Vinh được đọc. Nếu tái xuất hiện, kiểm tra ACL và chỉ cấp đọc cho tài khoản sở hữu; không chia sẻ key.                     |
| Permission denied (publickey)             | Kiểm tra đúng key của instance và user ubuntu. Alias không thay thế key hợp lệ.                                                                                                             |
| Swagger hỏi mật khẩu liên tục        | Dùng tài khoản bảo vệ Swagger trong configure.sh, không dùng account player.                                                                                                                  |
| Swagger 502 hoặc health 503              | Kiểm tra status.sh và logs API/Mongo/Caddy trên EC2.                                                                                                                                              |
| Compass connection refused                | Tunnel phải còn mở, đúng URI 27019, Mongo phải chạy. Kiểm tra lỗi trong terminal tunnel.                                                                                                    |
| Tunnel báo address already in use        | Cổng 27019 đang có tunnel/app khác. Dùng tunnel đang hoạt động hoặc Ctrl+C đúng cửa sổ cũ trước khi mở lại.                                                                       |
| Compass không thấy account              | Kiểm tra đang kết nối AWS 27019, Refresh, và đã register qua endpoint AWS. Account local không tự chuyển lên AWS.                                                                         |

Khi cần hỗ trợ, gửi lỗi/status đã che secrets. Không gửi private key, DuckDNS token, JWT, mật khẩu hay toàn bộ file `.env.aws`.

## 7. Xử lý khi mạng chặn truy cập EC2

Cập nhật phương án ngày **14/10/2026**. Các bước dưới đây do admin thực hiện trên máy đã có; không tạo EC2 mới hay sửa code game. Chưa cấu hình hoặc nghiệm thu Instance Connect/Session Manager trên tài khoản thật trong phiên viết tài liệu.

### 7.1. Chọn phương án

| Nhu cầu                                  | Phương án                                      | Điều kiện                                                               |
| ----------------------------------------- | ------------------------------------------------- | -------------------------------------------------------------------------- |
| Cần SSH/SCP/Compass ngay                 | Hotspot hoặc mạng khác được phép sử dụng | Cập nhật My IP trong Security Group cho mạng mới                       |
| Cần mở terminal để sửa DNS/dịch vụ | EC2 Instance Connect trên trình duyệt          | IAM, gói Instance Connect, SSH của server và rule cho dịch vụ AWS     |
| Thường xuyên dùng mạng chặn TCP 22  | Session Manager qua HTTPS                         | IAM role của EC2, SSM Agent, quyền người vận hành và HTTPS outbound |
| Compass khi TCP 22 bị chặn              | Session Manager port forwarding                   | AWS CLI + plugin trên PC, SSM Agent phù hợp và quyền tunnel           |

Chưa kết luận mạng chặn chỉ từ một lần timeout. Không chuyển SSH sang 443 trên máy này: Caddy đang sử dụng cổng đó. Mở thêm inbound cổng 22 không khắc phục mạng phía PC chặn outbound 22. Đường truy cập thay thế vẫn cần mạng cho phép HTTPS/WebSocket tới AWS.

### 7.2. Phân biệt DNS, TCP và xác thực

**AWS Console:** kiểm tra EC2 Running, status checks đạt; lấy Public IPv4 hiện tại, không dùng lại IP cũ sau Stop/Start. Xác nhận Security Group đang gắn vào đúng instance có SSH 22 từ My IP hiện tại.

**PC / PowerShell**, nhập IP vừa lấy:

```powershell
$ec2Ip = Read-Host 'Public IPv4 hien tai cua EC2'
Resolve-DnsName junglemaze.duckdns.org -Type A
Test-NetConnection -ComputerName $ec2Ip -Port 22
ssh -o ConnectTimeout=10 -o "HostName=$ec2Ip" junglemaze
```

| Kết quả                                          | Diễn giải / bước tiếp                                                                                           |
| -------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| DNS lỗi nhưng SSH bằng IP được               | Sửa DuckDNS theo mục 7.4; chưa cần thay phương thức SSH                                                       |
| TcpTestSucceeded=True, Permission denied/publickey | Đường TCP tới được; kiểm tra key/user/quyền file                                                            |
| TcpTestSucceeded=False hoặc timeout               | Kiểm tra IP, SG, đường mạng, SSH daemon; chưa xác định lỗi ở riêng PC hay AWS                            |
| Hotspot SSH được, mạng cũ không được      | Sau khi cho phép đúng My IP của từng mạng, đây là bằng chứng mạnh về hạn chế trên đường mạng cũ |
| Cả hai mạng đều timeout                        | Kiểm tra phía AWS/server trước: SG, public subnet/route Internet Gateway, NACL tùy chỉnh và ssh.service       |

Nếu thử hotspot, cập nhật source My IP cho mạng mới. Không mở SSH cho `0.0.0.0/0` chỉ để bỏ qua bước tìm nguyên nhân. Kiểm tra TCP 443 tới website chỉ chứng minh đường HTTPS, không chứng minh SSH hay UDP game dùng được.

### 7.3. Vào bằng EC2 Instance Connect qua trình duyệt

Luồng: **PC → HTTPS/WebSocket 443 → dịch vụ Instance Connect AWS → TCP 22 → EC2**. Vì vậy phía EC2 vẫn cần ssh.service chạy; chỉ phía PC không cần kết nối trực tiếp tới 22. Trình duyệt cần tới được endpoint `prod.ap-southeast-2.oneclickv2-proxy.ec2.aws.dev`. [Điều kiện kết nối](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/ec2-instance-connect-prerequisites.html)

**AWS Console**, region Sydney:

1. EC2 → Instances → chọn instance hiện tại → Security → mở Security Group đang gắn.
2. Edit inbound rules → Add rule → Type SSH, TCP 22, Source Custom.
3. Trong ô source, chọn **managed prefix list** `com.amazonaws.ap-southeast-2.ec2-instance-connect` (IPv4), rồi Save rules. Đây là nguồn dịch vụ AWS, khác My IP của bạn. Giữ rule My IP nếu vẫn dùng SSH trực tiếp.
4. Quay về instance → Connect → EC2 Instance Connect → Connect using a Public IP → username `ubuntu` → Connect.
5. Terminal mở trong trình duyệt: dùng các lệnh quản trị Ubuntu như mục 2. Không cần upload private key vào trình duyệt.

Không tạo instance/endpoint mới cho nhánh này. Instance cần có gói `ec2-instance-connect`. Nếu báo thiếu quyền, admin cấp `ec2-instance-connect:SendSSHPublicKey` cho đúng instance và user ubuntu, cùng quyền xem instance; không cấp AdministratorAccess chỉ để sửa lỗi kết nối. [Hướng dẫn AWS](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/ec2-instance-connect-tutorial.html)

Nếu thất bại: kiểm tra IAM, package, SG đúng nguồn dịch vụ, route/NACL và SSH daemon. Nếu cần cài package, vào bằng hotspot hoặc Session Manager đã có, chạy `sudo apt-get install ec2-instance-connect`. Nếu cả agent lẫn SSH đều không truy cập được, dùng đường dự phòng đã hoạt động để sửa; thay DNS đơn thuần không giải quyết SSH daemon hỏng.

### 7.4. Sửa DNS sau khi đã vào được server

**Terminal Ubuntu trên EC2**, bất kể vào từ SSH, Instance Connect hay Session Manager:

```bash
sudo systemctl restart junglemaze-dns.service
sudo journalctl -u junglemaze-dns.service -n 20 --no-pager
sudo systemctl enable --now junglemaze-dns.timer
```

Kỳ vọng `DuckDNS updated: junglemaze.duckdns.org`. Nếu update thất bại, kiểm tra domain trong tài khoản DuckDNS, token/config root và HTTPS outbound; không in token hay toàn bộ `.env.aws`. Nếu domain đã bị xóa, khôi phục/tạo lại đúng domain nếu còn khả dụng trước khi cập nhật.

**PC / PowerShell:**

```powershell
Clear-DnsClientCache
Resolve-DnsName junglemaze.duckdns.org -Type A
```

IP phải khớp Public IPv4 hiện tại. DNS có thể cần thời gian hết cache. Khi DNS đúng nhưng TCP 22 vẫn timeout, tiếp tục nhánh Instance Connect/Session Manager, không sửa DNS lặp lại.

### 7.5. Thiết lập Session Manager làm đường dự phòng

SSM Agent trên EC2 chủ động kết nối AWS qua HTTPS. Session Manager không yêu cầu mở inbound 22. Thiết lập IAM/agent trước, sau đó mới chuyển cách đăng nhập. Nếu Console của Free Plan báo tính năng không khả dụng hoặc yêu cầu nâng cấp, giữ nguyên plan và dùng hotspot/Instance Connect thay thế; không tự activate advanced features. [Điều kiện SSM](https://docs.aws.amazon.com/systems-manager/latest/userguide/session-manager-prerequisites.html)

**A. Gắn quyền cho EC2 — AWS Console**

1. IAM → Roles → Create role → Trusted entity AWS service → Use case EC2.
2. Thêm policy **AmazonSSMManagedInstanceCore**, đặt tên `JungleMazeEC2SSMRole`, tạo role.
3. EC2 → chọn instance → Actions → Security → Modify IAM role → chọn role vừa tạo → Update IAM role.
4. Nếu instance đã có role, admin bổ sung policy cần thiết vào role hiện tại thay vì thay thế làm mất quyền khác. Không đưa access key của người dùng lên EC2.

Role này là quyền **của máy EC2**, không phải quyền đăng nhập **của dev**. Người vận hành vẫn cần IAM cho phép bắt đầu/kết thúc session đúng instance và session document. Admin tham khảo [quyền instance](https://docs.aws.amazon.com/systems-manager/latest/userguide/session-manager-getting-started-instance-profile.html) và [quyền người dùng](https://docs.aws.amazon.com/systems-manager/latest/userguide/getting-started-restrict-access-quickstart.html).

**B. Kiểm tra agent — terminal Ubuntu từ Instance Connect/hotspot**

```bash
sudo snap list amazon-ssm-agent
sudo systemctl status snap.amazon-ssm-agent.amazon-ssm-agent.service --no-pager
```

Nếu Snap agent đã có nhưng chưa chạy:

```bash
sudo systemctl enable --now snap.amazon-ssm-agent.amazon-ssm-agent.service
```

Nếu không thấy Snap, kiểm tra `sudo systemctl status amazon-ssm-agent --no-pager` để xác định có bản deb hay chưa. Chỉ khi cả hai đều chưa cài mới dùng:

```bash
sudo snap install amazon-ssm-agent --classic
sudo systemctl enable --now snap.amazon-ssm-agent.amazon-ssm-agent.service
```

Không chạy đồng thời hai bản agent. Với Ubuntu 24.04, AWS hướng dẫn dùng Snap. [Cài và kiểm tra agent Ubuntu](https://docs.aws.amazon.com/systems-manager/latest/userguide/agent-install-ubuntu-64-snap.html)

**C. Kiểm tra mạng và mở session**

EC2 cần DNS và outbound HTTPS 443 tới `ssm.ap-southeast-2.amazonaws.com` và `ssmmessages.ap-southeast-2.amazonaws.com`; agent cũ có thể cần endpoint `ec2messages`. Nhánh dự án dùng public subnet/Internet Gateway hiện tại. Không tạo thêm NAT Gateway hoặc Interface VPC Endpoint chỉ cho bước này. [Kết nối mạng SSM](https://docs.aws.amazon.com/systems-manager/latest/userguide/setup-create-vpc.html)

Đợi vài phút → EC2 → instance → Connect → **Session Manager** → Connect. Shell có thể dùng user `ssm-user`, không phải ubuntu; các lệnh quản trị vẫn chạy bằng sudo. Nếu node không online, kiểm tra agent log, role gắn đúng instance, HTTPS/DNS và đúng region. Instance Online nhưng AccessDenied thường liên quan quyền người vận hành.

Nếu cần chẩn đoán sâu, trong terminal EC2 chạy các lệnh đọc trạng thái sau, không tắt firewall hoặc đổi SSH port:

```bash
sudo systemctl status ssh --no-pager
sudo ss -lntp 'sport = :22'
sudo ufw status verbose
sudo journalctl -u snap.amazon-ssm-agent.amazon-ssm-agent.service -n 40 --no-pager
```

Nếu dùng agent bản deb, thay tên unit cuối bằng `amazon-ssm-agent.service`. Role/agent đúng nhưng node vẫn offline: kiểm tra thêm `/var/log/amazon/ssm/amazon-ssm-agent.log` và thời gian hệ thống. Không gửi nguyên log nếu có dữ liệu nhạy cảm.

Chưa bỏ rule SSH hay đường dự phòng đang hoạt động trước khi mở được session SSM thực tế. Với mạng kiểm soát WebSocket/proxy nghiêm ngặt, quản trị mạng có thể cần cho phép các endpoint AWS; HTTPS qua 443 không bảo đảm mọi proxy đều cho kết nối.

### 7.6. Compass qua Session Manager khi không dùng SSH trực tiếp được

Luồng: **Compass → PC 27019 → Session Manager qua HTTPS → EC2 → IP Mongo Docker:27017**. Không publish Mongo ra Internet và không sửa compose.aws.yaml.

**Chuẩn bị một lần trên PC:**

1. Cài [AWS CLI v2](https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html) và [Session Manager plugin Windows](https://docs.aws.amazon.com/systems-manager/latest/userguide/install-plugin-windows.html) từ AWS. Mở PowerShell mới sau cài.
2. Chuẩn bị profile admin được cấp quyền session cho instance và document `AWS-StartPortForwardingSessionToRemoteHost`. Không dùng tài khoản root cho vận hành hằng ngày.
3. Với CLI **2.32.0 trở lên**, có thể đăng nhập bằng trình duyệt, không cần tạo access key dài hạn:

```powershell
aws --version
session-manager-plugin --version
aws login --profile junglemaze-admin
aws sts get-caller-identity --profile junglemaze-admin
```

Chọn đúng tài khoản AWS và region `ap-southeast-2` nếu được hỏi. IAM identity cần quyền `SignInLocalDevelopmentAccess` để dùng aws login, ngoài quyền SSM. Nếu team đã có profile tạm thời/SSO, dùng profile đó thay vì tạo mới. Không thiết lập AWS Organizations/Identity Center chỉ để giải quyết SSH. [Đăng nhập CLI bằng console credentials](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-sign-in.html)

**Mỗi lần cần Compass:**

1. Trong terminal EC2 (Session Manager hoặc Instance Connect), lấy IP container Mongo:

```bash
sudo docker inspect --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' junglemaze-aws-mongo-1
```

2. Trên PC, nhập **Instance ID** dạng `i-...` từ AWS Console (không nhập Public IP) và IP Mongo vừa lấy:

```powershell
$ssmInstanceId = Read-Host 'Instance ID EC2 (i-...)'
$mongoIp = Read-Host 'IP container Mongo vua lay'
aws ssm start-session --profile junglemaze-admin --region ap-southeast-2 --target $ssmInstanceId --document-name AWS-StartPortForwardingSessionToRemoteHost --parameters "host=$mongoIp,portNumber=27017,localPortNumber=27019"
```

3. Giữ terminal mở khi có thông báo port forwarding. Compass kết nối `mongodb://127.0.0.1:27019` như mục 4.2. Đóng tunnel SSH cũ nếu cổng đã bị chiếm.
4. Xem xong ngắt Compass, Ctrl+C kết thúc tunnel; kiểm tra danh sách Session Manager và terminate đúng phiên của mình nếu còn active. Không dừng phiên của người khác.

Nhánh remote-host forwarding cần SSM Agent **3.1.1374.0 trở lên**. EC2 host phải tới được IP Mongo bridge; lấy lại IP sau recreate container. Document này cho phép truy cập đích mạng từ EC2 nên chỉ cấp cho admin DB được phép. `TargetNotConnected`: kiểm tra agent/role/region; `AccessDenied`: quyền identity/document; plugin not found: cài plugin và mở terminal mới. [Port forwarding qua Session Manager](https://docs.aws.amazon.com/systems-manager/latest/userguide/session-manager-working-with-sessions-start.html)

### 7.7. Nghiệm thu, giới hạn và chi phí

- Từ mạng đang chặn SSH, mở được Instance Connect hoặc Session Manager và chạy status.sh.
- DNS trỏ đúng Public IPv4; Swagger /health hoạt động qua HTTPS.
- Compass qua SSM đọc được users đăng ký trên AWS; sau đóng tunnel, kết nối Compass ngắt.
- Không thay đổi rule UDP 7777 hoặc code gameplay chỉ để sửa đường quản trị. SSM port forwarding là TCP, không chuyển tiếp UDP LiteNetLib. Nếu mạng lớp chặn UDP, cần đo/test bằng NetSmokeClient trên mạng được phép; truy cập được Swagger chưa chứng minh chơi game được.
- Dịch vụ Session Manager cho EC2 không có phụ phí sử dụng theo bảng giá AWS, nhưng EC2/EBS/IPv4 và dịch vụ phụ trợ vẫn có usage. Không bật thêm CloudWatch/S3 logging, NAT/endpoint theo mặc định trong phương án này. Luôn Stop EC2 khi hết lịch test; không nâng cấp plan chỉ để mở đường quản trị. [Giá Systems Manager](https://aws.amazon.com/systems-manager/pricing/)

Đây là hướng dẫn thiết lập và tiêu chí kiểm tra, không phải xác nhận các đường truy cập mới đã được cấu hình trên AWS. Thực hiện mục 7.2 rồi 7.3 trước; chỉ triển khai 7.5–7.6 nếu cần đường quản trị thường xuyên từ mạng chặn SSH.

### 7.8. Bộ kết nối trên máy Vinh đã thiết lập ngày 14/10/2026

Phần 7.1–7.7 là quy trình tổng quát cho các máy khác. Riêng máy Vinh đã có bộ công cụ tại `D:/AWS/ConnectionSetup`, profile đăng nhập AWS CLI tạm thời `junglemaze-admin` và các shortcut Desktop:

| Shortcut | Thao tác |
| --- | --- |
| Jungle Maze - Swagger Auto | Tra IP hiện tại qua AWS API, sửa riêng ánh xạ hostname nếu cần (UAC), kiểm tra HTTPS health rồi mở Swagger |
| Jungle Maze - Compass | Lấy IP Mongo hiện tại bằng SSM Run Command, mở tunnel HTTPS vào cổng PC 27019 rồi chạy Compass |
| Jungle Maze - Close Compass Tunnel | Kết thúc đúng phiên tunnel SSM và đóng process local, giữ EC2 chạy |

Ưu tiên Swagger Auto thay vì shortcut URL thuần khi EC2 vừa Stop/Start. Do DNS Wi-Fi trả sai tên miền, máy dùng override trong hosts; helper kiểm chứng TLS và health trước khi cập nhật, giữ bản sao lưu. Không đổi DNS toàn máy. Khi DNS mạng hoạt động bình thường, có thể gỡ riêng dòng override Jungle Maze.

Alias `ssh junglemaze` đã chuyển qua **Session Manager/HTTPS**, dùng instance ID, private key sẵn có và HostKeyAlias để giữ kiểm tra host key. Không phụ thuộc DuckDNS hoặc kết nối trực tiếp TCP 22 từ PC. Cấu hình GitLab trong SSH giữ nguyên. Khi AWS login hết hạn, đăng nhập lại trong trình duyệt:

```powershell
& 'D:/AWS/ConnectionSetup/AWSCLI-portable/aws.exe' login --profile junglemaze-admin --region ap-southeast-2
```

Các helper cũng có thể chạy từ PowerShell tại bất kỳ thư mục nào. Chọn đúng thao tác cần dùng.

Mở Swagger:

```powershell
& 'D:/AWS/ConnectionSetup/Open-JungleMazeSwagger.ps1'
```

Mở Compass:

```powershell
& 'D:/AWS/ConnectionSetup/Open-JungleMazeCompass.ps1'
```

Đóng tunnel sau khi dùng xong:

```powershell
& 'D:/AWS/ConnectionSetup/Close-JungleMazeCompass.ps1'
```

Không chạy cả ba lệnh liên tiếp nếu muốn giữ Compass mở: dùng Open khi bắt đầu, Close khi kết thúc. Nếu cổng 27019 đang có tunnel, giữ tunnel đó hoặc đóng bằng helper rồi mở lại. Hướng dẫn trên PC ở `D:/AWS/ConnectionSetup/README.vi.md`.

EC2 dùng role `JungleMazeEC2SSMRole` / instance profile `JungleMazeEC2SSMProfile`, policy `AmazonSSMManagedInstanceCore`; agent đã báo Online. Helpers chỉ dành cho admin có quyền SSM Run Command/Session Manager, không chia sẻ profile hoặc key cho tester. AWS CLI và plugin được giải nén từ bộ chính thức có chữ ký AWS, nằm riêng trong thư mục công cụ; không cài thêm Windows service hay sửa PATH hệ thống.

Trong phiên thiết lập đã kiểm chứng HTTPS health, `ssh junglemaze` qua SSM, Mongo ping và collection users qua tunnel; quan sát thấy kết nối Compass trong MongoDB trên EC2. Người dùng xác nhận đã mở được danh sách API Swagger. Không sửa dữ liệu account. Endpoint Instance Connect cùng rule/SG tạm dùng để khởi động lại agent đã được gỡ; đường dùng hằng ngày là SSM. Không cần deploy lại backend cho thay đổi kết nối PC này.

Các xác nhận trên là kết quả tại thời điểm thiết lập, không thay thế kiểm tra Running/SSM Online mỗi buổi. EC2 không tự Stop khi đóng app hoặc tunnel; admin vẫn Stop trong AWS Console khi team dùng xong.
