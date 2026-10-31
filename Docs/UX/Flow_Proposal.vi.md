# Đề xuất luồng UI mới — Lastaway

Ngày 31/10/2026 · UX/UI Designer · Figma `oeZgisxdiR80hRqbikUwPb`, trang "Lastaway — Màn hình trong game" (`116:3`). Prototype: flow **"Lastaway — Luồng chính"**, bắt đầu ở `01_login`.

Đánh giá dựa trên skill `ui-ux-pro-max` (nhóm Navigation, Forms & Feedback): `nav-hierarchy`, `progressive-disclosure`, `confirmation-dialogs`, `back-behavior`, `error-recovery`, `accessible-authentication`.

## 1. Sơ đồ luồng

```
01 Đăng nhập ⇄ 02 Đăng ký ──(thành công)──► 03 Home  [thẻ tài khoản góc phải]
   └ "Máy chủ (nâng cao)" ─► 05 Cài đặt · Máy chủ ─(Đóng = quay lại)

03 Home
 ├ Vào phòng ─► 06 Modal nhập mã ─► 09 Đang kết nối ─► 13 Phòng chờ ─► 14 Đếm ngược ─► 15 Tải ─► 17 HUD
 │               ├ mã sai ─► 07          └ Hủy ─► Home        └ lỗi ─► 16 ─► Home
 │               └ phòng đầy ─► 08 ─► Hủy ─► Home
 ├ Tạo phòng ─► 10 Quản lý phòng ─► Xóa ─► 11 xác nhận │ Tạo ─► 12 giới hạn │ Vào ─► 09
 ├ Lịch sử ─► 31 (32 trống, 33 tạm ngừng) ─► dòng trận ─► 34 Bảng xếp hạng
 ├ Bảng xếp hạng ─► 34 (35 trống, 36 tạm ngừng)
 ├ Cài đặt ─► 05
 └ Đăng xuất (thẻ tài khoản) ─► 04 Xác nhận ─► 01 Đăng nhập

17 HUD ─ESC─► 20 Menu ─Thoát trận─► 21 Xác nhận ─► Home   (Tiếp tục ─► HUD)
17 HUD ─► 18 nhặt đồ ─► 19 máu thấp ─► 22 Bị loại ─► 23 Killcam (24 không có) ─► 25 Theo dõi
       ─► 27 Thua │ 26 Thắng ─► Chơi lại ─► 28 Chờ người khác ─► 13 Phòng chờ │ Về trang chủ ─► Home
Mất mạng (từ HUD) ─► 29 Đang kết nối lại ─► 30 Thất bại ─► Home
```

## 2. Vấn đề → cách sửa

| # | Vấn đề (hiện trạng) | Cách sửa |
| --- | --- | --- |
| 1 | Đăng nhập chỉ mở khi bấm "Quản lý phòng"; khách vào phòng bằng mã không cần tài khoản | Đăng nhập/Đăng ký là màn đầu tiên. Chưa đăng nhập thì không tới được Home, không vào hay tạo được phòng |
| 2 | Bảng Máy chủ (host/port/API) bày thẳng ở Home, lấn chỗ nút chính | Gỡ khỏi Home. Đưa vào **Cài đặt · Máy chủ (nâng cao)**, mở từ nút Cài đặt ở Home hoặc nút "Máy chủ (nâng cao)" ở màn đăng nhập (đổi máy chủ trước khi đăng nhập) |
| 3 | Home không cho biết đang đăng nhập hay chưa, tài khoản nào | Thẻ tài khoản góc trên phải: "Đã đăng nhập", tên tài khoản, nút Đăng xuất |
| 4 | Đăng xuất nằm sâu trong Quản lý phòng, không xác nhận | Đăng xuất ở thẻ tài khoản, mở hộp xác nhận (Hủy / Đăng xuất), xong về màn đăng nhập. Gỡ nút Đăng xuất trong Quản lý phòng |
| 5 | "Vào phòng" là màn riêng, ghi "Không cần đăng nhập" | Thành **modal** trên Home: mã phòng + tên hiển thị (lấy sẵn từ tài khoản), nút Hủy về Home, dòng "Vào bằng tài khoản …" |
| 6 | "Quản lý phòng" không rõ là nơi tạo phòng | Nút Home đổi thành **"Tạo phòng"**, dẫn tới Quản lý phòng (form tạo ở dưới, danh sách phòng của mình ở trên) |
| 7 | Không có lối tới Bảng xếp hạng từ Home | Thêm nút "Bảng xếp hạng" (bảng của trận gần nhất, vì API chỉ có leaderboard theo trận) |
| 8 | Nhãn quay lại lẫn lộn "Về menu" / "Quay lại" / "Về sảnh" | Thống nhất "Về trang chủ" cho đường về Home; "Quay lại" chỉ cho màn con (Bảng xếp hạng); "Hủy" trong modal |
| 9 | Màn đăng nhập có "Quay lại" nhưng nó là màn đầu, không có chỗ quay về | Ẩn nút Quay lại ở Đăng nhập/Đăng ký; thêm logo game để màn đầu có nhận diện |
| 10 | Mọi lỗi phải có lối thoát | Đã có: mã sai/phòng đầy (sửa mã hoặc Hủy), lỗi tải và mất kết nối (Về trang chủ). Giữ nguyên |

