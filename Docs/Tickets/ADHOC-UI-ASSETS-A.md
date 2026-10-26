# ADHOC-UI-ASSETS-A — Sản xuất và gắn bộ UI hướng A

| Trường | Giá trị |
| --- | --- |
| Ngày | 27/10/2026 |
| Module | UI/UX & Art, client Presentation |
| PIC | V.Huy |
| Trạng thái | Build — người dùng đã cho phép sản xuất và tích hợp |
| Căn cứ duyệt | Sau khi xem A đã đổi màu trong Figma, người dùng nói **“Oke bắt đầu sản xuất Assets và gắn Assets vào game”**. Đây là quyền triển khai kế hoạch pack và nối asset; không hỏi lại G1 cho cùng công việc. |
| Nguồn thiết kế | Figma `oeZgisxdiR80hRqbikUwPb`, page `51:2`, hướng A `51:3`, HUD `51:32`, menu `51:427`; [ảnh và read-back](../Art/A_Recolor_Review.vi.md) |
| Phụ thuộc | UI-01 lõi/phần 2: Done. ITM-01/WPN-01 chưa hoàn tất: dùng read port/mock hiện có, không chặn art. |
| Tài liệu liên quan | [Kế hoạch 7 pack](../Art/UI_Asset_Packs.vi.md), [manifest](../Art/asset_manifest.csv), [kiểm kê 34 icon](ART-04_icon_inventory.csv). Không ghi đè spec ART-04 đang được phiên khác chuẩn bị. |

## 1. Mục tiêu

Thay các khung và ô giữ chỗ trong HUD đang chạy bằng bộ A than chì/xanh ngọc/vàng dịu, có đúng icon của 18 vũ khí và 16 trang bị; bổ sung đủ ảnh cho 8 loại hiệu ứng. Bộ menu được sản xuất và dùng để trang trí màn kết nối hiện có, không thêm luồng game mới. Toàn bộ asset nằm trong workspace, được import và tham chiếu thực, không để ảnh concept ngoài Unity được coi là đã gắn game.

## 2. Phạm vi và ranh giới

- **64 dòng UI trong manifest**, chia 7 pack: P01 11 khung HUD, P02 6 ảnh phản hồi, P03 18 weapon, P04 6 mũ/giáp, P05 10 dược/hỗ trợ, P06 7 effect mới (Adrenaline reuse item), P07 6 menu. Đây là 64 dòng logic; một ảnh hiện có có thể phục vụ nhiều dòng nếu có mapping rõ và đã kiểm. Không buộc sinh lại các mask/geometry đúng chỉ để đủ số file.
- Chuẩn hóa item theo kiểm kê JSurviv: ưu tiên bản sao nguồn đúng món; HAND/ROPE sinh mới để lấp thiếu. Không sửa `Assets/Imported/JSurviv/`. Icon gốc có sai nhận dạng như mũ gai/mũ cối và giáp gỗ/vest phải được ghi nguồn/tạm dùng rõ, không tự tuyên bố đúng hình; ưu tiên sửa art nếu thực hiện được trong phạm vi nguồn hiện có. ADRENALINE không giữ logo nhãn hiệu của bản nguồn.
- UI-A: khung khắc/bevel nhẹ, Point filtering và palette đã chọn. Giữ Be Vietnam Pro cho chữ Việt và Pixelify Sans cho số. Số lượng, tính năng và bố cục theo UI-01 hiện có; A là nguồn lớp da, không chép dữ liệu minh họa hoặc ô giữ chỗ của Figma lên game.
- Tích hợp icon trong 8 khe nhanh, pickup feed; gán 8 icon trong `HudEffectCatalog`. Không thêm port pickup mới chỉ để hoàn tất ART-04 trong đợt này. Gợi ý nhặt dưới sàn hiện chưa có nguồn/đích đầy đủ: ghi deferred sang ART-04/ITM-01, không bịa dữ liệu từ map.
- Gắn logo/background/key art/button vào `ConnectionMenuView` IMGUI hiện có, giữ presenter và toàn bộ thao tác Local/LAN/Online, profile, login, vào/rời phòng, lỗi và hủy. Không thay thành UI-02 hoàn chỉnh.
- **Ngoài phạm vi:** full-screen Inventory, kéo/thả/nhặt/vứt thật, nguồn inventory server, protocol, balance JSON, damage/HP, map/Hero/collider/world sprite, WPN-01/MatchSession/MatchDemoView, UI-02/03 mới, âm thanh/animation.
- Giữ GUID và internalIDs của asset cũ. Không gộp `ProjectSettings/TimeManager.asset` hoặc `.codex/config.toml` có sẵn. EditorSettings phải trả `m_EnterPlayModeOptions: 0` sau test.
- Tối đa **3 vòng thử/sửa cho từng lỗi**; ghi lỗi, số vòng, bằng chứng và việc để sau nếu còn fail, tiếp tục phần độc lập. Không push.

## 3. Tiêu chí nghiệm thu

