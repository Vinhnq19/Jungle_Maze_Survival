# M · Dark 2D UI — khối rõ, bóng đổ và ánh sáng

Ngày **27/10/2026**. Trạng thái: **demo theo yêu cầu mới, chưa nghiệm thu production**.

Người dùng yêu cầu **“Hãy làm UI tối màu, 2D không làm style pixel, nó rõ được các khối, có bóng đổ, có ánh sáng”**. Yêu cầu này thay hướng pixel của UI trong L. M dùng UI vector mượt, gradient nhẹ và shadow; không dùng Pixelify Sans hay filter giả pixel. Giữ nguyên các bản J/K/L.

## Bản xem

- [Inventory 1280×720](directions/M_inventory_1280.png).
- [HUD 1280×720](directions/M_hud_1280.png).
- [Hai màn cạnh nhau](directions/M_dark_2d.png),2672×900.
- [SVG nguồn](directions/M_dark_2d.svg):text, đường vector, group, gradient và shadow chỉnh sửa được; không nhúng ảnh hoàn chỉnh của UI.
- [Script dựng](directions/M_render.py):tạo SVG, nhúng2weight Be Vietnam Pro của repo và tái sử dụng riêng group world/hero concept của L làm nền. Xuất PNG bằng Playwright/Edge viewport2672×900, inventory clip40,122;HUD clip1352,122;mỗi ảnh1280×720.

## Cách thể hiện yêu cầu

Palette charcoal/deep navy, lớp mặt desaturated teal, chữ trắng ấm. Highlight teal trên mép trên và nền slot; amber dịu cho khe Shotgun đang chọn. Bóng mềm đổ xuống tách khối, không dùng bevel dày hay chất liệu3D photoreal.

Inventory tách rõ4khối:rail tiêu đề/phím đóng, card chân dung/chỉ số bên trái, card vật phẩm bên phải, panel chi tiết thấp hơn. Từng món nằm trong slot chữ nhật bo góc riêng, có viền sáng và bóng rơi; icon smooth2D nhiều lớp màu, highlight và bóng riêng. BVP dùng cho cả chữ/số để đồng nhất và đủ dấu tiếng Việt.

Đúng8khe:4vũ khí KNIFE/RUBBER_GUN/SHOTGUN/TREE_BRANCH;4dược phẩm/hỗ trợ GOD_LEAF/ADRENALINE/PHOENIX_FEATHERS/COMPASS. Có4chỉ số máu/giáp/mũ/Adrenaline; không thêm shop, tiền, gem, cấp tài khoản hay attack. Bố cục inventory và lời nhắc phím chỉ là đề xuất, chưa chốt hành vi ITM-01.

HUD dùng cùng hệ dark2D. Minimap200×200 ở1056,24;Vitals296×164 ở24,532 đủ4thanh;quickbar564×130 ở358,566 đủ8khe;vùng giữa thoáng. Nền world/hero pixel được giữ từ L và chỉ phủ scrim tối để kiểm tương phản với UI smooth. **Yêu cầu đổi style UI không tự đổi đồ họa world của game.** Nền này là concept, không phải ảnh Unity/Game view.

## Kiểm tra và giới hạn

- Render thành công;Be Vietnam Pro Medium/Bold đều loaded. Không dùng Pixelify Sans cho M.
- Đã mở cả2ảnh1280×720 và kiểm trực quan:không thấy chữ bị cắt/tràn;khối tách rõ,shadow/highlight hiện rõ,8khe và4thanh sinh tồn đầy đủ. Phiên chính đã xem cả2ảnh và không yêu cầu sửa thêm.
- Số liệu chỉ minh họa, không phải bằng chứng gameplay. Chưa tách sprite production, chưa QA alpha/PPU/9slice hay gắn catalog/prefab. Không thay Unity/ProjectSettings và không chạy Unity test cho ảnh concept.
- Figma vẫn bị connector chặn ghi theo log ở[I](I_Demo_Review.vi.md);không retry trong M. Chưa có page/node/file M trên Figma. SVG/PNG cục bộ không được coi là hoàn thành demo trong Figma.

Việc người dùng đổi yêu cầu style là căn cứ dựng M, không đồng nghĩa đã nghiệm thu M hoặc toàn bộ bộ assets production.
