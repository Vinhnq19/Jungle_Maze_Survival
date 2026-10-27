# Thay asset demo bằng asset thật sinh bằng Codex

**Cập nhật yêu cầu bộ UI mới — 27/10/2026:** người dùng quay lại đúng hướng **A trong Figma**, chọn **than chì + xanh ngọc, điểm nhấn vàng dịu**. Đã đổi màu frame A trong file thật; xem [báo cáo và ảnh Figma](A_Recolor_Review.vi.md) và [kế hoạch pack](UI_Asset_Packs.vi.md). Các ghi chú H bên dưới là lịch sử P0 đã gắn cho UI-01; đợt recolor A chưa thay asset Unity hoặc nghiệm thu production. Không tự dùng H/I/J/K/L/M làm hướng cho bộ mới.

**Hướng đã chọn: H · Rừng gọn (24/10/2026).** Người dùng yêu cầu giảm chi tiết, kết nối Figma, rồi xác nhận tiếp tục. Demo: [H_simple_forest.png](directions/H_simple_forest.png), gameplay [1280×720](directions/H_gameplay_1280.png). Dùng art vector có sẵn của hướng H: mảng màu phẳng, bóng cứng nhẹ, giữ khung HUD mỏng và palette/font Figma v2. Nguồn SVG rừng/menu/icon ở `directions/H_*`; rasterize về PNG theo manifest, không thêm texture hay trang trí của bản G đã bị bỏ. Chi tiết nguồn và giới hạn: [H_Demo_Review.vi.md](H_Demo_Review.vi.md).

Đợt triển khai hiện tại: P0 (6 asset đang dùng), sau đó UI-01 phần 2 tới G3. Icon catalog ART-04, code menu UI-02, vật cản và animation thuộc ticket tương ứng; demo không tự mở rộng phạm vi các ticket đó. File Figma đã đọc trực tiếp nhưng chỉ cho đọc; không chặn build Unity theo bản thiết kế đã có.

Cập nhật **24/10/2026**. Toàn bộ art hiện tại trong `Assets/_JungleMaze/Art` là bản **demo**: sprite 9-slice tự sinh, ô vuông thay icon, Hero và ô đất 48 px vẽ tạm. Tài liệu này chuẩn bị để thay chúng bằng ảnh do **Codex sinh**, mà không phải sửa code, test hay bố cục.

Đọc kèm: bố cục và component trong [UI_Wireframes.vi.md](../UI_Wireframes.vi.md) mục 5, danh sách asset máy đọc được trong [asset_manifest.csv](asset_manifest.csv) (73 dòng), quyết định "art tạm, AI render sau" trong mục đầu của UI_Wireframes.

## 1. Chọn hướng art trước khi sinh

Figma page `Lastaway v3 — Art directions` (node `51:2`) có 3 hướng. Mỗi hướng dựng **cùng bố cục HUD v2 đã duyệt** và Menu chính, chỉ khác lớp da. Vì vậy chọn hướng nào cũng không ảnh hưởng code.

