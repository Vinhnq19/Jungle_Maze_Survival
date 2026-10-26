# AUD-01 — AudioManager và phản hồi âm thanh trong game

| Trường | Giá trị |
| --- | --- |
| Giai đoạn / Deadline | Giai đoạn 2 / 30/10/2026 (ô Deadline = 46325, Excel serial) |
| Module | Audio |
| Ưu tiên | P1 |
| PIC | V.Huy |
| Phụ thuộc | Chính thức: CLI-03 (Done). Làm song song bằng âm thanh tạm ("Làm song song: dùng placeholder để test event bus trước khi ART-05 nộp asset cuối"). Nguồn sự kiện thật, **không chặn** ticket này (xem mục 2): WPN-01 · WPN-02 · ITM-01 · Game-10 · ZON-01 · SRV-03 · DRG-01 · FOW-01: đều Not Yet. ART-05 (kiểm kê file, license, metadata): Not Yet, P2, phụ thuộc None |
| Trạng thái | Đã duyệt G1 (27/10, CPO thay người dùng), chờ build |
| Sheet GDD tham chiếu | Dòng AUD-01 trong PROJECT TRACKING (khớp dòng người dùng dán). Ref ghi "Assets!11–16; WIREFRAME!13–15" là **số dòng trong sheet** (lệch +2 so với cột STT): ứng với **Assets STT 9–14** (9 Bước chân/Tương tác môi trường · 10 Dùng vũ khí/Bom/Bẫy · 11 Gây/Nhận sát thương/Bị loại · 12 Nhặt/Đổi/Dùng đồ · 13 Cảnh báo/Kết quả/Kết nối · 14 Nhạc nền) và **WIREFRAME STT 11–13** (11 Gây/Nhận sát thương · 12 Nguy hiểm sắp xảy ra · 13 Loot/Hành động/Trạng thái). Số liệu thêm: Weapons (STT 6, 11–18), Equipments (STT 7–16), Obstacles (STT 12–14), MATCH DESIGN DATA (`visionRadius`, `zoneWarningTime`, `targetFPS`), Hero (STT 13), PROJECT TRACKING (ART-05, UI-03, DRG-01, ZON-01, WPN-01, WPN-02, ITM-01, Game-10, FOW-01, SRV-03). Đầu vào kiến trúc: `Docs/Tickets/AUD-01_proposal.vi.md` (27/10/2026) |
| Figma node | Không có. Wireframe v2 không vẽ phần âm thanh; tín hiệu âm thanh bám WIREFRAME STT 11–13 và UI-01 phần 2 (`Docs/Tickets/UI-01-P2.md`: các tín hiệu hình đi kèm) |

## 1. Mục tiêu

Người chơi **nghe** được những gì họ cần biết khi chơi: bắn, chém, trúng đòn, bị trúng, nhặt và dùng đồ, bom sắp nổ, cảnh báo bo (3 chuông), rồng xuất hiện, kết thúc trận. Có một `AudioManager` duy nhất sở hữu audio runtime: AudioMixer (Master, SFX, và Music nếu BGM được duyệt), một **pool AudioSource có giới hạn**, API `PlaySfx(eventId, position)`, và bảng ánh xạ **26 SFX** từ sự kiện của game sang clip. Âm thanh dùng tạm (file JSurviv sẵn có hoặc tiếng bíp) để demo được ngay; khi ART-05 nộp asset cuối thì chỉ thay clip trong catalog, không viết lại code, và sự kiện server gửi lặp không phát trùng. Nhạc nền (BGM) chỉ dựng sẵn bộ điều phối, mặc định tắt, bật/tắt theo quyết định GDD và không che cảnh báo. HUD, gameplay và mạng **không** gọi thẳng `AudioSource`: chỉ có một cổng `IAudioService`. Đây là việc **audio runtime**, không có giao diện cài đặt (UI-03) và không chuẩn bị/kiểm định file (ART-05).

## 2. Phạm vi và ranh giới

**Trong phạm vi:**

1. **AudioMixer và bus** (G-1): `Master` → `SFX` → `Alert` (nhóm con của SFX, cho âm cảnh báo), và `Music` (con của Master). Giao diện người dùng chỉ có 3 thanh Master / SFX / Music (UI-03 vẽ). API đặt và đọc âm lượng từng bus (0–1), quy đổi sang dB.
2. **Pool AudioSource có giới hạn** (mặc định 16, G-2), có ưu tiên 4 mức; khi đầy thì cắt âm ưu tiên thấp nhất, âm ưu tiên cao không bị cắt bởi âm thấp.
3. **API** `PlaySfx(eventId, position)` (vị trí là tọa độ thế giới tính bằng cell, có bản không vị trí cho âm 2D của người chơi local) trên cổng `IAudioService`. Âm có vị trí giảm theo khoảng cách từ Hero (G-3).
4. **Catalog 26 SFX** (`SfxId` → danh sách clip, âm lượng, cao độ, ưu tiên, bus, phạm vi nghe, số bản tối đa cùng lúc, cooldown). Thay clip chỉ sửa catalog/asset, không sửa code.
5. **Bộ ánh xạ sự kiện → SfxId** (`SfxRouter`) nghe các port/sự kiện **đã có** (bảng 26 SFX, cột "Nguồn") và một **cổng mock** cho các sự kiện chưa có nguồn. Router không tạo luồng dữ liệu riêng; HUD và gameplay không biết router.
6. **Chống phát trùng** khi cùng một sự kiện tới nhiều lần (server gửi lặp, snapshot lặp, reconnect) và **chống spam** (cooldown mỗi SFX, số bản tối đa).
7. **`MusicDirector`** (BGM): mặc định **tắt** (G-4, Q2); khi bật đổi nhạc theo màn hình (menu/phòng chờ/loading/trận/kết quả), chuyển mượt, tự nhỏ khi có cảnh báo.
8. **Demo/công cụ thử** trong Editor/Development build (phím F6): phát từng SFX, thanh trượt âm lượng, số AudioSource đang dùng, nút spam, nút gửi lặp sự kiện, công tắc BGM.
9. Âm thanh **tạm**: dùng file JSurviv khi có, tiếng bíp (mỗi SFX một tần số khác nhau) khi thiếu. File gốc `Assets/Imported/JSurviv/` **không bị sửa**.

**Ngoài phạm vi (thuộc ticket khác):**

- Kiểm kê file, chất lượng, độ dài, âm lượng chuẩn hóa, **license/nguồn**, đặt tên và metadata clip, thay clip tạm bằng asset cuối → ART-05. AUD-01 chỉ **đọc** clip qua catalog
- Giao diện cài đặt âm lượng (ESC menu, thanh trượt), lưu lựa chọn người dùng nếu UI-03 quyết định lưu (Q9) → UI-03. AUD-01 chỉ cung cấp API đặt/đọc âm lượng
- Nhạc, tiếng nút bấm và hiệu ứng riêng của Menu/Lobby/Kết quả → UI-02/UI-03 (SFX 26 mã không gồm tiếng UI; `button.mp3` để dành cho UI)
- Logic tạo sự kiện: bắn/chém/ném/nạp/trúng (WPN-01, WPN-02), nhặt/dùng/mặc đồ (ITM-01), hiệu ứng khống chế (Game-10), lịch bo (ZON-01), người còn sống/bị loại (SRV-03), rồng (DRG-01), bẫy/bom (WPN-01), luật "ai được nghe ai" (FOW-01, Hero STT 13). AUD-01 chỉ **nghe** và đổi thành âm
- Âm thanh môi trường nền, âm khi stun/root/độc/hóa giải bắt đầu, cửa đá, portal, vỡ rương/chum, hồi sinh (Tiên dược sự sống), kết nối/mất kết nối: **không thuộc 26 mã**, cần ticket hoặc đợt mở rộng sau (Q1)
- VFX và tín hiệu hình tương ứng (hit marker hình, banner bom) → UI-01 phần 2, VFX-01
- Đo cảm giác "hay/dở" của nhạc và âm: cần người nghe (AC-A28)

**Điều kiện nguồn sự kiện** (kiểm trong code ngày 27/10/2026):

- ✔ **Có sẵn trong code client:** `IHudState.ItemPicked`, `HitDealt`, `DamageTaken`, `DangerChanged` (+ `GetDangerAt`), `Changed` (giáp/mũ đổi); `IInventoryState.UseChanged`; `IEventBus` (`Core/Events/EventBus.cs`); `AppScreenChanged`. **Nhưng chưa có chủ ghi thật** (WPN-02, ITM-01, WPN-01 đều Not Yet), nên các port này chỉ có dữ liệu khi dùng công cụ thử/mock của UI-01.
- ◐ **Có gói mạng, chưa có bộ chuyển thành sự kiện cho client:** `EventPacket` (`PlayerHit`, `PlayerDied`, `ItemPickedUp`, `MatchEnded`; `Tick`, `SourceNetId`, `TargetNetId`), `ZoneStatePacket.InWarning` + `PhaseIndex` (qua `MatchSession.Zone`), `MatchEntityEvent.Eliminated`, `PlayerStatePacket.IsDead`. `GameStates` hiện chỉ dùng `MatchStarted`/`MatchEnded` để chuyển màn hình.
- ✖ **Chưa có nguồn (dùng mock trong demo):** bước chân (không có event, chỉ có vị trí snapshot), tiếng vào bụi/đầm lầy, chém/bắn/ném/nạp/hết đạn của **người khác** và của mình (WPN-01), bẫy kích hoạt, rồng gầm (DRG-01), bị loại của người khác ở vị trí.

**Mâu thuẫn và khoảng trống GDD/code đã xử lý:**

- GDD **không có danh sách 26 SFX**: dòng ticket chỉ nêu ví dụ ("bước chân, chém/bắn, hitmarker, trúng đòn, cảnh báo bo 3 chuông, rồng gầm, nhặt đồ..."); Assets STT 9–14 chỉ ghi nhóm. Danh sách 26 mã do **designer chốt dựa trên đề xuất `AUD-01_proposal.vi.md`**, chờ PIC duyệt ở G1 (Q1).
- GDD ghi "BGM nếu bật": Assets STT 14 trạng thái "Chưa chốt phạm vi". Mặc định tắt (Q2).
- "Cảnh báo bo 3 chuông": GDD không ghi khoảng cách giữa các tiếng chuông và thời điểm phát. `zoneWarningTime` = 10 s trước mỗi lần co (MATCH DESIGN DATA). Giả định G-8.
- "Tiếng rồng nhận ra được": DRG-01 chưa có (Not Yet), ART-05 chưa nộp rồng. Dùng tiếng tạm (Q4).
- Giấy phép file JSurviv: `Docs/JSurviv_Integration.vi.md` và `Docs/JSurviv_Verification.vi.md` **không** nhắc giấy phép âm thanh. AUD-01 chỉ dùng làm âm tạm, xác minh thuộc ART-05 (Q5).
- Thư mục JSurviv có hai file `.zip` không dùng trực tiếp được trong Unity: `bot di chuyển.zip` (thực chất chứa 8 file "Leather Armor Put On"; tên zip không khớp nội dung) và `pick-impacts-….zip` (1 file `Pick_Impacts_ODY-0409-030.wav`). Giải nén thuộc ART-05.
- `Gun Put On.mp3` và `Helmet Put On.mp3` cùng kích thước 82 616 byte: có thể trùng nội dung, ART-05 kiểm. Spec chỉ dùng làm tạm.

### 2.1. Bảng 26 SFX

Chú thích cột: **Ưu tiên** Critical (không bao giờ bị cắt), High, Normal, Low. **Nghe trong** là bán kính tính bằng cell tính từ Hero (G-3): "2D" = chỉ người chơi local, không phụ thuộc khoảng cách; số = âm có vị trí, hết nghe ở khoảng cách đó. **Tối đa** = số bản cùng lúc trên toàn pool. **Cooldown** = khoảng cách tối thiểu giữa hai lần phát cùng SfxId **cùng nguồn** (không có nguồn: toàn cục). Mọi số trong bảng này là **giả định G-5**, chờ G1. **Nguồn**: ✔ có port/event; ◐ có gói mạng; ✖ chưa có (mock).

