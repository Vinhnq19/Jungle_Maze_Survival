# Nhiệm vụ cho Codex: chuẩn hóa asset về pixel art 32×32 đồng bộ

Giao ngày 27/10/2026. Bắt đầu khi Codex có lại hạn mức (khoảng 5:40 sáng 28/10). Người dùng yêu cầu:

> "chuẩn hóa lại assets cho chúng đồng bộ về phong cách, màu sắc, đồ họa (lưu ý là các ảnh mà nó gen lại như tay không và dây thừng đang bị chi tiết quá, nó rất chi tiết cần chuẩn hóa lại đơn giản phong cách 32x32px thôi)"

**Đọc trước:** [../Agent_Coordination.vi.md](../Agent_Coordination.vi.md) (đọc dòng mới nhất ở mục 4), [../Tickets/ADHOC-UI-ASSETS-A.md](../Tickets/ADHOC-UI-ASSETS-A.md) mục 5 (Claude đã import và gắn asset vào Unity tới đâu), [A_Recolor_Review.vi.md](A_Recolor_Review.vi.md), bảng màu `directions/A_figma_recolor_readback.json`.

## 1. Vấn đề hiện tại (đã đo)

- `HAND` và `ROPE`: ảnh ImageGen khoảng 1000 px được thu về 52–56 px bằng **Lanczos**, nên giữ rất nhiều chi tiết. Tông màu và độ mịn không giống các icon khác.
- Các icon JSurviv có ảnh gốc từ 12 px tới 200 px, rồi đều được phóng hoặc thu về cạnh dài 56 px trong khung 64 px. Hệ số phóng không phải số nguyên, nên **mỗi icon có cỡ "pixel" khác nhau**.
- 4 icon vẽ lại (`SPIKE_HELMET`, `WOODEN_ARMOR`, `ADRENALINE`, `SEED`) và 7 icon hiệu ứng đã vẽ ở lưới 32 px: đây là chuẩn đúng, giữ làm mẫu.
- Chi tiết từng icon: `Production_A/Items/Normalization.json` (`resize_from`, `resize_method`).

## 2. Chuẩn phong cách bắt buộc

| Mục | Chuẩn |
| --- | --- |
| Lưới gốc | Vẽ trên **32×32 px**. Nội dung có cạnh dài **26–28 px**, căn giữa, cách mép tối thiểu 2 px |
| File xuất | **64×64 RGBA = phóng ×2 bằng nearest-neighbor** từ ảnh 32×32. Không Lanczos, không bilinear, không khử răng cưa |
| Alpha | Chỉ 0 hoặc 255, không có pixel bán trong suốt |
| Viền | Viền ngoài 1 px (ở lưới 32) màu `#0C1417`. Không vẽ nét chi tiết bên trong dày hơn 1 px |
| Bảng màu | Một **bảng chung ≤ 24 màu**, lưu ở `Tools/Art/pixel_palette.json`. Gồm các tông UI hướng A (`#0C1417`, `#202A2D`, `#526B69`, `#77908A`, `#A6BBB7`, `#EDF2EE`, `#63BAA5`, `#28534F`, `#C6AE78`), cộng các dải chất liệu, mỗi dải 3 tông (tối/gốc/sáng): gỗ, kim loại, da, đỏ, lá xanh, xanh dương, tím, vàng. **Mỗi icon ≤ 8 màu**, chỉ lấy từ bảng chung |
| Đổ bóng | 3 tông cho mỗi chất liệu, ánh sáng từ trên-trái. Không gradient, không dither (tối đa 1 hàng dither nếu thật sự cần) |
| Độ đơn giản | Hình dáng phải nhận ra ở ô 28 px và toast 18 px. Bỏ chi tiết nhỏ hơn 2 px gốc. Không chữ, không logo |

**HAND:** nắm đấm đơn giản, 3 tông da + viền, khoảng 6 màu, không vẽ móng tay hay nếp nhăn. **ROPE:** cuộn dây 2–3 vòng, 3 tông nâu + viền, đầu dây thò ra một đoạn ngắn. Cả hai vẽ trực tiếp ở lưới 32 px (công thức tay trong script như `original_sprite()`), **không** thu nhỏ từ ảnh AI.

## 3. Phạm vi

1. **34 icon vật phẩm** trong `Assets/_JungleMaze/Art/UI/Icons/{Weapons,Equipments}/<ID>.png`. Đưa hết về lưới 32 px:
   - Ảnh gốc ≤ 32 px: đặt lại lên lưới 32 không resample, rồi quantize vào bảng chung và thêm viền.
   - Ảnh gốc > 32 px: thu bằng nearest về cạnh dài 26–28, quantize, rồi **sửa tay theo công thức** chỗ nào bị vỡ.
   - Giữ đúng món: `Provenance.csv` ghi lại nguồn.
2. **7 icon hiệu ứng** `Art/UI/Icons/Effects/*.png`: đã ở lưới 32. Chỉ đối chiếu với bảng màu chung và cùng kiểu viền.
3. **Sprite HUD** (`Art/Hud/*.png`) và **menu** (`Art/UI/Menu/*.png`): chỉ kiểm tra màu có thuộc bảng hướng A không, và đồng bộ với icon (viền, độ tương phản). Không đổi kích thước, không đổi viền 9-slice. Nếu cần sửa thì ghi vào báo cáo trước, không tự sửa hàng loạt.