| Hướng | Figma | Ảnh | Ý tưởng | Font số/tiêu đề | Lọc ảnh trong Unity |
| --- | --- | --- | --- | --- | --- |
| **A · Rừng Pixel** | `51:3` | [A_jungle_pixel.png](directions/A_jungle_pixel.png) | Pixel 16-bit, khung gỗ khắc có viền sáng, thanh chia khúc. Khớp "2D Pixel Art" trong GDD | Pixelify Sans | Point, không nén |
| **B · Btooom Tác chiến** | `51:451` | [B_btooom_tactical.png](directions/B_btooom_tactical.png) | Kính tối, viền cyan mảnh, góc ngoặc, cam cho bom. Gần anime Btooom! | Rubik Bold | Bilinear |
| **C · Đảo phiêu lưu** | `51:849` | [C_island_adventure.png](directions/C_island_adventure.png) | Hoạt hình vẽ tay, giấy da, viền nâu dày, bo tròn, bóng đổ. Vui nhộn, dễ đọc | Baloo 2 ExtraBold | Bilinear |
| **D · Đền rừng cổ** | `56:2` | [D_jungle_temple.png](directions/D_jungle_temple.png) | Theo Core Keeper (Steam): đền cổ phủ rêu, ánh đuốc ấm, viền tối; UI đá khắc viền vàng, ngọc phát sáng, minimap tròn vòng đá | Pixelify Sans | Point (pixel) hoặc Bilinear |
| **E · Hoạt hình sáng** | `57:2` | [E_bright_cartoon.png](directions/E_bright_cartoon.png) | Theo Super Animal Royale (Steam, battle royale 2D top-down): màu tươi, nét mực đậm, bóng cứng kiểu sticker, UI trắng bo dày | Baloo 2 ExtraBold | Bilinear |
| **F · Mực vẽ tay** | `58:2` | [F_ink_handdrawn.png](directions/F_ink_handdrawn.png) | Theo Don't Starve Together (Steam): mực vẽ tay, sepia u tối, bụi gai, lửa trại; huy hiệu tim tròn, khe hơi nghiêng | Be Vietnam Pro ExtraBold Italic | Bilinear |

D, E, F là đợt 2, chi tiết hơn: có ánh sáng, viền tối, icon vật phẩm dạng nét và minimap tròn. Đây là các hướng được đề xuất mạnh nhất.

Chữ tiếng Việt ở cả ba hướng vẫn dùng **Be Vietnam Pro**, vì các font số/tiêu đề không đủ dấu tiếng Việt. Khi đã chọn hướng: cập nhật `HudPalette` (màu) và style guide `39:3`, rồi sinh asset theo mục 3.

## 2. Asset demo đang có và cách thay

**Ghi đè đúng file, giữ nguyên `.meta`.** Unity giữ GUID, nên mọi prefab đang tham chiếu tự đổi sang ảnh mới. Sau khi ghi đè, chỉnh lại PPU và viền 9-slice theo cột tương ứng trong manifest, vì ảnh thật lớn hơn ảnh demo.

| File demo | Đang dùng ở | Thay bằng (manifest `id`) | Lưu ý khi ghi đè |
| --- | --- | --- | --- |
| `Art/Hud/HudRound10.png` | Khung Vitals, Minimap, Match status, killfeed, dải vũ khí | `hud_panel` | Viền đang 10 → đặt 24 (ảnh 96×96) |
| `Art/Hud/HudRound6.png` | Khe Default | `hud_slot` | Viền 20, ảnh 128×128 |
| `Art/Hud/HudRound6Stroke2.png` | Khe Selected | `hud_slot_selected` | |
| `Art/Hud/HudRound6Dashed.png` | Khe Empty | `hud_slot_empty` | |
| `Art/Hud/HudRound6Stroke1.png` | KeyCap, viền mảnh | `hud_outline` | |
| `Art/Hud/HudCircle16.png` | Chấm vị trí trên Minimap | `hud_marker_self` | Ảnh 64×64, giữ tâm ở giữa |
| `Art/Foundation/Hero48.png` | `Hero.prefab` | `world_hero` | PPU đang 48. Ảnh 96 px thì đặt PPU 96, nếu không Hero to gấp đôi |
| `Art/Foundation/Cell48.png` | Lưới mặt đất | `world_ground` | Như trên; ảnh phải liền mép (tileable) |
| `Art/Fonts/TMP/*.asset` | Chữ HUD | Giữ nguyên | Đổi font số theo hướng art bằng cách tạo lại `HudNumber SDF` |