| # | SfxId | Nhóm GDD | Sự kiện kích hoạt (nguồn) | File tạm | Ưu tiên | Nghe trong | Tối đa | Cooldown |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | `FootstepGrass` | Assets 9 | Hero di chuyển trên nền thường, mỗi 1,5 cell một bước (suy từ vị trí snapshot; ✖ chưa có bộ suy, G-7) | Bíp (`bot di chuyển.mp3` 10 KB là ứng viên chưa nghe, ART-05 kiểm) | Low | 6 | 6 | 250 ms |
| 2 | `FootstepSwamp` | Assets 9 / Obstacles 12 | Như trên khi đang trong Đầm lầy (✖ cần dữ liệu địa hình hoặc `EffectKind.Slow`; Game-10/ENV) | Bíp | Low | 6 | 6 | 300 ms |
| 3 | `BushEnter` | Assets 9 / Obstacles 9 | Hero chui vào Bụi rậm (✖ ENV/FOW-01) | Bíp | Low | 2D (Q7) | 2 | 500 ms |
| 4 | `MeleeSwing` | Assets 10 / Weapons 1–6, 18 | Hero vung vũ khí cận chiến (✖ WPN-01) | `melee chém.mp3` | Normal | 8 | 4 | 150 ms |
| 5 | `MeleeHit` | Assets 10 | Đòn cận chiến trúng mục tiêu (✖ WPN-02/WPN-01) | `Đập.wav` | Normal | 8 | 4 | 100 ms |
| 6 | `ShootRanged` | Assets 10 / Weapons 7, 8, 10 | Bắn Ná cao su / Quyền trượng / Shotgun (✖ WPN-01) | `súng bắn.mp3` | High | 12 | 6 | 80 ms |
| 7 | `ShootCrossbow` | Assets 10 / Weapons 9 | Bắn Nỏ (✖ WPN-01) | `súng bắn.mp3` hạ cao độ, hoặc bíp | High | 12 | 3 | 300 ms |
| 8 | `Throw` | Assets 10 / Weapons 11–15 | Ném bom/vật phẩm ném (✖ WPN-01) | Bíp | Normal | 8 | 3 | 300 ms |
| 9 | `Reload` | Assets 10 / ITEM LIST 11 | Bắt đầu nạp đạn bằng R (✖ WPN-01; có `IHudState.Ammo` nhưng không báo "bắt đầu nạp") | Bíp | Low | 6 | 2 | 500 ms |
| 10 | `EmptyClick` | Assets 10 / WPN-01 ("hết băng không tự nạp") | Bắn khi hết đạn trong băng (✖ WPN-01) | Bíp | Low | 2D | 1 | 300 ms |
| 11 | `BombFuse` | Assets 10 / WIREFRAME 12 / Weapons 11–15 | Bom có ngòi được kích (**✔** `IHudState.DangerChanged`, `Type=Added`, `Kind=FusedBomb`; vị trí = `SourceX/Y`, chỉ khi `HasSource`) | `countdown.mp3` | High | 12 | 3 | 200 ms |
| 12 | `Explosion` | Assets 10 / Weapons 11, 13–15 | Bom hết ngòi và nổ (**✔** `DangerChanged`, `Type=Expired`, `Kind=FusedBomb`) | `bomb nổ.mp3` | High | 12 | 3 | 80 ms |
| 13 | `TrapTriggered` | Assets 10 / WIREFRAME 12 / Weapons 11 (vỏ chuối), Obstacles 14 (ổ gai) | Bẫy kích hoạt (✖ WPN-01; `DangerKind.Trap` chỉ báo "có bẫy", chưa báo "đã kích hoạt", Q12) | `Đập.wav` hạ cao độ, hoặc bíp | Normal | 8 | 3 | 150 ms |
| 14 | `HitMarker` | Assets 11 / WIREFRAME 11 | Người chơi local gây sát thương lên Hero khác (**✔** `IHudState.HitDealt`, `Target=Hero`) | `button.mp3` (tiếng tách ngắn) | Critical | 2D | 1 | 60 ms |
| 15 | `HurtLocal` | Assets 11 / WIREFRAME 11 | Người chơi local bị sát thương (**✔** `IHudState.DamageTaken`; kể cả không có hướng) | `bleed.mp3` | Critical | 2D | 2 | 100 ms |
| 16 | `ArmorAbsorb` | Assets 11 / Equipments 1–6 | Sát thương nhận vào bị giáp/mũ hấp thụ: `DamageTaken` **và** độ bền giáp/mũ giảm trong ≤ 100 ms (**✔** `DamageTaken` + `Changed`, Q14) | Bíp (`Pick_Impacts_….wav` trong zip là ứng viên) | High | 2D | 2 | 100 ms |
| 17 | `Eliminated` | Assets 11 / WIREFRAME 8 | Một Hero bị loại (◐ `EventPacket.PlayerDied`, `MatchEntityEvent.Eliminated`, `PlayerStatePacket.IsDead`; chưa có bộ chuyển thành sự kiện). Bản thân bị loại: 2D; người khác: có vị trí | Bíp (`revive.mp3` không dùng vì nghĩa ngược) | High | 2D (bản thân) / 12 (người khác) | 2 | 500 ms |
| 18 | `PickupWeapon` | Assets 12 / ITEM LIST 1–10, 12–19 | Nhận một vũ khí (**✔** `IHudState.ItemPicked`, id thuộc `weapons.json`) | `nhặt súng.mp3` | Normal | 2D | 2 | 150 ms |
| 19 | `PickupItem` | Assets 12 / ITEM LIST 11, 26–35 | Nhận đạn, dược phẩm, hoặc món không phải vũ khí/giáp (**✔** `ItemPicked`, phân loại theo `equipments.json`/ID đạn) | `Gun Put On.mp3` | Normal | 2D | 3 | 100 ms |
| 20 | `WearArmor` | Assets 12 / Equipments 1–6 | Giáp hoặc mũ đang mặc **đổi món** (từ không → có, hoặc món này → món khác) (**✔** `IHudState.Changed` cờ Armor/Helmet, id mới khác id cũ và khác rỗng) | `Helmet Put On.mp3` | Normal | 2D | 2 | 200 ms |
| 21 | `UseHeal` | Assets 12 / Equipments 7–11 | Bắt đầu dùng dược phẩm hồi máu (**✔** `IInventoryState.UseChanged`, `Phase=Using`, `type=Healing`) | `gauze.mp3` | Normal | 2D (Q8) | 1 | 300 ms |
| 22 | `UseDrink` | Assets 12 / Equipments 12–14, 16 | Bắt đầu dùng món hỗ trợ (Nước tăng lực, Thần dược, Gậy thần, La bàn) (**✔** `UseChanged`, `Phase=Using`, `type=Support/Airdrop`) | `cola.mp3` | Normal | 2D | 1 | 300 ms |
| 23 | `UseCancel` | Assets 12 / Hero 9 | Dùng đồ bị hủy (**✔** `UseChanged`, `Phase=Cancelled`) | Bíp | Normal | 2D | 1 | 200 ms |
| 24 | `ZoneBell3` | Assets 13 / WIREFRAME 13 / dòng ticket | Bo sắp co: cảnh báo bắt đầu, **ba tiếng chuông** (◐ `ZoneStatePacket.InWarning` false→true; `PhaseIndex` làm khóa; `MatchSession.Zone` có sẵn nhưng chưa có sự kiện) | Chuông bíp tổng hợp 3 tiếng (`Bo co.mp3` là âm bo co, không dùng cho cảnh báo) | Critical, bus Alert | 2D toàn cục | 1 | 1 lần mỗi `PhaseIndex` |
| 25 | `DragonRoar` | Assets 13 / DRG-01 | Rồng xuất hiện/thả trứng (✖ DRG-01, `dragonFlyDuration` = 10 s, mốc 2:00/4:00/5:30) | Âm trầm quét tần số tổng hợp (khác mọi bíp khác) | Critical, bus Alert | 2D toàn cục | 1 | 5 s |
| 26 | `MatchEnd` | Assets 13 / WIREFRAME 9 | Trận kết thúc (**✔** `AppScreenChanged` → `Results`; hoặc `EventPacket.MatchEnded`) | `TimeOver.wav` | Critical | 2D | 1 | 5 s |

**Tổng kết nguồn:** ✔ 12 mã (11, 12, 14–16, 18–23, 26; trong đó 14–16 và 18–23 chỉ có dữ liệu khi UI-01 mock hoặc chủ ghi thật được nối) · ◐ 2 mã (17, 24) · ✖ 12 mã (1–10, 13, 25) dùng mock. Tổng 26.

**Nhạc nền (nếu bật, Q2):** `Waiting Room Music.wav` (25,7 MB, WAV không nén, **cần ART-05 nén**) · `Loading Music.mp3` (1,4 MB) · `ParkingMapMusic.mp3` (4,9 MB). Ánh xạ màn hình mặc định (G-9): `MainMenu`, `InRoom`, `Results` → Waiting Room; `Connecting` → Loading; `InMatch` → ParkingMap; `Boot` → im lặng.

**Số liệu từ GDD và code:**

| Tham số | Giá trị | Nguồn |
| --- | --- | --- |
| Số SFX | 26 (danh sách do designer chốt theo proposal) | Dòng AUD-01 ("26 SFX"); `AUD-01_proposal.vi.md` |
| Tầm nhìn | 12 cell (cơ sở cho phạm vi nghe tối đa) | MATCH DESIGN DATA `visionRadius` |
| Thời gian cảnh báo bo | 10 s trước mỗi lần co | MATCH DESIGN DATA `zoneWarningTime` |
| Ngòi bom | Chuối/Thiên thạch/Tinh thể/Hạt 1,5 s; Bom khói 1,0 s | Weapons STT 11–15; `weapons.json` `fuseSeconds` |
| Nhịp bắn | Ná 3/s, Quyền trượng 2/s, Nỏ 1,2/s, Shotgun 0,8/s; cận chiến CD 0,45–1,5 s | Weapons STT 1–10 (cơ sở cho cooldown) |
| Thời gian dùng đồ | 0,5–3,5 s (9 món) | Equipments STT 7–14, 16; `equipments.json` `useSeconds` |
| Tốc độ Hero | 4 cell/s (suy ra bước chân 1,5 cell/bước ≈ 2,7 bước/s) | MATCH DESIGN DATA `heroBaseSpeed` |
| Mục tiêu FPS | 60 fps | MATCH DESIGN DATA `targetFPS` |
| Tick server | 30 Hz | `GameplayProtocol.TickRate` |
| Số AudioSource trong pool | 16 | **Giả định G-2** (proposal ghi "khoảng 16") |
| Cửa sổ chống lặp | 150 ms cho sự kiện không có khóa; khóa sự kiện nhớ 2 s (tối đa 128 khóa) | **Giả định G-6** (proposal ghi "khoảng 150 ms") |
| Âm lượng → dB | `dB = 20·log10(v)`, `v = 0` → −80 dB | Quy ước Unity AudioMixer; **giả định G-1** |
| Hạ nhạc khi có cảnh báo | −9 dB trong ≤ 0,2 s, trả lại trong ≤ 1,0 s sau khi cảnh báo kết thúc | **Giả định G-10** |
| Chuông bo | 3 tiếng, cách nhau 0,8 s ± 0,05 | **Giả định G-8** |
| Giảm theo khoảng cách | Tuyến tính: gain = 1 − d/R (d = khoảng cách cell tới Hero, R = "nghe trong") | **Giả định G-3** |
| Chuyển nhạc | Crossfade 1,0 s | **Giả định G-9** |

## 3. Tiêu chí nghiệm thu

Ưu tiên: P0 phải đạt để qua G3; P1 nên đạt, bỏ phải có PIC đồng ý; P2 không chặn. Số đánh dấu giả định (G-1…G-10) có thể đổi ở G1. Phần "Ánh xạ tiêu chí GDD" dưới bảng cho biết mỗi AC thuộc tiêu chí nào. "Âm tạm" = file JSurviv hoặc bíp. Mọi AC đo bằng sự kiện giả (cổng mock và công cụ F6), không cần gameplay thật.

### 3.1. Phát SFX theo event ID, giới hạn AudioSource, điều khiển âm lượng (tiêu chí GDD 1)

| # | Tiêu chí (đo được) | Cách kiểm | Ưu tiên |
| --- | --- | --- | --- |
| AC-A1 | Catalog có đủ **26** mã `SfxId` (không tính `None`), mỗi mã có ≥ 1 clip khác rỗng, bus, ưu tiên, phạm vi nghe, số bản tối đa và cooldown đúng bảng 2.1. Thiếu clip hoặc sai thuộc tính thì test fail | EditMode test đọc catalog | P0 |
| AC-A2 | `PlaySfx(id, vị trí)` với mỗi trong 26 mã phát **đúng một** AudioSource trong frame đó (hoặc frame kế tiếp), clip thuộc catalog của mã đó, đúng bus (Alert cho `ZoneBell3`, `DragonRoar`; SFX cho các mã còn lại). Với mã "2D" thì phát bất kể vị trí truyền vào | PlayMode test | P0 |
| AC-A3 | Pool bị chặn trên: gửi 100 yêu cầu phát trong một frame (hỗn hợp mọi ưu tiên) → số AudioSource đang phát ≤ 16 ở mọi frame; không tạo thêm AudioSource ngoài pool (đếm `GameObject` AudioSource trước/sau). Không exception | PlayMode test | P0 |
| AC-A4 | Ưu tiên khi pool đầy: pool đầy bằng 16 âm Low, rồi gửi `HitMarker` (Critical) → `HitMarker` phát trong cùng frame và đúng một âm Low bị cắt. Pool đầy bằng 16 âm High/Critical, gửi âm Low → âm Low **không** phát và không cắt âm nào. Không bao giờ có âm Critical bị cắt bởi âm thấp hơn | PlayMode test | P0 |
| AC-A5 | `SetVolume(bus, v)` cho Master/SFX/Music/Alert: tham số mixer tương ứng = `20·log10(v)` dB ± 0,1 (v = 0,5 → −6,02 dB; v = 1 → 0 dB; v = 0 → −80 dB). Giá trị ngoài [0, 1] bị kẹp. `GetVolume` trả đúng v vừa đặt (± 0,001). Đổi âm lượng Master áp cho mọi bus con (đo bằng mức ra mixer hoặc tham số dB) | PlayMode test (`AudioMixer.GetFloat`) | P0 |
| AC-A6 | Giới hạn mỗi SFX (cột "Tối đa" và "Cooldown"): gửi `FootstepGrass` 10 lần từ cùng một nguồn trong 100 ms → 1 bản (cooldown 250 ms); từ 10 nguồn khác nhau cùng lúc → ≤ 6 bản (tối đa). Sau cooldown thì phát lại được. Kiểm một mã mỗi nhóm: `FootstepGrass`, `ShootRanged`, `Explosion`, `PickupItem`, `HitMarker` | EditMode test (bộ đếm thời gian giả lập) | P0 |
| AC-A7 | Giảm theo khoảng cách (G-3): âm có vị trí với R = 12 (`ShootRanged`): Hero ở (0,0), nguồn ở (0,0) → gain = 1,0; ở (6,0) → 0,50 ± 0,05; ở (12,0) hoặc xa hơn → **không** tạo AudioSource. Âm 2D (`HurtLocal`) không đổi theo khoảng cách. Khoảng cách tính Euclid theo cell, Hero lấy từ `IHudState.Pose`; không có Hero (`HasPose` = false) thì âm có vị trí không phát, âm 2D vẫn phát | EditMode test | P0 |
| AC-A8 | Có công cụ thử (F6) trong Editor/Development build, **không** có trong Release build: nút phát từng trong 26 SFX, thanh trượt Master/SFX/Music, hiển thị số AudioSource đang dùng/tối đa, nút "spam 100 sự kiện", nút "gửi lặp sự kiện" (AC-A13), công tắc BGM. Bấm nút phát đúng mã thì có âm | Review + PlayMode | P0 |
| AC-A9 | Âm tạm đủ 26 mã: mỗi mã có clip (JSurviv hoặc bíp). Hai mã **không** dùng chung cùng một clip khi chúng cần phân biệt: kiểm `HitMarker`, `HurtLocal`, `ZoneBell3`, `DragonRoar`, `MatchEnd`, `Explosion` đôi một khác nhau. Các bíp có tần số khác nhau. File dưới `Assets/Imported/JSurviv/` không bị sửa (git diff trống ở thư mục này) | EditMode test (so tham chiếu clip) + review diff | P0 |

