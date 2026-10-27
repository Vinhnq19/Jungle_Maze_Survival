# TEL-01 — Ghi sự kiện trận để phân tích và cân bằng

| Trường | Giá trị |
| --- | --- |
| Giai đoạn / Deadline | Giai đoạn 3 / 31/10/2026 (ô Deadline = 46326, Excel serial) |
| Module | Testing |
| Ưu tiên | P2 |
| PIC | V.Huy |
| Phụ thuộc | Chính thức: **CLI-02** (Done, PIC Vũ). Thực tế nguồn dữ liệu thật còn thiếu: chiến đấu/vũ khí (WPN-02), túi đồ và loot (ITM-01), vũ khí ném/đặc biệt (WPN-03), bụi rậm/hazard. Vì vậy ticket làm song song bằng event giả (mục 2.10), không chờ các ticket đó. |
| Trạng thái | Spec (chờ G1) |
| Sheet GDD tham chiếu | Dòng TEL-01 trong PROJECT TRACKING, Ref `TEST & BALANCE!3–18,21–22`. Công cụ đọc GDD bỏ qua dòng trống, nên tôi **suy ra** số dòng như sau: dòng 3–10 = bảng kiểm thử STT 1–8 (End-to-End … Performance); dòng 11–12 là tiêu đề và đầu bảng BALANCE; dòng 13–18 = BALANCE STT 1–6 (TTK, Khống chế, Bo/Map, Endgame, Luật cũ mâu thuẫn, Scope mới); dòng 19–20 là tiêu đề và đầu bảng PLAYTEST; dòng 21–22 = PLAYTEST STT 1–2 (Playtest setup, Data to collect). Khớp đúng Ref nên giữ làm giả định G-1. Đọc thêm: `Weapons`, `Equipments`, `ITEM LIST`, `Obstacles` (lấy Code ID), QA-01, NET-02, BOT-01 trong PROJECT TRACKING. |
| Figma node | Không có (không có giao diện người chơi) |

## 1. Mục tiêu

Sau ticket này, một lượt playtest (thật hoặc giả lập) để lại **các file sự kiện thống nhất**, và một lệnh biến chúng thành **báo cáo so sánh được**: TTK theo vũ khí, tỷ lệ chết vì bo, loot và khống chế (CC) theo từng phiên bản config, cùng số đo hiệu năng (tick, FPS, RTT). QA-01 dùng báo cáo này để so trước/sau mỗi lần chỉnh cân bằng. TEL-01 **chỉ đo và báo cáo**: không quyết kết quả gameplay, không lưu kết quả sau trận, không tự đề xuất chỉnh số.

**Hướng đã chọn (đề xuất của designer, chờ G1):**

1. Dùng **18 loại bản ghi** do spec này định nghĩa (bảng 2.4). GDD không liệt kê sẵn 18 loại, nên danh sách là đề xuất để PIC xác nhận (Q1).
2. **JSONL là định dạng gốc**, mỗi dòng một bản ghi; CSV và báo cáo Markdown được sinh ra từ JSONL.
3. **Không sửa GameServer của Vinh trong ticket này.** Phần quan sát được từ ngoài (vòng đời trận, xuất hiện/loại, pha bo, hiệu năng) lấy qua các event public sẵn có và log `[metrics]`. Chỗ nào bắt buộc có hook, spec mô tả hook tối thiểu để **Vinh duyệt và tự gắn** (mục 2.7).
4. **FPS, RTT, jitter ghi ở client** vào file cục bộ, không thêm packet mới (không đụng `Backend/Shared/Protocol`).
5. **Làm song song bằng event giả:** bộ phát event giả, bộ kiểm schema và bộ báo cáo xong trước khi gameplay đầy đủ. Báo cáo ghi rõ mỗi loại event đến từ nguồn thật hay giả.

## 2. Phạm vi và ranh giới

**Trong phạm vi:**

1. **Schema 18 loại bản ghi** (mục 2.3–2.4): trường chung (match ID, tick/thời gian, build, config version) và trường riêng; khóa chống trùng; tài liệu schema và bản máy đọc được.
2. **Bộ kiểm schema** và test với fixture đúng/sai.
3. **Sink ghi file**: JSONL, một file theo trận và theo nguồn, có giới hạn dung lượng, bật/tắt bằng cấu hình, **không chặn tick** (mục 2.6, 2.11).
4. **Phía client:** ghi `perf_client` (FPS, frame time, RTT, jitter) và các bản ghi hành vi của chính client vào file cục bộ.
5. **Phía server, không sửa mã của Vinh:** bộ chuyển log `[metrics]` thành `perf_server`; **đề xuất hook** bằng tài liệu để Vinh duyệt (mục 2.7).
6. **Gộp, chống trùng/mất, báo cáo:** công cụ gộp file nhiều nguồn theo `matchId`, báo trùng/thiếu/xung đột; sinh CSV và báo cáo Markdown (mục 2.9).
7. **Bộ phát event giả** có tiêm lỗi (trùng, thiếu, đảo thứ tự, reconnect, xung đột) để chạy song song (mục 2.10).
8. **Hướng dẫn cho QA-01**: bật, thu file, chạy báo cáo, so hai phiên bản config.
9. **Phép đo ngân sách tick** trước/sau khi bật telemetry (mục 2.11).

**Ngoài phạm vi (thuộc ticket khác):**

- Quyết định kết quả gameplay: sát thương, TTK mục tiêu, luật bo, luật loot → WPN-02, WPN-03, ITM-01, ZON-01, SRV-03.
- Lưu kết quả sau trận, thứ hạng, lịch sử, bảng xếp hạng, Mongo/API → NET-02. TEL-01 không tính lại thứ hạng và không ghi vào Mongo.
- Quyết định chỉnh cân bằng, kết luận chất lượng → QA-01. Báo cáo TEL-01 **không có kết luận đạt/không đạt về TTK** vì GDD chưa có mục tiêu TTK (M5).
- Tạo tải 10 người giả để đo tải mạng → BOT-01. TEL-01 chỉ cung cấp số đo `perf_server` dùng chung.
- Thêm packet vào Wire Protocol, đổi `EventPacket` → CLI-02 (Vũ) và G2. Ticket này không đổi protocol.
- Killfeed `41:59` trên HUD **không** thuộc TEL-01 (UI-01 Q7 đã ghi nhầm gán): đó là giao diện, không phải telemetry.
- Dashboard trực tiếp, đẩy telemetry lên AWS/Mongo, phân tích thống kê nâng cao.
- Gắn lời gọi ghi event vào mã của WPN-02, ITM-01, WPN-03, ZON-01: mỗi chủ ticket tự thêm một dòng gọi sink tại điểm quyết định của mình, theo hợp đồng ở mục 2.7.

**Vùng file (đề xuất, theo `Docs/Agent_Coordination.vi.md`):**

| Việc | Vùng của TEL-01 | Ghi chú |
| --- | --- | --- |
| Công cụ gộp, báo cáo, event giả, bộ kiểm schema | `Tools/Telemetry/**` (Python stdlib, chạy `python -I`) | Mới, cùng kiểu `Tools/Audio/**` |
| Schema và hướng dẫn | `Docs/Telemetry/**`, `Docs/Tickets/TEL-01*` | Mới |
| Báo cáo mẫu và kết quả đo | `Docs/Reports/TEL-01_<ngày>/**` | Mới |
| Phía client | `Assets/_JungleMaze/Scripts/**/Telemetry/**`, `Assets/_JungleMaze/Tests/**/Telemetry/**` | Thư mục mới; chỉ thêm điểm nối tối thiểu vào code của người khác, Plan phải liệt kê từng điểm |
| Hook server | **Không sửa.** Chỉ tài liệu đề xuất cho Vinh | `Backend/GameServer/**` là của Vinh |
| Đọc, không sửa | `Backend/Shared/Protocol/**`, `Assets/StreamingAssets/JungleMaze/balance/**`, GDD xlsx, `Backend/Shared/GameData/**` | |

Thư mục này chưa có dòng trong mục 4 của `Agent_Coordination.vi.md`; CPO cần thêm một dòng trước khi build (R9).

**Số liệu từ GDD** (ô/dòng; "giả định" nếu GDD chưa có):

