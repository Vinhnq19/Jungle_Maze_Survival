# QA ADHOC-UI-ASSETS-A sau khi vẽ lại icon 32 px (27/10/2026)

Bản dựng: HEAD `17ea59ef` (gồm `01e477fa`, `b96db211`, `81de7d82`). Unity 6000.3.15f1. QA không sửa code, PNG, prefab hay scene.

## 1. Import và console

- `refresh_unity` (force, all, compile) rồi đọc console: **0 error, 0 warning**. `git status` sau refresh: không có `.meta` nào đổi.
- Audit importer 68 texture dưới `Art/UI` và `Art/Hud`: 66 texture đúng Sprite, Single, Point, không mipmap, Uncompressed. Hai texture còn lại là `HudArcFilled.png`, `HudArrowOutline.png` (Bilinear): đây là ảnh cũ reuse nguyên, ticket 5.1 ghi rõ không đổi meta.
- 34 icon item: 64×64, tổng bộ nhớ runtime **603 904 B (590 KiB)** ≤ 1 MB.
- `UiArtAssetBuild.Validate()`: 0 vấn đề. 7 prefab HUD: 0 script thiếu.

## 2. Test

| Suite | Job | Kết quả |
| --- | --- | --- |
| EditMode (toàn bộ) | `c59f8158…` | **333/333** pass, 19,8 s |
| PlayMode (toàn bộ) | `b6285589…` | **59/59** pass, 22,4 s |

Gồm `UiArtCatalogTests` (8), `UiArtIntegrationTests` (6), `ConnectionMenuSmokeTests`, `HudAcceptanceTests`, `HudFeedbackAcceptanceTests`. Số này là kết quả chạy mới, không lấy từ nhật ký build. Chi tiết ở `test_results.json`.

## 3. Đo 34 icon từ file PNG (`icon_audit.json`)

34/34 đạt: 64×64 RGBA, 4 góc alpha 0, cạnh dài nhất vùng nội dung (alpha > 8) đều **56 px** (trong 44–60), lệch tâm tối đa **1,0 px**, bán trong suốt **0 %**, ≤ 8 màu mỗi icon, 34 ảnh đôi một khác nhau (không trùng nội dung). 7 icon hiệu ứng 64×64.

## 4. Ảnh Game view thật (ScreenCapture, có Overlay UI)

| Ảnh | Nội dung |
| --- | --- |
| `hud_1280.png`, `hud_1920.png`, `hud_2560.png` | HudPreview: MockupV2, ROPE ở khe 4, 3 pickup (HAND, ROPE, ID lạ), 5 chip hiệu ứng |
| `quickbar_34icons_1280_x3.png` | 5 ảnh 1280×720 ghép lại: đủ 34 icon trong khe 28 px (phóng 3× nearest chỉ để xem) |
| `toast_34icons_1280_x2.png` | 12 ảnh 1280×720: đủ 34 icon ở toast 18 px, 3 dòng mỗi lần, dòng tái dùng đổi icon đúng (phóng 2×) |
| `boot_1280.png`, `boot_1920.png` | Boot, MainMenu: nền, logo, key art |

- **HAND và ROPE đọc được**: ở 28 px (nắm đấm cam, cuộn dây nâu) và ở 18 px trong toast. Đo độ nhòe: ô 28×28 quanh HAND và ROPE có **5 màu** = 4 màu icon + 1 màu nền khe, tức không có nội suy (Point đúng). KNIFE 8, MACE 7 màu, cùng kiểu.
- ID lạ `ZZ_UNKNOWN` ở toast hiện ô giữ chỗ xám, không giữ icon cũ. Khe rỗng hiện khung nét đứt, không icon.
- `KeyArt.png` mới (RGBA, 4 góc alpha 0, 52,4 % pixel trong suốt): trên Boot chỉ thấy vòng tím và đảo, **không còn ô vuông nền tối**.
- `EffectFrame.png`: viền 2 px, cột 0 trống. Trên chip ở 1920/2560 viền mảnh và thanh fill dưới không còn dính thành một cạnh dày.
- Ảnh cũ `boot_menu_1280.png`, `inroom_bar_1280.png` là của lượt dev trước (key art còn ô vuông), giữ để đối chiếu.

## 5. Ánh xạ AC ART-04 (phần build)