### 3.2. Thay asset không viết lại code, không phát trùng khi server gửi lặp (tiêu chí GDD 2)

| # | Tiêu chí (đo được) | Cách kiểm | Ưu tiên |
| --- | --- | --- | --- |
| AC-A10 | Thay clip chỉ sửa asset/catalog: thay clip của một `SfxId` bằng clip khác (hoặc thêm biến thể) rồi chạy lại AC-A1…A9 → vẫn xanh, và diff của lần thay không có file `.cs` hay test nào. Catalog hỗ trợ nhiều clip cho một mã: chọn ngẫu nhiên, **không** lặp liên tiếp cùng một clip khi có ≥ 2 clip; cao độ biến thiên ± 5% (G-5) | EditMode test + review diff | P0 |
| AC-A11 | Chống phát trùng sự kiện có khóa: sự kiện `EventPacket` giả (`Tick`, `EventType`, `SourceNetId`, `TargetNetId`) hoặc `ZoneState` (`PhaseIndex`) tới 5 lần liên tiếp (trong 150 ms và lặp lại sau 1 s) → phát **đúng 1 lần**. Cùng loại nhưng khác `Tick` thì phát riêng. Khóa được nhớ ≥ 2 s và tối đa 128 khóa (khóa cũ nhất bị bỏ khi đầy) | EditMode test | P0 |
| AC-A12 | Chống phát trùng sự kiện không khóa (HUD, bus): hai `HitDealt`, hai `ItemPicked` cùng id hoặc hai `DamageTaken` tới trong cùng cửa sổ 150 ms (G-6) → vẫn tuân theo cooldown của mã (AC-A6), không phát hai lần. Hai `ItemPicked` khác id trong cùng frame đều phát (hai món khác nhau là hai sự kiện) | EditMode test | P0 |
| AC-A13 | Công cụ thử "gửi lặp": nhấn một lần tạo 5 bản sao của cùng sự kiện server; AudioSource đang phát tăng tối đa 1 (hoặc 0 nếu trong cooldown) | PlayMode test | P0 |
| AC-A14 | Ánh xạ sự kiện **đã có** (cột "Nguồn" ✔) đúng và đủ, đo bằng cách ghi qua port ghi của UI-01 (`IHudStateWriter`, `IInventoryStateWriter`): `NotifyPickup("CROSSBOW", 1)` → `PickupWeapon`; `NotifyPickup(ID đạn, 12)` → `PickupItem`; `NotifyHitDealt(Hero)` → `HitMarker`; `NotifyHitDealt(Other)` → **không** phát (Q6); `NotifyDamageTaken(…)` → `HurtLocal`, và nếu độ bền giáp/mũ giảm trong ≤ 100 ms thì thêm `ArmorAbsorb` (cả hai cùng phát); `SetDanger(Id, FusedBomb, 1,5 s, hasSource, x, y)` → `BombFuse` tại (x, y); khi nổ (`Expired`) → `Explosion` tại (x, y); `ClearDanger` (`Removed`) → **không** có `Explosion`; `BeginUse` món Healing → `UseHeal`, món Support/Airdrop → `UseDrink`; `CancelUse` → `UseCancel`; `CompleteUse` → không phát (Q8); `SetArmor`/`SetHelmet` đổi món (không → có, hoặc khác id) → `WearArmor`; `AppScreenChanged` → `Results` → `MatchEnd` | EditMode test (router + port thật hoặc store) | P0 |
| AC-A15 | Không phát khi chỉ khởi tạo/đặt lại: `IHudStateWriter.Reset()`, `SetArmor`/`SetHelmet` lần đầu ở trạng thái khởi tạo của trận, `SetArmor(null)` (cởi), độ bền giáp **giảm** mà không kèm sát thương: **không** phát `WearArmor` hay `ArmorAbsorb`. Vào scene Game rồi nhận trạng thái đầu tiên: không có âm nào | EditMode test | P0 |
| AC-A16 | Router chỉ phụ thuộc cổng sự kiện (`IHudState`, `IInventoryState`, `IEventBus`, các sự kiện mạng/zone đã có), **không** tham chiếu vào code của WPN-01/02, ITM-01, Game-10, DRG-01 hay kiểu mạng ngoài Application. Nguồn thật (khi có) nối vào cùng cổng, không phải sửa router (kiểm bằng cách thay nguồn mock bằng hiện thực thứ hai của cổng trong test: kết quả giống nhau) | EditMode test + review | P0 |
| AC-A17 | Sự kiện chưa có nguồn (✖) phát được qua cổng mock `IAudioEventSource` (hoặc tên tương đương do tech-lead đặt): mock gửi 12 mã ✖ (1–10, 13, 25) thì đều phát đúng clip tạm; mã chưa có nguồn không phát khi **không** có ai gọi (không tự phát bằng thời gian). Công cụ F6 dùng chính cổng này | EditMode + PlayMode test | P1 |
| AC-A18 | Mã không có clip hoặc catalog thiếu một mã: ghi cảnh báo Console **một lần** cho mã đó, không phát, không exception, không ảnh hưởng mã khác; không có cảnh báo "Missing AudioClip" ở bất kỳ mã nào đủ clip | EditMode + PlayMode test, kiểm Console | P1 |

### 3.3. Hitmarker rõ, chuông bo 3 tiếng, tiếng rồng, BGM bật/tắt (tiêu chí GDD 3)

| # | Tiêu chí (đo được) | Cách kiểm | Ưu tiên |
| --- | --- | --- | --- |
| AC-A19 | **Hitmarker rõ:** từ lúc `HitDealt(Hero)` tới lúc AudioSource bắt đầu phát ≤ 1 frame. Hitmarker là Critical (không bị cắt, AC-A4) và âm lượng catalog ≥ 1,0 và ≥ gấp đôi `FootstepGrass`. Năm `HitDealt` trong 60 ms (Shotgun năm viên) → một lần phát (cooldown 60 ms). Hitmarker nghe được khi có 15 âm khác đang phát cùng lúc (không bị cắt khỏi pool) | PlayMode test + người nghe (AC-A28) | P0 |
| AC-A20 | **Chuông bo 3 tiếng:** `ZoneState.InWarning` chuyển false → true tạo ra **đúng 3 tiếng chuông**, tiếng đầu ở frame kế tiếp, các tiếng cách nhau 0,8 s ± 0,05 (G-8), trên bus Alert, 2D (không giảm theo khoảng cách). `InWarning` vẫn true và snapshot lặp 30 Hz thì **không** phát lại; cùng `PhaseIndex` nhận lại sau khi `InWarning` rơi về false rồi true trong ≤ 2 s cũng **không** phát lại (khóa theo `PhaseIndex`); `PhaseIndex` mới thì phát | EditMode test (thời gian giả lập, lập lịch 3 tiếng) + PlayMode đếm điểm bắt đầu | P0 |
| AC-A21 | **Tiếng rồng nhận ra được:** `DragonRoar` (mock hoặc DRG-01) phát 1 lần, 2D toàn cục, bus Alert, độ dài clip ≥ 1,5 s (G-5) và clip **không** trùng clip của mã nào khác. Phát lặp trong 5 s thì bỏ qua (cooldown). Nghe có phân biệt hay không thuộc AC-A28 | EditMode + PlayMode test; người nghe | P1 |
| AC-A22 | **Cảnh báo không bị che:** khi `ZoneBell3` hoặc `DragonRoar` đang phát, bus Music (nếu đang bật) hạ ≥ 9 dB trong ≤ 0,2 s và trở lại mức cũ ≤ 1,0 s sau khi âm cảnh báo kết thúc (G-10). Bus SFX **không** bị hạ ngoài mức người dùng đặt. Không bao giờ có hơn 16 AudioSource kể cả Music | PlayMode test (`AudioMixer.GetFloat` theo thời gian) | P0 |
| AC-A23 | **BGM tắt là mặc định:** `musicEnabled = false` → không có AudioSource Music nào đang phát ở mọi `AppScreen`, `MusicDirector` tồn tại và không báo lỗi. Bật (qua F6 hoặc API) → phát nhạc đúng màn hình hiện tại trong ≤ 1 s | PlayMode test | P0 |
| AC-A24 | **BGM theo màn hình** (khi bật, G-9): `AppScreenChanged` sang `MainMenu`/`InRoom`/`Results` → Waiting Room; `Connecting` → Loading; `InMatch` → ParkingMap; `Boot` → im lặng. Khi đổi bài: crossfade 1,0 s ± 0,1; trong lúc fade có tối đa 2 nguồn Music, ngoài lúc fade đúng 1; bài đang phát gặp lại cùng bài thì **không** khởi động lại từ đầu. Hai bài Music **không bao giờ** phát chồng với mức nghe được sau khi fade kết thúc (tổng ≤ 1 nguồn Music phát) | PlayMode test (thời gian giả lập) | P1 |
| AC-A25 | Tắt BGM giữa chừng (`musicEnabled` true → false): nhạc tắt mượt trong ≤ 0,5 s, không click/đột ngột (hạ dần âm lượng), không còn AudioSource Music đang phát sau 0,6 s | PlayMode test | P1 |

### 3.4. Hiệu năng và ổn định

| # | Tiêu chí (đo được) | Cách kiểm | Ưu tiên |
| --- | --- | --- | --- |
| AC-A26 | Chạy kịch bản dồn (mock): trong 10 s gửi liên tục mọi trong 26 mã với tần suất cao (tổng ≥ 2 000 yêu cầu, hỗn hợp trùng khóa và khác khóa): Console không có error/exception/missing reference, số AudioSource đang phát ≤ 16 ở mọi frame (AC-A3), không sự kiện Critical nào bị mất (đếm `HitMarker`, `HurtLocal`, `ZoneBell3`, `MatchEnd` đã gửi so với đã phát sau khi loại phần trùng khóa và cooldown) | PlayMode test (thời gian giả lập) | P0 |
| AC-A27 | Không cấp phát GC theo frame khi phát SFX ở trạng thái ổn định: gọi `PlaySfx` 100 lần liên tiếp → 0 B GC Alloc/frame trong Profiler (sau khi làm nóng), pool không `Instantiate` thêm. Không rò: sau khi thoát scene Game, mọi AudioSource của gameplay được dừng và đăng ký sự kiện được hủy (`IEventBus.HandlerCount` về 0 cho các kiểu router đã đăng ký) | Profiler + EditMode test | P2 |
| AC-A28 | **Cần người nghe** (không chặn bởi máy): PIC nghe kịch bản dồn (F6) và xác nhận: hitmarker nghe rõ giữa nhiều âm, chuông bo nhận ra là cảnh báo bo, tiếng rồng phân biệt được với các âm khác, âm tạm đủ phân biệt 26 mã, âm lượng cân bằng giữa các nhóm, BGM không lấn át cảnh báo. Kết quả ghi lại; điều gì chưa ổn thì ghi cho ART-05 (chọn lại clip) thay vì sửa code | Cần người nghe khi playtest | P1 |

**Ánh xạ tiêu chí GDD:**

| Tiêu chí GDD | AC |
| --- | --- |
| 1. Demo phát SFX từ event ID với placeholder, giới hạn audio-source, volume hoạt động | AC-A1…A9 |
| 2. Thay bằng asset ART-05 không viết lại code; không phát trùng khi event server lặp | AC-A10…A18 |
| 3. Hitmarker rõ, bo cảnh báo 3 chuông, tiếng rồng nhận biết được; BGM bật/tắt theo quyết định GDD | AC-A19…A25, AC-A28 |
| Hiệu năng/ổn định (không thuộc tiêu chí GDD, bảo vệ ba tiêu chí trên) | AC-A26, AC-A27 |

**Ghi chú kiểm lại khi có asset ART-05** (không chặn nghiệm thu): tên và `SfxId` trong catalog vẫn khớp; clip WAV lớn (Waiting Room Music 25,7 MB) đã nén; âm lượng chuẩn hóa giữa các mã (AC-A19, AC-A28 đo lại bằng tai); độ dài `DragonRoar` ≥ 1,5 s; ba tiếng chuông bo thật có đủ ba tiếng trong **một** clip hay là một clip phát ba lần (AC-A20 đo theo lịch phát, không theo nội dung clip); giấy phép từng clip (ART-05 ghi, AUD-01 không khẳng định).

## Câu hỏi và giả định chờ G1

**Giả định** (đã đưa vào bảng 2.1 và các AC, mặc định đang theo):

- **G-1** Bus: `Master` → `SFX` → `Alert` (con của SFX), `Music` (con của Master). UI chỉ có 3 thanh Master/SFX/Music; Alert theo SFX. Quy đổi `dB = 20·log10(v)`, v = 0 → −80 dB.
- **G-2** Pool 16 AudioSource; ưu tiên 4 mức Critical/High/Normal/Low; đầy thì cắt âm thấp nhất, âm cao không bị cắt bởi âm thấp.
- **G-3** Phạm vi nghe theo bảng 2.1 (tối đa 12 cell = `visionRadius`); giảm tuyến tính theo khoảng cách; không có Hero thì âm có vị trí không phát. Âm 2D là âm của người chơi local hoặc toàn cục.
- **G-4** BGM mặc định tắt; `MusicDirector` dựng sẵn.
- **G-5** Mọi số "Tối đa", "Cooldown", "Nghe trong" và ưu tiên trong bảng 2.1; cao độ biến thiên ± 5%; `DragonRoar` ≥ 1,5 s.
- **G-6** Cửa sổ chống lặp 150 ms cho sự kiện không khóa; khóa sự kiện nhớ 2 s, tối đa 128.
- **G-7** Bước chân suy từ vị trí snapshot, mỗi 1,5 cell một bước, chỉ khi Hero thực sự di chuyển; chưa có bộ suy này trong code.
- **G-8** Chuông bo: 3 tiếng cách nhau 0,8 s, phát một lần khi cảnh báo bắt đầu, khóa theo `PhaseIndex`.
- **G-9** Ánh xạ nhạc theo màn hình như mục 2.1; crossfade 1,0 s.
- **G-10** Hạ nhạc −9 dB trong ≤ 0,2 s khi có âm cảnh báo, trả lại ≤ 1,0 s sau khi kết thúc.

