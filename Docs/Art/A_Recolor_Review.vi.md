# Recolor đúng hướng A — Charcoal, Jade và Soft Gold

Ngày **27/10/2026**. Trạng thái: **đã đổi màu trực tiếp frame A trong Figma; chờ người dùng xem bản màu**.

Người dùng quay lại **đúng hướng A trong Figma và chỉ đổi màu**, chọn charcoal/jade/soft gold. Bản này dùng trực tiếp [SVG xuất nguyên bản](directions/A_figma_original.svg) của frame`51:3`,page`51:2`,file`oeZgisxdiR80hRqbikUwPb`;không vẽ lại bố cục, khung, icon, nhân vật hoặc world. Không tạo hướng art N.

## Bản xem

- [Frame A trong Figma](https://www.figma.com/design/oeZgisxdiR80hRqbikUwPb/Codefest-2027?node-id=51-3).
- [Ảnh HUD chụp trực tiếp từ Figma sau đổi màu](directions/A_charcoal_jade_figma_hud.png).
- [Ảnh menu chụp trực tiếp từ Figma sau đổi màu](directions/A_charcoal_jade_figma_menu.png).

Các bản xuất cục bộ dưới đây được dựng từ SVG frame gốc, để tái tạo/đối chiếu palette; hai ảnh `figma_*` phía trên là bằng chứng bản đã lưu trong Figma:

- [HUD 1280×720](directions/A_charcoal_jade_hud_1280.png).
- [Menu 1280×720](directions/A_charcoal_jade_menu_1280.png).
- [Board2728×980](directions/A_charcoal_jade.png).
- [SVG đã đổi màu](directions/A_charcoal_jade.svg).
- [Script recolor](directions/A_recolor.py) và [script xuất ảnh](directions/A_recolor_export.cjs).

## Màu đã đổi

| Vai trò / màu cũ | Màu mới |
| --- | --- |
| Nền`#0B0F0D` | `#0B1114` |
| Panel`#3B2A1A` | `#202A2D` |
| Viền tối`#1E140C` | `#0C1417` |
| Mép bevel`#6B4A2B` | `#526B69` |
| Chữ`#F7EBD0` | `#EDF2EE` |
| Chữ phụ`#C9B48E` | `#A6BBB7` |
| Accent`#8BC34A` | `#63BAA5` |
| Chữ mô tả`#A7B5A3` | `#A4B9B1` |
| Chữ muted`#6E8070` | `#77908A` |

Điểm nhấn riêng:nền khe đang chọn`#28534F`giữ nguyên opacity0.25;viền khe và logo LASTAWAY`#C6AE78`;nền nút Chơi`#3B8D7B`,viền`#C6AE78`. Selector lấy đúng tọa độ rect trong SVG xuất vì export không giữ IDnode Figma.

Giữ màu world green, đỏ HP/nguy hiểm, xanh giáp, vàng Adrenaline và tím vòng bo. Mọi tên/món/số liệu/text từ A giữ nguyên;chỉ các nhãn hex swatch cập nhật theo màu mới. Nội dung placeholder vốn có trong A vẫn là placeholder, không phải asset mới đã sinh.

## Kiểm tra

- Script so cây SVG trước/sau, bỏ qua duy nhất thuộc tính fill/stroke và nội dung mã hex swatch:kết quả **geometry, cấu trúc, opacity, string, vị trí chữ và thuộc tính font không đổi**. Script dừng lỗi nếu selector không duy nhất hoặc geometry thay đổi.
- Nhúng Be Vietnam Pro Medium/Bold và Pixelify Sans variable từ repo.3font face đều loaded. Thuộc tính font-weight800 của tiêu đề ngoài board vẫn giữ;repo chỉ có BVP500/700 nên browser chọn/synthesize từ Bold cho riêng tiêu đề này. Chữ trong2crop dùng đúng weight500/700có sẵn.
- Render board2728×980;crop HUD tại64,236 và menu tại1384,236,mỗi ảnh1280×720. Đã mở riêng cả2ảnh kiểm trực quan sau recolor.
- Không đổi Unity/prefab/catalog/logic, không chạy test Unity cho ảnh recolor.

## Figma

Connector MCP vẫn không ghi được:mutation fill đầu tiên bị từ chối read-only;debugUUID`d1ecebc3-bfdb-4d8f-832f-60baedfdea80`. Không retry thao tác này.

**Đã xử lý bằng giao diện Figma trong trình duyệt, dùng phiên đăng nhập sẵn của người dùng.** Màn chọn tài khoản ghi `huycv18.work@gmail.com` là **Viewer on file**, còn tài khoản FPT `Cao Van Huy (K18 HL)` là **Editor on file**. Chọn tài khoản Editor rồi đổi màu trong Selection colors của riêng frame`51:3`, giữ opacity từng nhóm; đổi riêng nền/viền Slot2`51:380`,nút Chơi`51:432`,logo`51:430`. Không mở rộng quyền, không cài plugin, không đổi tài khoản kết nối MCP.

MCP đọc lại xác nhận màu mới đã đồng bộ trong file thật, root vẫn2728×980, các màu đỏHP/nguy hiểm,xanhgiáp,vàngAdrenaline,tímbo và worldgreen giữ nguyên. Đã chụp lại hai frame HUD/menu1280×720 từ Figma, không dùng preview cục bộ thay bằng chứng cập nhật. Mã hex ba nhãn palette cũng được sửa theo màu mới; nội dung game/placeholder khác giữ nguyên.

[Read-back sau cùng](directions/A_figma_recolor_readback.json) lưu ID, vị trí/kích thước, màu và text của các node chính. Ba nhãn hex giữ vị trí nhưng độ rộng tự tính theo chuỗi mới; kích thước/bố cục hai màn và slot/nút không đổi. MCP vẫn dùng tài khoản Viewer để đọc; đường ghi hoạt động là trình duyệt với tài khoản FPT Editor, không phải đã sửa được cấu hình MCP.
