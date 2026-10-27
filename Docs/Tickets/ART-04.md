# ART-04 — Tích hợp icon JSurviv vào HUD và Inventory

| Trường | Giá trị |
| --- | --- |
| Giai đoạn / Deadline | Giai đoạn 2 / 30/10/2026 (ô Deadline = 46325, Excel serial). Ngày viết spec 27/10/2026: còn 3 ngày |
| Module | UI/UX & Art |
| Ưu tiên | P1 |
| PIC | V.Huy |
| Phụ thuộc | Chính thức: CLI-04: Done. Liên quan (không chặn, spec dùng mock): UI-01 (HUD lõi + phần 2): Done G3 25/10/2026 · ITM-01 (nguồn ID/khe/số lượng thật, PIC Quang Huy): Not Yet · WPN-01/Hào (vũ khí đang cầm): Not Yet |
| Trạng thái | Đã duyệt G1 (27/10, CPO thay người dùng); build nằm trong ADHOC-UI-ASSETS-A |
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

**Câu hỏi kỹ thuật (tech-lead, 27/10/2026, sau khi đối chiếu với ADHOC-UI-ASSETS-A; chi tiết ở mục 4):**

| # | Câu hỏi | Mặc định khuyến nghị |
| --- | --- | --- |
| T-1 | Catalog icon gắn qua field serialized của Bootstrap, nhưng chỉ `Boot.unity` có object Bootstrap. Bấm Play ở `HudPreview`/`Game` thì `BootstrapAutoCreate` tạo Bootstrap rỗng, field serialized null, provider rơi về Null và icon không hiện. Ai sửa? | Báo chủ ADHOC sửa trong phạm vi ADHOC (ví dụ: tạo Bootstrap từ prefab có sẵn catalog, hoặc đặt catalog vào chính object Bootstrap của scene dev). ART-04 không sửa Bootstrap cho việc này. R4 chờ sửa xong |
| T-2 | AC5 ghi PPU 64; ADHOC không nêu PPU | Chấp nhận PPU ADHOC đặt (không ảnh hưởng `Image` UI có `preserveAspect`). Sửa AC5 thành "Point, không mipmap, không nén, pivot 0,5" theo ADHOC |
| T-3 | AC1 đòi "một trong ba trạng thái", AC2 đòi đổi sang `Đã chuẩn hóa`/`Dùng tạm` khi đóng: mâu thuẫn | Test R2-a chấp nhận 5 giá trị; lúc G3 chỉ được còn `Đã chuẩn hóa` / `Dùng tạm` |
| T-4 | Gợi ý nhặt dưới sàn (AC14): ADHOC hoãn. ART-04 làm sau ADHOC (port mock mới, sửa `Hud.prefab`, khoảng 4–6 giờ) hay chuyển sang ITM-01? | Làm nếu ADHOC commit xong prefab/Bootstrap trước trưa 29/10. Nếu không: hoãn AC14 sang ITM-01, ART-04 đóng với AC14 "Hoãn (người duyệt)" |
| T-5 | Script chuẩn hóa dùng Pillow (Python, ngoài stdlib) thay vì stdlib/Editor script | Chấp nhận: chỉ là công cụ ngoài Unity, không vào build. Ghi phiên bản Pillow trong báo cáo ADHOC để chạy lại được |
| T-6 | Test ART-04 viết ở đâu để không đụng test ADHOC? | File riêng `Tests/EditMode/Art04/` và `Tests/PlayMode/Art04IconSweepTests.cs`, chỉ chứa assert ADHOC chưa có |

## 4. Kế hoạch kỹ thuật — theo ADHOC-UI-ASSETS-A

**Bối cảnh (27/10/2026).** Người dùng đã duyệt ticket [ADHOC-UI-ASSETS-A](ADHOC-UI-ASSETS-A.md) (gọi tắt **ADHOC**) và Codex đang build ticket đó song song. ADHOC đã bao phần lớn việc build của ART-04: 34 icon `Assets/_JungleMaze/Art/UI/Icons/{Weapons,Equipments}/<ID>.png` (hiện chưa commit, chưa có `.meta`), script `Tools/Art/normalize_item_icons.py`, `HudItemIconCatalog` (ScriptableObject ở Presentation, implement `IItemIconProvider`), đăng ký provider trong `Bootstrap`, gắn icon vào 8 khe nhanh, pickup toast và effect chip (mục 4 của ADHOC). Vì vậy ART-04 **không** lập kế hoạch build, code hay asset trùng. Mục này chỉ gồm: (1) AC nào ADHOC đã đáp ứng, (2) phần ART-04 còn phải làm, (3) quyết định ADHOC đã chốt thay cho Q/G của ART-04, (4) rủi ro phối hợp. ART-04 chỉ bắt đầu phần việc của mình sau khi Codex commit xong các commit liên quan của ADHOC (xem 4.2). Theo chính ADHOC (cuối mục 3): ADHOC xong không tự đóng ART-04.

### 4.1. Đối chiếu AC ART-04 → ADHOC (A1–A12)

Ý nghĩa trạng thái: **do ADHOC đáp ứng** = ART-04 chỉ đọc lại bằng chứng của ADHOC ở QA. **ADHOC đáp ứng một phần** = phần còn lại ghi ở cột cuối và thuộc ART-04. **Còn thiếu — thuộc ART-04** = ADHOC không làm.