| # | Câu hỏi | Mặc định / khuyến nghị |
| --- | --- | --- |
| **Q1** | **Duyệt danh sách 26 SFX** (GDD không có danh sách, chỉ ví dụ). Có đổi mã nào, hoặc đưa thêm mã ngoài 26 (stun/root/độc bắt đầu, vỡ rương/chum, cửa đá, portal, hồi sinh, mất kết nối)? | **Khuyến nghị:** duyệt 26 mã như bảng 2.1; các âm ngoài 26 ghi vào danh sách "mở rộng" của ART-05/ticket sau, không làm trong AUD-01 |
| **Q2** | **BGM có thuộc MVP không?** Assets STT 14 "Chưa chốt phạm vi" | **Khuyến nghị:** mặc định **tắt**, chỉ dựng `MusicDirector` và công tắc F6; bật khi PIC chốt. Ba bài JSurviv chỉ là tạm |
| **Q3** | **Phạm vi nghe tiếng người khác** (Assets STT 9: "không mặc định nghe toàn map"; Hero STT 13 chưa chốt luật thông tin) | **Khuyến nghị:** tối đa 12 cell (`visionRadius`), nhỏ hơn cho âm nhỏ (bước chân 6, chém 8…) theo bảng 2.1; luật nghe qua vật cản/bụi thuộc FOW-01, AUD-01 chưa xét |
| **Q4** | **Tiếng rồng:** dùng tiếng tạm tổng hợp cho tới khi DRG-01 hoặc ART-05 có tiếng thật? | **Khuyến nghị:** có, tiếng tạm khác mọi âm khác, ART-05 thay sau |
| **Q5** | **Giấy phép file JSurviv:** chỉ dùng làm tạm trong AUD-01, việc kiểm giấy phép và thay bằng nguồn hợp lệ thuộc ART-05 (tài liệu hiện không ghi giấy phép âm thanh) | **Khuyến nghị:** đúng như vậy; AUD-01 không khẳng định license |
| **Q6** | **Hit marker âm thanh khi trúng vật không phải Hero** (rương, chum) và khi tự gây sát thương bằng bom của mình? (đồng bộ với Q25 của UI-01 phần 2) | **Khuyến nghị:** chỉ `Target=Hero` phát `HitMarker`; vật phá được: không (nếu muốn có tiếng vỡ rương thì là mã mở rộng, Q1) |
| **Q7** | **Ai được nghe `BushEnter`?** Người ngoài nghe tiếng vào bụi sẽ lộ vị trí người trong bụi (Obstacles 9: "VÔ HÌNH với Hero khác") | **Khuyến nghị:** chỉ người chơi local nghe (2D); không phát cho người khác để khỏi lộ thông tin |
| **Q8** | **Dùng đồ:** có tiếng riêng khi **xong**? Người khác có nghe tiếng uống/băng bó của mình không? Assets STT 12 ghi "bắt đầu/kết thúc dùng đồ" | **Khuyến nghị:** tiếng ở lúc **bắt đầu** (`UseHeal`/`UseDrink`) và **hủy** (`UseCancel`); **không** có tiếng khi xong (giữ 26 mã); chỉ người dùng nghe (2D) |
| **Q9** | **Ai lưu mức âm lượng** giữa các lần chạy: AUD-01 hay UI-03? | **Khuyến nghị:** AUD-01 cung cấp `GetVolume`/`SetVolume` và tải/lưu một chỗ (khóa `PlayerPrefs`); UI-03 chỉ gọi API. PIC chọn nếu muốn UI-03 lưu |
| **Q10** | **Bus `Alert` riêng:** dòng ticket chỉ nêu Master/SFX (và Music). Có chấp nhận thêm nhóm con Alert (không có thanh trượt riêng) để cảnh báo luôn ưu tiên? | **Khuyến nghị:** có (G-1); người dùng không chỉnh Alert riêng, chỉnh SFX thì Alert theo |
| **Q11** | **Chuông bo 3 tiếng:** khoảng cách giữa tiếng (0,8 s) và thời điểm (lúc bắt đầu cảnh báo 10 s trước khi co) — GDD không ghi | **Khuyến nghị:** như G-8; PIC chỉnh khi nghe (AC-A28) |
| **Q12** | **`TrapTriggered` và nguồn của nó:** `DangerKind.Trap` chỉ báo "có bẫy". Cần một sự kiện "bẫy kích hoạt" (WPN-01). Món nào là bẫy: Vỏ chuối sau khi nổ (stun 1 s), Ổ gai (Obstacles 14)? | **Khuyến nghị:** mock trong demo; WPN-01 quyết nguồn. Ổ gai/lava là hazard cố định, không phát `TrapTriggered` (để mở rộng) |
| **Q13** | **Bước chân:** suy từ vị trí snapshot (client) hay có sự kiện từ animation/server? Cần luật cho người khác nghe (FOW-01) | **Khuyến nghị:** client suy từ vị trí (G-7), mỗi 1,5 cell; chỉ người trong 6 cell nghe. Tách thành bộ suy riêng khi có nguồn tốt hơn |
| **Q14** | **`ArmorAbsorb`:** điều kiện "giáp đã đỡ" — độ bền giáp/mũ giảm trong ≤ 100 ms sau `DamageTaken`; phát cùng `HurtLocal` (chồng hai tiếng) hay thay `HurtLocal`? | **Khuyến nghị:** chồng (cả hai phát); nếu PIC thấy quá ồn thì đổi thành thay thế |

Câu hỏi kỹ thuật **T-1…T-8** của tech-lead (có mặc định) nằm ở mục 4.8. Cần chú ý **T-1** (sửa `Bootstrap.cs` 2 dòng ở commit cuối, sau ADHOC) và **T-4** (cách đếm nguồn của AC-A22).

## 4. Kế hoạch kỹ thuật

Viết 27/10/2026, tech-lead. Kế hoạch theo **mặc định khuyến nghị của spec** (Q1–Q14, G-1…G-10) và `AUD-01_proposal.vi.md`, cộng câu hỏi kỹ thuật T-1…T-8 ở cuối mục. Đường dẫn viết tắt: `Scripts/` = `Assets/_JungleMaze/Scripts/`, `Tests/` = `Assets/_JungleMaze/Tests/`, `Art/` = `Assets/_JungleMaze/Art/`. Code nguồn sự kiện được đối chiếu với working tree ngày 27/10 (`IHudState`, `IInventoryState`, `EventBus`, `AppScreenChanged`, `MatchSession.Zone`, `EventPacket`, `MatchEntityEventPacket`, `IPlayerPoseSource`, `Bootstrap.cs` gồm cả phần ADHOC chưa commit).

### 4.0. Vùng file và điều kiện với file dùng chung

- **Mọi file mới nằm trong vùng AUD-01** (`Docs/Agent_Coordination.vi.md` mục 2): `Scripts/**/Audio/**`, `Art/Audio/**`, `Tests/**/Audio/**`, `Tools/Audio/**`, `Docs/Tickets/AUD-01*`. Không sửa code gameplay, HUD, mạng, `MatchSession`, `GameFlow`, protocol, của Hào hay ITM-01.
- **`Bootstrap.cs` là file dùng chung duy nhất bị sửa, đúng 2 dòng**, theo mẫu partial hook có sẵn (`partial void ConfigureOnlineChecks()`):
  1. Khai báo `partial void ComposeAudio(IEventBus bus, TickRunner tickRunner, HudStateStore hudState, InventoryStateStore inventoryState, IBalanceConfigProvider balance, MatchSession match, IPacketReceiver receiver, IPlayerPoseSource poses);`
  2. Gọi `ComposeAudio(bus, tickRunner, hudState, inventoryState, balance, match, network, _registry.Resolve<IPlayerPoseSource>());` ngay **trước** `_registry.InitializeAll();`

  Phần thân nằm ở `Scripts/Bootstrap/Audio/Bootstrap.Audio.cs` (cùng assembly `JungleMaze.Bootstrap`, trong vùng AUD-01). Phần thân đăng ký service, `tickRunner.Add`, `Attach<AudioManager>()` và (dev) `Attach<AudioDebugPanel>()`. Gắn MonoBehaviour trước `InitializeAll()` không vấn đề gì, vì `Attach` chỉ inject, và mọi service cần đã đăng ký trước dòng gọi.