| # | Tiêu chí | Kiểm | Ưu tiên |
| --- | --- | --- | --- |
| A1 | 64 dòng UI đều có đường dẫn ảnh hoặc mapping reuse và trạng thái thật; không gán `done/bound` cho file chỉ mới xuất ngoài Unity. | Manifest + báo cáo pack | P0 |
| A2 | Đủ 34 ID item, không trùng ID, mỗi ID đúng sprite đã chọn; null/rỗng/unknown trả null an toàn; nguồn JSurviv giữ nguyên. | Test catalog từ roster JSON + CSV nguồn | P0 |
| A3 | Icon item runtime 64×64 RGBA, centered/padded, pivot0.5; Point, không mipmap/nén; đọc được ở28px slot và18px toast, không nền caro/viền trắng/cắt cụt. | Import audit + contact sheet + ảnh thật | P0 |
| A4 | 8 khe nhanh hiển thị sprite đúng; Selected/Empty/InUse/count/name/ammo vẫn đúng; missing ID dùng placeholder, đổi lại known ID phục hồi sprite thật. | PlayMode real prefab | P0 |
| A5 | Pickup feed dùng cùng ID→sprite provider; pooled/reused row đổi icon khi ID đổi, unknown không giữ sprite cũ; màu icon thật trắng, không bị tint placeholder. | PlayMode show/reuse/unknown + screenshot | P0 |
| A6 | 8 kind effect có icon (Adrenaline reuse item được kiểm); overflow vẫn hiện +n, fallback label khi missing; không đè icon và label lên nhau. | Catalog audit + PlayMode chip transition | P0 |
| A7 | Khung/palette A gắn lên prefab đang chạy và builder tái tạo không làm mất art; giữ root geometry, margin, raycast=false, Hint112×18 và các giới hạn UI-01. | Test hồi quy +9slice3res | P0 |
| A8 | Menu 6 ảnh được sản xuất, gắn vào màn kết nối hiện có và không che/khóa control; connecting/cancel/error/leave vẫn dùng cùng presenter/actions. | PlayMode/smoke menu + ảnh Boot | P1 |
| A9 | Không đổi gameplay/protocol/map/balance; Bootstrap chỉ wiring, service qua [Inject]/constructor, không singleton hay Find mới. Không lookup/load/alloc mỗi tick cho icon không đổi. | Review diff + focused GC lookup/view | P0 |
| A10 | GameView HudPreview và Game/Boot chụp thật; HUD kiểm1280×720,1920×1080,2560×1440; Console không compile error/missing reference. | Unity QA có bằng chứng | P0 |
| A11 | Chạy EditMode/PlayMode liên quan và suite hồi quy phù hợp một lần sau build ổn định; lưu job/result thật; không giả định số test lịch sử là kết quả mới. | Unity tests JSON | P0 |
| A12 | Commit từng phần đúng đường dẫn, message English ngắn, không attribution, không push; review độc lập rồi QA và trình G3 người dùng. | Git + report | P0 |

Không thay người làm kiểm nhận diện mù 3 người của ART-04 AC20; kiểm agent/contact sheet không được báo là đã đạt AC20(b). Việc làm xong asset/wiring ở đây không tự đóng toàn bộ ART-04.

## 4. Kế hoạch kỹ thuật

### 4.1. Kiến trúc và dữ liệu

Không cần thay Domain/Application/Core/Infrastructure. `IItemIconProvider` đã là port **Presentation** và trả `UnityEngine.Sprite`; giữ đúng chỗ hiện tại, không chuyển Sprite vào lớp thuần. `HudItemIconCatalog` ở Presentation implement port, entries serialize ID/Sprite, build dictionary `StringComparer.Ordinal` một lần khi Configure/OnEnable; `GetIcon` không LINQ, không uppercase/ToString/Resources.Load, không tạo dictionary mỗi frame. Null/unknown trả null. Duplicate/empty/missing sprite phải fail ở editor validator, không âm thầm đánh dấu catalog hợp lệ.

Bootstrap có serialized `HudItemIconCatalog itemIcons` và `UiMenuArtCatalog menuArt`. Đăng ký provider trước inject root; nếu thiếu asset ở scene test/scene cũ thì giữ `NullItemIconProvider` và cảnh báo có kiểm soát, không cấm helper test dùng null provider. Bản chơi đã gắn asset không được rơi fallback. Không load đường dẫn asset bằng UnityEditor trong runtime. Dùng `var menuView = Attach<ConnectionMenuView>(); menuView.ConfigureArt(menuArt);` sau inject; `ConfigureArt` chỉ giữ refs/style, không quyết định flow. Không thêm singleton/ServiceLocator.

`HudFeedbackRoot` hiện Construct3readports; thêm method inject riêng `ConstructIcons(IItemIconProvider icons)` để truyền provider vào feed, **giữ Construct3port tương thích test**, không tạo constructor bắt buộc4param làm hỏng helper. `PickupFeedView.ConfigureIcons` truyền cùng provider vào3row. `PickupToastView.Show` nhận ID (overload cũ nếu cần), cache last ID/provider, gán sprite mới khi đổi ID; placeholder capture một lần như QuickbarSlotView; unknown reset placeholder. Sử dụng `preserveAspect=true`; không thay18×18/28×28.

`EffectChipView` hiện label40×20 trùng vị trí icon20×20: khi có sprite thì bật icon/tắt renderer label; vẫn cập nhật `label.text` để giữ metadata; khi missing hoặc overflow thì tắt icon/bật label. Timer/fill/polarity border giữ nguyên. Test tên effect trên label ẩn dùng nội dung text/catalog; không xóa các kiểm timer/polarity/expiry hiện có.

