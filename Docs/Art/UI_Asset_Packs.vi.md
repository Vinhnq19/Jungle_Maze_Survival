# Kế hoạch sản xuất bộ asset UI

Ngày: **27/10/2026**. Trạng thái: **Đề xuất pack và kiểm kê; dựng demo M theo yêu cầu mới: UI tối màu, 2D mượt, khối rõ, có bóng đổ và ánh sáng. Chưa nghiệm thu bộ production**.

Yêu cầu hiện tại của người dùng: **“Hãy làm UI tối màu, 2D không làm style pixel, nó rõ được các khối, có bóng đổ, có ánh sáng”**. Demo M dùng bề mặt tối tách lớp, khung và ô vật phẩm rõ ranh giới, bóng mềm và highlight có kiểm soát. Chỉ giữ cách gom khối từ [ảnh tham chiếu](references/K_user_style_reference.png); các demo I/J/K/L và yêu cầu pixel trước đó không quyết định hướng UI hiện tại. Đây là thay đổi cách thể hiện UI, chưa thay đồ họa thế giới trong game. Cần xem demo trước khi khóa palette/border và nhân rộng cả pack. Chưa thay prefab, catalog hoặc ảnh đang chạy. UI-01 đã Done, không mở lại ticket.

## 1. Căn cứ và phạm vi

- [Manifest hiện có](asset_manifest.csv): 73 dòng = **64 dòng UI** (17 HUD, 41 icon, 6 menu) + 9 World. Kế hoạch này chỉ gồm 64 dòng UI; Hero, nền map, vật cản, rương, chum và bụi thuộc ART-01/02/03, ngoài bộ UI.
- [Asset Pipeline](Asset_Pipeline.vi.md) cung cấp đường dẫn, kích thước, PPU và border ban đầu. Một số mô tả cũ chưa phản ánh UI-01 phần 2 đã hoàn thành; phải đối chiếu prefab trước khi tạo ảnh trùng.
- GDD sheet Weapons: STT 1–18, cột Code ID/Tên cũ; Equipments: STT 1–16, cột Code ID/Tên cũ. Đã đọc bằng `Tools/read_gdd.py` ngày 27/10. Chỉ lấy nhận dạng vật phẩm, không dùng ảnh để chốt lại balance đang có trạng thái chờ duyệt.
- GDD PROJECT TRACKING, ART-04: đối chiếu đủ 18 weapon + 16 equipment; chuẩn hóa alpha/pivot; nối HUD/inventory bằng catalog/mock, không lấy logic Socket.IO cũ. Sản xuất ảnh chưa tự hoàn tất ART-04.
- [Wireframe](../UI_Wireframes.vi.md): Figma file `oeZgisxdiR80hRqbikUwPb`, style `39:3`, HUD `41:2`, phản hồi `48:239`, menu `42:88`, vào phòng `42:137`, phòng chờ `42:182`, loading `42:269`, reconnect `43:131`, kết quả `43:445`. Đây là tham chiếu bố cục, không phải duyệt art mới.

## 2. Chia 7 pack

Số lượng dưới đây là **số dòng bàn giao trong manifest**, không phải số lần gen. Các mask/khung dùng chung có thể giảm số nguồn ảnh độc lập; không bắt buộc tạo file trùng chỉ để đủ số.