- **Điều kiện sửa `Bootstrap.cs`:** làm ở **commit cuối** (commit 10, mục 4.6), chỉ sau khi ADHOC-UI-ASSETS-A đã **commit** `Bootstrap.cs`. Ngày 27/10 file này còn sửa dở (`uiArt`, `ConfigureArt`). Trước khi sửa phải ghi một dòng vào `Docs/Agent_Coordination.vi.md` mục 4. Tới lúc đó, mọi phần khác (commit 1–9) build và test được mà không cần Bootstrap: EditMode dựng service bằng tay, PlayMode tự tạo `AudioManager` trong test.
- **Không cần scene mới.** Panel F6 được `Bootstrap.Audio.cs` gắn trong `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, nên dùng được ở scene dev sẵn có `Scenes/Dev/HudPreview.unity` (không nằm trong Build Settings, `BootstrapAutoCreate` tự tạo Bootstrap) và ở scene Game. `Scenes/*.unity` là file dùng chung nên không mở/lưu (T-6).
- **Unity Editor dùng chung với Codex:** trước mỗi lần refresh, chạy test hoặc vào Play Mode qua MCP, ghi vào `Agent_Coordination.vi.md` mục 4. Sau PlayMode: `git checkout -- ProjectSettings/EditorSettings.asset`.

### 4.1. Thiết kế chính

- **Một cổng phát duy nhất: `IAudioService`** (Application). HUD, gameplay, mạng và router chỉ gọi cổng này, không ai đụng `AudioSource`. Phần **quyết định** (cooldown, số bản tối đa, chọn voice theo ưu tiên, giảm theo khoảng cách, chọn clip, lặp chuông, crossfade, ducking) là C# thuần ở Application, test bằng EditMode với thời gian giả lập. Phần **thực thi** là `AudioManager` (Presentation), làm việc qua port hẹp `IAudioOutput`: phát clip thứ k của một mã trên voice i với gain/pitch/bus, dừng voice, đọc voice còn phát không, đặt dB của tham số mixer.
- **Luật của từng mã nằm trong `SfxCatalog` (ScriptableObject), không nằm trong code.** Mỗi mục gồm clip[], âm lượng, biến thiên cao độ, ưu tiên, bus, phạm vi nghe (0 = 2D), số bản tối đa, cooldown, số lần lặp + khoảng lặp. Lúc khởi động, `AudioManager` đổi catalog thành `SfxRuleTable` (Domain, C# thuần) và đưa sang Application. Thay clip, thêm biến thể hay đổi số chỉ là sửa asset (AC-A10). Số trong bảng 2.1 là giá trị mặc định của asset.
- **Chuông bo 3 tiếng = luật lặp trong catalog** (`ZoneBell3`: lặp 3, cách 0,8 s), không phải logic riêng. `SfxScheduler` (Application) phát lần đầu ngay, hai lần sau theo đồng hồ tick. Mỗi lần là một yêu cầu phát Critical trên bus Alert. Nếu ART-05 nộp một clip có sẵn 3 tiếng thì chỉ đặt lặp = 1 trên asset (AC-A20 đo theo lịch phát).
- **Chống lặp chia hai lớp.**
  - (1) `SfxDeduper` cho sự kiện **có khoá**: khoá `ulong` lấy từ (`Tick`, `EventType`, `SourceNetId`, `TargetNetId`) với `EventPacket`, hoặc (loại "zone", `PhaseIndex`) với cảnh báo bo. Khoá nhớ 2 s trong vòng đệm cố định 128 mục, đầy thì bỏ khoá cũ nhất (G-6, AC-A11).
  - (2) `SfxGate` cho **mọi** yêu cầu: cooldown theo (mã, nguồn), nguồn mặc định là toàn cục, kèm số bản tối đa trên toàn pool (G-5, AC-A6, AC-A12).

  Thứ tự kiểm: deduper → gate → khoảng cách → cấp voice.
- **Cấp voice có ưu tiên** (`VoiceAllocator`, Application): pool cố định 16 slot SFX (G-2). Còn slot trống thì dùng. Hết slot thì cắt voice có ưu tiên **thấp hơn hẳn** yêu cầu (ưu tiên thấp nhất, cùng mức thì cắt voice cũ nhất). Không có voice thấp hơn thì yêu cầu bị bỏ. Critical không bao giờ bị cắt bởi âm thấp hơn (AC-A4).
- **Giảm theo khoảng cách tự tính, không dùng 3D audio của Unity.** Gain = 1 − d/R với d tính bằng cell từ `IHudState.Pose`; d ≥ R thì không cấp voice (AC-A7). AudioSource để `spatialBlend = 0`, nên kết quả không phụ thuộc vị trí AudioListener hay camera. Pan trái/phải chưa làm (T-7).
- **Router chỉ nghe, không ghi.**
  - `SfxRouter` (Application) đăng ký vào event có sẵn: `IHudState.ItemPicked`, `HitDealt`, `DamageTaken`, `DangerChanged`, `Changed` (cờ Armor/Helmet), `IInventoryState.UseChanged`, `IEventBus<AppScreenChanged>`, và cổng `IAudioEventSource` cho sự kiện mạng hoặc mock. Router không tham chiếu WPN/ITM/Game-10/DRG, không có port ghi nào (AC-A16).
  - Phân loại vũ khí/đồ và Healing/Support qua `IBalanceConfigProvider` (port Application có sẵn).
- **Sự kiện mạng ◐ đi qua một adapter đọc, không sửa `MatchSession`/`GameFlow`.** `MatchAudioEventSource` (Application, implement `IAudioEventSource`):
  - Subscribe thêm `IPacketReceiver<EventPacket>` (`PlayerDied` → `Eliminated`, có khoá).
  - Mỗi tick đọc `MatchSession.Zone`/`HasZone`: `InWarning` false→true → `ZoneBell3`, khoá = `PhaseIndex`.
  - Vị trí người khác lấy từ `IPlayerPoseSource`. Bản thân bị loại (`TargetNetId == MatchSession.OwnNetId`) thì phát 2D.
  - Không map `PlayerHit`/`ItemPickedUp` của `EventPacket`, vì các âm đó đã đến từ `IHudState` khi WPN-02/ITM-01 ghi. Map cả hai nguồn sẽ phát đôi.
  - `MatchEnd` chỉ lấy từ `AppScreenChanged → Results` (không lấy `EventPacket.MatchEnded`), cũng để tránh phát đôi.
- **Mock cho mã ✖** (`MockAudioEventSource`, chỉ Editor/Dev build) implement cùng `IAudioEventSource`. Router nhận nhiều nguồn qua `AudioEventSources` (gộp danh sách). WPN-01/DRG-01 sau này chỉ cần implement `IAudioEventSource` (hoặc ghi vào port UI-01), không sửa router.
- **`ArmorAbsorb` và chống phát lúc khởi tạo** (AC-A14, AC-A15, Q14):
  - Router giữ đồng hồ tick, lần `DamageTaken` cuối và độ bền giáp/mũ trước đó. Độ bền **giảm** trong ≤ 0,1 s quanh một `DamageTaken` (trước hoặc sau) → `ArmorAbsorb`, phát chồng với `HurtLocal`.
  - `WearArmor` chỉ khi id mới khác rỗng và khác id cũ.
  - **Cửa sổ ổn định 0,5 s** sau `AppScreenChanged → InMatch` và sau mỗi lần store reset (nhận ra qua `HudStateChange` có đủ các cờ Hp/Armor/Helmet/HeldWeapon trong cùng một lần phát): trong cửa sổ này router cập nhật trạng thái nhớ nhưng không phát `WearArmor`/`ArmorAbsorb`/`PickupWeapon`/`PickupItem` (T-3).
- **Nhạc nền mặc định tắt (Q2).** `MusicPlan` (Application, C# thuần) chọn bài theo `AppScreen` (G-9) và tính trọng số crossfade 1,0 s giữa 2 nguồn. Gặp lại cùng bài thì không khởi động lại. Bản tin tắt nhạc là fade-out 0,4 s. Ducking: −9 dB trong 0,2 s khi có âm bus Alert đang phát, trả lại trong 1,0 s (G-10). `MusicDirector` (Presentation) áp kết quả lên 2 AudioSource nhạc riêng và tham số mixer `MusicDuck`.
- **Mixer** (G-1, Q10): `Art/Audio/JungleMazeAudio.mixer`, nhóm `Master` → `SFX` → `Alert`; `Master` → `Music` → `MusicTrack`. Năm tham số expose: `MasterVolume`, `SfxVolume`, `AlertVolume` (cố định 0 dB, để dành), `MusicVolume` (âm lượng người dùng), `MusicDuck` (ducking). Nhạc phát vào `MusicTrack` để ducking và âm lượng người dùng cộng dồn mà không ghi đè nhau. Quy đổi `dB = 20·log10(v)`, v ≤ 0.0001 → −80 dB (AC-A5).
- **Âm lượng lưu một chỗ (Q9):** port `IAudioSettingsStore` (Application), hiện thực `PlayerPrefsAudioSettings` (Infrastructure, khoá `jm.audio.master/sfx/music`, `jm.audio.musicEnabled`). Nạp lúc khởi động, lưu khi `SetVolume`/`MusicEnabled` đổi. Tham số mixer chỉ đặt được sau khi mixer đã nạp: `AudioManager` áp giá trị trong `Start`.
- **Asset nạp giống `UiArtSet` của ADHOC.** `AudioRuntimeSet` (ScriptableObject: mixer, nhóm cho từng bus, `SfxCatalog`, clip nhạc, kích thước pool) đặt ở `Art/Audio/Resources/AudioRuntimeSet.asset`. Bootstrap tự tạo trong Game/HudPreview không có tham chiếu serialize, nên `Bootstrap.Audio.cs` gọi `Resources.Load` **một lần** lúc compose. Thiếu asset thì log warning và đăng ký `NullAudioService` (không exception, AC-A18).
- **Không cấp phát khi phát.** Voice, khoá, cooldown đều dùng mảng cố định. Mã → luật tra bằng mảng đánh chỉ số theo `(int)SfxId`. Random dùng `System.Random` tạo sẵn. `AudioSource.PlayOneShot` **không** dùng (không dừng được từng bản); mỗi voice gán `clip` rồi `Play()` (AC-A27).

### 4.2. Chữ ký chính (dev giữ đúng tên)

```csharp
// ---- Domain: Scripts/Domain/Audio/ (namespace JungleMaze.Domain.Audio) ----
public enum SfxId : byte { None = 0, FootstepGrass = 1, FootstepSwamp, BushEnter, MeleeSwing, MeleeHit, ShootRanged,
    ShootCrossbow, Throw, Reload, EmptyClick, BombFuse, Explosion, TrapTriggered, HitMarker, HurtLocal, ArmorAbsorb,
    Eliminated, PickupWeapon, PickupItem, WearArmor, UseHeal, UseDrink, UseCancel, ZoneBell3, DragonRoar, MatchEnd } // 26, đúng thứ tự bảng 2.1
public enum AudioBus : byte { Master = 0, Sfx = 1, Music = 2, Alert = 3 }
public enum SfxPriority : byte { Low = 0, Normal = 1, High = 2, Critical = 3 }
public readonly struct SfxRule { AudioBus Bus; SfxPriority Priority; float RangeCells /*0 = 2D*/; int MaxInstances;
    float CooldownSeconds; int ClipCount; float Volume; float PitchJitter; int RepeatCount; float RepeatInterval; }
public sealed class SfxRuleTable { SfxRuleTable(SfxRule[] byId); bool TryGet(SfxId id, out SfxRule rule); }  // mảng theo (int)SfxId

// ---- Application: Scripts/Application/Audio/ (namespace JungleMaze.App.Audio) ----
public interface IAudioService
{
    void PlaySfx(SfxId id);                                              // 2D
    void PlaySfx(SfxId id, float worldX, float worldY, int sourceKey = 0); // có vị trí (cell), cooldown theo nguồn
    void SetVolume(AudioBus bus, float volume01);                        // Master/Sfx/Music; kẹp [0,1]
    float GetVolume(AudioBus bus);
    bool MusicEnabled { get; set; }                                      // mặc định false (G-4)
    int ActiveVoices { get; }  int MaxVoices { get; }                    // cho panel F6 và test
}
public readonly struct AudioCue { SfxId Id; bool HasPosition; float X, Y; int SourceKey; ulong DedupeKey /*0 = không khoá*/; }
public interface IAudioEventSource { event Action<AudioCue> CueRaised; }
public interface IAudioOutput   // Presentation implement (AudioManager)
{
    int VoiceCount { get; }
    void PlayVoice(int voice, SfxId id, int clipIndex, AudioBus bus, float gain, float pitch);
    void StopVoice(int voice);
    bool IsVoicePlaying(int voice);
    void SetBusDecibels(AudioBus bus, float db);
    void SetMusicDuckDecibels(float db);
}
public interface IAudioSettingsStore { float Load(AudioBus bus, float fallback); void Save(AudioBus bus, float v);
                                       bool LoadMusicEnabled(bool fallback); void SaveMusicEnabled(bool on); }
// SfxPlaybackService : IAudioService, ITickable — ghép SfxGate + VoiceAllocator + SfxDistance + SfxScheduler + ClipPicker
// SfxDeduper   { bool IsDuplicate(ulong key, float now); }               // 2 s, 128 khoá
// SfxRouter    : IInitializable, ITickable, IDisposable                  // nghe, gọi IAudioService
// MatchAudioEventSource : IAudioEventSource, IInitializable, ITickable, IDisposable
// MusicPlan    { void SetScreen(AppScreen s); void SetEnabled(bool on); void SetAlertActive(bool on); void Tick(float dt);
//                MusicTrack TrackA/TrackB; float WeightA/WeightB; float DuckDb; }  // MusicTrack: None, WaitingRoom, Loading, Match
// AudioVolume  { static float ToDecibels(float v01); }                 // 20·log10, sàn −80
```

**Ticket sau nối vào đâu (không sửa AUD-01):**

| Nguồn | Cách nối | Mã |
| --- | --- | --- |
| UI-01 port (ITM-01, WPN-02, WPN-01 ghi vào) | Đã nghe sẵn qua `IHudState`/`IInventoryState` | 11, 12, 14–16, 18–23 |
| WPN-01 (bắn/chém/ném/nạp/hết đạn/bẫy) | Implement `IAudioEventSource` hoặc gọi thẳng `IAudioService.PlaySfx(id, x, y, netId)`; đăng ký trong Bootstrap của ticket đó | 4–10, 13 |
| Bộ suy bước chân (Q13, G-7) | Một `IAudioEventSource` mới đọc `IPlayerPoseSource`; **không làm trong AUD-01** (mock trong demo) | 1–3 |
| DRG-01 | `IAudioEventSource` phát `DragonRoar` | 25 |
| SRV-03/`EventPacket.PlayerDied` | Đã có trong `MatchAudioEventSource` | 17 |
| ZON-01/`ZoneStatePacket` | Đã có trong `MatchAudioEventSource` | 24 |
| UI-03 | `IAudioService.SetVolume/GetVolume/MusicEnabled` | — |
| ART-05 | Thay clip trong `SfxCatalog.asset`/`AudioRuntimeSet.asset` | mọi mã |

### 4.3. Lớp và file

| Lớp | File (mới, trừ dòng có ghi) | Trách nhiệm |
| --- | --- | --- |
| Domain | `Scripts/Domain/Audio/SfxId.cs` | `SfxId` (26 + None), `AudioBus`, `SfxPriority` |
| Domain | `Scripts/Domain/Audio/SfxRule.cs` | `SfxRule`, `SfxRuleTable` (kiểm đủ 26, trả lỗi cho mã thiếu) |
| Application | `Scripts/Application/Audio/IAudioService.cs`, `IAudioOutput.cs`, `IAudioEventSource.cs`, `IAudioSettingsStore.cs`, `AudioCue.cs` | Port như 4.2 |
| Application | `Scripts/Application/Audio/SfxGate.cs` | Cooldown theo (mã, nguồn) trong mảng cố định (64 nguồn × mã, LRU) + đếm bản đang phát theo mã |
| Application | `Scripts/Application/Audio/SfxDeduper.cs` | Vòng đệm 128 khoá, nhớ 2 s |
| Application | `Scripts/Application/Audio/VoiceAllocator.cs` | Bảng 16 slot (mã, ưu tiên, thời điểm bắt đầu, đang phát); chọn slot trống hoặc slot để cắt |
| Application | `Scripts/Application/Audio/SfxDistance.cs`, `ClipPicker.cs`, `SfxScheduler.cs`, `AudioVolume.cs` | Gain tuyến tính; chọn clip ngẫu nhiên không lặp liền + pitch ± jitter; hàng lặp (chuông) cố định 8 mục; quy đổi dB |
| Application | `Scripts/Application/Audio/SfxPlaybackService.cs` | `IAudioService` + `ITickable`: ghép các phần trên, đọc `IHudState.Pose`, gọi `IAudioOutput`, nạp/lưu âm lượng qua `IAudioSettingsStore`, đánh dấu "có âm Alert đang phát" cho `MusicPlan` |
| Application | `Scripts/Application/Audio/SfxRouter.cs` | Ánh xạ sự kiện → mã (bảng 2.1, AC-A14), luật `ArmorAbsorb`/`WearArmor`, cửa sổ ổn định, gom nhiều `IAudioEventSource` |
| Application | `Scripts/Application/Audio/MatchAudioEventSource.cs` | Adapter ◐: `EventPacket.PlayerDied` + `ZoneStatePacket.InWarning` → `AudioCue` có khoá |
| Application | `Scripts/Application/Audio/MusicPlan.cs` | Chọn bài, crossfade, fade-out khi tắt, ducking |
| Application | `Scripts/Application/Audio/Mocks/MockAudioEventSource.cs` (cả file trong `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`) | `Raise(id[, x, y, source])`, `RaiseKeyed(id, key)`, `Spam(n)` (100 yêu cầu hỗn hợp, seed cố định), `RepeatServerEvent(5)`, `Burst10s` (AC-A26) |
| Infrastructure | `Scripts/Infrastructure/Audio/PlayerPrefsAudioSettings.cs` | `IAudioSettingsStore` bằng `PlayerPrefs` |
| Presentation | `Scripts/Presentation/Audio/SfxCatalog.cs` | ScriptableObject: mục cho từng `SfxId`, `BuildRules()` → `SfxRuleTable`, `GetClip(id, k)`; `OnValidate` báo mã thiếu |
| Presentation | `Scripts/Presentation/Audio/AudioRuntimeSet.cs` | ScriptableObject: mixer, `AudioMixerGroup` cho Sfx/Alert/MusicTrack, catalog, clip nhạc theo `MusicTrack`, `PoolSize` = 16; `ResourcePath` = `"AudioRuntimeSet"` |
| Presentation | `Scripts/Presentation/Audio/AudioManager.cs` | MonoBehaviour trên Bootstrap (DontDestroyOnLoad): `Configure(AudioRuntimeSet)`, tạo 16 AudioSource con SFX + 2 nhạc **một lần** trong `Awake`/`Configure`; implement `IAudioOutput`; `Update` gọi `MusicDirector`. Không logic quyết định |
| Presentation | `Scripts/Presentation/Audio/MusicDirector.cs` | Áp `MusicPlan` lên 2 AudioSource nhạc và `MusicDuck` |
| Presentation | `Scripts/Presentation/Audio/NullAudioService.cs` | Không phát gì; dùng khi thiếu asset |
| Presentation | `Scripts/Presentation/Audio/AudioDebugPanel.cs` (cả file trong `#if`) | IMGUI phím **F6**, góc trái dưới (F3/F4/F5 đã dùng). `[Inject] IAudioService, MockAudioEventSource, IHudStateWriter?` (chỉ để đặt pose giả khi scene không có Hero). Có 26 nút phát (2D, hoặc ở vị trí đặt sẵn cách Hero 0/6/12 cell), 3 thanh trượt, "voice đang dùng/tối đa", "Spam 100", "Gửi lặp ×5", "Dồn 10 s", công tắc BGM, chọn màn hình nhạc |
| Bootstrap | `Scripts/Bootstrap/Audio/Bootstrap.Audio.cs` | Phần thân `partial void ComposeAudio(...)`: nạp `AudioRuntimeSet`, tạo `PlayerPrefsAudioSettings`, `Attach<AudioManager>().Configure(set)`, `SfxPlaybackService`, `MatchAudioEventSource`, (dev) `MockAudioEventSource`, `SfxRouter`; đăng ký `IAudioService` và các service; `tickRunner.Add` (service, router, match source); (dev) `Attach<AudioDebugPanel>()` |
| Bootstrap | `Scripts/Bootstrap/Bootstrap.cs` (**sửa chung, 2 dòng**, commit cuối) | Khai báo + gọi `ComposeAudio` (4.0) |
| Art | `Art/Audio/JungleMazeAudio.mixer` | Mixer như 4.1. Dựng bằng MCP `execute_code` (API editor nội bộ `AudioMixerController` qua reflection, chạy một lần, chỉ commit asset). Nếu reflection lỗi thì người dựng tay theo hướng dẫn 6 bước ghi trong Nhật ký build (T-2) |
| Art | `Art/Audio/Placeholder/Beeps/*.wav` | Sinh bằng `Tools/Audio/make_placeholder_sfx.py` |
| Art | `Art/Audio/Placeholder/JSurviv/*` | **Chép** (không tham chiếu `Assets/Imported`) các file dùng tạm theo bảng 2.1, đổi tên ASCII (ví dụ `melee_slash.mp3`, `hit_thud.wav`, `bomb_explosion.mp3`). **Không chép `.meta`**, để Unity sinh GUID mới, tránh trùng GUID với bản gốc. Nhạc: chỉ chép `Loading Music.mp3` và `ParkingMapMusic.mp3` (T-5) |
| Art | `Art/Audio/SfxCatalog.asset`, `Art/Audio/Resources/AudioRuntimeSet.asset` | Giá trị mặc định = bảng 2.1; nhạc mặc định tắt |
| Tools | `Tools/Audio/make_placeholder_sfx.py` | Python 3 thư viện chuẩn (`wave`, `struct`, `math`), chạy `python -I Tools/Audio/make_placeholder_sfx.py`. Mono 16-bit 44,1 kHz, có envelope để không có click. Tần số khác nhau cho mỗi mã thiếu file (bước 1/4 quãng tám từ 330 Hz). `zone_bell.wav` 0,5 s (hai tần số 880 + 1320 Hz, tắt dần). `dragon_roar.wav` 2,0 s (quét 70 → 220 Hz + nhiễu lọc, khác mọi bíp). Ghi `Tools/Audio/README.md` một đoạn: cách chạy, file sinh ra là âm tạm |
| Test | `Tests/EditMode/Audio/*.cs`, `Tests/PlayMode/Audio/*.cs` | Dùng asmdef test hiện có; xem 4.5. Fake: `FakeAudioOutput`, `FakeAudioSettings`, `FakeAudioEventSource` |

