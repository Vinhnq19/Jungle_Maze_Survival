# AUD-01 — AudioManager và phản hồi âm thanh trong game

| Trường | Giá trị |
| --- | --- |
| Giai đoạn / Deadline | Giai đoạn 2 / 30/10/2026 (ô Deadline = 46325, Excel serial) |
| Module | Audio |
| Ưu tiên | P1 |
| PIC | V.Huy |
| Phụ thuộc | Chính thức: CLI-03 (Done). Làm song song bằng âm thanh tạm ("Làm song song: dùng placeholder để test event bus trước khi ART-05 nộp asset cuối"). Nguồn sự kiện thật, **không chặn** ticket này (xem mục 2): WPN-01 · WPN-02 · ITM-01 · Game-10 · ZON-01 · SRV-03 · DRG-01 · FOW-01: đều Not Yet. ART-05 (kiểm kê file, license, metadata): Not Yet, P2, phụ thuộc None |
| Trạng thái | Spec (chờ G1) |
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

## 4. Kế hoạch kỹ thuật

<tech-lead điền sau G1.>

## 5. Nhật ký build

<Dev ghi ngắn: đã làm gì, quyết định phát sinh, lệch kế hoạch và lý do.>

## 6. Review

<code-reviewer: lỗi theo mức Chặn / Nên sửa / Gợi ý, trạng thái đã xử lý.>

## 7. QA

| # | Kết quả | Bằng chứng |
| --- | --- | --- |
| AC-A1 | ✓ / ✗ / chưa kiểm chứng | |

## 8. Nghiệm thu

<CPO tổng hợp; người duyệt G3, ngày, ghi chú. Nhắc cập nhật Status trong GDD.>
