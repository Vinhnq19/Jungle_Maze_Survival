# Báo cáo chuẩn hóa icon 32 px

Ngày 27/10/2026 (thời điểm UTC của kiểm thử lưu trong bằng chứng). Phạm vi: 34 item + 7 effect; HUD/menu chỉ đọc. Kết quả: **41/41 icon đạt đủ (a)–(f)**; không có ID icon còn lỗi kỹ thuật.

## Sản phẩm và quyết định

- [Bảng màu chung](../../../../Tools/Art/pixel_palette.json) có đúng 24 màu, đủ 9 tông hướng A. Tám dải chất liệu dùng chung một số màu sáng để không vượt giới hạn: đỏ dùng sáng da, lá dùng vàng sáng, xanh dương/tím dùng sáng kim loại. Mỗi icon thực tế dùng 4–8 màu; HAND và ROPE đều 3 tông chất liệu + viền (4 màu).
- Tất cả PNG runtime giữ tên/đường dẫn, xuất RGBA 64×64 từ lưới 32×32 bằng nearest ×2; alpha chỉ 0/255; viền ngoài được thêm 1 px ở lưới gốc bằng màu `#0C1417`. Không sửa `.meta`, mã C#, prefab, scene, tài nguyên Imported, ProjectSettings hoặc cấu hình công cụ. Không dùng Unity Editor/MCP, không push.
- Công cụ `normalize_item_icons.py --native32` kiểm trước toàn bộ batch, lỗi pixel thì dừng trước khi ghi runtime; kiểm thêm PNG sau khi mã hóa. `--native32 --check-only` kiểm file thực tế mà không ghi. Chế độ native32 không nhận `--hand`, `--rope` hoặc `--allow-missing`; đường xử lý cũ vẫn giữ để tái hiện lịch sử.
- 21 icon được vẽ lại trực tiếp bằng tọa độ nguyên trong [native32_recipes.py](../../../../Tools/Art/native32_recipes.py). Các nguồn chỉ 12–20 px nội dung không thể vừa giữ nguyên từng pixel vừa đạt cạnh dài 26–28 px; chọn vẽ lại, không phóng ảnh nhỏ. HAND/ROPE hoàn toàn không đọc hai ảnh sinh trước đây. Bốn icon đã có công thức cũ được vẽ lại gọn hơn, giữ nhận dạng và bỏ nhãn hiệu.
- 13 item còn lại lấy đúng PNG/slice nguồn JSurviv, thu nearest về cạnh dài 26 trước khi thêm viền; KNIFE/SHOTGUN giữ phép chỉnh độ dày và hướng chéo bằng nearest cho dễ đọc. Các slice BELL, METEORITE_FRAGMENT, SHOTGUN vẫn tra đúng tên từ `.meta` nguồn (chỉ đọc).
- Sửa mảng chất liệu trực tiếp trên lưới sau khi thu cho TREE_BRANCH, SHOTGUN, AXE, MACE, RUBBER_GUN: làm sáng gỗ/kim loại, giữ alpha và silhouette nguồn. Đầu AXE chuyển từ xanh lá sang kim loại. [Inspection_Round1.png](Inspection_Round1.png) ghi tình trạng trước sửa độ sáng.
- Bảy effect lấy đúng bản gốc 32 px đã vẽ ở `Production_A/UI/*_native32.png`, đưa vào palette, thêm viền và căn/thu vừa khung. Ghi đè PNG 256² cũ thành 64²; không thay ý nghĩa biểu tượng. Các nguồn effect cũ giữ nguyên.
- [Normalization.json](Normalization.json) ghi `native_size: 32`, `scale: 2`, `method: nearest`, số đo và từng kiểm tra; [Provenance.csv](Provenance.csv) có 41 dòng, nguồn/công thức, bản native và SHA-256 nguồn/đích. [Native32Sources](Native32Sources) lưu cả 41 PNG native cuối cùng. `Before32` giữ nguyên báo cáo/contact sheet cũ; công cụ chạy lại không ghi đè baseline này.

Danh sách 21 ID vẽ tay: `HAND`, `ROPE`, `SCEPTER`, `CROSSBOW`, `SEED`, `WOODEN_HELMET`, `SPIKE_HELMET`, `MAGIC_HELMET`, `WOODEN_ARMOR`, `ARMOR`, `MAGIC_ARMOR`, `GOD_LEAF`, `SPIRIT_TEAR`, `MERMAID_TAIL`, `PHOENIX_FEATHERS`, `UNICORN_BLOOD`, `ADRENALINE`, `ELIXIR`, `MAGIC`, `ELIXIR_OF_LIFE`, `COMPASS`.