Menu art qua ScriptableObject, các GUIStyle được tạo/cached lúc có GUI.skin lần đầu hoặc khi đổi art, không tạo Texture2D/style mỗi OnGUI. `GUI.DrawTexture` chỉ vẽ background/keyart/logo khi MainMenu/Connecting và không nhận input. Đặt logo/keyart trong phần trống bên phải bảng kết nối; giữ control panel tại(20,20,380,H−40), không chèn logo làm form tràn. Button styles truyền theo từng GUILayout call, không thay global GUI.skin. InRoom/InMatch chỉ giữ bar cũ có skin phù hợp, không phủ full-screen. 9slice IMGUI dùng GUIStyle.border trên ảnh button; uGUI dùng Sprite.border.

Palette mới lấy từ [A read-back](../Art/directions/A_figma_recolor_readback.json): Surface RGB`202A2D`, bevel/line`526B69`, dark`0C1417`, text`EDF2EE`, muted`A6BBB7`, faint`77908A`, green`63BAA5`, selected fill`28534F`(alpha0.25), selected border`C6AE78`. Giữ ý nghĩa HP đỏ, armor/helmet xanh, Adrenaline vàng, zone tím; có thể giữ giá trị semantic UI-01 đang chạy nếu đổi không cần thiết, phải ghi chính xác giá trị đã áp trong báo cáo. Surface alpha lấy rõ từ palette/prefab, không bake alpha hai lần vào texture mask. **Một grayscale mask nhân Surface không thể tạo bevel sáng hơn Surface**: dùng nền trung tính tintSurface và overlayImage bevel/outline riêng tint`526B69`/selectedgold (sprite `ABEVEL*` do artworker cung cấp), hoặc fullcolorframe không nhân tint. Ưu tiên helper Presentation `HudArtFrameView` nếu cần gom palette3lớp, các overlay có raycast=false/stretchkhớp9slice; không đổiRectkhối. Kiểm màu hiệu dụng trên screen, không chỉ sofilePNG.

### 4.2. Bảng file và trách nhiệm

| Lớp | File mới/sửa | Trách nhiệm |
| --- | --- | --- |
| Asset | `Art/UI/Icons/Weapons/<18ID>.png` | Item canvas64² từ source chuẩn hóa; HAND/ROPE từ source sinh mới. |
| Asset | `Art/UI/Icons/Equipments/<16ID>.png` | Trang bị canvas64², bỏ logo nguồn Adrenaline. |
| Asset | `Art/UI/Icons/Effects/{Poison,Slow,Invisible,Stun,Root,Reverse,CcImmune}.png` | 7 kind effect mới; Adrenaline dùng item. |
| Asset | `Art/Hud/*.png` theo mapping64dòng | Khung A, marker/track/fill/feedback; giữ ảnh arc/arrow đã đúng nếu reuse. |
| Asset | `Art/UI/Menu/{Logo,KeyArt,Background,ButtonPrimary,ButtonSecondary,ButtonGhost}.png` | Sáu asset riêng, không bake cả màn UI hoặc control/text hành vi vào background. |
| Tool ngoài Unity | `Tools/Art/build_a_ui_assets.py` | Sinh mask/khung/effect/menu theo palette, xuất source/output rõ ràng. |
| Tool ngoài Unity | `Tools/Art/normalize_item_icons.py` (tên có thể theo worker) | Cắt đúng frame theo CSV, alpha/padding, export64² và provenance/contact sheet. |
| Presentation mới | `Scripts/Presentation/UI/Hud/HudItemIconCatalog.cs` | ScriptableObject entries+lookup implement IItemIconProvider. |
| Presentation mới | `Scripts/Presentation/UI/UiMenuArtCatalog.cs` | Refs menu textures/sprites và thông số9slice cần cho view. |
| Presentation mới có điều kiện | `Scripts/Presentation/UI/Hud/HudArtFrameView.cs` | Nền + overlay bevel/outline độc lập, tint riêng và không doubletint; không đụng state/layout. |
| Presentation sửa | `Scripts/Presentation/UI/Hud/HudPalette.cs` | Default palette A; không thay balance. |
| Presentation sửa | `Scripts/Presentation/UI/Hud/HudFeedbackRoot.cs` | Method Inject provider→feed, giữ3readport hiện có. |
| Presentation sửa | `Scripts/Presentation/UI/Hud/Feedback/PickupFeedView.cs` | Truyền ID/provider xuống đúng row. |
| Presentation sửa | `Scripts/Presentation/UI/Hud/Feedback/PickupToastView.cs` | Sprite thật/fallback/reset/tint/cache, geometry giữ18px. |
| Presentation sửa | `Scripts/Presentation/UI/Hud/Feedback/EffectChipView.cs` | Tránh icon/label overlap, phục hồi fallback/overflow. |
| Presentation sửa có điều kiện | `Scripts/Presentation/UI/Hud/QuickbarSlotView.cs` | Chỉ sửa preserveAspect/cache nếu cần; đã hỗ trợ provider/placeholder. |
| Presentation sửa | `Scripts/Presentation/UI/ConnectionMenuView.cs` | Cached skin và ảnh menu, toàn bộ presenter/actions giữ. |
| Bootstrap sửa | `Scripts/Bootstrap/Bootstrap.cs` | Serialized catalog refs, register real provider, ConfigureArt menu. |
| Editor mới | `Tools/Editor/UiArt/UiArtAssetBuild.cs` | Configure TextureImporter, validator, catalog/menu refs, apply palette/art tới prefab và scene Bootstrap; idempotent, không recreate scene/world. |
| Editor mới | `Tools/Editor/UiArt/JungleMaze.UiArt.Editor.asmdef` | Editor-only; refs Presentation, Domain nếu đọcEffectKind, Unity UI/TMP khi cần. |
| Editor sửa | `Tools/Editor/HudFeedback/HudFeedbackPrefabBuild.cs` | Tái tạo không mất catalog icon/palette; tránh builder reset entries Icon vềnull. |
| Asset wiring | `Prefabs/UI/HudItemIconCatalog.asset`, `UiMenuArtCatalog.asset`, `HudEffectCatalog.asset` | Đủ34 item/8effect/menu refs. |
| Prefab sửa | `Prefabs/UI/Hud.prefab`, `HudSlot.prefab`, `HudKeyCap.prefab`, `HudEffectChip.prefab`, `HudPickupToast.prefab` | Palette/sprites/import9slice và aspect; không đổi khối root/hint. Arc/arrow prefab chỉ đổi sprite nếu thật sự cần. |
| Scene sửa | `Scenes/Boot.unity`, `Scenes/Game.unity`, `Scenes/Dev/HudPreview.unity` | Chỉ gán catalog vào Bootstrap/scene object hiện có, nếu scene đó chứa Bootstrap; không thêm gameplay object. |
| Test mới | `Tests/EditMode/UiArt/UiArtCatalogTests.cs` | Coverage roster/unknown/duplicates/import/settings/memory. |
| Test mới | `Tests/PlayMode/UiArtIntegrationTests.cs` | Real prefab/provider slot/toast/effect/menu-ref art-swap/reset, allocations scoped. |
| Test sửa | `Tests/PlayMode/HudAcceptanceTests.cs` | Cập nhật duy nhất kỳ vọng palette/sprite art đã đổi, giữ layout/behaviour assertions. |
| Test sửa có điều kiện | `Tests/PlayMode/HudFeedbackAcceptanceTests.cs` | Giữ3porthelper; đổi text assertion cho label ẩn nếu cần, giữ expiry/timer/Hint/geometry. |
| Docs do CPO | `Docs/Art/asset_manifest.csv`, pack plan/pipeline và report | Mapping64, export64item thay256 cũ, actualimport/bound status, source/diff/QA. |