| AC ART-04 | Tương ứng ADHOC | Trạng thái | Phần ART-04 còn làm |
| --- | --- | --- | --- |
| AC1 Bảng đối chiếu đủ 34 | A2 (test catalog từ roster JSON + CSV nguồn) | ADHOC đáp ứng một phần | ADHOC kiểm 34 ID của catalog khớp JSON. ART-04 còn: kiểm chính file CSV (34 dòng, cột `trang_thai` hợp lệ, nguồn thực tồn tại), test R2-a |
| AC2 Không còn Thiếu/Cần chỉnh; HAND, ROPE có icon thật | A1, A2 (HAND/ROPE sinh mới) | ADHOC đáp ứng một phần | Icon có rồi. Còn cập nhật cột trạng thái và nguồn thực trong CSV (R1) |
| AC3 34 file 64×64, alpha góc 0, nội dung 44–60, lệch tâm ≤ 2 px | A3 | ADHOC đáp ứng một phần | Script ADHOC tự assert các số này lúc xuất. Chưa có test trong Unity đo bbox/tâm: R2-a |
| AC4 Bán trong suốt ≤ 10%; icon tối có viền | A3 ("không nền caro/viền trắng") | ADHOC đáp ứng một phần | Thêm test tỉ lệ bán trong suốt (R2-a). Chụp icon trên nền `Selected` (ADHOC mới có contact sheet trên nền Surface) |
| AC5 Thiết lập import | A3 | ADHOC đáp ứng, giá trị đổi | Filter là **Point** thay Bilinear (xem 4.3, G-3). ADHOC không ghi PPU 64: T-2 |
| AC6 Provider: 34 khác null, đúng asset, đôi một khác nhau, null/unknown → null | A2 (+ test duplicates/unknown của ADHOC) | Do ADHOC đáp ứng | Không |
| AC7 Đổi ID mock → đúng sprite ở khe (18 vũ khí vào khe 1–4, 10 dược vào khe 5–8) | A4 (`RealQuickbar_KnownUnknownKnownAndEmpty`) | ADHOC đáp ứng một phần | ADHOC chỉ thử known/unknown/known. Còn test quét đủ 28 ID theo nhóm khe (R2-b) và QA bằng F4 |
| AC8 HAND là icon mặc định ở khe 1 | A4 (gián tiếp) | ADHOC đáp ứng một phần | Assert riêng trạng thái mặc định (R2-b) |
| AC9 Ô Empty không icon | A4 ("…AndEmpty") | Do ADHOC đáp ứng | Không |
| AC10 Khe đang chọn, alpha icon | A4 | Do ADHOC đáp ứng | Test quét R2-b assert thêm `color.a` (không tốn thêm việc) |
| AC11 Số lượng/đạn đọc được, rect icon không giao số phím/`×n`/tên | A4, A7 (giữ hình học UI-01) | ADHOC đáp ứng một phần | Assert giao rect = 0 px² ở 1280×720 và 1920×1080 (R2-b) |
| AC12 Ô icon 28 ± 1 px, toast 18 ± 1 px, texture ≥ cỡ hiển thị | A3, A7, A10 | Do ADHOC đáp ứng | ADHOC giữ 28×28/18×18 và chụp 3 độ phân giải. Nếu test UI-01 hiện có chưa đo rect icon thì R2-b đo thêm |
| AC13 Toast có icon đúng cho cả 34 ID | A5 (`PickupRow_ReusedIdsAndUnknownReset`) | ADHOC đáp ứng một phần | Quét đủ 34 ID qua toast (R2-b) |
| AC14 Icon trong gợi ý nhặt "[F] Nhặt X" | **Không có.** ADHOC ghi rõ "gợi ý nhặt dưới sàn … deferred sang ART-04/ITM-01", không thêm port pickup | **Còn thiếu — thuộc ART-04** | R3 (có điều kiện, T-4) |
| AC15 Writer giả thứ hai cho cùng sprite; không sửa logic item | A9 (không đổi gameplay/port) | ADHOC đáp ứng một phần | Test hai writer (R2-b). Kiểm với ITM-01 thật khi ITM-01 xong (R5) |
| AC16 3 độ phân giải: icon đủ, không cắt | A10 | ADHOC đáp ứng một phần | Khe và toast đã có ảnh ADHOC. Gợi ý nhặt thuộc R3 |
| AC17 Không missing reference; 0 GUID trỏ `Assets/Imported/JSurviv/` | A10 (console, missing ref), A2 (giữ nguồn) | ADHOC đáp ứng một phần | Quét dependency của `Hud.prefab`, `HudSlot.prefab`, `HudPickupToast.prefab`, catalog: không phụ thuộc nào nằm dưới `Assets/Imported/` (R2-a) |
| AC18 Diff không chạm Imported/Backend/balance | Phạm vi ADHOC (mục 2), A9 | Do ADHOC đáp ứng | Review `git diff --stat` cả hai ticket ở G3 |
| AC19 0 GC khi cấp icon | A9 (`ScopedLookupAndViewWarmGC`) | ADHOC đáp ứng một phần | AC19 đòi xoay vòng 34 ID qua 8 khe 600 lần. Nếu test ADHOC chỉ lặp một ID thì R2-b thêm vòng xoay |
| AC20 (a) PIC ký CSV, (b) thử nhận diện mù 3 người, (c) ảnh không nhòe | A3, A10 (chỉ phần c). ADHOC ghi rõ không thay AC20(b) | **Còn thiếu — thuộc ART-04** ((a), (b)); (c) dùng ảnh ADHOC | R4 |
| AC21 Đổi art chỉ sửa asset | `SwapCatalogSprite_PreservesSlotAndToastRects` | Do ADHOC đáp ứng | Không |
| AC22 Bộ nhớ ≤ 1 MB | Test "tổng texture item ≤ 1 MB" trong `UiArtCatalogTests` | Do ADHOC đáp ứng | Không |
| AC23 Manifest cập nhật 34 dòng | A1 (CPO cập nhật manifest, `export_px` 64) | Do ADHOC đáp ứng | Đối chiếu manifest với CSV ở R1 |

Tổng: **9 do ADHOC đáp ứng · 12 ADHOC đáp ứng một phần · 2 còn thiếu (AC14, AC20)**.

### 4.2. Phần việc ART-04 còn lại (chỉ những gì ADHOC không làm)

Agent: chỉ **unity-dev** (không cần backend-dev). Không làm song song với ADHOC trên file chung. Thứ tự và điều kiện bắt đầu:

| # | Việc | Bắt đầu khi | File (ART-04 sở hữu) | AC |
| --- | --- | --- | --- | --- |
| R1 | Cập nhật CSV: `trang_thai` → `Đã chuẩn hóa` / `Dùng tạm` (+ ghi chú); **thêm** cột cuối `nguon_thuc` (đường dẫn nguồn thật lấy từ provenance ADHOC: JSurviv / vẽ lại / ImageGen). **Không đổi tên hay xóa cột cũ**, vì `normalize_item_icons.py` đọc `id`, `loai`, `ten_cu_gdd`, `sprite_de_xuat`, `dich_chuan_hoa`. Đối chiếu với manifest | Codex commit xong 34 icon (commit 2 của ADHOC 4.6) | `Docs/Tickets/ART-04_icon_inventory.csv` | AC1, AC2, AC23 |
| R2-a | Test EditMode **chỉ cho các assert ADHOC thiếu**: CSV 34 dòng khớp JSON, trạng thái thuộc tập hợp lệ (T-3), `nguon_thuc` tồn tại; mỗi PNG đọc bằng `File.ReadAllBytes` + `Texture2D.LoadImage` (không cần Read/Write import): bbox alpha > 8 dài 44–60, lệch tâm ≤ 2, 4 góc alpha 0, tỉ lệ bán trong suốt ≤ 10%; `AssetDatabase.GetDependencies` của 3 prefab + catalog không có đường dẫn nào dưới `Assets/Imported/` | ADHOC commit catalog + test (commit 3–4, 6) | `Assets/_JungleMaze/Tests/EditMode/Art04/Art04IconInventoryTests.cs` (thư mục riêng, không viết vào `Tests/EditMode/UiArt/` của ADHOC) | AC1, AC3, AC4, AC17 |
| R2-b | Test PlayMode quét: dựng `Hud.prefab` như `HudAcceptanceTests` nhưng truyền **provider thật từ asset catalog**. Gồm: mặc định khe 1 = sprite HAND, Selected, alpha 1 (AC8, AC10); 18 vũ khí qua khe 1–4 và 10 dược qua khe 5–8, mỗi lần sprite = `GetIcon(id)` và ≠ lần trước (AC7); giao rect icon với số phím/`×n`/tên = 0 ở 1280×720 và 1920×1080 (AC11); 34 ID qua pickup toast (AC13); writer giả thứ hai cho cùng sprite (AC15); xoay vòng 34 ID × 600 lần sau warm-up 120 → 0 B nếu ADHOC chưa đo (AC19). Trước khi viết, đọc test của ADHOC đã commit và bỏ mọi assert trùng | Như R2-a, sau khi ADHOC QA xong (Unity chỉ một phiên chạy test) | `Assets/_JungleMaze/Tests/PlayMode/Art04IconSweepTests.cs` | AC7, AC8, AC10, AC11, AC13, AC15, AC19 |
| R3 | Gợi ý nhặt dưới sàn có icon (**có điều kiện, T-4**). Thiết kế tối thiểu, chỉ thêm, không đổi chữ ký ba port UI-01: port đọc `IGroundItemState` (`HasItem`, `ItemId`, `Count`, event `Changed`) + `IGroundItemStateWriter` + store trong `JungleMaze.App.Hud.State`; mock nối vào panel F4 hiện có; view `GroundPickupHintView` (Presentation) đặt trong `Hud.prefab` theo Figma `41:172` (676,414 · 141×34), gồm KeyCap "F" + icon 20×20 (G-5) + tên `displayName`, `raycastTarget = false`; Bootstrap thêm 2 dòng đăng ký store. Trước khi làm, grep ITM-01/ENV-02 xem đã có port "món gần nhất" chưa; có thì dùng lại, không tạo port thứ hai | ADHOC commit xong mọi thay đổi `Hud.prefab` và `Bootstrap.cs` (commit 4–5 của ADHOC 4.6) **và** G1 chọn làm ở T-4 | App: port + store + mock; Presentation: view; `Hud.prefab`; `Bootstrap.cs` (2 dòng); test EditMode store + PlayMode rect ⊂ vùng giữa 640×360 ở 3 độ phân giải, thử `SHOTGUN`/`GOD_LEAF`/`ARMOR` | AC14, AC16 (phần hint) |
| R4 | PIC ký từng dòng CSV (đặc biệt 9 dòng từng "Cần chỉnh" và các dòng vẽ lại). Thử nhận diện mù: 3 người xem 34 icon trong HUD ở 28 px (1280×720), ghi tỉ lệ đúng từng người (≥ 80%, G-6) và danh sách ID bị gọi sai. ID sai **báo lại chủ asset ADHOC sửa**, ART-04 không sửa PNG. (c) dùng ảnh 3 độ phân giải của ADHOC A10 | Sau R2-b (HUD đã có icon thật trong HudPreview, xem rủi ro T-1) | Kết quả ghi vào mục 7 và một file báo cáo QA trong `Docs/Reports/ART-04_<ngày>/` | AC20 |
| R5 | Kiểm lại AC7/AC13/AC15 với GameServer thật | ITM-01 Done (không chặn G3 ART-04, ghi "chưa kiểm chứng") | Không thêm file | AC15 câu cuối |
| R6 | Nhắc người: sửa ô Ref GDD (Q11); cập nhật Assets STT 4 và STT 6 (Q12); "Đạn" vẫn dùng ô giữ chỗ đến khi ITM-01 chốt ID (Q9) | Khi G3 | Không có file trong repo | — |

**Protocol / config:** không đổi protocol, wire format hay balance JSON, **không cần G2**. R3 chỉ thêm port/store phía client (Application + Presentation), không đụng `Backend/`.

**Chia commit ART-04** (tiếng Anh, `git commit -m "…" -- <đường dẫn>`, không attribution, không push, không `git add -A`):

1. `docs(art-04): align plan with the in-progress UI asset build` — `Docs/Tickets/ART-04.md` (commit này).
2. `docs(art-04): record final icon sources and statuses` — `Docs/Tickets/ART-04_icon_inventory.csv` (R1).
3. `test(art-04): check icon inventory and normalized files` — `Tests/EditMode/Art04/*` (R2-a).
4. `test(art-04): sweep all item IDs through HUD slots and toasts` — `Tests/PlayMode/Art04IconSweepTests.cs` (R2-b).
5. Chỉ khi T-4 = làm: `feat(hud): add ground item state port and mock` (App + test EditMode) → `feat(hud): show ground pickup hint with item icon` (view + `Hud.prefab` + 2 dòng Bootstrap) → `test(hud): cover ground pickup hint rects` (PlayMode).
6. `docs(art-04): add QA and blind recognition results` — ticket + `Docs/Reports/ART-04_<ngày>/`.

**Ước lượng trước deadline 30/10:** R1 0,5 giờ; R2-a 1–2 giờ; R2-b 2–3 giờ; R3 4–6 giờ; R4 khoảng 1 giờ cho 3 người. Nếu ADHOC commit xong trong 28/10 thì R1, R2 và R4 kịp trước 30/10. **R3 chỉ kịp nếu bắt đầu muộn nhất trưa 29/10**; quá mốc này dùng nhánh "hoãn" của T-4.

### 4.3. Q/G của spec ART-04 mà ADHOC đã quyết thay

Tất cả ghi **"đã quyết theo ADHOC, chờ người xác nhận ở G1"** của ART-04. Người đã duyệt ADHOC, nên G1 ở đây chỉ xác nhận ART-04 chấp nhận các quyết định đó, không mở lại việc build.

| Q/G | Spec ART-04 (mặc định) | ADHOC đã quyết | Ảnh hưởng tới ART-04 |
| --- | --- | --- | --- |
| G-3 | Bilinear, không mipmap, không nén, PPU 64 | **Point**, không mipmap, không nén, pivot 0,5. Không ghi PPU | Sửa giá trị AC5 thành Point. PPU: T-2. Rủi ro hiển thị ở 4.4 |
| G-1 | Canvas 64×64 RGBA | 64×64 RGBA (manifest đổi `export_px` từ 256 xuống 64) | Khớp |
| G-2 | Nội dung 44–60 px | Script đặt đích 56 px (58 px với icon có viền) | Khớp |
| Q1 | JSurviv trước, Codex lấp chỗ thiếu | JSurviv trước, sinh mới HAND/ROPE | Khớp |
| Q3 | (a) Codex cho HAND, ROPE | HAND, ROPE sinh bằng ImageGen (source + prompt lưu riêng) | Khớp (a). AC2 đạt về mặt có icon |
| Q4 | Tạm chấp nhận Mũ cối, Vest, mũ gỗ; ADRENALINE xóa logo | Theo provenance hiện có (đọc trước khi có yêu cầu đổi hướng, có thể đổi khi Codex commit): **vẽ lại pixel 32 px** cho SPIKE_HELMET (mũ có gai), WOODEN_ARMOR (giáp gỗ nâu), ADRENALINE (lon không logo), cả **SEED**; WOODEN_HELMET giữ JSurviv | Tốt hơn mặc định. CSV ghi các dòng này `Đã chuẩn hóa (vẽ lại)` thay `Dùng tạm`. Người xác nhận hình ở R4(a) |
| Q5 | Chấp nhận lẫn phong cách; viền chỉ cho icon tối | Viền sáng 1 px cho SHOTGUN, ARMOR, MAGIC_ARMOR, TREE_BRANCH; alpha nhị phân hóa | Khớp. AC4 bán trong suốt thực tế ≈ 0% |
| Q6 | Giữ 28 px khe, 18 px toast | Giữ 28×28 và 18×18, `preserveAspect` | Khớp |
| Q10 | Bản sao trong `_JungleMaze`; công cụ do tech-lead đề xuất (ưu tiên script tái chạy được) | Bản sao; công cụ `Tools/Art/normalize_item_icons.py` dùng **Pillow** (không phải stdlib) | Chấp nhận. Pillow là thư viện của script công cụ, không vào build Unity: T-5 |
| Q2 | (a) gợi ý nhặt, ART-04 làm bằng mock | Hoãn, không thêm port | ART-04 tự làm (R3) hoặc hoãn sang ITM-01 (T-4) |
| Q7, Q8 | Không làm Inventory toàn màn, không icon ở Vitals | Ngoài phạm vi | Khớp |
| Q9, Q11, Q12, G-5, G-6 | — | ADHOC không quyết | Vẫn thuộc ART-04 (R3, R4, R6) |

