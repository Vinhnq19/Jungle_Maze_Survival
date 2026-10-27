# QA ART-04: icon 34 vật phẩm (27/10/2026)

Bản dựng: HEAD `f61cf4e8` (gồm R1 `29cf9797`, `48165122`, test R2 `e8b57415`, `8e047049`, `0a5a4e28`). Unity 6000.3.15f1. QA không sửa code, test hay asset.

## 1. Chạy mới

| Suite | Job | Kết quả |
| --- | --- | --- |
| EditMode (toàn bộ) | `2e4d69ce…` | **337/337** pass, 11,9 s |
| PlayMode (toàn bộ) | `49dc1a95…` | **65/65** pass, 23,4 s |

Console sau test và sau playtest HudPreview: **0 error, 0 warning**. Chi tiết: `test_results.json`.

Từ `17ea59ef` (bản ADHOC đã QA) đến `f61cf4e8`, `git diff` ngoài `Docs/` chỉ thêm 5 file test (`Art04IconInventoryTests`, `Art04IconSweepTests` và `.meta`). Không PNG, prefab, scene hay code production nào đổi, nên **ảnh HUD và số đo PNG của ADHOC được dùng lại** (đã ghi rõ ở từng dòng), không chụp lại.

## 2. Đo trực tiếp thêm trong lượt này

- **Importer 34 icon** (`execute_code`): 34/34 đạt Sprite, Single, Point, không mipmap, Uncompressed, `alphaIsTransparency` bật, không Read/Write, pivot (0,5; 0,5). PPU = 100 (không phải 64: chấp nhận ở T-2).
- **CSV**: 34 dòng, 34/34 `Đã chuẩn hóa`, 0 `Thiếu`, 0 `Cần cắt/chỉnh`.
- **Manifest**: 34 dòng icon đều `bound (32px native, 64x64 nearest x2)`, `export_px` 64x64, `ppu` 100. Manifest không có cột nguồn.
- **Kích thước hiển thị đo từ ảnh Game view thật** (ROPE, nội dung nhìn thấy; expected = 56/64 × 28 × hệ số canvas): 1280×720 đo **24 px** (24,5), 1920×1080 đo **36 px** (36,8), 2560×1440 đo **48 px** (49). Ô icon 28 / 42 / 56 px đúng.
- **Hình học prefab**: `HudSlot/Icon` 28×28 tại (18, −17) trong ô 64×64; `HudPickupToast/Icon` 18×18 tại (24, −6,5) trong dòng 141×31; không Image nào bật `raycastTarget`.
- **Icon tối trên 2 nền khe** (`dark_icons_selected_vs_default_1280_x3.png`, Game view thật 1280×720): SHOTGUN và WOODEN_HELMET trên khe Selected; ARMOR, SHOTGUN, MAGIC_ARMOR, TREE_BRANCH, SMOKE, SPIKE_HELMET, BONE trên khe Default. Tất cả đọc rõ, có viền sáng.

## 3. Bảng AC1–AC23

Ảnh ADHOC: `Docs/Reports/ADHOC-UI-ASSETS-A/` (`hud_1280/1920/2560.png`, `quickbar_34icons_1280_x3.png`, `toast_34icons_1280_x2.png`).

