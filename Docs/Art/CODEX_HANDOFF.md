# Bàn giao cho Codex: thay asset demo bằng asset thật (24/10/2026)

Mục tiêu: thay toàn bộ asset **demo** của Lastaway (Jungle Maze) bằng asset thật do Codex **sinh ảnh**, mà không phải sửa bố cục, logic hay test.

## 0. Việc đầu tiên: hỏi người dùng chọn hướng art

Có 6 hướng, cùng một bố cục HUD v2 đã duyệt; mỗi hướng có HUD và Menu chính. Ảnh nằm trong [directions/](directions/), Figma ở page `Lastaway v3 — Art directions` (file key `oeZgisxdiR80hRqbikUwPb`):

| Hướng | Ảnh | Ghi chú |
| --- | --- | --- |
| A · Rừng Pixel | `directions/A_jungle_pixel.png` | Đợt 1 |
| B · Btooom Tác chiến | `directions/B_btooom_tactical.png` | Đợt 1 |
| C · Đảo phiêu lưu | `directions/C_island_adventure.png` | Đợt 1 |
| **D · Đền rừng cổ** | `directions/D_jungle_temple.png` | Đợt 2, theo Core Keeper. Khuyến nghị nếu giữ chất pixel của GDD |
| **E · Hoạt hình sáng** | `directions/E_bright_cartoon.png` | Đợt 2, theo Super Animal Royale. Khuyến nghị nếu ưu tiên dễ đọc, vui nhộn |
| **F · Mực vẽ tay** | `directions/F_ink_handdrawn.png` | Đợt 2, theo Don't Starve Together |

**Chưa sinh ảnh khi người dùng chưa chọn hướng.** Ghi lựa chọn vào đầu file [Asset_Pipeline.vi.md](Asset_Pipeline.vi.md).

## 1. Đọc trước

1. [Asset_Pipeline.vi.md](Asset_Pipeline.vi.md): asset demo nào thay bằng gì, mẫu prompt, cách tách nền/thu nhỏ/import Unity, danh sách kiểm tra.
2. [asset_manifest.csv](asset_manifest.csv): 73 asset (17 HUD, 6 menu, 41 icon, 9 world). Mỗi dòng có `target_path`, `export_px`, `nine_slice_LTRB`, `ppu`, `subject`, `priority`, `status`.
3. Quyết định "art tạm, AI render sau": mục đầu của [../UI_Wireframes.vi.md](../UI_Wireframes.vi.md). Ghi chú kỹ thuật HUD: `.claude/memory/ui-01-hud.md`.

## 2. Thứ tự làm

1. Sau khi chốt hướng: cập nhật màu trong `Assets/_JungleMaze/Scripts/Presentation/UI/Hud/HudPalette.cs` (hoặc giá trị trên prefab nếu palette được serialize). Trong cùng commit, sửa test PlayMode `Palette_MatchesStyleGuide` cho khớp.
2. **P0 (6 dòng)**: `hud_panel`, `hud_slot`, `hud_slot_selected`, `hud_slot_empty`, `world_hero`, `world_ground`. **Ghi đè đúng file cũ, giữ `.meta`** để prefab tự nhận ảnh. Sau đó sửa PPU và Border 9-slice theo manifest.
   - Lưu ý: `Hero48.png` và `Cell48.png` đang dùng PPU 48. Ảnh 96 px thì đặt PPU 96.
3. **P1**: icon 18 vũ khí + 16 trang bị, menu, vật cản, keycap, minimap, crosshair. Icon chỉ đặt đúng đường dẫn; gắn vào HUD cần ART-04 (catalog `code → Sprite`, HUD hiện có `IconPlaceholder`).
4. **P2**: asset phần 2 của UI-01 và icon hiệu ứng. Build phần 2 có bàn giao riêng ở [../Tickets/UI-01_HANDOFF.md](../Tickets/UI-01_HANDOFF.md) mục B.

Mỗi asset làm xong: đổi `status` trong CSV thành `done`, chạy EditMode + PlayMode qua Unity MCP (`HudAcceptanceTests` phải xanh), rồi commit.

## 3. Quy tắc của người dùng (bắt buộc)

- **Commit ngay khi xong mỗi phần**, message tiếng Anh ngắn kiểu `art(hud): replace panel frame with generated asset`. **Không** ghi Claude/Codex/AI, **không** thêm `Co-Authored-By`.
- Chỉ add đúng file của phần đó, kèm `.meta`, và commit kèm đường dẫn: `git commit -m "…" -- <file…>`. **Không push** nếu người dùng chưa bảo.
- **Không commit** `ProjectSettings/EditorBuildSettings.asset`, `ProjectSettings.asset`, `ShaderGraphSettings.asset` (đang bị sửa dở từ trước). Sau mỗi lần chạy PlayMode: `git checkout -- ProjectSettings/EditorSettings.asset`, vì file này phải giữ `m_EnterPlayModeOptions: 0`.
- Không đụng vùng code của thành viên khác: Hào (Hero/map/balance), Vinh (backend). Chỉ thay ảnh và chỉnh import setting; muốn đổi prefab hay logic thì hỏi trước.
- Ảnh sinh ra phải là tác phẩm mới. Chỉ lấy *cảm hứng* từ các game Steam được nêu; không sao chép nhân vật, logo hay asset của họ.
- Font chữ tiếng Việt giữ **Be Vietnam Pro**. Font số/tiêu đề theo hướng đã chọn, nhưng chỉ dùng cho ký tự không dấu.

## 4. Khi xong một đợt

Báo người dùng: hướng đã chọn, danh sách asset `done`, hash commit, kết quả test, ảnh chụp Game view 1280×720 so với ảnh hướng art tương ứng.