| Pack | Nội dung | Dòng UI | Kích thước xuất | Nơi dùng / thứ tự |
| --- | --- | ---: | --- | --- |
| UI-P01 · Khung HUD | Panel, 3 trạng thái slot, outline, marker bản thân, fill/track thanh, keycap, minimap frame, crosshair | 11 | Theo bảng dưới | Kiểm reuse trước; làm cùng pilot để kiểm độ hợp icon |
| UI-P02 · Phản hồi | Hit marker, damage arc, danger arrow, effect frame, toast, banner | 6 | Theo bảng dưới | Kiểm reuse UI-01; hoàn thiện sau P01 |
| UI-P03 · Vũ khí | 18 Code ID Weapons | 18 | 256×256 RGBA/món | Ưu tiên pilot KNIFE, RUBBER_GUN, SHOTGUN để so silhouette |
| UI-P04 · Mũ và giáp | 3 mũ + 3 giáp | 6 | 256×256 RGBA/món | Sau khi nét vẽ icon được chọn; phân biệt loại bằng hình |
| UI-P05 · Dược phẩm và hỗ trợ | 10 Code ID Equipments còn lại | 10 | 256×256 RGBA/món | Pilot GOD_LEAF, ADRENALINE, COMPASS rồi mở rộng |
| UI-P06 · Trạng thái | 7 icon effect; Adrenaline dự kiến reuse P05 nếu đọc rõ | 7 | 256×256 RGBA/icon | Catalog hiện có 8 effect, cần đủ ảnh cho cả 8 |
| UI-P07 · Menu | Logo, key art, background, 3 nền button | 6 | Theo bảng dưới | Pilot background/key art; chữ và button giữ tách lớp |
| **Tổng** | **UI** | **64** | | **Không bao gồm 9 World** |

### UI-P01: 11 dòng

| ID manifest | Xuất | Border L,T,R,B / xử lý |
| --- | --- | --- |
| `hud_panel` | 96×96 | 24,24,24,24; mask tintable |
| `hud_slot` / `hud_slot_selected` / `hud_slot_empty` | 128×128 mỗi trạng thái | 20,20,20,20; giữ chung hình học |
| `hud_outline` | 96×96 | 16,16,16,16; tâm trong suốt |
| `hud_marker_self` | 64×64 | Pivot giữa, nhìn rõ hướng |
| `hud_bar_fill` / `hud_bar_track` | 64×16 mỗi ảnh | 6,6,6,6; fill trắng để tint |
| `hud_keycap` | 48×48 | 12,12,12,12; không chứa ký tự |
| `hud_minimap_frame` | 400×400 | 32,32,32,32; không chứa ảnh bản đồ |
| `hud_crosshair` | 128×128 | Pivot giữa; không bake màu gameplay |

### UI-P02: 6 dòng

| ID manifest | Xuất ban đầu | Ghi chú reuse trước khi sản xuất |
| --- | --- | --- |
| `hud_hit_marker` | 128×128 | UI-01 đã có view/hình học marker; chưa buộc tạo sprite mới |
| `hud_damage_arc` | 88×232 | Hiện có `Art/Hud/HudArcFilled.png`; đối chiếu hình và geometry thật |
| `hud_danger_arrow` | 120×120 | Hiện có `Art/Hud/HudArrowOutline.png`; giữ hướng/pivot đúng view |
| `hud_effect_frame` | 88×88 | Border18; ưu tiên reuse slot/outline hiện có |
| `hud_toast_bg` | 96×64 | Border20; cân nhắc reuse panel |
| `hud_banner_bg` | 192×80 | Border28; giữ tương phản chữ khi tint cảnh báo |

Kích thước manifest là đích dự kiến, không tự đổi geometry view hiện tại. Khi reuse ảnh có tên/kích thước khác, ghi mapping và số đo mới trước khi cập nhật manifest.

### UI-P03: 18 ID vũ khí

`HAND`, `TREE_BRANCH`, `KNIFE`, `BONE`, `AXE`, `MACE`, `RUBBER_GUN`, `SCEPTER`, `CROSSBOW`, `SHOTGUN`, `BANANA`, `SMOKE`, `METEORITE_FRAGMENT`, `CRYSTAL`, `SEED`, `ROPE`, `BELL`, `SAHUR_BAT`.

Nhận dạng bắt buộc: RUBBER_GUN là **ná cao su**, không phải súng; HAND có icon tay dù không có prefab weapon; SCEPTER khác gậy thần MAGIC. Các cặp cần so cạnh nhau ở32px: KNIFE/AXE/MACE, METEORITE_FRAGMENT/CRYSTAL/SEED, TREE_BRANCH/SAHUR_BAT.

### UI-P04: 6 ID mũ và giáp

`WOODEN_HELMET`, `SPIKE_HELMET`, `MAGIC_HELMET`, `WOODEN_ARMOR`, `ARMOR`, `MAGIC_ARMOR`.

