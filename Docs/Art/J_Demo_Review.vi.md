# J · Rừng thám hiểm — demo có chất liệu và chiều sâu

Ngày **27/10/2026**. Trạng thái: **bản nháp đã được thay thế bởi hướng tham chiếu người dùng vừa gửi; chưa duyệt; chưa dựng được trong Figma**.

Sau khi J được dựng, người dùng gửi ảnh tham chiếu quyết định và nói “kiểu này đi”: hướng fantasy RPG nâu đá/gold cổ, không phải teal/emerald của J. Dừng chỉnh J tại đây; không xem J là hướng được chọn.

Người dùng nhận xét bản I quá đơn giản và muốn đẹp hơn. Bản J giữ I nguyên vẹn, bổ sung minh họa menu có ánh sáng, hệ khung/nút có chất liệu nhẹ, biểu trưng la bàn, icon nhiều lớp và thế giới HUD có cây, đá, di tích, suối. Không thay asset, prefab hay code Unity.

## Bản xem

- [Menu 1280×720](directions/J_menu_1280.png).
- [HUD 1280×720](directions/J_hud_1280.png).
- [Hai màn cạnh nhau](directions/J_jungle_polished.png),2672×900.
- [Nguồn SVG editable](directions/J_jungle_polished.svg):text, path, group, gradient và UI icon là lớp riêng. Menu chỉ nhúng bitmap minh họa riêng `J_sources/MenuIsland.png` phía dưới scrim, chữ và nút; không nhúng ảnh hoàn chỉnh của giao diện.
- Nguồn ảnh minh họa và prompt nằm tại `directions/J_sources/`, do phiên chính tạo bằng ImageGen. Script SVG `directions/J_render.py`; script xuất ảnh `directions/J_export.cjs` dùng Playwright/Edge.

## Khác biệt so với I

Menu có key art rừng/đảo nguyên bản, ánh hoàng hôn ở phải và scrim xanh đậm ở trái giúp chữ dễ đọc. LASTAWAY, biểu trưng thám hiểm và hành động chính có cấp bậc rõ. Nút chính vát góc, viền sáng mảnh và gradient; nút phụ dùng nền xanh ngọc có chiều sâu, không khung gỗ dày.

HUD dùng panel emerald/teal với viền sáng nhẹ và bóng mềm. Icon có cạnh kim loại, gỗ, thủy tinh, gân lá và kim la bàn; tám khe vẫn phân biệt bằng hình. HP/giáp/Adrenaline giữ màu ngữ nghĩa. Minimap, Vitals và quickbar theo vùng bố cục UI-01; vùng giữa giữ thoáng. Số liệu chỉ minh họa, không phải bằng chứng gameplay.

Hình menu là tranh raster; thế giới HUD hiện vẫn là vector concept. Hai lớp art chưa phải bộ production hoàn chỉnh và chưa chứng minh đồng nhất khi chạy game. Chưa tách sprite từng món, chưa xác nhận alpha/PPU/border9slice hay gắn catalog.

## Tham khảo và trạng thái Figma

Tiếp nối nghiên cứu Pinterest ghi tại [I_Demo_Review.vi.md](I_Demo_Review.vi.md):pin GUI rừng đã xem ảnh, pin Inventory chỉ thấy kết quả tìm kiếm. Chỉ tham khảo phân cấp khối, không lấy ảnh/asset của pin để gắn game. J dùng minh họa mới và UI vector mới.

MCP Figma vẫn chặn ghi:Codefest read-only sau3lần; tạo file team người dùng chọn lỗi Invalid planKey2lần. Không retry trong đợt J. Không có node/page/file J trên Figma; SVG này là bản cục bộ để xem trước và chuẩn bị chuyển sang layers khi connector hoạt động.

## Kiểm tra

- Render thành công qua Playwright/Edge; Medium và Bold của Be Vietnam Pro đều loaded.
- Đã mở riêng menu và HUD ở1280×720 kiểm trực quan:chữ tiếng Việt đầy đủ, menu3nút rõ, HUD8khe, không thấy chữ bị cắt; đối chiếu cấp bậc/cạnh panel/icon.
- Nguồn SVG nhúng font từ repo và bitmap minh họa riêng; các group có ID riêng. Script không sửa I, Unity hoặc ProjectSettings.
- Không chạy test Unity vì chỉ thêm ảnh/tài liệu concept. Chưa coi đây là nghiệm thu art, ảnh Game view hay hoàn thành yêu cầu dựng trong Figma.

Tái tạo:chạy `python Docs/Art/directions/J_render.py`, sau đó `node Docs/Art/directions/J_export.cjs` trong môi trường có Playwright và Edge. Phiên này dùng bundled Python/Node runtime, không cài package mới.
