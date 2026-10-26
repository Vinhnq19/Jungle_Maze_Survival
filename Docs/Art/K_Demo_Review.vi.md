# K · Antique Survival — demo theo hướng người dùng chọn

Ngày **27/10/2026**. Trạng thái: **người dùng chọn ngôn ngữ hình ảnh qua ảnh tham chiếu; bản demo K và toàn bộ asset production chưa nghiệm thu**.

Người dùng gửi [ảnh mẫu](references/K_user_style_reference.png) và nói **“kiểu này đi”**. Đây là căn cứ chọn hướng demo K:đá nâu xám tối, viền vàng cổ mảnh có bevel/corner gấp, nền ô vật phẩm tròn lõm tối, icon màu có khối, chữ ngà và plaque đồng ở tiêu đề. I/J giữ nguyên là bản nháp trước đó, không tiếp tục phát triển.

## File xem và nguồn

- [Inventory 1280×720](directions/K_inventory_1280.png).
- [HUD 1280×720](directions/K_hud_1280.png).
- [Hai màn cạnh nhau](directions/K_antique_survival.png),2672×900.
- [SVG chỉnh sửa](directions/K_antique_survival.svg):text/path/group/gradient đều là lớp riêng, không nhúng screenshot UI hay chép ảnh mẫu. Be Vietnam Pro Medium/Bold được nhúng từ font trong repo.
- [Script tái tạo SVG](directions/K_render.py), chỉ ghi file K; xuất PNG bằng Playwright/Edge ở viewport2672×900, clip inventory tại40,122 và HUD tại1352,122, mỗi ảnh1280×720.

## Phạm vi demo

Inventory là **bố cục đề xuất mới**, chưa chốt hành vi của ITM-01. Cột trái có chân dung survivor vẽ riêng và máu/giáp/mũ/Adrenaline; không thêm attack, tiền, gem, cấp tài khoản, cửa hàng hay nhiệm vụ của ảnh mẫu.

Đúng **8 khe**:4 vũ khí KNIFE, RUBBER_GUN, SHOTGUN, TREE_BRANCH;4 dược phẩm/hỗ trợ GOD_LEAF, ADRENALINE, PHOENIX_FEATHERS, COMPASS. Hàng vũ khí và hàng hỗ trợ tách rõ, Shotgun được chọn, dòng chi tiết chỉ mô tả lựa chọn và phím chuyển, không bịa damage/balance. Icon Adrenaline là lon đồ uống; icon còn lại dùng nhiều lớp màu, gradient và highlight để đọc như đồ vật thu nhỏ. Chưa phải8bitmap painted production độc lập.

HUD dùng cùng hệ khung nâu/gold nhưng mảnh và gọn. Minimap200×200 ở1056,24; Vitals296×164 ở24,532; quickbar8khe bắt đầu358,566 trong vùng564×130. Có minh họa warning, effect, pickup, crosshair và cây/đá/suối nền olive. Đây là art concept ngoài Unity, không phải ảnh Game view hay gameplay thật. Số liệu chỉ minh họa.

## Kiểm tra

- Render thành công;2weight Medium/Bold của Be Vietnam Pro đều loaded.
- Đã mở riêng cả2ảnh ở1280×720:không thấy chữ cắt/tràn; đủ8khe và nhãn, selected state rõ; khu vực giữa HUD thoáng. Qua1vòng sửa có mục tiêu theo review, HUD có đủ4thanh máu/giáp/mũ/Adrenaline trong nguyên khung296×164; đã xuất và xem lại riêng HUD sau sửa.
- Đối chiếu ảnh người dùng:chuyển khỏi teal/emerald của J sang đá nâu tối, gold cổ, circular wells và plaque đồng; không lấy nội dung tiền/cấp/shop của ảnh mẫu.
- Kiểm cấu trúc SVG trước sửa bar:96ID đều duy nhất,82text node,0image nhúng; sau sửa thêm text cho hàng mũ. Các hình đều là text/vector chỉnh sửa được. Không sửa I/J, font, prefab, catalog, logic hay ProjectSettings.
- Không chạy test Unity vì chỉ thêm ảnh/tài liệu concept. Chưa tách sprite production, chưa QA alpha/PPU/9slice, chưa gắn vào game.

## Figma

Figma vẫn bị chặn ghi qua MCP theo log ở [I](I_Demo_Review.vi.md):3lần read-only dù quyền hiển thị cho phép sửa;2lần tạo file mới lỗi Invalid planKey. **Không retry trong đợt K**. Không có node/page/file K trên Figma; bản này là nguồn SVG cục bộ và PNG để người dùng xem trước. Không coi việc chọn hướng tham chiếu là duyệt toàn bộ bộ asset hoặc hoàn thành demo trong Figma.
