# ART-04 — Tích hợp icon JSurviv vào HUD và Inventory

| Trường | Giá trị |
| --- | --- |
| Giai đoạn / Deadline | Giai đoạn 2 / 30/10/2026 (ô Deadline = 46325, Excel serial). Ngày viết spec 27/10/2026: còn 3 ngày |
| Module | UI/UX & Art |
| Ưu tiên | P1 |
| PIC | V.Huy |
| Phụ thuộc | Chính thức: CLI-04: Done. Liên quan (không chặn, spec dùng mock): UI-01 (HUD lõi + phần 2): Done G3 25/10/2026 · ITM-01 (nguồn ID/khe/số lượng thật, PIC Quang Huy): Not Yet · WPN-01/Hào (vũ khí đang cầm): Not Yet |
| Trạng thái | Spec (chờ G1) |
| Sheet GDD tham chiếu | Ref GDD của dòng ticket ghi `Assets!6,8; Weapons; Equipments; WIREFRAME!7–8`. Spec dùng: **Weapons** (STT 1–18, cột "Tên cũ", "Code ID"), **Equipments** (STT 1–16), **ITEM LIST** (STT 1–35, đối chiếu tên cũ), **Assets** (STT 4 "Icon vũ khí / trang bị", STT 6 "HUD / Inventory"), **WIREFRAME** (STT 5 Gameplay HUD, STT 6 Inventory / Loot, STT 13 Loot/Hành động/Trạng thái), PROJECT TRACKING (UI-01, ITM-01, ART-03, UI-02). Lưu ý lệch số dòng: xem Q11 |
| Figma node | Bố cục lấy từ HUD v2 (đã duyệt 24/10/2026): `41:2` ([ảnh](../Wireframes/v2/41-2_hud.png)), `41:180` chú thích ([ảnh](../Wireframes/v2/41-180_hud_annotated.png)), component `Slot` `39:139` (Default `39:124`, Selected `39:129`, Empty `39:134`), `KeyCap` `39:115`, gợi ý nhặt `41:172` ("[F] Nhặt Shotgun", 676,414 · 141×34), `PickupToast` `48:234` trong khối `48:431` ([ảnh](../Wireframes/v2/48-239_hud_feedback.png)). Wireframe Inventory v1 `3:3` ([ảnh](../Wireframes/3-3_inventory.png), bảng map: `Docs/UI_Wireframes.vi.md` mục 2) chỉ làm tham khảo ý tưởng "Dưới đất", các khối bị bỏ liệt kê ở mục 2 |
| Bảng kiểm kê | [ART-04_icon_inventory.csv](ART-04_icon_inventory.csv) (34 dòng, UTF-8 có BOM để Excel mở đúng dấu) |

## 1. Mục tiêu

Hiện mọi khe HUD hiển thị ô vuông giữ chỗ vì `Bootstrap` đăng ký `NullItemIconProvider` (UI-01 để lại cổng `IItemIconProvider` cho ticket này). Sau ART-04, người chơi nhìn **icon đúng món** trong 8 khe nhanh (4 vũ khí, 4 dược phẩm), trong dòng thông báo nhặt đồ và trong gợi ý nhặt item dưới sàn; khe đang chọn, số lượng, đạn vẫn đọc rõ như UI-01. Ticket cũng trả lời câu hỏi "sprite JSurviv có đủ cho 18 vũ khí + 16 trang bị không": **kiểm kê thật 34/34 ID** (bảng ở mục 2), chuẩn hóa 34 icon về một kích thước, alpha, pivot thống nhất, và dựng bảng ID → Sprite do asset quyết định (code không biết art). Làm được bằng dữ liệu giả (mock UI-01), không cần ITM-01 hay server loot; khi ITM-01 xong chỉ đổi nguồn ghi, không sửa bảng icon hay view.

## 2. Phạm vi và ranh giới

**Trong phạm vi:**

- Kiểm kê, đối chiếu và ghi trạng thái **Đã có / Cần cắt-chỉnh / Thiếu** cho đủ 18 weapon ID và 16 equipment ID (kết quả ban đầu bên dưới; cập nhật cột trạng thái khi làm xong).
- Chuẩn hóa 34 icon: cắt frame khỏi sheet, cắt padding, độ phân giải, alpha, pivot, kích thước canvas, thiết lập import. Bản chuẩn hóa là **bản sao** đặt tại `Assets/_JungleMaze/Art/UI/Icons/{Weapons,Equipments}/<ID>.png` (đúng `target_path` đã có trong `Docs/Art/asset_manifest.csv`). Không sửa file trong `Assets/Imported/JSurviv/`.
- Icon mặc định cho **HAND** (Tay không) và icon cho **ROPE** nếu không tìm được sprite dùng được (hai ID Thiếu), theo phương án người chọn ở Q3.
- Bảng ID → Sprite dưới dạng asset (ScriptableObject hoặc tương đương, theo mẫu `HudEffectCatalog`) và provider thật của `IItemIconProvider` thay `NullItemIconProvider` trong `Bootstrap`. Cấu trúc lớp/file do tech-lead quyết.
- Gắn icon vào HUD UI-01: 8 khe `HudSlot` (4 vũ khí + 4 dược phẩm), trạng thái Selected/Empty, số lượng `×n`; ô icon của `PickupToast` (UI-01-P2 để placeholder, hiện `PickupFeedView.SetRow` nhận `id` nhưng chưa gán sprite); icon trong gợi ý nhặt item sàn (Q2).
- Demo bằng mock UI-01 (`MockInventoryState`, panel F4, F5): chọn lần lượt cả 34 ID, thấy đúng icon.
- Cập nhật hàng icon trong `Docs/Art/asset_manifest.csv` cho 34 dòng `icon_weapon_*` / `icon_equipment_*` để ghi nguồn thực (JSurviv hay Codex) và trạng thái.