Mọi file mới cần `.meta` do Unity tạo/configure; các `.meta` là ownership của integration dev, không để art worker đoán GUID. Không thêm runtime package hoặc reference asmdef khi assembly hiện có đã đủ.

### 4.3. Chia việc song song và handoff

1. **Worker UI art** (`/root/pinterest_ui_demo`): chỉ PNG HUD/effect7/menu6, source cần thiết và `Tools/Art/build_a_ui_assets.py`. Không `.meta`, C#, prefab, scene, manifest. Giao danh sách output+kích thước+border+mask/fullcolor+reuse. Không viết chung thư mục item34.
2. **Worker item art** (`/root/item_icon_assets`): chỉ34 PNG Weapons/Equipments, script chuẩn hóa/source audit/contact sheet. Không `.meta`, C#, prefab, scene, manifest. HAND/ROPE source từ CPO/ImageGen. Không sửa Imported. Kiểm tất cả34 trước handoff, ghi rõ các ứng viên dùng tạm.
3. **Unity integration dev**: toàn bộ C#/.meta/catalog/prefab/scene/builder/test trong bảng; có thể viết code khi2artworker đang xuất file, nhưng **chờ output ổn định rồi mới import/configure/build catalog và chạy Unity**. Là người duy nhất dùng Unity mutation/test trong giai đoạn build, parent/QA không chạy test song song. Không đổi PNG art worker đang sở hữu; báo lại cần sửa và chờ.
4. CPO cập nhật manifest/docs khi nhận output ổn định, giao reviewer đọc diff, rồi QA độc lập. Nếu source sinh không dùng được sau3vòng, ghi log ID đó; không thay bằng icon chung và gọi đủ34.

### 4.4. Protocol/config

**Không đổi protocol, wire format, balance/config JSON; không cần G2.** Chỉ thêm ScriptableObject Presentation và ref scene/prefab. Nếu xuất64×64 icon thay256manifest cũ, CPO đổi `export_px` cho34dòng cho đúng file thật. Không sửa spec ART-04 hoặc giả định ITM-01 để hợp thức hóa hành vi mới.

### 4.5. Test và QA