| Tham số | Giá trị | Nguồn |
| --- | --- | --- |
| Tick server | 30 Hz; mục tiêu tick p95 < 33,3 ms dưới tải gameplay | TEST & BALANCE STT 8; QA-01 |
| FPS | avg ≥ 55, 1% low ≥ 40 với 10 người trên máy ghi cấu hình | TEST & BALANCE STT 8; QA-01 |
| Quy mô | tối đa 10 người/phòng; trận baseline 7 phút (420 s) | TEST & BALANCE, `match_settings.json` |
| Bo | 5 phase, 5 HP/s ngoài bo, bán kính 25→18→12→7→3→0 | `Network_Architecture.vi.md` mục 6 |
| Dữ liệu cần thu | trận, chơi lại, đồ nhận/dùng, giao tranh/rút, nguyên nhân chết, lỗi mạng; "chỉ thêm event khi cần trả lời câu hỏi thiết kế" | PLAYTEST STT 2 |
| Loot | thời gian có trang bị đầu tiên, đồ nhận/dùng, tình huống thiếu tài nguyên | TEST & BALANCE STT 4 |
| Pacing | chạm trán, rút lui, thời lượng, nguyên nhân chết | TEST & BALANCE STT 5 |
| Mạng | mất mạng, timeout, về menu/join lại; HP/loot nhất quán | TEST & BALANCE STT 7 |
| TTK | chưa được định nghĩa: bảng cũ chỉ HP/DPS | BALANCE STT 1 |
| Hiệu năng cần ghi | build, cấu hình máy, số entity, FPS/frame time, CPU/RAM, băng thông, RTT/jitter/loss, tick p95/p99, VPN direct/relay | TEST & BALANCE STT 8 |
| Mục tiêu TTK, ngưỡng "đủ mẫu", cửa sổ giao tranh | **không có** trong GDD | giả định G-4, G-5 |

### 2.1. Hiện trạng trong repo (đọc ngày 27/10/2026)

| Thành phần | Có gì | Dùng được cho TEL-01 | Nguồn |
| --- | --- | --- | --- |
| `GameEventType` / `EventPacket` | 6 loại (`MatchStarted`, `MatchEnded`, `PlayerSpawned`, `PlayerHit`, `PlayerDied`, `ItemPickedUp`); trường `Tick`, `Source/TargetNetId`, 2 int. **Server chưa phát**, chỉ test đăng ký nhận | **Không.** Đây là event server→client, không có match ID, vũ khí, nguyên nhân | `Backend/Shared/Protocol/Gameplay/EventPacket.cs` |
| `MatchRuntime` | Có `MatchId`, `CurrentTick`, các event public `MatchCompleted`, `PlayerDropped`, `GraceExpired`, `ResumeTokenRotated`, `SnapshotGenerated(byte[])`, `ReliableBroadcast(byte[])`. Frame phát đi chứa phase (kèm `ConfigVersion`, `ConfigHash`), roster, spawn/eliminated, kết quả, snapshot 30 Hz và `ZoneStatePacket` (PhaseIndex, InWarning) | **Có, không cần sửa:** bắt các event public này để suy ra match_start/end, spawn, elimination, zone_phase, position_sample | `MatchRuntime.cs` |
| `IHealthService` | Event `EntityDamaged(netId, damage, sourceNetId)` và `EntityDied(netId, sourceNetId)`; nguồn `255` = bo | Có cho damage/nguyên nhân chết ở mức thô. **`HealthService` bị tạo lại mỗi trận** (`TickCleanup`), nên subscription phải gắn lại mỗi trận (R3) | `HealthService.cs`, `MatchRuntime.cs:588` |
| Chết do mất kết nối | `ApplyDamage(netId, 255, chính netId)` rồi `GraceExpired`: nguồn = chính nạn nhân, **không phải 255** | Phân biệt được với bo nhờ `GraceExpired` cùng tick (M8) | `MatchRuntime.cs:458–462` |
| Mất mạng trong trận | `MarkDisconnected` đặt cờ, **không phát event**; snapshot không mang cờ này. Resume phát `ResumeTokenRotated` | **Thiếu** điểm bắt DROPPED, cần hook nhỏ của Vinh (H5) | `MatchRuntime.cs:271`, `:326–351` |
| `Kills` | luôn 0 (chưa có combat) | Chưa có người hạ thật | `MatchRuntime.cs:672` |
| `ServerMetrics` | In `[metrics] … tickP95Ms tickP99Ms loopP95Ms loopP99Ms ticksPerSecond cpuOneCorePercent tickOverruns droppedTicks workingSetMiB rooms roomSlots utc=…` mỗi `MetricsIntervalSeconds` (1–60 s). **Theo cả tiến trình, không theo trận** | **Có, không cần sửa:** chuyển dòng log thành `perf_server` | `ServerMetrics.cs` |
| `MatchOutbox` | Mẫu đã có: `Channel` có giới hạn, `TryEnqueue` không chặn, ghi đĩa ngoài tick, trần số file/byte, idle khi thiếu cấu hình | **Mẫu để theo** cho sink telemetry | `MatchOutbox.cs`, `Program.cs:36–52` |
| Client | `MovementTelemetry` (ITickable) ghi dòng `[Mov]` mỗi 5 s: transport, RTT p50/p95, prediction/interpolation. `INetworkClient.Stats` có transport, P50/P95 RTT. **Chưa có** FPS, loss | Nguồn RTT sẵn có; FPS cần mới | `MovementTelemetry.cs` |
| Config version | Server: `match_settings.json` `version` (hiện `1.0.0`) và `ConfigHash` = SHA-256 của map+match+zone. Client: `balance/weapons.json`, `equipments.json` là mảng trần, **không có trường version** | Dùng `version` + hash (M6) | `MatchProfile.cs`, `GameDataLoader.cs` |
| Vũ khí/vật phẩm | 18 vũ khí (`HAND` … `SAHUR_BAT`) và 16 trang bị (`WOODEN_HELMET` … `COMPASS`) có Code ID; **chưa có** server combat, inventory, loot, bụi, hazard | ID lấy từ GDD; hệ thống chưa tồn tại nên event liên quan là **fake-only** tới khi có (mục 2.4 cột Nguồn thật) | Weapons, Equipments |

### 2.2. Truy vết: 18 loại bản ghi trả lời câu hỏi nào của GDD

| Nội dung ticket / GDD | Loại bản ghi (số trong bảng 2.4) |
| --- | --- |
| TTK theo vũ khí (BALANCE STT 1; ticket) | 4 `attack`, 5 `damage`, 6 `elimination` |
| Nguồn gây chết, chết do bo (ticket; AC2) | 5, 6, 8 `zone_phase` |
| Khống chế / CC (BALANCE STT 2; AC2) | 7 `cc_span` |
| Loot, đồ nhận/dùng, thời gian có trang bị đầu tiên, thiếu tài nguyên (TEST STT 4; PLAYTEST STT 2) | 9 `loot_drop`, 10 `item_pickup`, 12 `equip_change` |
| Thời gian dùng vật phẩm (ticket) | 11 `item_use` |
| Hành vi ẩn nấp, chạm trán, rút lui, điểm nóng (ticket; TEST STT 5) | 13 `hide_span`, 14 `position_sample` (+ suy ra từ 5) |
| Vòng đời trận, thời lượng, chơi lại (TEST STT 5–6; PLAYTEST STT 2) | 1 `match_start`, 2 `match_end`, 3 `player_spawn`, 16 `replay_choice` |
| Lỗi mạng, mất mạng, join lại (TEST STT 7; PLAYTEST STT 2) | 15 `session_state` |
| Hiệu năng: FPS, tick p95/p99, RTT/jitter/loss, CPU/RAM (TEST STT 8) | 17 `perf_server`, 18 `perf_client` |

Không có loại nào ngoài các câu hỏi trên (PLAYTEST STT 2: "chỉ thêm event khi cần trả lời câu hỏi thiết kế"). Không tạo bảng default config thứ hai: bản ghi chỉ mang **ID và hash** của config, không chép giá trị.

### 2.3. Trường chung của mọi bản ghi (envelope)