**Ngoài phạm vi (thuộc ticket khác):**

- Quản lý túi đồ, nhặt/đổi/vứt, loot trên sàn, death-drop, danh sách ID/drop table, điều kiện dùng đồ, ghi `IInventoryStateWriter` thật → **ITM-01** (Quang Huy). ART-04 chỉ đọc ID/khe/số lượng.
- Sprite thế giới của vật phẩm nằm trên sàn (đồ vật trong world), rương/chum/bụi/lava/portal → **ART-03 / ENV-02** (Quang Huy). ART-04 chỉ cung cấp bảng icon để ENV-02/ITM-01 dùng lại nếu muốn.
- Quản lý vũ khí cầm tay, bắn, nạp, đạn thật, animation vũ khí trong tay → **WPN-01 / Hào**; prefab vũ khí cũ trong `Imported/JSurviv/Prefabs/Weapon` chỉ để đối chiếu ID, không port.
- Âm thanh nhặt/dùng đồ → **AUD-01**.
- Icon hiệu ứng buff/debuff (7 dòng `icon_effect_*` trong manifest), banner, cung sát thương → **UI-01 phần 2 / Game-10** (`HudEffectCatalog`).
- Menu, Lobby, Result → **UI-02**.
- Sinh ảnh bằng Codex nói chung (HUD frame, menu, 41 icon…): chỉ dùng cho ID Thiếu hoặc icon không qua review, và **chỉ khi người duyệt ở Q1/Q3**. Chưa duyệt thì không sinh.
- Màn hình Inventory toàn màn hình (kéo thả, thay/giữ/vứt) kiểu Figma `3:3`: không có frame v2, không có ticket sở hữu → xem Q7.
- Icon "Đạn" (ITEM LIST STT 11): chưa có ID trong `weapons.json`/`equipments.json` → xem Q9.
- Icon giáp/mũ trong hàng Vitals (Figma v2 chỉ vẽ tên + thanh): không làm, xem Q8.
- Tên tiếng Việt cho món (cần thêm trường vào balance JSON) → ticket riêng chưa có ID (UI-01 Q10).
- Dùng lại khung HUD/nút cũ trong `Sprites/UI/GamePlay/elements` (`inven.png`, `button*.png`, `hp*.png`…): **không dùng**. Khung HUD đã có bản hướng H (`Assets/_JungleMaze/Art/Hud/`) khớp bố cục 16:9 v2; khung cũ vẽ cho bố cục khác (ví dụ `inven.png` 336×388 là cột dọc). Chỉ lấy khi người chỉ định.

**Figma `3:3` (Inventory v1, khung battle royale 100 người) đối chiếu GDD:** giữ ý tưởng cột "Dưới đất" (danh sách món dưới sàn kèm số lượng) cho gợi ý item sàn; **bỏ** ống ngắm/phụ kiện, "Balo lv2 142/250", thẻ "Mũ lv2/Giáp lv3/Balo lv2", M416/Kar98k/P92 và ba khe súng, đạn 5.56/7.62mm, "Chuột phải nhặt nhanh", "kéo/Ctrl kéo vứt" (không có trong GDD: nhặt bằng **F**, 4+4 khe, vứt theo Hero STT 8). Không dựng màn này.

### 2.1. Kết quả kiểm kê (đã làm, đọc file thật trong repo)

Nguồn đọc: thư mục `Assets/Imported/JSurviv/Sprites/{WEAPONS,EQUIPMENTS,UI/GamePlay/elements}`, các `.meta` (PPU, sliced, pivot), kích thước PNG đọc từ header, vùng nội dung (bbox alpha > 8) tính từ pixel thật, và ánh xạ ID cũ → sprite từ prefab `Assets/Imported/JSurviv/Prefabs/{Weapon,Equipment}` (tên prefab trùng ID: `KNIFE.prefab`, `MAGIC.prefab`…). Chi tiết 14 cột mỗi ID: **[ART-04_icon_inventory.csv](ART-04_icon_inventory.csv)**.

**Tổng: 34 ID = 23 Đã có · 9 Cần cắt/chỉnh · 2 Thiếu** (vũ khí 18 = 10 / 6 / 2; trang bị 16 = 13 / 3 / 0).

