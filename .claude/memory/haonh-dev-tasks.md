# CLI-04 / MOV-01 / ENV-01 (nhánh haonh/dev)

- **Trạng thái**: Đang làm
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

### CLI-04
- [ ] `Domain/Balance/WeaponCategory.cs`, `WeaponDefinition.cs`, `EquipmentType.cs`, `EquipmentDefinition.cs`, `MatchConfigData.cs`
- [ ] `Application/Ports/IBalanceConfigProvider.cs`
- [ ] `Infrastructure/Config/BalanceConfigJson.cs` + `BalanceConfigProvider.cs`
- [ ] `Assets/StreamingAssets/JungleMaze/balance/match.json|weapons.json|equipments.json`
- [ ] Đăng ký `IBalanceConfigProvider` trong `Bootstrap.Compose()`
- [ ] Tests `Tests/EditMode/Config/BalanceConfigJsonTests.cs`

### MOV-01
- [ ] `Application/Player/HeroMotion.cs` (pure kinematics: normalize diagonal, aim smoothing)
- [ ] `Presentation/Player/HeroController.cs` (MonoBehaviour, Rigidbody2D + CircleCollider2D, đọc config qua [Inject])
- [ ] Prefab `Assets/_JungleMaze/Prefabs/Hero.prefab`, đặt 1 instance trong `Game.unity` để test bằng Play Mode
- [ ] Tests `Tests/EditMode/App/HeroMotionTests.cs`

### ENV-01
- [ ] `Domain/World/HardCoverType.cs` + size lookup (pure C#)
- [ ] Prefab 4 loại Hard Cover (`Assets/_JungleMaze/Prefabs/Obstacles/`) với BoxCollider2D đúng size, layer/tag `HardCover`
- [ ] `Presentation/World/MapGridBuilder.cs` (đặt sample layout trong `Game.unity`, dùng `mapSize` từ Match Config)
- [ ] Physics2D layer `HardCover` (ProjectSettings/Physics2DSettings hoặc TagManager)
- [ ] Tests nếu có logic pure (bounds check)

## Còn phải làm
1. Code từng phần theo thứ tự: CLI-04 trước (MOV-01/ENV-01 phụ thuộc config) → MOV-01 → ENV-01.
2. Chạy EditMode test qua Unity MCP (`run_tests`) sau mỗi phần.
3. Dùng Unity MCP (`manage_asset`, `manage_prefabs`, `manage_gameobject`, `manage_components`, `manage_physics`) để tạo prefab/scene object thay vì tự tay viết YAML (an toàn hơn, đúng cách Unity quản lý GUID).
4. Vào Play Mode kiểm tra: không lỗi console, Hero object tồn tại đúng component. **Giới hạn**: MCP hiện không có tool giả lập bàn phím/chuột thật trong Play Mode, nên không tự động hoá được việc "bấm WASD rồi xem Hero chạy" — phần này cần người dùng tự bấm thử trong Editor, Claude chỉ xác nhận được qua test NUnit (toán kinematics) + không có lỗi compile/console.
5. Commit chia nhỏ theo từng gạch đầu dòng ở mục "Đã làm" (không gộp 3 ticket vào 1 commit).
6. Sau khi xong cả 3, cập nhật mục "Trạng thái" thành "Xong" và liệt kê rõ phần nào đã playtest thật, phần nào chỉ mới qua test tự động.

## Ghi chú / rủi ro cần nhớ
- `ZONE BASELINE` và `AIRDROP BASELINE` trong sheet MATCH DESIGN DATA **chưa được đưa vào** `MatchConfigData` (xem lý do #5 ở trên) — nếu task sau cần (ví dụ zone/bo thu), phải mở rộng JSON schema + Domain model, đừng tưởng đã có sẵn.
- Obstacles sheet có thêm nhiều tag khác ngoài Hard Cover (Destructible, Can go through, Hazard, Interactive — rương, sông, đầm lầy, lava, portal, cửa đá...) — ENV-01 chỉ làm đúng 4 loại **Hard Cover** liệt kê trong ticket gốc, các loại khác (rương, sông, lava...) KHÔNG nằm trong scope lần này.
- Map layout thật (vị trí từng obstacle) **chưa có** — sheet "MAP DESIGN" ghi rõ "Figma/Map Plan: chưa cung cấp". `MapGridBuilder` chỉ đặt một layout mẫu/demo để chứng minh collider hoạt động đúng, KHÔNG phải layout chính thức — khi có Figma/sơ đồ thật phải thay lại.
- Hero hiện chưa gắn vào luồng InMatchState/GameFlow nào cả (vì server chưa hỗ trợ match thật) — Hero.prefab đặt thủ công trong scene Game để test standalone. Khi MOV-02 (prediction/reconciliation) hoặc server hỗ trợ gameplay thật, cần quyết định lại cách spawn Hero (qua GameFlow/InMatchState thay vì đặt tay trong scene).
- Comment tiếng Anh chỉ áp dụng cho code MỚI viết trong đợt task này; không đi sửa comment tiếng Việt của code cũ (ngoài phạm vi, tránh diff lan man).