## 3. Đánh giá bố cục (giữ nguyên)

Lobby, Loading, Kết quả, HUD, ESC, Bị loại, Killcam, Theo dõi, Lịch sử: thứ bậc rõ, nút chính nổi bật (viền vàng), hộp xác nhận cho hành động phá hủy (xóa phòng, thoát trận) đã có. Không đổi bố cục các màn này, chỉ đổi tên frame theo thứ tự luồng và thống nhất nhãn.

## 4. Thử nhánh lỗi trong prototype

Prototype chỉ cho một đích mỗi lần bấm, nên nhánh lỗi gắn vào chỗ bấm phụ:
- `06` bấm ô **Mã phòng** → `07` mã sai; ở `07` bấm **Vào phòng** → `08` phòng đầy.
- `15` Đang tải: bấm **danh sách bước** → `16` lỗi tải.
- HUD `17`–`19`: bấm **Minimap** → `29` mất kết nối; phím **Esc** → `20`.
- Màn chờ (Kết nối, Phòng chờ, Đếm ngược, Tải, Killcam, Kết nối lại) tự chuyển sau thời gian chờ.

100 kết nối (ON_CLICK, AFTER_TIMEOUT, phím Esc/Space, BACK). `26`, `32`, `33`, `35`, `36` là trạng thái thay thế, không nằm trên đường bấm chính.

## 5. Frame trong Figma (đổi tên theo thứ tự luồng, không xóa frame nào)

Mới: `04_logout-confirm` (`133:31`), `05_settings-server` (`133:195`). Sửa lớn: `01_login` (`121:285`), `02_register-validation` (`121:355`), `03_home` (`121:2`), `06`–`08` modal vào phòng (`121:106`, `121:157`, `121:210`), `10`–`12` quản lý phòng (gỡ Đăng xuất). Còn lại chỉ đổi tên và đổi nhãn "Về trang chủ". Mỗi hàng có nhãn A–J ở bên trái canvas.

## 6. Việc phía Unity để khớp (UI-02, cần CPO duyệt vì đổi quyết định "Guest không bị chặn")

1. Khởi động vào màn Đăng nhập thay vì Menu chính; JWT hợp lệ trong bộ nhớ thì vào thẳng Home. Gỡ đường Guest không đăng nhập (đổi mục 1, 2 và M1 của UI-02; server/NET-01 cần cho join bằng JWT).
2. `MainMenu`: ẩn `Endpoint`; thêm `AccountCard` (`Status`, `Username`, `Logout`) và nút `Leaderboard`; đổi nhãn `Rooms` thành "Tạo phòng".
3. Thêm `LogoutConfirm` (`Text`, `Yes`, `No`), đăng xuất xong xóa JWT và về Đăng nhập.
4. Thêm `ServerSettings` (dùng lại `Host`/`Port`/`Api`/`Save`/`Status` của `Endpoint` cũ + `Close`), mở từ Cài đặt và từ `Login/Panel/Advanced`. Panel cài đặt âm thanh của UI-03 nên gộp vào cùng màn Cài đặt này.
5. `JoinForm` thành modal trên `MainMenu` (thêm `Scrim`); `Back` đổi nhãn "Hủy"; `Hint` hiện tên tài khoản; `DisplayName` điền sẵn từ tài khoản.
6. `Login`/`Register`: ẩn `Back`, thêm `Advanced` và `Logo`.
7. `RoomManager`: gỡ `Logout`; mọi nhãn "Về menu" thành "Về trang chủ".