ARMOR là **giáp gai** trong GDD. Tạo khác biệt silhouette/vật liệu, không phân cấp chỉ bằng đổi màu. Không thêm ký hiệu chỉ số hay độ hiếm chưa có nguồn.

### UI-P05: 10 ID dược phẩm và hỗ trợ

`GOD_LEAF`, `SPIRIT_TEAR`, `MERMAID_TAIL`, `PHOENIX_FEATHERS`, `UNICORN_BLOOD`, `ADRENALINE`, `ELIXIR`, `MAGIC`, `ELIXIR_OF_LIFE`, `COMPASS`.

MERMAID_TAIL có tên GDD **Vảy đuôi tiên cá**; ảnh phải bám nhận dạng đó, không mặc định vẽ toàn bộ nàng tiên cá. ELIXIR và ELIXIR_OF_LIFE cần khác hình để tránh nhầm ở32px. MAGIC là gậy thần, COMPASS là la bàn. Không bake số HP, giây hay hiệu ứng vào ảnh item.

### UI-P06: 7 ID trạng thái mới trong manifest

`icon_effect_poison`, `icon_effect_slow`, `icon_effect_invisible`, `icon_effect_stun`, `icon_effect_root`, `icon_effect_reverse`, `icon_effect_ccimmune`.

Catalog `Assets/_JungleMaze/Prefabs/UI/HudEffectCatalog.asset` hiện khai báo 8 kind: Adrenaline, Poison, Slow, Invisible, Stun, Root, Reverse, CcImmune; **cả8 trường Icon hiện rỗng**. Dự kiến dùng `ADRENALINE` của P05 cho kind Adrenaline; nếu thử ở kích thước chip không đạt thì đề xuất icon riêng và tăng manifest sau. Chưa coi reuse này đã kiểm chứng.

### UI-P07: 6 dòng menu

| ID | Xuất | Bàn giao |
| --- | --- | --- |
| `menu_logo` | 1024×256 | Nền trong suốt; chữ LASTAWAY nên dựng riêng để đúng chính tả, chưa gen chữ vào tranh |
| `menu_keyart` | 1040×1040 | Minh họa tách khỏi logo/UI; không bake button/chữ |
| `menu_background` | 1920×1080 | Nền phẳng hoặc minh họa theo art direction được chọn; vùng đặt chữ ít chi tiết |
| `menu_button_primary` / `menu_button_secondary` / `menu_button_ghost` | 192×96 mỗi loại | Border32; mặt trống, trạng thái interaction dùng tint nếu đủ |

Chưa bổ sung pack settings/navigation/đồng tiền/skin/rank: chưa có danh mục và bố cục đã chốt cho bộ mới. Khi xác định thiếu icon UI chức năng, ghi phụ lục có ID + nơi dùng trước khi gen.

## 3. Hiện có và còn thiếu

- Manifest đang đánh dấu4 dòng UI `done`: panel và3 slot; chúng là kết quả UI-01 cũ, không phải art mới được chấp thuận. Hai dòng P0 còn lại đã done là Hero/Ground, ngoài bộ UI.
- Còn60 dòng UI trong manifest là `todo`, nhưng **todo không luôn có nghĩa thiếu hình**: outline/marker đã có nguồn demo; arc/arrow đã có asset UI-01 dưới tên khác. Cần review/reuse, không gen lại toàn bộ theo status cũ.
- Hiện chưa có file sản phẩm tại các thư mục `Art/UI/Icons/Weapons`, `Equipments`, `Effects`; 34 icon item +7 icon effect cần sản xuất/đối chiếu nguồn. Kiểm tài nguyên `Assets/Imported/JSurviv/` chỉ đọc trước khi quyết định gen mới; không sửa thư mục gốc.
- `directions/H_icons.json` có6 hình minh họa vector cho demo: Tay không, Nỏ, Ná cao su, Lá thần, Máu kỳ lân, Tăng lực. Đây là nguồn tham khảo cũ, chưa phải catalog đủ34 ID và không phải lựa chọn style mới.
- Demo M dùng Be Vietnam Pro cho chữ và số để nhất quán UI2D mượt; không dùng Pixelify Sans trong demo này. Khi triển khai production, thay gán font chỉ ở lớp trình bày theo kế hoạch riêng; giữ chữ TMP tách khỏi ảnh, không sửa font đang chạy trong đợt concept.

