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

Tech Lead, 27/10/2026. Kế hoạch đọc từ code hiện tại: `MatchRuntime.cs`, `HealthService.cs`, `MatchOutbox.cs`, `ServerMetrics.cs`, `Program.cs` (GameServer); `PacketRegistry.cs`, `MatchPackets.cs` (Protocol); `MovementTelemetry.cs`, `MatchSession.cs`, `NetworkStats.cs`, `Bootstrap.cs` (client); `Tests/MatchChecks` (cách test backend tham chiếu GameServer).

### 4.0. Kết luận chính

1. **Phase A chạy được mà không sửa một dòng nào của GameServer.** Mã sink và bộ quan sát phía server nằm ở thư mục mới `Backend/Telemetry/` (của TEL-01). Thư mục này **không phải project riêng**: các file `.cs` được **liên kết mã nguồn** (`<Compile Include=…>`), giống cách GameServer đang nhúng `Shared/Protocol`.
   - Phase A: project kiểm `Backend/Tests/TelemetryChecks/` tham chiếu `GameServer.csproj` (giống `MatchChecks`) và nhúng nguồn `Backend/Telemetry/**`. Nhờ vậy test chạy trên **`MatchRuntime` thật**, trong cùng tiến trình.
   - Phase B: Vinh chỉ thêm 1 dòng `<Compile Include="../Telemetry/**/*.cs" …>` vào `GameServer.csproj` và khoảng 5 dòng vào `Program.cs` (H1). Không có tham chiếu vòng giữa hai project.
   - Lý do không làm project thư viện riêng: thư viện cần kiểu của GameServer (`MatchRuntime`), còn GameServer lại cần gọi thư viện. Hai project tham chiếu nhau thì bị vòng. Liên kết mã nguồn tránh được vòng này mà không đổi gì trong GameServer.
2. **Không đổi protocol, không cần G2.** Bộ quan sát giải mã frame bằng `PacketRegistry.TryReadAll` có sẵn, ở luồng nền. `Backend/Shared/Protocol/**` chỉ đọc (AC29).
3. **Phía client chỉ ghi `perf_client`.** Theo bảng 2.4, đây là loại duy nhất có nguồn C; 17 loại còn lại do server hoặc bộ phát giả ghi. Client bật bằng tham số dòng lệnh `-telemetry`, nên **không sửa `ClientConfig`** của người khác.
4. **Đo ngân sách tick (Q9) có bằng chứng ngay ở Phase A.** Harness trong tiến trình dùng đúng `FixedTickLoop` và `ServerMetrics` thật. `tickP95Ms` lấy từ chính dòng `[metrics]` của server, không tự đo (4.6). Phase B chạy lại trên tiến trình GameServer thật.
5. **Spec có 4 điểm code thực tế khác giả định** (4.9, X1–X4). Plan xử lý cả 4, không cần sửa spec trước G1:
   - `ResumeTokenRotated` cũng phát lúc vào trận, không chỉ khi resume.
   - Roster và kết quả trận chứa `DisplayName`.
   - Elimination phải ghép từ 4 tín hiệu, vì các tín hiệu này tới ở các bước khác nhau trong cùng tick.
   - Dòng `[metrics]` không có `windowSec`.

### 4.1. Kiến trúc

```text
GameServer (của Vinh, KHÔNG sửa ở Phase A)        Backend/Telemetry/** (TEL-01, liên kết nguồn)
 MatchRuntime ── event public ───────────────►  MatchTelemetryObserver  (luồng tick: chỉ chép tham chiếu + tick)
   ReliableBroadcast(byte[])                        │ TryWrite(TelemetryEvent)  ← struct, 0 cấp phát, không khóa
   SnapshotGenerated(byte[])                        ▼
   MatchCompleted / PlayerDropped / GraceExpired   TelemetryRing ×2 (critical, bulk)  ← MPSC Vyukov, có giới hạn
   ResumeTokenRotated                               │ (luồng nền, Thread riêng)
 HealthService.EntityDamaged / EntityDied           ▼
                                                 MatchTelemetryAssembler → JsonlTelemetryWriter
 ServerMetrics → stdout "[metrics] …"               (giải mã PacketRegistry, ghép bản ghi, ghi file theo trận)
        │
        └── server.log ──► Tools/Telemetry  perflog → perf_server.jsonl

Client (Unity, Clean Architecture)
 Domain/Telemetry      FrameStats (C# thuần: fpsAvg, 1% low, p95, max trên mảng cấp sẵn)
 Application/Telemetry ITelemetrySink (port) · PerfClientRecorder : ITickable (đọc MatchSession, INetworkClient.Stats)
 Infrastructure/Telemetry JsonlTelemetryFileSink (ring SPSC + Thread nền, persistentDataPath/Telemetry/…)
 Bootstrap/Telemetry   Bootstrap.Telemetry.cs: ComposeTelemetry(...) chỉ khi có -telemetry

Ngoại tuyến: Tools/Telemetry/*.py (stdlib) ── validate · fake · perflog · merge · report · compare
Hợp đồng chung: Docs/Telemetry/telemetry.schema.v1.json (một nguồn, ba bên cùng đọc)
```

**Hợp đồng schema dùng chung** là file `Docs/Telemetry/telemetry.schema.v1.json`. Đây là định dạng gọn tự định nghĩa, không phải JSON Schema draft, vì Python stdlib không có bộ kiểm JSON Schema.

- **Nội dung file:**
  - Phần envelope.
  - 18 `types`, mỗi loại có: `fields` (kiểu, cho phép null hay không, miền giá trị), `dedupe` (danh sách trường của khóa ngữ nghĩa), `once` (true/false), `source` (`S0`/`S+`/`W`/`C`).
  - `enums`.
  - `extraIds`: các ID trong G-6.
- **Code ID vũ khí và trang bị không chép vào schema.** Bộ kiểm đọc trực tiếp `balance/weapons.json` và `equipments.json` lúc chạy. Như vậy không sinh bảng config thứ hai, đúng mục 2.2.
- **Cách giữ ba bên khớp schema:**
  - Python dùng file này làm bộ kiểm chính.
  - Test C# server và client kiểm tên trường bằng một danh sách hằng.
  - QA chạy `validate` trên file do C# thật sinh ra (4.5).

### 4.2. Danh sách file theo vùng

**A. Tài liệu và schema — `Docs/Telemetry/**`** (mới)

| File | Nội dung | AC |
| --- | --- | --- |
| `telemetry.schema.v1.json` | Schema máy đọc (4.1) | AC1, AC2, AC5 |
| `Schema.vi.md` | Bảng người đọc của 18 loại, trường, khóa, nguồn. Có test kiểm danh sách loại trong file này trùng với JSON | AC1 |
| `Hook_Contract.vi.md` | Bảng 18 loại × chủ ticket × điểm gọi × trạng thái (S0/S+/W/C); hợp đồng `ITelemetrySink` cho H7 | AC31 |
| `GameServer_Hook_Proposal.vi.md` | Đề xuất riêng cho Vinh: H1, H5, H6, mỗi hook có diff mẫu, lý do và ô ký duyệt (4.7) | AC31 |
| `QA_Guide.vi.md` | Lệnh chép-dán: bật server, bật client, thu file, gộp, báo cáo, so hai config, đo tick | AC23 |

**B. Công cụ ngoại tuyến — `Tools/Telemetry/**`** (mới, Python 3.12 stdlib, chạy `python -I`)

| File | Vai trò |
| --- | --- |
| `telemetry.py` | CLI một cửa. Lệnh con: `validate`, `fake`, `perflog`, `merge`, `report`, `compare`. Mã thoát ở cuối bảng |
| `tel_schema.py` | Nạp schema và Code ID; `validate_record()` trả danh sách lỗi, không ném lỗi; quét PII (AC4) |
| `tel_fake.py` | Bộ phát giả xác định theo `seed` (4.4); các cờ `--dup --gap --reorder --conflict --reconnect --cfg A\|B` |
| `tel_perflog.py` | Chuyển dòng `[metrics]` thành `perf_server` (4.3) |
| `tel_merge.py` | Gộp, chống trùng kỹ thuật/ngữ nghĩa, phát hiện xung đột và hở `seq`, đối chiếu `_trailer` |
| `tel_metrics.py` | Các số đo ở 2.5: TTK, nguyên nhân chết, loot, CC, trang bị đầu tiên, thời gian dùng vật phẩm, ẩn nấp, hiệu năng |
| `tel_report.py` | Sinh `report.md` và 9 CSV ở 2.9; lệnh `compare A B` |
| `test_telemetry.py` | `unittest`, tự thêm thư mục của nó vào `sys.path` (giống `Tools/Audio`) |
| `fixtures/valid/*.jsonl` | 18 file, mỗi loại một file |
| `fixtures/invalid/*.jsonl` | Tối thiểu: 12 file thiếu trường, mỗi file thiếu một trường; 1 file mỗi trường liệt kê có giá trị ngoài miền; `BAD_ID`; PII (tên, RoomCode, JWT, IP) |
| `fixtures/hand/*.jsonl` | Giao tranh dựng tay cho AC13–AC15, kèm kết quả kỳ vọng viết sẵn trong test |
| `fixtures/from_csharp/*.jsonl` | Mẫu do C# thật sinh (server và client); QA chép vào để test chéo ngôn ngữ |
| `golden/seed1/` | `report.md` và 9 CSV của bộ giả seed 1 (AC12). Chỉ cập nhật qua `--update-golden`, có diff trong commit |
| `README.md` | Tóm tắt lệnh, trỏ sang `Docs/Telemetry/QA_Guide.vi.md` |

Mã thoát của CLI:

| Mã | Nghĩa |
| --- | --- |
| 0 | OK, kể cả khi thiếu dữ liệu một phần (AC18) |
| 2 | Có xung đột khi chạy `--strict` (AC7) |
| 3 | Có dữ liệu giả khi chạy `--require-real` (AC22) |
| 4 | Lỗi schema khi chạy `validate --strict` |
| 1 | Lỗi dùng lệnh |

**C. Module server — `Backend/Telemetry/**`** (mới; liên kết nguồn, namespace `JungleMaze.Telemetry.Server`)

