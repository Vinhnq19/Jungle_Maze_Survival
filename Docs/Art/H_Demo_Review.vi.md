# H · Rừng gọn — demo dựa trực tiếp trên Figma

Ngày: 24/10/2026. Trạng thái: **đã chọn H, đang triển khai P0 rồi UI-01 phần 2 tới G3**.

Người dùng xác nhận tiếp tục: "Oke làm tiếp đi" sau khi đã xem H và kiểm tra kết nối Unity MCP. H là hướng art triển khai, giữ mức độ đơn giản của demo và palette Figma v2.

## Yêu cầu và nguồn

Người dùng phản hồi bản G quá chi tiết và yêu cầu kết nối Figma. Đã kết nối, đọc trực tiếp file `oeZgisxdiR80hRqbikUwPb`: style guide `39:3`, HUD feedback `48:239`, menu `42:88`, và cấu trúc các hướng art trên page `51:2`. Font sản phẩm là Be Vietnam Pro cho chữ và Pixelify Sans cho số/logo.

Đã thử dựng frame H trong file nhưng Figma từ chối ở lệnh tạo node đầu tiên: `Can't call "createAutoLayout" in read-only mode`. Không frame nào được tạo và không chỉnh nguồn gốc. Tài khoản kết nối đọc được file; chưa ghi được demo lên Figma. Nếu muốn dùng file mới, tài khoản có nhiều team nên cần người dùng chọn nơi tạo; không tự chọn team.

## Demo đã làm

- [Ảnh tổng hợp HUD + menu](directions/H_simple_forest.png).
- [Gameplay đúng 1280×720](directions/H_gameplay_1280.png).
- [Nguồn SVG tổng hợp](directions/H_simple_forest.svg): giữ vector/text, font nhúng từ font đã có trong repo.
- Hai file `H_hud_figma_source.svg` và `H_menu_figma_source.svg` là bản xuất trực tiếp từ hai frame nguồn. Chữ bị mã hoá trong bản xuất được chuẩn hoá UTF-8 khi dựng.
- `H_world.svg`, `H_island.svg`, `H_icons.json`: art vector mới, mảng màu phẳng, ít chi tiết, không texture hay ánh sáng phức tạp.
- Script dựng: `Tools/Art/build_simple_demo.py`; render bằng `Tools/Art/render_simple_demo.cjs` với Playwright, Edge headless và font của dự án. Đây là công cụ demo ngoài Unity.

UI dùng bố cục và nội dung Figma, giữ màu ngữ nghĩa, khung mỏng và các trạng thái đã có. Chỉ thay lớp minh hoạ thế giới, key art menu và sáu icon minh hoạ trên các khe có món. Hai khe trống không có icon. Chưa gắn catalog ART-04 hay thêm gameplay. Menu chỉ là concept ngoài phạm vi code UI-01.

## Kiểm tra

- Render thành công, font Be Vietnam Pro Medium/Bold và Pixelify Sans đều loaded.
- Đã xem ảnh tổng hợp và gameplay 1280×720; sửa lệch icon ở khe trống và icon Shotgun trên đất, rồi xem lại ảnh sau sửa.
- Không chạy test Unity vì chỉ tạo file demo trong Docs/Art và Tools/Art. Không coi ảnh concept là bằng chứng QA/ảnh Game view.
- Không đánh dấu dòng manifest nào done và không chọn H vào Asset_Pipeline.vi.md khi chưa có quyết định của người dùng.

## Việc tiếp theo

## Tiến độ triển khai P0

- `911ddcbd`: thay sprite P0; EditMode 193/193, PlayMode 19/20 rồi sửa assertion border 10→24 theo manifest và test lỗi chạy lại 1/1. Job thật lưu tại `Docs/Reports/Art-H_2026-10-24/P0_test_jobs.json`.
- Review phát hiện bàn giao gọi `Cell48.png` là ground nhưng `MapGridView` tự sinh nền; Cell48 thực tế là mask dùng chung của obstacle/probe. Màu xanh trên mask gây nhân tint ngoài dự kiến.
- `d860ceee`: phục hồi Cell48 trắng; tách `Ground96.png` và đổi manifest `world_ground` sang asset mới, trạng thái **review**. Năm asset HUD/Hero còn lại đã thay; ground chưa được renderer map dùng, không coi hoàn tất.
- Đã trình người dùng móc nối tối thiểu ở [H_Ground_Integration.patch](H_Ground_Integration.patch), đang chờ cho phép chạm vùng map của Hào. Chưa áp patch hay sửa scene.

## Tiếp tục UI-01

Làm P0 theo Asset_Pipeline.vi.md và manifest; sau phần art, tiếp tục UI-01 phần 2 theo UI-01_HANDOFF.md mục B và UI-01-P2.md mục 4. G1 đã duyệt, G3 lõi đã đạt; không yêu cầu duyệt lại G1. Build → review → QA → dừng G3; commit theo đường dẫn, không push. Các lưu ý kỹ thuật đã chuẩn bị ở G_Demo_Review.vi.md vẫn còn hiệu lực.