| Test/kiểm | AC |
| --- | --- |
| UiArtCatalogTests: đối chiếu IDs canonical roster34, refs khácnull/đúngasset, duplicates/missing/unknown/null, importerPoint/nomipmap/pivot/size64, tổng texture item≤1MB | A1,A2,A3,A9 |
| RealQuickbar_KnownUnknownKnownAndEmpty: đủ4+4, selected/inuse/count/name giữ, icon đúng không tint, icon giữaspect | A4,A7 |
| PickupRow_ReusedIdsAndUnknownReset: đổi knownID→otherID→unknown→known, provider thật, khôngstalesprite | A5 |
| EffectIcons_AndFallbackOverflow:8kind đủsprite; icon/label mutuallyexclusive; missing/overflow→fallback; timer/polarity giữ | A6 |
| Scene/PrefabCatalogBindings: Bootstraprefs trong scene thật, HudEffectCatalog8, menu6; editorbuilder chạy2lần khôngnhânentry/object | A1,A7,A8 |
| SwapCatalogSprite_PreservesSlotAndToastRects: thay sprite bằng clone in-memory rồiassert identity mới/geometry cũ, không sửaC# | A4,A5,A7 |
| ScopedLookupAndViewWarmGC: warmup trước, repeatedlookupknown/unknown + cachedrow/slot pathkhông cấp phát; không claim deferredCanvas/wholeframe0GC | A9 |
| HudAcceptanceTests + HudFeedbackAcceptanceTests + EditMode HUD feedback; fullEditMode/PlayMode khi compile/build ổn định | A7,A9,A11 |
| Ảnh GameView thật1280/1920/2560, không camera-onlyrender bỏOverlayUI; Bootmenuảnh và smokecontrols; contact sheet18/28/32/48px trên trắng/đen/nềnslot | A3,A8,A10 |

Test thêm đủ ý nghĩa trên catalog và chuyển trạng thái, không viết test mirror từngpixel/borderprocedural. QA nhìn rõ nativepixelvsscalePoint; nếu28px silhouette không nhận dạng thì ghi ID fail và sửa asset, không nới ngưỡng bừa. Chưa kiểmmanual3người hoặc ReleasePlayer thì ghi chưakiểm, không tính thaytest agent. Sau test khôi phục Playmode/EditorSettings và các temporaryGameViewsize/capture/runInBackground/timeScale đã đổi.

### 4.6. Chia commit

1. `art(ui): produce direction A HUD and feedback sprites` — PNG/source/script UI; `.meta` tương ứng sau import nếu đã có. Không gộp itemworker.
2. `art(items): normalize weapon and equipment icons` —34PNG/script/sourceaudit +meta sauimport, nguồnHAND/ROPE riêng nếu cần.
3. `feat(ui): add item and menu art catalogs` — Presentationcatalog/provider và testslookup, compile được.
4. `feat(ui): bind item and effect art to HUD` — feed/toast/effect integration, editorbuilder/catalog/prefab wiring, palette và tests liên quan.
5. `feat(ui): skin connection menu with direction A assets` — cachedIMGUIart và wiringBootstrap/scenes, không đổi flow.
6. `test(ui): verify asset bindings and HUD regressions` — meaningfulintegrationtests/result; cập nhật artpalette assertions cùng change tương ứng, không để committrước cốýđỏtest.
7. `docs(ui): record asset production and QA evidence` —CPOmanifest/report/memory/limitations. Parent có thể gộp commit3+4 nếu chia nhỏ khiếncatalog/wiring không compile hoặc testbịđỏ.

Commit English ngắn, explicit path list khi add/commit, không attribution, không push. CPO serialize các thao tác git để không giẫm index chung; giữ mọi thay đổi người khác.

### 4.7. Rủi ro và quyết định thường gặp

- FigmaA giữ nhiều placeholder/hình world minh họa. Không rasterize screenshotHUD làm sprite và không port cây/hero/map chỉ để giống concept. Phần runtime phải dùng state/UIgeometry đã nghiệm thu.
- Mask trắng×palette và fullcolorbevel khác nhau: kiểm kết quảrender để khôngtintdouble/giảmcontrast. Border9slice khôngđược làm icon/frame dày lấn chữ ở64px.
- Chọn providerqua serializedBootstrap tránh Resources.Load mỗiframe; sceneBootstrap tồn tại ởBoot/Game/HudPreview phảiđược kiểmthật, không đoán cả3đều có.
- Current32JSurvivcandidates có pixel density/nhận dạng khôngđều. Không ghi “đã sinh mới34icon”; báo nguồnthật. Memory34×64²RGBA≈544KiB trướcUnityoverhead; không giữ34source1024textures đượcreferenceruntime.
- Menu IMGUI style có thể bị themeGUI nhân tint: restore `GUI.color/backgroundColor` sau vẽ; không tác động overlay network/MatchDemoView hoặc input gameplay. Menubackground chỉhiện ởmànmenu/connecting.
- 8effecticons làm lộ lỗi label/iconoverlap đã có sẵn; sửa đúng lớpview, không đổi models/ports/epoch hay các timing UI-01.

## 5. Nhật ký build

Dev bổ sung output/commit, số lần sửa và giới hạn. Hiện đã có kiểm kênguồn và plan; chưa coi64dòng là sản xuất hoặcbound.

### 5.1. Build 27/10/2026 (Claude, Unity integration, tiếp quản phần Codex)

**Commit** (nhánh `huycv`, chưa push):