## Tự kiểm (a)–(f) từng icon

(a) RGBA 64²; (b) mỗi khối RGBA 2² đồng nhất; (c) alpha nhị phân; (d) thuộc palette, ≤8 màu; (e) cạnh dài native 26–28, lệch tâm ≤1 và đệm ≥2; (f) mọi pixel biên 8 hướng, gồm cả lỗ trong hình, là màu viền và có mảng vật liệu khác màu viền. Độ dày viền xuất phát từ phép thêm một vòng pixel ngoài silhouette. Không tính RGB của pixel alpha 0 vào màu hiển thị.

| ID | (a) | (b) | (c) | (d) | (e) | (f) | Màu | Cạnh native | Lệch tâm |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| HAND | PASS | PASS | PASS | PASS | PASS | PASS | 4 | 28 | [0.0, 0.0] |
| TREE_BRANCH | PASS | PASS | PASS | PASS | PASS | PASS | 4 | 28 | [0.0, 0.0] |
| KNIFE | PASS | PASS | PASS | PASS | PASS | PASS | 7 | 28 | [0.0, -0.5] |
| BONE | PASS | PASS | PASS | PASS | PASS | PASS | 5 | 28 | [0.0, -0.5] |
| AXE | PASS | PASS | PASS | PASS | PASS | PASS | 5 | 28 | [0.0, 0.0] |
| MACE | PASS | PASS | PASS | PASS | PASS | PASS | 6 | 28 | [-0.5, 0.0] |
| RUBBER_GUN | PASS | PASS | PASS | PASS | PASS | PASS | 4 | 28 | [0.0, 0.0] |
| SCEPTER | PASS | PASS | PASS | PASS | PASS | PASS | 7 | 28 | [0.0, 0.0] |
| CROSSBOW | PASS | PASS | PASS | PASS | PASS | PASS | 6 | 28 | [0.0, 0.0] |
| SHOTGUN | PASS | PASS | PASS | PASS | PASS | PASS | 5 | 28 | [0.0, -0.5] |
| BANANA | PASS | PASS | PASS | PASS | PASS | PASS | 8 | 28 | [-0.5, 0.0] |
| SMOKE | PASS | PASS | PASS | PASS | PASS | PASS | 8 | 28 | [0.0, 0.0] |
| METEORITE_FRAGMENT | PASS | PASS | PASS | PASS | PASS | PASS | 5 | 28 | [0.0, 0.0] |
| CRYSTAL | PASS | PASS | PASS | PASS | PASS | PASS | 6 | 28 | [-0.5, 0.0] |
| SEED | PASS | PASS | PASS | PASS | PASS | PASS | 6 | 28 | [0.0, 0.0] |
| ROPE | PASS | PASS | PASS | PASS | PASS | PASS | 4 | 28 | [0.0, -0.5] |
| BELL | PASS | PASS | PASS | PASS | PASS | PASS | 8 | 28 | [-0.5, 0.0] |
| SAHUR_BAT | PASS | PASS | PASS | PASS | PASS | PASS | 5 | 28 | [-0.5, 0.0] |
| WOODEN_HELMET | PASS | PASS | PASS | PASS | PASS | PASS | 4 | 28 | [0.0, -0.5] |
| SPIKE_HELMET | PASS | PASS | PASS | PASS | PASS | PASS | 5 | 28 | [0.0, 0.0] |
| MAGIC_HELMET | PASS | PASS | PASS | PASS | PASS | PASS | 7 | 28 | [0.0, 0.0] |
| WOODEN_ARMOR | PASS | PASS | PASS | PASS | PASS | PASS | 5 | 28 | [0.0, 0.0] |
| ARMOR | PASS | PASS | PASS | PASS | PASS | PASS | 5 | 28 | [0.0, 0.0] |
| MAGIC_ARMOR | PASS | PASS | PASS | PASS | PASS | PASS | 6 | 28 | [0.0, 0.0] |
| GOD_LEAF | PASS | PASS | PASS | PASS | PASS | PASS | 4 | 28 | [-0.5, 0.0] |
| SPIRIT_TEAR | PASS | PASS | PASS | PASS | PASS | PASS | 5 | 28 | [0.0, 0.0] |
| MERMAID_TAIL | PASS | PASS | PASS | PASS | PASS | PASS | 4 | 28 | [0.0, 0.0] |
| PHOENIX_FEATHERS | PASS | PASS | PASS | PASS | PASS | PASS | 5 | 28 | [0.0, 0.0] |
| UNICORN_BLOOD | PASS | PASS | PASS | PASS | PASS | PASS | 7 | 28 | [0.0, 0.0] |
| ADRENALINE | PASS | PASS | PASS | PASS | PASS | PASS | 6 | 28 | [0.0, 0.0] |
| ELIXIR | PASS | PASS | PASS | PASS | PASS | PASS | 6 | 28 | [0.0, 0.0] |
| MAGIC | PASS | PASS | PASS | PASS | PASS | PASS | 6 | 28 | [0.0, 0.0] |
| ELIXIR_OF_LIFE | PASS | PASS | PASS | PASS | PASS | PASS | 7 | 28 | [0.0, 0.0] |
| COMPASS | PASS | PASS | PASS | PASS | PASS | PASS | 7 | 28 | [0.0, 0.0] |
| CcImmune | PASS | PASS | PASS | PASS | PASS | PASS | 5 | 26 | [0.0, 0.0] |
| Invisible | PASS | PASS | PASS | PASS | PASS | PASS | 5 | 28 | [0.0, 0.0] |
| Poison | PASS | PASS | PASS | PASS | PASS | PASS | 5 | 28 | [-0.5, 0.0] |
| Reverse | PASS | PASS | PASS | PASS | PASS | PASS | 5 | 28 | [0.0, 0.0] |
| Root | PASS | PASS | PASS | PASS | PASS | PASS | 4 | 28 | [0.0, 0.0] |
| Slow | PASS | PASS | PASS | PASS | PASS | PASS | 5 | 28 | [-0.5, 0.0] |
| Stun | PASS | PASS | PASS | PASS | PASS | PASS | 4 | 26 | [0.0, 0.0] |

