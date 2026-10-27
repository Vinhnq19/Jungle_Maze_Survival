# I · Rừng thám hiểm — demo UI mới

Ngày **27/10/2026**. Trạng thái: **concept cục bộ, chưa duyệt art; chưa dựng được trong Figma**.

## Tham khảo

- Pin đã xem hình: [Free Jungle Cartoon 2D Game UI](https://in.pinterest.com/pin/812125745289329402/), ảnh pin công khai `https://i.pinimg.com/736x/fb/30/6d/fb306df82f7c9eb0259f6cb46552ff87.jpg`. Học cách chia cấp nút, gom HUD và dùng lá tại tiêu đề/mép; không trích hay sao chép asset.
- [Inventory / Game UI Database](https://es.pinterest.com/pin/374361787797394445/) chỉ thấy qua kết quả tìm kiếm, **chưa xác minh hình** nên không dùng làm căn cứ nhận xét hình ảnh.
- Pinterest khi mở trang trực tiếp hiện hộp đăng nhập. Ảnh pin thứ nhất xem được qua URL công khai nêu trên.
- Bố cục HUD dựa trên `Docs/UI_Wireframes.vi.md`: minimap200×200, Vitals296×164 ở24,532; quickbar564×130 ở358,566. Chỉ là concept ngoài Unity, không thay code/prefab.

## Hướng đề xuất

Khung xanh rừng, chữ ngà, điểm nhấn xanh mạ và hổ phách; mảng phẳng, ít chi tiết; Be Vietnam Pro. Icon lấy silhouette dễ đọc làm chính; không grain, bevel dày hay khung gỗ rườm rà. Menu có đảo minh họa nguyên bản bằng vector. Hướng H của UI-01 cũ không được coi là duyệt bộ art mới.

## File bàn giao

- [PNG xem nhanh](directions/I_jungle_expedition.png):2672×900, gồm hai màn1280×720 cạnh nhau và dải ghi chú.
- [SVG chỉnh sửa](directions/I_jungle_expedition.svg):text, shape, path và group riêng; nhúng hai weight Medium/Bold của font Be Vietnam Pro có trong repo. Không nhúng ảnh hoàn chỉnh của UI.
- Chưa gen bitmap vật phẩm, chưa tách sprite production, chưa import/bind Unity. Số liệu HUD chỉ minh họa, không phải bằng chứng gameplay hay QA.

## Kiểm tra

Render bằng Playwright/Edge headless từ SVG, hai font báo loaded; đã mở PNG kiểm trực quan, đủ menu3nút và HUD8khe, chữ tiếng Việt hiển thị được, vùng giữa giữ thoáng. Không chạy test Unity vì chỉ thêm tài liệu/ảnh concept.

## Figma đang bị chặn

File người dùng cung cấp: [Codefest2027 / Art directions](https://www.figma.com/design/oeZgisxdiR80hRqbikUwPb/Codefest-2027?node-id=51-2).

- Đọc được file qua MCP; tạo page riêng bị từ chối `read-only mode` ba lần trong task này, gồm lần kiểm lại sau khi người dùng gửi link. Lần cuối MCP vẫn báo read-only dù screenshot quyền chia sẻ hiển thị huycv18 có thể chỉnh sửa và Anyone can edit; có bất nhất giữa quyền hiển thị và connector, chưa rõ nguyên nhân. Debug UUID: `1cc2112f-5718-4dbb-ac0b-93e17c3d0eeb`.
- Người dùng chọn `Cao Van Huy (K18 HL)'s team` có seat Full. Tạo Design file với đúng key do whoami trả về vẫn báo `Invalid planKey` hai lần. Không có file mới được tạo.
- Không retry tiếp các thao tác trên. Cần file có quyền chỉnh sửa hoạt động qua MCP hoặc xử lý lỗi tạo file rồi mới dựng layers trong Figma. SVG cục bộ là bản chuẩn bị, **không coi là đã hoàn thành yêu cầu demo trong Figma**.