Định nghĩa trạng thái: **Đã có** = có đúng một sprite đúng món (theo prefab cũ hoặc tên), chỉ cần chuẩn hóa nhập (padding, phóng nguyên, canvas); **Cần cắt/chỉnh** = phải cắt frame khỏi sheet, cắt/xoay/thêm viền để đọc được, hoặc hình khác tên GDD nên người phải xác nhận; **Thiếu** = không có sprite dùng được.

| Nhóm | ID | Sprite đề xuất (rút gọn) | Trạng thái |
| --- | --- | --- | --- |
| Vũ khí | HAND | không có | **Thiếu** |
| Vũ khí | TREE_BRANCH | `WEAPONS/Cành Cây/stick.png` (50×50) | Đã có |
| Vũ khí | KNIFE | `WEAPONS/Dao/knife.png` (80×80, nội dung 62×16) | Cần chỉnh (cắt, xoay chéo) |
| Vũ khí | BONE | `WEAPONS/Xương/bone.png` (160×160) | Đã có |
| Vũ khí | AXE | `WEAPONS/Riuf/axe.png` (38×38) | Đã có |
| Vũ khí | MACE | `WEAPONS/Chùy gaii/mace.png` (46×47) | Đã có |
| Vũ khí | RUBBER_GUN | `WEAPONS/Ná cao su/2-NaCaoSu_normal.png` (64×64) | Đã có |
| Vũ khí | SCEPTER | `WEAPONS/Quyền trượng/quyen_truong.png` (18×18, nội dung 6×16) | Cần chỉnh (rất nhỏ, hẹp) |
| Vũ khí | CROSSBOW | `WEAPONS/Nỏ/no.png` (25×25) | Đã có |
| Vũ khí | SHOTGUN | `WEAPONS/Shotgun/shotgun.png` (128×128, nội dung 58×10, gần đen) | Cần chỉnh (cắt, thêm viền sáng) |
| Vũ khí | BANANA | `WEAPONS/Chuối/Banana.png` (100×100) | Đã có |
| Vũ khí | SMOKE | `WEAPONS/Bom khói/smoke-grenade.png` (100×100) | Đã có |
| Vũ khí | METEORITE_FRAGMENT | `WEAPONS/Mảnh thiên thạch/meteorite_throwing-Sheet.png` (5 frame 63×63) | Cần chỉnh (chọn frame) |
| Vũ khí | CRYSTAL | `WEAPONS/Crystal/crystal.png` (200×200) | Đã có |
| Vũ khí | SEED | `WEAPONS/Hạt/seed.png` (15×15, hình mơ hồ) | Cần chỉnh |
| Vũ khí | ROPE | chỉ có `WEAPONS/Dây thừng/day_thung-Sheet.png` (dây thẳng cao 9 px) | **Thiếu** |
| Vũ khí | BELL | `WEAPONS/Chuông/bell.png` (8 frame 288×288, đề xuất `bell_2`) | Cần chỉnh (chọn frame, thu nhỏ) |
| Vũ khí | SAHUR_BAT | `WEAPONS/Gậy sahur/sahurbat.png` (32×48) | Đã có |
| Trang bị | WOODEN_HELMET | `EQUIPMENTS/Armor/Mũ gỗ/mu_go.png` (25×14, nội dung 12×10) | Đã có (rất nhỏ) |
| Trang bị | SPIKE_HELMET | `EQUIPMENTS/Armor/03. Mũ cối/MuCoi1.png` (có quầng sáng; không có "Mũ gai") | Cần chỉnh |
| Trang bị | MAGIC_HELMET | `EQUIPMENTS/Armor/Mũ phù thủy/Mũ phù thủy.png` (16×16) | Đã có |
| Trang bị | WOODEN_ARMOR | `EQUIPMENTS/Armor/01. Vest/Vest.png` (Vest xanh lá, không phải "gỗ") | Cần chỉnh |
| Trang bị | ARMOR | `EQUIPMENTS/Armor/Giáp gai/Giap_gai.png` (16×16) | Đã có |
| Trang bị | MAGIC_ARMOR | `EQUIPMENTS/Armor/Giáp phù thủy/Giáp phù thủy.png` (16×16) | Đã có |
| Trang bị | GOD_LEAF | `…/Healing items/Lá thần mộc/la_than.png` (25×25) | Đã có |
| Trang bị | SPIRIT_TEAR | `…/Nước mắt tinh linh/Nước mắt linh tinh.png` (16×16) | Đã có |
| Trang bị | MERMAID_TAIL | `…/Vảy đuôi tiên cá/Vảy đuôi tiên cá.png` (16×16) | Đã có |
| Trang bị | PHOENIX_FEATHERS | `…/Lông vũ phượng hoàng lửa/Lông vũ.png` (14×16) | Đã có |
| Trang bị | UNICORN_BLOOD | `…/Máu kỳ lân/Máu kỳ lân.png` (20×20) | Đã có |
| Trang bị | ADRENALINE | `…/05. Nước ngọt/nuoc ngot.png` (64×64, lon có logo giống nhãn hiệu thật) | Cần chỉnh |
| Trang bị | ELIXIR | `…/Thần dược/thanduoc.png` (16×16) | Đã có |
| Trang bị | MAGIC | `…/Gậy thần/gaythan.png` (15×28) | Đã có |
| Trang bị | ELIXIR_OF_LIFE | `…/Tiên dược sự sống/tienduocsusong.png` (16×16) | Đã có |
| Trang bị | COMPASS | `…/La bàn/laban.png` (16×16) | Đã có |