| Trường | Kiểu | Ý nghĩa |
| --- | --- | --- |
| `v` | int | Phiên bản schema, hiện `1` |
| `type` | string | Một trong 18 tên ở bảng 2.4 |
| `matchId` | GUID | `MatchId` của server (có từ Waiting, đổi mỗi trận). **Ngoại lệ duy nhất:** `perf_server` là số đo cả tiến trình, `matchId = null`, gộp vào trận theo khoảng UTC |
| `roomId` | string | `RoomId` của server. **Không** ghi `RoomCode` (RoomCode là bí mật vào phòng) |
| `src` | `server` \| `client` | Nguồn ghi |
| `wid` | 8 hex | Mã ngẫu nhiên của lần chạy bộ ghi (mỗi tiến trình một mã) |
| `seq` | int ≥ 1 | Số thứ tự tăng dần theo (`matchId`, `src`, `wid`); không bao giờ đặt lại trong cùng một `wid` |
| `tick` | uint | `CurrentTick` của server, cùng đơn vị `ServerTick` trong packet. Client ghi tick của snapshot mới nhất nhận được |
| `mt` | uint \| null | Tick tính từ lúc InGame bắt đầu (0 = bắt đầu); `null` trước InGame |
| `utc` | int64 | Mili-giây từ epoch UTC của máy ghi (so sánh giữa hai máy chỉ ở mức giây vì lệch đồng hồ; thứ tự dùng `tick`) |
| `build` | string ≤ 32 | Bản build của bên ghi (server/client); giá trị `dev` nếu chưa có nguồn build version (BLD-01) |
| `cfg` | string | `ConfigVersion` của trận (hiện `1.0.0`) |
| `cfgHash` | 64 hex | `ConfigHash` của dữ liệu server (map, match, zone) |
| `balHash` | 64 hex \| null | SHA-256 của `balance/weapons.json` + `equipments.json` + `match.json` phía client; `null` ở server cho tới khi WPN-02 đưa balance lên server (khi đó gộp vào `cfgHash`) |
| `fake` | true (chỉ khi giả) | Đánh dấu dữ liệu từ bộ phát giả; báo cáo tách riêng |

**Khóa config của báo cáo:** `cfgKey = cfg + "/" + 8 ký tự đầu cfgHash + "/" + (8 ký tự đầu balHash hoặc "-")`. Mọi phép so "cùng config" dùng `cfgKey`, không bao giờ trộn hai `cfgKey`.

**Không bao giờ ghi:** tên hiển thị, `RoomCode`, JWT, ticket, credential Guest, `ProfileKey`, địa chỉ IP. Định danh người chơi trong trận là `netId` (0–9); `PlayerSessionId` chỉ nằm trong bản ghi `player_spawn` để khớp file client với server.

### 2.4. 18 loại bản ghi

Cột "Nguồn thật": **S0** = bắt được ngay từ server hiện tại không cần sửa mã (qua event public hoặc log); **S+** = cần hook nhỏ của Vinh (mục 2.7); **W** = cần hệ thống chưa có (ticket ghi trong ngoặc), tới lúc đó chỉ là **fake-only**; **C** = ghi ở client. Mã nguyên nhân: `cause` ∈ `WEAPON`, `ZONE`, `HAZARD`, `DISCONNECT`, `OTHER`. ID vũ khí/trang bị là **Code ID của GDD**. Các ID GDD chưa đặt (đạn, vật chứa, hazard, bụi) dùng ID đề xuất ở G-6.

| # | `type` | Khi phát | Trường riêng (ngoài envelope) | Khóa chống trùng (ngoài `wid`+`seq`) | Nguồn thật |
| --- | --- | --- | --- | --- | --- |
| 1 | `match_start` | Chuyển sang InGame | `profile` (`main`/`test`), `players`, `minPlayers`, `seedHash` (hash của MatchId dùng làm seed bo) | một lần mỗi `matchId` | S0 |
| 2 | `match_end` | `MatchCompleted` | `reason` (`LastAlive`/`Tie`/`Timeout`/`Cancelled`), `winnerNetId` (null nếu hòa), `durTicks`, `alive` | một lần mỗi `matchId` | S0 |
| 3 | `player_spawn` | Spawn đầu trận | `netId`, `sess` (PlayerSessionId), `x`, `y`, `loadout` (mảng ID, rỗng nếu chưa có) | (`matchId`, `netId`) | S0; `loadout` W (ITM-01) |
| 4 | `attack` | Một lần đánh/bắn/ném | `netId`, `weapon`, `kind` (`MELEE`/`SHOT`/`THROW`/`SPECIAL`), `x`, `y`, `ammo` (null nếu không dùng đạn) | (`matchId`, `netId`, `tick`, `weapon`, `kind`) khi nội dung giống | W (WPN-02, WPN-03) |
| 5 | `damage` | Mỗi lần HP giảm | `victim`, `attacker` (null nếu không có), `cause`, `weapon`/`hazard`, `raw`, `applied`, `absorbed`, `hpAfter`, `helmetDur`, `armorDur` | (`matchId`, `tick`, `victim`, `attacker`, `weapon`/`hazard`) | S0 mức thô (`EntityDamaged`: nạn nhân, lượng, nguồn; bo = 255); đủ trường **W** (WPN-02) |
| 6 | `elimination` | Người chơi hết HP | `victim`, `killer` (null nếu bo/hazard/mất kết nối), `cause`, `weapon`/`hazard`, `rank` (bản sao từ server, **không tính lại**), `survTicks`, `x`, `y`, `zonePhase`, `hidden` (đang ẩn trong bụi?) | (`matchId`, `victim`) **một lần** | S0 (`Eliminated` + `EntityDied` + `GraceExpired`); `killer`, `weapon` W |
| 7 | `cc_span` | Khi một khống chế **kết thúc** (hết giờ, bị hóa giải, chết, hết trận) | `victim`, `source` (netId), `by` (Code ID), `ccKind` (`STUN`/`ROOT`/`REVERSE`), `startTick`, `endTick`, `plannedMs`, `end` (`EXPIRED`/`CLEANSED`/`DEATH`/`MATCH_END`) | (`matchId`, `victim`, `startTick`, `ccKind`) | W (WPN-02/03, ITM-01) |
| 8 | `zone_phase` | Đổi trạng thái bo | `phase` (0–4, `-1` trước phase 0), `state` (`WARNING`/`SHRINK_START`/`SHRINK_END`/`FINAL`), `cx`, `cy`, `r`, `targetR` | (`matchId`, `phase`, `state`) **một lần** | S0 (suy từ `ZoneStatePacket` trong snapshot) |
| 9 | `loot_drop` | Vật chứa vỡ hoặc airdrop đáp | `container` (`CHEST_WOODEN`/`POT_CLAY`/`AIRDROP_CRATE`/`CORPSE`), `by` (netId phá, null nếu airdrop), `items` (≤ 8 phần tử `{id, qty}`), `x`, `y` | (`matchId`, `container`, `x`, `y`, `tick`) | W (ITM-01, ECONOMIC) |
| 10 | `item_pickup` | Nhặt một món | `netId`, `item`, `qty`, `from` (`CONTAINER`/`FLOOR`/`CORPSE`/`AIRDROP`), `x`, `y` | (`matchId`, `netId`, `tick`, `item`) | W (ITM-01) |
| 11 | `item_use` | Khi lượt dùng **kết thúc** | `netId`, `item`, `startTick`, `endTick`, `plannedMs` (từ config `useSeconds`), `result` (`DONE`/`CANCELLED`/`INTERRUPTED`), `hpBefore`, `hpAfter` | (`matchId`, `netId`, `startTick`, `item`) | W (ITM-01) |
| 12 | `equip_change` | Đổi trang bị | `netId`, `action` (`EQUIP`/`UNEQUIP`/`DROP`/`BREAK`/`SWAP`), `slot` (`WEAPON`/`HELMET`/`ARMOR`), `item`, `from` (món trước, null nếu trống) | (`matchId`, `netId`, `tick`, `slot`, `action`) | W (ITM-01, WPN-02) |
| 13 | `hide_span` | Khi một đợt ẩn trong bụi **kết thúc** (ra khỏi bụi, chết, hết trận) | `netId`, `startTick`, `endTick`, `bush` (ID/vị trí bụi), `x`, `y`, `end` (`EXIT`/`DEATH`/`MATCH_END`), `attackedWhileHidden` (bool) | (`matchId`, `netId`, `startTick`) | W (luật bụi chưa có hệ thống; Obstacles STT 9) |
| 14 | `position_sample` | Mỗi `sampleSec` giây (mặc định 1) cho mỗi người còn sống | `netId`, `x`, `y`, `hp`, `weapon`, `inZone` | (`matchId`, `netId`, `tick`) | S0 (suy từ snapshot, lấy 1 trong 30 frame) |
| 15 | `session_state` | Đổi trạng thái kết nối của người chơi trong trận | `netId`, `kind` (`DROPPED`/`RESUMED`/`GRACE_EXPIRED`/`LEFT`/`LOAD_TIMEOUT`), `transport` (`UDP`/`WSS`/null), `graceMs` | (`matchId`, `netId`, `kind`, `tick`) | S0 cho `RESUMED`, `GRACE_EXPIRED`, `LOAD_TIMEOUT`; **S+** cho `DROPPED`, `LEFT` (H5) |
| 16 | `replay_choice` | Hết pha Ending cho mỗi người | `netId`, `choice` (`REPLAY`/`TIMEOUT`/`DISCONNECTED`), `afterMs` | (`matchId`, `netId`) **một lần** | S+ (`ProcessReplayAck`, H6) |
| 17 | `perf_server` | Mỗi dòng `[metrics]` | `windowSec`, `ticksPerSecond`, `tickP95Ms`, `tickP99Ms`, `loopP95Ms`, `loopP99Ms`, `tickOverruns`, `droppedTicks`, `cpuOneCorePercent`, `workingSetMiB`, `peers`, `active`, `rooms` | (`utc` của dòng log) | S0 (đọc log, không sửa server) |
| 18 | `perf_client` | Mỗi 5 s trong InGame | `netId`, `windowSec`, `frames`, `fpsAvg`, `fps1Low`, `frameMsP95`, `frameMsMax`, `transport`, `rttP50`, `rttP95`, `jitterMs`, `lossPct` (null nếu chưa có nguồn), `entities` | (`matchId`, `netId`, `wid`, `seq`) | C |