| # | Hash | Nội dung |
| --- | --- | --- |
| 1 | `a529b10a` | `art(ui): produce direction A HUD and feedback sprites`: 19 PNG `Art/Hud` (13 mới + 6 ghi đè, giữ GUID), 7 effect, 6 menu, kèm `.meta` |
| 2 | `02804008` | `art(items): normalize weapon and equipment icons`: 34 PNG + `.meta` + `.meta` thư mục |
| 3 | `49c4943b` | `feat(ui): add item and menu art catalogs`: `HudItemIconCatalog`, `UiMenuArtCatalog`, `UiArtSet` + 3 asset |
| 4 | `1760cf68` | `feat(ui): bind item and effect art to HUD`: palette A, `HudArtFrameView`, slot/toast/feed/chip, editor `UiArtAssetBuild`, hook builder cũ, 5 prefab + `HudEffectCatalog`, sửa kỳ vọng palette trong 2 test cũ |
| 5 | `8ee4f49d` | `feat(ui): skin connection menu with direction A assets`: `ConnectionMenuView`, wiring `Bootstrap.cs`, `Boot.unity` (+1 dòng `uiArt`) |
| 6 | `b56dac75` | `test(ui): verify asset bindings and HUD regressions`: `UiArtCatalogTests` (EditMode, 8 test), `UiArtIntegrationTests` (PlayMode, 6 test) |

**Import (A3).** 66 texture (47 trong `Art/UI/**` + 19 sprite `Art/Hud` mới/ghi đè): Sprite, Single, Point, không mipmap, Uncompressed, pivot 0.5, FullRect, PPU 100, border theo `asset_mapping.json`. 34 icon 64×64 border 0; 7 effect 256²; nút menu border 32 (uGUI). `HudArcFilled`/`HudArrowOutline` reuse nguyên, không đổi meta. Cấu hình lặp lại được bằng menu **Jungle Maze/UI Art/Build And Bind Direction A** (`UiArtAssetBuild.ConfigureImporters`).

**Lựa chọn T-1 (Bootstrap tự tạo không có ref serialized).** Thêm ScriptableObject `UiArtSet` (giữ `HudItemIconCatalog` + `UiMenuArtCatalog`) ở `Assets/_JungleMaze/Resources/UiArtSet.asset`. `Boot.unity` gán `Bootstrap.uiArt` trực tiếp. Khi field rỗng (Bootstrap do `BootstrapAutoCreate` tạo ở Game/HudPreview), `Compose()` gọi `Resources.Load<UiArtSet>("UiArtSet")` **một lần** lúc nối service, không mỗi frame, không singleton. Thiếu asset thì cảnh báo một dòng và dùng `NullItemIconProvider`. Đã kiểm thật: Play ở `HudPreview` → provider resolve là `HudItemIconCatalog`, slot/toast/chip có icon. Lệch kế hoạch 4.1: thay hai field `itemIcons`/`menuArt` bằng một field `uiArt` để chỉ có một nguồn cho cả 3 scene.

**Binding đã gắn thật** (validator `UiArtAssetBuild.Validate()` = 0 lỗi; chạy builder 2 lần → md5 prefab/asset/scene không đổi):

- Item: 34/34 ID roster (18 weapon + 16 equipment) → `HudItemIconCatalog`, dictionary `StringComparer.Ordinal` dựng một lần ở `OnEnable/OnValidate/SetEntries`; null/rỗng/unknown/sai hoa thường → null.
- Khe nhanh: sprite thật dùng `Image.Type.Simple` + `preserveAspect`, màu trắng; unknown/empty về placeholder 9-slice + màu `IconPlaceholder`. Ô 28×28 giữ nguyên.
- Pickup feed/toast: `HudFeedbackRoot.ConstructIcons` (method `[Inject]` riêng, `Construct` 3 port giữ nguyên) → `PickupFeedView.ConfigureIcons` → 3 row. Row cache ID, chỉ tra khi ID đổi; unknown về placeholder. Icon 18×18 giữ nguyên.
- Effect: 8/8 kind có icon trong `HudEffectCatalog` (Adrenaline dùng `Equipments/ADRENALINE.png`). Có icon thì bật icon, tắt renderer label (text vẫn set); overflow `+n` hoặc thiếu art thì tắt icon, bật label. Timer/fill/polarity không đổi.
- Khung A: base giữ tint Surface của view, thêm 2 overlay con `ArtEdge` (`AFrameEdge*`, tint `Edge`) và `ArtBevel` (`ABevel*`, tint `Line`) qua `HudArtFrameView`, stretch, `raycastTarget=false`, `LayoutElement.ignoreLayout=true`. Gắn ở Vitals, Minimap, MatchStatus, WeaponStrip (ppum 2) và HudSlot (ẩn khi slot Empty). KeyCap → `KeyCap.png`; toast + overflow → `ToastBg` (ppum 2); chip border → `EffectFrame`; danger banner nền → `DangerBanner` (ppum 2); UseActionStrip ppum 2. Không đổi RectTransform khối nào (test layout UI-01 vẫn xanh).
- Menu: `ConnectionMenuView.ConfigureArt(art, menuScene)`: style GUIStyle tạo một lần (khi có GUI.skin hoặc khi đổi art), không đổi `GUI.skin`. Panel dùng `ButtonGhost`, nút "Vào phòng" `ButtonPrimary`, nút còn lại `ButtonSecondary`/`ButtonGhost`, border IMGUI 10 px (khung ảnh dày 7 px; border 32 của uGUI không vừa nút cao 28–32 px). Background/logo/key art chỉ vẽ ở Repaint, khi MainMenu/Connecting **và** scene đang mở là `menuScene` (Boot). Lệch kế hoạch: thêm điều kiện scene vì ở HudPreview/Game background full-screen che mất HUD/world. Presenter, action Local/LAN/Online/profile/login/vào/rời/hủy không đổi.
- Builder cũ `HudFeedbackPrefabBuild.Build` có event `AfterBuild`; `UiArtAssetBuild` đăng ký `[InitializeOnLoad]` nên chạy lại builder cũ không mất art (đã thử: chạy builder cũ → chip `EffectFrame`, toast `ToastBg`, 4 frame, 8 effect icon, validator 0 lỗi; sau đó trả prefab về bản đã commit).