### 4.4. Protocol / config / package

- **Protocol: không đổi.** Chỉ **đọc** `EventPacket`, `ZoneStatePacket` (qua `MatchSession.Zone`) và `MatchSession.OwnNetId`. Không cần G2. `IPacketReceiver` cho nhiều subscriber cùng kiểu (`GameFlow` đã subscribe `EventPacket`). Nếu thực tế chỉ cho một subscriber thì `MatchAudioEventSource` bỏ phần `PlayerDied` và ghi lại, chứ không sửa `NetworkClient`.
- **Balance JSON: chỉ đọc** (`weapons.json` để phân loại nhặt, `equipments.json` `type` để phân Healing/Support). Âm thanh không vào balance JSON: nằm trong catalog asset (spec mục 2).
- **Không thêm package.** `UnityEngine.Audio` (AudioMixer) là module built-in. Không thêm asmdef mới (thư mục `Audio` nằm trong asmdef của từng lớp).

### 4.5. Agent build, test và ước lượng

**Agent build:** chỉ **unity-dev** (backend-dev không có việc). Trong ticket không tách song song. AUD-01 chạy song song được với ADHOC của Codex vì không chung file, trừ `Bootstrap.cs` (commit cuối) và Unity Editor (xếp lượt qua `Agent_Coordination`).

**Test** (EditMode `Tests/EditMode/Audio/`, thời gian giả lập qua `Tick(dt)` + `FakeAudioOutput`; PlayMode `Tests/PlayMode/Audio/AudioAcceptanceTests.cs` tự tạo `AudioManager` với `AudioRuntimeSet` thật, không load scene Game):

| AC | Test (E = EditMode, P = PlayMode) | Ghi chú |
| --- | --- | --- |
| A1 | E `SfxCatalogTests.Has26Ids_EachHasClipAndRuleFromTable` | Đọc asset qua `AssetDatabase`, so từng cột với bảng 2.1 (đặt bảng mong đợi trong test) |
| A2 | P `PlaySfx_EachOfThe26_OneVoiceRightClipAndBus` | `outputAudioMixerGroup` = Alert cho 24, 25 |
| A3 | P `Spam100_NeverMoreThan16_NoNewAudioSources` | Đếm `AudioSource` trước/sau |
| A4 | E `VoiceAllocatorTests.CriticalEvictsLow`, `LowDroppedWhenFullOfHigh`, `CriticalNeverEvicted`; P xác nhận trên AudioSource thật | |
| A5 | P `SetVolume_MixerDecibels` (`AudioMixer.GetFloat`); E `AudioVolumeTests.ToDecibels` | Master áp cho con: kiểm cấu trúc nhóm của mixer (Sfx, Music là con Master) |
| A6 | E `SfxGateTests.CooldownPerSource`, `MaxInstancesGlobal`, `ReplaysAfterCooldown` (5 mã của AC) | |
| A7 | E `SfxDistanceTests.LinearGain_RejectBeyondRange`, `SfxPlaybackServiceTests.NoPose_PositionalSilent_2DPlays` | |
| A8 | Review `#if` + P `DebugPanel_PlayButtonsCallService` (gọi trực tiếp hàm của panel, không click IMGUI) | Không có Release build thì ghi "chưa kiểm chứng trên build thật" |
| A9 | E `SfxCatalogTests.DistinguishableIdsUseDistinctClips`, `BeepsHaveDistinctFrequencies` (đọc tần số từ tên file sinh ra); review `git diff -- Assets/Imported/JSurviv` trống | |
| A10 | E `ClipPickerTests.NoImmediateRepeat_PitchWithin5pct`; P `SwapClipInCatalogCopy_TestsStayGreen` (đổi clip trên bản sao trong bộ nhớ) | Review: diff thay clip không có `.cs` |
| A11 | E `SfxDeduperTests.FiveCopies_OnePlay`, `DifferentTick_Plays`, `Remember2s_Cap128`; E `MatchAudioEventSourceTests.PlayerDied_KeyedCue` | |
| A12 | E `SfxRouterTests.HudEventsWithinCooldown_OnePlay`, `DifferentPickupIds_BothPlay` | |
| A13 | P `RepeatServerEvent_AddsAtMostOneVoice` | |
| A14 | E `SfxRouterTests.MapsEveryAvailableEvent` (store thật `HudStateStore`/`InventoryStateStore` + balance thật, ghi qua port ghi, kiểm `FakeAudioService` nhận đúng mã/vị trí) | Đủ từng gạch đầu dòng của AC |
| A15 | E `SfxRouterTests.ResetAndInitialState_Silent`, `TakeOff_Silent`, `DurabilityDropWithoutDamage_Silent`, `SettleWindowAfterInMatch` | |
| A16 | E `SfxRouterTests.SecondSourceImplementation_SameOutput` + review: assembly Application/Audio không `using` namespace của WPN/ITM/DRG/Infrastructure | |
| A17 | E `SfxRouterTests.MockRaisesAll12Unsourced`, `NothingWithoutCalls_600Ticks`; P qua F6 | |
| A18 | E `SfxPlaybackServiceTests.MissingClip_WarnOnce_NoThrow`; P `LogAssert` không có "Missing AudioClip" | |
| A19 | P `HitMarker_StartsSameFrame_With15Others`; E cooldown 60 ms (5 sự kiện/60 ms → 1); E catalog: volume ≥ 1,0 và ≥ 2 × `FootstepGrass` | Người nghe: A28 |
| A20 | E `MatchAudioEventSourceTests.WarningRisingEdge_OncePerPhase` (30 Hz lặp, rơi về false rồi true cùng phase, phase mới); E `SfxSchedulerTests.ThreeBells_08s`; P đếm 3 lần `PlayVoice` theo thời gian giả lập | |
| A21 | E catalog: `DragonRoar` clip ≥ 1,5 s, clip riêng, bus Alert, cooldown 5 s; P phát 1 lần | |
| A22 | E `MusicPlanTests.DuckMinus9In02_ReleaseIn1`; P `AlertDucksMusic_SfxUntouched` (`GetFloat("MusicDuck")`, `GetFloat("SfxVolume")` theo thời gian) | Đếm nguồn: xem T-4 |
| A23 | P `MusicOffByDefault_NoMusicSource`, `EnableMusic_PlaysScreenTrackWithin1s` | |
| A24 | E `MusicPlanTests.ScreenMapping`, `Crossfade1s_AtMostTwo`, `SameTrack_NoRestart` | |
| A25 | E `MusicPlanTests.DisableFadesOutIn04`; P không còn nguồn nhạc phát sau 0,6 s | |
| A26 | P `Burst10s_2000Requests_NoErrors_CriticalNotLost` (`MockAudioEventSource.Burst10s`, `captureDeltaTime = 1/60`, `LogAssert.NoUnexpectedReceived`) | |
| A27 | E `SfxPlaybackServiceTests.PlaySfx100_NoGcAlloc` (`Is.Not.AllocatingGCMemory()` sau làm nóng); E `SfxRouterTests.Dispose_UnsubscribesAll` (`HandlerCount` = 0); QA Profiler (P2) | |
| A28 | Không tự động hoá. PIC nghe preset "Dồn 10 s" trên F6, ghi kết quả vào mục 7 | |

**Ước lượng (khả thi trước 30/10/2026):** khoảng 2 ngày làm việc của unity-dev, tính từ khi G1 duyệt sáng 27/10.

| Phần | Ước lượng | Commit |
| --- | --- | --- |
| Domain + Application thuần + EditMode test | 0,7 ngày | 1–4 |
| Âm tạm (Python + chép JSurviv) + mixer + catalog/asset | 0,4 ngày (mixer bằng reflection là chỗ dễ trễ nhất) | 5–6 |
| `AudioManager`, `MusicDirector`, panel F6 | 0,4 ngày | 7–8 |
| PlayMode test + Bootstrap (chờ ADHOC) + QA | 0,5 ngày | 9–10 |

Nếu trễ, phần có thể lùi mà không phạm P0 là MusicDirector crossfade/tắt mượt (AC-A24, A25 đều P1). Phần bắt buộc là AC-A23 (BGM tắt mặc định) và ducking AC-A22. AC-A22 chỉ đo được khi bật nhạc, nên tối thiểu cần phát được một bài.

### 4.6. Chia commit

Tiếng Anh, ngắn, commit kèm đường dẫn (`git commit -m "…" -- <file…>`), không Co-Authored-By, không nhắc AI, không push. Test EditMode đi cùng phần được test. Không đưa `ProjectSettings/*` hay file của Codex vào commit.

1. `feat(aud-01): add SFX ids, buses, priorities and rule table` — `Scripts/Domain/Audio/*` + `Tests/EditMode/Audio/SfxRuleTableTests.cs`.
2. `feat(aud-01): add audio service ports, gate, deduper and voice allocator` — port, `SfxGate`, `SfxDeduper`, `VoiceAllocator`, `SfxDistance`, `ClipPicker`, `SfxScheduler`, `AudioVolume`, `SfxPlaybackService` + test.
3. `feat(aud-01): add SFX router and match audio event source` — `SfxRouter`, `MatchAudioEventSource`, `MockAudioEventSource` + test.
4. `feat(aud-01): add music plan with crossfade and ducking` — `MusicPlan` + test.
5. `chore(aud-01): add placeholder SFX generator and copied JSurviv clips` — `Tools/Audio/*`, `Art/Audio/Placeholder/**`.
6. `feat(aud-01): add audio mixer, SFX catalog and runtime set assets` — `Art/Audio/JungleMazeAudio.mixer`, `Presentation/Audio/SfxCatalog.cs`, `AudioRuntimeSet.cs`, `Art/Audio/SfxCatalog.asset`, `Art/Audio/Resources/AudioRuntimeSet.asset` + `SfxCatalogTests`.
7. `feat(aud-01): add AudioManager voice pool and music director` — `AudioManager.cs`, `MusicDirector.cs`, `NullAudioService.cs`, `Infrastructure/Audio/PlayerPrefsAudioSettings.cs`.
8. `feat(aud-01): add dev-only audio test panel (F6)` — `AudioDebugPanel.cs`, `Scripts/Bootstrap/Audio/Bootstrap.Audio.cs` (compile được vì partial chưa được gọi).
9. `test(aud-01): add audio PlayMode acceptance tests` — `Tests/PlayMode/Audio/*`.
10. `feat(aud-01): wire audio composition into Bootstrap` — **chỉ** `Scripts/Bootstrap/Bootstrap.cs` (2 dòng). **Điều kiện:** ADHOC đã commit `Bootstrap.cs` và đã ghi dòng xin sửa ở `Agent_Coordination.vi.md` mục 4.