Bản ghi phụ (không tính trong 18): dòng đầu `_header` (schema, `wid`, `src`, `build`, `cfgKey`, thời điểm mở) và dòng cuối `_trailer` (số dòng đã ghi, số bị bỏ vì hàng đầy, `truncated`). Chúng giúp phát hiện mất dữ liệu (AC10).

### 2.5. Định nghĩa số đo (để báo cáo ra cùng một con số)

Các định nghĩa này là **định nghĩa đo**, không phải mục tiêu cân bằng. QA-01 hoặc người thiết kế duyệt lại ở G1 (G-4, G-5).

- **Giao tranh của V với K:** chuỗi `damage` có `attacker = K`, `victim = V`, `cause = WEAPON`, hai lần liên tiếp cách nhau ≤ `gapSec` (mặc định **5 s = 150 tick**), kết thúc bằng `elimination` của V với `killer = K`.
- **TTK(V, K)** = (tick của `elimination` − tick của `damage` đầu chuỗi) / 30 giây. Chuỗi chỉ gồm một đòn trùng tick với kết liễu thì TTK = 0 và ghi `oneShot`. Gán cho `weapon` của đòn kết liễu.
- **hitsToKill** = số `damage` trong chuỗi. **Không** vào TTK: kết liễu do `ZONE`, `HAZARD`, `DISCONNECT` hoặc `killer = null`; chúng vào bảng nguyên nhân chết.
- **Tỷ lệ chết vì bo** = số `elimination` có `cause = ZONE` / tổng `elimination`, theo `cfgKey`; kèm phân bố theo `zonePhase` và tỷ lệ người chơi bị ít nhất một `damage` `ZONE`.
- **Thời gian có trang bị đầu tiên** = (tick của `equip_change` `EQUIP` đầu tiên có `slot = WEAPON` khác `HAND`, hoặc `slot` thuộc `HELMET`/`ARMOR`, trừ tick `player_spawn`) / 30 giây. Người không bao giờ có: ghi "không có", không tính vào trung vị.
- **Thiếu tài nguyên** = bị loại khi chỉ có `HAND` và không có mũ/giáp (suy từ `equip_change` đến tick chết).
- **Chênh lệch thời gian dùng** = (`endTick − startTick`) × (1000/30) ms so với `plannedMs`; báo trung bình và tỷ lệ `CANCELLED`/`INTERRUPTED`.
- **CC:** số lần, tổng giây CC trên mỗi người mỗi trận, theo `ccKind` và `by`, tỷ lệ kết thúc sớm (`CLEANSED`).
- **Ẩn nấp:** số đợt, độ dài trung bình, tỷ lệ người từng ẩn, số lần bị loại khi đang ẩn.
- **FPS 1% low:** FPS trung bình của 1% khung chậm nhất trong cửa sổ 5 s (GDD không định nghĩa, G-7).
- **Mẫu thấp:** nhóm có n < 10 được gắn "mẫu thấp", không so sánh kết luận (G-5).

### 2.6. Quyết định: bắt ở đâu, ghi ở đâu

| Lớp | Bắt ở đâu | Ghi ở đâu | Sửa mã của Vinh? |
| --- | --- | --- | --- |
| Quyền uy (damage, elimination, bo, loot, CC, spawn) | **Server**, vì chỉ server biết nguồn và kết quả đúng; client chỉ thấy snapshot | JSONL phía server `${TELEMETRY_PATH}/<ngày>/<matchId>.server.jsonl` | Không cho phần S0. Phần S+ và W xem mục 2.7 |
| Hiệu năng server | Log `[metrics]` của tiến trình | Bộ chuyển log → `perf_server` trong cùng thư mục | **Không** |
| FPS, frame time, RTT, jitter, loss, hành vi riêng client | **Client** | JSONL cục bộ `persistentDataPath/Telemetry/<matchId>.client.<netId>.jsonl` | Không |
| Gộp, chống trùng, báo cáo | Ngoại tuyến (Python) | `Docs/Reports/TEL-01_<ngày>/` | Không |

Lý do không chỉ làm ở client: client không biết vũ khí và nguồn sát thương thật, và dữ liệu client bị trùng khi reconnect. Lý do không gửi telemetry client lên server: cần packet mới (G2, CLI-02) và đưa việc vào đường tick, trái yêu cầu "không làm nghẽn tick". File cục bộ do QA thu về sau buổi chơi (Q4).

**Sink phía server** (nếu Vinh duyệt) theo mẫu `MatchOutbox`: hàng đợi có giới hạn, ghi không chặn (`TryWrite` trả false khi đầy và đếm số bỏ), một luồng nền ghi đĩa, trần dung lượng theo trận, tự tắt khi lỗi đĩa. **Mặc định tắt** khi chưa đặt `TELEMETRY_PATH` (giống outbox tắt khi thiếu token), nên chơi không đổi gì.

### 2.7. Hook tối thiểu đề xuất cho Vinh (không tự sửa)

**Hợp đồng chung:** một cổng ghi `ITelemetrySink` với đúng một thao tác `TryWrite(record)` **không chặn**, không cấp phát trên đường nóng, không I/O, không khóa, không ném ngoại lệ; mặc định là sink rỗng. Bản ghi là struct; chuỗi chỉ là tham chiếu tới chuỗi sẵn có (ID từ config). Tiền xử lý, tuần tự hóa JSON và ghi file chạy hoàn toàn ở luồng nền.

| Hook | Vị trí (Vinh duyệt) | Cho loại bản ghi | Trạng thái |
| --- | --- | --- | --- |
| H1 | `Program.cs`: tạo sink khi có `TELEMETRY_PATH`, gắn vào từng phòng giống `HookRoomMatch` của outbox, giải phóng cuối tiến trình | tất cả | thêm vài dòng, giống outbox |
| H2 | Bộ quan sát trong sink đăng ký các event public của `room.Match`: `ReliableBroadcast`, `SnapshotGenerated`, `MatchCompleted`, `PlayerDropped`, `GraceExpired`, `ResumeTokenRotated`. Trình xử lý **chỉ chép tham chiếu `byte[]` và `CurrentTick` vào hàng đợi**; giải mã bằng `PacketRegistry` ở luồng nền. `position_sample` lấy 1 trong 30 frame ngay tại trình xử lý | 1, 2, 3, 6, 8, 14, 15 (một phần) | **S0**, không sửa `MatchRuntime` |
| H3 | Đăng ký `Match.Health.EntityDamaged/EntityDied`; **gắn lại khi `MatchId` đổi** vì `HealthService` được tạo lại mỗi trận | 5, 6 (mức thô) | S0, rủi ro R3 |
| H4 | Log `[metrics]` đọc bởi bộ chuyển ngoài tiến trình | 17 | S0, không sửa |
| H5 | **Cần Vinh thêm:** một event nhỏ `PlayerDisconnected(sessionId)` (và `PlayerLeft`) trong `MarkDisconnected`/`ProcessLeave`, vì hiện không có tín hiệu nào ra ngoài | 15 `DROPPED`, `LEFT` | S+ |
| H6 | **Cần Vinh thêm:** một event khi `ProcessReplayAck` thành công | 16 | S+ |
| H7 | **Chủ ticket tự thêm** khi làm hệ thống của mình: một dòng `sink.TryWrite(...)` tại điểm quyết định (WPN-02: 4, 5, 6, 7; ITM-01: 9–12; WPN-03: 4, 7; hệ thống bụi: 13) | 4, 5, 7, 9–13 | W |