| AC | Kết quả | Bằng chứng |
| --- | --- | --- |
| AC1 | Đạt | `Csv_34Rows_MatchRosterOneToOne_WithValidStatusAndRealSource` và `Csv_TargetPath_IsTheSpriteTheCatalogServes` xanh; đếm riêng 34 dòng, 34 `nguon_thuc` tồn tại |
| AC2 | Đạt | 0 `Thiếu`, 0 `Cần cắt/chỉnh` (34/34 `Đã chuẩn hóa`); HAND và ROPE có icon thật (ảnh `quickbar_34icons_1280_x3.png`, dùng lại ADHOC) |
| AC3 | Đạt | `Png_64x64_TransparentCorners_Content44To60_Centered_FewSemiTransparent` xanh. Đo độc lập (`icon_audit.json`, dùng lại ADHOC vì PNG không đổi): 34/34 64×64 RGBA, góc alpha 0, cạnh dài 56 px, lệch tâm ≤ 1,0 px |
| AC4 | Đạt | Bán trong suốt 0 % (test và `icon_audit.json`); icon tối đọc được trên Selected và Default (mục 2) |
| AC5 | Đạt (giá trị đã sửa) | Mục 2: 34/34 đúng. AC gốc ghi Bilinear, PPU 64; đã đổi thành Point theo G-3/T-2 (PPU 100 chấp nhận) |
| AC6 | Đạt | `UiArtCatalogTests.ItemCatalog_Covers34RosterIds_EachWithItsOwnSprite`, `…NullEmptyUnknownAndWrongCase_ReturnNull`; `icon_audit.json`: 34 ảnh đôi một khác nội dung |
| AC7 | Đạt | `Sweep_18WeaponsThroughSlots1To4_10ConsumablesThroughSlots5To8` xanh (112 lần ghi, sprite đúng và khác lần trước) |
| AC8 | Đạt | `DefaultSlot1_IsHand_Selected_FullAlpha_AndSelectionMovesWithoutSwappingIcons` xanh; `hud_1920.png` khe 1 = Bare Hands (dùng lại ADHOC) |
| AC9 | Đạt | `UiArtIntegrationTests.RealQuickbar_KnownUnknownKnownAndEmpty` xanh; ảnh khe 7 rỗng nét đứt, không icon (dùng lại ADHOC) |
| AC10 | Đạt | Cùng test AC8: chọn lần lượt 8 khe, khung đi theo, sprite giữ nguyên, alpha icon 1 |
| AC11 | Đạt (theo định nghĩa CPO) | `VisibleIcon_ClearOfKeyCountAndName_At1280x720And1920x1080` xanh: nội dung nhìn thấy giao số phím, `×n`, tên = 0 px² ở 720p và 1080p. Theo AC gốc (rect `RectTransform`) thì lấn 2 px² (720p) và 4,5 px² (1080p) với số phím, 12 và 27 px² với `×n`; CPO chọn đo phần nhìn thấy, rect lấn là follow-up UI-01. Đã đối chiếu ảnh: không thấy chạm |
| AC12 | Đạt | Mục 2: prefab 28×28 và 18×18; nội dung đo trên màn hình 24 / 36 / 48 px ở 720p / 1080p / 1440p khớp 28 / 42 / 56 px; texture 64 px ≥ 56 px nên không phóng to. `FeedbackLayout_ScalesAt1080And1440_WithSafeMargins` xanh |
| AC13 | Đạt | `PickupToast_ShowsTheRightIconForAll34Ids` xanh (đúng sprite, không tô màu, 1 dòng mỗi lần); `toast_34icons_1280_x2.png` (dùng lại ADHOC); dòng toast cao 31 giữ nguyên |
| AC14 | Không kiểm | Hoãn sang ITM-01 (quyết định T-4) |
| AC15 | Một phần | `SecondWriter_StandInForItm01_GivesTheSameSprites` xanh; `git diff a529b10a~1..HEAD` không có file Domain/Application/Infrastructure/Core nào ngoài Audio (AUD-01). Kiểm với GameServer thật: **chưa kiểm chứng** (R5, chờ ITM-01) |
| AC16 | Đạt | 8 khe và toast đủ icon ở 1280, 1920, 2560, không cắt (`hud_*.png`, dùng lại ADHOC); icon nằm trọn trong khe và dòng cha (mục 2). Phần hint nhặt thuộc AC14. Ghi chú: panel kết nối IMGUI của HudPreview vẽ đè một phần HUD, không do art |
| AC17 | Đạt (theo quyết định CPO) | `HudPrefabsAndCatalog_DependOnNoImageUnderImported` xanh: 0 ảnh/sprite từ `Imported`. `UiArtAssetBuild.Validate()` 0 vấn đề; 0 script thiếu ở 7 prefab (ADHOC). Shader TMP JSurviv bị loại trừ có chủ đích, follow-up. Console 0 error khi chạy HudPreview và duyệt 34 ID |
| AC18 | Đạt | `git diff a529b10a~1..HEAD`: 0 file dưới `Assets/Imported/`, `Backend/`, `StreamingAssets/…/balance/`, và không file Domain/Application/Infrastructure/Core ngoài Audio |
| AC19 | Đạt | `Rotate34IdsThrough8Slots600Times_AfterWarmup_NoGc` xanh: warm-up 120 frame, 600 vòng, 0 B; thêm `ScopedLookupAndViewWarmGC` 0 B |
| AC20 | Không kiểm | Việc của người dùng (ký CSV, nhận diện mù 3 người, cảm nhận độ nhòe). Ảnh Point không nội suy (5 màu trong ô 28×28, ADHOC) chỉ là dữ liệu tham khảo, không thay AC20(b) |
| AC21 | Đạt | `SwapCatalogSprite_PreservesSlotAndToastRects` xanh (identity mới, hình học cũ) |
| AC22 | Đạt | `ItemIcons_Import64PointNoMipUncompressed_UnderOneMegabyte` xanh; đo 603 904 B (590 KiB) ≤ 1 MB (ADHOC) |
| AC23 | Một phần | Trạng thái và kích thước khớp thực tế (34/34 `bound (32px native, 64x64 nearest x2)`, 64x64, không còn `todo`). Manifest không có cột nguồn; nguồn thật nằm ở cột `nguon_thuc` của CSV và `Docs/Art/Production_A/Items/Provenance.csv` |

Tổng: **Đạt 19** (AC1–AC13, AC16–AC19, AC21, AC22; trong đó AC5, AC11, AC17 theo giá trị hoặc định nghĩa đã được duyệt), **Một phần 2** (AC15, AC23), **Không kiểm 2** (AC14, AC20), **Không đạt 0**.

## 4. Quan sát và rủi ro

1. AC11 và AC17 đạt theo quyết định của CPO, không theo chữ AC gốc. Follow-up: rect icon lấn 1 px lên nhãn (UI-01), shader TMP trong `Imported/JSurviv`.
2. Panel kết nối IMGUI ở HudPreview che khe 1 (1280) và một phần Vitals/chip (1920, 2560). Ai chụp ảnh G3 hoặc làm AC20(b) ở HudPreview nên ẩn panel hoặc đặt món cần xem ở khe 2–8.
3. Tên dài trong toast ngắt giữa từ ("Crossbo/w"); tên khe bị cắt ("Bare H…", "Rubber …"). Layout UI-01.
4. Ghi chú `ghi_chu` trong CSV giữ nguyên mô tả hiện trạng cũ (ví dụ "Không có sprite…") dù trạng thái đã `Đã chuẩn hóa`; không sai nhưng dễ gây hiểu nhầm khi PIC ký ở AC20(a).

## 5. Chưa kiểm chứng

AC14 (hoãn), AC20 (người dùng), R5 kiểm lại AC7/AC13/AC15 với GameServer thật (chờ ITM-01), Release Player.

## 6. Dọn dẹp

Thoát Play Mode, scene mở lại `HudPreview` (không dirty), size Game view tạm đã xóa và trả về 16:10.