Ngoài phạm vi ART-04 nhưng ADHOC làm thêm: 7 icon hiệu ứng, khung HUD hướng A, menu. Không xung đột với ART-04 (spec ART-04 mục 2 xếp icon hiệu ứng vào UI-01 P2 / Game-10).

### 4.4. Rủi ro phối hợp

- **Chung working tree và index git.** Hai phiên commit trên cùng nhánh `huycv`. ART-04 luôn commit theo đường dẫn tường minh. Không commit `Assets/_JungleMaze/Art/**` (gồm 5 PNG `Art/Hud/*.png` đang sửa dở và thư mục `Art/UI/` chưa track), `Tools/Art/*`, `Docs/Art/*`, `ProjectSettings/TimeManager.asset`, `.codex/config.toml`: đều thuộc ADHOC hoặc phiên khác. ART-04 không phụ thuộc trực tiếp vào 5 PNG khung HUD, chỉ phụ thuộc 34 icon và catalog.
- **File chung không merge được bằng text:** `Hud.prefab`, `Bootstrap.cs`, `HudFeedbackRoot.cs`, scene. ADHOC đang sửa các file này, nên R3 chỉ bắt đầu sau khi ADHOC commit. Nếu ADHOC còn sửa prefab sau mốc đó thì R3 dừng và báo CPO, không tự gộp.
- **Một Unity Editor.** ADHOC quy định integration dev của họ là người duy nhất mutate/chạy test Unity trong giai đoạn build. ART-04 chạy R2/R3 sau ADHOC QA. Sau mỗi lần chạy PlayMode qua MCP: `git checkout -- ProjectSettings/EditorSettings.asset` (giữ `m_EnterPlayModeOptions: 0`).
- **CSV là đầu vào script của ADHOC.** Đổi tên/xóa cột hay đổi `dich_chuan_hoa` sẽ làm hỏng lần chạy lại `normalize_item_icons.py`. R1 chỉ đổi `trang_thai`/`ghi_chu` và thêm cột cuối. Báo Codex trước khi commit R1.
- **Catalog gắn qua field serialized của Bootstrap nhưng Bootstrap có thể tự tạo (T-1).** Chỉ `Scenes/Boot.unity` chứa object Bootstrap. Khi bấm Play thẳng ở `HudPreview.unity`/`Game.unity`, `BootstrapAutoCreate` tạo `new GameObject("[Bootstrap]").AddComponent<Bootstrap>()`, field serialized là null, và theo kế hoạch ADHOC sẽ rơi về `NullItemIconProvider`. Hậu quả: demo F4 trong HudPreview (bằng chứng AC7 và môi trường thử AC20) **hiện ô giữ chỗ**. ADHOC 4.7 đã ghi "phải kiểm thật". Cần ADHOC xử lý trước R4.
- **Point filter khi thu nhỏ** texture 64 px xuống 28/42 px (tỉ lệ 0,44 và 0,66, không nguyên) có thể làm rơi pixel viền 1 px, nhất là các icon có viền sáng. Contact sheet 28 px của ADHOC dùng nearest nên đã phản ánh phần nào. Phải xem ở R4(c) cả 3 độ phân giải. Nếu răng cưa rõ thì cân nhắc Bilinear riêng cho icon item (quay về G-3): sửa asset import, không sửa code.
- **Đếm AC hai lần.** G3 của ADHOC không đóng ART-04. G3 ART-04 cần thêm AC20(b) do người làm và quyết định T-4.
- **Trùng test.** R2 phải đọc test ADHOC đã commit rồi bỏ assert trùng. Nếu ADHOC đổi tên/vị trí test thì cập nhật cột "Tương ứng ADHOC" của 4.1 ở nhật ký build.
- **Ticket kề cận:** ITM-01 (Quang Huy) sau này ghi vào writer port và có thể ghi `IGroundItemStateWriter` nếu R3 làm. ENV-02/ART-03 có thể gọi `IItemIconProvider` cho đồ trên sàn. WPN-01 (Hào) không bị ảnh hưởng. ART-04 không sửa vùng của Hào hay ITM-01.

**G1 · 27/10/2026 02:05 · CPO duyệt thay người dùng** (người dùng giao toàn quyền quyết định tới 12:00 27/10, xem `Docs/CPO_Runbook.vi.md` mục 1):
- Duyệt mặc định của spec và các quyết định ADHOC đã chốt thay ở mục 4.3.
- AC14 (gợi ý nhặt dưới sàn) hoãn sang ITM-01. AC20 (thử nhận diện với 3 người) do người dùng làm, không chặn G3 kỹ thuật.
- Q11/Q12 (sửa Ref GDD, cập nhật sheet Assets STT 4/6) giao người dùng.
- T-1 bắt buộc sửa trong ADHOC.
- Sau G1, toàn bộ icon được vẽ lại ở lưới 32 px (`Docs/Art/CODEX_TASK_32px_Restyle.md`); không ảnh hưởng `.meta` hay liên kết.

## 5. Nhật ký build

### R2-a, R2-b · 27/10/2026 · unity-dev (commit e8b57415, 8e047049)

Chỉ thêm test, không sửa code production, PNG hay prefab. Đã đọc `UiArtCatalogTests`, `UiArtIntegrationTests`, `HudAcceptanceTests` và bỏ các assert trùng: phủ 34 ID của catalog, import, cỡ sprite, 1 MB, known/unknown/known, placeholder, swap art, GC 3 ID.

**R2-a** `Tests/EditMode/Art04/Art04IconInventoryTests.cs` (4 test, đạt 4/4):