Quy ước vận hành: Phase A (không cần Vinh) là schema + sink + client + `perf_server` + event giả + báo cáo; Phase B là H1–H6 sau khi Vinh duyệt; Phase C là H7 do chủ ticket thêm. Ticket có thể nghiệm thu Phase A trước hạn; các AC phụ thuộc Vinh/chủ ticket gắn nhãn điều kiện (mục 3.7).

### 2.8. Chống trùng và mất, kể cả khi reconnect

- **Khóa kỹ thuật:** (`matchId`, `src`, `wid`, `seq`). Cùng khóa, cùng nội dung → giữ một, đếm `dup`. Cùng khóa, khác nội dung → **xung đột**: không gộp im lặng, đưa vào danh sách xung đột, báo cáo cờ lỗi.
- **Khóa ngữ nghĩa:** cột "Khóa chống trùng" bảng 2.4. Cùng khóa ngữ nghĩa nhưng `seq` khác (hệ thống báo hai lần) → bản đầu giữ, bản sau đếm `semantic_dup`. Loại "một lần": `match_start`, `match_end`, `player_spawn`, `elimination`, `zone_phase`, `replay_choice`.
- **Reconnect:** server chỉ phát khi trạng thái đổi, nên resume **không phát lại** `match_start`, `player_spawn`. Client tiếp tục cùng `wid` và `seq` khi nối lại cùng tiến trình; khởi động lại tiến trình tạo `wid` mới, không đè dòng cũ (file mở chế độ ghi nối). Resume xuất hiện là một bản ghi `session_state` `RESUMED` riêng.
- **Không mất:** mỗi (`matchId`, `src`, `wid`) có `seq` liên tục từ 1 tới N; chỗ hở được báo cáo kèm số thiếu. `_trailer` ghi số đã ghi và số bị bỏ (hàng đầy, đĩa lỗi); báo cáo đối chiếu với số dòng thật. Bỏ dữ liệu khi quá tải được **chấp nhận nhưng phải thấy** (đếm và báo), không bao giờ chặn tick.
- **Thứ tự:** gộp sắp theo (`tick`, `src`, `seq`); không dựa vào `utc` giữa hai máy.

### 2.9. Định dạng đầu ra và báo cáo mẫu

**File gốc:** UTF-8, LF, một đối tượng JSON mỗi dòng; mỗi (trận, nguồn, `wid`) một file; dòng đầu `_header`, dòng cuối `_trailer`. Ước lượng: 10 người × 420 s, `position_sample` 1 Hz ≈ 4 200 dòng, các loại khác vài nghìn dòng, tổng ≲ 2 MB mỗi trận. Trần đề xuất: 8 MiB mỗi file, 256 MiB tổng; vượt trần thì dừng ghi và ghi `truncated: true` ở `_trailer` (G-8).

**Gộp và báo cáo** (Python stdlib, `python -I Tools/Telemetry/...`; tên lệnh do Plan quyết):

- `events.jsonl` đã gộp, chống trùng; `conflicts.jsonl`; `report.md`.
- CSV mỗi bảng: `ttk_by_weapon.csv`, `death_causes.csv`, `zone_deaths_by_phase.csv`, `loot_by_cfg.csv`, `cc_by_cfg.csv`, `item_use.csv`, `hide.csv`, `perf.csv`, `coverage.csv`.
- `compare` cho hai `cfgKey`: cột A, cột B, chênh lệch và `n` mỗi nhóm; nhóm mẫu thấp gắn nhãn.

**Mẫu báo cáo** (số **giả** để minh họa cấu trúc, không phải dữ liệu thật):

```text
# Báo cáo telemetry  run=FAKE-seed1  matches=6  cfgKey=1.0.0/3fa1c2d0/9b04e17a
Kiểm: events=48 213 dup=212 semantic_dup=6 conflicts=0 gaps=0 dropped(trailer)=0 fake=100%

## TTK theo vũ khí (giây; giao tranh kết thúc bằng hạ gục, gap 5 s)
weapon      kills  median  p90   hitsToKill  ghi chú
KNIFE          14   1.9    3.4     4.1
AXE             9   1.3    2.8     2.6
CROSSBOW        5   0.7    1.6     1.5       mẫu thấp (n<10)

## Nguyên nhân chết
cause       n    %      theo phase bo (0/1/2/3/4)
WEAPON      31   64.6%
ZONE        15   31.3%  1 / 3 / 6 / 4 / 1
DISCONNECT   2    4.2%
Tỷ lệ chết vì bo = 31.3%; người chơi bị ≥1 damage bo = 71.7%

## Loot và CC theo cfgKey (mỗi người, mỗi trận)
món nhặt: 6.2 | có trang bị đầu tiên (trung vị): 38 s | chết khi chỉ có HAND: 4/60
CC: STUN 0.8 lần / 2.4 s; ROOT 0.3 lần / 0.5 s; kết thúc sớm (CLEANSED) 12%

## Hiệu năng
tick p95 = 4.1 ms (mục tiêu < 33.3)   p99 = 7.9 ms   droppedTicks = 0
FPS avg 61 (mục tiêu ≥ 55)   1% low 44 (≥ 40)   RTT p95 38 ms

## Nguồn dữ liệu theo loại: coverage.csv (real / fake mỗi loại)
```

### 2.10. Làm song song bằng event giả

Bộ phát giả dựng trận 10 người xác định theo `seed` với **cả 18 loại**, hai bản config (A, B, khác `cfgHash`) có chủ ý TTK khác nhau để thử `compare`, và các cờ tiêm lỗi: `--dup` (bản ghi trùng), `--gap` (mất k dòng), `--reorder`, `--conflict`, `--reconnect` (client rớt và nối lại trong grace). Mọi bản ghi giả mang `"fake": true`. Báo cáo in tỷ lệ giả/thật mỗi loại và có chế độ `--require-real` từ chối coi dữ liệu giả là thật. Nhờ vậy báo cáo, schema và hướng dẫn QA-01 xong trước WPN-02/ITM-01.

### 2.11. Ngân sách tick (không làm chậm 30 Hz)

Hợp đồng sink (2.7) là biện pháp chính. Cách đo, theo thứ tự rẻ đến đắt:

1. **Vi đo hook:** gọi `TryWrite` 1 triệu lần với bản ghi mẫu: p99 ≤ **10 µs**, **0 byte** cấp phát sau khởi động, không khóa, không I/O.
2. **Tải sự kiện giả vào server thật cục bộ:** trận một phòng 10 phiên, 60 s, bật và tắt telemetry, mỗi cấu hình 3 lần; hai mức: thực tế (~70 bản ghi/s: 10 người × tối đa 3 đòn/s + 3 hit/s + mẫu vị trí) và căng (1 000 bản ghi/s). So sánh `tickP95Ms` do chính `ServerMetrics` in ra: chênh ≤ **0,5 ms**, `tickP95Ms` < 33,3 ms, `tickP99Ms` được ghi, `droppedTicks` = 0, `tickOverruns` không hơn cấu hình tắt.
3. **Lỗi đĩa:** thư mục mất/chỉ đọc/đầy: sink tự tắt, một cảnh báo, tick không đổi.
4. **Client:** 0 byte cấp phát mỗi khung trong trạng thái ổn định (đo bằng test), không I/O ở luồng chính, chênh FPS ≤ 1 hoặc frame time p95 ≤ 0,3 ms trên máy QA.

Tải 10 người **người thật** chưa có (BOT-01, WPN-02); số đo cục bộ là tải giả, phải ghi là "tải giả" khi báo cáo (Network_Architecture mục 8: không suy diễn thành sức chứa gameplay).

### 2.12. Mâu thuẫn và khoảng trống giữa GDD, code và ticket

