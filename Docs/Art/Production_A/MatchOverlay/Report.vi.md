# UI-03 — Bộ overlay hướng A

Ngày 27/10/2026. **Hoàn tất: 19/19 PNG đạt kiểm file; 8/8 kiểm thử validator đạt.** Import, binding và runtime Unity chưa được kiểm trong lượt art.

## Sản phẩm và tái tạo

- 8 mask trắng: SliderHandle, ToggleBox, ToggleCheck, ZoneRing, KillcamGrid, PlayerDot, PlayerDotOutline, KillerMark.
- 11 icon màu nguyên bản: 5 nguyên nhân bị loại và 6 icon âm lượng, nhạc, chuột, rung, thoát trận, theo dõi. Vẽ trực tiếp trên lưới 32×32, viền 1 px gốc `#0C1417`, nearest ×2 thành 64×64. Không lấy sprite từ Assets/Imported hoặc game khác.
- 11 nguồn `*_native32.png` ở thư mục báo cáo. [Contact.png](Contact.png) có mọi ảnh ở kích thước xuất 1× và kích thước màn hình, ghép dot/outline/killer, lưới lặp 6×5 ô. Mask preview bằng tint đề xuất. Đã mở kiểm trực quan: icon đọc được tại 28 px, handle có mẫu 20 và 24 px.
- [asset_mapping.json](asset_mapping.json) ghi size, kind, tint, border LTRB, PPU 100, Point, no mipmap, uncompressed, phần tử prefab và 16 đường dẫn reuse kèm SHA-256.
- `Tools/Art/build_ui03_overlay_assets.py` tạo PNG, nguồn native, mapping và contact; `Tools/Art/test_ui03_overlay_validation.py` kiểm file đã encode, ghi [Validation.json](Validation.json).
- Manifest thêm đúng 19 dòng, có ID riêng `ov_player_dot_outline`, used_by là phần tử UI-03, status `imported, not bound`. Giữ toàn bộ byte cũ, UTF-8 không BOM, CRLF. Status là quy ước bàn giao theo yêu cầu, không xác nhận importer đã được kiểm.

Chạy PowerShell:

```powershell
& 'C:\Users\Admin\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -I -X utf8 Tools/Art/build_ui03_overlay_assets.py
& 'C:\Users\Admin\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -I -X utf8 Tools/Art/test_ui03_overlay_validation.py
```

Generator không tự ghi thêm manifest/nhật ký khi chạy lại, tránh trùng dòng. Không mở Unity hoặc viết `.meta`.

## Kết quả kiểm từng file

Mọi ảnh RGBA, đúng size, alpha nhị phân 0/255 và có nội dung. Mask trắng RGB255 tại mọi pixel hiện, chỉ 1 màu. Icon màu có ≤8 màu từ palette chung, khối 2×2 đồng nhất, outline ink quanh mép ngoài và lỗ, cạnh nội dung 52–56 px, padding ≥3 px và lệch tâm ≤1 px tại ảnh xuất. Padding icon thực tế ít nhất 4 px.

| File | Size | Số màu hiện | Padding nhỏ nhất (px xuất) | Lệch tâm lớn nhất (px xuất) | Kết quả |
| --- | --- | --- | --- | --- | --- |
| `SliderHandle.png` | 48×48 | 1 | 4 | 0.0 | Đạt |
| `ToggleBox.png` | 48×48 | 1 | 4 | 0.0 | Đạt |
| `ToggleCheck.png` | 32×32 | 1 | 3 | 0.5 | Đạt |
| `ZoneRing.png` | 256×256 | 1 | 4 | 0.0 | Đạt |
| `KillcamGrid.png` | 48×48 | 1 | 0 | ô tile, miễn căn tâm | Đạt |
| `PlayerDot.png` | 32×32 | 1 | 5 | 0.0 | Đạt |
| `PlayerDotOutline.png` | 32×32 | 1 | 4 | 0.0 | Đạt |
| `KillerMark.png` | 32×32 | 1 | 1 | 0.0 | Đạt |
| `CausePlayer.png` | 64×64 | 6 | 4 | 0.0 | Đạt |
| `CauseZone.png` | 64×64 | 5 | 4 | 0.0 | Đạt |
| `CauseLeft.png` | 64×64 | 6 | 4 | 1.0 | Đạt |
| `CauseDisconnect.png` | 64×64 | 6 | 6 | 0.0 | Đạt |
| `CauseUnknown.png` | 64×64 | 4 | 4 | 1.0 | Đạt |
| `IconVolume.png` | 64×64 | 6 | 4 | 0.0 | Đạt |
| `IconMusic.png` | 64×64 | 5 | 4 | 0.0 | Đạt |
| `IconMouse.png` | 64×64 | 7 | 4 | 0.0 | Đạt |
| `IconShake.png` | 64×64 | 6 | 4 | 0.0 | Đạt |
| `IconExit.png` | 64×64 | 7 | 4 | 1.0 | Đạt |
| `IconSpectate.png` | 64×64 | 5 | 4 | 1.0 | Đạt |

