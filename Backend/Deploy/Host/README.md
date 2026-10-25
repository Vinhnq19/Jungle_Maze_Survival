# Tự host backend Local / VPN / public

Hướng dẫn đầy đủ: [Phase 2, mục6–9](../../../Docs/Phase2_Match_Guest_Zone.vi.md#6-chọn-môi-trường-online). Chạy các lệnh dưới từ `Backend`. Đây là công cụ chuẩn bị host; chỉ ghi PASS kết nối từ xa khi đã chạy checklist với thiết bị thật.

## Tạo cấu hình một lần

```powershell
# Docker Desktop đang chạy. Script hỏi mật khẩu Swagger >=12 ký tự.
powershell -File Deploy/Host/configure-host.ps1 -Mode vpn
# Hoặc public, dùng domain RIÊNG, không dùng hostname AWS:
powershell -File Deploy/Host/configure-host.ps1 -Mode public -Domain your-host.duckdns.org -AcmeEmail you@example.com
```

Script tạo ba secret riêng, mã phòng mặc định và bcrypt qua stdin; không ghi password vào dòng lệnh. File `.env.host.<mode>` chỉ current-user đọc và không nằm trong Git. Có file rồi thì giữ nguyên, không tự thay key/DB. Không copy secret từ `.env` Local hoặc AWS.

## VPN

Cài và đăng nhập Tailscale trên host và máy chơi, bật HTTPS/Serve theo hướng dẫn Phase2. API/WSS dùng hostname HTTPS `.ts.net` hợp lệ; UDP dùng IP100.x của host.

```powershell
powershell -File Deploy/Host/preflight-host.ps1 -Mode vpn -EnvFile Deploy/Host/.env.host.vpn
docker compose -f compose.host.yaml --env-file Deploy/Host/.env.host.vpn --profile vpn up -d --build
tailscale serve --bg http://127.0.0.1:8080
```

`caddy-vpn` chỉ publish loopback8080; không mở8080 từ mạng ngoài. Trong PowerShell **Administrator**, chỉ khi cần mở rule riêng cho UDP VPN:

```powershell
powershell -File Deploy/Host/setup-firewall.ps1 -Action Enable -Mode Vpn
```

## Public

DNS phải trỏ về host; router forward TCP80/443 và UDP7777. CGNAT hoặc chính sách mạng chặn inbound cần giải quyết trước, không coi compose là đã mở được Internet.

```powershell
powershell -File Deploy/Host/preflight-host.ps1 -Mode public -EnvFile Deploy/Host/.env.host.public
docker compose -f compose.host.yaml --env-file Deploy/Host/.env.host.public --profile public up -d --build
# Chạy riêng trong PowerShell Administrator:
powershell -File Deploy/Host/setup-firewall.ps1 -Action Enable -Mode Public
```

Preflight dùng file env thực, kiểm tra secret và render compose. `-StaticOnly` bỏ kiểm tra host đang chạy; kết quả đó không chứng minh kết nối từ xa.

## Dừng và giữ dữ liệu

```powershell
docker compose -f compose.host.yaml --env-file Deploy/Host/.env.host.vpn --profile vpn down
# Public: đổi cả env-file và --profile thành public.
tailscale serve reset
# Administrator, chỉ gỡ rule của nhóm JungleMaze-Host:
powershell -File Deploy/Host/setup-firewall.ps1 -Action Disable
```

Không thêm `-v`: Mongo/outbox có volume riêng của `junglemaze-host`. Dừng GameServer dev đang chiếm UDP7777 trước khi bật host này. Không publish Mongo27017, API5000, control7780 hoặc WS7781. Caddy chặn `/internal`, chỉ Swagger yêu cầu Basic auth. Tạo/list/xóa phòng vẫn yêu cầu JWT account; Guest chỉ chơi và xem lịch sử của mình.