| AC ART-04 | Kết quả | Căn cứ |
| --- | --- | --- |
| AC3 34 file, 64×64, alpha góc 0, nội dung 44–60, lệch tâm ≤ 2 | Đạt | mục 3 |
| AC4 bán trong suốt ≤ 10 %, icon tối đọc được | Đạt | 0 %; ảnh SHOTGUN, ARMOR, MAGIC_ARMOR, TREE_BRANCH đọc được trên khe Selected và Default |
| AC5 import | Đạt (Point thay Bilinear theo T-2, G-3) | mục 1 |
| AC6 provider 34, đôi một khác nhau, null/unknown → null | Đạt | `UiArtCatalogTests` + mục 3 |
| AC7 ID → sprite ở khe | Đạt | `RealQuickbar_KnownUnknownKnownAndEmpty` + ảnh quét 34 ID; chưa có test quét 28 ID theo nhóm khe (R2-b của ART-04) |
| AC8 HAND mặc định khe 1 | Một phần | `hud_1920.png` khe 1 = Bare Hands. Chưa có test trạng thái mặc định riêng (R2-b) |
| AC9 Ô Empty không icon | Đạt | test `…AndEmpty` + ảnh khe 7 |
| AC10 khe chọn, alpha icon | Một phần | ảnh: icon alpha đủ ở khe chọn và khe thường; chưa assert `color.a` bằng test |
| AC11 số, tên, đạn đọc được, rect không giao | Một phần | ảnh 3 độ phân giải đọc rõ; chưa có test giao rect |
| AC12 icon 28, toast 18, texture ≥ cỡ hiển thị | Đạt | test assert 28×28 ở 720p (`UiArtIntegrationTests:96`); canvas scale tuyến tính 1,5 và 2 ở 1080p/1440p (`FeedbackLayout_ScalesAt1080And1440…`); texture 64 ≥ 56 |
| AC13 toast có icon đúng cho 34 ID | Đạt | ảnh quét 34 ID |
| AC15 writer thứ hai, không sửa logic | Một phần | diff không đụng Domain/Application/Infrastructure/Core/Backend; test hai writer chưa có (R2-b); GameServer thật chưa kiểm |
| AC16 3 độ phân giải | Một phần | 8 khe và toast đủ icon, không cắt ở 3 độ phân giải; hint nhặt thuộc AC14 |
| AC17 không missing reference, 0 GUID Imported | Một phần | 0 script thiếu, 0 sprite null, catalog 0 phụ thuộc Imported. Nhưng 6 prefab HUD (Hud, HudSlot, HudKeyCap, HudEffectChip, HudPickupToast, HudDangerArrow) phụ thuộc `Assets/Imported/JSurviv/TextMesh Pro/Shaders/TMP_SDF-Mobile.shader` (shader TMP, có từ UI-01, không phải sprite) |
| AC18 diff không chạm Imported, Backend, balance | Đạt | `git diff a529b10a~1..HEAD` 0 file dưới `Assets/Imported/JSurviv/`, `Backend/`, `StreamingAssets/…/balance/` |
| AC19 0 GC | Một phần | `ScopedLookupAndViewWarmGC` 0 B, nhưng lặp theo ID known/unknown, chưa xoay 34 ID qua 8 khe 600 lần |
| AC21 đổi art chỉ sửa asset | Đạt | `SwapCatalogSprite_PreservesSlotAndToastRects` |
| AC22 bộ nhớ ≤ 1 MB | Đạt | 590 KiB |
| AC23 manifest | Không đạt | 34 dòng icon còn `bound (32px restyle queued)`, 7 dòng effect `bound (256px, 64px re-export queued)` dù file đã 64×64, dòng KeyArt `bound (no alpha, re-export queued)` dù đã có alpha; cần CPO cập nhật |
| AC1, AC2 | Chưa đạt (chưa làm) | `ART-04_icon_inventory.csv` còn 23 `Đã có` / 9 `Cần cắt/chỉnh` / 2 `Thiếu`, chưa có cột `nguon_thuc`: việc R1 của ART-04 |
| AC14, AC20 | Không kiểm | hoãn ITM-01 và việc của người dùng |

## 6. Điều quan sát ngoài AC

1. **Panel kết nối IMGUI vẽ đè HUD ở HudPreview.** Panel cố định (20,20,380,H−40) theo pixel màn hình, HUD theo canvas scale. Ở 1280×720 khe 1 (Bare Hands) nằm sau panel nên mờ đi; ở 1920 và 2560 panel phủ một phần Vitals và chip hiệu ứng 1–4. Không do bộ art mới. Ai chụp ảnh nghiệm thu HUD sạch cần ẩn panel hoặc đặt khe có HAND ở khe khác.
2. Tên dài trong toast ngắt giữa từ: "Crossbo/w". Tên khe cắt "Bare H…", "Rubber …", "Unicorn…". Layout UI-01, không đổi ở ticket này.
3. `HudArcFilled`, `HudArrowOutline` còn Bilinear (reuse có chủ đích).
4. Mở scene `Game` trong Editor in 3 cảnh báo "AnimationClip 'RHINO.move' … must be marked as Legacy" (asset thế giới, không thuộc ticket). Play Mode từ scene Game vẫn khởi động ở scene Boot, nên QA **không chụp được HUD thật của Game** (cần vào phòng qua GameServer).
5. Hai dòng manifest `hud_damage_arc`, `hud_danger_arrow` trỏ tới đường dẫn không tồn tại; trạng thái ghi mapping reuse sang file khác, hợp lệ theo A1 nhưng dễ gây hiểu nhầm.

## 7. Chưa kiểm chứng

- Click chuột thật, hover, nhập liệu menu: không có chuột thật trong phiên.
- Game scene, GameServer thật, Release Player, nhận diện mù 3 người (ART-04 AC20b): chưa làm.
- Chip hiệu ứng Invisible, CcImmune, Adrenaline (chip) và `+n`: chỉ có test, chưa có ảnh.
- Mục 6 (Review) của ticket còn trống, QA không kiểm được review độc lập.

## 8. Dọn dẹp

Thoát Play Mode, scene mở lại `HudPreview` (không dirty), size Game view tạm đã xóa và trả về 16:10 (index 2). **`ProjectSettings/EditorSettings.asset` bị Unity đổi `m_EnterPlayModeOptions` 0→1 sau PlayMode; `git checkout` bị từ chối quyền, cần CPO hoàn về 0.**