| Test | AC | Kết quả đo |
| --- | --- | --- |
| `Csv_34Rows_MatchRosterOneToOne_WithValidStatusAndRealSource` | AC1, AC2 | 34 dòng khớp 1-1 với JSON (18 Weapon + 16 Equipment, cột `loai` đúng); 34/34 `Đã chuẩn hóa`; 34/34 `nguon_thuc` tồn tại (13 JSurviv, 21 `Tools/Art/native32_recipes.py`) |
| `Csv_TargetPath_IsTheSpriteTheCatalogServes` | AC1 | `dich_chuan_hoa` của cả 34 dòng = đường dẫn sprite catalog trả về |
| `Png_64x64_TransparentCorners_Content44To60_Centered_FewSemiTransparent` | AC3, AC4 | 34/34 64×64; 4 góc alpha 0; cạnh dài nhất của bbox alpha > 8 = 56 px; lệch tâm ≤ 1 px; bán trong suốt 0 % |
| `HudPrefabsAndCatalog_DependOnNoImageUnderImported` | AC17 | 100 phụ thuộc. Dưới `Assets/Imported/` chỉ có `JSurviv/TextMesh Pro/Shaders/TMP_SDF-Mobile.shader`, loại trừ có chủ đích theo quyết định của CPO, có chú thích trong test. 0 ảnh |

**R2-b** `Tests/PlayMode/Art04IconSweepTests.cs` (6 test, đạt 5/6), dùng `Hud.prefab` với catalog thật:

| Test | AC | Kết quả |
| --- | --- | --- |
| `DefaultSlot1_IsHand_Selected_FullAlpha_AndSelectionMovesWithoutSwappingIcons` | AC8, AC10 | Đạt. Mặc định khe 1 có sprite HAND, Selected, alpha 1. Chọn lần lượt 8 khe: khung đi theo, sprite không đổi, alpha 1 |
| `Sweep_18WeaponsThroughSlots1To4_10ConsumablesThroughSlots5To8` | AC7 | Đạt. 112 lần ghi; sprite = `GetIcon(id)`, khác lần trước (trừ HAND ghi đè HAND mặc định) |
| `IconRect_ClearOfKeyCountAndName_At1280x720And1920x1080` | AC11 | **Đỏ, layout UI-01 giao nhau thật** (giữ nguyên assert, xem dưới) |
| `PickupToast_ShowsTheRightIconForAll34Ids` | AC13 | Đạt. 34/34 ID, mỗi lần đúng 1 dòng, sprite đúng, không tô màu |
| `SecondWriter_StandInForItm01_GivesTheSameSprites` | AC15 | Đạt. Writer giả ITM-01 ghi snapshot theo thứ tự khác qua `IInventoryStateWriter`/`IHudStateWriter`: 8 sprite, 8 trạng thái khe và sprite toast ARMOR giống writer mock |
| `Rotate34IdsThrough8Slots600Times_AfterWarmup_NoGc` | AC19 | Đạt. Warm-up 120 frame; 600 vòng × (8 khe + 1 dòng toast), 34 ID luân phiên: **0 B** |

**AC11 đỏ, số đo.** Canvas world-space theo hệ số của scaler Expand (1 đơn vị = 1 px màn hình):
- 1280×720: icon 28×28 lấn **1 px theo chiều dọc** lên ô số phím (giao 2×1 = **2 px²**, cả 8 khe) và lên `×n` (giao 12×1 = **12 px²**, khe 5–8).
- 1920×1080: số phím 3×1,5 = **4,5 px²**; `×n` 18×1,5 = **27 px²**.
- Tên: 0 px² ở cả hai độ phân giải.
- Đây là rect `RectTransform` của bố cục UI-01: icon y −317…−289, nhãn y −290…−274. Phần ảnh nhìn thấy không chạm, vì nội dung 56/64 để lại khoảng trong suốt khoảng 1,75 px quanh icon 28 px.
- Cần CPO chọn: (a) UI-01 dời số phím/`×n` lên 1 px hoặc thu icon; (b) đổi AC11 sang đo rect nội dung nhìn thấy.

**Toàn bộ suite:** EditMode 337/337. PlayMode 64/65 (chỉ đỏ AC11 ở trên). Console 0 Error/Warning. Đã `git checkout -- ProjectSettings/EditorSettings.asset`.

### AC11 theo quyết định (b) của CPO · 27/10/2026 · unity-dev

**Quyết định của CPO (27/10):** AC11 đo phần ảnh nhìn thấy, không dời layout UI-01.

**Thay đổi test.** Test đổi tên thành `VisibleIcon_ClearOfKeyCountAndName_At1280x720And1920x1080`.
- Lấy bbox alpha > 8 của sprite đang gắn, giải mã từ PNG như R2-a.
- Quy bbox đó ra rect màn hình bên trong rect icon (Image Simple, preserveAspect).
- Assert rect nội dung nằm trong rect icon và giao với số phím, `×n`, tên = 0 px².
- Phần RectTransform lấn chỉ được ghi log `[ART-04 AC11]`, không assert.

**Số đo mới** (8 icon: HAND, CROSSBOW, RUBBER_GUN, SAHUR_BAT, GOD_LEAF, UNICORN_BLOOD, SPIRIT_TEAR, ELIXIR_OF_LIFE):
- Nội dung nhìn thấy giao số phím, `×n`, tên = **0 px²** ở cả 1280×720 và 1920×1080.
- Khoảng trống còn lại giữa mép trên nội dung và nhãn: khoảng 0,75 px ở 720p, khoảng 1,1 px ở 1080p (đệm 4/64 của icon cao 56 px).
- Ghi chú: R2-a đo được đệm trên nhỏ nhất của cả 34 icon là 4/64, tức khoảng 1,75 px ở ô 28. Con số này vẫn lớn hơn phần lấn 1 px, nên các icon chưa có trong lượt đo cũng không chạm nhãn.
- Log RectTransform giữ nguyên như lần trước: số phím 2 px² (720p) và 4,5 px² (1080p); `×n` 12 px² và 27 px².

**Toàn bộ suite:** PlayMode 65/65, EditMode 337/337. Console 0 Error/Warning. Đã `git checkout` `EditorSettings.asset`.

**Follow-up UI-01:** rect icon lấn nhãn (số phím, `×n`) 1 px trong phần đệm trong suốt, không ảnh hưởng hình. Sửa khi đổi layout ô khe.

### Sửa theo review R2 · 27/10/2026 · unity-dev (commit e43e7727)

Chỉ sửa `Tests/EditMode/Art04/Art04IconInventoryTests.cs`, không đụng PNG, prefab hay code production. R2-a giờ có 5 test, đạt 5/5.
- **Nên sửa 1 (AC11).** Test PNG đổi tên thành `Png_64x64_TransparentCorners_Content44To60_Padded3_Centered_FewSemiTransparent`. Test thêm assert đệm trong suốt ≥ 3 px ở mỗi cạnh, đo trên bbox alpha > 8. Lý do: 1 px lấn ÷ 0,4375 px màn hình mỗi px nguồn ở ô 28 cho khoảng 2,3 px nguồn. Đo được: đệm nhỏ nhất là 4 px ở cả 34 icon.
- **Nên sửa 2 (AC17).** Thêm test mới `HudPrefabs_HaveNoMissingScriptOnAnyGameObject`. Test gọi `GameObjectUtility.GetMonoBehavioursWithMissingScriptCount` riêng cho từng GameObject, kể cả object ẩn: `Hud.prefab` 253, `HudSlot.prefab` 10, `HudPickupToast.prefab` 5. Tổng script thiếu là **0**.
- **Gợi ý 1.** `Csv_TargetPath…` và test PNG tự kiểm `Assert.AreEqual(34, rows.Count)` nên vòng lặp không thể rỗng.
- **Gợi ý 5.** Đã sửa số ở bảng R2-a thành 13 JSurviv, 21 công thức, khớp CSV.