KillcamGrid có ngoại lệ chủ ý: đúng 1 px trắng ở cạnh trên/trái, các pixel khác trong suốt; không áp padding/căn tâm của icon. Kiểm từng pixel để tile lặp không tạo đường đôi. ZoneRing kiểm ellipse dày 4 px; ToggleBox kiểm tâm trống trong border 12; dot và outline kiểm hình học riêng, cùng rect/pivot.

8/8 kiểm thử validator đạt: fixture hợp lệ; size/mode sai; alpha bán trong suốt; màu lạ/quá 8 màu; vỡ khối 2×2/mất viền; thiếu padding/lệch tâm/rỗng; mask không trắng; topology lưới/toggle sai. Fixture chỉ trong bộ nhớ.

**Tái lập:** chạy lại generator, 32/32 artifact giống từng byte theo SHA-256: 19 PNG runtime, 11 nguồn native32, contact và mapping. 16/16 ảnh reuse giữ nguyên hash. Manifest kiểm đúng 19 dòng mới, ID/path duy nhất, used_by/status đúng và UTF-8 không BOM/CRLF.

**Retry:** preflight lượt đầu dừng trước khi ghi PNG do CausePlayer cao 27 px vật liệu (giới hạn 26); kiểm bbox cũng phát hiện IconShake rộng 27 px. Sửa endpoint trên lưới gốc; preflight lượt hai đạt, không resample để che lỗi. Validator đạt ngay lượt đầu; lần tái tạo cũng đạt. Kiểm nội dung báo cáo phát hiện PowerShell truyền tiếng Việt qua pipe thành dấu hỏi; đặt OutputEncoding UTF-8 rồi ghi lại, kiểm encoding lần hai đạt. Không có check còn đỏ hoặc vượt 3 lần.

## Tái sử dụng nguyên đường dẫn — không vẽ lại, không sao chép

| File đang có | Phần tử UI-03 | Border LTRB | Tint gợi ý |
| --- | --- | --- | --- |
| `Assets/_JungleMaze/Art/Hud/HudRound10.png` | MenuPanel; Eliminated/Frame; Spectator/Watching; Spectator/TargetCard; LeaveConfirm/Box | 24,24,24,24 | `#202A2D` |
| `Assets/_JungleMaze/Art/Hud/HudRound6.png` | Spectator/List/Row1..9; Spectator/KeyBar | 20,20,20,20 | `#202A2D` |
| `Assets/_JungleMaze/Art/Hud/HudRound6Stroke2.png` | Spectator/List/Row1..9 (selected overlay) | 20,20,20,20 | `#C6AE78` |
| `Assets/_JungleMaze/Art/Hud/HudRound6Dashed.png` | Spectator/List/Row1..9 (optional empty style) | 20,20,20,20 | `#202A2D` |
| `Assets/_JungleMaze/Art/Hud/HudRound6Stroke1.png` | Spectator/List/Row1..9 (outline overlay) | 16,16,16,16 | `#202A2D` |
| `Assets/_JungleMaze/Art/Hud/BarFill.png` | MenuPanel/MasterRow/Master/FillArea/Fill; MenuPanel/SfxRow/Sfx/FillArea/Fill; MenuPanel/MusicRow/Music/FillArea/Fill; MenuPanel/SensitivityRow/Sensitivity/FillArea/Fill; Spectator/TargetCard/Hp/Track/Fill; Killcam/Board/Player0..9/Hp/Fill | 6,6,6,6 | `#202A2D` |
| `Assets/_JungleMaze/Art/Hud/BarTrack.png` | MenuPanel/MasterRow/Master/Background; MenuPanel/SfxRow/Sfx/Background; MenuPanel/MusicRow/Music/Background; MenuPanel/SensitivityRow/Sensitivity/Background; Spectator/TargetCard/Hp/Track; Killcam/Board/Player0..9/Hp | 6,6,6,6 | `#202A2D` |
| `Assets/_JungleMaze/Art/Hud/KeyCap.png` | Spectator/KeyBar/Keys (optional keycap Images; currently text) | 12,12,12,12 | `#202A2D` |
| `Assets/_JungleMaze/Art/Hud/ToastBg.png` | Notice | 20,20,20,20 | `#202A2D` |
| `Assets/_JungleMaze/Art/Hud/ABevel96.png` | MenuPanel; Eliminated/Frame; Spectator/TargetCard; LeaveConfirm/Box | 24,24,24,24 | `#526B69` |
| `Assets/_JungleMaze/Art/Hud/ABevelSlot128.png` | Spectator/List/Row1..9 | 20,20,20,20 | `#526B69` |
| `Assets/_JungleMaze/Art/Hud/AFrameEdge96.png` | MenuPanel; Eliminated/Frame; Spectator/TargetCard; LeaveConfirm/Box | 24,24,24,24 | `#0C1417` |
| `Assets/_JungleMaze/Art/Hud/AFrameEdgeSlot128.png` | Spectator/List/Row1..9 | 20,20,20,20 | `#0C1417` |
| `Assets/_JungleMaze/Art/UI/Menu/ButtonPrimary.png` | MenuPanel/Resume; Eliminated/Frame/Spectate; Spectator/Home; LeaveConfirm/Box/Stay | 32,32,32,32 | `#FFFFFF` |
| `Assets/_JungleMaze/Art/UI/Menu/ButtonSecondary.png` | MenuPanel/Defaults; MenuPanel/Leave; Eliminated/Frame/Home; Spectator/Killer; Killcam/Skip; LeaveConfirm/Box/Confirm | 32,32,32,32 | `#FFFFFF` |
| `Assets/_JungleMaze/Art/UI/Menu/ButtonGhost.png` | Killcam/Skip (optional style) | 32,32,32,32 | `#FFFFFF` |