Phát hiện cần biết (không phải việc làm thêm, mà để dev không chọn nhầm):

- **16 thư mục vũ khí đánh số (`01. Dao`, `01. Súng nước`, `02. Súng Lego`, `04…13`, `37…40`) là vũ khí/đạn của JSurviv cũ, không thuộc roster 18.** `01. Dao/knife.png` đứng thẳng 26×64, không phải `Dao/knife.png`. Chỉ dùng thư mục không đánh số cho KNIFE.
- Thư mục healing còn 4 món cũ không thuộc roster (Bim bim, Thuốc sát trùng, Bông băng, Hộp cơm): không dùng, trừ khi Q4 đổi hướng.
- Hầu hết icon là pixel art rất nhỏ (12–25 px nội dung), một số 64–200 px: **độ mịn pixel không đồng nhất** giữa các icon (ví dụ BONE/BANANA/CRYSTAL mịn hơn AXE/COMPASS). Hướng H "Rừng gọn" là vector phẳng, nên đây là rủi ro thẩm mỹ chấp nhận hay không → Q5.
- ADRENALINE: lon "Nước ngọt" có logo tròn đỏ-trắng-xanh nhìn giống một nhãn hiệu có thật. Spec không khẳng định giấy phép; chỉ đánh dấu để người xem có thay hình hay không (Q4).
- Mọi sprite ứng viên đều pivot (0,5; 0,5) và `alphaIsTransparency = 1`, nên "chuẩn hóa pivot" thực chất là **chuẩn hóa vị trí nội dung trong canvas** (nhiều icon chạm mép, sheet có nhiều frame, canvas rộng hơn nội dung).
- Đã đối chiếu tên cũ GDD ↔ thư mục: "Rìu" = `Riuf`, "Chuỳ gai" = `Chùy gaii`, "Gậy đánh bóng" (SAHUR_BAT) = `Gậy sahur`, "Tinh thể" = `Crystal`, "Nước tăng lực" (ADRENALINE) ≈ prefab `Drink` (sprite "Nước ngọt"), "Giáp gỗ"/"Mũ gai" không có thư mục cùng tên.

**Quan hệ với Codex (đề xuất, chờ G1 ở Q1):** `asset_manifest.csv` từng dự kiến Codex sinh 34 icon 256×256. Từ ART-04, **ưu tiên sprite JSurviv**; Codex (hoặc vẽ tay) chỉ lấp ID Thiếu (HAND, ROPE) và ID Cần chỉnh mà sau chỉnh vẫn không đọc được ở 28 px. Số lượng Codex tối đa theo kiểm kê hiện tại: 2 bắt buộc + tối đa 9 tùy chọn.

**Số liệu từ GDD / repo** (ghi "giả định" nếu chưa có nguồn):

| Tham số | Giá trị | Nguồn |
| --- | --- | --- |
| Số weapon ID / equipment ID | 18 / 16 (tổng 34) | `weapons.json` (18), `equipments.json` (16); GDD Weapons STT 1–18, Equipments STT 1–16. GDD và JSON khớp ID |
| Tên ID (`id`) | Trùng cột "Code ID" của GDD cho cả 34 | Weapons/Equipments cột "Code ID" |
| Khe HUD | 4 vũ khí + 4 dược, Slot 64×64, bước 70, nhóm cách 16 | UI-01 mục 2, Figma `39:139` |
| Ô icon trong Slot | 28×28 tại (18,17) trên canvas 1280×720; tên ở y = 46 | UI-01 kế hoạch (ước từ ảnh Figma, chưa phải số Figma) |
| Ô icon trong PickupToast | 18×18, dòng cao 31 | UI-01-P2 mục 3, Figma `48:431` |
| Canvas HUD | 1280×720, `CanvasScaler` Expand; kiểm 1920×1080, 2560×1440 | UI-01 mục 2 (AC2) |
| Quy đổi px hiển thị | 28 px canvas = 28 / 42 / 56 px thật tại 720p / 1080p / 1440p | Tính từ hệ số H/720 |
| Mặc định khe | Khe 1 = HAND, được chọn | UI-01 G-7 (`DefaultsMatchG7`) |
| Phím nhặt | F | UI_Wireframes mục 3, ENV-02 |
| Tên món đang hiển thị | `displayName` tiếng Anh trong JSON | UI-01 Q10 |
| Kích thước canvas icon chuẩn hóa | **64×64 px, RGBA, nền trong suốt** | **Giả định G-1** |
| Cỡ nội dung sau chuẩn hóa | Cạnh dài nhất của vùng nội dung 44–60 px, căn giữa ±2 px | **Giả định G-2** |
| Thiết lập import | Sprite (2D and UI), Single, PPU 64, Bilinear, không mipmap, không nén, `alphaIsTransparency` bật, pivot (0,5; 0,5) | **Giả định G-3** (đồng bộ `HudRound6.png`: filter 1, không mipmap) |
| Ô icon trong Slot sau ART-04 | giữ 28×28 (28–40 theo yêu cầu CPO; Slot 64 và nhãn tên y = 46 không cho tăng quá 28 nếu không đổi bố cục UI-01) | **Giả định G-4** |
| Icon trong gợi ý nhặt | 20×20, đặt bên trái KeyCap, hint vẫn trong vùng giữa sạch 640×360 | **Giả định G-5** |
| Tổng bộ nhớ 34 texture | 34 × 64 × 64 × 4 B ≈ 557 KB | Tính toán (G-1) |
| Ngưỡng nhận diện mù | ≥ 80% icon được gọi đúng tên món bởi mỗi người thử (3 người) | **Giả định G-6** |

