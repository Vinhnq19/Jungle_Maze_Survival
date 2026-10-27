# Telemetry schema v1 (TEL-01)

Cập nhật 27/10/2026. Tài liệu này dành cho người đọc. Bản máy đọc là [telemetry.schema.v1.json](telemetry.schema.v1.json). Hai file phải khớp nhau, và `Tools/Telemetry/test_telemetry.py` kiểm điều đó: danh sách loại trong bảng ở mục 3 phải trùng `types` của JSON, cả về số thứ tự lẫn tên. Spec và lý do chọn nằm ở [TEL-01](../Tickets/TEL-01.md) mục 2.3–2.5. Hợp đồng gọi sink cho từng chủ ticket xem [Hook_Contract.vi.md](Hook_Contract.vi.md).

## 1. Định dạng file

- **JSONL:** mỗi dòng một đối tượng JSON, UTF-8 không BOM, xuống dòng LF.
- **Thứ tự khóa khi ghi:** các trường envelope theo thứ tự ở mục 2, sau đó các trường riêng theo thứ tự của `fields` trong JSON. Bộ kiểm không phụ thuộc thứ tự khóa, nhưng mọi bộ ghi (C# server, C# client, Python fake) giữ đúng thứ tự này để diff file dễ đọc.
- **Số:** số nguyên ghi không có phần thập phân. Số thực ghi tối đa 3 chữ số thập phân với văn hóa bất biến. Không bao giờ ghi `NaN` hay `Infinity`; giá trị không hợp lệ ghi `null` nếu trường cho phép, không thì bỏ cả bản ghi.
- **Một file cho mỗi (trận, nguồn, `wid`):**
  - Server ghi `${TELEMETRY_PATH}/<yyyy-MM-dd>/<matchId>.server.<wid>.jsonl`.
  - Client ghi `persistentDataPath/Telemetry/<matchId>.client.<netId>.jsonl`, mở chế độ nối thêm. Lần chạy mới có `wid` mới.
  - Bộ phát giả và `perflog` tự đặt tên trong thư mục đầu ra.
- **Dòng đầu là `_header`, dòng cuối là `_trailer`** (mục 4). Hai dòng này không tính vào 18 loại và không có `seq`.
- **Trần dung lượng:** 8 MiB mỗi file và 256 MiB tổng (G-8). Khi chạm trần, bộ ghi ngừng ghi bản ghi mới, vẫn ghi `_trailer` với `truncated: true`.

## 2. Envelope: trường chung của mọi bản ghi

Mọi trường đều bắt buộc có khóa. "null được" nghĩa là khóa có mặt với giá trị `null`.

| Trường | Kiểu | Null được | Ý nghĩa |
| --- | --- | --- | --- |
| `v` | int, luôn `1` | không | Phiên bản schema |
| `type` | string | không | Một trong 18 tên ở mục 3 |
| `matchId` | guid | chỉ `perf_server` | `MatchRuntime.MatchId`, viết thường dạng `8-4-4-4-12` |
| `roomId` | string ≤ 64 | `perf_server`, `perf_client` | `RoomId` của server. Client không biết `RoomId` nên ghi `null`. **Không bao giờ** ghi `RoomCode` |
| `src` | `server` \| `client` | không | Phải bằng cột "Bên ghi" ở mục 3 |
| `wid` | 8 hex | không | Mã ngẫu nhiên của một lần chạy bộ ghi (mỗi tiến trình một mã) |
| `seq` | uint ≥ 1 | không | Tăng 1 theo (`matchId`, `src`, `wid`), bắt đầu từ 1. Bản ghi bị bỏ vì hàng đợi đầy **không** chiếm `seq` |
| `tick` | uint | chỉ `perf_server` | `MatchRuntime.CurrentTick` lúc sự kiện xảy ra. Client ghi tick của snapshot mới nhất |
| `mt` | uint | có | Tick tính từ lúc vào InGame (0 = tick vào InGame); `null` trước InGame |
| `utc` | int64 | không | Mili-giây epoch UTC của máy ghi. Chỉ để tham khảo; thứ tự dùng `tick` |
| `build` | string ≤ 32 | không | Bản build của bên ghi; `dev` khi chưa có nguồn version (BLD-01) |
| `cfg` | string ≤ 32 | chỉ `perf_server` | `ConfigVersion` của trận (`match_settings.json` `version`) |
| `cfgHash` | 64 hex | chỉ `perf_server` | `ConfigHash` của server (`GameDataLoader.DataHash`) |
| `balHash` | 64 hex | có | Hash balance phía client (mục 5). Server ghi `null` tới khi WPN-02 đưa balance lên server |
| `fake` | `true` | khóa có thể vắng | Chỉ có ở dữ liệu giả. Dữ liệu thật **không bao giờ** có khóa này |

**Vì sao `perf_server` được null nhiều hơn spec:** spec 2.3 nói `matchId` là ngoại lệ duy nhất. Nhưng dòng `[metrics]` là số đo cả tiến trình: không có phòng, không có `CurrentTick` của trận, không có config. Vì vậy `perf_server` để `roomId`, `tick`, `cfg`, `cfgHash` là `null`. `merge` gắn nó vào trận theo khoảng `utc` (TEL-01 mục 4.3). `perflog` được phép điền `cfg`/`cfgHash` khi người chạy truyền vào.

**Khóa config của báo cáo:** `cfgKey = cfg + "/" + cfgHash[0:8] + "/" + (balHash[0:8] hoặc "-")`. Không bao giờ trộn hai `cfgKey` trong cùng một nhóm số đo.

## 3. 18 loại bản ghi

Cột "Nguồn":

- **S0:** bắt được từ server hiện tại mà không sửa GameServer.
- **S+:** cần hook nhỏ của Vinh ([GameServer_Hook_Proposal.vi.md](GameServer_Hook_Proposal.vi.md)).
- **W:** cần hệ thống chưa có. Tới lúc đó loại này chỉ có từ bộ phát giả.
- **C:** ghi ở client.

Cột "Một lần" = `once` trong JSON: `merge` chỉ giữ bản đầu theo khóa ngữ nghĩa và đếm `semantic_dup`. Với loại không "một lần", khóa ngữ nghĩa chỉ để tham khảo.

| # | `type` | Bên ghi | Nguồn | Một lần | Khóa ngữ nghĩa |
| --- | --- | --- | --- | --- | --- |
| 1 | `match_start` | server | S0 | có | matchId |
| 2 | `match_end` | server | S0 | có | matchId |
| 3 | `player_spawn` | server | S0 (`loadout` W) | có | matchId, netId |
| 4 | `attack` | server | W | không | matchId, netId, tick, weapon, kind |
| 5 | `damage` | server | S0 mức thô (trường vũ khí/giáp W) | không | matchId, tick, victim, attacker, weapon, hazard |
| 6 | `elimination` | server | S0 (`killer`, `weapon`, `hidden` W) | có | matchId, victim |
| 7 | `cc_span` | server | W | không | matchId, victim, startTick, ccKind |
| 8 | `zone_phase` | server | S0 | có | matchId, phase, state |
| 9 | `loot_drop` | server | W | không | matchId, container, x, y, tick |
| 10 | `item_pickup` | server | W | không | matchId, netId, tick, item |
| 11 | `item_use` | server | W | không | matchId, netId, startTick, item |
| 12 | `equip_change` | server | W | không | matchId, netId, tick, slot, action |
| 13 | `hide_span` | server | W | không | matchId, netId, startTick |
| 14 | `position_sample` | server | S0 (`weapon` W) | không | matchId, netId, tick |
| 15 | `session_state` | server | S0 (`DROPPED`, `LEFT`, `transport` S+) | không | matchId, netId, kind, tick |
| 16 | `replay_choice` | server | S+ | có | matchId, netId |
| 17 | `perf_server` | server (qua `perflog`) | S0 | không | utc |
| 18 | `perf_client` | client | C | không | matchId, netId, wid, seq |

### 3.1. Trường riêng từng loại

Ký hiệu: `?` = null được. `netId` luôn là uint 0–9. Tọa độ `x`, `y`, `cx`, `cy` là đơn vị ô của map (float). ID vũ khí, trang bị là Code ID của GDD (mục 6).

| # | `type` | Trường riêng (theo thứ tự ghi) |
| --- | --- | --- |
| 1 | `match_start` | `profile` (`main`/`test`), `players` (0–10), `minPlayers` (1–10), `seedHash` (16 hex) |
| 2 | `match_end` | `reason` (`LastAlive`/`Tie`/`Timeout`/`Cancelled`), `winnerNetId?`, `durTicks`, `alive` (0–10) |
| 3 | `player_spawn` | `netId`, `sess` (16 hex, mục 5), `x`, `y`, `loadout` (mảng ≤ 8 ID, rỗng khi chưa có ITM-01) |
| 4 | `attack` | `netId`, `weapon`, `kind` (`MELEE`/`SHOT`/`THROW`/`SPECIAL`), `x`, `y`, `ammo?` |
| 5 | `damage` | `victim`, `attacker?`, `cause`, `weapon?`, `hazard?`, `raw`, `applied`, `absorbed?`, `hpAfter` (0–255), `helmetDur?`, `armorDur?` |
| 6 | `elimination` | `victim`, `killer?`, `cause`, `weapon?`, `hazard?`, `rank` (1–10, chép từ server), `survTicks`, `x`, `y`, `zonePhase` (−1–4), `hidden?` |
| 7 | `cc_span` | `victim`, `source?`, `by` (ID vũ khí/trang bị/hazard), `ccKind` (`STUN`/`ROOT`/`REVERSE`), `startTick`, `endTick`, `plannedMs`, `end` (`EXPIRED`/`CLEANSED`/`DEATH`/`MATCH_END`) |
| 8 | `zone_phase` | `phase` (−1–4), `state` (`WARNING`/`SHRINK_START`/`SHRINK_END`/`FINAL`), `cx`, `cy`, `r`, `targetR` |
| 9 | `loot_drop` | `container` (`CHEST_WOODEN`/`POT_CLAY`/`AIRDROP_CRATE`/`CORPSE`), `by?`, `items` (≤ 8 phần tử `{id, qty}`, qty 1–999), `x`, `y` |
| 10 | `item_pickup` | `netId`, `item`, `qty` (1–999), `from` (`CONTAINER`/`FLOOR`/`CORPSE`/`AIRDROP`), `x`, `y` |
| 11 | `item_use` | `netId`, `item` (ID trang bị), `startTick`, `endTick`, `plannedMs`, `result` (`DONE`/`CANCELLED`/`INTERRUPTED`), `hpBefore`, `hpAfter` |
| 12 | `equip_change` | `netId`, `action` (`EQUIP`/`UNEQUIP`/`DROP`/`BREAK`/`SWAP`), `slot` (`WEAPON`/`HELMET`/`ARMOR`), `item`, `from?` |
| 13 | `hide_span` | `netId`, `startTick`, `endTick`, `bush` (string ≤ 32), `x`, `y`, `end` (`EXIT`/`DEATH`/`MATCH_END`), `attackedWhileHidden` |
| 14 | `position_sample` | `netId`, `x`, `y`, `hp` (0–255), `weapon?`, `inZone` |
| 15 | `session_state` | `netId`, `kind` (`DROPPED`/`RESUMED`/`GRACE_EXPIRED`/`LEFT`/`LOAD_TIMEOUT`), `transport?` (`UDP`/`WSS`), `graceMs?` |
| 16 | `replay_choice` | `netId`, `choice` (`REPLAY`/`TIMEOUT`/`DISCONNECTED`), `afterMs?` |
| 17 | `perf_server` | `windowSec`, `ticksPerSecond`, `tickP95Ms`, `tickP99Ms`, `loopP95Ms`, `loopP99Ms`, `tickOverruns`, `droppedTicks`, `cpuOneCorePercent`, `workingSetMiB`, `peers`, `active`, `rooms` |
| 18 | `perf_client` | `netId`, `windowSec`, `frames`, `fpsAvg`, `fps1Low`, `frameMsP95`, `frameMsMax`, `transport?`, `rttP50?`, `rttP95?`, `jitterMs?`, `lossPct?` (0–100), `entities` (0–10) |

### 3.2. Quy tắc ngữ nghĩa từng loại

- **`match_start`**
  - Phát ở frame phase InGame đầu tiên của `matchId`.
  - `players` = số người khi vào InGame.
  - `seedHash` = 16 hex đầu của SHA-256 trên UTF-8 của `matchId` (dạng chữ thường `8-4-4-4-12`).
  - `mt` = 0.
- **`match_end`**
  - `reason` lấy từ `MatchResultPacket.Reason`. Server không phát `None`.
  - `winnerNetId` = `null` khi `WinnerNetId` = 255.
  - `durTicks` = `tick` lúc kết thúc − tick vào InGame.
  - `alive` = số người còn sống.
- **`player_spawn`:** một bản cho mỗi người ở đầu trận. Tọa độ là điểm spawn. Resume **không** phát lại.
- **`damage`:** một bản mỗi lần HP giảm.
  - `raw` = lượng sát thương trước giáp; ở mức S0 là tham số `damage` của `HealthService`.
  - `applied` = HP thực sự mất (`hpBefore − hpAfter`), nên luôn `applied ≤ raw`.
  - `absorbed` = phần giáp/mũ chặn; `null` tới WPN-02.
  - Phân loại ở mức S0:

    | Tín hiệu từ `HealthService` | Ghi thành |
    | --- | --- |
    | nguồn 255 | `cause = ZONE`, `hazard = "ZONE"` |
    | nguồn = chính nạn nhân, kèm `GraceExpired` cùng tick | `cause = DISCONNECT` |
    | nguồn = chính nạn nhân, không có `GraceExpired` (rời trận chủ động) | `cause = OTHER` |
    | nguồn khác | `cause = WEAPON`, `attacker = nguồn`, `weapon = null` tới WPN-02 |

- **`elimination`:** đúng một bản cho mỗi người bị loại.
  - `cause` và `killer` lấy theo `damage` cuối cùng của nạn nhân.
  - `rank` và `survTicks` chép từ roster của server, **không tính lại**.
  - `zonePhase` = `phase` của bản ghi `zone_phase` gần nhất có `tick` ≤ tick chết; −1 nếu chưa có.
  - `hidden` = `null` tới khi có hệ thống bụi.
- **`zone_phase`:**
  - Bo có 5 lần thu, đánh số `phase` 0–4 (= `index − 1` trong `zone_schedule.json`).
  - Bốn trạng thái:

    | `state` | Khi nào |
    | --- | --- |
    | `WARNING` | Bắt đầu cảnh báo trước lần thu `phase` |
    | `SHRINK_START` | Bo bắt đầu thu |
    | `SHRINK_END` | Bo thu xong |
    | `FINAL` | Đúng một lần khi bán kính về 0, sau `SHRINK_END` của phase 4 |

  - `r` = bán kính hiện tại, `targetR` = bán kính đích.
  - Giá trị −1 được schema giữ cho tương thích spec; server hiện không phát.
- **`position_sample`:**
  - Một bản mỗi `1 / sampleHz` giây cho mỗi người còn sống. Mặc định 1 Hz, kẹp 0,5–2 Hz.
  - `inZone` = vị trí nằm trong vòng an toàn hiện tại. Bán kính 0 thì mọi chỗ đều là ngoài bo, giống `ZoneSimulation.IsOutside`.
- **`session_state`:**
  - `RESUMED` chỉ phát khi trận đã vào InGame và người chơi nối lại. `ResumeTokenRotated` lúc vào trận **không** phải resume (TEL-01 4.9 X1).
  - `LOAD_TIMEOUT` = bị loại vì không tải xong trong pha Loading.
  - `graceMs` = thời gian grace còn lại khi `DROPPED`, hoặc thời gian đã vắng khi `RESUMED`.
- **`replay_choice`:**
  - `REPLAY` khi `ProcessReplayAck` thành công.
  - Hết pha Ending mà chưa ack thì ghi `TIMEOUT`, hoặc `DISCONNECTED` nếu người đó đang mất kết nối.
  - `afterMs` = thời gian từ lúc vào Ending tới lúc ack; `null` với `DISCONNECTED`.
- **`perf_server`:**
  - Mỗi dòng `[metrics]` của `ServerMetrics` cho một bản.
  - `utc` = giá trị `utc=` của dòng log, đổi sang mili-giây epoch.
  - `windowSec` = hiệu `utc` với dòng trước; dòng đầu dùng tham số `--interval` (mặc định 10).
  - Các khóa thừa trong log (`pending`, `ticks`, `pings`, `invalidPackets`, `historyUnavailable`, `rejected`, `roomSlots`) bị bỏ qua.
  - `perflog` nên đặt `wid` = 8 hex đầu của SHA-256 nội dung file log. Nhờ vậy nạp lại cùng một log cho ra bản trùng kỹ thuật thay vì bản ghi mới.
- **`perf_client`:**
  - Một bản mỗi cửa sổ 5 s, chỉ trong InGame.
  - `entities` = số người trong snapshot.
  - `lossPct` = `null` khi cửa sổ không có ping nào.

### 3.3. Quy tắc chéo trường (`rules` trong JSON)

Mục `rules` của JSON chứa hai dạng quy tắc:

- **Dạng `if`/`then`:** khai báo, ví dụ `cause = ZONE` thì `attacker`, `weapon` phải `null` và `hazard` phải là `"ZONE"`.
- **Dạng `check`:** kiểm có tên. Bộ kiểm cài từng quy tắc theo `id`: `src-matches-type`, `span-order` (`endTick ≥ startTick`), `applied-le-raw`.

Vi phạm bất kỳ quy tắc nào cho mã lỗi `rule_violation` kèm `id` của quy tắc.

| `cause` (damage/elimination) | `attacker`/`killer` | `weapon` | `hazard` |
| --- | --- | --- | --- |
| `WEAPON` | thường có (S0 luôn có) | ID vũ khí hoặc `null` tới WPN-02 | `null` |
| `ZONE` | `null` | `null` | `"ZONE"` |
| `HAZARD` | `null` | `null` | `LAVA` / `SPIKE_PATCH` / `SWAMP` |
| `DISCONNECT` | `null` | `null` | `null` |
| `OTHER` | tùy | tùy | tùy |

## 4. `_header` và `_trailer`

| Bản ghi | Trường (theo thứ tự) |
| --- | --- |
| `_header` | `v`, `type: "_header"`, `schema: "telemetry.v1"`, `src`, `wid`, `matchId?`, `build`, `opened` (utc ms), `fake` (chỉ dữ liệu giả) |
| `_trailer` | `v`, `type: "_trailer"`, `src`, `wid`, `matchId?`, `written`, `droppedQueue`, `droppedIo`, `truncated`, `closed` (utc ms), `fake` (chỉ dữ liệu giả) |

- **`written`:** số dòng bản ghi đã ghi vào file này, không tính `_header`/`_trailer`. Khi file đủ, `written` bằng `seq` lớn nhất trong file.
- **`droppedQueue`:** số bản ghi bị bỏ vì hàng đợi đầy. **`droppedIo`:** số bản ghi bị bỏ vì lỗi đĩa hoặc chạm trần.
- **File thiếu `_trailer`** (tiến trình bị kill): `merge` báo `unterminated` nhưng vẫn gộp.
- **`matchId` ở `_header`/`_trailer`:** spec không yêu cầu, thêm vào để file đổi tên vẫn gộp đúng trận.

## 5. Định danh và hash (không có PII)

- **Không bao giờ ghi:** tên hiển thị, `RoomCode`, JWT, ticket, credential Guest, `ProfileKey`, resume token, IP hay địa chỉ.
- **Bộ kiểm PII** từ chối các khóa trong `pii.forbiddenKeys` (không phân biệt hoa thường, ở mọi độ sâu). Nó cũng quét mọi giá trị chuỗi theo `pii.patterns` (JWT, IPv4, IPv6). Mã lỗi là `pii`.
- **`sess`:** 16 hex đầu của SHA-256 trên UTF-8 của chuỗi `"tel1:" + PlayerSessionId`.
  - Spec 2.4 ghi thẳng `PlayerSessionId`. Nhưng ở chế độ JWT, `PlayerSessionId` là mã tài khoản cố định (`SessionHub`), nên schema chỉ ghi bản hash một chiều.
  - Hash vẫn ổn định qua các trận của cùng một phiên, nên vẫn tính được "chơi lại".
  - Ghép file client với server dùng (`matchId`, `netId`), không cần `sess`.
- **`cfgHash`:** chép nguyên `ConfigHash` của server. Đó là SHA-256 chữ thường của `map_demo.json`, `match_settings.json`, `zone_schedule.json`; mỗi file băm tên file rồi nội dung đã đổi CRLF sang LF.
- **`balHash`:** cùng cách băm như `cfgHash`, áp lên `weapons.json`, `equipments.json`, `match.json` trong `Assets/StreamingAssets/JungleMaze/balance/`, theo đúng thứ tự đó. Mỗi file: UTF-8 của tên file, rồi UTF-8 của nội dung đã đổi CRLF sang LF. Kết quả là 64 hex chữ thường.
- **`wid`:** 8 hex ngẫu nhiên, tạo một lần mỗi tiến trình ghi.

## 6. Code ID

- **Vũ khí:** 18 `id` trong `balance/weapons.json` (`HAND` … `SAHUR_BAT`). **Trang bị:** 16 `id` trong `balance/equipments.json` (`WOODEN_HELMET` … `COMPASS`).
- **Đọc lúc chạy:** bộ kiểm đọc hai file này mỗi lần chạy, schema không chép danh sách. Như vậy không có bảng config thứ hai.
- **ID đề xuất cho thứ GDD chưa đặt (G-6), nhóm theo `idSets`:**

  | Nhóm | ID |
  | --- | --- |
  | `ammo` | `AMMO` |
  | `container` | `CHEST_WOODEN`, `POT_CLAY`, `AIRDROP_CRATE`, `CORPSE` |
  | `hazard` | `LAVA`, `SPIKE_PATCH`, `SWAMP`, `ZONE` |
  | `bush` | `BUSH` |

  Chờ ITM-01/CLI-04 xác nhận (Q8).
- **ID lạ:** trường kiểu `id` mang ID không thuộc các tập ở `ids` của nó thì bị lỗi `invalid_id`.

## 7. Bộ kiểm và mã lỗi

- **Đầu ra:** bộ kiểm trả danh sách lỗi `{line, code, field, detail}` và không ném ngoại lệ.
- **Mã lỗi** (trùng `errorCodes` trong JSON):

  | Mã | Nghĩa |
  | --- | --- |
  | `bad_json` | Dòng không phải JSON hợp lệ |
  | `unknown_type` | `type` không thuộc 18 loại, `_header` hay `_trailer` |
  | `missing_field` | Thiếu khóa bắt buộc |
  | `unknown_field` | Có khóa không khai trong schema |
  | `null_not_allowed` | Giá trị `null` ở trường không cho phép |
  | `bad_type` | Sai kiểu JSON, hoặc sai định dạng guid/hex |
  | `bad_const` | Khác giá trị cố định (`v`, `schema`, `fake`) |
  | `out_of_range` | Ngoài `min`/`max` |
  | `too_long` | Chuỗi dài hơn `maxLen` |
  | `too_many_items` | Mảng dài hơn `maxItems` |
  | `bad_enum` | Giá trị ngoài miền liệt kê |
  | `invalid_id` | Code ID lạ |
  | `rule_violation` | Vi phạm quy tắc chéo trường (mục 3.3) |
  | `pii` | Có khóa cấm hoặc chuỗi giống JWT/IP |

- **Kiểu `guid`/`hexN`** chỉ nhận chữ thường.
- **Số nguyên:** `true`/`false` không được coi là số. Với `uint`, `1.0` bị coi là `bad_type`.

## 8. Định nghĩa số đo dùng chung

Python, C# server và C# client dùng chung các định nghĩa sau:

- **Trung vị:** `statistics.median`. Số chẵn phần tử thì lấy trung bình hai phần tử giữa.
- **Phân vị p90/p95/p99:** nearest-rank, phần tử thứ `ceil(q · n)` (đánh số từ 1) của dãy đã sắp tăng. Giống `ServerMetrics.Percentile`.
- **`fpsAvg`:** số khung / tổng thời gian khung của cửa sổ.
- **`fps1Low`:** lấy `k = max(1, ceil(0,01 · n))` khung chậm nhất, rồi tính `k / tổng thời gian của k khung đó` (G-7).
- **Số đo cân bằng:** TTK, tỷ lệ chết vì bo, trang bị đầu tiên, thời gian dùng, CC, ẩn nấp theo đúng định nghĩa ở TEL-01 mục 2.5. `gapSec` mặc định 5 s (150 tick); nhóm có n < 10 gắn "mẫu thấp".

## 9. Đổi schema

- **Thêm trường không bắt buộc hoặc thêm giá trị liệt kê:** vẫn là v1, nhưng sửa JSON và tài liệu này trong cùng một commit, rồi chạy lại test của cả ba bên.
- **Đổi nghĩa, xóa hay đổi tên trường:** tăng lên v2, tạo `telemetry.schema.v2.json` mới; bộ đọc phải nhận cả hai phiên bản trong thời gian chuyển.
