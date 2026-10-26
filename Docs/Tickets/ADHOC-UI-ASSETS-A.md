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

## 6. Review

Reviewer độc lập ghi sau build.

## 7. QA

| AC | Kết quả | Bằng chứng |
| --- | --- | --- |
| A1–A12 | Chưa kiểm chứng | Sẽ ghi job JSON, screenshot và mapping thật sau integration. |

## 8. Nghiệm thu

Chờ G3 sau review/QA. Quyền bắt đầu build ngày27/10 không thay cho nghiệm thu chất lượng toàn bộasset. Không tự đóng ART-04/UI-02.