## 3. Tiêu chí nghiệm thu

AC1–AC3 của dòng GDD được tách thành các tiêu chí đo được bên dưới (bảng đối chiếu: AC1–AC4; HUD/inventory hiển thị: AC5–AC13, AC16; kết nối ITM-01/CLI-04, đúng tên, không blur/missing reference: AC14, AC15, AC17–AC20).

| # | Tiêu chí (đo được) | Cách kiểm | Ưu tiên |
| --- | --- | --- | --- |
| AC1 | **Bảng đối chiếu đủ 34.** `ART-04_icon_inventory.csv` có đúng 34 dòng: 18 `id` khớp 1-1 với `weapons.json` và 16 `id` khớp 1-1 với `equipments.json` (không thừa, không thiếu, không trùng); mỗi dòng có nguồn sprite (đường dẫn file thật tồn tại) hoặc phương án được duyệt, và đúng một trong ba trạng thái | EditMode test đọc CSV + 2 file JSON qua `IBalanceConfigProvider`; kiểm file tồn tại | P0 |
| AC2 | **Không còn ID Thiếu/chưa xử lý khi đóng ticket.** Khi G3: số dòng trạng thái `Thiếu` = 0 và `Cần cắt/chỉnh` = 0 (đã đổi thành `Đã chuẩn hóa` hoặc `Dùng tạm` kèm ghi chú); hiện trạng ban đầu 23/9/2 (Đã có/Cần chỉnh/Thiếu). HAND và ROPE có icon thật (không phải ô giữ chỗ) | Review CSV cuối; đếm trạng thái | P0 |
| AC3 | **34 file icon chuẩn hóa tồn tại** tại `Assets/_JungleMaze/Art/UI/Icons/Weapons/<ID>.png` (18) và `…/Equipments/<ID>.png` (16); mỗi file đúng 64×64, RGBA, 4 góc ảnh có alpha = 0 (không có nền cứng), cạnh dài nhất của vùng nội dung (alpha > 8) trong 44–60 px, tâm vùng nội dung lệch tâm canvas ≤ 2 px | EditMode test đọc PNG (kích thước, alpha góc, bbox, tâm) | P0 |
| AC4 | **Không quầng/viền nền thừa.** Tỉ lệ pixel bán trong suốt (0 < alpha < 255) trên tổng pixel nhìn thấy ≤ 10% ở mọi icon (loại quầng sáng trắng của `MuCoi1.png` và viền trắng của `Vest.png`); icon có thân quá tối (SHOTGUN, ARMOR) có viền hoặc tô sáng để đọc được trên nền khe `Surface` và `Selected` | EditMode test tỉ lệ bán trong suốt; review ảnh chụp trên 2 nền khe | P1 |
| AC5 | **Import đúng chuẩn.** 34 texture đều: Sprite (2D and UI), Single, PPU 64, filter Bilinear, mipmap tắt, không nén, `alphaIsTransparency` bật, pivot (0,5; 0,5), không Read/Write | EditMode test đọc `TextureImporter` | P0 |
| AC6 | **Bảng ID → Sprite.** Provider thật (`IItemIconProvider`) trả sprite khác null cho đủ 34 ID; sprite trả về đúng bằng sprite ghi trong bảng chuẩn (so tên asset = `<ID>`); 34 sprite **đôi một khác nhau** (không icon nào dùng chung cho hai ID); `GetIcon(null)` và ID không có trong bảng trả `null` (giữ ô giữ chỗ) và không ném exception | EditMode test lặp 34 ID từ JSON | P0 |
| AC7 | **Đổi ID mock → đúng sprite ở khe.** Dùng `MockInventoryState`: đặt lần lượt từng ID vũ khí (18) vào khe vũ khí 1–4 và từng ID dược/hỗ trợ/Thính (10: `GOD_LEAF`, `SPIRIT_TEAR`, `MERMAID_TAIL`, `PHOENIX_FEATHERS`, `UNICORN_BLOOD`, `ADRENALINE`, `ELIXIR`, `MAGIC`, `ELIXIR_OF_LIFE`, `COMPASS`) vào khe dược 5–8; mỗi lần `Image` icon của khe có `sprite` đúng bằng sprite của ID đó và khác sprite của lần ID trước | PlayMode test dựng `Hud.prefab` rồi đọc `QuickbarSlotView` icon; QA quan sát bằng F4 | P0 |
| AC8 | **HAND hiển thị icon mặc định.** Ở trạng thái HUD mặc định (không ghi gì), khe 1 ở trạng thái Selected có icon = sprite của `HAND` (không phải ô giữ chỗ màu `IconPlaceholder`), không báo thiếu sprite | PlayMode (cùng dạng `Hud_DefaultState_BeforeAnyData`) + so sprite | P0 |
| AC9 | **Ô Empty không icon.** Khe `Empty` (ví dụ khe 4 và 7 như Figma `41:180`) không hiển thị sprite của bất kỳ ID nào: `sprite` của icon không thuộc bảng, nhãn tên và `×n` tắt; khe Selected-nhưng-rỗng cũng vậy. Khung/nền Empty của UI-01 giữ nguyên | PlayMode test (đặt khe rỗng sau khi từng có đồ → icon biến mất, không còn sprite cũ) | P0 |
| AC10 | **Khe đang chọn.** Đúng 1 trong 8 khe ở trạng thái Selected (khung hổ phách, nền Selected); đổi `SetActiveSlot(i)` thì khung chuyển đúng khe trong cùng frame, icon từng khe không đổi sprite; icon khe Selected và Default đều hiển thị alpha 1 (chỉ khe không có đồ mới ≤ 0,35) | PlayMode test qua mock, đọc `color.a` và `SlotVisual` | P0 |
| AC11 | **Số lượng và đạn vẫn đọc được.** Với icon đã gán, số phím, `×n`, nhãn tên và chữ đạn "N / M" của dải vũ khí hiển thị giá trị đúng như UI-01 (mock: `RUBBER_GUN`, `GOD_LEAF ×6`, `UNICORN_BLOOD ×1`), và hình chữ nhật icon **không giao** (0 px²) với hình chữ nhật số phím, `×n`, tên trong cùng khe (ở 1280×720 và 1920×1080) | PlayMode test tính giao `RectTransform` bằng `GetWorldCorners` | P0 |
| AC12 | **Kích thước hiển thị.** Ô icon của khe = 28 ± 1 px tại 1280×720 (G-4; nếu Q6 chọn khác thì sửa số, trong khoảng 28–40 px), tức 28 / 42 / 56 ± 1,5 px thật tại 720p / 1080p / 1440p; texture 64 px ≥ 56 px nên **không bao giờ phóng to** (không blur do upscale). Ô icon toast = 18 ± 1 px | PlayMode test đọc rect ở 3 độ phân giải; EditMode test kích thước texture ≥ kích thước hiển thị lớn nhất | P1 |
| AC13 | **Thông báo nhặt đồ có icon.** Gửi sự kiện nhặt cho từng ID trong 34 (qua F5/mock): dòng `PickupToast` hiển thị icon đúng sprite của ID đó (kể cả giáp, mũ, bom); ID không có trong bảng thì giữ ô giữ chỗ; bố cục dòng (cao 31, bước 37, tối đa 3 dòng) không đổi | PlayMode test dùng `PickupToastView.Icon`; chạy lại bộ test UI-01-P2 liên quan toast (AC-FB22…) vẫn xanh | P1 |
| AC14 | **Icon item sàn (nếu Q2 = khuyến nghị).** Khi mock báo có món dưới sàn trong tầm nhặt, gợi ý "[F] Nhặt <tên>" hiện kèm icon 20×20 (G-5) bằng sprite của ID đó, ẩn khi không còn món; hint nằm trọn trong vùng giữa sạch 640×360 ở cả 3 độ phân giải (rect ⊂ x 320–960, y 180–540 trên canvas 1280×720); gợi ý không chặn chuột ngắm (`raycastTarget = false`) | PlayMode test + ảnh chụp 3 độ phân giải; thử với 3 ID (`SHOTGUN`, `GOD_LEAF`, `ARMOR`) | P1 |
| AC15 | **Nối ITM-01/CLI-04 không sửa logic quản lý item.** Thay nguồn ghi mock bằng một writer giả thứ hai (mô phỏng ITM-01) qua `IInventoryStateWriter`/`IHudStateWriter` cho ra cùng sprite ở AC7; diff của ticket **không** sửa Domain/Application logic quản lý item (chỉ thêm, không đổi chữ ký ba port UI-01), không chạm ITM-01. Khi ITM-01 xong, kiểm lại với GameServer thật (đánh dấu "chưa kiểm chứng" đến lúc đó) | PlayMode test hai writer; review diff | P0 |
| AC16 | **Mọi độ phân giải 16:9.** Ở 1280×720, 1920×1080, 2560×1440: 8 khe, toast và hint hiển thị đủ icon, không bị cắt (rect icon nằm trong rect khe/dòng cha), không giao khối HUD khác | PlayMode test rect + ảnh chụp Game view do QA đặt cạnh `41-2_hud.png` | P1 |
| AC17 | **Không missing reference.** Quét `Hud.prefab`, `HudSlot.prefab`, `HudPickupToast.prefab`, prefab gợi ý nhặt (nếu có) và asset bảng icon: 0 script thiếu, 0 sprite null trong 34 mục của bảng, 0 tham chiếu GUID tới `Assets/Imported/JSurviv/` (chỉ trỏ tới bản chuẩn hóa trong `_JungleMaze`); Console 0 Error/Exception khi chạy `HudPreview` và duyệt qua cả 34 ID | EditMode test quét asset (`GameObjectUtility.GetMonoBehavioursWithMissingScriptCount`, kiểm null/GUID); `read_console` | P0 |
| AC18 | **Không động tới nguồn gốc.** `git diff` của ticket không có file nào dưới `Assets/Imported/JSurviv/`, `Backend/`, `Assets/StreamingAssets/JungleMaze/balance/`, và không đưa lại script/logic Socket.IO hay prefab UI cũ vào `_JungleMaze` | Review diff (`git diff --stat`) | P0 |
| AC19 | **Không GC khi cấp icon.** Sau warm-up 120 frame, gọi `GetIcon` + cập nhật 8 khe 600 lần với 34 ID luân phiên: 0 B cấp phát (cùng phương pháp đo UI-01) | PlayMode test `GC.GetAllocatedBytesForCurrentThread` | P1 |
| AC20 | **Icon đọc được, đúng tên, không blur nặng.** (a) Bảng đối chiếu 34 dòng được PIC ký xác nhận từng dòng (đặc biệt 9 dòng Cần chỉnh và 3 dòng tên lệch: SPIKE_HELMET, WOODEN_ARMOR, ADRENALINE); (b) thử nhận diện mù: 3 người xem 34 icon ở đúng kích thước 28 px (1280×720), mỗi người gọi đúng tên ≥ 80% (G-6), liệt kê các ID bị gọi sai và xử lý; (c) ảnh chụp 3 độ phân giải: không icon nào có cạnh nhòe/răng cưa rộng hơn 1 pixel nguồn — **cần người cảm nhận khi playtest** | Review + playtest đo số (b) + xem ảnh chụp (c) | P1 |
| AC21 | **Thay art chỉ sửa asset.** Đổi sprite của một mục trong bảng bằng ảnh khác (bản sao trong bộ nhớ) rồi chạy lại AC6–AC11: kết quả giữ nguyên, không sửa C#/test | PlayMode test như `SwapPlaceholderSprite_TestsStayGreen` của UI-01-P2 | P2 |
| AC22 | **Bộ nhớ nhỏ.** Tổng dung lượng 34 texture sau import ≤ 1 MB (≈ 557 KB theo G-1) | Profiler/AssetDatabase `GetRuntimeMemorySize` | P2 |
| AC23 | **Manifest cập nhật.** 34 dòng `icon_weapon_*`/`icon_equipment_*` trong `Docs/Art/asset_manifest.csv` ghi đúng nguồn (JSurviv/Codex/vẽ tay) và `status` khớp thực tế (không còn `todo` nếu đã có icon) | Review diff manifest | P2 |

