# Phòng Thẩm mỹ Art: tóm tắt

Ngày 31/10/2026 · Art Director. Tài liệu trong thư mục:

| File | Nội dung |
| --- | --- |
| [00_Charter](00_Charter.vi.md) | Mục tiêu, 5 vai, thang chấm 1–5, quy trình, cách giao việc |
| [01_Visual_Audit](01_Visual_Audit.vi.md) | Chấm 10 nhóm màn, 18 vấn đề, điểm mạnh, so sánh 4 game |
| [02_Art_Direction](02_Art_Direction.vi.md) | Hướng "Trạm cuối trong rừng ngọc", token màu/chữ/khoảng cách/motion, brief art AI |
| [03_Improvement_Plan](03_Improvement_Plan.vi.md) | Quick wins / Ngắn hạn / Trung hạn, ticket AES-01…12 |

## Kết luận 3 trục

| Trục | Điểm | Một câu |
| --- | :-: | --- |
| **Tốt** | 3/5 | Luồng và thông tin đúng, lỗi có lối thoát; còn 2 lỗi chồng lấn, 3 cặp màu dưới 4,5:1, font pixel làm nhầm số |
| **Mượt** | 2/5 | Mọi màn bật/tắt tức thì; màn chờ không có chỉ báo chuyển động; phản hồi HUD có view nhưng chưa xem chạy thật |
| **Hấp dẫn** | 2/5 | Palette jade và rừng pixel có bản sắc, nhưng không có nhân vật, màn thắng/thua phẳng, nhiều màn mất nền rừng |

Đích: Tốt 4 sau Quick wins; Mượt 4 và Hấp dẫn 3 sau Ngắn hạn; Hấp dẫn 4 sau Trung hạn.

## Top 10 việc nên làm

| # | Việc | Ticket | Công sức |
| --- | --- | --- | --- |
| 1 | Sửa chồng lấn: xác nhận xóa phòng, chế độ Theo dõi | AES-01 | S–M |
| 2 | Scrim cho mọi modal | AES-01 | S |
| 3 | Đưa chữ lên ≥ 4,5:1 (nút chính `#2E7263`, Faint `#859E98`, RedText `#F06A6E`), nút phá hủy kiểu riêng | AES-02 | S |
| 4 | Mọi số dùng Be Vietnam Pro; Pixelify chỉ cho logo | AES-02 | S |
| 5 | Lớp motion: panel 220 ms OutCubic, modal 200 ms OutBack, nút nhấn 80 ms | AES-04 | M |
| 6 | Dàn dựng màn Kết quả: tiêu đề lớn, 3 số, stagger, ánh ngọc/sương tím | AES-06 | M–L |
| 7 | Nền rừng pixel cho mọi màn ngoài trận + đom đóm | AES-05 | M |
| 8 | Đo nền: cỡ chữ, Profiler, playtest 5 người | AES-08 | M |
| 9 | Không cắt tên vật phẩm, cỡ chữ ≥ 14 | AES-03 | S–M |
| 10 | Key art nhân vật cho Home/Phòng chờ/Kết quả | AES-09 | L |

## Ràng buộc đã giữ

Chỉ ghi tài liệu; không sửa code, prefab, scene, asset; không vào Play mode. Các hạng mục đụng file UI-02 đang sửa phải chờ UI-02 commit. Chưa có file `10_`–`13_` của Codex; phần Tech Art & Motion tạm nằm trong `03` mục 4.