## 4. Quy trình sản xuất theo đợt

1. **Kiểm kê và chọn hướng:** pack ở trên là đề xuất tổ chức. Chuẩn bị preview so sánh ngắn với cùng6 icon pilot + một vùng HUD/menu; ghi rõ từng hướng là đề xuất. Người chọn art direction trước khi nhân rộng64 dòng. Không diễn giải việc duyệt UI-01 cũ thành duyệt bộ mới.
2. **Pilot:** KNIFE, RUBBER_GUN, SHOTGUN, GOD_LEAF, ADRENALINE, COMPASS và1 background/menu. Có thể gen ảnh tham khảo khi được yêu cầu; chưa gọi là sprite sẵn gắn chỉ vì có sheet đẹp.
3. **Khóa style recipe:** từ pilot được chọn, lưu ảnh chuẩn, palette, độ dày nét, hướng ánh sáng, tỷ lệ lấp đầy ô. Những thông số này hiện **chưa chốt**.
4. **Sản xuất P03/P05 → P04/P06:** từng món là một output độc lập; mỗi lần gen lấy ảnh chuẩn đã chọn làm reference. Sheet chỉ để duyệt tổng thể hoặc làm nguồn cần tách và kiểm từng ô; không bind nguyên sheet vào một slot.
5. **P01/P02:** ưu tiên chỉnh nguồn vector/mask có sẵn cho border, marker, thanh và hình học. Không gen bitmap ngẫu nhiên cho khung9-slice nếu dựng vector bảo đảm đồng đều hơn. Giữ màu ngữ nghĩa trên view/palette.
6. **P07:** gen minh họa, tách chữ/logo/button; kiểm vùng đặt nội dung ở1280×720 trước khi xuất fullHD.
7. **Chuẩn hóa và kiểm:** kiểm từng sprite + contact sheet32/48px trên nền sáng/tối và slot thật. Chỉ ảnh đạt mới chuyển sang import/candidate.
8. **Nối Unity theo đợt riêng:** catalog weapon/equipment thuộc ART-04, effect catalog và prefab do người sở hữu UI quản lý. Sau khi art được chọn, lập scope binding và test liên quan; không thay logic map/inventory/balance.

Mỗi lỗi tối đa3 vòng thử/sửa theo yêu cầu người dùng; chưa đạt thì ghi ID, lỗi, ảnh bằng chứng và hướng xử lý vào retry log, chuyển việc độc lập. Không nhân rộng cả pack khi pilot còn lỗi.

## 5. Chuẩn xuất và import

- Icon: PNG RGBA256×256 theo manifest, nguồn2D mượt có cạnh sạch, shading nhẹ và silhouette rõ ở32/48px; không dùng lưới pixel hay nearest-neighbor cho hướng M. Một món ở giữa, pivot(0.5,0.5), tỉ lệ1:1, có vùng trong suốt quanh mép; silhouette chiếm khoảng72–80% khung là **đề xuất pilot**. Không nền caro giả, chữ, watermark hoặc bàn tay cầm thêm. Bóng trong icon nằm trong padding; bóng panel/glow trạng thái là lớp riêng để không bị cắt khi9slice.
- Ảnh gen nguồn giữ nguyên chất lượng trong thư mục source; bản export/candidate tách riêng. Không phóng file nhỏ để gọi là source độ phân giải cao.
- Downsample chất lượng cao; xem alpha trên trắng/đen và màu slot để bắt viền sáng. Không dùng tẩy nền tự động làm mất chi tiết trong suốt của pha lê/chai.
- UI import dự kiến: Sprite2D/UI, Single, FullRect, PPU100, CompressionNone, MaxSize≥cạnh dài; **FilterBilinear, tắt mipmap** cho art UI2D mượt. Border theo từng dòng, không áp chung cho icon; kiểm bóng/glow không bị crop hoặc kéo méo. Chưa đổi import hiện tại khi chưa triển khai; không thay bộ world pixel hay setting của nó.
- Nếu thay file đang dùng: giữ GUID/.meta, kiểm SpriteMode và internalIDs trước vì một số asset cũ có thể là Multiple; không áp Single mù quáng. File mới: tạo.meta bằng Unity sau khi chấp thuận import.
- Không bake màu cảnh báo/selected vào mask trắng nếu view đang tint. Ảnh item màu có thể giữ màu bản thân; không dùng màu duy nhất làm dấu phân biệt.