Không có màu ngoài palette ở cả 41 icon. Toàn bộ item có cạnh dài 28; effect CcImmune/Stun 26, các effect khác 28. Lệch tâm tối đa 0,5 px; đệm tối thiểu 2 px. 34 item dùng 544 KiB RGBA không mipmap; cả 41 icon dùng 656 KiB, chưa tính chi phí Unity.

## Bằng chứng và chạy lại

- Trước: [18 px](Before32/Contact_18px_slot.png), [28 px](Before32/Contact_28px_slot.png), [40 px](Before32/Contact_40px_slot.png), [effect](Before32/Effects_contact.png).
- Sau: [18 px](Contact_18px_slot.png), [28 px](Contact_28px_slot.png), [40 px](Contact_40px_slot.png), [lưới native 32 px](Contact_32px_grid.png), [effect 18/28/40](Effects_18_28_40px.png), [lưới effect](Effects_32px_grid.png). Có đủ nền slot/trắng/đen/selected ở cả 18/28/40.
- Đã mở và xem sheet trước/sau, 18 px nền slot/trắng, 28 px nền slot, 40 px, native 32 và effect. HAND/ROPE đơn giản hơn rõ rệt; nguồn súng/cành tối đã được sửa. Các món thân mảnh như AXE/MACE/SHOTGUN vẫn cần người xem nhận diện ở 18 px. Không dùng nhận xét này thay kiểm mù ba người của ART-04 hoặc QA runtime.
- [Restyle32_Verification.json](Restyle32_Verification.json) lưu lệnh, stdout/stderr và mã thoát: check-only 41/41; **9/9 unittest đạt**, gồm ảnh sai kích thước/mode, pixel lệch khối 2², alpha bán trong suốt, màu lạ, 9 màu, nội dung nhỏ/lệch tâm, hỏng viền ngoài/lỗ trong hình và ảnh rỗng. Mỗi ca lỗi thực sự bị bộ kiểm phát hiện.
- Chạy lại toàn bộ native32 và so SHA-256: **102/102 artifact giống từng byte**. Nguồn và đích đều khớp hash provenance; toàn bộ HUD/menu khớp hash audit, không bị ghi. Có thể tái hiện bằng các lệnh dưới đây với Python + Pillow; phiên này dùng Python đi kèm runtime vì alias `python.exe` của Windows không chạy được.

```text
python Tools/Art/normalize_item_icons.py --native32
python Tools/Art/normalize_item_icons.py --native32 --check-only
python -m unittest discover -s Tools/Art -p test_native32_validation.py -v
```

## HUD/menu: nhận xét, không sửa

[HudMenu_ReadOnly_Audit.json](HudMenu_ReadOnly_Audit.json) ghi đủ 21 HUD + 6 menu, kích thước, mode, alpha, màu ngoài bảng hướng A và SHA-256.