Sau G3, CPO thêm vào `Docs/Client_Developer_Guide.vi.md` mục 4 công thức "Phát âm thanh: implement `IAudioEventSource` hoặc gọi `IAudioService`, thêm mã vào `SfxId` + `SfxCatalog`".

### 4.7. Rủi ro và chỗ dễ vỡ

1. **Tạo AudioMixer bằng code** phải đi qua API editor nội bộ (`AudioMixerController`, expose parameter). API này đổi theo phiên bản Unity, nên reflection có thể lỗi trên 6000.3. Phương án dự phòng là người tạo tay khoảng 5 phút (T-2). Test AC-A5 kiểm đủ 5 tham số expose nên sẽ bắt được mixer dựng thiếu.
2. **`Bootstrap.cs` đang bị ADHOC sửa dở:** commit 10 bị chặn tới khi Codex commit. Trước đó QA vẫn chạy được PlayMode test (tự dựng `AudioManager`), nhưng chưa nghe được trong scene Game/HudPreview qua F6. Nếu tới 29/10 ADHOC vẫn chưa commit, CPO cần xin người dùng thứ tự.
3. **Chép file JSurviv:** chép cả `.meta` sẽ làm trùng GUID với bản gốc và Unity sẽ đổi GUID một trong hai, có thể làm hỏng tham chiếu của JSurviv. Kế hoạch chỉ chép file âm thanh, đổi tên ASCII. Giấy phép vẫn chưa rõ (Q5, thuộc ART-05).
4. **Hai nguồn cùng một sự kiện:** khi WPN-02 nối, `EventPacket.PlayerHit` và `IHudState.DamageTaken` cùng nói về một lần trúng. Kế hoạch chỉ nghe `IHudState` cho hit/hurt/pickup, và chỉ nghe `EventPacket` cho `PlayerDied`. Ticket sau phải giữ quy ước này, nếu không sẽ phát đôi.
5. **Cửa sổ ổn định 0,5 s** có thể nuốt một lần nhặt đồ thật ngay đầu trận. Chấp nhận được vì đầu trận chưa nhặt được gì, nhưng phải ghi lại (T-3).
6. **Ducking cần biết "âm Alert đang phát":** `SfxPlaybackService` theo dõi voice Alert còn `isPlaying`. Chuông có 3 lần lặp, nên giữa các lần (0,8 s) không có voice Alert nào: duck có thể nhả giữa chừng. `MusicPlan` coi Alert còn hoạt động khi `SfxScheduler` còn lần lặp chờ.
7. **Mixer chưa nạp lúc `Awake`:** `SetFloat` trong `Awake` đôi khi không ăn. Áp âm lượng trong `Start` và test đọc ở frame sau.
8. **Panel F6 và các IMGUI khác** (F3, F4, F5) có thể chồng nhau. F6 đặt góc trái dưới. Việc dời overlay là của UI-02.
9. **Âm có vị trí ở scene HudPreview** không có Hero (`HasPose = false`), nên âm có vị trí im lặng đúng như AC-A7. Panel F6 có nút "đặt pose giả (0,0)" qua `MockHudState` (dev-only) để demo khoảng cách.
10. **Ghi chú về spec (không sửa mục 1–3):**
    - AC-A22 ghi "không bao giờ hơn 16 AudioSource kể cả Music", trong khi AC-A3/A4 dùng pool 16 cho SFX. Kế hoạch: 16 voice SFX + 2 nguồn nhạc riêng, nên AC-A22 nên đọc là "SFX ≤ 16, Music ≤ 2" (T-4).
    - AC-A2 "đúng một AudioSource" với `ZoneBell3` được hiểu là tiếng đầu; 2 tiếng sau theo AC-A20.
    - AC-A26/A27 dùng thời gian giả lập, như các ticket trước.

### 4.8. Câu hỏi kỹ thuật cho G1 (T-1…T-8, có mặc định)

| # | Câu hỏi | Mặc định / khuyến nghị |
| --- | --- | --- |
| **T-1** | Sửa `Bootstrap.cs` bằng partial hook 2 dòng (`ComposeAudio`), phần thân ở `Scripts/Bootstrap/Audio/Bootstrap.Audio.cs`, làm ở commit cuối sau khi ADHOC commit `Bootstrap.cs`? | **Có.** Giữ file dùng chung gần như không đổi, phần còn lại nằm trong vùng AUD-01 |
| **T-2** | Mixer dựng bằng MCP + reflection API editor nội bộ, dự phòng là người dựng tay theo hướng dẫn? | **Có.** Thử reflection trước, lỗi thì unity-dev ghi hướng dẫn và CPO nhờ người dựng (khoảng 5 phút) |
| **T-3** | Cửa sổ ổn định 0,5 s sau khi vào trận/reset để không phát `WearArmor`/`Pickup*`/`ArmorAbsorb` từ trạng thái khởi tạo (AC-A15)? | **Có, 0,5 s**, giá trị nằm trên `AudioRuntimeSet` |
| **T-4** | Đếm nguồn: 16 voice SFX + 2 nguồn nhạc riêng (tổng tối đa 18), hay nhạc ăn vào 16? | **16 SFX + 2 nhạc riêng.** Đọc lại AC-A22 thành "SFX ≤ 16, Music ≤ 2". Giữ AC-A3/A4 đúng như chữ |
| **T-5** | Nhạc tạm: chép `Waiting Room Music.wav` 25,7 MB vào repo, hay dùng `Loading Music.mp3` cho cả màn hình Waiting Room cho tới khi ART-05 nén? | **Không chép file 25,7 MB.** Waiting Room tạm dùng `Loading Music.mp3`, ghi cho ART-05 |
| **T-6** | Panel F6 dùng ở scene dev sẵn có `Scenes/Dev/HudPreview.unity` (và Game), không tạo scene mới, không sửa scene? | **Có.** Scene là file dùng chung; Bootstrap tự tạo đã gắn panel |
| **T-7** | Âm có vị trí chỉ giảm âm lượng theo khoảng cách (mono, không pan trái/phải)? | **Có** cho AUD-01; pan theo hướng để mở rộng sau (gắn với FOW-01) |
| **T-8** | `MatchEnd` chỉ lấy từ `AppScreenChanged → Results`, không lấy `EventPacket.MatchEnded`; hit/hurt/pickup chỉ lấy từ port UI-01, không lấy `EventPacket` | **Có**, tránh phát đôi (rủi ro 4) |

**G1 · 27/10/2026 02:25 · CPO duyệt thay người dùng** (người dùng giao toàn quyền quyết định tới 12:00 27/10, xem `Docs/CPO_Runbook.vi.md` mục 1). Duyệt toàn bộ mặc định khuyến nghị của Q1–Q14, T-1…T-8 và giả định G-x. Cụ thể:
- 26 SFX theo bảng 2.1. Các âm ngoài 26 mã để ticket sau.
- BGM mặc định tắt; chỉ dựng `MusicDirector` và công tắc F6.
- Phạm vi nghe tối đa 12 ô, âm nhỏ thì ngắn hơn.
- Rồng và các âm còn thiếu dùng tiếng tạm tổng hợp.
- Giấy phép âm thanh JSurviv thuộc ART-05.
- Hit marker chỉ khi trúng Hero. `BushEnter` chỉ người chơi local nghe. Tiếng dùng đồ phát lúc bắt đầu và lúc hủy.
- AUD-01 lưu âm lượng (PlayerPrefs); UI-03 chỉ gọi API.
- Có bus Alert. Chuông bo 3 tiếng cách nhau 0,8 s. Bẫy và bước chân dùng mock hoặc suy từ vị trí. `ArmorAbsorb` phát chồng với `HurtLocal`.
- T-1: hook 2 dòng trong `Bootstrap.cs`, sửa sau cùng, sau khi ADHOC commit file này.
- T-5: không chép file nhạc 25,7 MB.
- T-8: không phát đôi từ `EventPacket`.

**Điều kiện thứ tự:** Unity Editor đang được ADHOC-UI-ASSETS-A dùng. AUD-01 chỉ viết C# và dùng Unity khi ADHOC đã commit xong code và test (CPO báo). Trước đó chỉ làm phần không cần Unity: script sinh âm thanh tạm, file WAV/OGG tạm, chép clip JSurviv vào `Art/Audio`.

## 5. Nhật ký build

### Pha 0 · 27/10/2026 · unity-dev (không dùng Unity, không C#)

**Đã làm:**

- `Tools/Audio/make_placeholder_sfx.py` (Python 3, chỉ thư viện chuẩn) sinh 14 WAV mono 16-bit 44,1 kHz vào `Art/Audio/Placeholder/Beeps/`. File có fade hai đầu, chuẩn hóa đỉnh 0,7 (khoảng −3 dBFS). Chạy lại cho kết quả giống từng byte (đã so md5). Tên file ghi tần số gốc để test `BeepsHaveDistinctFrequencies` đọc được. Mỗi mã có một kiểu tiếng riêng, không chỉ khác tần số.
- `Tools/Audio/copy_jsurviv_placeholders.py` chép 12 SFX và 2 bài nhạc từ JSurviv sang `Art/Audio/Placeholder/JSurviv/`, đổi tên sang ASCII. Script chỉ chép file âm thanh, không chép `.meta`. Tham số `--check` so từng byte với bản gốc: cả 14 file khớp. `git status -- Assets/Imported/JSurviv` trống.
- `Tools/Audio/README.md`: cách chạy và ghi chú đây là âm tạm.

**Ánh xạ âm tạm (đủ 26 mã):**

| SfxId | File | Ghi chú |
| --- | --- | --- |
| FootstepGrass | `Beeps/footstep_grass_330hz.wav` | 0,09 s, tiếng thịch nhẹ và sột soạt |
| FootstepSwamp | `Beeps/footstep_swamp_392hz.wav` | 0,18 s, tiếng lép nhép trầm |
| BushEnter | `Beeps/bush_enter_467hz.wav` | 0,26 s, ba đợt lá xào xạc |
| MeleeSwing | `JSurviv/melee_slash.mp3` | `melee chém.mp3` |
| MeleeHit | `JSurviv/hit_thud.wav` | `Đập.wav` |
| ShootRanged | `JSurviv/gun_shot.mp3` | `súng bắn.mp3` |
| ShootCrossbow | `Beeps/shoot_crossbow_555hz.wav` | 0,20 s, tiếng dây bật |
| Throw | `Beeps/throw_660hz.wav` | 0,26 s, tiếng vút |
| Reload | `Beeps/reload_785hz.wav` | 0,24 s, hai tiếng lách cách |
| EmptyClick | `Beeps/empty_click_933hz.wav` | 0,06 s, một tiếng tách khô |
| BombFuse | `JSurviv/bomb_fuse_countdown.mp3` | `countdown.mp3` |
| Explosion | `JSurviv/bomb_explosion.mp3` | `bomb nổ.mp3` |
| TrapTriggered | `Beeps/trap_triggered_1110hz.wav` | 0,22 s, tiếng sập |
| HitMarker | `Beeps/hit_marker_2220hz.wav` | 0,06 s, tiếng tick cao, gọn và sắc |
| HurtLocal | `JSurviv/hurt_bleed.mp3` | `bleed.mp3` |
| ArmorAbsorb | `Beeps/armor_absorb_1320hz.wav` | 0,30 s, tiếng kim loại (partial lệch) |
| Eliminated | `Beeps/eliminated_1570hz.wav` | 0,75 s, tiếng rơi xuống hai quãng tám |
| PickupWeapon | `JSurviv/pickup_weapon.mp3` | `nhặt súng.mp3` |
| PickupItem | `JSurviv/pickup_item_gun_put_on.mp3` | `Gun Put On.mp3` |
| WearArmor | `JSurviv/wear_armor_helmet_put_on.mp3` | `Helmet Put On.mp3` |
| UseHeal | `JSurviv/use_heal_gauze.mp3` | `gauze.mp3` |
| UseDrink | `JSurviv/use_drink_cola.mp3` | `cola.mp3` |
| UseCancel | `Beeps/use_cancel_1867hz.wav` | 0,18 s, hai nốt đi xuống |
| ZoneBell3 | `Beeps/zone_bell_880hz.wav` | 0,7 s, chuông gõ (hum 440 Hz + partial 1/1,5/2/2,76/4,07). Lặp 3 lần theo catalog |
| DragonRoar | `Beeps/dragon_roar_70hz.wav` | 2,0 s ≥ 1,5 s, tiếng gầm trầm: saw quét 70→210→91 Hz, điều biên 30 Hz, nhiễu thở |
| MatchEnd | `JSurviv/match_end_time_over.wav` | `TimeOver.wav` |
| Nhạc Loading, WaitingRoom | `JSurviv/Music/loading_music.mp3` | T-5: không chép `Waiting Room Music.wav` |
| Nhạc Match | `JSurviv/Music/parking_map_music.mp3` | 4,9 MB |

**Lệch kế hoạch và lý do:**