**Palette A đã áp** (`HudPalette.Default`, prefab Hud): Surface `#202A2D` α 0.92; Text `#EDF2EE`; Muted `#A6BBB7`; Faint `#77908A`; Line (bevel) `#526B69`; Edge (mới) `#0C1417`; Raised `#0C1417`; Track `#0C1417` α 0.80; Grid `#526B69` α 0.60; Amber (chọn/đang dùng) `#C6AE78`; SelectedFill `#28534F` α 0.25; Green `#63BAA5`; IconPlaceholder `#77908A`. Giữ nguyên ngữ nghĩa UI-01: Red `#E5484D`, Blue `#5BB8E8`, Yellow `#F5C542`, Purple `#A274FF`, Trail, DangerFill, HitMarker trắng.

**Chưa gắn (đã sản xuất, có import):** `BarFill`, `BarTrack` (thanh HP/giáp đang dùng anchor fill phẳng; đổi sang sliced có rủi ro hình học, để sau), `MinimapFrame`, `Crosshair` (HUD chưa có crosshair), `HitMarker.png` (hit marker vẫn 4 tick do code dựng). Manifest cần ghi các dòng này là "imported, not bound".

**Test (kết quả thật, Unity 6000.3.15f1, build ổn định cuối cùng):**

- EditMode `JungleMaze.Tests.EditMode`: **274/274 pass** (job `89476790…`), gồm 8 test `UiArtCatalogTests`.
- PlayMode `JungleMaze.Tests.PlayMode`: **43/43 pass** (job `7c892fcc…`), gồm 6 test `UiArtIntegrationTests` và toàn bộ `HudAcceptanceTests`/`HudFeedbackAcceptanceTests`.
- GC: `Lookup_KnownAndUnknown_DoesNotAllocateAfterWarmup` (4000 lookup) và `ScopedLookupAndViewWarmGC` (300 vòng lookup + `SetSlot` + toast `Show`, xen ID known/unknown) đều 0 byte. Không claim 0 GC cả frame/Canvas.
- Console sau build: 0 error, 0 warning của `_JungleMaze`. `ProjectSettings/EditorSettings.asset` đã `git checkout` sau mỗi lượt PlayMode (`m_EnterPlayModeOptions: 0`).
- Ảnh GameView thật (ScreenCapture, có Overlay UI): HudPreview 1280×720, 1920×1080, 2560×1440 (MockupV2 + 3 pickup gồm 1 ID lạ + 6 effect), Boot menu 1280×720 (MainMenu, lỗi "Nhập mã phòng", Connecting → Hủy về MainMenu). Ảnh để ở scratchpad phiên, chưa đưa vào repo. GameView size tạm và các size "QA" đã xóa, trả về 16:10.

**Lỗi gặp và số vòng:**

1. Lỗi biên dịch có sẵn trước phiên (không do ticket): 9 script MOV-02/03 (`LocalPrediction`, `MovementSimulation`, `MovementTelemetry`, `PlayerPoseSource`, `RemotePlayerInterpolator`, `SnapshotBuffer`, `IPlayerPoseSource`, `PlayerPose`, `Bootstrap.MovementChecks`) có file và `.meta` đúng nhưng không nằm trong danh sách biên dịch của Editor → `MatchSession.cs: LocalPrediction could not be found`. Vòng 1 ForceUpdate import, vòng 2 clean build cache: chưa hết. Vòng 3 `AssetDatabase.MoveAsset` ra tên tạm rồi trả lại: hết lỗi. Nội dung file không đổi (git không có diff).
2. Test `SwapCatalogSprite…` đỏ lần đầu vì presenter chỉ đẩy slot khi state đổi; sửa test để phát lại cùng state qua view (1 vòng).
3. Background menu che HUD khi Play ở HudPreview: thêm điều kiện scene (1 vòng).

**Giới hạn / việc cho art hoặc QA:**

- `Menu/KeyArt.png` và `Menu/Background.png` là RGB không có alpha (Unity import RGB24): key art hiện thành ô vuông nền tối trên background. Không sửa PNG; cần art xuất lại KeyArt có nền trong suốt nếu muốn hòa vào nền.
- Viền chip `EffectFrame` dày 3 px và thanh fill 3 px cùng màu nằm sát cạnh dưới, nhìn như cạnh dưới dày hơn. Hình học UI-01 giữ nguyên; QA/art quyết có đổi ppum hay không.
- Hàng nút profile trên menu tràn ngang khi có nhiều profile (có từ trước, không do skin).
- Chưa kiểm nhận diện mù 3 người (ART-04 AC20b), chưa build Release Player, chưa playtest qua GameServer thật (HUD đo bằng mock state).
- Codex sẽ vẽ lại icon 32 px và ghi đè PNG cùng tên từ 28/10: importer giữ Point/no mipmap/uncompressed, test chỉ kiểm ID, tham chiếu, 64×64 và Point, nên ảnh mới không cần chỉnh lại.

