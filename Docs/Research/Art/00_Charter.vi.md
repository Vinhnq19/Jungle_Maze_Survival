# Điều lệ Phòng Thẩm mỹ Art — Lastaway

Ngày 31/10/2026 · Art Director. Phòng làm độc lập: tự quyết, ghi giả định, không chờ chủ dự án duyệt từng bước. Quyết định nội dung và số liệu gameplay vẫn theo GDD; phòng chỉ quyết phần nhìn, cảm giác và độ hấp dẫn.

## 1. Mục tiêu

1. Trả lời ba câu hỏi cho mọi màn và mọi khoảnh khắc trong trận: **Tốt** chưa (rõ, đúng, nhất quán, đọc được), **Mượt** chưa (chuyển cảnh, phản hồi, nhịp), **Hấp dẫn** chưa (bản sắc, cảm xúc, khoảnh khắc đáng nhớ).
2. Giữ một hướng art duy nhất (hướng A: than chì + xanh ngọc + vàng dịu trên nền rừng pixel, xem [02_Art_Direction](02_Art_Direction.vi.md)) và một style guide mà mọi ticket UI/Art dùng chung.
3. Biến nhận xét thẩm mỹ thành ticket có tiêu chí nghiệm thu đo được, đưa vào pipeline CPO.
4. Chuẩn bị đầu vào cho art AI-render: brief, kích thước, quy chuẩn, để art mới chỉ cần gắn vào prefab.

Ngoài phạm vi: viết code, sửa prefab/scene/asset (thuộc unity-dev), cân bằng gameplay (game-designer), quyết định kỹ thuật (tech-lead).

## 2. Vai và trách nhiệm

Hiện Art Director kiêm cả 5 vai. Khi có thành viên Codex (Tech Art & Motion), Codex nhận vai 3 và 4, ghi `10_`–`13_` trong thư mục này.

| Vai | Trách nhiệm | Đầu ra |
| --- | --- | --- |
| **Art Director** | Giữ hướng art, chấm điểm, chốt ưu tiên, duyệt brief và asset trước khi gắn | `00`, `02`, `03`, README; phiếu duyệt asset |
| **UI/UX Researcher** | Audit màn, đối chiếu game tham chiếu, kiểm tương phản, cỡ chữ, nhãn | `01_Visual_Audit`, bảng vấn đề |
| **Motion / Game-feel** | Chuẩn thời lượng, easing, phản hồi đòn đánh, chuyển màn | Token motion trong `02`; `11_Motion_GameFeel` khi có Codex |
| **Tech Artist** | Kiểm import (PPU, filter, nén, atlas), chi phí render, shader/hậu kỳ URP | `10_Asset_Inventory`, `12_Render_Perf`, `13_TechArt_Backlog` |
| **Player Researcher** | Playtest nhìn-hiểu (5 giây), khảo sát cảm nhận, đo thời gian thao tác | Phiếu playtest, số đo trong `01`/`03` |

## 3. Thang chấm 1–5

Chấm từng **nhóm màn** trên ba trục. Điểm nhóm là trung vị các màn trong nhóm, làm tròn xuống. Một lỗi mức Chặn khiến trục đó tối đa 2.

### Trục Tốt (rõ ràng, đúng, nhất quán)

| Điểm | Mô tả |
| --- | --- |
| 1 | Người chơi không biết phải làm gì; chữ khó đọc hoặc chồng nhau; thông tin sai |
| 2 | Làm được nhưng có lỗi bố cục rõ (chồng lấn, cắt chữ), nhãn lẫn lộn, tương phản dưới 3:1 ở chữ quan trọng |
| 3 | Đúng và đủ thông tin; thứ bậc rõ; còn chữ nhỏ, khoảng trống phí, vài nhãn chưa thống nhất |
| 4 | Nhất quán theo style guide; tương phản ≥ 4,5:1 cho chữ thường; không cắt chữ; mỗi màn có một hành động chính rõ |
| 5 | Như 4, thêm: người mới hiểu màn trong ≤ 5 giây (đo playtest), trạng thái lỗi/trống có hướng dẫn và minh họa |

### Trục Mượt (chuyển động, phản hồi, nhịp)