1. `HitMarker` dùng tiếng tổng hợp, **không** dùng `button.mp3` như bảng 2.1. Mục 2 dành `button.mp3` cho tiếng nút UI (UI-02/UI-03), và CPO yêu cầu hitmarker "gọn sắc". Nếu dùng chung, tiếng bấm nút và hitmarker sẽ giống nhau. Vì vậy `button.mp3` không được chép.
2. `ShootCrossbow` và `TrapTriggered` dùng tiếng tổng hợp riêng, không dùng `súng bắn.mp3`/`Đập.wav` hạ cao độ. Bảng 2.1 cho phép "hoặc bíp", và `SfxRule` không có trường cao độ gốc, chỉ có jitter.
3. `zone_bell` dài 0,7 s thay vì 0,5 s như 4.3, để tiếng chuông ngân tắt tự nhiên. Vẫn ngắn hơn khoảng lặp 0,8 s nên ba tiếng không chồng nhau.
4. Thêm `copy_jsurviv_placeholders.py` (không có trong 4.3) để việc chép tái lập và kiểm được. `Tools/Audio/**` nằm trong vùng AUD-01.

**Phát hiện cho ART-05:** `Gun Put On.mp3` và `Helmet Put On.mp3` cùng kích thước nhưng **khác nội dung** (md5 khác nhau), không phải file trùng.

**Commit:** chỉ `Tools/Audio/*` và mục này. File âm thanh trong `Art/Audio/Placeholder/**` chưa có `.meta`, nên sẽ commit ở pha 1 sau khi Unity import (commit 5 của 4.6).

**Còn lại (pha 1, chờ CPO báo "Unity rảnh"):** commit 1–4 và 6–10 của mục 4.6.

### Pha 1 · 27/10/2026 · unity-dev (giữ Unity Editor, đã ghi ở `Agent_Coordination.vi.md` mục 4)

**Đã làm:** đủ các lớp và file của mục 4.3, theo 4.1 và 4.2.

- Âm thanh đã import. SFX dùng Decompress On Load, ADPCM, ép mono. Nhạc dùng Streaming, Vorbis 0,7, load in background.
- Mixer `Art/Audio/JungleMazeAudio.mixer` dựng bằng reflection (T-2), không cần người dựng tay:
  - Nhóm: `Master` → `SFX` → `Alert`, `Master` → `Music` → `MusicTrack`.
  - 5 tham số expose: `MasterVolume`, `SfxVolume`, `AlertVolume`, `MusicVolume`, `MusicDuck`.
  - Mixer tạo bằng code chưa có "view" nào, nên đã tạo thêm một view mặc định để cửa sổ Audio Mixer hiện đủ nhóm.
- `SfxCatalog.asset` có 26 mục đúng bảng 2.1. `ZoneBell3` lặp 3 lần, cách 0,8 s.
- Âm lượng catalog do dev chọn (spec không ghi): HitMarker, ZoneBell3, DragonRoar, MatchEnd 1,0 · FootstepGrass 0,4 · FootstepSwamp 0,45 · âm Low khác 0,5 · còn lại 0,6–0,9. Pitch jitter 0,05; HitMarker 0,03; các âm dài hoặc cảnh báo 0. ART-05 chỉnh lại khi nghe (AC-A28).
- `Resources/AudioRuntimeSet.asset`: Waiting Room tạm dùng `loading_music.mp3` (T-5).

**Commit (kèm đường dẫn, không push):**

| Commit | Nội dung |
| --- | --- |
| `485c10ab` | Âm thanh tạm và `.meta` |
| `ca7fb134` | Domain |
| `0b66f8e2` | Port, gate, deduper, allocator, MusicPlan, playback service |
| `3b04a3cb` | Router, match source, mock và test EditMode |
| `1fb09b78` | Mixer, catalog, runtime set và `SfxCatalogTests` |
| `78b4624b` | AudioManager, MusicDirector, NullAudioService, PlayerPrefs |
| `3d3f8faf` | Panel F6 và `Bootstrap.Audio.cs` |
| `ec8ad0c5` | Test PlayMode |
| `4d65a32e` | Sửa: listener dự phòng, `Stop()` source mới |
| `9ab20f1a` | Hook `Bootstrap.cs`, 1 dòng |

**Lệch kế hoạch và lý do:**

1. **`Bootstrap.cs` chỉ thêm 1 dòng gọi** `ComposeAudio(bus, tickRunner, hudState, inventoryState, balance, match, network, flow)`, không có khai báo `partial void`.
   - Nếu `Bootstrap.Audio.cs` chứa phần thân của một partial method mà chưa có khai báo, commit 8 sẽ không compile. Vì vậy `ComposeAudio` là method private trong file partial.
   - Chữ ký dùng `ITickScheduler` thay `TickRunner`, bỏ `IPlayerPoseSource` (resolve trong thân hàm), thêm `GameFlow`.
   - Lý do thêm `GameFlow`: router đăng ký sau GameFlow nên lỡ `AppScreenChanged` đầu tiên. Router nhận `Func<AppScreen>` để đặt màn hình hiện tại cho `MusicPlan` lúc `Initialize`.
2. **`IAudioService` thêm `Play(in AudioCue)`.** Router cần phát âm 2D có khóa nguồn, ví dụ để cooldown `PickupItem` tính theo từng món: hai món khác nhau trong cùng frame đều phát (AC-A12).
3. **Thêm `IMatchAudioFeed` và `MatchSessionAudioFeed`** (trong `MatchAudioEventSource.cs`), để test `MatchAudioEventSource` không cần `MatchSession` và mạng.
4. **Router có cửa sổ 150 ms (G-6)** cho sự kiện HUD không có khóa, cùng loại và cùng nội dung, chạy trước cooldown của mã. Nhờ vậy hai `HitDealt` cách nhau 100 ms chỉ phát một lần dù cooldown `HitMarker` là 60 ms (AC-A12, AC-A19).
5. **Nhặt mũ/giáp không phát `PickupItem`.** Tiếng nhặt mũ/giáp đến từ `WearArmor` khi món đang mặc đổi, theo bảng 2.1 ("món không phải vũ khí/giáp").
6. **Dự phòng AudioListener (ngoài kế hoạch, cần CPO biết).**
   - Cả `Boot.unity`, `Game.unity` và `HudPreview.unity` đều **không có AudioListener**, nên game không phát ra tiếng và console spam "There are no audio listeners".
   - `AudioManager` tự gắn một listener lên `[Audio]` khi không có listener nào đang bật. Khi scene load có listener riêng thì tắt listener này. Âm đều 2D nên vị trí listener không quan trọng.
   - Không sửa scene (file dùng chung).
7. **Hai lỗi Unity phát hiện khi test:**
   - AudioSource tạo bằng `AddComponent` đã chạy Awake với `playOnAwake` bật, nên báo `isPlaying = true` dù không có clip. Sửa: gọi `Stop()` ngay khi tạo, và voice chỉ được tính "đang phát" khi có clip.
   - Thêm mốc kết thúc dự phòng (`clip.length / pitch`) cho trường hợp thiết bị âm thanh tắt.
8. **`SfxPlaybackService.DroppedNoVoice(priority)`:** bộ đếm chẩn đoán, để AC-A26 kiểm được "không mất âm Critical vì hết voice".
9. **Chia commit:** commit 1–4 gộp thành 3 commit để commit nào cũng compile được (test dùng chung `AudioTestSupport`). File helper tách đúng tên ở 4.3.

**Test (chạy thật qua Unity MCP):**

- EditMode `JungleMaze.Tests.Audio.*`: 52/52. Toàn bộ EditMode: **326/326**.
- PlayMode `AudioAcceptanceTests`: 13/13. Toàn bộ PlayMode: **56/56** ở lần chạy cuối.
  - Một lần chạy toàn bộ trước đó có `HudAcceptanceTests.Hud_HoldsValues10sWithoutUpdates` lệch 4,2e-6 (adrenaline). Test này không liên quan audio, chạy riêng thì đạt, chạy lại toàn bộ cũng đạt. Đây là lỗi chập chờn về timing của test HUD.
- Console sạch sau compile và sau test. Còn 2 cảnh báo CS0618 cũ ở `HudFeedbackPrefabBuild.cs`, không thuộc AUD-01.
- Smoke trong `Scenes/Dev/HudPreview.unity` (mở sẵn, không lưu):
  - Play: Bootstrap nối audio, 16 voice SFX và 2 nguồn nhạc, listener dự phòng bật.
  - Gọi nút panel F6 (HitMarker, ZoneBell3, DragonRoar, Spam 100, Gửi lặp ×5): phát đúng, voice ≤ 16, console không lỗi hay cảnh báo.
  - Đã thoát Play Mode. `ProjectSettings/EditorSettings.asset` đã `git checkout`.
- `git status -- Assets/Imported/JSurviv`: trống.

**Ánh xạ AC → test:**

| AC | Test |
| --- | --- |
| A1 | E `SfxCatalogTests.Has26Ids_EachHasClipAndRuleFromTable` |
| A2 | P `PlaySfx_EachOfThe26_OneVoiceRightClipAndBus` |
| A3 | P `Spam100_NeverMoreThan16_NoNewAudioSources`; E `PoolBound_100Requests_…` |
| A4 | E `VoiceAllocatorTests.*`; P `PoolFullOfLow_CriticalEvictsExactlyOne` |
| A5 | E `AudioVolumeTests`, `SetVolume_DecibelsClampedAndSaved`; P `SetVolume_MixerDecibels`; E `RuntimeSet_MixerHasBusesAndExposedParams` (nhóm con) |
| A6 | E `CooldownAndMaxInstances_PerGroup`, `SfxGateTests.*` |
| A7 | E `DistanceFalloff`, `NoPose_PositionalSilent_2DPlays`, `SfxDistanceTests` |
| A8 | P `DebugPanel_PlayButtonsCallService`; review `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` (chưa kiểm trên Release build thật) |
| A9 | E `DistinguishableIdsUseDistinctClips`, `BeepsHaveDistinctFrequencies`; git diff JSurviv trống |
| A10 | E `ClipPickerTests`; P `SwapClipInCatalogCopy_NoCodeChange` |
| A11 | E `SfxDeduperTests.*`, `KeyedCues_PlayOncePerKey`, `PlayerDied_KeyedCue` |
| A12 | E `HudEventsWithinCooldown_OnePlay`, `DifferentPickupIds_BothPlay` |
| A13 | P `RepeatServerEvent_AddsAtMostOneVoice` |
| A14 | E `MapsEveryAvailableEvent` |
| A15 | E `ResetAndInitialState_Silent`, `TakeOff_Silent`, `DurabilityDropWithoutDamage_Silent` |
| A16 | E `SecondSourceImplementation_SameOutput`; review: `Application/Audio` chỉ dùng HUD port, EventBus, Protocol, MatchSession |
| A17 | E `MockRaisesAll12Unsourced`, `NothingWithoutCalls_600Ticks` |
| A18 | E `MissingClip_WarnOnce_NoThrow`; P `LogAssert.NoUnexpectedReceived` ở A2/A3/A26 |
| A19 | E `HitMarker_FiveIn60ms_OnePlay`, `HitMarkerLoud_…`; P `HitMarker_StartsSameFrame_With15Others` |
| A20 | E `WarningRisingEdge_OncePerPhase`, `SfxSchedulerTests.ThreeBells_08s`, `ZoneBell_ThreePlaysOnAlert_…`; P đếm 3 tiếng chuông |
| A21 | E `HitMarkerLoud_DragonRoarLongAndUnique` (2,0 s, Alert, 5 s, clip riêng) |
| A22 | E `DuckMinus9In02_ReleaseIn1`; P `ZoneBell_ThreeBellsOnAlert_DucksMusic_SfxUntouched` (đo `MusicDuck`/`SfxVolume` theo thời gian thật; SFX ≤ 16, Music ≤ 2 theo T-4) |
| A23 | P `MusicOffByDefault_NoMusicSource`, `EnableMusic_PlaysScreenTrackWithin1s_DisableFadesOut` |
| A24 | E `ScreenMapping`, `Crossfade1s_AtMostTwo`, `SameTrack_NoRestart` |
| A25 | E `DisableFadesOutIn04`; P phần tắt nhạc của test A23 |
| A26 | P `Burst10s_2000Requests_NoErrors_CriticalNotLost` (≥ 2 000 yêu cầu, `DroppedNoVoice(Critical) = 0`) |
| A27 | E `PlaySfx100_NoGcAlloc`, `Dispose_UnsubscribesAll`, `MatchAudioEventSourceTests.Dispose_Unsubscribes`; Profiler (P2) chưa đo |
| A28 | Cần PIC nghe qua F6, chưa làm |

**Chưa kiểm chứng:**

- Nghe bằng tai (AC-A28).
- F6 không có trong Release build: mới kiểm bằng review `#if`, chưa build.
- Profiler GC trong trận thật (AC-A27, P2).
- Scene Game với GameServer: chưa chạy, vì AUD-01 không đổi luồng mạng; `PlayerDied` và zone mới đo bằng packet giả.

**Rủi ro và việc cho ticket khác:**

- **Cho ART-05:**
  - `bomb_fuse_countdown.mp3` dài 11,8 s trong khi ngòi chỉ 1,5 s. Âm này giữ một voice 11,8 s và vẫn kêu sau khi bom nổ. ART-05 nên cắt, hoặc ticket sau dừng `BombFuse` khi `Explosion`.
  - `bomb_explosion.mp3` (5,4 s), `cola`/`gauze` (khoảng 3 s) cũng dài.
- **Panel F6** ở góc trái dưới có thể chồng F3/F4/F5 (đã ghi ở 4.7).
- **AudioListener:** nên do camera của scene giữ (World/scene ticket). Bản dự phòng tự tắt khi scene có listener riêng.

## 6. Review

<code-reviewer: lỗi theo mức Chặn / Nên sửa / Gợi ý, trạng thái đã xử lý.>

## 7. QA

| # | Kết quả | Bằng chứng |
| --- | --- | --- |
| AC-A1 | ✓ / ✗ / chưa kiểm chứng | |

## 8. Nghiệm thu

<CPO tổng hợp; người duyệt G3, ngày, ghi chú. Nhắc cập nhật Status trong GDD.>
