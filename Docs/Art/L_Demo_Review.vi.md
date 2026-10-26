# L · Pixel Survival — sửa đúng chất liệu pixel của game

Ngày **27/10/2026**. Trạng thái: **demo mới theo yêu cầu sửa “nó là game đồ họa pixel”; chưa nghiệm thu production**.

K đã dùng vector mượt/gradient nên sai chất liệu. L giữ nhận diện đá nâu, vàng cổ và bố cục của [ảnh người dùng chọn](references/K_user_style_reference.png), nhưng vẽ lại trên lưới pixel; không downsample, mosaic hay áp filter lên ảnh K. Giữ nguyên các bản I/J/K.

## Bản xem

- [Inventory 1280×720](directions/L_inventory_1280.png).
- [HUD 1280×720](directions/L_hud_1280.png).
- [Hai màn cạnh nhau](directions/L_pixel_survival.png),2672×900.
- [SVG nguồn](directions/L_pixel_survival.svg):các pixel là rect/path tọa độ nguyên; text riêng, không bitmap UI nhúng.
- [Script tái tạo](directions/L_render.py):sprite nhỏ được author thành ma trận16×16; portrait/cây32×32. Inventory dùng icon scale4, HUD scale3; world320×180 scale4. Xuất PNG bằng Playwright/Edge viewport2672×900, crop inventory40,122 và HUD1352,122.

## Quy tắc art đã áp

- Khung đá tối2–3tone, viền vàng cổ dạng bậc, mảng bóng đặc và highlight1–2pixel; góc không bo mượt.
- Không gradient, blur, ellipse, rotate mượt hay đường Bézier. Sprite dùng palette hữu hạn26màu; màu panel/nền bổ sung là màu phẳng.
- Icon dao/ná/súng/cành cây/lá/lon Adrenaline/lông/la bàn được dựng từ ô vuông và nét bậc. Cây, đá, nhân vật, đường đất, suối đều là hình pixel nguyên.
- Minimap giữ ngữ nghĩa vòng bo tròn bằng dải ô vuông4px chạy theo khoảng cách tới tâm, không thay bo bằng hình vuông. Đã sửa qua1vòng review có mục tiêu và xem lại HUD.
- **Ngoại lệ chữ:**tiếng Việt vẫn dùng Be Vietnam Pro Medium/Bold để giữ đủ dấu và dễ đọc. Logo, nhãn Latin và số dùng Pixelify Sans của repo. Đây là ngoại lệ typography có chủ đích, không phải lý do dùng art mượt.

## Phạm vi

Inventory chỉ là layout đề xuất; đúng8khe gồm4vũ khí KNIFE/RUBBER_GUN/SHOTGUN/TREE_BRANCH và4dược phẩm/hỗ trợ GOD_LEAF/ADRENALINE/PHOENIX_FEATHERS/COMPASS. Không thêm tiền/gem/shop/cấp tài khoản/attack. Có selected state Shotgun, số lượng và phím minh họa.

HUD giữ minimap200×200 tại1056,24; Vitals296×164 tại24,532 với đủ4thanh máu/giáp/mũ/Adrenaline; quickbar8khe tại358,566 trong vùng564×130. Vùng giữa thoáng. Dữ liệu chỉ minh họa; đây không phải Game view hay gameplay.

## Kiểm tra và giới hạn

- Render thành công;3font face Be Vietnam Pro Medium/Bold và Pixelify Sans đều loaded.
- Đã xem cả2ảnh1280×720; đủ8khe,4thanh sinh tồn, chữ tiếng Việt, không thấy label cắt/tràn. Sau sửa minimap đã xem lại riêng HUD.
- Đã render crop icon dao lên512×512 từ chính SVG:pixel gốc thành khối vuông rõ, cạnh bậc và hard shade, không smooth giữa màu. Đây là kiểm cấu trúc pixel, không phải filter tạo style.
- Chưa tách8sprite thành file production hay kiểm import Point/no mipmap/PPU/pivot/9slice trong Unity. Chưa thay prefab/catalog/logic; không chạy Unity test cho ảnh concept.
- Figma vẫn bị chặn ghi theo log ở[I](I_Demo_Review.vi.md), không retry trong L; chưa có page/node/file L trên Figma.

Yêu cầu pixel là ràng buộc của game; việc người dùng chọn ảnh tham chiếu trước đó không đồng nghĩa đã nghiệm thu L hay toàn bộ asset production.