4. **Việc bổ sung (CPO duyệt 27/10 02:15, được sửa trực tiếp, không cần ghi báo cáo trước):**
   - `Assets/_JungleMaze/Art/UI/Menu/KeyArt.png` không có alpha: Unity import RGB24, nên trong menu hiện thành ô vuông nền tối. Xuất lại **RGBA, nền trong suốt** quanh hòn đảo/key art, giữ nguyên kích thước 1040×1040. `Background.png` để nền đặc như cũ.
   - `Assets/_JungleMaze/Art/Hud/EffectFrame.png`: viền dày 3 px nằm sát thanh thời gian cùng màu, nên cạnh dưới trông dày hơn. Làm viền mỏng còn 2 px (ở 88×88) và để 1 px trống giữa viền với vùng thanh thời gian. Giữ kích thước 88×88 và viền 9-slice 18 px.
   - **7 icon hiệu ứng** (`Art/UI/Icons/Effects/*.png`) đang là 256×256. Khi thu về ô 20 px bằng Point, hình bị vỡ. Xuất lại **64×64 = lưới 32 ×2 nearest**, đúng chuẩn mục 2 như icon vật phẩm, giữ tên file. Ghi đè đúng tên, không sửa `.meta`. Importer vẫn đúng vì không đặt max size cố định theo 256.
   - Các sprite đã import nhưng chưa gắn (`BarFill`, `BarTrack`, `MinimapFrame`, `Crosshair`, `HitMarker`): chỉ đồng bộ màu với bảng chung, không đổi kích thước.

## 4. Cách làm và sản phẩm

- Cập nhật `Tools/Art/normalize_item_icons.py`: thêm chế độ `--native32` thực hiện đúng mục 2. Các bước gồm quantize theo `pixel_palette.json`, thêm viền, căn giữa, phóng ×2 nearest. Script phải chạy lại được.
- Script **tự kiểm** và in kết quả cho từng icon; fail thì dừng:
  - (a) ảnh 64×64;
  - (b) mọi khối 2×2 cùng một màu, tức đúng là ×2 của ảnh 32;
  - (c) alpha chỉ 0 hoặc 255;
  - (d) mọi màu thuộc bảng chung, mỗi icon ≤ 8 màu;
  - (e) nội dung cạnh dài 26–28 px ở lưới gốc, lệch tâm ≤ 1 px;
  - (f) có viền `#0C1417` quanh hình.
- Xuất lại contact sheet `Production_A/Items/Contact_{18,28,40}px_*.png`, thêm sheet lưới gốc 32 px. Cập nhật `Normalization.json` (`native_size: 32`, `scale: 2`, `method: nearest`) và `Provenance.csv`.
- **Ghi đè PNG đúng tên, đúng đường dẫn.** Không xóa, không đổi tên, **không sửa `.meta`**. Claude đã import và đặt Point, no mipmap, PPU, viền; Unity sẽ tự nhận ảnh mới và giữ nguyên liên kết prefab/catalog.

## 5. Quy tắc phối hợp (bắt buộc)

- Trước khi bắt đầu: đọc mục 4 của `Docs/Agent_Coordination.vi.md`, rồi ghi một dòng `· Codex · bắt đầu chuẩn hóa 32 px (PNG + Tools/Art + Docs/Art)`. Xong thì ghi tiếp một dòng kèm hash commit.
- **Chỉ sửa:** PNG trong `Art/UI/Icons/**` (và `Art/Hud/**`, `Art/UI/Menu/**` khi đã ghi trước ở báo cáo), `Tools/Art/**`, `Docs/Art/**`. **Không sửa:** `.meta`, C#, prefab, scene, `Bootstrap.cs`, ticket của Claude, `Assets/Imported/**`.
- **Không dùng Unity Editor** (Claude đang giữ Editor để gắn asset và QA). Kiểm hình bằng contact sheet và script.
- Git: commit kèm đường dẫn (`git commit -m "…" -- <file…>`), không `git add -A`, không stage, reset hay checkout file của bên khác. Message tiếng Anh ngắn, ví dụ `art(items): redraw item icons on a shared 32px grid`, `art(items): add shared pixel palette and native32 normalizer`. **Không** ghi Claude/Codex/AI, không `Co-Authored-By`, không push. Không commit `ProjectSettings/*`, `.codex/config.toml`.
- Mỗi lỗi tối đa 3 vòng sửa. Quá thì ghi ID đó vào báo cáo và làm tiếp phần khác.

## 6. Báo cáo khi xong

Ghi vào `Docs/Art/Production_A/Items/Restyle32_Report.vi.md`:
- Kết quả tự kiểm (a)–(f) cho từng icon.
- Contact sheet trước/sau.
- Danh sách ID đã vẽ lại tay.
- Màu ngoài bảng (nếu có) và lý do.
- Hash các commit.

Sau đó báo người dùng. Claude sẽ refresh Unity, chạy lại test catalog/HUD và chụp HUD thật để xác nhận.