| Điểm | Mô tả |
| --- | --- |
| 1 | Màn bật/tắt tức thì, giật, không phản hồi bấm |
| 2 | Có phản hồi trạng thái (hover/nhấn) nhưng chuyển màn tức thì, không có trạng thái chờ |
| 3 | Có trạng thái chờ rõ (đếm giờ, spinner); chuyển màn còn cứng; phản hồi trong trận đủ dùng |
| 4 | Chuyển màn có fade/slide theo token; nút có hover/press; phản hồi trận (hit, nhặt, sát thương) có motion riêng; giữ 60 fps |
| 5 | Như 4, thêm: chuỗi khoảnh khắc lớn (đếm ngược, bị loại, thắng) được dàn dựng; có chế độ giảm chuyển động; không frame nào > 33 ms do UI |

### Trục Hấp dẫn (bản sắc, cảm xúc)

| Điểm | Mô tả |
| --- | --- |
| 1 | Trông như công cụ debug; không có bản sắc |
| 2 | Có palette và logo nhưng mọi màn là hộp xám phẳng; không có hình minh họa |
| 3 | Bản sắc nhận ra được (logo, màu, nền); thiếu art nhân vật/vật phẩm; khoảnh khắc thắng/thua bình thường |
| 4 | Có key art, khung có chất liệu, icon thật; thắng/thua có cảm xúc; ảnh chụp màn hình đủ đẹp để đăng |
| 5 | Ngang hoặc hơn game tham chiếu cùng phân khúc; người chơi nhắc lại khoảnh khắc trong playtest |

## 4. Quy trình định kỳ

| Nhịp | Việc | Đầu ra |
| --- | --- | --- |
| Mỗi ticket UI/Art qua Build | Art Director soát ảnh chụp trước Review: đúng token màu/chữ/khoảng cách/motion | Ghi chú trong mục Review của ticket, nhãn "Art OK" hoặc danh sách sửa |
| Hằng tuần (thứ Sáu) | Chụp lại toàn bộ màn (`Tools/Editor/FigmaExport` hoặc ảnh Play mode do QA chụp), chấm lại 3 trục, cập nhật bảng vấn đề | Dòng mới trong `01_Visual_Audit` mục Lịch sử điểm |
| Mỗi mốc giai đoạn | Playtest nhìn-hiểu 5 người, khảo sát 5 câu (mục 6) | Phiếu trong `Docs/Research/Art/playtest/` |
| Khi có art AI-render mới | Duyệt theo checklist brief (`02` mục 6) trước khi gắn prefab | Phiếu duyệt asset |

## 5. Giao việc cho đội

Phòng không tự sửa file ngoài `Docs/Research/Art/`. Mọi thay đổi đi qua CPO theo [Team_Workflow](../../Team_Workflow.vi.md):

1. Phòng viết đề xuất ticket (mẫu trong `03` mục 5): ID, tiêu đề, phạm vi, tiêu chí đo được.
2. **CPO** nhận vào pipeline, kiểm phụ thuộc, xếp ưu tiên.
3. **game-designer** viết spec khi ticket có nội dung gameplay (ví dụ chỉ số trên màn kết quả); ticket thuần thẩm mỹ thì spec lấy thẳng từ đề xuất của phòng.
4. **tech-lead** lập kế hoạch file/lớp; phòng góp ý ở G1 về token và motion.
5. **unity-dev** build: prefab, `HudPalette`, `UiArtSet`, view trong `Presentation/UI`.
6. **code-reviewer** review code; Art Director soát ảnh chụp song song.
7. **qa-tester** đo theo tiêu chí (tương phản, fps, thời lượng, playtest), ghi `Docs/Reports/`.

Ranh giới vùng: `Assets/_JungleMaze/Art/**`, `Docs/Art/**` và các view art ghi trong [Agent_Coordination](../../Agent_Coordination.vi.md) mục 2 thuộc Codex khi ADHOC-UI-ASSETS-A còn mở. Đề xuất đụng các file đó ghi owner "Codex (ADHOC)" thay cho unity-dev.

## 6. Khảo sát cảm nhận (5 câu, thang 1–5)

1. Màn này cho tôi biết rõ phải làm gì tiếp theo.
2. Chữ và số dễ đọc khi đang chơi.
3. Chuyển cảnh và phản hồi khi bấm cảm thấy mượt.
4. Game có phong cách riêng, nhận ra được qua ảnh chụp.
5. Khoảnh khắc thắng/bị loại khiến tôi muốn chơi thêm trận nữa.
