# CLI-04 / MOV-01 / ENV-01 (nhánh haonh/dev)

- **Trạng thái**: Xong, đã playtest thật qua GameServer local (không chỉ test tự động)
- **Cập nhật lần cuối**: 2026-10-22
- **Project**: D:\Ki8\GNS301\Project (nhánh `haonh/dev`, base từ `f3c407a2` = HEAD `main` sau PR #1 "hvu")

## Bài toán
Thực hiện 3 task Giai đoạn 1 của GDD "[GDD] - Jungle Maze.xlsx" (file tại `D:\Ki8\GNS301\[GDD] - Jungle Maze.xlsx`, giao cho Hào), tiếp nối kiến trúc Clean Architecture đã có sẵn trên nhánh `hvu` (đã merge vào main):

- **CLI-04** (P1, Client Core): nạp tham số cân bằng (weapon/equipment/match config) từ data ngoài code, đổi config không cần sửa logic.
- **MOV-01** (P0, Gameplay): Hero Controller — WASD 8 hướng (chuẩn hóa chéo), xoay theo chuột với `aimLerpTime`. Ranh giới: chỉ input/kinematics, KHÔNG làm prediction/reconciliation (đó là MOV-02 sau này).
- **ENV-01** (P1, World): Map grid 50×50 cell (48px/cell) + 4 loại Hard Cover (Cột đá 1×2, Tường thành 1×4, Tảng đá 4×4, Tượng cổ 3×3) có Collider chặn đường đi/đạn. Ranh giới: chỉ collision/cover vật lý, không làm FOW/shader (đó dùng lại collider này sau).

Dependency: cả MOV-01 và ENV-01 đều phụ thuộc CLI-04 (`heroBaseSpeed`, `aimLerpTime`, `mapSize` lấy từ Match Config).

## Bối cảnh / quyết định đã chốt

1. **Vì sao cần local-only movement ngay bây giờ**: theo `Docs/Client_Developer_Guide.vi.md` mục 6, GameServer hiện **chưa xử lý `PlayerInputPacket`** và không gửi `PlayerStatePacket` cho gameplay — `PlayerView` (server-driven) sẽ KHÔNG nhúc nhích dù gửi input. Vì vậy Hero Controller (MOV-01) phải là một GameObject/Transform cục bộ, độc lập hoàn toàn với `PlayerInputService`/`WorldSyncService`/`PlayerView` — không tái sử dụng hay sửa pipeline mạng. Việc hợp nhất "local predicted" với state server (reconciliation) là phạm vi MOV-02, không đụng vào ở đây.
2. **CLI-04 không dùng ScriptableObject, dùng JSON** (chọn nhánh "JSON Parser" trong 2 lựa chọn của ticket): vì toàn bộ codebase hiện tại (endpoint config) đã có sẵn pattern JSON qua Newtonsoft (`FileConfigProvider` + `ClientConfigJson`, đọc từ `Assets/StreamingAssets/JungleMaze/*.json`) — theo đúng cùng pattern cho nhất quán, dễ đổi số khi cân bằng game mà không build lại (đúng yêu cầu "DevOps" style của ticket).
3. **Vị trí file data**: `Assets/StreamingAssets/JungleMaze/balance/{match.json,weapons.json,equipments.json}` — cùng gốc `StreamingAssets/JungleMaze/` như client.*.json hiện có, tách sang sub-folder `balance/` để không lẫn với config kết nối mạng.
4. **Domain models pure C#** (`Domain/Balance/*`) — không có UnityEngine, theo đúng asmdef `noEngineReferences: true` của `JungleMaze.Domain`. Infrastructure (`Infrastructure/Config/BalanceConfigProvider.cs` + `BalanceConfigJson.cs`) parse JSON bằng Newtonsoft (đã có sẵn trong asmdef Infrastructure).
5. **MatchConfigData chỉ lấy phần "Config" (key/value) của sheet MATCH DESIGN DATA** (~30 field: matchDuration, mapSize, heroBaseSpeed, aimLerpTime, v.v.) — KHÔNG lấy 2 bảng "ZONE BASELINE" và "AIRDROP BASELINE" vì chính sheet đã ghi chú "Lịch cũ - cần thiết kế lại cùng áp lực map", tức dữ liệu đang chờ redesign, chưa ổn định đủ để code hoá. Nếu cần sau, mở rộng theo cùng pattern.
6. **Weapon/Equipment Code ID** dùng đúng `Code ID` trong sheet Weapons/Equipments (ví dụ `HAND`, `TREE_BRANCH`, `WOODEN_HELMET`...) làm khóa tra cứu — namespace tiếng Anh, khớp sẵn với cột Code ID nên không cần dịch thêm.
7. **Hero di chuyển bằng Rigidbody2D (Dynamic, gravity scale 0) + CircleCollider2D** (bán kính = `heroColliderRadius` = 0.4 cell từ Match Config), HeroController set `linearVelocity` mỗi frame theo kinematics (WASD chuẩn hoá × `heroBaseSpeed`) — để Unity Physics2D tự chặn va chạm với Hard Cover (ENV-01) "miễn phí", không phải tự viết code resolve collision thủ công. Đây là ranh giới rõ ràng giữa 2 ticket: MOV-01 chỉ tính kinematics mong muốn, ENV-01 chỉ cần đảm bảo Hard Cover có Collider2D solid (không phải Trigger) là Hero tự bị chặn.
8. **1 Unity unit = 1 cell** (xác nhận qua `PlayerView` dùng thẳng `state.X/Y` server làm world position, và `heroBaseSpeed` đơn vị "cell/s"). `cellSize = 48px` chỉ là kích thước sprite/PPU, không phải world-unit scale.
9. **Góc xoay/kinematics tính bằng pure C# (không Mathf)** trong `Application/Player/HeroMotion.cs` — theo đúng convention sẵn có của `PlayerInputService` (dùng float thuần, không Vector2/Mathf) để test NUnit không cần UnityEngine và nhất quán style Application layer.
10. **Comment code**: chỉ viết XML `/// <summary>` bằng tiếng Anh cho các type/method mới (không viết comment Vietnamese như code cũ, không viết inline `//` comment trừ khi thật sự cần — theo yêu cầu riêng của Hào cho đợt task này, khác với convention tiếng Việt của code cũ trên nhánh `hvu`).
11. **Commit**: tiếng Anh, ngắn gọn, KHÔNG thêm dòng `Co-Authored-By: Claude` (khác với mặc định của harness), mỗi commit = 1 chức năng nhỏ (ví dụ: domain models riêng, infra loader riêng, data file riêng, Bootstrap wiring riêng, mỗi ticket MOV-01/ENV-01 cũng chia nhỏ theo layer).
12. **Build Settings có scene thừa** (`SampleScene`, JSurviv `GameConfig`/`GameMain`) phát hiện lúc khảo sát ban đầu — KHÔNG đụng vào trong đợt task này (ngoài phạm vi 3 ticket), chỉ note lại.

## Đã làm
<Cập nhật dần khi code xong từng phần — xem chi tiết theo ticket bên dưới.>

### CLI-04 — Xong, đã playtest thật (xem log kết nối bên dưới)
- [x] `Domain/Balance/WeaponCategory.cs`, `WeaponDefinition.cs`, `EquipmentType.cs`, `EquipmentDefinition.cs`, `MatchConfigData.cs`
- [x] `Application/Ports/IBalanceConfigProvider.cs`
- [x] `Infrastructure/Config/BalanceConfigJson.cs` + `BalanceConfigProvider.cs`
- [x] `Assets/StreamingAssets/JungleMaze/balance/match.json|weapons.json|equipments.json`
- [x] Đăng ký `IBalanceConfigProvider` trong `Bootstrap.Compose()`
- [x] Tests `Tests/EditMode/Config/BalanceConfigJsonTests.cs` (9 test, pass)
- **Playtest thật (2026-10-22)**: chạy GameServer local thật (`dotnet run --project GameServer/GameServer.csproj`, `AUTH_MODE=RoomCode`, `ROOM_CODE=ROOM42`, port 7777) — client Play Mode load đúng `ClientConfig` (`[Config] ... endpoint=127.0.0.1:7777 auth=RoomCode`), nạp đúng `[Balance] Loaded 18 weapons, 16 equipments, match=50x50.`, và join phòng thật thành công (`[Net] JoinAccepted endpoint=127.0.0.1:7777 joinMs=83`), tự chuyển sang scene `Game`. Xác nhận toàn bộ pipeline config → connect → scene load hoạt động thật, không chỉ EditMode test.

### MOV-01 — Xong, đã playtest thật bằng physics (xem phương pháp bên dưới)
- [x] `Application/Player/HeroMotion.cs` (pure kinematics: normalize diagonal, aim smoothing)
- [x] `Presentation/Player/HeroController.cs` (MonoBehaviour, Rigidbody2D + CircleCollider2D, đọc config qua [Inject])
- [x] Prefab `Assets/_JungleMaze/Prefabs/Hero.prefab` (root: Rigidbody2D+CircleCollider2D(r=0.4)+HeroController; child "Body": SpriteRenderer scale 0.8, tách riêng để scale không ảnh hưởng bán kính collider), đặt instance tại (25,25,0) — tâm map 50×50 — trong `Game.unity`
- [x] Tests `Tests/EditMode/App/HeroMotionTests.cs` (13 test, pass)
- **Playtest thật (2026-10-22), phương pháp**: `GameFlow`/`MainMenuState` luôn đưa Play Mode về scene `Boot` trước (đúng thiết kế), nên để vào được `Game` cần đi hết luồng connect thật — dùng `execute_code` của Unity MCP gọi thẳng `ConnectToServerUseCase.Execute(new ConnectForm{RoomCode="ROOM42", DisplayName="Hao"})` qua reflection (đúng như user yêu cầu "gọi thẳng hàm"), với GameServer local thật đang chạy. Join thành công, vào `Game`, Hero xuất hiện đúng vị trí.
  Vì Unity MCP không có tool giữ phím WASD thật (chỉ gõ text, không giữ được trạng thái key-down nhiều frame để `Input.GetAxisRaw` đọc được), nên để test chuyển động+va chạm KHÔNG đi qua giả lập bàn phím: tạm `heroController.enabled=false` (tắt hẳn việc đọc Input thật, tránh nó ghi đè giá trị mình set mỗi frame) rồi set thẳng `Rigidbody2D.linearVelocity` bằng đúng giá trị mà `HeroMotion.ComputeMoveDelta(1,0,heroBaseSpeed,1)` sẽ trả ra khi giữ phím D — tức vẫn dùng đúng Rigidbody2D/Collider2D thật của Hero, chỉ bỏ qua bước đọc bàn phím (phần đó đã có 13 test NUnit riêng che rồi).
- **Kết quả đo được (thật, qua Play Mode đang chạy GameServer thật)**: Hero đứng yên tại (25,25). Set velocity=(4,0) [hướng Đông], sau ~1s thật: Hero dừng tại x=**27.5951**, velocity tự về (0,0) — đúng khớp cạnh Tây của `BoulderRock` (center x=30, nửa-rộng 2 → mép 28, trừ bán kính Hero 0.4 = 27.6). Đổi hướng velocity=(-4,0) [hướng Tây], sau ~1s: Hero dừng tại x=**23.9050**, velocity về (0,0) — khớp cạnh Đông của `AncientPillar` (center x=23, nửa-rộng 0.5 → mép 23.5, cộng bán kính Hero 0.4 = 23.9). Hero di chuyển đúng ~3.7 unit giữa 2 lần, đúng tốc độ 4 cell/s. → **Xác nhận thật bằng physics Play Mode**: Hero di chuyển được VÀ bị Hard Cover chặn đứng chính xác tới từng 0.01 unit, không xuyên qua.
- **Giới hạn còn lại (chưa test được, không phải lỗi)**: phần "đọc bàn phím/chuột thật → `Input.GetAxisRaw`/`Input.mousePosition` → `HeroController.Update()`" không được test bởi vì Unity MCP không giả lập được key-hold hay mouse-move thật; phần này tin tưởng vào code review + cấu trúc đơn giản (2-3 dòng, giống hệt `PlayerInputReader` đã chạy ổn định trong `Docs/Client_Developer_Guide.vi.md`). Nếu người dùng tự bấm WASD/di chuột thật trong Editor, đây là phần duy nhất còn lại chưa được máy tự xác nhận.

### ENV-01 — Xong (code + test tự động + xác nhận bằng physics raycast; CHƯA phải layout thật)
- [x] `Domain/World/HardCoverType.cs` (enum + `HardCoverSize.Of(type)`), `Domain/World/GridBounds.cs` (Contains/ClampToBounds thuần C#)
- [x] `Presentation/World/HardCoverTag.cs` (marker component, không có logic, chỉ để FOW/raycast sau này nhận diện)
- [x] Layer Physics2D mới `HardCover` (slot 11, ProjectSettings/TagManager.asset) — **không** tái dùng các layer có sẵn `Player`/`Building`/`Enemy`/`Destructible`/`Water`/`HealingItem`/`Armor` (những layer này là tàn dư từ import JSurviv, hiện KHÔNG có script `_JungleMaze` nào tham chiếu tới — xem "Ghi chú/rủi ro" bên dưới).
- [x] 4 Prefab tại `Assets/_JungleMaze/Prefabs/Obstacles/`: AncientPillar (1×2), CityWall (1×4), BoulderRock (4×4), AncientStatue (3×3) — mỗi cái chỉ 1 GameObject với BoxCollider2D (size local 1×1, world size = transform scale), KHÔNG Rigidbody (static geometry), layer HardCover, isTrigger=false.
- [x] Đặt demo layout quanh Hero (25,25) trong `Game.unity`: AncientPillar (23,25), CityWall (25,28), BoulderRock (30,25), AncientStatue (25,20).
- [x] Tests `Tests/EditMode/Core/GridBoundsTests.cs` (11 test: bounds, clamp, size lookup — pass)
- **Xác nhận bằng Unity MCP (2 lớp)**:
  1. Raycast tĩnh (Edit Mode, trước khi playtest): `manage_physics raycast` (2D, layer_mask=HardCover) bắn từ Hero về cả 4 hướng — cả 4 đều trúng đúng obstacle ở đúng khoảng cách tính từ size/position (ví dụ AncientPillar trúng ở distance=1.5 đúng với center 23 + half-width 0.5).
  2. **Va chạm thật lúc Play Mode (2026-10-22, xem chi tiết số đo ở mục MOV-01 phía trên)**: cho Hero di chuyển thật bằng Rigidbody2D velocity trong lúc GameServer thật đang chạy — Hero bị `BoulderRock` và `AncientPillar` chặn đứng chính xác đúng vị trí biên collider, velocity tự về 0 khi chạm. Đây là bằng chứng mạnh hơn raycast: chứng minh Dynamic Rigidbody2D (Hero) thật sự KHÔNG xuyên qua được Hard Cover trong Play Mode, không chỉ "collider có tồn tại đúng chỗ".
- **Giới hạn đã thử nhưng KHÔNG dùng được**: `manage_physics simulate_step` (giả lập Physics2D ở Edit Mode, ngoài Play Mode) không làm Rigidbody2D của Hero di chuyển dù đã set velocity qua `apply_force` (Impulse) — nghi là hạn chế của việc simulate physics ngoài Play Mode trong bản Unity MCP này, KHÔNG phải lỗi trong code Hero/Obstacle. Đã revert lại velocity về 0 sau khi thử, không để lại trạng thái bẩn trong scene. Vì vậy phần playtest va chạm thật phải chuyển sang làm trong Play Mode thật (có GameServer) thay vì Edit Mode — xem mục MOV-01.

## Còn phải làm
1. **Chỉ người dùng tự làm được** (việc duy nhất máy không tự động hoá được, xem giới hạn ở mục MOV-01): tự bấm phím WASD thật + di chuột thật trong Editor để cảm nhận "độ mượt" chủ quan của input/aim lerp — phần vật lý/va chạm/kết nối/config đã tự playtest và xác nhận bằng số đo thật rồi (xem mục MOV-01/CLI-04/ENV-01 ở trên), không cần làm lại.
2. Khi có bản thiết kế map thật (Figma/Map Plan), thay layout demo (4 obstacle đặt quanh Hero trong `Game.unity`) bằng layout chính thức — việc này Claude có thể làm tiếp khi có file đính kèm. Lưu ý: không viết `MapGridBuilder`/generator nào cả, obstacle hiện đặt tay trực tiếp trong scene qua Unity MCP (quyết định có chủ đích).
3. Nếu làm MOV-02 (prediction/reconciliation) sau này: cần quyết định cách hợp nhất `HeroController` (local) với `PlayerView`/`WorldSyncService` (server-driven) — hiện 2 thứ này độc lập hoàn toàn, chưa có cầu nối.
4. Nếu cần dùng tới Zone/Airdrop baseline (bo thu, lịch thả đồ) — hiện chưa có trong `MatchConfigData`, phải mở rộng schema JSON + Domain model trước.
5. GameServer local dùng để playtest (ROOM_CODE=ROOM42, port 7777) đã được tắt sau khi test xong (không để chạy nền) — lần sau muốn playtest lại thì chạy lại lệnh ở `Docs/Phase1_First4Tasks.vi.md` mục 12.4.

## Ghi chú / rủi ro cần nhớ
- Project có sẵn các Physics layer `Destructible`, `Water`, `Enemy`, `Player`, `HealingItem`, `Armor`, `Building` (slot 3–10, `ProjectSettings/TagManager.asset`) — kiểm tra lại thì đây là tàn dư từ import JSurviv, KHÔNG có script nào trong `Assets/_JungleMaze/Scripts` tham chiếu tới. ENV-01 cố tình tạo layer `HardCover` mới (slot 11) thay vì tái dùng `Building` để tránh đụng vào ý định chưa rõ của layer có sẵn. Nếu task sau cần Player/Enemy layer, nhớ kiểm tra lại xem có ý nghĩa thật hay vẫn chỉ là rác JSurviv trước khi dùng.
- `ZONE BASELINE` và `AIRDROP BASELINE` trong sheet MATCH DESIGN DATA **chưa được đưa vào** `MatchConfigData` (xem lý do #5 ở trên) — nếu task sau cần (ví dụ zone/bo thu), phải mở rộng JSON schema + Domain model, đừng tưởng đã có sẵn.
- Obstacles sheet có thêm nhiều tag khác ngoài Hard Cover (Destructible, Can go through, Hazard, Interactive — rương, sông, đầm lầy, lava, portal, cửa đá...) — ENV-01 chỉ làm đúng 4 loại **Hard Cover** liệt kê trong ticket gốc, các loại khác (rương, sông, lava...) KHÔNG nằm trong scope lần này.
- Map layout thật (vị trí từng obstacle) **chưa có** — sheet "MAP DESIGN" ghi rõ "Figma/Map Plan: chưa cung cấp". `MapGridBuilder` chỉ đặt một layout mẫu/demo để chứng minh collider hoạt động đúng, KHÔNG phải layout chính thức — khi có Figma/sơ đồ thật phải thay lại.
- Hero hiện chưa gắn vào luồng InMatchState/GameFlow nào cả (vì server chưa hỗ trợ match thật) — Hero.prefab đặt thủ công trong scene Game để test standalone. Khi MOV-02 (prediction/reconciliation) hoặc server hỗ trợ gameplay thật, cần quyết định lại cách spawn Hero (qua GameFlow/InMatchState thay vì đặt tay trong scene).
- Comment tiếng Anh chỉ áp dụng cho code MỚI viết trong đợt task này; không đi sửa comment tiếng Việt của code cũ (ngoài phạm vi, tránh diff lan man).