| # | Phát hiện | Xử lý trong spec |
| --- | --- | --- |
| M1 | Ticket nói "18 event/metrics theo GDD" nhưng **không có sheet nào liệt kê 18**; TEST & BALANCE chỉ có câu hỏi và cách kiểm | Designer đề xuất 18 loại, truy vết ở 2.2; PIC xác nhận ở G1 (Q1) |
| M2 | Ref `3–18,21–22` là số dòng sheet, lệch +2 so với STT; công cụ bỏ dòng trống nên không xác minh được số dòng vật lý | Suy luận G-1; Ref không bao gồm PLAYTEST STT 3 (Report discipline) vì đó là quy ước, không phải dữ liệu |
| M3 | Ticket nói thu "về server hoặc file thống nhất" nhưng **FPS chỉ client biết**; đưa FPS lên server cần packet mới (G2, CLI-02) và đi qua tick | Chọn file cục bộ ở client (Q4) |
| M4 | AC2 yêu cầu "TTK theo vũ khí" nhưng server chưa có vũ khí, `Kills` = 0, `PlayerDied` chưa phát | Event giả cho tới WPN-02; mục 2.4 cột Nguồn thật; AC12–AC13 kiểm bằng giả |
| M5 | BALANCE STT 1 nói TTK **chưa định nghĩa**, bảng cũ chưa đạt; GDD không có mục tiêu TTK | Báo cáo chỉ đưa số theo định nghĩa đo (2.5), **không** có đạt/không đạt TTK |
| M6 | "Loot/CC cùng version config": client `balance/*.json` **không có trường version**; chỉ `manifest.json` có `version` 1.0.0 | Dùng hash nội dung (`balHash`) + `cfg` + `cfgHash` làm `cfgKey` (G-3); nhắc CLI-04 (Hào) nếu muốn nhãn version |
| M7 | "Chết do bo": R0 gây damage mọi nơi; Lava/stun giải đồ còn **chưa chốt** (BALANCE STT 3, 5) | `cause` ghi theo server quyết, không tự suy luật; bảng có cột `HAZARD` riêng |
| M8 | `HealthService` đặt nguồn `255` cho bo, nhưng chết do mất kết nối dùng nguồn = **chính nạn nhân**; `HealthService` bị tạo lại mỗi trận | Phân loại `DISCONNECT` nhờ `GraceExpired` cùng tick; gắn lại subscription theo `MatchId` (H3, R3) |
| M9 | ITEM LIST STT 11 "Đạn" không có Code ID; Obstacles không có Code ID cho vật chứa, hazard, bụi | ID đề xuất ở G-6, chờ khớp với ITM-01/CLI-04 (Q8) |
| M10 | `EventPacket` có 6 loại nhưng không đủ trường (không match ID, vũ khí, nguyên nhân) và server chưa phát | Không tái dùng, không đổi (ngoài phạm vi, CLI-02) |
| M11 | PROJECT TRACKING ghi SRV-03, ZON-01 "Not Yet" nhưng code server đã có vòng đời trận và bo (Network_Architecture mục 1) | Spec dựa trên code thực; trạng thái GDD là việc của người cập nhật |
| M12 | `[metrics]` là số đo **cả tiến trình** (nhiều phòng), không theo trận | `perf_server.matchId = null`, gắn vào trận theo khoảng UTC và `rooms`; chạy một phòng khi đo ngân sách |

## 3. Tiêu chí nghiệm thu

Ánh xạ tiêu chí GDD: **(1)** 18 event đúng schema, không mất/nhân bản khi reconnect/lặp → 3.1, 3.2. **(2)** báo cáo TTK theo vũ khí, tỷ lệ chết vì bo, loot/CC cùng config → 3.3, 3.4. **(3)** không nghẽn tick, QA-01 đọc và so sánh được → 3.5, 3.6. Ưu tiên P0/P1 làm trong hạn 31/10 (Phase A); tiêu chí gắn "điều kiện" phụ thuộc Vinh hoặc chủ ticket khác.

### 3.1. Schema và 18 loại bản ghi (GDD 1)

| # | Tiêu chí (đo được) | Cách kiểm | Ưu tiên |
| --- | --- | --- | --- |
| AC1 | Tài liệu schema và bản máy đọc được định nghĩa **đúng 18 loại** ở bảng 2.4, mỗi loại có trường bắt buộc, kiểu, miền giá trị và khóa chống trùng | Test: nạp schema, đếm đúng 18 loại, mỗi loại có fixture hợp lệ; kiểm bằng bộ kiểm chạy 18/18 fixture đạt | P0 |
| AC2 | **100%** dòng có đủ trường chung (mục 2.3: `v, type, matchId, roomId, src, wid, seq, tick, utc, build, cfg, cfgHash`; `mt`, `balHash` có thể null theo bảng). Ngoại lệ duy nhất: `perf_server.matchId = null` | Test: 18 fixture đủ trường đạt; mỗi trường thiếu một lần (≥ 12 fixture sai) đều bị bộ kiểm từ chối; chạy trên toàn bộ file giả: 0 dòng thiếu | P0 |
| AC3 | Mọi trường vũ khí/trang bị mang Code ID có thật trong `weapons.json`/`equipments.json`, hoặc ID đề xuất đã khai ở G-6; ID lạ bị đánh dấu `invalid_id` | Test: fixture `BAD_ID` bị đánh dấu; dữ liệu giả 0 `invalid_id` | P0 |
| AC4 | Không có tên hiển thị, `RoomCode`, JWT, ticket, `ProfileKey`, IP trong bất kỳ bản ghi nào | Test: tên hiển thị và RoomCode mẫu cấy vào trận giả/server thử; quét toàn bộ output tìm các chuỗi đó = 0 khớp; schema không có trường tên các loại này | P0 |
| AC5 | Mỗi loại giữ đúng quy tắc nguồn và `cause`/`kind`/`action` trong miền liệt kê; giá trị ngoài miền bị từ chối | Test: mỗi trường liệt kê có ≥ 1 fixture ngoài miền bị từ chối | P1 |

### 3.2. Không mất, không nhân bản (GDD 1)

| # | Tiêu chí (đo được) | Cách kiểm | Ưu tiên |
| --- | --- | --- | --- |
| AC6 | **Trùng kỹ thuật:** cùng (`matchId`,`src`,`wid`,`seq`) và nội dung giống → giữ đúng một, `dup` đếm đúng số tiêm | Test: dữ liệu giả có tiêm 5% dòng trùng; số bản ghi sau gộp = số của lần chạy sạch cùng seed; `dup` = số dòng đã tiêm | P0 |
| AC7 | **Xung đột:** cùng khóa kỹ thuật khác nội dung → không gộp, vào `conflicts.jsonl`, báo cáo cờ lỗi; chế độ `--strict` thoát mã ≠ 0 | Test: tiêm 3 xung đột, đếm đúng 3 và mã thoát | P0 |
| AC8 | **Trùng ngữ nghĩa:** loại "một lần" (`match_start`, `match_end`, `player_spawn`, `elimination`, `zone_phase`, `replay_choice`) báo hai lần với `seq` khác → chỉ tính bản đầu, `semantic_dup` đếm đúng | Test: tiêm 6 lần báo lặp, số `elimination` sau gộp = số người bị loại thật | P0 |
| AC9 | **Reconnect:** kịch bản giả "client rớt ở tick T, nối lại T+Δ trong grace (15 s)": **không** có `match_start`/`player_spawn` thứ hai; có đúng một `session_state` `RESUMED`; seq phía client liên tục sau khi nối lại; tổng số bản ghi bằng giá trị kỳ vọng của kịch bản | Test trên dữ liệu giả `--reconnect` | P0 |
| AC10 | **Không mất:** hở `seq` được báo kèm số thiếu; lần chạy sạch 0 hở; tiêm hở k dòng → báo đúng k; `_trailer.written` bằng số dòng thật của file | Test: tiêm k = 1, 7, 40 | P0 |
| AC11 | Đảo thứ tự dòng trong file không làm đổi kết quả gộp và báo cáo (sắp theo `tick`, `src`, `seq`) | Test: xáo ngẫu nhiên file giả; hai báo cáo giống nhau byte-theo-byte | P1 |

### 3.3. Báo cáo phân tích (GDD 2)

