# Art Direction và Style Guide — Lastaway

Ngày 31/10/2026 · Art Director. Giữ **hướng A** đã chọn ngày 27/10 (than chì + xanh ngọc + vàng dịu, xem [A_Recolor_Review](../../Art/A_Recolor_Review.vi.md)) và chuẩn icon pixel 32×32 ([CODEX_TASK_32px_Restyle](../../Art/CODEX_TASK_32px_Restyle.md)). Tài liệu này không mở hướng art mới; nó gom hướng A thành luật để mọi ticket dùng chung.

## 1. Ý tưởng: "Trạm cuối trong rừng ngọc"

Một nhóm người sống sót bị thả vào khu rừng nhiệt đới cổ lúc chạng vạng. Rừng tối và dày (than chì xanh), thứ phát sáng duy nhất là **ngọc bích** (đá, đom đóm, dấu hiệu an toàn) và **đồng thau cũ** (vật có giá trị, phần thưởng, thứ hạng). Vòng bo là **sương tím** nuốt dần khu rừng. Người cuối cùng còn đứng là "Lastaway".

### Moodboard bằng chữ

- **Không khí:** chạng vạng, ẩm, yên lặng trước bão. Căng nhưng không kinh dị.
- **Hình khối:** pixel 16-bit rõ khối, viền ngoài tối 1 px, đổ bóng 3 tông, ánh sáng từ trên-trái.
- **Chất liệu UI:** tấm đá than chì mài phẳng, mép vát 1–2 px màu `#526B69`, viền ngoài `#0C1417`. Vàng dịu chỉ cho viền thứ quan trọng (nút chính, ô đang chọn, hạng #1). Không gỗ nâu, không giấy da (đã bỏ ở các hướng cũ).
- **Ánh sáng:** nền tối, điểm sáng nhỏ màu ngọc/đồng. Màn chờ có hạt bụi/đom đóm trôi chậm. Trong trận dùng vignette nhẹ, sương tím ở mép bo.
- **Nhân vật và vật phẩm:** silhouette đọc được ở 32 px; vũ khí thô sơ, đồ rừng (ná cao su, rìu, chuông, hạt giống). Màu chất liệu bão hòa vừa, nổi trên nền rừng tối.
- **Âm hưởng cảm xúc:** thắng là "ánh ngọc bùng lên giữa rừng tối"; bị loại là "sương tím phủ kín".

### Bản sắc cần giữ ở mọi màn

1. Logo LASTAWAY pixel vàng dịu.
2. Nền rừng pixel nhiều lớp (đang có ở Home); mở rộng cho Phòng chờ, Tải, Kết quả, Mất kết nối, có thể làm mờ/tối thêm khi cần nền cho chữ.
3. Một nút chính duy nhất mỗi màn, xanh ngọc viền vàng.
4. Ánh ngọc là tín hiệu "an toàn/tiến lên", đồng thau là "giá trị/thứ hạng", đỏ là "máu/nguy hiểm", tím là "vòng bo".

## 2. Token màu

Giữ tên theo `HudPalette.cs` để unity-dev đổi giá trị mà không đổi API. Ba giá trị **sửa** để đạt tương phản 4,5:1 (đo trong [01_Visual_Audit](01_Visual_Audit.vi.md) mục 2).

| Token | Hex | Vai trò | Ghi chú |
| --- | --- | --- | --- |
| `Background` | `#0B1114` | Nền tối nhất, scrim | Chữ Text trên nền này 16,78:1 |
| `Surface` | `#202A2D` @0,92 | Panel | |
| `Raised` / `Edge` | `#0C1417` | Ô nhập, viền ngoài, track | |
| `Line` | `#526B69` | Mép vát, đường chia | |
| `Text` | `#EDF2EE` | Chữ chính | 12,96:1 trên Surface |
| `Muted` | `#A6BBB7` | Nhãn phụ | 7,28:1 |
| `Faint` | **`#859E98`** (cũ `#77908A`) | Gợi ý, chữ mờ | 5,13:1 (cũ 4,29:1) |
| `Green` (jade) | `#63BAA5` | Chữ tích cực, đếm giờ, thanh tiến trình | 6,36:1 |
| `PrimaryFill` | **`#2E7263`** (cũ `#3B8D7B`) | Nền nút chính | Text trên nền này 5,01:1 (cũ 3,51:1) |
| `PrimaryHover` | `#2E7263` + viền sáng `#E3CC94` + scale 1,03 | Hover nút chính | Không làm sáng nền để giữ tương phản |
| `SelectedFill` | `#28534F` @0,25 | Ô đang chọn | |
| `Amber` (đồng) | `#C6AE78` | Viền nút chính, logo, hạng, mã phòng | 6,80:1 |
| `Yellow` | `#F5C542` | Adrenaline, cảnh báo | Chỉ cho nghĩa riêng |
| `Red` | `#E5484D` | Thanh máu, vùng nguy hiểm, fill | Không dùng cho chữ < 24 px |
| `RedText` | **`#F06A6E`** (mới) | Chữ lỗi, nhãn hành động phá hủy | 4,89:1 |
| `DangerFill` | `#2E1A1C` (mới) | Nền nút phá hủy (Xóa, Đăng xuất, Thoát trận) | Viền `RedText`, chữ `Text` |
| `Blue` | `#5BB8E8` | Giáp | |
| `Purple` | `#A274FF` | Vòng bo, sương | |
| `Scrim` | `#0B1114` @0,72 | Lớp tối sau modal | Mới, cho V3 |

Luật: không viết hex trong view; mọi màu đi qua `HudPalette` (HUD) hoặc `UiArtSet` (menu). Không dùng màu chỉ để phân biệt (luôn kèm icon/chữ).

## 3. Chữ

Canvas tham chiếu 1920×1080. Cỡ là `fontSize` TMP.

| Bậc | Cỡ | Font | Dùng cho |
| --- | ---: | --- | --- |
| Display | 72 | Be Vietnam Pro Bold | "CHIẾN THẮNG", "BẠN ĐÃ BỊ LOẠI" |
| H1 | 40 | Be Vietnam Pro Bold | Tiêu đề màn, số máu HUD |
| H2 | 28 | Be Vietnam Pro Bold | Tiêu đề panel, đếm ngược |
| H3 | 22 | Be Vietnam Pro Bold | Nhãn nút, tên người chơi trong bảng |
| Body | 18 | Be Vietnam Pro Medium | Nội dung, ô nhập |
| Label | 16 | Be Vietnam Pro Medium | Nhãn phụ, keycap |
| Caption | 14 | Be Vietnam Pro Medium | Chú thích, chỉ khi không quan trọng. **Không nhỏ hơn 14** |

Luật font:
1. **Pixelify Sans chỉ cho logo và trang trí không có dấu, không có số.** Mọi số (máu, đạn, đồng hồ, hạng, mã phòng) dùng Be Vietnam Pro Bold. Lý do: V7, "5"/"S", "7"/"ㄱ", "C"/"0" nhầm trong ảnh chụp.
2. Số thay đổi liên tục (đồng hồ, máu, đạn) dùng `<mspace=0.6em>` của TMP để không nhảy độ rộng.
3. Mã phòng: Be Vietnam Pro Bold, giãn chữ 0,15 em, màu Amber.
4. Nhãn không bị cắt: tên vật phẩm trong ô dùng tên ngắn tiếng Việt từ catalog (≤ 10 ký tự) hoặc chỉ hiện khi hover; không dùng "…".
5. Chữ trên nền rừng luôn có panel hoặc bóng chữ 2 px `#0B1114`.

## 4. Khoảng cách, khung, lưới

- Bước 4 px: `4 · 8 · 12 · 16 · 24 · 32 · 48 · 64`.
- Padding panel 24; khoảng giữa nhóm 32; giữa phần tử trong nhóm 12; giữa nút xếp dọc 12.
- Lề an toàn màn 32 (HUD 24).
- Nút: cao 48 (chính) / 40 (phụ); rộng tối thiểu 160; vùng bấm ≥ 44.
- Bo góc 0–2 px (pixel), mép vát 2 px. Không bo tròn lớn.
- Panel modal rộng 480 / 640 / 960; danh sách dòng cao 44.
- Một màn có tối đa một panel chính ở giữa; panel phụ (danh sách người chơi, bảng xếp hạng) cùng chiều cao.

## 5. Motion

Nguyên tắc (theo skill `ui-ux-pro-max`): vào thì giảm tốc (ease-out), ra thì tăng tốc (ease-in) và nhanh hơn vào, tiến trình đều dùng linear, không animation vô hạn trang trí trừ chỉ báo chờ.

| Token | Thời lượng | Easing | Dùng cho |
| --- | ---: | --- | --- |
| `instant` | 0 ms | — | Số HUD đổi giá trị (không tween số máu) |
| `press` | 80 ms | OutQuad | Nút nhấn: scale 1 → 0,96 → 1 |
| `hover` | 120 ms | OutQuad | Hover: viền sáng, scale 1,03 |
| `panelIn` | 220 ms | OutCubic | Panel vào: alpha 0→1 + trượt lên 16 px |
| `panelOut` | 160 ms | InCubic | Panel ra: alpha 1→0 |
| `modalIn` | 200 ms | OutBack (overshoot 1,2) | Modal: scale 0,94→1 + Scrim 0→0,72 |
| `toast` | vào 180 / giữ 2000 / ra 240 ms | OutCubic / InCubic | Toast nhặt đồ, thông báo |
| `countTick` | 250 ms | OutCubic | Đếm ngược mỗi giây: scale 1,25→1; 3 giây cuối đổi Amber → RedText |
| `hitMarker` | hiện 0 / giữ 80 / mờ 120 ms | Linear | Dấu trúng |
| `damageNumber` | 500 ms | OutQuad | Số sát thương bay lên 24 px, mờ 200 ms cuối |
| `emphasis` | 450 ms | OutBack | Tiêu đề kết quả: scale 1,4→1; dòng bảng xếp hạng stagger 40 ms |
| `progress` | theo thực tế | Linear | Thanh tải, vòng chờ |
| `spinner` | 1 vòng / 900 ms | Linear | Đang kết nối, đang tải |

- **Giảm chuyển động** (mục cài đặt mới, gợi ý ghép vào UI-03 Cài đặt): thay trượt/scale bằng fade ≤ 150 ms, tắt rung màn hình và stagger.
- Rung màn hình: chỉ khi bản thân trúng đòn mạnh hoặc nổ gần; biên độ ≤ 6 px, 120 ms; đã có cờ "Rung màn hình" ở ESC.
- Chuyển panel lobby dựa trên `CanvasGroup` sẵn có của `LobbyPanelView`: tween alpha thay cho đặt 0/1; không SetActive (giữ quy tắc G-1 trong code).
- Thư viện tween: tech-lead quyết. Giả định của phòng: một tweener nhỏ dùng `Time.unscaledDeltaTime`, không cấp phát mỗi frame; DOTween có sẵn trong `Assets/Imported/JSurviv/Plugins` là phương án thay thế nếu tech-lead cho dùng.

## 6. Quy chuẩn brief cho art AI-render

Art AI chỉ thay sprite; không đổi bố cục, kích thước RectTransform hay code. Ghi đè đúng file, giữ `.meta` ([Asset_Pipeline](../../Art/Asset_Pipeline.vi.md) mục 2).

### 6.1 Mẫu brief (một asset hoặc một pack)

```text
ID manifest:        <id trong Docs/Art/asset_manifest.csv>
Dùng ở:             <prefab/đường dẫn node>
Kích thước xuất:    <W×H> RGBA, PPU <n>, border 9-slice L,T,R,B = <…>
Lưới gốc:           32×32 (icon) hoặc bội số 16 (khung), phóng nearest-neighbor ×2
Bảng màu:           Tools/Art/pixel_palette.json, ≤ 8 màu/icon
Hướng:              Hướng A "Trạm cuối trong rừng ngọc"; ánh sáng trên-trái; viền ngoài 1 px #0C1417
Nội dung:           <mô tả silhouette, chất liệu, điều cần phân biệt với asset gần giống>
Không được:         chữ/số trong ảnh, gradient, khử răng cưa, alpha bán trong suốt, bóng đổ ngoài khung
Nền:                trong suốt (icon) / đặc (key art)
Tham chiếu:         <ảnh hướng A trong Docs/Art/directions/A_charcoal_jade_*.png>
Tiêu chí duyệt:     mục 6.2
```

### 6.2 Checklist duyệt asset trước khi gắn prefab

1. Đúng kích thước, PPU, border; Filter Point, không nén mất màu (theo manifest).
2. Alpha chỉ 0/255; không viền sáng quanh mép.
3. Màu nằm trong bảng chung; mỗi icon ≤ 8 màu.
4. Silhouette nhận ra ở 32 px khi xếp cạnh các asset dễ nhầm (danh sách cặp trong [UI_Asset_Packs](../../Art/UI_Asset_Packs.vi.md)).
5. Đặt trên `Surface` và trên nền rừng: vẫn rõ.
6. Không chứa chữ; chữ luôn là TMP để dịch và đổi cỡ.
7. Ghi nguồn sinh ảnh và prompt vào cột ghi chú của manifest.

### 6.3 Thứ tự ưu tiên art AI

1. Key art nhân vật cho Home/Phòng chờ/Kết quả (một nhân vật, 3 tư thế: đứng, thắng, ngã).
2. Nền rừng pixel 3 lớp parallax 1920×1080 dùng chung mọi màn ngoài trận.
3. Khung panel/nút/ô (pack UI-P01) theo chất liệu đá than chì.
4. 34 icon vật phẩm + 8 icon hiệu ứng (đang làm trong ADHOC-UI-ASSETS-A).
5. Minh họa trạng thái trống (3 hình nhỏ 256×256: rương trống, cây đổ, đom đóm tắt).