**Toàn bộ suite:** EditMode 338/338, PlayMode 65/65. Console 0 Error/Warning. Đã `git checkout` `EditorSettings.asset`.

## 6. Review

### Review test R2 · 27/10/2026 06:30 · Code Reviewer (phiên độc lập, chỉ đọc): **Qua**, không có lỗi Chặn

Phạm vi: `e8b57415`, `8e047049`, `0a5a4e28` (test) và `6c1ea276`, `f61cf4e8` (docs). Diff `Assets` chỉ có 2 file test, 2 `.meta` và thư mục `Art04.meta` (569 dòng thêm, 0 dòng sửa). Không đụng code production, PNG, prefab, `Imported`, `Backend`, balance. 5 commit message tiếng Anh, 0 dòng attribution. Đọc kèm `UiArtCatalogTests`, `UiArtIntegrationTests`, `HudAcceptanceTests`, hai asmdef test. Không chạy Unity: số 337/337 và 65/65 là theo log dev (mục 5). Số đo thêm do reviewer tính bằng Python/Pillow trên 34 PNG và CSV hiện có.

**Từng test có đo đúng AC không (xanh giả):**

| Test | AC | Nhận xét |
| --- | --- | --- |
| `Csv_34Rows_MatchRosterOneToOne_…` | AC1, AC2 | Đo thật: đếm 34, `Roster()` từ JSON (`Add` ném lỗi khi trùng), `loai`, trạng thái, `File.Exists`. Tập hợp thiếu/thừa kiểm hai chiều. CSV hiện có 34 dòng cùng một trạng thái `Đã chuẩn hóa`. Chỉ chấp nhận 2 giá trị (trạng thái lúc G3), chặt hơn T-3 (5 giá trị) có chủ ý |
| `Csv_TargetPath_IsTheSpriteTheCatalogServes` | AC1 | So CSV với **catalog** (không so với chính nó). Sprite thiếu cho đường dẫn rỗng nên vẫn đỏ |
| `Png_64x64_…` | AC3, AC4 | Công thức đúng: 4 góc đọc theo `GetPixels32` (gốc dưới-trái), bbox alpha > 8, cạnh dài 44–60, lệch tâm ≤ 2, bán trong suốt = `0<a<255` trên `a>0`. Không chia cho 0 (đã `continue` khi không có pixel nhìn thấy). Giải mã bằng `LoadImage`, huỷ texture trong `finally` |
| `HudPrefabsAndCatalog_DependOnNoImage…` | AC17 | Đúng quyết định CPO (xem dưới) |
| `DefaultSlot1_IsHand_…` | AC8, AC10 | `AreSame` với sprite catalog (không phải placeholder), Selected, alpha 1; rồi đổi `SetActiveSlot` qua cả 8 khe, mỗi khe kiểm đúng 1 Selected, sprite không đổi, alpha 1. Có chiều âm vì `Fill` điền đủ 8 khe |
| `Sweep_18Weapons…10Consumables…` | AC7 | 112 lần ghi (18×4 + 10×4), mỗi lần kiểm khe nhận ID, sprite = `GetIcon(id)`, khác sprite trước. Không rỗng vì có `Assert` số lượng 18 và 10 |
| `VisibleIcon_ClearOfKeyCountAndName_…` | AC11 | Xem bên dưới |
| `PickupToast_ShowsTheRightIconForAll34Ids` | AC13 | Lặp theo JSON; `Tick(10f)` làm hết hạn trước mỗi lần; `shown == 1` chặn vòng rỗng; màu trắng |
| `SecondWriter_StandInForItm01_…` | AC15 | Có đối chứng `Reset` (khe 2–8 đổi sang sprite khác) trước khi ghi lần hai; so sprite, `SlotVisual` và toast. Xem Gợi ý 2 |
| `Rotate34Ids…600Times_NoGc` | AC19 | 120 frame warm-up, xoay 34 ID qua 8 khe và 1 dòng toast; ID đổi ở mỗi lần gọi nên đi đúng nhánh đổi sprite. Xem Gợi ý 3 |

**Hai quyết định của CPO được cài đúng ý:**
- **AC17:** chỉ ảnh bị cấm (đuôi ảnh, `Texture2D`, `Sprite`); shader TMP JSurviv được loại có chú thích (`Art04IconInventoryTests.cs:111`). Log dev ghi 100 phụ thuộc, 0 ảnh. Test đo trên `GetDependencies(roots, true)` của `Hud.prefab`, `HudSlot.prefab`, `HudPickupToast.prefab`, catalog, và `Assert.NotNull` từng root nên không đo trên asset không tải được.
- **AC11:** đo rect phần nhìn thấy (bbox alpha > 8 của sprite đang gắn, đưa vào rect icon đã tính `preserveAspect`), giao với số phím, `×n`, tên phải = 0 px², cả 1280×720 và 1920×1080; phần RectTransform lấn vẫn ghi `Debug.Log "[ART-04 AC11]"` kèm px². So với rect của nhãn TMP (không phải glyph) nên đo thận trọng. Dung sai 0,001 px chỉ để chống nhiễu float.

**Trùng test:** không đáng kể. Phần giao nhau chỉ là hình dạng chung: AC19 cùng dạng `ScopedLookupAndViewWarmGC` nhưng xoay 34 ID thay vì 3; toast cùng dạng `PickupRow_…` nhưng quét 34 ID; đường dẫn catalog trong `Csv_TargetPath` đứng cạnh `ItemCatalog_Covers34…` nhưng chủ thể là CSV. Mọi assert khác nhau. Không test nào lặp `Validate`, import, 1 MB, known/unknown/known, swap art của ADHOC.

**Độc lập giữa các test, dọn dẹp:** không phụ thuộc thứ tự. Mỗi test dựng store, `EventBus`, prefab mới; `TearDown` khôi phục `Time.timeScale` và `DestroyImmediate` mọi object; texture tạm huỷ trong `finally`. Không động `PlayerPrefs`, không nạp scene, không đổi asset (catalog dùng chỉ đọc). Độ phân giải mô phỏng bằng canvas world-space nên không phụ thuộc cửa sổ Game. Bộ nhớ đệm `_contentBounds` sống theo fixture, khoá bằng sprite của asset, vô hại.

**asmdef, namespace:** EditMode `JungleMaze.Tests.Art04` (asmdef chỉ Editor, đúng với `JungleMaze.Tests.UiArt`); PlayMode `JungleMaze.Tests.PlayMode`, cả file nằm trong `#if UNITY_EDITOR` như các test HUD kề bên nên `AssetDatabase` không lọt vào player build. `.meta` 2 dòng giống `UiArtIntegrationTests.cs.meta`. Comment mới là XML summary tiếng Anh.

#### Nên sửa