| # | Tiêu chí (đo được) | Cách kiểm | Ưu tiên |
| --- | --- | --- | --- |
| AC12 | Báo cáo gồm các mục ở mẫu 2.9: kiểm toàn vẹn, TTK theo vũ khí, nguyên nhân chết (kể cả tỷ lệ chết vì bo và theo phase), loot, CC, thời gian dùng vật phẩm, ẩn nấp, hiệu năng, độ phủ nguồn (real/fake) | Test: chạy trên bộ giả cố định (seed 1); đối chiếu **đúng từng con số** với file vàng (TTK trung vị mỗi vũ khí, % chết vì bo, số loot, số CC) | P0 |
| AC13 | TTK tính đúng định nghĩa 2.5 | Test đơn vị với giao tranh dựng tay, kết quả kỳ vọng ghi trước: một vũ khí; hai kẻ tấn công; khoảng nghỉ > 5 s tách chuỗi; đổi vũ khí giữa chuỗi; kết liễu bằng bo không vào TTK; one-shot = 0 s | P0 |
| AC14 | Tỷ lệ chết vì bo = `ZONE`/tổng `elimination`; phân bố theo `zonePhase` cộng đúng tổng | Test: bộ dựng tay 10 `elimination`, 3 do `ZONE`: 30,0% đúng; tổng các phase = 3 | P0 |
| AC15 | Loot, CC, thời gian có trang bị đầu tiên, thời gian dùng, ẩn nấp tính đúng 2.5 | Test đơn vị mỗi chỉ số với dữ liệu dựng tay | P1 |
| AC16 | Nhóm theo `cfgKey`, **không trộn** hai `cfgKey` (số mẫu mỗi nhóm = số của từng tập) | Test: gộp hai tập giả A/B; số trận và số bản ghi từng nhóm đúng | P0 |
| AC17 | `compare A B` in cột A, cột B, chênh lệch và `n`; nhóm n < 10 gắn "mẫu thấp" | Test: hai tập có TTK khác chủ ý; chênh lệch in đúng dấu và giá trị; nhãn mẫu thấp xuất hiện đúng nhóm | P0 |
| AC18 | Thiếu dữ liệu một phần (không có file client, không có `perf_server`) → mục tương ứng ghi "không có dữ liệu", chương trình thoát mã 0 | Test: bỏ lần lượt từng nguồn | P1 |
| AC19 | Hiệu năng gộp ≤ **10 s** cho 50 trận giả (khoảng 400 000 dòng) trên máy dev; ghi số đo | Đo thời gian; ghi cấu hình máy | P2 |

### 3.4. Event giả và chạy song song (GDD "làm song song")

| # | Tiêu chí (đo được) | Cách kiểm | Ưu tiên |
| --- | --- | --- | --- |
| AC20 | Bộ phát giả sinh **cả 18 loại**, mỗi loại ≥ 1 bản ghi mỗi trận; xác định: cùng `seed` cho file **giống từng byte** (so hash) | Test: chạy hai lần seed 1, hash bằng nhau; đếm 18 loại | P0 |
| AC21 | Các cờ tiêm lỗi (`--dup --gap --reorder --conflict --reconnect`) cho đúng kết quả AC6–AC11 | Test theo từng cờ | P0 |
| AC22 | 100% bản ghi giả có `"fake": true`; báo cáo in tỷ lệ real/fake mỗi loại; `--require-real` thoát mã ≠ 0 khi có giả | Test hai chế độ | P0 |
| AC23 | Hướng dẫn QA-01 có lệnh chép-dán cho từng bước (bật server, bật client, thu file, gộp, báo cáo, so hai config); người không phải tác giả chạy từ checkout sạch tới báo cáo trong ≤ 15 phút | Review + chạy lại trên checkout sạch; ghi thời gian | P1 |

### 3.5. Sink, hook và ranh giới (GDD 3)

| # | Tiêu chí (đo được) | Cách kiểm | Ưu tiên |
| --- | --- | --- | --- |
| AC24 | Sink: `TryWrite` không chặn; hàng đầy trả false, đếm bỏ, không ngoại lệ; mọi I/O và JSON ở luồng nền | Test: file đích bị chặn/chậm, 10 000 lần `TryWrite` xong trong thời gian đặt trước, luồng gọi không đợi; review xác nhận không I/O trong đường gọi | P0 |
| AC25 | Lỗi đĩa (thư mục mất, chỉ đọc, đầy): sink tự tắt, một cảnh báo, bộ đếm hiển thị, tick và trò chơi không đổi | Test giả lập lỗi từng loại | P0 |
| AC26 | File đúng định dạng 2.9: một file mỗi trận mỗi nguồn, JSONL UTF-8 LF, có `_header` và `_trailer`; trận mới (`MatchId` mới sau Cleanup) mở file mới; vượt trần dung lượng thì dừng và `truncated: true` | Test: ghi vượt trần giả; chạy hai trận liên tiếp | P0 |
| AC27 | Mặc định **tắt**: khi chưa cấu hình, không file nào được tạo và hành vi server/client không đổi; bộ test hiện có của backend (`run_all.cmd`) vẫn xanh | Chạy bộ test hiện có, quét thư mục dữ liệu | P0 |
| AC28 | Bộ chuyển log `[metrics]` → `perf_server` khớp từng trường với dòng log mẫu (`tickP95Ms`, `tickP99Ms`, `loopP95Ms`, `loopP99Ms`, `droppedTicks`, `tickOverruns`, `cpuOneCorePercent`, `workingSetMiB`); dòng hỏng bị bỏ và đếm, không làm dừng | Test với dòng thật của `ServerMetrics` và dòng sai định dạng | P0 |
| AC29 | **Ranh giới:** diff của TEL-01 không sửa file nào trong `Backend/GameServer/**`, `Backend/Shared/Protocol/**`, `balance/*.json`, GDD xlsx; `EventPacket` và `GameEventType` giữ nguyên | `git diff --stat` theo đường dẫn; review | P0 |
| AC30 | Công cụ chỉ dùng thư viện chuẩn Python (`python -I`), không mạng, không `pip` | Quét import; chạy trên máy không cài gói phụ | P1 |
| AC31 | Tài liệu hợp đồng hook (mục 2.7) liệt kê cho 18 loại: chủ ticket, điểm gọi, trạng thái; **đề xuất hook cho Vinh** (H1–H6) nằm trong một tài liệu riêng, có chữ ký duyệt của Vinh trước khi ai gắn | Review tài liệu; xác nhận duyệt | P1 |

### 3.6. Ngân sách tick và hiệu năng (GDD 3)

| # | Tiêu chí (đo được) | Cách kiểm | Ưu tiên |
| --- | --- | --- | --- |
| AC32 | Vi đo hook: `TryWrite` p99 ≤ 10 µs và 0 byte cấp phát sau khởi động | Test vi đo 1 triệu lần; ghi số | P0 |
| AC33 | **Ngân sách tick:** server thật cục bộ, một phòng 10 phiên, 60 s, 3 lần mỗi cấu hình (tắt/bật), hai mức tải (≈ 70 và 1 000 bản ghi/s): `tickP95Ms` bật − tắt ≤ **0,5 ms**; `tickP95Ms` < **33,3 ms**; `droppedTicks` = 0; `tickOverruns` không hơn cấu hình tắt; ghi cả `tickP99Ms` | Đọc dòng `[metrics]` của chính server; bảng số trong QA; ghi rõ "tải giả" | P0 (điều kiện: có sink gắn vào server, H1–H3) |
| AC34 | Hàng đợi đầy khi tải căng được **thấy** (bộ đếm bỏ trong `_trailer`, báo cáo) và không làm tick quá hạn | Chạy 1 000 bản ghi/s, đếm bỏ ≥ 0 hiển thị và `droppedTicks` = 0 | P1 |
| AC35 | Client: 0 byte cấp phát mỗi khung do telemetry ở trạng thái ổn định; không I/O ở luồng chính; chênh FPS ≤ 1 hoặc frame time p95 ≤ 0,3 ms giữa bật/tắt | Test cấp phát; đo trên máy QA, nếu không đo được thì ghi "chưa kiểm chứng" và lý do | P1 |

### 3.7. Nguồn thật (điều kiện, không chặn nghiệm thu Phase A)