### 3.1. Câu hỏi cho người (G1), giả định và rủi ro

Mỗi câu có mặc định khuyến nghị; nếu người không trả lời, CPO dùng mặc định.

| # | Câu hỏi | Mặc định khuyến nghị |
| --- | --- | --- |
| Q1 | Chiến lược nguồn: **JSurviv trước, Codex/vẽ tay chỉ lấp ID Thiếu hoặc ID không đạt AC20**. Duyệt không? Nếu không duyệt Codex thì 2 ID Thiếu làm thế nào? | Duyệt. Codex tối đa 2 bắt buộc (HAND, ROPE) + ID không đạt sau chỉnh |
| Q2 | "Icon item sàn" trong dòng GDD nghĩa là gì? (a) icon trong gợi ý nhặt "[F] Nhặt X" (Figma `41:172`, UI-01 để lại vì chưa có chủ, UI-01 Q5), (b) sprite thế giới của đồ nằm trên sàn (ENV-02/ART-03), (c) cả hai | (a): ART-04 dựng gợi ý nhặt chạy bằng mock và cung cấp bảng icon; (b) để ENV-02/ART-03 tự gọi provider. Cần tech-lead đề xuất port mock cho "món dưới sàn gần nhất" (thêm, không đổi ba port cũ) |
| Q3 | HAND và ROPE (Thiếu) làm bằng cách nào: (a) Codex theo `asset_manifest`, (b) V.Huy vẽ pixel tay 64×64 theo phong cách JSurviv, (c) dùng tạm biểu tượng chung? Hạn chót 30/10 | (a) nếu Q1 duyệt, nếu không thì (b); (c) không đạt AC2 |
| Q4 | Chấp nhận dùng tạm: SPIKE_HELMET ← Mũ cối xanh (không có gai), WOODEN_ARMOR ← Vest xanh lá (không phải gỗ), ADRENALINE ← lon nước ngọt (có logo giống nhãn hiệu thật), WOODEN_HELMET ← mũ nâu 12×10? Hay đổi màu/vẽ lại/thay hình? | Chấp nhận tạm cả 4 (art sẽ AI render sau). Riêng ADRENALINE: bỏ/xóa logo trước khi vào bản chơi thử. Ghi rõ "Dùng tạm" trong CSV |
| Q5 | Độ mịn pixel không đồng nhất (12 px đến 200 px nguồn) và pixel art lệch hướng H "Rừng gọn" (vector phẳng): chấp nhận lẫn phong cách ở bản tạm, hay thống nhất (ví dụ viền tối 1 px, nền khe sáng hơn)? | Chấp nhận lẫn ở bản tạm; thêm viền đồng nhất chỉ cho icon tối (AC4) |
| Q6 | Ô icon trong Slot: giữ 28 px (không đụng bố cục UI-01) hay nâng lên 32–40 px (phải dịch nhãn tên y = 46 và sửa AC22 của UI-01)? Toast 18 px có chấp nhận không? | Giữ 28 px; toast 18 px chấp nhận. Nếu AC20 cho thấy toast không đọc được thì báo người, đổi kích thước toast thuộc UI-01-P2 |
| Q7 | Có cần màn Inventory toàn màn hình (Figma `3:3`) trong ART-04 không? Dòng GDD nhắc "Inventory" nhưng không có frame v2 và không ticket sở hữu | Không. "Inventory" = 8 khe nhanh + gợi ý nhặt; tạo ticket riêng khi chốt luật thay/giữ/vứt |
| Q8 | Icon giáp/mũ trong hàng Vitals (Figma v2 chỉ có tên + thanh) có thêm không? | Không, giữ Figma; icon giáp/mũ chỉ hiện ở toast và gợi ý nhặt |
| Q9 | "Đạn" (ITEM LIST STT 11) cần icon trong toast nhưng chưa có ID: làm hay chờ ITM-01 chốt catalog? | Chờ ITM-01; hiện dùng ô giữ chỗ cho "Đạn" |
| Q10 | Bản chuẩn hóa là **bản sao** trong `_JungleMaze/Art/UI/Icons` (không tham chiếu `Imported/JSurviv`)? Công cụ chuẩn hóa (script/Unity tool) do tech-lead đề xuất | Bản sao; giữ nguyên `Imported` |
| Q11 | Ref GDD của ticket lệch dòng: `Assets!6,8` (STT 8 là Graybox) và `WIREFRAME!7–8` (Settings, Bị loại/Spectate). Nội dung đúng là Assets STT 4 và 6, WIREFRAME STT 5, 6, 13. Người sửa ô Ref GDD trong PROJECT TRACKING? | Có; agent không sửa được xlsx |
| Q12 | Khi xong, nhắc người cập nhật Assets STT 4 ("Chờ roster") và STT 6 ("Chưa chọn nguồn") trong GDD? | Có |