### 5.2. Sửa theo review · 27/10/2026 (Claude, Unity Dev)

**Commit** (nhánh `huycv`, chưa push): `054b8a76` `fix(ui): add menu button hover feedback and fit the in-room bar`, `a9c89458` `test(ui): smoke the connection menu art on the Boot scene`. Chỉ sửa `ConnectionMenuView.cs`, thêm test và ảnh. Không đụng PNG, prefab, scene hay presenter.

- **A8 (Nên sửa), smoke test.** `Tests/PlayMode/ConnectionMenuSmokeTests.cs` chạy trên scene Boot thật với Bootstrap thật:
  - Boot gắn menu art đủ 6 ảnh. Backdrop chỉ hiện ở MainMenu và Connecting, ẩn ở InRoom.
  - Logo và key art không đè lên panel (20, 20, 380 px) ở 1280×720, 1920×1080, 2560×1440, và không đè lên rect thật của nút "Vào phòng"/"Hủy" vừa vẽ.
  - `ConfigureArt(null)` trả về skin mặc định, không backdrop, nút vẫn được vẽ. Gắn lại art thì skin A quay lại.
  - Màn hình được chuyển bằng đúng các action mà nút gọi: `Join` không có mã phòng thì báo "Nhập mã phòng."; `Join` → Connecting; `Disconnect` → MainMenu.
  - Có GameServer local thì test đi tiếp vào InRoom và kiểm thanh in-room.
  - Để test được, view có thêm: rect của nút ghi lại lúc repaint (`TryGetButtonRect`), `ShowsBackdrop`, `PrimaryStyle`, `InGameBarHeight`, và hàm tĩnh `BackdropLayout` (tách từ `DrawBackdrop`, hình học giữ nguyên).
- **Giới hạn: chưa bấm chuột thật.** Đã thử cả `EditorGUIUtility.QueueGameViewInputEvent` và `EditorWindow.SendEvent` lên GameView (kể cả khi đã focus): trong test runner không sự kiện nào tới OnGUI của game. Vì vậy test không bấm chuột thật. Cú bấm tay vẫn cần QA hoặc người dùng làm.
- **Gợi ý (a), hover/active.** Các nút trong menu và thanh in-room có phản hồi nhìn thấy được khi có art:
  - Khi chuột ở trên nút: phủ trắng α 0,14 (sáng lên).
  - Khi đang bấm (`hotControl` khác 0): phủ đen α 0,22 (tối xuống).
  - Không tạo texture mới, không đổi flow. Chữ vẫn chuyển trắng ở hover/active như trước.
  - Chưa chụp được trạng thái hover vì không có chuột thật trong phiên.
- **Gợi ý (b), thanh in-game.**
  - Lỗi cũ: thanh cao 34 px mà padding dọc 20 px, chỉ còn 14 px cho nội dung, nên nút "Rời phòng" bị cắt.
  - Sửa: chiều cao thanh = 28 px của nút + padding dọc của panel (48 px khi có art). Label và nút cùng cao 28 px.
  - Đo thật ở InRoom với GameServer local: thanh 48 px, nút "Rời phòng" (428, 20, 90×28) nằm trọn trong thanh (y 10–58).
  - Mép dưới thanh (y 58) sát hộp `MatchDemoView` (y ≈ 58). Đây là view khác, không sửa.
- **Ảnh GameView thật 1280×720** (ScreenCapture, có IMGUI):
  - `Docs/Reports/ADHOC-UI-ASSETS-A/boot_menu_1280.png`: MainMenu có backdrop, logo, key art, panel không bị che.
  - `Docs/Reports/ADHOC-UI-ASSETS-A/inroom_bar_1280.png`: InRoom, thanh mới, nút "Rời phòng" đủ.
  - Size GameView tạm đã xóa, trả về size cũ (index 2). Scene mở lại `HudPreview`, không lưu thay đổi.
- **GameServer local:**
  - Chạy bằng `dotnet run`, `AUTH_MODE=RoomCode`, mã phòng thử `SMOKE`, 127.0.0.1:7777. Đã tắt sau khi chụp.
  - Lần chạy `dotnet` đầu tiên trên máy tự cài chứng chỉ HTTPS dev của ASP.NET Core vào kho người dùng. Chứng chỉ chưa được trust.
- **Test:**
  - EditMode **326/326**.
  - PlayMode **57/57**, gồm `ConnectionMenuSmokeTests`, `UiArtIntegrationTests`, `HudAcceptanceTests`, `HudFeedbackAcceptanceTests`.
  - Smoke đã chạy đạt cả hai nhánh: không server (Connecting → hủy) và có server (InRoom, thanh 48 px).
  - Console sạch. `EditorSettings.asset` đã `git checkout`.

## 6. Review

Reviewer độc lập ghi sau build.

## 7. QA

| AC | Kết quả | Bằng chứng |
| --- | --- | --- |
| A1–A12 | Chưa kiểm chứng | Sẽ ghi job JSON, screenshot và mapping thật sau integration. |

## 8. Nghiệm thu

Chờ G3 sau review/QA. Quyền bắt đầu build ngày27/10 không thay cho nghiệm thu chất lượng toàn bộasset. Không tự đóng ART-04/UI-02.