| # | Tiêu chí (đo được) | Cách kiểm | Ưu tiên |
| --- | --- | --- | --- |
| AC36 | **Khi H1–H3 đã gắn:** một trận cục bộ thật (profile `test`, ≥ 2 client) cho bản ghi thật khớp trạng thái server: `match_start` 1, `player_spawn` = số người, `zone_phase` đủ 5 phase, `elimination` = số người bị loại (kể cả `cause` `ZONE`/`DISCONNECT` đúng), `match_end.reason` = `MatchResult.Reason`; **không** bản ghi nào mang `fake` | Chạy trận thật, so với màn hình kết quả và log server; đối chiếu số | P1 (điều kiện: Vinh duyệt H1–H3) |
| AC37 | **Khi H5/H6 đã gắn:** rớt rồi nối lại trong 15 s cho đúng một `DROPPED` và một `RESUMED`; rớt quá 15 s cho `GRACE_EXPIRED` và `elimination` `cause = DISCONNECT`; `replay_choice` đủ mỗi người | Test mạng thật cục bộ | P1 (điều kiện: Vinh duyệt H5/H6) |
| AC38 | Client thật 60 s trong trận: ≥ 12 bản ghi `perf_client`; `fpsAvg` lệch ≤ 2% so với (số khung / thời gian) đo độc lập trong test | Test + chạy thật | P1 |
| AC39 | Trước khi WPN-02/ITM-01/WPN-03/bụi tồn tại, báo cáo **ghi rõ** các loại W là "fake-only" (cột độ phủ); khi từng ticket gắn H7, QA chạy lại đo thật và cập nhật độ phủ | Kiểm `coverage.csv`; QA sau mỗi ticket | P2 |

## Câu hỏi và giả định chờ G1

### Giả định (G)

| # | Giả định |
| --- | --- |
| G-1 | Ref `3–18,21–22` = bảng kiểm thử (STT 1–8), BALANCE (STT 1–6) và PLAYTEST STT 1–2, theo cách tính ở đầu file. |
| G-2 | 18 loại ở bảng 2.4 là danh sách làm việc; PIC được thay loại nào cũng được, miễn mỗi loại giữ lại vẫn trả lời một câu hỏi thiết kế (Q1). |
| G-3 | "Config version" = `cfg` + `cfgHash` (server) + `balHash` (client), không cần thêm trường version vào JSON balance. |
| G-4 | Cửa sổ giao tranh `gapSec` = 5 s (150 tick). TTK là số đo theo định nghĩa 2.5, không phải mục tiêu. |
| G-5 | Ngưỡng "mẫu thấp": n < 10 mỗi nhóm. |
| G-6 | ID đề xuất cho thứ GDD chưa đặt: đạn `AMMO`; vật chứa `CHEST_WOODEN`, `POT_CLAY`, `AIRDROP_CRATE`, `CORPSE`; hazard `LAVA`, `SPIKE_PATCH`, `SWAMP`; bụi `BUSH`; bo `ZONE`. |
| G-7 | FPS 1% low = FPS trung bình của 1% khung chậm nhất trong cửa sổ 5 s. |
| G-8 | Trần dung lượng 8 MiB mỗi file, 256 MiB tổng; `position_sample` 1 Hz. |
| G-9 | Telemetry **tắt mặc định**; bật cho buổi playtest bằng `TELEMETRY_PATH` (server) và một cờ cấu hình (client); không bật trên hosting công khai. |
| G-10 | Công cụ viết bằng Python stdlib đặt ở `Tools/Telemetry/`; client viết C# trong thư mục `Telemetry` riêng; schema ngôn ngữ trung lập, không dùng chung assembly C# giữa server và client (không đụng `Backend/Shared`). |
| G-11 | Số đo tick trên cục bộ là **tải giả**; chưa suy ra sức chứa 10 người thật. |

### Câu hỏi (Q), mỗi câu có mặc định

| # | Câu hỏi | Mặc định khuyến nghị |
| --- | --- | --- |
| Q1 | Danh sách 18 loại ở bảng 2.4 có đúng ý PIC không (đặc biệt `position_sample` và `hide_span` có đáng không)? | Duyệt như đề xuất. |
| Q2 | JSONL làm bản gốc, CSV/Markdown sinh từ đó? | Có. |
| Q3 | Vinh có duyệt hướng không sửa `MatchRuntime`, chỉ gắn bộ quan sát qua event public (H1–H3) và thêm hai event nhỏ H5, H6? | Gửi đề xuất cho Vinh ở Plan; Phase A chạy không cần. |
| Q4 | FPS/RTT client: file cục bộ do QA thu về, hay sau này đẩy lên server qua packet mới (cần G2)? | File cục bộ. |
| Q5 | Bật telemetry thế nào cho playtest và có bật trên bản công khai không? | Theo buổi, không bật công khai (G-9). |
| Q6 | Tần suất `position_sample`? | 1 Hz, cấu hình trong khoảng 0,5–2 Hz. |
| Q7 | Cửa sổ giao tranh 5 s và định nghĩa TTK 2.5 có dùng được cho QA-01? | Dùng; QA-01 được đổi qua tham số. |
| Q8 | Ai chốt ID chưa có (G-6) và khớp với ITM-01/CLI-04? | ITM-01 (Quang Huy) và CLI-04 (Hào) xác nhận; TEL-01 dùng ID đề xuất tới lúc đó. |
| Q9 | Ngưỡng ngân sách tick (chênh ≤ 0,5 ms, hook p99 ≤ 10 µs)? | Dùng; chỉnh lại nếu đo thực tế cho thấy nền nhiễu cao. |
| Q10 | Ai gắn lời gọi sink vào hệ thống của mình (H7)? | Chủ từng ticket (Hào, Quang Huy, Vinh, Vũ) trong ticket của họ; TEL-01 không sửa. |
| Q11 | Thêm dòng cho vùng `Tools/Telemetry/**`, `Docs/Telemetry/**`, `Assets/_JungleMaze/**/Telemetry/**` vào mục 4 của `Agent_Coordination.vi.md`? | CPO thêm trước khi build. |
| Q12 | Thư mục báo cáo mẫu: `Docs/Reports/TEL-01_<ngày>/`? | Có. |

### Rủi ro (R)

| # | Rủi ro | Giảm thiểu |
| --- | --- | --- |
| R1 | Chưa có combat/loot thật, nên TTK, loot, CC mới chỉ kiểm trên dữ liệu giả; schema có thể hóa ra thiếu trường khi hệ thống thật xuất hiện | Trường tối thiểu, `v` cho phép thêm; chạy lại QA khi WPN-02/ITM-01 gắn H7 (AC39) |
| R2 | Phụ thuộc Vinh cho H5, H6 và duyệt H1–H3; chậm duyệt kéo Phase B | Phase A độc lập; AC36–AC37 để điều kiện |
| R3 | `HealthService` tạo lại mỗi trận: quên gắn lại sẽ mất `damage` từ trận thứ hai | AC36 chạy ≥ 2 trận liên tiếp; ghi vào Plan |
| R4 | Hook trên luồng tick làm chậm tick nếu ai đó thêm I/O hoặc cấp phát | AC24, AC32, AC33; review cấm I/O ở đường gọi |
| R5 | Hàng đợi đầy bỏ bản ghi quan trọng (đặc biệt `elimination`) khi tải căng | Đếm và hiển thị; hàng đợi đủ lớn cho 1 000 bản ghi/s; không chặn tick (AC34) |
| R6 | Lệch đồng hồ giữa máy client và server làm sai thứ tự theo `utc` | Sắp theo `tick`, `utc` chỉ tham khảo (mục 2.8) |
| R7 | Nguyên nhân chết thô (`damage` nguồn 255 = bo; mất kết nối dùng nguồn chính nạn nhân) có thể lẫn nếu `GraceExpired` và `EntityDied` khác tick | Phân loại theo cùng tick; AC37 kiểm; đề xuất Vinh truyền `cause` rõ ràng |
| R8 | Dữ liệu giả che lỗi schema thật; người đọc nhầm số giả là số thật | Cờ `fake`, độ phủ real/fake, `--require-real` |
| R9 | Vùng file mới chưa có dòng trong `Agent_Coordination.vi.md` (`Docs/Art/**` từng thuộc Codex, các vùng khác của người khác) | CPO thêm dòng trước khi build (Q11) |
| R10 | File telemetry client nằm trên máy người chơi, nếu quên thu thì mất; thư mục `persistentDataPath` có thể bị xóa | Hướng dẫn thu ngay sau buổi; `_trailer` giúp biết file đầy đủ |
| R11 | Dung lượng và quyền riêng tư: file chứa `PlayerSessionId` trong `player_spawn` | Chỉ ngẫu nhiên theo phiên, không phải tài khoản; không ghi tên (AC4) |

## 4. Kế hoạch kỹ thuật

_Tech Lead điền._

## 5. Nhật ký build

_Dev điền._

## 6. Review

_Code-reviewer điền._

## 7. QA

_QA điền._

## 8. Nghiệm thu

_CPO điền._