**Giả định (chờ chốt ở G1):** G-1 canvas icon 64×64; G-2 vùng nội dung 44–60 px; G-3 thiết lập import (Bilinear, không mipmap); G-4 ô icon Slot 28 px; G-5 icon gợi ý nhặt 20×20; G-6 ngưỡng nhận diện mù ≥ 80% với 3 người thử. Giá trị G-1 khác `export_px` 256×256 trong manifest Codex: khi Codex sinh icon thì xuất 256×256 rồi hạ về 64×64 để đồng bộ (đổi cột `export_px`/ppu trong manifest ở AC23).

**Rủi ro từ phía spec:**

- Deadline 30/10 còn 3 ngày; 9 icon Cần chỉnh và 2 Thiếu là phần mất thời gian nhất (HAND, ROPE, KNIFE, SHOTGUN, SCEPTER, SEED).
- Icon pixel 12–16 px phóng lớn (x4–x5) trông thô so với HUD H; cần xem sớm ở AC20.
- Hint nhặt item sàn có thể chồng việc với ITM-01/ENV-02 (chưa có port); nếu Q2 đổi thì AC14 bỏ.
- Không bị ITM-01 chặn: chỉ phần kiểm tích hợp thật (AC15, câu cuối) phải chờ ITM-01; mọi AC còn lại làm bằng mock.

## 4. Kế hoạch kỹ thuật

(Phần này do tech-lead điền.)

**Lớp và file:**

| Lớp | File mới/sửa | Ghi chú |
| --- | --- | --- |

**Protocol / config:** (tech-lead)

**Test sẽ viết:**

-

**Chia commit:**

1.

**Rủi ro / câu hỏi mở:** (tech-lead bổ sung; rủi ro từ phía spec ở mục 3.1)

## 5. Nhật ký build

(Dev điền.)

## 6. Review

(code-reviewer điền.)

## 7. QA

| # | Kết quả | Bằng chứng |
| --- | --- | --- |
| AC1 | chưa kiểm chứng | |

## 8. Nghiệm thu

(CPO điền; người duyệt G3, ngày, ghi chú. Nhắc cập nhật Status trong GDD.)