Asset **mới**, chưa có chỗ gắn (icon vật phẩm và hiệu ứng, khung phần 2, menu, vật cản thật): đặt đúng `target_path` trong manifest. Chỗ gắn nằm ở các ticket sau:
- Icon 18 vũ khí + 16 trang bị: **ART-04**. Cần thêm một catalog `code → Sprite` (ScriptableObject, giống `HudEffectCatalog` mà kế hoạch UI-01 phần 2 đã định). Khe HUD hiện chỉ có ô giữ chỗ `IconPlaceholder`.
- Icon hiệu ứng, cung sát thương, hit marker, toast, banner: **UI-01 phần 2** (đang bàn giao cho Codex).
- Logo, key art, nút menu: **UI-02**. Vật cản, rương, chum, bụi: **ART-02 / ART-03**. Hero có animation: **ART-01**.

## 3. Quy trình sinh một asset bằng Codex

1. Lấy một dòng `status = todo` trong `asset_manifest.csv`, ưu tiên P0 trước.
2. Ghép prompt theo mẫu:

   ```text
   {PROMPT NỀN CỦA HƯỚNG ĐÃ CHỌN}.
   Subject: {cột subject}.
   Single centered asset, front view, transparent background, no text, no watermark, no drop shadow outside the asset bounds.
   {nếu có nine_slice_LTRB}: UI frame with uniform border thickness, empty flat center, corners decorative, straight edges that can be stretched.
   {nếu là World}: top-down orthographic view, light from top-left, fits a {export_px} canvas.
   Palette: {mã màu của hướng}.
   ```

   Prompt nền của từng hướng nằm trên mỗi card trong Figma: A pixel gỗ, B kính tối viền cyan, C giấy da hoạt hình.
3. Sinh ở độ phân giải lớn (1024 px), rồi **tách nền**, **cắt sát**, thu về đúng `export_px`. Hướng A: thu bằng nearest-neighbor về lưới pixel; hướng B/C: thu bằng Lanczos.
4. Lưu PNG RGBA tại `target_path`. Tên file và thư mục phải khớp chính xác, vì code/prefab dựa vào đường dẫn.
5. Cài đặt import trong Unity:
   - Texture Type: Sprite (2D and UI), Sprite Mode: Single, Mesh Type: Full Rect.
   - PPU: theo cột `ppu` (UI 100; World 96 cho ảnh 96 px/ô).
   - Filter: Point (A) hoặc Bilinear (B/C). Compression: None cho UI. Max Size ≥ cạnh dài.
   - Có `nine_slice_LTRB` thì đặt Border trong Sprite Editor đúng 4 số đó.
6. Đổi `status` dòng đó thành `done` (hoặc `review`) và commit theo quy tắc 8 của [Team_Workflow.vi.md](../Team_Workflow.vi.md). Ví dụ: `art(ui): replace HUD panel with generated frame -- <png> <meta> Docs/Art/asset_manifest.csv`.

## 4. Kiểm tra trước khi nhận một asset

- Nền trong suốt thật (không có ô caro hay viền trắng), không có chữ, không có watermark.
- Đúng kích thước, đúng PPU. Đặt vào prefab không làm đổi vị trí/kích thước khối: test PlayMode `HudAcceptanceTests` (AC1–AC3, AC22) phải xanh.
- Khung 9-slice kéo dãn không vỡ góc ở mọi khối HUD (300×164, 200×200, 64×64…).
- Đọc rõ ở 1280×720: icon 28–40 px vẫn phân biệt được món, chữ trên khung vẫn đủ tương phản.
- Đổi màu chủ đạo thì sửa `HudPalette` và cập nhật test `Palette_MatchesStyleGuide` trong cùng commit.
- Chạy EditMode + PlayMode qua Unity MCP, rồi `git checkout -- ProjectSettings/EditorSettings.asset` (Unity hay tự bật lại Enter Play Mode Options).

## 5. Thứ tự đề xuất

1. **P0 (6 asset)**: khung HUD, 3 trạng thái khe, Hero, ô đất. Thay xong là cả HUD và map đổi diện mạo.
2. **P1**: 34 icon vật phẩm (cần ART-04 nối catalog), menu, vật cản, keycap, minimap, crosshair.
3. **P2**: asset của UI-01 phần 2 và hiệu ứng, sau khi phần 2 build xong.