Base panel tint `#202A2D`, edge `#0C1417`, bevel `#526B69`, selected `#C6AE78`; thứ tự base → edge → bevel → selection. Track tint panel; fill slider gold, fill HP đỏ ngữ nghĩa. Nút menu và icon màu giữ Image tint trắng.

Tham chiếu layout từ builder: MenuPanel 560×388, hàng 48 px; Eliminated/Frame 640×417; Spectator row 160×32, TargetCard 300×90; Notice 500×36. Không tạo lại panel/bar/nút/keycap.

## Giới hạn và bàn giao integration

- Không mở/chạy/điều khiển Unity; PPU, Point, mipmap, compression và border là metadata đề xuất, chưa áp qua `.meta`. Editor dùng chung tự sinh `.meta` sau khi PNG xuất hiện: không sửa, stage hoặc commit những file đó.
- Mapping trỏ tới tên phần tử/parent hiện có trong MatchOverlayPrefabBuild.cs, bỏ prefix root MatchOverlay. Nhiều phần tử hiện là Text/Button, chưa có Image slot cho icon; cần lượt integration thêm Image/overlay và chọn icon theo cause, giữ nguyên bố cục. Pack không triển khai logic, không sửa C#/prefab.
- PlayerDotOutline là mask TRẮNG riêng tint ink, đặt sau PlayerDot cùng rect/pivot. KillerMark đặt trên dot của kẻ hạ bằng state. Ở 18 px, viền gốc 1 px có thể rụng pixel khi thu Point; cần kiểm runtime trước nghiệm thu hình ảnh. Không bake RGB tối vào mask.
- SliderHandle có chu vi bậc thang dày 2 px và grip trong; source 48, builder hiện 20 px, contact có 20/24 px. ToggleBox source 48, border 12, screen 24; Checkmark source 32, screen 16.
- ZoneRing source 256, vòng dày 4 px; rect hiện 480 px và thay đổi theo replay nên độ dày hiển thị cũng scale. Grid cần Image Tiled ở Board, 48 px/ô; Board hiện chưa có Image slot.
- Không sửa HUD/menu PNG cũ, Assets/Imported, C#, prefab, scene, `.asset`, ProjectSettings, cấu hình hoặc file đang làm của agent khác. Không push.

## Commit sản xuất

- `d79874b2` — `art(ui-03): add overlay asset pipeline`
- `4be7cd97` — `art(ui-03): add match overlay sprites`

Tài liệu/manifest và hai dòng nhật ký có commit bàn giao riêng. Hash được liệt kê trong phản hồi cuối; báo cáo không ghi hash tự tham chiếu.