## 6. Prompt dùng lại

Ràng buộc hiện tại: **UI2D tối màu, mượt, khối rõ, bóng đổ và ánh sáng có kiểm soát**, đang kiểm bằng demo M; chưa khóa palette production. Panel nền than/xanh đen, lớp nổi sáng hơn lớp đáy, cạnh sáng mảnh ở hướng sáng chung, bóng mềm dưới khối và nhấn trạng thái chọn. Icon nhiều màu nhưng không phát sáng toàn bộ; chữ tương phản cao. Không pixel, không render3D bóng nhựa, không glow tràn gây mất ranh giới các khối. Mẫu dưới cố định cấu trúc bàn giao:

```text
Use case: stylized-concept
Asset type: single Unity inventory icon
Subject: {Code ID + tên GDD + nhận dạng bắt buộc}
Style: {recipe từ pilot được người chọn}
Reference: {ảnh style chuẩn đã chọn; chỉ tham khảo style}
Composition: one centered object, readable silhouette at 32 and 48 pixels,
consistent scale and lighting with approved reference, transparent background.
Constraints: no text, no watermark, no UI frame, no additional objects,
no baked checkerboard, no shadow outside the object bounds.
Output: individual source image; export a 256x256 RGBA sprite separately.
```

Background dùng prompt riêng: bố cục theo vùng keyart/background của Figma, không gen cả màn UI có chữ/nút; palette và mật độ chi tiết lấy từ hướng được chọn. Nếu đưa H làm một lựa chọn, ghi rõ **tham chiếu UI-01 cũ**, không ghi đã duyệt cho đợt mới.

## 7. Trạng thái và tiêu chí nhận pack

Các trạng thái cần phân biệt: `planned` → `concept` → `source-generated` → `exported` → `visual-QA` → `imported` → `bound` → `accepted`. Đây là vocabulary đề xuất cho sổ sản xuất riêng; chưa đổi schema/status CSV đang dùng. `review` trong manifest chỉ dành candidate thực sự đã có, `done` cần đủ tiêu chí sử dụng tương ứng.

| Kiểm | Điều kiện đạt / bằng chứng |
| --- | --- |
| Đủ danh mục |34/34 item map đúng Code ID GDD;8/8 effect có icon hoặc reuse đã kiểm; không sai tên/nhầm loại |
| Đúng file |Ảnh export đúng kích thước, RGBA nếu cần alpha, không trùng ID; lưu source, prompt, reference và output riêng |
| Đọc nhỏ |Contact sheet32/48px; từng cặp dễ nhầm được đặt cạnh nhau và reviewer phân biệt bằng silhouette/chi tiết lớn |
| Alpha |Xem trên trắng/đen/nền slot, không nền caro bake, không viền trắng; không cắt cụt mép |
| Khung |9-slice ở đúng size dùng trong HUD; không giãn góc, không ảnh trang trí đè chữ/giây/số lượng |
| Bố cục |Ảnh menu/HUD preview1280×720 giữ vùng chữ và vùng giữa sạch; chấp nhận style do người quyết |
| Binding |Chỉ sau đợt tích hợp: đổi mock ID cho đúng sprite;0 missing reference; số lượng/selected/overlay vẫn đọc được |
| Hồi quy |Chỉ sau khi gắn prefab/catalog: chạy test liên quan và kiểm GameView; chưa gọi ảnh ngoài Unity là QA trong game |

## 8. Nhật ký hiện tại

27/10: đã đối chiếu64 dòng UI trong manifest,18 weapon ID,16 equipment ID và8 kind trong effect catalog. **Chưa gen ảnh trong kế hoạch này, chưa import/bind, chưa chọn art direction mới.** Bước tiếp theo là trình pack + lựa chọn hình cụ thể để người chọn, rồi mới nhân rộng.
