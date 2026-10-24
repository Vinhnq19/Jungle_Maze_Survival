# Wireframe UX/UI

Cập nhật **24/10/2026**. Nguồn: [Figma Codefest-2027](https://www.figma.com/design/oeZgisxdiR80hRqbikUwPb/Codefest-2027?node-id=0-1), Page 1, 15 frame kích thước 1280×720 (16:9). Ảnh chụp từng frame lưu trong [Wireframes/](Wireframes/), tên file bắt đầu bằng node ID (`3-2_…` là node `3:2`). Ảnh là bản chụp ngày trên: Figma đổi thì chụp lại và cập nhật bảng dưới.

> **Bản thiết kế lại v2 (24/10/2026, chờ duyệt):** page `Lastaway v2 — Redesign` trong cùng file Figma, xem [mục 5](#5-bản-thiết-kế-lại-v2-chờ-duyệt). Page gốc `Page 1` giữ nguyên. Khi v2 được duyệt, v2 thay bảng ở mục 2–3 cho các màn đã có.

Mục này bổ sung cho sheet **WIREFRAME** trong GDD (cột "Luồng màn hình" trước đây ghi "Figma: chưa cung cấp").

## 1. Figma dùng để làm gì, GDD dùng để làm gì

Wireframe được vẽ trên khung một game battle royale chung ("PUBG 2.5D": 100 người, squad 4 người, M416/Kar98k, ống ngắm, Boost, BP/G-Coin, Battle Pass). Lastaway khác: tối đa **10 người/phòng**, 4 khe vũ khí + 4 khe dược phẩm, vũ khí và trang bị theo sheet Weapons/Equipments, Adrenaline thay Boost, Lobby 15 giây.

| Lấy từ Figma | Lấy từ GDD |
| --- | --- |
| Bố cục, vị trí và kích thước khối trên màn 1280×720 | Nội dung khối: chỉ số nào, bao nhiêu khe, tên món |
| Luồng chuyển màn và các nút hành động | Số liệu: HP, giáp, thời gian, số người, số khe |
| Phân cấp thông tin (gì to, gì nhỏ, gì nổi bật) | Tính năng có hay không (squad, cứu đồng đội, nhảy dù…) |

Khi hai nguồn mâu thuẫn, **GDD thắng**. Khối Figma không có trong GDD thì để ngoài phạm vi và đưa vào "Câu hỏi cho người", không tự làm.

## 2. Frame → mục WIREFRAME → ticket

| Node | Ảnh | Màn hình | WF trong GDD | Ticket |
| --- | --- | --- | --- | --- |
| `13:2` | [13-2_boot_screen](Wireframes/13-2_boot_screen.png) | Màn khởi động | WF-1 Splash / Main Menu | UI-02 |
| `7:2` | [7-2_main_lobby](Wireframes/7-2_main_lobby.png) | Sảnh chính | WF-1 Splash / Main Menu | UI-02 |
| `8:7` | [8-7_find_room](Wireframes/8-7_find_room.png) | Tìm phòng | WF-2 Tạo / Vào phòng | UI-02 |
| `8:124` | [8-124_create_room](Wireframes/8-124_create_room.png) | Tạo phòng | WF-2 Tạo / Vào phòng | UI-02 |
| `8:223` | [8-223_waiting_room](Wireframes/8-223_waiting_room.png) | Phòng chờ | WF-3 Lobby | UI-02, SRV-03 |
| `13:3` | [13-3_loading_match](Wireframes/13-3_loading_match.png) | Đang tải trận | WF-4 Loading | UI-02 |
| `3:2` | [3-2_hud_gameplay](Wireframes/3-2_hud_gameplay.png) | HUD trong trận | WF-5 Gameplay HUD, WF-13 Loot/Hành động/Trạng thái | **UI-01**; vòng bo ZON-01; killfeed chưa có ticket sở hữu |
| `7:186` | [7-186_big_map](Wireframes/7-186_big_map.png) | Bản đồ lớn (phím M) | WF-5 (phần bản đồ) | UI-01 (minimap), mở rộng sau |
| `3:3` | [3-3_inventory](Wireframes/3-3_inventory.png) | Balo / nhặt đồ | WF-6 Inventory / Loot | ITM-01, ART-04 |
| `7:337` | [7-337_settings](Wireframes/7-337_settings.png) | Cài đặt | WF-7 Settings | UI-03 |
| `7:304` | [7-304_spectate_teammate](Wireframes/7-304_spectate_teammate.png) | Theo dõi người chơi | WF-8 Bị loại / Spectate | UI-03 |
| `3:4` | [3-4_win](Wireframes/3-4_win.png) | Thắng | WF-9 Kết quả / Chơi lại | UI-02 |
| `3:5` | [3-5_lose](Wireframes/3-5_lose.png) | Thua | WF-9 Kết quả / Chơi lại | UI-02 |
| `7:268` | [7-268_knocked_down](Wireframes/7-268_knocked_down.png) | Bị hạ gục, chờ cứu | Không có trong GDD | Ngoài phạm vi (cần chốt) |
| `7:103` | [7-103_plane_parachute](Wireframes/7-103_plane_parachute.png) | Máy bay và nhảy dù | Không có trong GDD | Ngoài phạm vi (cần chốt) |

Chưa có frame cho **WF-10 Mất kết nối / Reconnect**, WF-11 (gây/nhận sát thương) và WF-12 (nguy hiểm sắp xảy ra). Ticket cần các màn này phải tự đề xuất bố cục và đưa người duyệt ở G1.

## 3. Khối của HUD `3:2` (cho UI-01)

| Khối Figma (node) | Vị trí trên 1280×720 | Ghi chú khi chuyển sang Lastaway |
| --- | --- | --- |
| Killfeed `3:6` | trái trên, 280×120 | Giữ |
| Zone info `3:13` | giữa trên, 340×73 | Pha bo + đếm ngược; "còn sống" tối đa 10, bỏ "Đội" |
| Minimap `3:16` | phải trên, 220×220 | Giữ; vòng bo từ ZON-01 |
| Squad `3:23` | trái, 210×100 | Không có squad trong GDD → bỏ hoặc hỏi |
| Quanh nhân vật `3:35`, Tiến trình hồi máu `3:41` | giữa | Tiến trình dùng dược phẩm (WF-13) |
| Gợi ý nhặt `3:45` | giữa phải | "[F] Nhặt …" (phím F theo ENV-02) |
| Con trỏ `3:48` | theo chuột | Tâm ngắm + số đạn |
| Sinh tồn `4:2` | trái dưới, 330×123 | HP, Giáp, **Adrenaline** (thay Boost); mũ/giáp theo Equipments |
| Thanh dùng nhanh `4:23` | giữa dưới, 500×130 | **4 khe vũ khí + 4 khe dược phẩm** theo GDD, không phải 3 súng + ném + hồi phục |
| Balo thu gọn `4:58` | phải dưới | Mở Inventory (ITM-01) |

## 4. Cách agent dùng

- **game-designer:** mở ảnh trong `Docs/Wireframes/` bằng Read; ghi node ID vào spec; liệt kê khối Figma bị bỏ hoặc đổi theo GDD.
- **tech-lead / unity-dev:** dựng layout theo vị trí/kích thước trong bảng (tham chiếu canvas 1280×720, scale theo 16:9). Cần số đo chi tiết hơn thì dùng Figma MCP (`get_metadata` / `get_design_context` với file key `oeZgisxdiR80hRqbikUwPb` và node ID) nếu phiên có kết nối Figma.
- **qa-tester:** chụp màn hình Game view 1280×720 và đặt cạnh ảnh wireframe khi kiểm AC về bố cục.

## 5. Bản thiết kế lại v2 (chờ duyệt)

Page `Lastaway v2 — Redesign` (node `39:2`). Nội dung đã viết lại theo GDD: 10 người, vào phòng bằng mã, 4 khe vũ khí + 4 khe dược phẩm, Adrenaline; không squad, không nhảy dù, không cứu đồng đội, không rank/cửa hàng. Ảnh chụp trong [Wireframes/v2/](Wireframes/v2/).

| Node | Ảnh | Màn | WF | Ticket |
| --- | --- | --- | --- | --- |
| `39:3` | [style guide](Wireframes/v2/39-3_style_guide.png) | Màu, chữ, component, quy tắc HUD | — | Mọi ticket UI |
| `41:2` | [HUD](Wireframes/v2/41-2_hud.png) | HUD trong trận | WF-5, WF-13 | **UI-01** |
| `41:180` | [HUD chú thích](Wireframes/v2/41-180_hud_annotated.png) | HUD kèm vị trí/kích thước khối và vùng giữa sạch | WF-5 | **UI-01** (AC bố cục) |
| `42:88` | [Menu chính](Wireframes/v2/42-88_flow01_main_menu.png) | Chơi · Cài đặt · Thoát, trạng thái máy chủ | WF-1 | UI-02 |
| `42:137` | [Vào phòng](Wireframes/v2/42-137_flow02_join_room.png) | Tên, mã 6 ký tự, Local/LAN/Online, RTT | WF-2 | UI-02, CLI-01 |
| `42:182` | [Phòng chờ](Wireframes/v2/42-182_flow03_lobby.png) | 10 chỗ, sẵn sàng, đếm ngược 15 s khi đủ 6 | WF-3 | UI-02, SRV-03 |
| `42:269` | [Đang tải](Wireframes/v2/42-269_flow04_loading.png) | Tiến trình, các bước, mẹo | WF-4 | UI-02 |
| `43:131` | [Mất kết nối](Wireframes/v2/43-131_flow05_reconnect.png) | Lớp phủ kết nối lại, thời gian chờ, về menu | WF-10 | UI-02 |
| `43:283` | [Bị loại](Wireframes/v2/43-283_flow06_eliminated.png) | Hạng, ai hạ, sát thương đã gây, máu còn lại của kẻ hạ | WF-8 | UI-03 |
| `43:445` | [Kết quả](Wireframes/v2/43-445_flow07_result.png) | Thắng/thua, chỉ số, bảng 10 người, chơi lại | WF-9 | UI-02 |

**Component cục bộ** (sửa một chỗ, mọi màn đổi theo): `Button` (`39:123`, Primary/Secondary/Ghost), `Slot` (`39:139`, Default/Selected/Empty), `PlayerSlot` (`39:155`, Ready/NotReady/Empty), `KillfeedRow` (`39:156`), `KeyCap` (`39:115`).

**Khối HUD v2** (canvas 1280×720, gốc trên trái):

| Khối | x, y | w × h |
| --- | --- | --- |
| Killfeed | 24, 24 | 171 × 90 (tối đa 3 dòng, dòng cũ mờ dần) |
| Minimap | 1056, 24 | 200 × 200 |
| Match status (bo + còn sống) | 1056, 232 | 200 × 74 |
| Vitals (máu, giáp, mũ, Adrenaline) | 24, 532 | 296 × 164 (mép phải x = 320, chạm vừa vùng giữa sạch, không giao) |
| Quick bar (vũ khí đang cầm + 8 khe) | 358, 566 | 564 × 130 |
| Vùng giữa sạch | 320, 180 | 640 × 360 |

### 5.1. HUD phản hồi (UI-01 phần 2, chờ G1)

Frame riêng, không sửa HUD lõi `41:2` đã duyệt: `48:239` ([ảnh](Wireframes/v2/48-239_hud_feedback.png)), bản chú thích `48:461` ([ảnh](Wireframes/v2/48-461_hud_feedback_annotated.png)). Component mới: `EffectChip` (`48:233`, Debuff/Buff) và `PickupToast` (`48:234`).

| Khối | Vị trí trên 1280×720 | Hành vi |
| --- | --- | --- |
| Tiến trình dùng đồ | Thay dải vũ khí trong Quick bar: 411,572 · 458×32 | "Đang dùng Lá thần", thanh cam, giây còn lại, "Di chuyển để hủy"; khe đang dùng viền cam |
| Hiệu ứng đang chịu | 24,466 · 144×58 (ngay trên Vitals) | Ô 40×40, đỏ = bất lợi, xanh lá = có lợi, thanh và giây đếm ngược, tối đa 6 |
| Nhặt đồ | Góc phải dưới: 1115,564 · 141×132 | Mới nhất ở dưới, tối đa 3 dòng mờ dần, phần dư gộp "+n món khác" |
| Cảnh báo bom/bẫy | Giữa trên: 490,24 · 301×40 | Nền đỏ nhạt, giây nổ, hướng và khoảng cách; tự ẩn |
| Hướng nhận sát thương | Cung đỏ trên vòng biên vùng giữa sạch (rx 330, ry 190 quanh tâm) | Chỉ về phía nguồn sát thương, mờ dần |
| Hướng bom/bẫy ngoài màn | Mũi tên đỏ viền + giây trên cùng vòng đó | Khác cung đặc của sát thương |
| Hit marker | 4 vạch quanh tâm ngắm | Trắng khi trúng; particle, số sát thương nổi thuộc VFX-01 |

Chưa vẽ trong v2: Cài đặt (UI-03), Theo dõi người chơi (UI-03), Balo (ITM-01), Bản đồ lớn. Màu đang là giá trị hex trong style guide, chưa tạo Figma variables.