1. **AC11 chỉ đo 8/34 icon, phần còn lại dựa vào một số đo mà không test nào ép.** `Art04IconSweepTests.cs:125` điền 8 ID. Log mục 5 suy ra 26 icon còn lại an toàn vì "đệm trên nhỏ nhất 4/64". Đúng với 34 file hiện nay (reviewer đo lại: đệm trên, dưới, trái, phải tối thiểu đều 4 px). Nhưng R2-a (`Art04IconInventoryTests.cs:88-91`) chỉ đòi cạnh dài ≤ 60 và lệch tâm ≤ 2, nghĩa là cho phép đệm trên bằng 0. Icon sẽ còn được vẽ lại tại chỗ (spec ADHOC). Kịch bản: art mới cao 60 px, lệch lên 2 px, đệm trên 0 ⇒ phần nhìn thấy của icon lấn 1 px canvas vào hàng nhãn (số phím, `×n`); cả hai test vẫn xanh vì AC11 không đo icon đó. Hướng sửa: thêm vào R2-a assert đệm mỗi cạnh ≥ 3 px (> 1 px canvas ÷ 0,4375 px/px nguồn ở ô 28), hoặc cho test AC11 quét cả 34 ID (vũ khí vào khe 1–4, trang bị vào khe 5–8) thay vì 8.
2. **AC17 "0 script thiếu" không có test nào giữ.** `Art04IconInventoryTests.cs:103` chỉ quét phụ thuộc; không file nào dưới `Tests` có `GetMonoBehavioursWithMissingScriptCount`. QA ADHOC có kiểm tay 7 prefab (mục 7 ADHOC) nhưng AC17 là P0 và R2-a là chỗ ghi nhận nó. Hướng sửa: gọi `GameObjectUtility.GetMonoBehavioursWithMissingScriptCount` cho **từng GameObject con** của 3 prefab (hàm chỉ đếm trên chính object truyền vào, không đệ quy), kết quả = 0.

#### Gợi ý

1. **Vòng lặp có thể rỗng nếu CSV mất dòng.** `Csv_TargetPath…` và `Png_64x64…` lặp theo `ReadCsv()` mà không tự kiểm số dòng; CSV chỉ còn dòng tiêu đề thì cả hai xanh. Hiện test 1 vẫn đỏ nên bộ test không bị xanh giả, nhưng mỗi test chạy riêng thì có. Thêm `Assert.AreEqual(34, rows.Count)` hoặc lặp theo `Roster()`.
2. **`SecondWriter…` mạnh hơn ở đối chứng `Reset` so với ở "writer thứ hai".** Cả hai writer ghi vào cùng `InventoryStateStore` qua cùng port, nên test chỉ bắt được phụ thuộc thứ tự ghi hoặc phụ thuộc `MockInventoryState`; đúng chữ AC15 (ghi qua `IInventoryStateWriter`/`IHudStateWriter`) và mô phỏng ITM-01 ở mức port, chưa nối server thật (R5). Writer 1 còn gọi `_hud.NotifyPickup` trực tiếp thay vì qua mock. Nên ghi rõ giới hạn này ở nhật ký.
3. **AC19 không đo phần dựng lại canvas.** 600 vòng chạy trong một frame, nên TMP/`Canvas` rebuild xảy ra sau điểm đo; 120 frame warm-up có rebuild nhưng không được tính. Cùng phương pháp với UI-01 và ADHOC (đúng theo spec "cùng phương pháp đo UI-01"). Profiler trong trận thật vẫn là bằng chứng duy nhất cho phần này.
4. **Loại trừ AC17 rộng hơn quyết định.** Code loại **mọi** phụ thuộc không phải ảnh dưới `Imported/`, không riêng shader TMP; một prefab, material hoặc font asset JSurviv (atlas là sub-asset của `.asset`, không phải `Texture2D` chính) sẽ lọt. Hiện chỉ có đúng một file shader nên chưa sai. Nếu muốn khoá: liệt kê phụ thuộc không-ảnh và assert bằng đúng đường dẫn `TMP_SDF-Mobile.shader`.
5. **`nguon_thuc` của 21 dòng cùng trỏ vào `Tools/Art/native32_recipes.py`.** Test chỉ kiểm file đó tồn tại. Reviewer đã đối chiếu tay: cả 21 ID đều có trong file công thức. Số trong log mục 5 ("10 JSurviv, 24 công thức") lệch CSV hiện tại (**13 JSurviv, 21 công thức**, CSV không đổi sau `29cf9797`); sửa số trong log.
6. **AC10 vế "khe không có đồ ≤ 0,35".** R2-b chỉ kiểm alpha 1 cho khe có đồ; vế khe rỗng ≤ 0,35 chưa test nào kiểm (`HudAcceptanceTests.cs:297` kiểm `< 0,5`). Ngoài phạm vi R2 đã giao, ghi lại để QA đo.

**Chưa kiểm chứng:** chạy Unity (số test theo log dev); AC11 tại 2560×1440 (spec chỉ yêu cầu 720p, 1080p); AC20 (nhận diện mù) vẫn thuộc người dùng.

(Phiên review ghi trực tiếp mục này theo yêu cầu của CPO ngày 27/10; không sửa file nào khác.)

## 7. QA

**Kết quả QA 27/10/2026 (HEAD `f61cf4e8`): Đạt 19 · Một phần 2 · Không kiểm 2 · Không đạt 0.** EditMode **337/337**, PlayMode **65/65** chạy mới (job `2e4d69ce…`, `49dc1a95…`), console 0 error, 0 warning. Chi tiết, số đo và ảnh: [`Docs/Reports/ART-04_2026-10-27/QA_report.md`](../Reports/ART-04_2026-10-27/QA_report.md). Từ bản ADHOC đã QA đến nay chỉ thêm test, nên ảnh HUD và `icon_audit.json` của ADHOC được dùng lại (ghi rõ từng dòng).