- 21 sprite HUD là mask trắng trung tính `#FFFFFF`, chưa phải màu hiển thị cuối. Đây là màu ngoài palette của icon nhưng có chủ đích để nhân tint hiện có; tự đổi RGB sang màu hướng A sẽ làm tối hai lần. Viền/tương phản cuối phải xem cùng tint trong Unity; chỉ đọc contact sheet `Production_A/UI/Masks_contact.png` ở phiên này. Giữ nguyên toàn bộ kích thước và viền 9-slice.
- Logo và ba nút menu dùng đúng hướng A; ButtonGhost có alpha tối đa 173 do thiết kế ghost, không áp chuẩn alpha nhị phân dành riêng cho icon. Background có 7 tông cảnh rừng ngoài palette icon; KeyArt có 21 màu ngoài tập hướng A được kiểm (22 màu hiển thị tổng cộng). Danh sách hex và số pixel có đầy đủ trong audit. Đây là minh họa nhiều chất liệu/world và vòng tím; chưa duyệt recolor hàng loạt.
- `KeyArt.png` thực tế có header RGBA nhưng alpha toàn 255: lỗi nền đặc vẫn tồn tại dù định dạng chứa kênh alpha. `EffectFrame.png` vẫn 88², viền nguồn 3 px, border importer 18 px giữ nguyên; nguy cơ cạnh dưới dày khi sát thanh thời gian vẫn còn theo QA trước.
- Quyết định phạm vi: mục 3.4 trong đặc tả cũ cho phép sửa KeyArt/EffectFrame, nhưng chỉ thị hiện tại chỉ cho sửa PNG ở `Icons/**` và yêu cầu HUD/menu chỉ kiểm tra. Ưu tiên chỉ thị hiện tại; ghi hai ID bàn giao dưới đây, không thực hiện ngoại lệ cũ. BarFill/BarTrack/MinimapFrame/Crosshair/HitMarker tiếp tục giữ nguyên mask và trạng thái chưa gắn theo ticket.

## Lỗi gặp, vòng sửa và ID bàn giao

| ID | Vòng xử lý | Kết quả |
| --- | --- | --- |
| R32-READ-DARK (TREE_BRANCH, SHOTGUN; rà thêm AXE/MACE/RUBBER_GUN) | 1 | Sửa mảng sáng chất liệu, xem lại 18/28 px; kỹ thuật (a)–(f) đều đạt. |
| R32-ROPE-RAMP | 1 | Bỏ màu sáng thứ tư, còn 3 tông nâu + viền. |
| R32-WRITE-AXE | 1 lần chạy lại | Một lần ghi AXE báo OSError 22; nguyên nhân chưa xác định. Chạy lại thành công; đã đóng handle đọc rõ ràng và kiểm hash/PNG sau ghi. |
| R32-GIT-INDEX | 1 | Sandbox chặn ghi `.git/index.lock`; commit bằng quyền ghi Git cục bộ được cấp qua cơ chế môi trường, không thay file ngoài phạm vi. |
| KEYART_ALPHA | 0, ngoài phạm vi hiện tại | Còn nền đặc; bàn giao để xuất RGBA có vùng trong suốt, giữ 1040². |
| EFFECTFRAME_THIN | 0, ngoài phạm vi hiện tại | Còn viền 3 px; bàn giao yêu cầu viền 2 px và khoảng trống 1 px, giữ 88²/border18. |
| QA_RUNTIME_18PX | 0, không dùng Editor | Chờ bên tích hợp refresh, test catalog/HUD, chụp HUD và kiểm nhận diện mù; không nhận là đã đạt QA Unity. |

Không lỗi nào vượt 3 vòng. Không có ID item/effect bị bỏ qua hoặc còn fail (a)–(f). Hai file có thay đổi từ trước phiên (`ProjectSettings/TimeManager.asset`, `.codex/config.toml`) được giữ nguyên và không đưa vào commit.

## Commit

| Hash | Nội dung |
| --- | --- |
| `01e477fa` | `art(items): add shared pixel palette and native32 normalizer` — bảng màu, công thức, pipeline, CLI và kiểm thử. |
| `b96db211` | `art(items): redraw item icons on a shared 32px grid` — 41 runtime PNG và 41 native PNG. |

Báo cáo, contact sheet, provenance, bằng chứng và hai dòng nhật ký điều phối nằm trong commit `docs(art): report 32px restyle checks` chứa báo cáo này; lấy hash bằng `git log -1 -- Docs/Art/Production_A/Items/Restyle32_Report.vi.md` (không ghi hash tự tham chiếu vào chính commit). Các commit đều cục bộ, chưa push.