| File | Vai trò |
| --- | --- |
| `TelemetryEvent.cs` | `struct` không cấp phát, gồm: `Kind` (byte), `Tick`, `MatchTick`, các byte `A`/`B`/`C`, 2 số `int`, 2 số `float` và một tham chiếu `object? Ref`. `Ref` chỉ trỏ tới `byte[]` frame **đã có sẵn**, hoặc chuỗi ID lấy từ config |
| `TelemetryRing.cs` | Hàng đợi MPSC có giới hạn kiểu Vyukov: mảng cấp sẵn lũy thừa 2, `Interlocked`/`Volatile`, không `lock`. `TryWrite` trả `false` khi đầy và tăng `Dropped`. Có 2 ring: **critical** (vòng đời, damage, chết, session, H7; 16 384 ô) và **bulk** (snapshot, position; 8 192 ô). Bulk đầy không làm mất elimination (R5) |
| `ITelemetrySink.cs` | `bool TryWrite(in TelemetryEvent e)`, `long Dropped`, `bool Enabled`. Có `NullTelemetrySink.Instance`. Đây là hợp đồng H7 |
| `MatchTelemetryObserver.cs` | Gắn vào một `MatchRuntime`. Mọi handler chỉ ghi struct vào ring. Handler `ReliableBroadcast` còn so tham chiếu `match.Health` với bản đã lưu: khác thì gỡ đăng ký cũ, đăng ký mới (R3). Delegate được tạo một lần, lưu thành field |
| `MatchTelemetryAssembler.cs` | Luồng nền: giải mã frame bằng `PacketRegistry` riêng của luồng này; giữ trạng thái theo `MatchId`; dựng bản ghi 1, 2, 3, 5 (thô), 6, 8, 14, 15. Không bao giờ đọc `DisplayName`, `ProfileKey` hay resume token (4.9) |
| `JsonlTelemetryWriter.cs` | Một `Thread` nền (`IsBackground`), cứ 50 ms gom hàng đợi một lần. Mỗi (trận, nguồn) một file `${TELEMETRY_PATH}/<yyyy-MM-dd>/<matchId>.server.<wid>.jsonl`. Có `_header` và `_trailer`. Trần 8 MiB mỗi file, 256 MiB tổng. Lỗi I/O thì tự tắt và in một dòng `[telemetry-error]`. Có seam `Func<string, Stream>` cho test lỗi đĩa |
| `TelemetryHost.cs` | `FromEnvironment()` đọc `TELEMETRY_PATH`, `TELEMETRY_SAMPLE_HZ` (mặc định 1, kẹp trong 0,5–2), `TELEMETRY_MAX_FILE_MIB`, `TELEMETRY_MAX_TOTAL_MIB`. Không có `TELEMETRY_PATH` thì trả host tắt: không tạo thư mục, không tạo luồng (AC27). Có `Attach(RoomManager.Room)` và `Dispose()` (flush tối đa 3 s, ghi `_trailer`). Đây là API cho H1 |
| `TelemetryJson.cs` | Viết JSON bằng tay vào `StringBuilder` dùng lại, `CultureInfo.InvariantCulture`, LF. Chỉ chạy ở luồng nền |

**D. Kiểm server — `Backend/Tests/TelemetryChecks/**`** (mới)

- **Project:** `TelemetryChecks.csproj` tham chiếu `../../GameServer/GameServer.csproj` và nhúng `../../Telemetry/**/*.cs`. Cùng kiểu `MatchChecks`: in `[PASS]`/`[FAIL]`, mã thoát ≠ 0 khi có FAIL.
- **Các chế độ của `Program.cs`:**

  | Chế độ | Kiểm gì | Thời gian |
  | --- | --- | --- |
  | Mặc định | Chức năng | < 60 s |
  | `--bench` | Vi đo AC32 | |
  | `--tick-budget [--runs 3 --seconds 60 --load 70,1000]` | AC33/AC34 trong tiến trình | |
  | `--emit-sample <dir>` | Sinh `fixtures/from_csharp/server_*.jsonl` | |
- **Không thêm vào `JungleBackend.sln`, không sửa `run_all.cmd`.** Hai file đó là dùng chung. Chạy bằng `dotnet run --project Tests/TelemetryChecks`. Phase B đề xuất Vinh thêm vào sln.

**E. Client — `Assets/_JungleMaze/Scripts/**/Telemetry/**`** (mới) **và test**

| File | Lớp | Vai trò |
| --- | --- | --- |
| `Domain/Telemetry/FrameStats.cs` | Domain | Ring `float[2048]` cấp sẵn. Tính `fpsAvg` (= số khung / tổng thời gian), `fps1Low` theo G-7, `frameMsP95`, `frameMsMax`. Sắp xếp tại chỗ trên một mảng nháp cấp sẵn. 0 cấp phát |
| `Domain/Telemetry/PerfClientSample.cs` | Domain | `struct` giữ đủ trường của `perf_client` |
| `Application/Telemetry/ITelemetrySink.cs` | App (port) | `bool TryWrite(in PerfClientSample s)`; `TelemetryContext` (matchId, netId, cfg, cfgHash, balHash, build, wid) |
| `Application/Telemetry/PerfClientRecorder.cs` | App | `ITickable`. Chỉ đếm khi `MatchSession.Phase == InGame`; cứ 5 s đóng cửa sổ và gửi 1 mẫu. Số liệu lấy từ: `rttP50`/`rttP95`/`transport` ← `INetworkClient.Stats`; `jitterMs` ← `RemotePlayerInterpolator.Stats.JitterMs`; `lossPct` ← chênh `PongsLost` / chênh `PingsSent` trong cửa sổ (null nếu không có ping); `entities` ← `Players` |
| `Infrastructure/Telemetry/JsonlTelemetryFileSink.cs` | Infra | Ring SPSC (main thread là nơi ghi duy nhất) cùng một `Thread` nền ghi file `persistentDataPath/Telemetry/<matchId>.client.<netId>.jsonl` (chế độ nối thêm), có `_header`/`_trailer`, trần 8 MiB, tự tắt khi lỗi I/O |
| `Infrastructure/Telemetry/ClientTelemetryIdentity.cs` | Infra | `wid` (8 hex ngẫu nhiên mỗi tiến trình); `build` = `Application.version` hoặc `dev`; `balHash` = SHA-256 của `weapons.json` + `equipments.json` + `match.json`, tính **một lần lúc compose** |
| `Bootstrap/Telemetry/Bootstrap.Telemetry.cs` | Bootstrap | `partial void`/`ComposeTelemetry(tickRunner, match, network, remote)`. Chỉ đăng ký khi dòng lệnh có `-telemetry` (`-telemetryDir <path>` tùy chọn) |
| `Bootstrap/Bootstrap.cs` | file dùng chung | **Thêm đúng 1 dòng** `ComposeTelemetry(...)` sau `ComposeMatchOverlay(...)`; ghi một dòng vào `Agent_Coordination.vi.md` |
| `Tests/EditMode/Telemetry/FrameStatsTests.cs` | test | AC38: `fpsAvg` lệch ≤ 2% so với số khung/thời gian; đúng định nghĩa 1% low; khung đứng yên và khung rất dài |
| `Tests/EditMode/Telemetry/PerfClientRecorderTests.cs` | test | 60 s giả lập cho ≥ 12 mẫu; ngoài InGame không có mẫu; `seq` liên tục qua mô phỏng rớt rồi nối lại; không có tên hay RoomCode; 0 cấp phát ở trạng thái ổn định (`Is.Not.AllocatingGCMemory()`, AC35) |
| `Tests/EditMode/Telemetry/JsonlTelemetryFileSinkTests.cs` | test | Header và trailer; `written` = số dòng; vượt trần thì `truncated`; thư mục hỏng thì tắt, không ném lỗi; mặc định không tạo file (AC27) |

`MovementTelemetry` giữ nguyên. Client không ghi `session_state` (bảng 2.4 không có nguồn C).

**Không đụng:** `Backend/GameServer/**`, `Backend/Shared/**`, `JungleBackend.sln`, `run_all.cmd`, `balance/*.json`, `ClientConfig`, `MovementTelemetry.cs`, GDD xlsx, `ProjectSettings/*`. Chỉ đọc: `Backend/Shared/Protocol/**`, `balance/**`.

### 4.3. Định dạng, sink và các quy tắc ghép bản ghi

- **Dòng JSONL:** UTF-8 không BOM, LF. Khóa ghi theo thứ tự cố định: envelope trước, trường riêng sau. Số thực ghi tối đa 3 chữ số thập phân. Không có `NaN`/`Infinity`; giá trị không hợp lệ ghi `null`.
- **`_header`** gồm `v`, `type: "_header"`, `src`, `wid`, `build`, `opened` (utc), `schema: "telemetry.v1"`. Chưa có `cfgKey` vì lúc mở file có thể chưa biết. `cfg`/`cfgHash` nằm trong envelope của từng dòng.
- **`_trailer`** gồm `written`, `droppedQueue`, `droppedIo`, `truncated`, `closed`. File thiếu `_trailer` (tiến trình bị kill) thì `merge` báo `unterminated`, nhưng vẫn gộp.
- **`seq`:** đếm theo (`matchId`, `src`, `wid`) ở luồng nền, nên luồng tick không phải đếm gì.
  - Bản ghi bị bỏ vì hàng đợi đầy **không chiếm** `seq`. Số lượng của chúng nằm ở `droppedQueue`, không thành hở `seq`.
  - Vì vậy hở `seq` chỉ xuất hiện khi mất dòng sau khi đã ghi, đúng nghĩa AC10.
- **Server ghép bản ghi từ tín hiệu thô** (luồng nền):

  | Bản ghi | Ghép từ |
  | --- | --- |
  | `match_start` | Frame phase có `Phase == InGame`. `profile` ← `match.Profile.Name`, chép lúc Attach. `seedHash` = 16 hex đầu của SHA-256(`MatchId`) |
  | `player_spawn` | `MatchEntityEvent.Spawn` ghép với roster ngay sau đó (`netId`, `sess`), tọa độ từ snapshot đầu. `loadout = []` |
  | `damage` (thô) | `EntityDamaged(victim, dmg, src)` ở tick T. `src == 255` → `ZONE`; `src == victim` → chờ ghép `DISCONNECT`; còn lại → `WEAPON`, `attacker = src`. Các trường W để `null` |
  | `elimination` | Ghép 4 tín hiệu: `EntityDied` (T) + `GraceExpired` (cùng T) + `MatchEntityEvent.Eliminated` + roster ngay sau (`rank`, `SurvivalTicks`). Chỉ phát khi đã thấy roster; `zonePhase` lấy từ trạng thái bo gần nhất; `x`/`y` từ snapshot gần nhất; `hidden = null` |
  | `zone_phase` | So `ZoneStatePacket` (PhaseIndex, InWarning, tick shrink) của **mọi** snapshot với trạng thái trước; phát khi đổi. Dùng mọi snapshot nên chính xác từng tick, không lệch ±1 s |
  | `position_sample` | Lấy 1 trong `30 / sampleHz` snapshot. Lọc ở luồng nền, không phải ở luồng tick |
  | `session_state` | `PlayerDropped` khi Phase = Loading → `LOAD_TIMEOUT`. `GraceExpired` → `GRACE_EXPIRED`. `ResumeTokenRotated` → `RESUMED`, **chỉ khi** trận đó đã có frame InGame (X1). `DROPPED`/`LEFT` cần H5 |
  | `match_end` | `MatchResultPacket` trong `ReliableBroadcast`: `Reason`, `WinnerNetId` (255 → null). `durTicks` = tick − tick InGame. `alive` = số người còn sống. Không đọc `MatchResult` vì nó có `DisplayName` và `ProfileKey` |
- **`perf_server`** (Python `perflog`):
  - Tách `key=value`. Lấy `peers`, `active`, `rooms` và các trường hiệu năng.
  - `windowSec` = hiệu `utc` với dòng trước; dòng đầu dùng `--interval`, mặc định 10 (X4).
  - Dòng hỏng thì bỏ qua và đếm `malformed`.
  - Gắn vào trận khi `utc` nằm trong [`match_start.utc`, `match_end.utc` + `windowSec`].