| # | Kết quả | Bằng chứng |
| --- | --- | --- |
| AC1 | Đạt | `Csv_34Rows_MatchRosterOneToOne_…` và `Csv_TargetPath_IsTheSpriteTheCatalogServes` xanh; 34 dòng, 34 `nguon_thuc` tồn tại |
| AC2 | Đạt | 34/34 `Đã chuẩn hóa`, 0 `Thiếu`, 0 `Cần cắt/chỉnh`; HAND, ROPE có icon thật (ảnh ADHOC) |
| AC3 | Đạt | `Png_64x64_TransparentCorners_Content44To60_…` xanh; 34/34 64×64, góc alpha 0, nội dung 56 px, lệch tâm ≤ 1 px |
| AC4 | Đạt | Bán trong suốt 0 %; icon tối đọc rõ trên Selected và Default (ảnh `dark_icons_selected_vs_default_1280_x3.png`) |
| AC5 | Đạt (giá trị đã sửa) | Đo importer 34/34: Sprite, Single, Point, không mipmap, Uncompressed, alphaIsTransparency, không Read/Write, pivot 0,5. Point thay Bilinear (G-3), PPU 100 (T-2) |
| AC6 | Đạt | `UiArtCatalogTests` (34 ID, đôi một khác nhau, null/unknown → null) |
| AC7 | Đạt | `Sweep_18WeaponsThroughSlots1To4_10ConsumablesThroughSlots5To8` xanh, 112 lần ghi |
| AC8 | Đạt | `DefaultSlot1_IsHand_Selected_FullAlpha_…` xanh; ảnh `hud_1920.png` (ADHOC) |
| AC9 | Đạt | `RealQuickbar_KnownUnknownKnownAndEmpty` xanh; ảnh khe rỗng (ADHOC) |
| AC10 | Đạt | Cùng test AC8: khung đi theo, sprite giữ nguyên, alpha 1 |
| AC11 | Đạt (theo định nghĩa CPO) | `VisibleIcon_ClearOfKeyCountAndName_…` xanh: nội dung nhìn thấy giao 0 px² ở 720p, 1080p. Rect `RectTransform` lấn 1 px (2 và 4,5 px² với số phím; 12 và 27 px² với `×n`): follow-up UI-01 |
| AC12 | Đạt | Prefab 28×28 và 18×18; đo trên màn hình 24 / 36 / 48 px (kỳ vọng 24,5 / 36,8 / 49) ở 720p / 1080p / 1440p; texture 64 ≥ 56 |
| AC13 | Đạt | `PickupToast_ShowsTheRightIconForAll34Ids` xanh; ảnh toast 34 ID (ADHOC); dòng cao 31 giữ nguyên |
| AC14 | Không kiểm | Hoãn sang ITM-01 (T-4) |
| AC15 | Một phần | `SecondWriter_StandInForItm01_GivesTheSameSprites` xanh; diff không đụng Domain/Application/Infrastructure/Core ngoài Audio. GameServer thật **chưa kiểm chứng** (R5) |
| AC16 | Đạt | 8 khe và toast đủ icon ở 1280/1920/2560 (ảnh ADHOC), icon nằm trọn trong khe và dòng cha (hình học prefab). Hint nhặt thuộc AC14 |
| AC17 | Đạt (theo quyết định CPO) | `HudPrefabsAndCatalog_DependOnNoImageUnderImported` xanh: 0 ảnh từ `Imported`; 0 script thiếu; Console 0 error. Shader TMP JSurviv loại trừ, follow-up |
| AC18 | Đạt | `git diff a529b10a~1..HEAD`: 0 file dưới `Imported/JSurviv`, `Backend/`, balance; không Domain/Application/Infrastructure/Core ngoài Audio |
| AC19 | Đạt | `Rotate34IdsThrough8Slots600Times_AfterWarmup_NoGc` 0 B; `ScopedLookupAndViewWarmGC` 0 B |
| AC20 | Không kiểm | Việc của người dùng: ký CSV, nhận diện mù 3 người, cảm nhận độ nhòe |
| AC21 | Đạt | `SwapCatalogSprite_PreservesSlotAndToastRects` xanh |
| AC22 | Đạt | Test ≤ 1 MB xanh; đo 603 904 B (590 KiB) |
| AC23 | Một phần | 34/34 dòng `bound (32px native, 64x64 nearest x2)`, 64x64, không còn `todo`. Manifest không có cột nguồn; nguồn nằm ở `nguon_thuc` (CSV) và `Provenance.csv` |

**Quan sát:** (1) panel kết nối IMGUI ở HudPreview che khe 1 (1280) và một phần Vitals/chip, cần ẩn khi chụp ảnh G3 hoặc làm AC20(b); (2) tên dài trong toast ngắt giữa từ; (3) `ghi_chu` trong CSV còn mô tả hiện trạng cũ, dễ gây hiểu nhầm khi PIC ký.

**Chưa kiểm chứng:** AC14, AC20, R5 (GameServer thật, chờ ITM-01), Release Player.

## 8. Nghiệm thu

### G3 · 27/10/2026 08:20 · CPO (quyền người dùng giao tới 12:00): **Nghiệm thu**

**Căn cứ:**
- Review test R2: **Qua**, không có lỗi Chặn (mục 6).
- Hai mục Nên sửa và Gợi ý 1, 5 đã sửa ở `e43e7727` (test) và `1712d3ef` (log):
  - Test PNG đòi đệm ≥ 3 px mỗi cạnh; đo được tối thiểu 4 px trên 34/34 icon.
  - Test mới kiểm 0 script thiếu trên mọi GameObject của 3 prefab HUD.
  - Hai test lặp theo CSV giờ tự đòi đủ 34 dòng.
- Suite chạy sau khi sửa: EditMode **338/338**, PlayMode **65/65**, console sạch.
- QA (mục 7, HEAD `f61cf4e8`): Đạt 19 · Một phần 2 · Không kiểm 2 · **Không đạt 0**. Mọi AC P0 đo được đều Đạt.
- `EditorSettings.asset` đã hoàn về; working tree chỉ còn `TimeManager.asset` và `.codex/config.toml` (không commit).

**Chấp nhận có điều kiện ở các AC chưa trọn:**
- **AC11:** đạt theo định nghĩa phần nhìn thấy (quyết định CPO 06:20). Từ `e43e7727`, đệm ≥ 3 px được test giữ cho cả 34 icon, không chỉ 8 icon quét qua khe.
- **AC14:** hoãn sang ITM-01 (G1, T-4).
- **AC15:** kiểm ở mức port bằng writer thứ hai. GameServer thật chờ ITM-01 (R5).
- **AC17:** đạt với ảnh. Shader TMP JSurviv được loại trừ (quyết định CPO 06:30).
- **AC20:** việc của người dùng, không chặn G3 kỹ thuật (G1).
- **AC23:** manifest không có cột nguồn; nguồn nằm ở `nguon_thuc` (CSV) và `Provenance.csv`. Chấp nhận, không thêm cột vào manifest dùng chung.

**Follow-up:**

| # | Việc | Giao cho |
| --- | --- | --- |
| F1 | Rect `RectTransform` icon lấn số phím, `×n` 1 px (2–27 px²); sửa khi đổi layout ô khe | UI-01 |
| F2 | Dời shader TMP khỏi `Imported/JSurviv`; sau đó thu hẹp loại trừ AC17 về đúng đường dẫn (Gợi ý 4) | Dọn dẹp UI (TMP Essentials) |
| F3 | Panel kết nối IMGUI ở HudPreview che khe 1 và Vitals | UI-01 |
| F4 | Tên dài trong toast ngắt giữa từ | UI-01 |
| F5 | AC14 hint nhặt dưới sàn; icon đạn (Q9); AC15 với GameServer thật (R5) | ITM-01 |
| F6 | AC10 vế khe rỗng ≤ 0,35 chưa test giữ (`HudAcceptanceTests` đang kiểm < 0,5); AC19 phần rebuild canvas chỉ có Profiler trong trận | QA-01 |
| F7 | `ghi_chu` trong CSV còn mô tả hiện trạng trước 32 px | Người dùng khi ký CSV (AC20) |

**Việc của người dùng:**
- AC20: ký CSV, nhận diện mù với 3 người, cảm nhận độ nhòe.
- Q11: sửa ô Ref trong GDD. Q12: cập nhật STT 4/6 sheet Assets.
- Cập nhật Status của ART-04 trong GDD thành Done.