- **Gộp (`merge`):**
  1. Đọc mọi file, kiểm từng dòng.
  2. Khóa kỹ thuật → `dup` hoặc `conflict`. So nội dung bằng JSON chuẩn hóa (`sort_keys`).
  3. Khóa ngữ nghĩa của loại `once` → `semantic_dup`.
  4. Tìm hở `seq` theo (`matchId`, `src`, `wid`).
  5. Đối chiếu `_trailer.written` với số dòng thật.
  6. Sắp theo (`tick`, `src`, `seq`, `wid`) và ghi `events.jsonl` cùng `conflicts.jsonl`.

  Kết quả không phụ thuộc thứ tự đọc file (AC11).
- **Định nghĩa phân vị** (ghi vào `Schema.vi.md` để Python và C# cùng cách tính): trung vị = `statistics.median`; p90/p95 = nearest-rank `ceil(q·n)`.

### 4.4. Bộ phát giả

- **Dữ liệu sinh ra:**
  - 6 trận mỗi lần chạy, 10 người.
  - Bo 5 phase theo `zone_schedule.json`.
  - Vũ khí và trang bị lấy từ Code ID thật cộng G-6.
  - `utc` = mốc cố định + tick × 33 ms, không đọc đồng hồ máy. Nhờ vậy cùng `seed` cho file giống từng byte (AC20).
- **Hai config:** `--cfg A|B` khác `cfgHash`. B tăng damage của 3 vũ khí lên 1,5 lần để TTK khác có chủ ý (AC17).
- **Tiêm lỗi:** mỗi cờ ghi số đã tiêm ra `inject_manifest.json`, để test so đúng con số.

  | Cờ | Tiêm gì |
  | --- | --- |
  | `--dup` | 5% dòng trùng |
  | `--gap k` | Mất k dòng |
  | `--reorder` | Đảo thứ tự dòng |
  | `--conflict 3` | 3 xung đột |
  | `--semantic-dup 6` | 6 lần báo lặp loại "một lần" |
  | `--reconnect` | Client rớt ở T, nối lại ở T+Δ < 15 s |
- **Đánh dấu giả:** mọi dòng giả có `"fake": true` (AC22). Dữ liệu từ C# thật **không bao giờ** có trường `fake`.

### 4.5. Kiểm thử, theo AC

| AC | Kiểm ở đâu | Loại |
| --- | --- | --- |
| AC1, AC2, AC3, AC5 | `test_telemetry.py` (18 fixture hợp lệ; ≥ 12 thiếu trường; ngoài miền; `BAD_ID`); test kiểm `Schema.vi.md` khớp JSON | Python unit |
| AC4 | Python: cấy tên/RoomCode/JWT/IP vào fixture thì bị bắt; quét toàn bộ output giả. Server: `TelemetryChecks` đặt `DisplayName` mẫu khi đăng ký người chơi rồi quét file kết quả. Client: test recorder | Python + .NET + EditMode |
| AC6–AC11, AC21 | Python: bộ giả có tiêm lỗi; so với `inject_manifest.json`; AC11 xáo trộn 5 lần, báo cáo giống từng byte | Python unit |
| AC12, AC16, AC17 | So `golden/seed1`; hai tập A/B | Python, golden |
| AC13, AC14, AC15 | `fixtures/hand`, kết quả kỳ vọng viết sẵn trong test | Python unit |
| AC18 | Bỏ lần lượt từng nguồn: mã thoát 0, mục tương ứng ghi "không có dữ liệu" | Python unit |
| AC19 | `test_telemetry.py --perf`: 50 trận giả, khoảng 400 000 dòng; ghi thời gian và cấu hình máy | Python, QA ghi số |
| AC20, AC22 | So hash hai lần chạy; `--require-real` cho mã 3 | Python unit |
| AC23 | Người không phải tác giả (QA) chạy `QA_Guide` từ checkout sạch, bấm giờ | QA |
| AC24, AC25, AC26 | `TelemetryChecks` mặc định: ring đầy; 10 000 lần `TryWrite` khi luồng ghi bị chặn; lỗi đĩa ba kiểu (thư mục là file, chỉ đọc, `Stream` ném lỗi hết chỗ); vượt trần; hai trận liên tiếp cho hai file | .NET check |
| AC27 | Server: `TelemetryHost.FromEnvironment()` không có biến thì không tạo thư mục. **`run_all.cmd` chạy lại, vẫn xanh.** Client: không có `-telemetry` thì không có file, không đăng ký `ITickable` | .NET + EditMode + QA |
| AC28 | Python: 3 dòng `[metrics]` thật chép từ log `ServerMetrics`, 4 dòng hỏng | Python unit |
| AC29 | `git diff --stat main… -- Backend/GameServer Backend/Shared balance` rỗng | Review |
| AC30 | Test quét `import` của `Tools/Telemetry/*.py`, chỉ cho phép module stdlib | Python unit |
| AC31 | Review tài liệu; Vinh ký ở `GameServer_Hook_Proposal.vi.md` | Review, người |
| AC32 | `TelemetryChecks --bench` | .NET, QA ghi số |
| AC33, AC34 | `--tick-budget` ở Phase A (proxy); chạy lại trên tiến trình thật ở Phase B | .NET, QA |
| AC35, AC38 | EditMode (cấp phát, `fpsAvg`) cùng đo FPS bật/tắt trên máy QA (`NetworkStatsOverlay`); không đo được thì ghi "chưa kiểm chứng" | EditMode + QA |
| AC36 | **Phase A:** `TelemetryChecks` chạy một trận `MatchRuntime` thật profile `test` với 4 người: đi ra ngoài bo đến chết (`ZONE`), một người rớt quá 15 s (`DISCONNECT`), chạy 2 trận liên tiếp để kiểm gắn lại `Health` (R3); so số bản ghi với trạng thái `MatchRuntime`. **Phase B:** trận thật qua GameServer | .NET (proxy) + QA |
| AC37 | Phase A chỉ kiểm được `RESUMED` và `GRACE_EXPIRED` qua `MatchRuntime.TryResume`. `DROPPED`, `LEFT`, `replay_choice` chờ H5/H6 | .NET + QA (điều kiện) |
| AC39 | `coverage.csv` ghi loại W là `fake-only` | Python unit |

**Kiểm chéo ngôn ngữ:** QA chạy `TelemetryChecks --emit-sample` và một trận client với `-telemetry`, rồi chạy `python -I Tools/Telemetry/telemetry.py validate --strict <file>` trên **file do C# thật sinh ra**. Mẫu được chép vào `fixtures/from_csharp/` để từ đó Python test luôn chạy cả mẫu này.

**Bộ test có sẵn phải giữ xanh:** EditMode/PlayMode của client, `run_all.cmd`, `MatchChecks`.

### 4.6. Ngân sách tick: đo thế nào (Q9)

Ba tầng, rẻ trước:

1. **Vi đo (AC32), `--bench`:**
   - Gọi 1 000 000 lần `TryWrite` với struct mẫu. Luồng nền vẫn gom như thật, chạy ở hai chế độ: ring rỗng và ring đầy.
   - Đo từng lần bằng `Stopwatch.GetTimestamp`. In p50/p99/max theo µs, kèm sai số của chính đồng hồ.
   - Cấp phát đo bằng `GC.GetAllocatedBytesForCurrentThread()` trước/sau, sau khởi động. Phải bằng 0.
   - Ngưỡng: p99 ≤ 10 µs (dự kiến thực tế < 0,2 µs).
2. **Harness trong tiến trình (Phase A, không cần H1), `--tick-budget`:**
   - Dựng `ServerMetrics` và `FixedTickLoop(30, metrics)` thật, một `MatchRuntime` profile `test`. 10 phiên giả: `TryRegisterPlayer`, `SetPlayerReady`, `SetPlayerLoaded`, rồi `ProcessInput` di chuyển ngẫu nhiên có seed.
   - **Tắt:** không gắn observer. **Bật:** gắn observer qua `TelemetryHost` và ghi ra thư mục tạm.
   - Thêm bộ tạo tải trong callback tick, gọi `TryWrite` với bản ghi `attack`/`damage` giả để mô phỏng H7. Mức tải ≈ 70 và 1 000 bản ghi/s.
   - 60 s mỗi lần, 3 lần mỗi cấu hình, chạy xen kẽ theo thứ tự ABBA để triệt trôi nhiệt và nền máy. `MetricsIntervalSeconds` = 10.
   - `tickP95Ms`, `tickP99Ms`, `tickOverruns`, `droppedTicks` lấy từ **dòng `[metrics]` do `ServerMetrics` in**, không tự đo, nên cùng thước đo với production.
   - Kết quả là bảng CSV vào `Docs/Reports/TEL-01_<ngày>/tick_budget.csv`.
   - Đạt khi: trung vị 3 lần (bật − tắt) ≤ 0,5 ms; `tickP95Ms` < 33,3; `droppedTicks` = 0; `tickOverruns` bật ≤ tắt. Với AC34: `droppedQueue` hiện trong `_trailer`.
   - Ghi rõ "tải giả, trong tiến trình" (G-11). Tổng khoảng 12 phút, QA chạy nền.
3. **Phase B (sau H1):**
   - Chạy `GameServer` thật với `TELEMETRY_PATH` đặt và không đặt, cùng `NetSmokeClient --clients 10 --duration 60`.
   - Đọc `[metrics]` từ stdout và chạy `perflog` để ra số.
   - Nếu NetSmokeClient không đưa phiên vào InGame được, chỉ giữ số đo của tầng 2 và ghi lý do. Không được tự sửa NetSmokeClient: đó là code của người khác.

Client (AC35): test 0 cấp phát (EditMode) cộng QA đo FPS. Không có I/O ở main thread theo thiết kế: ghi file chỉ ở `Thread` nền. Reviewer kiểm.

### 4.7. Hook đề xuất cho Vinh (Phase B, không tự sửa)

Tất cả nằm trong `Docs/Telemetry/GameServer_Hook_Proposal.vi.md`, kèm diff mẫu. Đây là **đề xuất**, Vinh tự commit.

| Hook | Diff đề xuất | Số dòng | AC mở khóa |
| --- | --- | --- | --- |
| H1 | `GameServer.csproj`: `<Compile Include="../Telemetry/**/*.cs" LinkBase="Telemetry" />`. `Program.cs`: `using var telemetry = TelemetryHost.FromEnvironment();`; trong `HookRoomMatch` thêm `telemetry.Attach(room);`; trong `finally` gọi `telemetry.Dispose()` (flush ≤ 3 s) | ~5 | AC33 (tiến trình thật), AC36 |
| H2, H3 | **Không cần sửa GameServer.** Nằm sẵn trong `MatchTelemetryObserver` (TEL-01). H1 chỉ gọi `Attach` | 0 | AC36 |
| H4 | Không cần: đọc log | 0 | AC28 |
| H5 | `MatchRuntime`: `public event Action<string>? PlayerDisconnected;` gọi trong nhánh InGame của `MarkDisconnected`; `public event Action<string>? PlayerLeft;` gọi khi `ProcessLeave` trả true | ~4 | AC37 (`DROPPED`, `LEFT`) |
| H6 | `public event Action<string>? ReplayAcked;` gọi khi `ProcessReplayAck` trả true | ~2 | AC37 (`replay_choice`) |
| H7 | Mỗi chủ ticket (WPN-02, ITM-01, WPN-03, hệ thống bụi) gọi `sink.TryWrite(...)` tại điểm quyết định, theo `Hook_Contract.vi.md` | 1 dòng/điểm | AC39 |
| Tùy chọn | Thêm `Tests/TelemetryChecks` vào `JungleBackend.sln`; truyền `cause` rõ ràng vào `ApplyDamage` (giảm R7) | | |

Sau H1, TEL-01 có **một commit nhỏ của mình**: xóa dòng nhúng nguồn trong `TelemetryChecks.csproj`, vì lúc đó các kiểu đã nằm sẵn trong GameServer. Nếu giữ dòng này, cùng một kiểu sẽ bị định nghĩa hai lần.

**Phụ thuộc hook của từng AC:**

| Phụ thuộc | AC |
| --- | --- |
| Không phụ thuộc hook nào | AC1–AC32, AC34, AC35, AC38 |
| H1 (đã có bằng chứng proxy ở Phase A) | AC33, AC36 |
| H5, H6 | AC37 |
| Chữ ký của Vinh | AC31 |
| H7 | AC39 |

### 4.8. Chia commit và người làm

Không push. Mỗi commit chỉ chứa đường dẫn của nó.

| # | Commit (tiếng Anh) | File | Agent | Phụ thuộc |
| --- | --- | --- | --- | --- |
| 1 | `docs(tel-01): telemetry schema v1, hook contract and GameServer hook proposal` | `Docs/Telemetry/telemetry.schema.v1.json`, `Schema.vi.md`, `Hook_Contract.vi.md`, `GameServer_Hook_Proposal.vi.md` | backend-dev | Không; **là cổng cho cả ba luồng** |
| 2 | `tools(tel-01): schema validator and deterministic fake event generator` | `Tools/Telemetry/telemetry.py`, `tel_schema.py`, `tel_fake.py`, `fixtures/valid`, `fixtures/invalid`, test phần 1 | backend-dev (luồng P) | 1 |
| 3 | `tools(tel-01): merge with dedupe, conflict and gap detection` | `tel_merge.py`, test | backend-dev (P) | 2 |
| 4 | `tools(tel-01): balance report, config compare and metrics log ingest` | `tel_metrics.py`, `tel_report.py`, `tel_perflog.py`, `fixtures/hand`, `golden/seed1`, `README.md`, test | backend-dev (P) | 3 |
| 5 | `server(tel-01): bounded telemetry ring and jsonl writer outside GameServer` | `Backend/Telemetry/TelemetryEvent.cs`, `TelemetryRing.cs`, `ITelemetrySink.cs`, `JsonlTelemetryWriter.cs`, `TelemetryJson.cs`, `TelemetryHost.cs`, `Backend/Tests/TelemetryChecks/**` (chức năng, `--bench`) | backend-dev (luồng S) | 1 |
| 6 | `server(tel-01): match observer and assembler from public MatchRuntime events` | `MatchTelemetryObserver.cs`, `MatchTelemetryAssembler.cs`, kiểm trận thật trong tiến trình, `--tick-budget`, `--emit-sample` | backend-dev (S) | 5 |
| 7 | `client(tel-01): perf_client recorder and background jsonl sink` | Domain, App, Infra `Telemetry/**`, `Tests/EditMode/Telemetry/**` | unity-dev | 1 |
| 8 | `client(tel-01): compose telemetry behind -telemetry flag` | `Bootstrap/Telemetry/Bootstrap.Telemetry.cs`, **1 dòng** `Bootstrap.cs`, 1 dòng `Agent_Coordination.vi.md` | unity-dev | 7 |
| 9 | `docs(tel-01): QA guide, C# sample fixtures and sample report` | `Docs/Telemetry/QA_Guide.vi.md`, `Tools/Telemetry/fixtures/from_csharp/*`, `Docs/Reports/TEL-01_<ngày>/` (báo cáo seed 1, `tick_budget.csv`, `bench.txt`) | backend-dev | 4, 6, 8 |
| B1 | `server(tel-01): drop source include once GameServer links telemetry` | `Backend/Tests/TelemetryChecks/TelemetryChecks.csproj` | backend-dev | Vinh xong H1 |

Phase A có 9 commit. Phase B có 1 commit của TEL-01; commit H1/H5/H6 là của Vinh.

**Chạy song song:** sau commit 1, có ba luồng với file tách rời hẳn nhau.

| Luồng | Phạm vi | Dùng Unity Editor | Ghi chú |
| --- | --- | --- | --- |
| P | Python, `Tools/Telemetry/**` | Không | |
| S | .NET, `Backend/Telemetry/**` và `Backend/Tests/TelemetryChecks/**` | Không | |
| C | Unity, `**/Telemetry/**` của client cùng 1 dòng `Bootstrap.cs` | Có | Có thể chạy ngay sau khi unity-dev trả Editor từ UI-03 |

- **Số phiên:** P và S cùng do backend-dev làm. Để nhanh, mở **hai phiên backend-dev**, một cho P và một cho S; nếu CPO muốn ít phiên thì làm tuần tự P rồi S.
- **Gom kết quả:** commit 9 gom kết quả của cả ba luồng.

**Ước lượng:**

| Phần | Thời gian |
| --- | --- |
| Commit 1 | 0,5 ngày |
| P | 1,5 ngày |
| S | 1,5 ngày |
| C | 1 ngày |
| Commit 9 cùng QA đo tick | 0,5 ngày |

Đường găng là commit 1 → S → 9, khoảng **2,5 ngày** nếu chạy song song, xong ngày 30/10, kịp hạn 31/10. Làm tuần tự P rồi S thì khoảng 4 ngày, **trễ hạn**. Vì vậy cần hai phiên backend-dev.

### 4.9. Code thực tế khác giả định của spec (đã xử lý trong plan)

| # | Phát hiện | Xử lý |
| --- | --- | --- |
| X1 | `ResumeTokenRotated` **phát cho mọi người lúc vào trận** (`StartInGame`), không chỉ khi resume. Nếu đếm thẳng, AC9/AC37 sai | Chỉ ghi `RESUMED` khi trận đã có frame InGame. Thứ tự trong code: `ResumeTokenRotated` phát trong `StartInGame`, **trước** frame phase InGame. Test ở commit 6 |
| X2 | Roster và `MatchResult` mang `DisplayName`, `ProfileKey`; resume token có trong event | Assembler không đọc các trường đó; token không bao giờ vào struct. Có test AC4 phía server |
| X3 | Bước 1 (`GraceExpired`) và bước 4 (`Eliminated`, roster) của `TickInGame` tới ở các bước khác nhau trong cùng tick, nên một tín hiệu không đủ để ra elimination | Ghép elimination từ 4 tín hiệu, chỉ phát khi đã thấy roster sau `Eliminated` (4.3) |
| X4 | Dòng `[metrics]` **không có** `windowSec`; có thêm `pending`, `ticks`, `pings`, `invalidPackets`, `historyUnavailable`, `rejected`, `roomSlots` | `windowSec` tính từ hiệu `utc`; các trường thừa bỏ qua (không thuộc schema) |
| X5 | `EntityDamaged` không mang tick | Observer đọc `match.CurrentTick` trong handler (luồng tick, không cấp phát) |
| X6 | `HealthService` thay mới trong `TickCleanup` mà không phát event; frame phase Waiting phát **sau** khi thay | Handler `ReliableBroadcast` so tham chiếu `match.Health` để gắn lại (R3); kiểm bằng 2 trận liên tiếp |

### 4.10. Rủi ro kỹ thuật

| # | Rủi ro | Giảm thiểu |
| --- | --- | --- |
| TR1 | `ReliableBroadcast`/`PlayerDropped` có thể được gọi ngoài luồng tick (poll mạng, RoomControl) | Ring là MPSC (Vyukov), an toàn nhiều luồng ghi mà không khóa. Reviewer kiểm |
| TR2 | Giữ tham chiếu `byte[]` của snapshot (30/s mỗi phòng) làm GC giữ bộ nhớ lâu hơn | Bulk ring 8 192 ô ≈ 27 s mỗi phòng × khoảng 1 KB ≈ 8 MB trần; luồng nền gom mỗi 50 ms nên thực tế < 100 KB. Đo `workingSetMiB` trong AC33 |
| TR3 | Liên kết nguồn: khi Vinh làm H1, `TelemetryChecks` có thể bị trùng kiểu | Commit B1, ghi rõ trong đề xuất |
| TR4 | Đo tick ở tầng 2 nhiễu hơn 0,5 ms trên máy dev (Windows, 16 luồng) | Chạy ABBA, trung vị 3 lần. Nếu nền tắt/tắt đã chênh > 0,25 ms thì báo và đề xuất CPO nới ngưỡng (Q9 đã cho phép) |
| TR5 | Bộ ghép trên luồng nền giải mã sai vì frame có nhiều packet | Dùng `PacketRegistry.TryReadAll` như client; test với frame thật từ `MatchRuntime` |
| TR6 | Trường hợp dữ liệu giả che lỗi schema thật | Kiểm chéo bằng `fixtures/from_csharp` (4.5) |
| TR7 | `Is.Not.AllocatingGCMemory()` báo nhầm khi Unity Editor bật Deep Profile | Chạy test ở Editor thường; test có sẵn (`EventBusTests`, `ObjectPoolTests`) đã dùng cách này |
| TR8 | NetSmokeClient có thể không đưa được 10 phiên vào InGame cho Phase B | Giữ bằng chứng tầng 2; không sửa NetSmokeClient |

### 4.11. Câu hỏi kỹ thuật cho G1 (đều có mặc định)

| # | Câu hỏi | Mặc định khuyến nghị |
| --- | --- | --- |
| T-1 | Cho phép thư mục mới `Backend/Telemetry/**` và `Backend/Tests/TelemetryChecks/**`? Spec chỉ khóa `Backend/GameServer/**`, nhưng đây vẫn là cây `Backend` | Cho phép; thêm vào dòng Q11 ở `Agent_Coordination.vi.md` cùng `Tools/Telemetry/**`, `Docs/Telemetry/**`, `Docs/Reports/TEL-01_*`, `Assets/_JungleMaze/**/Telemetry/**` |
| T-2 | Liên kết nguồn (4.0) hay chờ Vinh tạo project/assembly riêng? | Liên kết nguồn: Vinh chỉ thêm ~5 dòng, không có tham chiếu vòng |
| T-3 | Bật client bằng `-telemetry` trên dòng lệnh thay vì thêm trường vào `ClientConfig`? | Dòng lệnh; không sửa config của người khác (G-9) |
| T-4 | Hai phiên backend-dev song song (P, S)? | Có; nếu không thì trễ hạn khoảng 1 ngày |
| T-5 | Harness trong tiến trình (4.6 tầng 2) có được coi là bằng chứng AC33 cho Phase A không, với nhãn "proxy, tải giả"? | Có, cho Phase A; AC33 trên tiến trình thật để điều kiện H1 |
| T-6 | Kích thước ring: critical 16 384, bulk 8 192? | Dùng; chỉnh sau AC34 |
| T-7 | Gửi `GameServer_Hook_Proposal.vi.md` cho Vinh ngay sau commit 1 (trước khi build xong)? | Có, để Vinh có thời gian duyệt song song |

Không có câu hỏi nào chặn việc bắt đầu build. Không cần G2: protocol không đổi.

### 4.12. Kết quả G1

**G1 · 27/10/2026 · CPO** (quyền người dùng giao: "Bạn được quyền quyết thay thôi. ko đợi tôi quyết"): **Duyệt** kế hoạch 4.0–4.10 cùng mọi mặc định T-1…T-7. Spec Q1–Q11 cũng theo mặc định.

- **T-1:** cho phép tạo `Backend/Telemetry/**` và `Backend/Tests/TelemetryChecks/**`. `Backend/GameServer/**` vẫn **khóa**: không sửa một dòng nào. CPO ghi vùng mới vào `Agent_Coordination.vi.md`.
- **T-2:** liên kết nguồn. Phase B chỉ cần Vinh thêm khoảng 5 dòng; TEL-01 không tự thêm.
- **T-3:** bật client bằng cờ dòng lệnh `-telemetry`, không sửa `ClientConfig`.
- **T-4:** P và S chạy song song. Phiên Backend Dev làm commit 1 rồi luồng S; một subagent backend-dev của CPO làm luồng P. Unity Dev làm luồng C. Cả ba bắt đầu sau commit 1.
- **T-5:** harness trong tiến trình được dùng làm bằng chứng AC33 cho Phase A, gắn nhãn "proxy, tải giả". AC33 trên tiến trình thật là điều kiện của H1.
- **T-6:** dùng giá trị đề xuất.
- **T-7:** sau commit 1, CPO ghi một dòng vào `Agent_Coordination.vi.md` báo Vinh đọc `Docs/Telemetry/GameServer_Hook_Proposal.vi.md`. Vinh duyệt độc lập, không chặn Phase A.
- **Không cần G2:** protocol không đổi. Mọi bản ghi không được chứa PII: tên hiển thị, RoomCode, JWT, IP (AC4).

## 5. Nhật ký build

_Dev điền._

### Luồng C (client), commit 7–8 · 27/10/2026 · unity-dev

Commit `cf78b3c9` (commit 7) và `8c12cdc1` (commit 8).

**File** (đúng vùng 4.2 E):

| Lớp | File | Vai trò |
| --- | --- | --- |
| Domain | `Telemetry/FrameStats.cs` | Hai mảng `float[2048]` cấp sẵn; `fpsAvg` = số khung / tổng thời gian; `fps1Low` = k / tổng k khung chậm nhất, k = max(1, ceil(0,01·n)); p95 nearest-rank; max. Sắp xếp tại chỗ bằng quicksort tự viết, vì `Array.Sort` cấp phát dưới Mono (test bắt được) |
| Domain | `Telemetry/PerfClientSample.cs` | Struct đủ trường `perf_client`; null = `NaN` (số) hoặc `-1` (`mt`) |
| App | `Telemetry/ITelemetrySink.cs` | Port `TryWrite(in PerfClientSample)` không chặn; `TelemetryContext` (build, wid, balHash) |
| App | `Telemetry/PerfClientRecorder.cs` | `ITickable`, cửa sổ 5 s chỉ khi đã nối và `Phase == InGame`; đứt kết nối thì bỏ cửa sổ dở. RTT/transport từ `INetworkClient.Stats` (null khi chưa có pong); `jitterMs` từ `RemotePlayerInterpolator.Stats` (null trước khi đồng hồ chạy); `lossPct` = chênh `PongsLost` / chênh `PingsSent` trong cửa sổ (null khi không có ping); `entities` = `Players.Count` |
| Infra | `Telemetry/JsonlTelemetryFileSink.cs` | Ring SPSC 256 ô + `Thread` nền, cứ 50 ms gom một lần; JSON viết tay theo đúng thứ tự khóa của schema; `_header`/`_trailer`; trần 8 MiB → `truncated`; lỗi I/O → tự tắt, một cảnh báo, đếm `droppedIo` |
| Infra | `Telemetry/ClientTelemetryIdentity.cs` | `wid` 8 hex ngẫu nhiên; `balHash` theo đúng cách của `GameDataLoader.DataHash` (tên file UTF-8, rồi nội dung CRLF→LF) trên `weapons.json`, `equipments.json`, `match.json` |
| Infra | `Telemetry/ClientTelemetryOptions.cs` | Đọc `-telemetry` và `-telemetryDir <path>` (mặc định `persistentDataPath/Telemetry`) |
| Bootstrap | `Telemetry/Bootstrap.Telemetry.cs` + 1 dòng `Bootstrap.cs` | Không có `-telemetry` thì không tạo gì. Có cờ: sink đăng ký vào registry (dispose thì gom nốt hàng đợi và ghi `_trailer`), recorder vào `TickRunner` |

**Lệch kế hoạch, quyết định:**
- **Tên file client** `<matchId>.client.<netId>.<wid>.jsonl` (plan 4.2 E ghi `<matchId>.client.<netId>.jsonl`). Thêm `wid` để đúng "mỗi (trận, nguồn, wid) một file" (spec 2.9), và để lần khởi động lại tiến trình không ghi `_header` thứ hai vào giữa file cũ. Rớt rồi nối lại trong cùng tiến trình vẫn ghi tiếp cùng file, `seq` liên tục.
- **`seq`** đếm theo `matchId` ở luồng nền. Bản ghi bị bỏ vì hàng đầy không chiếm `seq` (4.3).
- **`roomId = null`** (client không biết `RoomId`), không có trường `fake`.
- **Recorder bỏ cửa sổ** khi chưa biết `OwnNetId` hoặc `ConfigHash`, hoặc khi sink từ chối; đếm ở `Skipped`.
- Cửa sổ đóng khi tổng thời gian khung ≥ 5 s − 1 ms. Cộng dồn 300 × (1/60) bằng float có thể hụt 5 s một chút, làm mỗi cửa sổ trễ một khung.

**Test EditMode `Tests/EditMode/Telemetry/` (28):**
- `FrameStatsTests` (10): 60 fps; 1% low với 1 khung chậm và với k làm tròn lên; `fpsAvg` lệch ≤ 2% so với đếm độc lập (AC38); bỏ giá trị 0/âm/NaN; khung đứng yên; quá dung lượng; so p95/max/1% low với sắp xếp tham chiếu (3 bộ ngẫu nhiên); 0 cấp phát (AC35).
- `PerfClientRecorderTests` (7), chạy trên `MatchSession` và `RemotePlayerInterpolator` thật: 60 s InGame cho 12 bản ghi (AC38); ngoài InGame 0 bản; đứt kết nối thì bỏ cửa sổ dở; `lossPct` theo cửa sổ và null khi không ping; RTT/transport null; sink tắt hoặc từ chối; 0 cấp phát mỗi khung kể cả khi đóng cửa sổ (AC35).
- `JsonlTelemetryFileSinkTests` (11):
  - Thứ tự khóa header, bản ghi, trailer so với danh sách hằng; so trọn một dòng mẫu: tối đa 3 chữ số thập phân, `NaN` → `null`, không BOM, LF.
  - Trận mới thì file mới và `seq` theo trận (AC26); vượt trần thì `truncated` (AC26).
  - Thư mục là file, và stream ném lỗi hết chỗ: tự tắt, một cảnh báo, không ném (AC25).
  - Luồng ghi bị chặn: 10 000 lần `TryWrite` xong < 200 ms, `Dropped` đúng (AC24).
  - Không có bản ghi thì không tạo thư mục; cờ tắt mặc định (AC27).
  - `TryWrite` không cấp phát.
  - `balHash`: CRLF và LF cho cùng hash, thiếu file thì null, file balance thật cho 64 hex.
  - Đầu cuối recorder → sink: 12 bản ghi, `seq` 1..12; không có tên hiển thị, khóa PII hay IPv4 (AC4).

**Kiểm chéo:** sinh một file mẫu bằng chính sink và `FrameStats` (12 cửa sổ). `Tools/Telemetry/tel_schema.py` (luồng P) báo `validate_files`: 12 bản ghi, **0 lỗi**, thứ tự khóa khớp `ordered()`. `balHash` C# = `015655ea…4d19`, bằng đúng giá trị Python tính độc lập. File mẫu nằm trong thư mục ghi chú chung (ngoài repo) để commit 9 chép vào `fixtures/from_csharp/`.

**Toàn bộ suite:**
- EditMode **507/507** (479 + 28). Lần chạy đầu có 1 lỗi: `ArtAudioCatalogTests.NothingFromImported` vượt timeout 180 s. Lúc đó Unity MCP mất kết nối và Editor bị đứng; chạy riêng lại thì đạt trong 3,4 s.
- PlayMode **106/106**. Lần chạy đầu có 1 lỗi: `HudAcceptanceTests.Hud_HoldsValues10sWithoutUpdates` so float bằng 0 tuyệt đối và nhận 5,6e−7; chạy riêng lại thì đạt. Telemetry không chạy trong test (không có `-telemetry`), nên lỗi này không do TEL-01. Follow-up cho UI-01: dùng dung sai trong test đó.
- Console 0 Error. `EditorSettings.asset` đã `git checkout`.

**Còn lại / follow-up:**
- AC35 phần đo FPS bật/tắt trên máy QA và AC38 chạy thật 60 s: QA làm khi chạy build có `-telemetry` (commit 9).
- Commit 8 lỡ gồm cả một dòng nhật ký của phiên luồng P trong `Agent_Coordination.vi.md`. Đó là dòng của họ, nội dung không đổi; tôi không sửa lịch sử.

### Commit 1 và luồng S (server), commit 5–6 · 27/10/2026 · backend-dev

Commit `8565a4df` (commit 1, schema và tài liệu), `a38773f0` (commit 5), `27ada614` (commit 6). Không file nào trong `Backend/GameServer/**`, `Backend/Shared/**`, `balance/**`, `JungleBackend.sln`, `run_all.cmd` bị sửa (AC29, kiểm bằng `git show --name-only` của ba commit).

**Commit 1 (`Docs/Telemetry/`):**

| File | Nội dung |
| --- | --- |
| `telemetry.schema.v1.json` | Envelope; 18 `types` (mỗi loại có `fields`, `dedupe`, `once`, `source`); `enums`; `idSets`; `extraIds`; `pii`; `rules`; `errorCodes` |
| `Schema.vi.md` | Bảng 18 loại, khớp JSON; dạng dòng `\| n \| \`type\` \| server/client` để test Python so |
| `Hook_Contract.vi.md` | Bảng 18 loại × chủ ticket × điểm gọi × trạng thái; API H7 (`ITelemetrySink`, factory trên `TelemetryEvent`) và 7 quy tắc tại điểm gọi |
| `GameServer_Hook_Proposal.vi.md` | Đề xuất H1, H5, H6, H7-prep kèm diff mẫu và ô ký duyệt của Vinh |

**Commit 5–6 (`Backend/Telemetry/`, namespace `JungleMaze.Telemetry.Server`, nhúng nguồn):**

| File | Vai trò |
| --- | --- |
| `TelemetryEvent.cs` | Struct không cấp phát, 8 factory H7, enum khớp schema |
| `TelemetryRing.cs` | MPSC Vyukov có giới hạn, chỉ `Interlocked`/`Volatile`; nhả tham chiếu `byte[]` sau khi đọc |
| `ITelemetrySink.cs` | `ITelemetrySink`, `NullTelemetrySink`, `ITelemetryAware` (H7-prep) |
| `TelemetryJson.cs` | JSON viết tay, InvariantCulture, tối đa 3 chữ số thập phân, NaN → null |
| `JsonlTelemetryWriter.cs` | Thread nền 50 ms; file `<matchId>.server.<wid>.jsonl`; `_header`/`_trailer`; trần 8/256 MiB; lỗi I/O thì tự tắt, một dòng `[telemetry-error]`; seam `OpenStream` |
| `TelemetryHost.cs` | `FromEnvironment()`/`FromValues()`; tắt khi thiếu `TELEMETRY_PATH`; từ chối khi `PUBLIC_HOSTING=true`; `Attach(Room)`/`Attach(MatchRuntime)`; `Dispose` idempotent |
| `MatchTelemetryObserver.cs` | H2/H3: handler chỉ chép struct vào ring; gắn lại `Health` khi đổi tham chiếu (R3); H5/H6 đăng ký theo tên event lúc `Attach`; là sink H7 của phòng |
| `MatchTelemetryAssembler.cs` | Luồng nền: giải mã bằng `PacketRegistry` riêng; dựng bản ghi 1, 2, 3, 5, 6, 8, 14, 15, 16 và H7 (4, 7, 9–13); ghép `DamageDetail` với `EntityDamaged` |

**`Backend/Tests/TelemetryChecks/`** (không nằm trong sln): `Program.cs` có các chế độ mặc định, `--bench`, `--tick-budget`, `--emit-sample`. `SchemaKeys` đọc chính `telemetry.schema.v1.json` để so tên và thứ tự khóa của mọi dòng C# sinh ra.

**Lệch kế hoạch, quyết định:**

- **Một ring mỗi phòng thay vì hai.** Ring có 16 384 ô; bản ghi bulk (snapshot) chỉ được vào khi ring đang dùng < 8 192 ô, nên luôn chừa ≥ 8 192 ô cho critical (R5 giữ nguyên, T-6 giữ số). Lý do: hai ring riêng làm mất thứ tự giữa snapshot và damage/chết. Thứ tự này cần cho tọa độ `elimination` và `player_spawn`.
- **`TelemetryEvent`** không có `MatchTick` (`mt` tính ở luồng nền từ tick vào InGame). Thêm `Utc`, `D`, `I2`, `I3`, `Ref2` để đủ chỗ cho `DamageDetail` và `EquipChange`.
- **Schema (commit 1), ba chỗ ghi trong `Schema.vi.md`:**
  - `perf_server` được null cả `roomId`, `tick`, `cfg`, `cfgHash`, vì dòng `[metrics]` là số đo cả tiến trình.
  - `player_spawn.sess` = 16 hex SHA-256(`"tel1:"` + PlayerSessionId), vì ở chế độ JWT `PlayerSessionId` là mã tài khoản.
  - `_header`/`_trailer` có thêm `matchId`.
- **H5/H6 bằng reflection theo tên**, một lần lúc `Attach`. Khi Vinh thêm event thì telemetry tự bật, không cần commit TEL-01 nào thêm.
- **Tự gỡ phòng không dùng** (`RoomStaleProbe`): `RoomManager` không có event xóa phòng. Phòng không mặc định, không còn peer và không có tín hiệu trong 15 phút thì bị gỡ observer, đóng file, nhả ring. Không có cơ chế này thì phòng đã hết hạn vẫn giữ khoảng 1 MiB ring.
- **Rời trận chủ động** (`ProcessLeave`, nguồn = chính nạn nhân, không có `GraceExpired`) ghi `cause = OTHER`; `DISCONNECT` chỉ dùng cho hết grace.
- **`replay_choice` chỉ ghi khi có H6.** Không có H6 thì không đoán `TIMEOUT` cho mọi người.
- **Harness tick-budget dùng profile riêng trong tiến trình:** min 2, lobby 0,1 s, bo tốc độ thường, nên 60 s vẫn ở InGame, không ai chết. Bỏ cửa sổ `[metrics]` đầu tiên vì đó là thời gian khởi động.

**Bằng chứng (máy dev: Ryzen 7 5800H, 16 luồng, Windows 11, .NET 8.0.33):**

- **Build:** `dotnet build Backend/JungleBackend.sln` cho 0 cảnh báo, 0 lỗi. `TelemetryChecks` build Debug và Release đều 0 cảnh báo.
- **`dotnet run --project Backend/Tests/TelemetryChecks`: 70/70 PASS.** Gồm:
  - Ring: đầy, bulk chừa chỗ, 4 luồng ghi × 200 000 không mất/trùng và FIFO theo từng luồng, nhả `byte[]`.
  - JSON dưới văn hóa `de-DE`.
  - Writer: header/trailer, `written` = số dòng, `seq` liên tục, trần theo file và tổng.
  - Lỗi đĩa: thư mục là file, chỉ đọc, đầy giữa chừng (AC25).
  - 10 000 `TryWrite` khi writer bị chặn xong < 50 ms (AC24).
  - Host tắt mặc định và `PUBLIC_HOSTING` (AC27).
  - Hai trận `MatchRuntime` thật liên tiếp (AC26, R3), xem phần "Trận thật" bên dưới.
  - Kịch bản H7: đủ 7 loại W, `DamageDetail` gộp thành một `damage`, đòn bị giáp chặn hết ghi `applied = 0`, `elimination` lấy `killer`/`weapon` từ damage cuối, `position_sample.weapon` theo `equip_change`.
  - Gỡ phòng không dùng.
- **Trận thật (proxy AC36, phần S0 của AC37):**
  - **Trận 1:** `match_start` 1; `player_spawn` 4 tại điểm spawn của map; `zone_phase` đủ 16 (5 phase × WARNING/SHRINK_START/SHRINK_END, cộng FINAL); SHRINK_START lệch ≤ 2 tick so với `zone_schedule` × `zoneTimeScale`. `elimination` khớp `MatchResult`: số lượng và `rank`; hết grace cho `DISCONNECT`; chết vì bo cho `ZONE`. `match_end.reason` = `MatchResult.Reason`. Đúng 1 `RESUMED`, không đếm lần xoay token lúc vào trận (X1); đúng 1 `GRACE_EXPIRED`.
  - **Trận 2:** 1 `LOAD_TIMEOUT`; vẫn có damage sau khi `HealthService` được tạo lại (R3).
  - **PII (AC4):** không có tên hiển thị, `RoomCode`, session id thô hay resume token nào trong file.
- **Kiểm chéo với Python:** `--emit-sample` sinh 3 file (289 dòng). Chạy `python -I Tools/Telemetry/telemetry.py validate --strict --pii-sample SECRET_NAME --pii-sample ROOMCODE_SECRET_9Z --pii-sample sess-secret <dir>` cho `files=3 records=283 errors=0 fake=0`, mã thoát 0.
- **`--bench` (Release, AC32):** gọi `TryWrite` qua sink H7 thật của phòng, 1 000 000 lần.

  | Chế độ | p50 | p99 | max | Cấp phát |
  | --- | --- | --- | --- | --- |
  | Ring được rút | 0,1 µs | 0,2 µs | 159 µs | **0 byte**, nhận 1 000 000/1 000 000 |
  | Ring đầy | 0,1 µs | 0,1 µs | 40 µs | **0 byte** |

  Độ phân giải đồng hồ 0,1 µs. Ngưỡng p99 ≤ 10 µs: **đạt**.
- **`--tick-budget --runs 3 --seconds 60 --load 70,1000` (Release, AC33/AC34, proxy, tải giả trong tiến trình):** `FixedTickLoop` và `ServerMetrics` thật, một phòng 10 phiên di chuyển ngẫu nhiên, chạy xen kẽ ABBA. Số lấy từ chính dòng `[metrics]`, trung vị của 4 cửa sổ 10 s mỗi lần.

  | Tải | tickP95 tắt (3 lần) | tickP95 bật (3 lần) | Trung vị (bật − tắt) | tickP99 bật lớn nhất | overruns | droppedTicks | Bản ghi/lần | droppedQueue |
  | --- | --- | --- | --- | --- | --- | --- | --- | --- |
  | ≈ 70/s | 0,081 / 0,066 / 0,080 ms | 0,096 / 0,081 / 0,085 ms | **0,014 ms** | 0,140 ms | 0 / 0 | 0 | ~4 850 | 0 |
  | 1 000/s | 0,086 / 0,104 / 0,081 ms | 0,108 / 0,098 / 0,127 ms | **0,023 ms** | 0,228 ms | 0 / 0 | 0 | ~20 500 | 0 |

  Ngưỡng chênh ≤ 0,5 ms, `tickP95` < 33,3 ms, `droppedTicks` = 0 và overruns bật ≤ tắt: **đạt cả hai mức**. `workingSetMiB` 34–60 ở cả bật lẫn tắt.
- **`dotnet run --project Backend/Tests/MatchChecks`:** 84/84 PASS (GameServer không đổi).
- **Chưa chạy `Backend/Tests/run_all.cmd`** vì cần Docker. Commit này không đổi project nào trong sln hay `run_all.cmd`, nên phần còn lại của AC27 để QA chạy.

**File cho commit 9** nằm trong thư mục ghi chú chung, ngoài repo, ở `TEL-01_streamS/`:

- `from_csharp/server_flow_match1.jsonl`, `server_flow_match2.jsonl`, `server_h7.jsonl`
- `tick_budget.csv`, `tick_budget.log`, `bench.txt`

**Còn lại / follow-up:**

- **AC33 trên tiến trình GameServer thật, AC36 qua mạng:** cần H1 của Vinh. Sau H1 làm commit B1, xóa dòng nhúng nguồn trong `TelemetryChecks.csproj`.
- **AC37 `DROPPED`/`LEFT`/`replay_choice`:** cần H5/H6. Hiện chỉ có phần S0 đã kiểm.
- **AC31:** chờ Vinh ký `GameServer_Hook_Proposal.vi.md`.
- **Commit 9** (QA guide, chép fixture C#, báo cáo mẫu): chưa giao cho phiên này.

### Commit 9 · 27/10/2026 · backend-dev

- **`Docs/Telemetry/QA_Guide.vi.md`:** lệnh chép-dán cho bật server/client, thu file, `perflog`, `validate`, `report`, `compare`, đo tick, kiểm chéo C#.
- **`Tools/Telemetry/fixtures/from_csharp/`:** 3 file server (`--emit-sample`) và 1 file client (luồng C, đổi tên `client_perf.jsonl`). `validate --strict`: `files=4 records=295 errors=0 fake=0`.
- **Test mới `CrossLanguageTests`** trong `test_telemetry.py`. `python -I Tools/Telemetry/test_telemetry.py`: **80 test, OK** (1 skip là test `--perf`).
- **`Docs/Reports/TEL-01_2026-10-27/`:** `seed1/` (`report.md` và 9 CSV, giống từng byte `golden/seed1`), `tick_budget.csv`, `bench.txt`, `README.md`.
- **Đã chạy thử** lệnh `fake` A/B rồi `compare` trong QA guide: mã thoát 0.

### Luồng P (công cụ Python), commit 2–4 · 27/10/2026 · backend-dev

Commit `ccb58d48` (commit 2), `e3d746a8` (commit 3), `970695e1` (commit 4). Cả ba chỉ chạm `Tools/Telemetry/**` (AC29). Python 3.12.10 stdlib, chạy `python -I`.

**File:**

| File | Vai trò |
| --- | --- |
| `telemetry.py` | CLI một cửa: `validate`, `fake`, `merge`, `report`, `compare`, `perflog`. Mã thoát đúng bảng 4.2: 0, 1 (dùng sai lệnh, kể cả lỗi của argparse), 2, 3, 4 |
| `tel_schema.py` | Nạp schema; Code ID đọc lúc chạy từ `idSets` (`--balance-dir` để thay); `validate_record()` trả `{line, code, field, detail}`, không ném lỗi; quét PII; `ordered()`, `dumps()`, `canonical()` |
| `tel_fake.py` | Bộ phát giả xác định theo `seed`: 6 trận × 10 người, đủ 18 loại mỗi trận, hai config A/B, các cờ tiêm lỗi, `inject_manifest.json` |
| `tel_merge.py` | Gộp: trùng kỹ thuật, xung đột, trùng ngữ nghĩa (loại `once`), hở `seq`, đối chiếu `_trailer`, `unterminated` |
| `tel_metrics.py` | Số đo 2.5 theo `cfgKey`: TTK, nguyên nhân chết, bo theo phase, loot, trang bị đầu tiên, chết khi chỉ có HAND, CC, thời gian dùng, ẩn nấp, hiệu năng, độ phủ |
| `tel_report.py` | `report.md` cùng 9 CSV; `compare` cho ra A, B, delta, nA, nB, nhãn mẫu thấp |
| `tel_perflog.py` | `[metrics]` → `perf_server`; `wid` = 8 hex đầu SHA-256 của log |
| `fixtures/valid` (18), `fixtures/invalid` (61), `fixtures/make_fixtures.py` | File sai đặt tên `<mã lỗi>__<mô tả>`: 14 file thiếu trường envelope, 19 file trường liệt kê ngoài miền, 4 `invalid_id` (có `BAD_ID`), 6 PII, và mỗi mã lỗi còn lại ≥ 1 file |
| `fixtures/hand` (7), `fixtures/perflog` | Dữ liệu dựng tay cho AC13–AC15; 3 dòng `[metrics]` đúng chuỗi định dạng của `ServerMetrics.cs` cùng 4 dòng hỏng |
| `golden/seed1` | `report.md` và 9 CSV của `fake --seed 1 --cfg A` |
| `README.md`, `.gitattributes`, `.gitignore` | Tóm tắt lệnh. `eol=lf` để golden so từng byte đúng trên máy có `autocrlf`. Bỏ qua `__pycache__` |

**Quyết định trong lúc làm (không đổi schema):**

- **Xung đột:** mọi biến thể vào `conflicts.jsonl`, báo cáo gắn cờ lỗi. `events.jsonl` giữ biến thể có JSON chuẩn hóa nhỏ nhất, để kết quả không phụ thuộc thứ tự đọc (AC11) và số bản ghi không đổi.
- **cfgKey của trận** lấy từ `match_start` (envelope server), bản ghi client theo `matchId` vào cùng nhóm. Server ghi `balHash = null` nên khóa hiện có dạng `1.0.0/xxxxxxxx/-`; không có file client thì khóa không đổi.
- **`perf_server` nằm ngoài khoảng mọi trận** chỉ được đếm ở một dòng đầu báo cáo, không thành nhóm cfgKey.
- **Chênh thời gian dùng vật phẩm** chỉ tính lượt `DONE`. Lượt `CANCELLED`/`INTERRUPTED` vào tỷ lệ riêng.
- **Bộ phát giả:**
  - `utc` = mốc cố định + tick × 33 ms. Config B lệch một ngày để `perf_server` của A và B không chồng nhau khi gộp chung.
  - `replay_choice` `TIMEOUT` ghi `afterMs = null`, vì không có ack.
  - File client tên `<matchId>.client.<netId>.jsonl`; luồng C ghi thêm `.<wid>`. Gộp không dựa vào tên file.
- **Tốc độ:** mỗi loại có một hàm kiểm sinh mã một lần lúc nạp schema, gồm kiểu, miền, quy tắc chéo và đếm khóa. Hàm này chỉ trả lời "chắc chắn hợp lệ". Có nghi ngờ thì chạy bộ kiểm đầy đủ để ra danh sách lỗi chính xác. Kết quả PII được nhớ theo từng chuỗi; enum và ID kiểm PII một lần lúc nạp. Có test so hai đường trên hơn 3 000 bản ghi và biến thể (fixture, dữ liệu giả, giá trị đột biến): hai đường cho cùng kết luận.
- **Mẫu `perflog`** là `.txt`, vì `.gitignore` gốc chặn `*.log`. Các dòng được dựng từ chuỗi định dạng ở `ServerMetrics.cs:45`, không chép từ log chạy thật (phiên này không chạy GameServer).

**Test:** `python -I Tools/Telemetry/test_telemetry.py` → **79 test, 78 chạy, OK, 1 skip** (`PerfTests`, chỉ chạy khi có `--perf`), khoảng 25 s. Chia theo lớp: Schema 17, ValidateCli 4, FakeGenerator 12, Merge 17, HandMetric 8, Report 13, Perflog 5, Tooling 2. Đã chạy suite đầy đủ trước mỗi commit. Lần cuối chạy lại trên bản `git archive HEAD` sạch: OK.

| AC | Bằng chứng (Python) |
| --- | --- |
| AC1 | Đúng 18 loại, `n` 1–18; bảng mục 3 của `Schema.vi.md` khớp JSON (số thứ tự, bên ghi, một lần, khóa); 18/18 fixture hợp lệ đạt |
| AC2 | 14 fixture thiếu một trường envelope đều bị từ chối; toàn bộ dữ liệu giả 0 lỗi |
| AC3 | `BAD_ID` cho đúng `invalid_id`; dữ liệu giả 0 `invalid_id`; ID đọc từ `weapons.json`/`equipments.json` lúc chạy |
| AC4 | Fixture tên, RoomCode, JWT, IPv4, IPv6, khóa cấm lồng sâu đều bị bắt. Tên hiển thị và RoomCode mẫu của bộ phát giả không xuất hiện ở byte nào của file giả hay báo cáo |
| AC5 | Mỗi trường liệt kê (19) có fixture ngoài miền, bị `bad_enum` |
| AC6 | `--dup` 5%: `dup` = số đã tiêm; `events.jsonl` giống từng byte lần chạy sạch |
| AC7 | `--conflict 3`: 3 dòng `conflicts.jsonl`; `--strict` thoát 2 |
| AC8 | `--semantic-dup 6`: `semantic_dup` = 6; số mỗi loại "một lần" bằng lần chạy sạch |
| AC9 | `--reconnect`: 1 `match_start`, 1 `player_spawn` cho người rớt, đúng `DROPPED` rồi `RESUMED`, `seq` client liên tục, tổng = `expectedMerged` |
| AC10 | `--gap` k = 1, 7, 40 báo đúng k, cả qua `_trailer.written`; mất dòng cuối file vẫn bắt được; lần chạy sạch `written` = số dòng |
| AC11 | Xáo dòng (kể cả header/trailer) và thứ tự file 5 lần: `events.jsonl`, `merge_summary.json` và báo cáo giống từng byte |
| AC12 | `golden/seed1` khớp từng byte; các con số golden (TTK trung vị mỗi vũ khí, % chết vì bo, số loot, số CC) khớp số tính lại |
| AC13 | Dựng tay: một vũ khí; hai kẻ tấn công; nghỉ > 5 s tách chuỗi; nghỉ đúng 150 tick vẫn giữ; đổi vũ khí giữa chuỗi; kết liễu bằng bo không vào TTK; one-shot = 0 s; `--gap-sec` đổi chuỗi |
| AC14 | 10 `elimination`, 3 `ZONE`: 30,0%, tổng các phase = 3, người bị damage bo 6/12 |
| AC15 | Loot, trang bị đầu tiên (trung vị 20 s, 1 người không có), chết khi chỉ có HAND 2/3, CC, thời gian dùng, ẩn nấp: đều so với số viết sẵn |
| AC16 | Gộp A + B: hai nhóm, mỗi nhóm 6 trận, số bản ghi, số loại và `perf` đúng bằng từng tập; TTK nhóm A giống khi chạy riêng A |
| AC17 | `compare`: delta = B − A ở mọi dòng; nhãn mẫu thấp đúng nhóm; cặp dựng tay cho KNIFE 4,0 → 3,5 s (delta −0,5); B ít đòn hạ hơn trên 3 vũ khí được tăng damage |
| AC18 | Bỏ file client, bỏ `perf_server`, chỉ còn client, thư mục rỗng: mã thoát 0, mục tương ứng ghi "không có dữ liệu", đủ 9 CSV |
| AC19 | `--perf`: 50 trận, 415 695 dòng; merge 6,40 s, metrics + report 0,84 s, **tổng 7,24 s** (≤ 10 s). Máy AMD64 Family 25 Model 80, 16 luồng, Python 3.12.10 |
| AC20 | Hai lần chạy seed 1 cho hash từng file bằng nhau; mỗi trận đủ 18 loại ở cả A và B |
| AC21 | Từng cờ ở AC6–AC11, và test gộp mọi cờ cùng lúc |
| AC22 | 100% dòng giả có `fake: true`; `--require-real` thoát 3 (merge, report); dữ liệu thật thoát 0 |
| AC28 | 3 dòng khớp từng trường, `windowSec` 10 / 10 / 10,502, 4 dòng hỏng bị bỏ và đếm, nạp lại cùng log thành trùng kỹ thuật |
| AC30 | Quét `import` của mọi `.py`: chỉ module stdlib hoặc module cục bộ |
| AC39 | `coverage.csv`: dữ liệu giả cho `fake-only` ở cả 18 loại; trộn dữ liệu thật cho `mixed`/`real`/`none` đúng |

**Lệnh đã chạy (kết quả thật):**

- `python -I Tools/Telemetry/test_telemetry.py` → `Ran 79 tests … OK (skipped=1)`.
- `python -I Tools/Telemetry/test_telemetry.py --perf PerfTests` → OK, số đo AC19 ở bảng trên.
- `python -I Tools/Telemetry/telemetry.py validate --strict Tools/Telemetry/fixtures/valid` → `errors=0`, mã thoát 0.
- `python -I Tools/Telemetry/telemetry.py fake --out <tmp> --dup --gap 7 --conflict --semantic-dup --reorder --reconnect`, sau đó `merge <tmp> --out <tmp2> --strict` → `dup=950 semantic_dup=6 conflicts=3 gaps=7`, mã thoát 2.
- `python -I Tools/Telemetry/telemetry.py report <fake seed 1> --out <tmp> --run FAKE-seed1` → mã thoát 0, giống `golden/seed1`.

**Còn lại / follow-up:**

- **AC23** (QA chạy từ checkout sạch ≤ 15 phút) và chép `fixtures/from_csharp/` thuộc commit 9. Commit đó nên thêm vào `test_telemetry.py` một test `validate --strict` trên `fixtures/from_csharp/` (0 lỗi, không có `fake`); hiện test chưa quét thư mục này.
- **Mẫu `perflog` thật:** khi có log GameServer thật (Phase B), chép 3 dòng vào `fixtures/perflog/` để thay dòng dựng.
- Một lần chạy `--perf` đầu tiên, trước khi tối ưu, dừng với `SystemError` trong bộ mã hóa `_json`. Hai lần chạy sau không lặp lại; chưa rõ nguyên nhân.

## 6. Review

### Review TEL-01 Phase A · 27/10/2026 · code-reviewer

**Phạm vi review:** `git diff f77c031e..00cdb6db` (8565a4df, ccb58d48, e3d746a8, 970695e1, a38773f0, 27ada614, cf78b3c9, 8c12cdc1, 00cdb6db và nhật ký), bỏ qua f374fd49, 4d53acf1, 9387e0b3 (ART-05, UI-01). Một lượt tập trung theo lệnh CPO, không chạy lại bộ test dài.

**Kết quả: Qua.** Chặn 0 · Nên sửa 1 · Gợi ý 4.

**Đã kiểm và đạt**
1. **Không đụng vùng khóa.** Không có file nào dưới `Backend/GameServer/**` hay `Backend/Shared/**`; protocol không đổi. Phần backend mới nằm trong `Backend/Telemetry/**` và `Backend/Tests/TelemetryChecks/**`; `TelemetryChecks.csproj` biên dịch nguồn bằng `Compile Include="../../Telemetry/**"`. Mọi hook vào GameServer vẫn là đề xuất Phase B (`MatchRuntime` chưa có `PlayerDisconnected`/`PlayerLeft`/`ReplayAcked`, observer tìm theo tên và tắt khi không thấy).
2. **Đường tick không khóa, không cấp phát, đầy thì bỏ và đếm.**
   - `TelemetryRing.TryWrite` là hàng đợi Vyukov: chỉ `Volatile`/`Interlocked`, không `lock`, không chờ; đầy (`diff < 0`) thì tăng `_droppedCritical`/`_droppedBulk` và trả false. Bulk (snapshot) bị chặn ở nửa ring nên chừa chỗ cho bản ghi critical.
   - `MatchTelemetryObserver`: delegate tạo một lần trong constructor, bản ghi là struct, `DateTime.UtcNow.Ticks` không cấp phát, frame `byte[]` là bản sao mới của `MatchRuntime` (`writer.CopyData()`), nên ring giữ tham chiếu không bị ghi đè. Mọi I/O, JSON và `lock` nằm ở luồng nền (`JsonlTelemetryWriter`, `_pumpGate` chỉ ở `Register`/`Unregister`).
   - Lỗi I/O tự tắt writer và observer thôi đẩy; đã đối chiếu số trong nhật ký (p99 0,2 µs, 0 byte cả khi ring đầy).
3. **Không có PII trong bản ghi.**
   - Assembler không đọc `DisplayName`, `ProfileKey`, `RoomCode` hay resume token (`_onResume` bỏ token ngay). `roomId` là `Guid` riêng (`RoomManager.cs:46,115`), không phải mã phòng.
   - `sess` là 8 byte đầu SHA-256 của `"tel1:" + sessionId`, không ghi session id thô. Client chỉ ghi id, tick, số liệu khung hình/RTT.
   - `tel_schema.py` còn chặn khóa và giá trị PII (có fixture `pii__*` và `--pii-sample`).
4. **Client tắt hẳn khi không có `-telemetry`.** `ClientTelemetryOptions.FromArgs` mặc định tắt; `ComposeTelemetry` thoát trước khi tạo sink, luồng, file hay tickable. `Bootstrap.cs` chỉ thêm đúng 1 dòng (`ComposeTelemetry(...)`, `Bootstrap.cs:168`); phần còn lại ở `Bootstrap.Telemetry.cs`. `ClientConfig` không đổi. Sink client là SPSC ring 256 ô: đầy hoặc tắt thì `TryWrite` trả false và đếm; luồng chính không chờ.
5. **Ranh giới Clean Architecture của client đúng.** `Domain/Telemetry` chỉ dùng `System` (asmdef `noEngineReferences`); `Application/Telemetry` (cổng `ITelemetrySink`, `PerfClientRecorder`) chỉ dùng Core, Domain, Protocol và không có `UnityEngine`; `Infrastructure/Telemetry` (file sink, options, identity) tham chiếu Application; composition ở Bootstrap. Không có asmdef nào bị đổi.
6. **Dedupe và xung đột (`tel_merge.py`).**
   - Đọc logic: khóa kỹ thuật `(matchId, src, wid, seq)`; cùng nội dung chỉ đếm `dup`, khác nội dung thì vào `conflicts.jsonl`. Biến thể giữ lại là biến thể có JSON chuẩn tắc nhỏ nhất nên kết quả không phụ thuộc thứ tự file/dòng. Việc tách khe `slot[3]` và đếm lại bản trùng sau khi tách đúng.
   - Khe seq tính trên khóa kỹ thuật trước khi bỏ trùng ngữ nghĩa nên bản trùng ngữ nghĩa vẫn chiếm một seq; trailer mỗi file chỉ tính một lần.
   - `once` chỉ áp cho loại có `once: true`; xung đột được báo ở `report.md` và với `--strict` thì thoát mã 2.
   - Đã chạy `python -I test_telemetry.py -k Merge`: 17 test OK.
   - Phía server, `match_start`, `match_end`, `player_spawn`, `elimination` có cờ một lần (`_startEmitted`, `_endEmitted`, `_spawned`, `_eliminated`); `OnReplayAcked` có `_replayDone`; resume do `StartInGame` gây ra bị loại bằng `_startEmitted`.

**Nên sửa**
1. **Drop của trận A bị tính nhầm sang trận B** — `MatchTelemetryAssembler.cs:122-142` (`Pump`/`AttributeDrops`) và `:297-301` (`OnPhase` `Cleanup`).
   - Mô tả: `Pump` rút hết ring, và trong lúc đó `OnPhase(Cleanup)` gọi `CloseFile()` (ghi `_trailer`) rồi mới đến `AttributeDrops()` ở cuối `Pump`. Lúc đó `_file == null` nên phần drop phát sinh từ lần `AttributeDrops` trước tới Cleanup đi vào `_carryDrops`, và `EnsureFile` cộng nó vào file của trận kế tiếp. (Hai đường Stale và `Complete` gọi `AttributeDrops` trước `CloseFile`, nên không bị.)
   - Kịch bản lỗi: ring đầy ở cuối trận A (đúng lúc cần số đếm nhất) thì `_trailer.droppedQueue` của A thấp hơn thực tế còn trận B bị cộng thừa; `merge` cộng đúng tổng nhưng báo cáo theo trận và AC "không mất mà không đếm" theo từng file sai.
   - Hướng sửa: gọi `AttributeDrops()` ngay trước `CloseFile()` trong `OnPhase(Cleanup)` (hoặc đưa vào chính `CloseFile`), thêm một test kiểm trailer của file đóng bằng Cleanup khi ring đầy.

**Gợi ý**
1. **Race nhỏ khi gỡ observer** — `MatchTelemetryObserver.cs:84,112,126-129`: `OnFrame` qua được `IsDetached` rồi `Detach()` (luồng nền) chạy xong, sau đó `RebindHealth()` gắn handler vào `HealthService` mới. `Push` vẫn chặn bằng `IsDetached` nên không ghi sai, chỉ rò một vài handler đến khi phòng bị thu. Có thể kiểm lại `IsDetached` sau `RebindHealth()` rồi gỡ ngay.
2. **Bằng chứng 0 byte (AC32) chỉ đo `TryWrite`** — `Bench.cs:64-79`. Các handler thật trên luồng tick (`OnFrame`, `Push`, `_onDamaged` gọi `_match.Health.GetHp`, `RebindHealth`) không nằm trong phép đo; đọc code thì không cấp phát, nhưng một test `GC.GetAllocatedBytesForCurrentThread` quanh vài tick `MatchRuntime` thật có observer sẽ khóa điều đó. Với H7, `TelemetryEvent.LootDrop` buộc người gọi cấp một `TelemetryLootItem[]`: hợp đồng Phase B nên nêu "dùng mảng dựng sẵn, không `new` trên đường tick".
3. **Chưa có chốt chống bản ghi session đôi** — `MatchTelemetryAssembler.cs:595-620` (`OnResume`, `OnDisconnected`, `OnLeft`, `OnGraceExpired`). Hiện H5/H6 chưa tồn tại trong GameServer nên chưa xảy ra; khi Vinh thêm sự kiện mà phát hai lần thì mỗi lần ra một `session_state` seq khác nhau và `merge` không bỏ được (`session_state` có `once: false`). Thêm `if (_disconnected[n]) return;` ở `OnDisconnected` và chốt tương tự cho `Left`/`GraceExpired`.
4. **`tel_merge.py` giữ nguyên văn mọi dòng đã nhận** (`text_key`, `:60,68,94`). Với trần 256 MiB ở server thì Python có thể tốn vài trăm MB RAM; không vấn đề ở quy mô QA, ghi chú nếu sau này gộp cả tháng log.

**Chưa kiểm chứng**
- Không chạy Unity, nên EditMode (client) và việc `-telemetry` tắt thật trong player là theo đọc code và số của dev. `Bootstrap.Telemetry.cs` không có test composition (chỉ `FromArgs` được test); QA nên chạy player không cờ và xác nhận không có thư mục `Telemetry`, không luồng `TelemetrySink`.
- Không chạy lại `TelemetryChecks` (`--bench`, `--tick-budget`); số p99, 0 byte và `Δ tickP95` lấy từ nhật ký mục 5. Đó là proxy tải giả trong tiến trình (G1 T-5), không phải GameServer thật.
- Không đọc hết 782 dòng assembler: đã đọc kỹ `Pump`, vòng đời trận, session/replay, elimination, damage; không soi từng bản ghi H7 (4, 7, 9–13) và `OnSnapshotFrame`/zone ngoài phần đã nêu.
- Chưa mở được log GameServer thật để so `perflog`; mẫu `perflog` vẫn là dòng dựng (đã ghi ở mục 5).

## 7. QA

_QA điền._

## 8. Nghiệm thu

_CPO điền._
