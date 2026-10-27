# ART-05 — Kiểm kê và đưa âm thanh JSurviv vào AudioManager

| Trường | Giá trị |
| --- | --- |
| Giai đoạn / Deadline | Giai đoạn 2 / 30/10/2026 (ô Deadline = 46325, Excel serial) |
| Module | Audio |
| Ưu tiên | P2 |
| PIC | V.Huy |
| Phụ thuộc | Chính thức: None. Thực tế dùng `SfxCatalog.asset`, `AudioRuntimeSet.asset`, mixer và panel F6 của **AUD-01** (Done, G3 27/10/2026), nên không chặn. Nguồn thật của sự kiện (WPN-01, WPN-02, ITM-01, DRG-01, ZON-01) chưa có: kiểm bằng panel F6 và nguồn giả của AUD-01, như dòng ticket ghi ("phát từng clip bằng bảng event giả rồi thay vào event thật") |
| Trạng thái | Spec (chờ G1) |
| Sheet GDD tham chiếu | Dòng ART-05 trong PROJECT TRACKING. Ref ghi "Assets!11–16" là **số dòng trong sheet** (lệch +2 so với cột STT, như AUD-01 đã xác nhận): **Assets STT 9–14** (9 Bước chân/Tương tác môi trường · 10 Dùng vũ khí/Bom/Bẫy · 11 Gây/Nhận sát thương/Bị loại · 12 Nhặt/Đổi/Dùng đồ · 13 Cảnh báo/Kết quả/Kết nối · 14 Nhạc nền). Cột "Link / License" của sheet Assets **để trống ở cả 6 dòng này**. Số liệu thêm: Equipments STT 7–14, 16 (thời gian dùng đồ), Weapons STT 11–15 (ngòi bom), MATCH DESIGN DATA (`zoneWarningTime`, `dragonFlyDuration`, `matchDuration`, `visionRadius`), WIREFRAME STT 11–13. Đầu vào: `Docs/Tickets/AUD-01.md` (mục 2.1, 5, 7, 8), `Docs/JSurviv_Integration.vi.md`, `Docs/JSurviv_Verification.vi.md` |
| Figma node | Không có (âm thanh không có Figma) |

## 1. Mục tiêu

Biến thư mục âm thanh JSurviv nhập sẵn (`Assets/Imported/JSurviv/Sound`) từ "có vẻ dùng được" thành một **bảng sự thật**: trong 26 mã SFX của AUD-01, mã nào đã có file dùng được, mã nào cần cắt/chuẩn hóa, mã nào thiếu; từng file có định dạng, độ dài, kích thước, nguồn và tình trạng giấy phép ghi rõ. Sau đó đưa các clip đã xử lý vào game **chỉ bằng dữ liệu** (clip trong `SfxCatalog.asset`, nhạc trong `AudioRuntimeSet.asset`, import settings), không sửa mã `AudioManager`/router của AUD-01, để người chơi nghe đúng hitmarker, tiếng nhận sát thương, bắn, chém, nhặt đồ, chuông cảnh báo bo và tiếng rồng. Mọi clip chung một mức âm có kiểm đo được, nên không clip nào lấn át cảnh báo. Nhạc nền (BGM) được chuẩn bị (nén, đo, ánh xạ) nhưng **mặc định vẫn tắt** vì GDD chưa chốt phạm vi.

Ticket **không** quyết định quyền sử dụng: mọi file JSurviv ghi "chưa kiểm chứng" cho tới khi có bằng chứng bằng văn bản trong repo.

## 2. Phạm vi và ranh giới

**Trong phạm vi:**

1. **Kiểm kê** 21 file trong `Assets/Imported/JSurviv/Sound` (19 file âm thanh + 2 file `.zip` chứa 9 clip): định dạng, tần số lấy mẫu, kênh, độ dài, kích thước, mức đỉnh/mức trung bình (khi đo được), nguồn và giấy phép. Kết quả ghi vào manifest `Docs/Tickets/ART-05_audio_manifest.csv` (một dòng cho mỗi clip có trong repo, xem 2.5).
2. **Đối chiếu 26 SfxId với file thật** (bảng 2.2): trạng thái **Có / Cần chỉnh / Thiếu**. Chỉ "Có" khi có file đúng loại sự kiện, độ dài trong khoảng đích (bảng 2.3) và không cần ghép/cắt. Chưa ai nghe file: mọi trạng thái là **tạm** cho tới khi PIC nghe (AC6).
3. **Xử lý clip** cho các mã "Cần chỉnh" và "Thiếu" có nguồn: giải nén (zip), cắt, fade hai đầu, đổi mono, chuẩn hóa mức, xuất WAV 16-bit. Chuẩn hóa theo mục 2.6. Mọi thao tác nằm ngoài `Assets/Imported`.
4. **Nơi lưu clip cuối** (quyết định ở 2.4): sao chép/chuyển đổi vào `Assets/_JungleMaze/Art/Audio/Final/`, không tham chiếu `Assets/Imported`.
5. **Thay clip trong catalog bằng dữ liệu**: `SfxCatalog.asset` (`clips`, `volume`, `pitchJitter`) và `AudioRuntimeSet.asset` (nhạc), cùng cài đặt import của từng file (2.5, 2.7).
6. **BGM**: nén file nhạc, đo mức, ánh xạ vào `AudioRuntimeSet`, giữ mặc định tắt (2.8).
7. **Giấy phép/nguồn**: ghi nhận từng file, nêu bằng chứng tìm được, không khẳng định (2.9).
8. **Bàn giao cho AUD-01 đã ghi** và được nhận vào ticket này: cắt `countdown.mp3` (11,8 s) và `bomb nổ.mp3` (5,4 s); chọn lại âm nếu người nghe không hài lòng (AC-A28 của AUD-01); thay Waiting Room tạm (`loading_music.mp3`, quyết định T-5 của AUD-01).

**Ngoài phạm vi (thuộc ticket/người khác):**

- Mọi mã trong `Scripts/**` của audio: `AudioManager`, `SfxRouter`, `SfxPlaybackService`, `MusicPlan`, `VoiceAllocator`, `Bootstrap.Audio.cs`, panel F6 → **AUD-01**. ART-05 chỉ sửa dữ liệu (catalog, runtime set, import settings, file clip). Cột `priority`, `bus`, `rangeCells`, `maxInstances`, `cooldownSeconds`, `repeatCount`, `repeatIntervalSeconds` của catalog do G1 AUD-01 duyệt: **không đổi**, trừ `repeatCount` của `ZoneBell3` nếu clip cuối đã chứa sẵn ba tiếng (xem AC28) và chỉ khi PIC đồng ý
- Thêm mã `SfxId` mới (ngoài 26): cần sửa enum → **AUD-01 / ticket sau**. Danh sách "mở rộng" ghi ở Q8
- Nguồn sự kiện thật: bắn/chém/ném/nạp (WPN-01), trúng đòn (WPN-02), nhặt/dùng/mặc (ITM-01), rồng (DRG-01), bo (ZON-01), bước chân (bộ suy riêng) → các ticket đó; kiểm bằng F6/nguồn giả
- Giao diện cài đặt âm lượng và lưu mức âm lượng → UI-03 và AUD-01 (Q9 AUD-01). Tiếng nút bấm và tiếng UI (`button.mp3`) → UI-02/UI-03
- **Sửa file trong `Assets/Imported/**`** (kể cả `.meta`, cài đặt import của file gốc): không bao giờ. Mã nguồn âm thanh/mạng cũ của JSurviv không sửa
- Việc của người khác: Hào (hero/map/balance), Vinh (backend, UI-02, SRV-03), Quang Huy (ITM-01, ART-03), Vũ (MOV-02, VFX-01). ART-05 không đụng file trong vùng của họ
- Quyết định có phát hành với clip chưa rõ giấy phép hay không: người dùng/CPO (Q5). ART-05 chỉ báo cáo

**Vùng file của ART-05** (không ngoài các đường dẫn này, theo `Docs/Agent_Coordination.vi.md`):

| Đường dẫn | Nội dung |
| --- | --- |
| `Assets/_JungleMaze/Art/Audio/Final/**` | Clip cuối (SFX, nhạc) và `.meta` |
| `Assets/_JungleMaze/Art/Audio/SfxCatalog.asset`, `Resources/AudioRuntimeSet.asset` | Chỉ thay clip, `volume`, `pitchJitter`, tham chiếu nhạc |
| `Assets/_JungleMaze/Tests/**/Audio/**` | Chỉ test **dữ liệu** (đọc catalog/clip, không logic runtime), nếu cần |
| `Tools/Audio/**` | Script đo/chuẩn hóa dùng thư viện chuẩn Python |
| `Docs/Tickets/ART-05*` và `Docs/Licenses/Audio/**` | Spec, manifest, bằng chứng giấy phép |

**Số liệu từ GDD và code** (sheet, ô/dòng; "giả định" nếu GDD chưa có):

| Tham số | Giá trị | Nguồn |
| --- | --- | --- |
| Số SFX cần có file | 26 (do AUD-01 chốt; GDD chỉ có nhóm) | `Docs/Tickets/AUD-01.md` mục 2.1; Assets STT 9–13 |
| Giấy phép trong GDD | Cột "Link / License" trống ở Assets STT 9–14; "Tình trạng": STT 9, 11, 12, 13 "Chưa chọn nguồn", STT 10 "Chờ roster", STT 14 "Chưa chốt phạm vi" | Assets sheet |
| Ghi chú nhạc nền | "Cảm xúc căng thẳng xen các pha hỗn loạn; không che tín hiệu chiến thuật." | Assets STT 14 |
| Ngòi bom | Chuối, Thiên thạch, Tinh thể, Hạt: 1,5 s; Bom khói: 1,0 s | Weapons STT 11–15 |
| Thời gian dùng đồ | Hồi máu 0,5 / 0,8 / 1,2 / 2,0 / 3,5 s; hỗ trợ 1,5 / 1,0 / 1,0 / 2,0 s | Equipments STT 7–14, 16 |
| Cảnh báo bo | 10 s trước mỗi lần co | MATCH DESIGN DATA `zoneWarningTime` |
| Rồng bay | 10 s | MATCH DESIGN DATA `dragonFlyDuration` |
| Thời lượng trận | 420 s (7:00) | MATCH DESIGN DATA `matchDuration` |
| Tầm nhìn (cơ sở cho phạm vi nghe) | 12 cell | MATCH DESIGN DATA `visionRadius` |
| Pool và chuông | 16 voice SFX + 2 nguồn nhạc; chuông bo 3 tiếng cách 0,8 s; nhạc hạ −9 dB khi có cảnh báo | AUD-01 (G-2, G-8, G-10, T-4) |
| Catalog và volume hiện tại | Volume: HitMarker, ZoneBell3, DragonRoar, MatchEnd 1,0; FootstepGrass 0,4; FootstepSwamp 0,45; HurtLocal, Explosion 0,9; còn lại 0,5–0,8. Chỉ đọc/đổi được `volume`, `pitchJitter`, `clips` | `Art/Audio/SfxCatalog.asset` (đọc 27/10) |
| Cài đặt import AUD-01 | SFX: Decompress On Load, ADPCM, **forceToMono = 1, normalize = 1**; nhạc: Streaming, Vorbis 0,7. Xem rủi ro R3 | `.meta` trong `Art/Audio/Placeholder/**` |
| Ngân sách dung lượng | Tối đa 3,0 MB cho mỗi file nhạc, 0,5 MB cho mỗi clip SFX, 12 MB tổng file mới | **Giả định G-9** |

### 2.1. Hiện trạng trong repo (đo ngày 27/10/2026)

- `Assets/Imported/JSurviv/Sound`: **21 file** (19 âm thanh + 2 zip), tổng **39 658 338 byte (≈ 39,7 MB)**. Riêng `Waiting Room Music.wav` là 25,7 MB (65 %). Import ở commit `09e0f1cc` ngày 12/10/2026. `git status` của thư mục này sạch.
- **Không có** file giấy phép, README hay credit nào trong `Sound`. `Docs/JSurviv_Integration.vi.md` và `Docs/JSurviv_Verification.vi.md` không nhắc âm thanh hay giấy phép (đã tìm theo "sound", "âm thanh", "nhạc", "licen", "giấy phép": không có kết quả).
- AUD-01 đã **chép** (không tham chiếu) 12 file SFX + 2 nhạc sang `Art/Audio/Placeholder/JSurviv/` với tên ASCII, và sinh 14 file bíp (`Placeholder/Beeps`, tự tạo bằng `Tools/Audio/make_placeholder_sfx.py`). Catalog hiện trỏ tới các bản chép/bíp này. Không clip nào là bản **đã xử lý**.
- Dấu vết trong metadata của file (chỉ là dữ kiện, không phải bằng chứng về quyền sử dụng):
  - `TimeOver.wav` có khối `bext` ghi "Clock, Alarm Loop, Wind Up, Ringing" và **www.hollywoodedge.com**: dấu hiệu đây là âm thanh lấy từ một thư viện hiệu ứng thương mại.
  - `ParkingMapMusic.mp3`: năm 2023, công cụ "Studio One 6.2.1". `Gun Put On.mp3`, `Helmet Put On.mp3`: năm 2020, "LAME in FL Studio 10". `bleed`, `bot di chuyển`, `countdown`, `melee chém`, `nhặt súng`, `revive`, `súng bắn` (mp3): năm 2022, bốn trong số đó ghi "Pro Audio Converter 1.8.1". `Waiting Room Music.wav`, `Loading Music.mp3`, `Bo co.mp3`, `bomb nổ.mp3`, `button.mp3`, `cola.mp3`, `gauze.mp3`, `Đập.wav`: không có thẻ nào ngoài định dạng.
  - `pick-impacts-2023-11-27-05-02-53-utc.zip` có tên kiểu tệp tải về (ngày giờ UTC), chứa `Pick_Impacts_ODY-0409-030.wav`: nguồn không ghi.

### 2.2. Bảng 26 SFX đối chiếu file thật

**Chú thích.** **File thật** = ứng viên trong `Assets/Imported/JSurviv/Sound` (tên theo thư mục, `Nhặt đồ/` là thư mục con). **Có** = đúng loại sự kiện (theo tên và độ dài) và độ dài nằm trong khoảng đích (bảng 2.3), không cần ghép hay giải nén; **Cần chỉnh** = có file nhưng phải cắt/chuẩn hóa/giải nén/chọn lại; **Thiếu** = không file nào khớp, hiện dùng bíp tự sinh của AUD-01. Mọi trạng thái **chưa nghe** (AC6). **Định dạng** viết dạng `codec, tần số, kênh, kbps`. "Mức đo" là mức đỉnh / mức trung bình **cả file** (dBFS), chỉ đo được với WAV; MP3 cần bộ giải mã, ghi "chưa đo". **Giấy phép** = "chưa kiểm chứng" cho mọi file JSurviv (mục 2.9). ★ = thuộc 7 nhóm ưu tiên của ticket (hitmarker, nhận sát thương, bắn, melee, loot, cảnh báo bo, rồng); ◆ = bàn giao từ AUD-01.

| # | SfxId | File thật | Trạng thái | Độ dài | Định dạng · kích thước | Việc cần làm (ART-05) |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | `FootstepGrass` | `bot di chuyển.mp3` (ứng viên theo tên, **chưa nghe**) | Cần chỉnh | 0,57 s | mp3, 44,1 kHz, stereo, 134 kbps · 10 176 B | Nghe xác nhận có phải bước chân; nếu đúng thì cắt ≤ 0,40 s, mono. Nếu không phải: Thiếu |
| 2 | `FootstepSwamp` | — | Thiếu | — | — | Giữ bíp tạm; nguồn ở Q3 |
| 3 | `BushEnter` | — | Thiếu | — | — | Giữ bíp tạm; nguồn ở Q3 |
| 4 ★ | `MeleeSwing` | `melee chém.mp3` | **Có** | 0,37 s | mp3, 44,1 kHz, stereo, 183 kbps · 8 932 B | Đổi mono, chuẩn hóa mức; vẫn cần nghe |
| 5 ★ | `MeleeHit` | `Đập.wav` (thêm: `Pick_Impacts_ODY-0409-030.wav` trong zip, 58 s) | Cần chỉnh | 0,32 s | wav 16-bit, 44,1 kHz, stereo · 56 464 B · đỉnh −7,2 / TB −27,4 | Nâng mức (đỉnh −7,2 → −2,0 dBFS), mono. Có thể cắt 2–3 biến thể từ file `pick impacts` (mono, 58 s, đỉnh 0,0 dBFS: chạm trần) |
| 6 ★ | `ShootRanged` | `súng bắn.mp3` | **Có** | 1,08 s | mp3, **24 kHz**, stereo, 85 kbps · 12 077 B | Mono, chuẩn hóa mức. Băng thông thấp (24 kHz); một clip cho cả Ná cao su, Quyền trượng và Shotgun: biến thể riêng từng vũ khí là mở rộng (Q8) |
| 7 ★ | `ShootCrossbow` | — (`súng bắn.mp3` hạ cao độ bị AUD-01 loại) | Thiếu | — | — | Cần clip riêng, khác `ShootRanged`; nguồn ở Q3 |
| 8 | `Throw` | — | Thiếu | — | — | Giữ bíp tạm |
| 9 | `Reload` | — | Thiếu | — | — | Giữ bíp tạm |
| 10 | `EmptyClick` | — (`button.mp3` để dành cho UI) | Thiếu | — | — | Giữ bíp tạm |
| 11 ◆ | `BombFuse` | `countdown.mp3` | Cần chỉnh | **11,78 s** | mp3, **24 kHz**, stereo, 111 kbps · 163 806 B | Cắt còn 0,8–1,5 s (ngòi bom 1,0–1,5 s), fade cuối, mono |
| 12 ◆ | `Explosion` | `bomb nổ.mp3` | Cần chỉnh | **5,41 s** | mp3, 44,1 kHz, stereo, 256 kbps · 173 035 B | Cắt đuôi còn ≤ 3,0 s, fade cuối, mono |
| 13 | `TrapTriggered` | — (`Đập.wav` hạ cao độ bị AUD-01 loại) | Thiếu | — | — | Giữ bíp tạm |
| 14 ★ | `HitMarker` | — (`button.mp3`, 0,16 s, để dành cho UI) | Thiếu | — | — | **Ưu tiên cao nhất**: clip gọn, sắc, ≤ 0,15 s; nguồn ở Q3 |
| 15 ★ | `HurtLocal` | `bleed.mp3` | Cần chỉnh | 1,07 s | mp3, 44,1 kHz, stereo, 174 kbps · 23 851 B | Tên "bleed" (chảy máu) có thể là âm trạng thái, không phải tiếng trúng đòn: nghe; cắt ≤ 0,60 s |
| 16 | `ArmorAbsorb` | — (`Pick_Impacts_…wav` là ứng viên chưa nghe) | Thiếu | — | — | Giữ bíp tạm; có thể cắt từ file `pick impacts` |
| 17 | `Eliminated` | — (`revive.mp3` nghĩa ngược, loại) | Thiếu | — | — | Giữ bíp tạm |
| 18 ★ | `PickupWeapon` | `nhặt súng.mp3` | **Có** | 0,60 s | mp3, 44,1 kHz, stereo, 152 kbps · 12 055 B | Mono, chuẩn hóa mức |
| 19 ★ | `PickupItem` | `Nhặt đồ/Gun Put On.mp3` | Cần chỉnh | **2,06 s** | mp3, 44,1 kHz, stereo, 320 kbps · 82 616 B | Cắt ≤ 0,80 s. Tên là "đặt súng": nghe xem có hợp "nhặt đồ chung" |
| 20 ★ | `WearArmor` | `bot di chuyển.zip` → 4 × `Leather Armor Put On 01–04` (wav 1,14–1,73 s, quá nhỏ: đỉnh −10 … −15 dBFS); hoặc `Nhặt đồ/Helmet Put On.mp3` (2,06 s) | Cần chỉnh | 1,14–1,73 s | wav 16-bit, 44,1 kHz, stereo · 201 160–304 948 B (zip 519 065 B) | Giải nén (đúng nội dung "mặc giáp da", tên zip sai); chọn 2–4 biến thể, cắt ≤ 1,20 s, nâng mức ~ +10 dB, mono. Dùng được làm biến thể (catalog nhận nhiều clip cho một mã) |
| 21 | `UseHeal` | `gauze.mp3` | Cần chỉnh | 3,06 s | mp3, 44,1 kHz, **mono**, 137 kbps · 52 174 B | Cắt ≤ 1,50 s (đồ hồi máu dùng 0,5–3,5 s; 3 trong 5 món dùng ≤ 1,2 s), fade cuối |
| 22 | `UseDrink` | `cola.mp3` | Cần chỉnh | 3,24 s | mp3, 44,1 kHz, **mono**, 101 kbps · 40 852 B | Cắt ≤ 1,50 s (món hỗ trợ dùng 1,0–2,0 s), fade cuối |
| 23 | `UseCancel` | — | Thiếu | — | — | Giữ bíp tạm |
| 24 ★ | `ZoneBell3` | — (`Bo co.mp3` 15,05 s là âm bo co/thu hẹp, **không** phải chuông cảnh báo) | Thiếu | — | — | Cần một tiếng chuông 0,40–0,75 s (catalog lặp 3 lần cách 0,8 s); nguồn ở Q3 |
| 25 ★ | `DragonRoar` | — | Thiếu | — | — | Cần tiếng gầm 1,5–4,0 s, **không** trùng clip nào khác; nguồn ở Q3 |
| 26 | `MatchEnd` | `TimeOver.wav` | **Có** | 2,82 s | wav 16-bit, 44,1 kHz, stereo · 719 398 B · đỉnh −1,1 / TB −17,2 | Mono (hoặc giữ stereo, Q2), chuẩn hóa mức. **Giấy phép rủi ro cao** (thẻ hollywoodedge.com, mục 2.1 và R1) |

**Tổng kết (27/10/2026):** **Có 4** (4, 6, 18, 26) · **Cần chỉnh 9** (1, 5, 11, 12, 15, 19, 20, 21, 22) · **Thiếu 13** (2, 3, 7, 8, 9, 10, 13, 14, 16, 17, 23, 24, 25). Cộng 26. Trong 11 mã ưu tiên ★ (4, 5, 6, 7, 14, 15, 18, 19, 20, 24, 25): Có 3, Cần chỉnh 4, **Thiếu 4** (7, 14, 24, 25).

### 2.3. Đích cho từng mã (độ dài, tầng mức, clip hiện tại)

**Độ dài đích** (giả định G-8) dựa trên cooldown/số bản tối đa của catalog, thời gian dùng đồ, ngòi bom, và mục "Tối đa/Cooldown" của AUD-01: clip dài hơn khoảng đích làm giữ voice quá lâu (pool chỉ 16). **Tầng mức** là mức hiệu dụng đích E (mục 2.6, giả định G-6): **T1** −16, **T2** −20, **T3** −24, **T4** −29 dBFS (± 3 dB). **Clip hiện tại** = clip AUD-01 đang trỏ tới và `volume` trong catalog.

| # | SfxId | Độ dài đích (s) | Tầng | Clip hiện tại (volume) |
| --- | --- | --- | --- | --- |
| 1 | `FootstepGrass` | 0,05–0,40 | T4 | bíp 330 Hz (0,40) |
| 2 | `FootstepSwamp` | 0,08–0,50 | T4 | bíp 392 Hz (0,45) |
| 3 | `BushEnter` | 0,10–0,60 | T4 | bíp 467 Hz (0,50) |
| 4 | `MeleeSwing` | 0,15–0,60 | T2 | `melee_slash.mp3` (0,70) |
| 5 | `MeleeHit` | 0,10–0,50 | T2 | `hit_thud.wav` (0,80) |
| 6 | `ShootRanged` | 0,15–1,20 | T2 | `gun_shot.mp3` (0,75) |
| 7 | `ShootCrossbow` | 0,15–1,00 | T2 | bíp 555 Hz (0,75) |
| 8 | `Throw` | 0,10–0,60 | T2 | bíp 660 Hz (0,60) |
| 9 | `Reload` | 0,20–1,50 | T3 | bíp 785 Hz (0,50) |
| 10 | `EmptyClick` | 0,03–0,30 | T3 | bíp 933 Hz (0,50) |
| 11 | `BombFuse` | 0,80–1,50 | T2 | `bomb_fuse_countdown.mp3` 11,8 s (0,70) |
| 12 | `Explosion` | 0,50–3,00 | T2 | `bomb_explosion.mp3` 5,4 s (0,90) |
| 13 | `TrapTriggered` | 0,10–0,80 | T2 | bíp 1110 Hz (0,70) |
| 14 | `HitMarker` | 0,03–0,15 | **T1** | bíp 2220 Hz (1,00) |
| 15 | `HurtLocal` | 0,20–0,60 | **T1** | `hurt_bleed.mp3` 1,07 s (0,90) |
| 16 | `ArmorAbsorb` | 0,10–0,40 | T2 | bíp 1320 Hz (0,80) |
| 17 | `Eliminated` | 0,30–1,50 | T2 | bíp 1570 Hz (0,90) |
| 18 | `PickupWeapon` | 0,15–0,80 | T3 | `pickup_weapon.mp3` (0,80) |
| 19 | `PickupItem` | 0,15–0,80 | T3 | `pickup_item_gun_put_on.mp3` 2,06 s (0,70) |
| 20 | `WearArmor` | 0,30–1,20 | T3 | `wear_armor_helmet_put_on.mp3` 2,06 s (0,70) |
| 21 | `UseHeal` | 0,50–1,50 | T3 | `use_heal_gauze.mp3` 3,06 s (0,70) |
| 22 | `UseDrink` | 0,50–1,50 | T3 | `use_drink_cola.mp3` 3,24 s (0,70) |
| 23 | `UseCancel` | 0,10–0,40 | T3 | bíp 1867 Hz (0,60) |
| 24 | `ZoneBell3` | 0,40–0,75 (một tiếng, lặp 3) hoặc 2,0–2,6 (một clip chứa 3 tiếng, lặp 1) | **T1** | bíp chuông 0,70 s (1,00), bus Alert, lặp 3 cách 0,8 s |
| 25 | `DragonRoar` | 1,50–4,00 | **T1** | gầm tổng hợp 2,0 s (1,00), bus Alert, cooldown 5 s |
| 26 | `MatchEnd` | 1,00–3,00 | **T1** | `match_end_time_over.wav` 2,82 s (1,00) |

### 2.4. Quyết định: clip cuối nằm ở đâu

**Khuyến nghị (Q1): sao chép/chuyển đổi sang `Assets/_JungleMaze/Art/Audio/Final/`, không tham chiếu `Assets/Imported`.** Lý do:

1. **Cài đặt import nằm trong `.meta`.** Đổi nén, mono, tải-khi-nạp của file trong `Assets/Imported/**` là sửa vùng nhập (cấm). Bản chép có `.meta` riêng.
2. **Phải cắt và chuẩn hóa**: mọi clip "Cần chỉnh" cần bản dẫn xuất (cắt, mono, mức), nên không thể dùng file gốc nguyên bản.
3. **Giấy phép chưa rõ**: nếu một file JSurviv phải bỏ, chỉ cần xóa bản chép và dòng manifest; thay bằng file khác không chạm vùng nhập.
4. **Thư mục JSurviv là vùng tham khảo** (`JSurviv_Integration.vi.md`), có thể bị dọn sau này.
5. Phù hợp cách AUD-01 đã làm với `Placeholder/` (chép, đổi tên ASCII, **không chép `.meta`** để Unity cấp GUID mới).

Cái giá: lặp byte (khoảng 1–2 MB SFX sau khi cắt; nhạc đã nén nhỏ hơn bản gốc). Chấp nhận được. **Không** chép `Waiting Room Music.wav` (25,7 MB) vào `_JungleMaze`; chỉ chép bản đã nén.

Bố cục đề xuất (G-3):

```
Assets/_JungleMaze/Art/Audio/
  Final/Sfx/<sfx_snake>_<NN>.wav        # vd. melee_swing_01.wav, wear_armor_02.wav (1–4 biến thể mỗi mã)
  Final/Music/<track>.<ogg|mp3>         # waiting_room, loading, match
  Placeholder/…                         # của AUD-01; bíp vẫn là clip của mã Thiếu
Docs/Tickets/ART-05_audio_manifest.csv  # một dòng cho mỗi clip, xem 2.5
Docs/Licenses/Audio/                    # bằng chứng giấy phép, nếu có
```

**Phương án còn lại (không khuyến nghị): tham chiếu thẳng `Assets/Imported/JSurviv/Sound`.** Nhanh hơn (không chép), nhưng không cắt/chuẩn hóa được, không đổi được import, và buộc game phụ thuộc vùng nhập.

### 2.5. Cách một clip thay một clip tạm, không đổi mã

1. **Chọn nguồn** → thêm dòng manifest (nguồn gốc, giấy phép "chưa kiểm chứng" hoặc bằng chứng).
2. **Xử lý ngoài Unity** từ file gốc (giữ nguyên): giải nén nếu cần, cắt đầu/cuối, fade 2–5 ms hai đầu (không click), đổi mono, chuẩn hóa đỉnh về −2,0 dBFS, xuất WAV 16-bit 44,1 kHz. Ghi vào manifest: đoạn cắt (giây), độ lợi (dB), công cụ.
3. **Đặt file** vào `Final/Sfx/` với tên ASCII chữ thường. Unity sinh `.meta` mới; cài đặt import theo 2.7.
4. **Sửa dữ liệu** trong `SfxCatalog.asset`: thêm/thay clip trong `clips` của **đúng** `SfxId` (catalog nhận nhiều clip cho một mã, `ClipPicker` chọn không lặp liền nhau), chỉnh `volume` (và `pitchJitter` nếu cần) cho đúng tầng mức. Không sửa cột khác. Nhạc: sửa `waitingRoomMusic`, `loadingMusic`, `matchMusic` trong `AudioRuntimeSet.asset`.
5. **Kiểm**: chạy bộ test Audio của AUD-01 (phải xanh nguyên), phát bằng panel F6 (từng mã, "Dồn 10 s", "Gửi lặp ×5"), đo mức và độ dài (AC).
6. **Commit** kèm đường dẫn: clip + `.meta` + `SfxCatalog.asset`/`AudioRuntimeSet.asset` + manifest cùng một commit cho mỗi nhóm (vd. nhóm ưu tiên trước). Không có `.cs` nào trong diff (AC11).

Đổi lại dễ: mỗi lần thay chỉ là đổi tham chiếu clip trong asset; bản bíp/`Placeholder` còn nguyên cho tới khi bước dọn dẹp (Q7).

### 2.6. Chuẩn hóa âm lượng: mục tiêu và cách đo

**Nguyên tắc:** chuẩn hóa **trong file** (cùng một đỉnh cho mọi clip), thứ tự to-nhỏ giữa các sự kiện đặt bằng `volume` trong catalog (dữ liệu). Không dùng LUFS theo ITU-R BS.1770 vì nhiều clip ngắn hơn khối 400 ms (hitmarker 0,06 s, click 0,06 s) nên không đo được.

**Định nghĩa đo** (G-7). Tất cả tính trên tín hiệu **đã trộn mono**, mẫu −1…1:

- **Đỉnh** = max |x| của clip, dBFS.
- **Mức hoạt động L** = RMS (dBFS) tính trên đoạn từ mẫu đầu tiên đến mẫu cuối cùng có |x| ≥ 0,003 (−50 dBFS), tức bỏ im lặng hai đầu.
- **Mức hiệu dụng E** = L + 20·log10(`volume` trong catalog). E là con số nghe thấy thật; `volume` ≤ 1 nên E ≤ L.

**Quy tắc cho mọi clip SFX cuối (G-5, G-6):**

| Quy tắc | Giá trị |
| --- | --- |
| Đỉnh | từ **−3,0 đến −1,0 dBFS** (đích −2,0); không có chuỗi ≥ 3 mẫu liên tiếp chạm trần |
| Chống click | |x| trong 2 ms đầu và 2 ms cuối ≤ −40 dBFS (có fade) |
| Mức hiệu dụng E | trong tầng của mã ± 3 dB (bảng 2.3): **T1 −16** (HitMarker, HurtLocal, ZoneBell3, DragonRoar, MatchEnd) · **T2 −20** (bắn, chém, trúng cận chiến, bom, bẫy, giáp đỡ, bị loại, ném) · **T3 −24** (nhặt, mặc, dùng đồ, nạp, hết đạn, hủy) · **T4 −29** (bước chân, vào bụi) |
| Quan hệ ưu tiên | E của `HitMarker` ≥ E của **mọi mã thuộc T2–T4** + 3 dB; E của `HitMarker` − E của `FootstepGrass` ≥ 12 dB |
| BGM | đỉnh ≤ −1,0 dBFS; mức hoạt động (cả bài) **−26 ± 2 dBFS** ở `MusicVolume` = 1 (nên thấp hơn T1 ≥ 8 dB, trước khi AUD-01 hạ thêm −9 dB lúc có cảnh báo) |

Lý do T1 > T2 > T3 > T4: người chơi cần nghe được hitmarker, tiếng bị đánh, chuông bo và rồng **trong lúc** súng nổ và bước chân dày; bước chân rất thường xuyên nên ở tầng thấp nhất (AUD-01 A19: HitMarker ≥ 2 × FootstepGrass, vẫn thỏa: 1,0 so với 0,4).

**Công cụ đo** (hai đường, kết quả lệch ≤ 0,5 dB):

1. **Trong Unity**: test EditMode đọc `AudioClip.GetData` của từng clip trong catalog (clip SFX nạp kiểu Decompress On Load). Cách này đo đúng dữ liệu **sau import**, nên bắt được hiệu ứng của cờ `normalize`/`forceToMono` (R3).
2. **Ngoài Unity**: `Tools/Audio/measure_clips.py` (thư viện chuẩn, đọc WAV bằng `wave`) cho các file WAV nguồn của `Final/Sfx`. File MP3/OGG (nhạc) không đọc được bằng thư viện chuẩn: đo trên bản WAV trước khi nén và đo lại một lần sau nén bằng đường 1 (R4).

Chưa có số đo mức cho các file MP3 (14 file) vì máy này không có bộ giải mã (`ffmpeg`/`sox` không có). Các số mức ở bảng 2.2 chỉ cho WAV.

### 2.7. Cài đặt import cho clip cuối

| Loại | Cài đặt (G-10) |
| --- | --- |
| SFX (`Final/Sfx/*.wav`) | WAV 16-bit **mono đã bake**, 44,1 kHz; Load Type **Decompress On Load**; nén **ADPCM** (hoặc PCM với clip ≤ 0,3 s); `forceToMono` **tắt**, `normalize` **tắt**; `preloadAudioData` bật; `loadInBackground` tắt; `3D` giữ mặc định (AUD-01 để `spatialBlend` = 0) |
| Nhạc (`Final/Music/*`) | giữ stereo; Load Type **Streaming**; Vorbis chất lượng 0,7; `loadInBackground` bật; `normalize` tắt; tệp nguồn ≤ 3,0 MB |

Lý do tắt `normalize`/`forceToMono` ở SFX: theo tài liệu Unity, khi ép mono mà bật `normalize` thì bản trộn xuống mono bị chuẩn hóa tự động. `.meta` của clip tạm AUD-01 đang có đúng tổ hợp này (`forceToMono: 1`, `normalize: 1`), nên mọi clip qua import có thể bị đưa về mức đỉnh xấp xỉ 0 dBFS bất kể mức trong file. Phải **kiểm chứng bằng đo** (AC16, R3), không giả định.

### 2.8. Nhạc nền (BGM)

GDD chưa chốt phạm vi (Assets STT 14: "Chưa chốt phạm vi"; dòng ticket: "việc BGM có thuộc MVP hay không vẫn theo GDD chưa chốt"; "chỉ tích hợp khi được duyệt"). **Đề xuất mặc định (Q4): giữ BGM tắt, vẫn chuẩn bị file.** Nghĩa là `MusicEnabled` mặc định `false` như AUD-01 (G-4), nhưng ba bài đã nén và ánh xạ đúng, nên khi PIC bật thì không cần việc thêm.

| Bài | File gốc | Độ dài | Kích thước gốc | Trạng thái | Việc |
| --- | --- | --- | --- | --- | --- |
| Waiting Room | `Waiting Room Music.wav` | 145,53 s | **25 722 642 B** (wav 16-bit, 44,1 kHz, stereo; đỉnh 0,0 / TB −14,4: quá to) | Cần chỉnh | Nén còn ≤ 3,0 MB (128 kbps ≈ 2,3 MB), giảm mức theo 2.6. Thay tham chiếu tạm `loading_music` (T-5) |
| Loading | `Loading Music.mp3` | 43,65 s | 1 396 819 B (mp3, 44,1 kHz, **mono**, 256 kbps) | Có | Chuẩn hóa mức; chưa nghe |
| Match | `ParkingMapMusic.mp3` | 123,48 s | 4 939 324 B (mp3, 44,1 kHz, stereo, 320 kbps) | Cần chỉnh | Nén còn ≤ 3,0 MB (160 kbps ≈ 2,5 MB), chuẩn hóa mức |

Ánh xạ màn hình → bài theo AUD-01 (G-9): `MainMenu`, `InRoom`, `Results` → Waiting Room; `Connecting` → Loading; `InMatch` → Match; `Boot` → im lặng. Trận dài 420 s, bài Match 123 s lặp 3–4 lần: **điểm lặp chưa nghe** (AC34). Âm `Bo co.mp3` (15,05 s) không phải nhạc nền và không thuộc 26 mã: ghi ở Q8.

### 2.9. Giấy phép và nguồn

**Hiện trạng: chưa có bằng chứng bằng văn bản cho bất kỳ file nào.** Các dữ kiện đã tìm (2.1) cho thấy ít nhất `TimeOver.wav` có nguồn gốc thư viện thương mại, nhưng thẻ này không chứng tỏ quyền dùng. Quy tắc cho ART-05:

- Mỗi clip trong manifest có cột **giấy phép** nhận đúng một trong ba giá trị: `chưa kiểm chứng` · `tự tạo (Tools/Audio/<script>)` · `có bằng chứng: <đường dẫn file trong repo>`.
- **Không** ghi "miễn phí", "CC0", "royalty-free", "được phép" hay tương tự nếu không có tệp bằng chứng đi kèm trong `Docs/Licenses/Audio/` (giấy phép, biên lai, email).
- Mọi file JSurviv, và mọi clip dẫn xuất từ chúng, mặc định `chưa kiểm chứng`. Bíp/tiếng tổng hợp do script của repo sinh ra ghi `tự tạo`.
- ART-05 **không chặn** build bởi giấy phép (dự án đang là bản mẫu LAN lớp học), nhưng xuất một **danh sách clip chưa kiểm chứng** để CPO/người dùng quyết định trước khi phát hành (Q5).

### 2.10. Mâu thuẫn và khoảng trống GDD/code đã xử lý

| # | Mâu thuẫn / khoảng trống | Xử lý |
| --- | --- | --- |
| M1 | Dòng ticket: "đã thấy các file như melee chém.mp3, súng bắn.mp3, bomb nổ.mp3, Bo co.mp3 …". Đề xuất AUD-01 ghi "khoảng 20 file dùng tạm được". **Thực tế chỉ 4 trong 26 mã có file dùng được ngay** (và chưa nghe); 13 mã không có file nào | Dùng bảng 2.2 làm sự thật. Số "khoảng 20" đếm file chứ không đếm sự kiện |
| M2 | Dòng ticket: "Đưa audio clip vào AudioManager/AudioMixer của AUD-01 … tự làm import, mapping, volume và playback" ↔ dòng AUD-01: "owner duy nhất của audio runtime, mixer và playback; ART-05 … không sửa AudioManager" | Hiểu: ART-05 làm import, mapping, volume ở dạng **dữ liệu** (catalog, runtime set, `.meta`); mã playback thuộc AUD-01. Ghi ở phạm vi (không sửa `.cs`) |
| M3 | Dòng ticket: "Chuẩn hóa tên/mapping EventId → AudioClip"; `SfxId` của AUD-01 đã cố định 26 mã | EventId = `SfxId`. Thêm mã mới (stun, cửa đá, hồi sinh …) cần sửa enum: ngoài phạm vi (Q8) |
| M4 | Phụ thuộc ghi "None" nhưng việc thật cần `SfxCatalog`/`AudioRuntimeSet`/F6 của AUD-01 | AUD-01 đã Done: không chặn. Ghi ở header |
| M5 | Tên file không khớp nội dung: `bot di chuyển.zip` (tên "bot di chuyển") thực chứa 8 tệp `Leather Armor Put On` (mặc giáp da); `bot di chuyển.mp3` chưa rõ có phải bước chân | Dùng nội dung thật làm chuẩn; ghi tên sai vào manifest |
| M6 | Assets STT 9–13 ghi "Chưa chọn nguồn"/"Chờ roster"; cột "Link / License" trống ↔ file đã nằm trong repo từ 12/10 | Coi file JSurviv là tạm; giấy phép `chưa kiểm chứng` (mục 2.9) |
| M7 | Hai clip dài bất thường: `countdown.mp3` 11,78 s cho ngòi 1,0–1,5 s; `gauze.mp3`/`cola.mp3` 3,0–3,2 s cho món dùng 0,5–1,5 s | Cắt theo độ dài đích (bảng 2.3) |
| M8 | `súng bắn.mp3` và `countdown.mp3` là 24 kHz (băng thông ≤ 12 kHz), phần còn lại 44,1 kHz | Không nội suy lên 44,1 kHz để "sửa" chất lượng; ghi nhận, mono và chuẩn hóa mức |
| M9 | AUD-01 chép `Loading Music.mp3` làm cả nhạc Waiting Room (T-5, vì không chép 25,7 MB) | ART-05 nén bản Waiting Room thật và thay tham chiếu (2.8) |

### 2.11. Quyết định từng mã: bản chép JSurviv hay bíp (bổ sung theo CPO 27/10)

Hiện trạng catalog (đối chiếu `SfxCatalog.asset` và `Tools/Audio/copy_jsurviv_placeholders.py`): **12 mã dùng bản chép JSurviv** (4, 5, 6, 11, 12, 15, 18, 19, 20, 21, 22, 26) và **14 mã dùng bíp tự sinh** (1, 2, 3, 7, 8, 9, 10, 13, 14, 16, 17, 23, 24, 25). Cả 12 bản chép đã kiểm **giống từng byte** với file gốc (md5 trùng, đo 27/10). Không chép `.meta` nên GUID khác bản gốc.

**(a) 12 mã đang dùng clip JSurviv:** quyết định là *giữ / cắt-chuẩn hóa / thay*.

| # | SfxId | Quyết định | Việc |
| --- | --- | --- | --- |
| 4 | `MeleeSwing` | **Giữ + chuẩn hóa** | Mono, đỉnh −2 dBFS, volume theo T2 |
| 5 | `MeleeHit` | **Chuẩn hóa** (đỉnh −7,2 → −2 dBFS) | Có thể thêm 1–3 biến thể cắt từ `Pick_Impacts_…wav` sau khi nghe |
| 6 | `ShootRanged` | **Giữ + chuẩn hóa** | Băng thông thấp (24 kHz); chỉ thay nếu PIC nghe thấy dở và có nguồn tốt hơn |
| 11 | `BombFuse` | **Cắt** 11,78 → 0,8–1,5 s | Fade cuối |
| 12 | `Explosion` | **Cắt** 5,41 → ≤ 3,0 s | Fade cuối |
| 15 | `HurtLocal` | **Cắt ≤ 0,6 s sau khi nghe; thay nếu đó là tiếng trạng thái chảy máu** | Nếu phải thay: không còn ứng viên JSurviv nào, thành khoảng trống thật (2.11 b) |
| 18 | `PickupWeapon` | **Giữ + chuẩn hóa** | |
| 19 | `PickupItem` | **Cắt** 2,06 → ≤ 0,8 s; thay nếu nghe không hợp "nhặt đồ chung" | |
| 20 | `WearArmor` | **Thay** bằng 2–4 biến thể `Leather Armor Put On` (trong `bot di chuyển.zip`), cắt ≤ 1,2 s, nâng ~ +10 dB | `Helmet Put On.mp3` có thể giữ làm biến thể thứ năm; bản chép hiện tại dài 2,06 s |
| 21 | `UseHeal` | **Cắt** 3,06 → ≤ 1,5 s | |
| 22 | `UseDrink` | **Cắt** 3,24 → ≤ 1,5 s | |
| 26 | `MatchEnd` | **Giữ + chuẩn hóa**; **thay trước phát hành** | Thẻ hollywoodedge.com (2.12); chưa có clip thay thế nào được xác định |

**(b) 14 mã còn là bíp:** với từng mã, có file JSurviv **chưa dùng** nào khớp không? File JSurviv chưa được catalog dùng: `button.mp3`, `Bo co.mp3`, `revive.mp3`, `bot di chuyển.mp3`, `bot di chuyển.zip` (8 tệp Leather Armor), `pick-impacts-….zip` (1 tệp 58 s). `countdown.mp3`, `Đập.wav` và hai file `Nhặt đồ/*` **đã được dùng** (bản chép của 11, 5, 19, 20), không phải file chưa dùng.

| # | SfxId | File JSurviv chưa dùng có khớp? | Kết luận | Cách lấp (không bịa nguồn) |
| --- | --- | --- | --- | --- |
| 1 | `FootstepGrass` | `bot di chuyển.mp3` (0,57 s): chỉ khớp **theo tên**, chưa nghe | **Thử ứng viên** | Nghe; nếu đúng thì cắt ≤ 0,40 s. Không đúng: giữ bíp |
| 2 | `FootstepSwamp` | Không | **Khoảng trống thật** | Giữ bíp |
| 3 | `BushEnter` | Không | **Khoảng trống thật** | Giữ bíp |
| 7 ★ | `ShootCrossbow` | Không (`súng bắn.mp3` hạ cao độ đã bị loại, trùng `ShootRanged` vi phạm AC13) | **Khoảng trống thật** | Tự tổng hợp bằng script (Q3) hoặc giữ bíp |
| 8 | `Throw` | Không | **Khoảng trống thật** | Giữ bíp |
| 9 | `Reload` | Không rõ: `Gun Put On.mp3` có thể là tiếng thao tác súng nhưng đã dùng cho `PickupItem` | **Khoảng trống thật** | Giữ bíp |
| 10 | `EmptyClick` | `button.mp3` (0,16 s) là tiếng nút UI, để dành cho UI-02/UI-03; dùng chung sẽ làm bấm nút và hết đạn trùng tiếng | **Khoảng trống thật** | Thử ứng viên ngoài (bên dưới) hoặc giữ bíp |
| 13 | `TrapTriggered` | `Pick_Impacts_…wav` (58 s, mono, đỉnh 0,0 dBFS): chỉ có thể cắt đoạn, **chưa nghe**. `Đập.wav` đã dùng cho `MeleeHit` | **Thử ứng viên** | Nghe; cắt một tiếng khác hẳn các biến thể `MeleeHit`. Không hợp: giữ bíp |
| 14 ★ | `HitMarker` | `button.mp3` bị AUD-01 loại (UI) | **Khoảng trống thật, ưu tiên cao nhất** | Thử ứng viên ngoài (bên dưới); nếu không hợp: tự tổng hợp lại tiếng gọn sắc ≤ 0,15 s bằng script |
| 16 | `ArmorAbsorb` | `Pick_Impacts_…wav`: chỉ có thể cắt đoạn, **chưa nghe** | **Thử ứng viên** | Như mã 13; không hợp: giữ bíp |
| 17 | `Eliminated` | `revive.mp3` (1,57 s): nghĩa ngược (hồi sinh), loại | **Khoảng trống thật** | Giữ bíp |
| 23 | `UseCancel` | Không | **Khoảng trống thật** | Thử ứng viên ngoài hoặc giữ bíp |
| 24 ★ | `ZoneBell3` | `Bo co.mp3` (15,05 s): là âm bo co/thu hẹp, **không** phải chuông; cắt một đoạn ngắn từ nội dung chưa nghe là đoán | **Khoảng trống thật, ưu tiên** | Tự tổng hợp chuông tốt hơn bằng script (Q3); `Bo co.mp3` ghi cho ZON-01 (Q8) |
| 25 ★ | `DragonRoar` | Không | **Khoảng trống thật, ưu tiên** | Không xác định được nguồn tự do. Giữ tiếng gầm tổng hợp hiện có (có thể làm tốt hơn bằng script) tới khi PIC cung cấp nguồn |

**Tổng:** trong 14 bíp, **3 mã thử ứng viên JSurviv** (1, 13, 16) và **11 mã là khoảng trống thật** (2, 3, 7, 8, 9, 10, 14, 17, 23, 24, 25), trong đó 4 mã ưu tiên (7, 14, 24, 25). Khớp với bảng 2.2: 13 mã "Thiếu" = 11 khoảng trống thật + 2 mã thử ứng viên (13, 16); mã 1 nằm ở "Cần chỉnh" vì có ứng viên theo tên.

**Cách lấp khoảng trống, theo thứ tự ưu tiên, mỗi bước phải ghi nguồn:**

1. **Tự tạo bằng script của repo** (`Tools/Audio/*`): giấy phép sạch (`tự tạo`), lặp lại được. Phù hợp `ZoneBell3`, `HitMarker`, `ShootCrossbow`, `DragonRoar`; chất lượng thô hơn bản thu thật.
2. **Nguồn tự do có giấy phép ghi trên trang nguồn.** Đã kiểm trang của Kenney ngày 27/10/2026: **Impact Sounds** (130 file, thẻ "impact", "foley") và **Interface Sounds** (100 file, thẻ "interface", "click", "button") ghi giấy phép **Creative Commons CC0**, và trang hỗ trợ ghi tài sản "public domain licensed (CC0)", dùng được trong dự án thương mại, không bắt buộc ghi công. **Chưa tải, chưa nghe, chưa biết tên file bên trong** nên chưa biết có clip nào khớp (không khẳng định mã nào lấp được). Ứng viên: Interface Sounds cho `HitMarker`, `EmptyClick`, `UseCancel`; Impact Sounds cho `TrapTriggered`, `ArmorAbsorb`, biến thể `MeleeHit`, có thể bước chân. **Tải file cần người dùng đồng ý** (Q3). Khi dùng: lưu bản chụp trang giấy phép và tệp license đi kèm gói vào `Docs/Licenses/Audio/`, ghi URL, tên file gốc, ngày tải vào manifest, giấy phép `có bằng chứng: <đường dẫn>`.
3. **Giữ bíp tạm** (giấy phép `tự tạo`), chấp nhận khoảng trống. Với mã không ưu tiên đây là mặc định.

Không có nguồn nào ở trên được coi là "đã có" cho tới khi tải về, nghe và ghi bằng chứng.

### 2.12. Giấy phép của 12 clip đang dùng (và 2 bài nhạc)

**Kết luận: cả 12 clip SFX và 2 bài nhạc AUD-01 chép đều `chưa kiểm chứng`.** Bản chép giống từng byte file gốc nên mang nguyên tình trạng của file gốc. Bằng chứng văn bản về quyền sử dụng: **không có** (không license/readme/credit trong `Sound`, hai tài liệu JSurviv không nhắc, cột "Link / License" của sheet Assets trống). Dữ kiện từ metadata, **không phải bằng chứng quyền dùng**:

| Clip chép (SfxId) | File gốc | Dấu vết trong file |
| --- | --- | --- |
| `match_end_time_over.wav` (26) | `TimeOver.wav` | Thẻ `bext`: "Clock, Alarm Loop, Wind Up, Ringing", **www.hollywoodedge.com**: dấu hiệu thư viện thương mại. **Rủi ro cao nhất** |
| `hurt_bleed.mp3` (15), `melee_slash.mp3` (4), `gun_shot.mp3` (6), `pickup_weapon.mp3` (18) | `bleed`, `melee chém`, `súng bắn`, `nhặt súng` | ID3 năm 2022; `bleed`, `súng bắn`, `nhặt súng` ghi "Pro Audio Converter 1.8.1" (công cụ chuyển đổi, không nói về tác giả) |
| `bomb_fuse_countdown.mp3` (11) | `countdown.mp3` | ID3 năm 2022 |
| `pickup_item_gun_put_on.mp3` (19), `wear_armor_helmet_put_on.mp3` (20) | `Nhặt đồ/Gun Put On`, `Helmet Put On` | Năm 2020, "LAME in FL Studio 10", BPM 80: xuất từ phần mềm sản xuất nhạc |
| `parking_map_music.mp3` | `ParkingMapMusic.mp3` | Năm 2023, "Studio One 6.2.1" |
| `hit_thud.wav` (5), `bomb_explosion.mp3` (12), `use_heal_gauze.mp3` (21), `use_drink_cola.mp3` (22), `loading_music.mp3` | `Đập.wav`, `bomb nổ.mp3`, `gauze.mp3`, `cola.mp3`, `Loading Music.mp3` | Không có thẻ nào |

Hệ quả: (1) manifest ghi `chưa kiểm chứng` cho cả 14 dòng; (2) danh sách ở AC7 gồm 14 clip này và mọi clip dẫn xuất; (3) `TimeOver.wav` nên được thay trước phát hành công khai (Q5), chưa có clip thay thế; (4) bản chép trong `Placeholder/JSurviv/` và bản dẫn xuất trong `Final/` đều mang cùng trạng thái cho tới khi có bằng chứng.

### 2.13. Thư mục `Placeholder` và việc đưa lên `Final`: có cần đổi catalog không

Đã kiểm code và dữ liệu (27/10):

- **Catalog tham chiếu clip bằng GUID** (`clips: {fileID: 8300000, guid: …}`), không bằng đường dẫn. Tên thư mục `Placeholder` **không** xuất hiện trong `SfxCatalog.asset` hay `AudioRuntimeSet.asset`. Không có mã runtime nào (`Scripts/**`) dùng chuỗi `Placeholder` cho âm thanh.
- **Hai nơi phụ thuộc đường dẫn:** `Tests/EditMode/Audio/SfxCatalogTests.cs:64` lọc bíp theo `"/Placeholder/Beeps/"` (test `BeepsHaveDistinctFrequencies`), và hai script `Tools/Audio/copy_jsurviv_placeholders.py` (`TARGET`) và `make_placeholder_sfx.py` (`DEFAULT_OUT`) ghi/kiểm ở `Placeholder/…`.

Kết luận và quyết định (Q7):

1. **Không đổi tên thư mục `Placeholder`, không "chuyển" file.** Clip cuối là **file dẫn xuất mới** (mono, cắt, chuẩn hóa) trong `Final/Sfx/`, nên không có phép "thăng cấp" nguyên file. Đổi tên/di chuyển trong Unity giữ GUID nên catalog không đổi, nhưng sẽ làm test ở dòng 64 và hai script sai đường dẫn.
2. **Catalog có thay đổi, nhưng chỉ là dữ liệu cho phép:** `clips` của mã đó trỏ sang GUID file mới trong `Final/` (đúng thao tác 2.5, bước 4). Không thêm cột, không đổi mã.
3. **Bíp còn lại** (mã khoảng trống thật vẫn dùng bíp) **giữ nguyên trong `Placeholder/Beeps/`**, catalog vẫn trỏ tới đó. Đây là clip hợp lệ, giấy phép `tự tạo`. Nên AC10 chỉ đòi `Final/` cho 11 mã ★.
4. Sau G3: xóa các clip `Placeholder/JSurviv/*` không còn được tham chiếu (commit riêng). Khi đó bước này **không** đụng `SfxCatalogTests.cs:64` vì chỉ lọc `Beeps`; nhưng nếu xóa `copy_jsurviv_placeholders.py` hay `--check` thì ghi vào `Tools/Audio/README.md`.

## 3. Tiêu chí nghiệm thu

Ưu tiên: **P0** phải đạt để qua G3; **P1** nên đạt, bỏ phải có PIC đồng ý; **P2** không chặn. Số đánh dấu giả định (G-1…G-12) có thể đổi ở G1. "Ưu tiên ★" là 11 mã ở bảng 2.2. Mọi AC kiểm bằng dữ liệu, test dữ liệu và panel F6 của AUD-01, không cần gameplay thật. Mọi chỗ ghi "người nghe" là PIC nghe, không máy kiểm được.

### 3.1. Kiểm kê và đối chiếu (tiêu chí GDD 1)

| # | Tiêu chí (đo được) | Cách kiểm | Ưu tiên |
| --- | --- | --- | --- |
| AC1 | Manifest `Docs/Tickets/ART-05_audio_manifest.csv` có **đúng 26 dòng SFX** khớp enum `SfxId` (cùng tên, cùng thứ tự 1–26, không thiếu, không thừa, không trùng). Mỗi dòng có đúng một trạng thái trong {Có, Cần chỉnh, Thiếu} | Test dữ liệu EditMode đọc CSV và `Enum.GetNames(typeof(SfxId))` | P0 |
| AC2 | Mỗi mã không phải "Thiếu" ghi đủ: file nguồn, độ dài (s, 2 chữ số), codec, tần số, kênh, kích thước (byte). Chạy `Tools/Audio/measure_clips.py` hoặc đo lại trên Unity cho kết quả khớp: độ dài ± 0,01 s, kích thước đúng từng byte. Tổng Có + Cần chỉnh + Thiếu = 26. Số hiện có ở bảng 2.2 là mốc so sánh | Script + review CSV | P0 |
| AC3 | Mỗi trong **21 file** của `Assets/Imported/JSurviv/Sound` và mỗi **thành viên của hai zip** (8 + 1) có đúng 1 dòng manifest với phân loại: mã SFX (26) / BGM / UI / ngoài 26 / không dùng. Số dòng "nguồn JSurviv" = 21 + 9 = 30. Không file nào bị bỏ sót | Test đếm `Directory` + CSV | P0 |
| AC4 | Cột giấy phép của **mọi** dòng chỉ nhận một trong ba giá trị ở 2.9. Mọi dòng nguồn JSurviv (và clip dẫn xuất) ghi `chưa kiểm chứng`; dòng `có bằng chứng` phải có tệp ở `Docs/Licenses/Audio/` (đường dẫn tồn tại); không dòng nào chứa "miễn phí", "CC0", "royalty-free", "được phép" mà thiếu đường dẫn bằng chứng. Ghi dấu vết `hollywoodedge.com` của `TimeOver.wav` vào cột ghi chú nguồn | Test CSV + review | P0 |
| AC5 | Hai file `.zip` được liệt kê nội dung đủ (8 + 1 thành viên), tên zip sai lệch được ghi (M5). Không file zip nào được tham chiếu trong catalog. Phần giải nén nằm ở `Final`/thư mục tạm, **không** ở `Assets/Imported/` | Review + `git diff` | P0 |
| AC6 | Mỗi mã "Có" hoặc "Cần chỉnh" có cột "Đã nghe": `có` (PIC hoặc người được chỉ định nghe qua F6, ghi tên và ngày) hoặc `chưa`. Dòng `chưa` coi là **tạm**; báo cáo cuối ghi số mã đã nghe / tổng. Một mã đã nghe và **không khớp** sự kiện thì đổi trạng thái thành Thiếu (hoặc Cần chỉnh) và ghi lý do | Review CSV | P1 |
| AC7 | Báo cáo cuối có **danh sách clip chưa kiểm chứng** (đường dẫn, mã SFX dùng nó) và danh sách "Thiếu" kèm nguồn tạm (bíp tự sinh); số liệu khớp manifest | Review | P1 |

### 3.2. Đưa clip vào catalog bằng dữ liệu (tiêu chí GDD 1–2)

| # | Tiêu chí (đo được) | Cách kiểm | Ưu tiên |
| --- | --- | --- | --- |
| AC8 | Clip cuối nằm trong `Assets/_JungleMaze/Art/Audio/Final/Sfx/` (SFX) và `Final/Music/` (nhạc); tên chỉ gồm `[a-z0-9_]`, đuôi `.wav`/`.ogg`/`.mp3`, không dấu, không khoảng trắng, duy nhất. SFX theo dạng `<sfx_snake>_<NN>.wav`, tối đa **4 biến thể** mỗi mã | Test đường dẫn + review | P1 |
| AC9 | Không clip nào trong `SfxCatalog.asset` và `AudioRuntimeSet.asset` có GUID thuộc `Assets/Imported/**`; GUID của file `Final` **không trùng** GUID nào trong `Assets/Imported/**` (kiểm bằng tập `.meta`). `git diff` của `Assets/Imported/**` **trống** (kể cả `.meta`) | Test dữ liệu + `git diff -- Assets/Imported` | P0 |
| AC10 | **11 mã ưu tiên ★** (4, 5, 6, 7, 14, 15, 18, 19, 20, 24, 25) trong catalog mỗi mã có ≥ 1 clip nằm trong `Final/Sfx/`, không còn trỏ vào `Placeholder/`. Với 4 mã "Thiếu" ưu tiên (7, 14, 24, 25), nguồn của clip cuối ghi trong manifest (tự tạo hoặc nguồn khác theo Q3) | Test dữ liệu đọc catalog | P0 |
| AC11 | Mã nguồn **không đổi**: diff của toàn bộ công việc ART-05 không có tệp `.cs` nào trong `Assets/_JungleMaze/Scripts/**`, không có `Bootstrap.cs`, scene, prefab, `ProjectSettings/**`, `Backend/**`. Chỉ các đường dẫn trong "Vùng file của ART-05". Cột `priority`, `bus`, `rangeCells`, `maxInstances`, `cooldownSeconds`, `repeatCount`, `repeatIntervalSeconds` của 26 mục catalog giữ nguyên (diff YAML chỉ chạm `clips`, `volume`, `pitchJitter`), trừ ngoại lệ ZoneBell3 ở AC28 | Review diff + `git diff --stat` | P0 |
| AC12 | Sau khi thay clip, **bộ test Audio của AUD-01 xanh nguyên**: EditMode `JungleMaze.Tests.Audio.*` (59/59 khi G3) và PlayMode `AudioAcceptanceTests` (15/15 khi G3). Console 0 error, 0 warning, đặc biệt không có "Missing AudioClip" và không có cảnh báo một-lần-mỗi-mã của `SfxPlaybackService`. Nếu một test fail vì **cố định sẵn tên clip tạm** (ví dụ tần số trong tên bíp), ghi nguyên nhân và báo CPO, không sửa logic test | Chạy test qua Unity MCP | P0 |
| AC13 | Các mã cần phân biệt dùng **clip khác nhau đôi một**: `HitMarker`, `HurtLocal`, `ZoneBell3`, `DragonRoar`, `MatchEnd`, `Explosion`; `ShootRanged` ≠ `ShootCrossbow`; `MeleeSwing` ≠ `MeleeHit`; `PickupWeapon`, `PickupItem`, `WearArmor` đôi một khác nhau. Không dùng bản hạ cao độ của cùng một file làm hai mã | Test dữ liệu (so tham chiếu clip) | P0 |
| AC14 | **Không phát trùng và âm lượng hoạt động với clip cuối**: với catalog đã thay, PlayMode của AUD-01 cho kết quả: "Gửi lặp ×5" → ≤ 1 voice mới (A13); `SetVolume(Master|SFX|Music, 0,5)` → tham số mixer −6,02 dB ± 0,1 (A5); spam 100 → ≤ 16 voice SFX (A3). Số voice nhạc ≤ 2 | PlayMode AUD-01 + panel F6 | P0 |
| AC15 | Thay đổi **đảo ngược được bằng dữ liệu**: đổi tham chiếu của một mã ưu tiên về clip trong `Placeholder/` (chỉ sửa asset) thì AC12 vẫn xanh, và diff của lần đổi không có `.cs` | Thử một mã trên bản sao tạm | P2 |

### 3.3. Chất lượng clip, độ dài, mức âm (tiêu chí GDD 2–3)

| # | Tiêu chí (đo được) | Cách kiểm | Ưu tiên |
| --- | --- | --- | --- |
| AC16 | **Import settings** của mọi clip Final theo 2.7: SFX Decompress On Load + ADPCM/PCM, `forceToMono` tắt, `normalize` tắt, mono thật (1 kênh sau import); nhạc Streaming + Vorbis. **Xác nhận bằng đo** rằng cờ `normalize` không đưa mức về 0 dBFS: đỉnh đo bằng `GetData` của clip đã import khớp đỉnh đo trên WAV nguồn ± 0,5 dB | Test EditMode đọc `AudioImporter` + `GetData` + script | P0 |
| AC17 | **Độ dài** (đo `AudioClip.length`) của mọi clip Final nằm trong khoảng đích ở bảng 2.3. Riêng 4 mã bắt buộc: `BombFuse` 0,80–1,50 s; `Explosion` ≤ 3,00 s; `HitMarker` ≤ 0,15 s; `DragonRoar` ≥ 1,50 s | Test dữ liệu đọc catalog | P0 cho mã ★ và `BombFuse`, `Explosion`; P1 phần còn lại |
| AC18 | **Đỉnh và chống click:** mọi clip SFX Final có đỉnh trong **−3,0…−1,0 dBFS**, không chuỗi ≥ 3 mẫu chạm trần, |x| ≤ −40 dBFS ở 2 ms đầu và 2 ms cuối | Script + test `GetData` | P0 (mã ★), P1 (còn lại) |
| AC19 | **Mức hiệu dụng E** (2.6) của mỗi mã nằm trong tầng của nó ± 3 dB: T1 −16, T2 −20, T3 −24, T4 −29 dBFS. `volume` trong catalog ≤ 1,0 và đủ để đạt E (nếu `L` quá thấp thì tăng độ lợi trong file, không nâng `volume` quá 1,0) | Test dữ liệu: `E = L + 20·log10(volume)` từ `GetData` | P0 (mã ★), P1 (còn lại) |
| AC20 | **Hitmarker rõ:** E(`HitMarker`) ≥ E của mọi mã thuộc T2–T4 + 3 dB, và ≥ E(`FootstepGrass`) + 12 dB; `volume` của `HitMarker` ≥ 1,0 (A19 AUD-01 giữ nguyên). Với 15 âm khác đang phát cùng lúc, `HitMarker` vẫn phát cùng khung (A19 của AUD-01, chạy lại) | Test dữ liệu + PlayMode AUD-01 | P0 |
| AC21 | **Nhận sát thương:** `HurtLocal` 0,20–0,60 s, tầng T1, khác clip `HitMarker`; phát qua `NotifyDamageTaken` (A14 AUD-01, chạy lại) đúng một voice trong cùng khung | Test dữ liệu + PlayMode AUD-01 | P0 |
| AC22 | **Bắn:** `ShootRanged` 0,15–1,20 s, `ShootCrossbow` 0,15–1,00 s (khác file), cả hai T2; 5 phát `ShootRanged` trong 400 ms (Ná 3/s) không vượt "Tối đa" 6 voice của catalog | Test dữ liệu + PlayMode (spam F6) | P0 |
| AC23 | **Melee:** `MeleeSwing` 0,15–0,60 s, `MeleeHit` 0,10–0,50 s (khác file), cả hai T2 | Test dữ liệu | P0 |
| AC24 | **Loot:** `PickupWeapon`, `PickupItem` 0,15–0,80 s; `WearArmor` 0,30–1,20 s; cả ba T3 và đôi một khác file. `PickupItem` và `WearArmor` **không** còn dài 2,06 s | Test dữ liệu | P0 |
| AC25 | **Chuông bo (`ZoneBell3`):** clip một tiếng 0,40–0,75 s với lặp 3 hoặc clip chứa ba tiếng 2,0–2,6 s với lặp 1; lịch phát đo được: tiếng thứ nhất ở khung kế tiếp, ba tiếng bắt đầu cách nhau 0,80 ± 0,05 s (A20 AUD-01, chạy lại với clip thật); bus Alert; tầng T1 | PlayMode AUD-01 + test dữ liệu | P0 |
| AC26 | **Tiếng rồng (`DragonRoar`):** 1,50–4,00 s, clip riêng (không dùng chung với mã khác), bus Alert, tầng T1; phát một lần, lặp trong 5 s bị bỏ qua (cooldown AUD-01) | Test dữ liệu + F6 | P0 |
| AC27 | **Bom:** sau `Explosion`, voice `BombFuse` của đúng quả bom đó **không còn phát sau 0,2 s** (router AUD-01 đã dừng; chạy lại với clip thật). Clip `BombFuse` ≤ 1,50 s, `Explosion` ≤ 3,00 s, đuôi cả hai kết thúc bằng fade (|x| ≤ −40 dBFS ở 2 ms cuối) | PlayMode AUD-01 + test dữ liệu | P1 |
| AC28 | Nếu clip chuông cuối chứa ba tiếng, `repeatCount` của `ZoneBell3` đổi từ 3 xuống 1 **trong asset** (không `.cs`) và A20 vẫn đạt; nếu không, giữ 3. PIC đồng ý đổi (đây là cột duy nhất ngoài `clips`/`volume`/`pitchJitter` được phép đổi) | Diff YAML + PlayMode A20 | P1 |

### 3.4. Nhạc nền (tiêu chí GDD 3)

| # | Tiêu chí (đo được) | Cách kiểm | Ưu tiên |
| --- | --- | --- | --- |
| AC29 | Ba bài có bản cuối trong `Final/Music/`, **mỗi file ≤ 3,0 MB**, độ dài mỗi bài lệch ≤ 0,1 s so với bản gốc (145,53 / 43,65 / 123,48 s). **Không** có file ≥ 5 MB nào trong `Assets/_JungleMaze/` do ART-05 thêm. `Waiting Room Music.wav` (25,7 MB) không được chép | Test kích thước + độ dài | P1 |
| AC30 | Mức bài: đỉnh ≤ −1,0 dBFS; mức hoạt động −26 ± 2 dBFS; mức này thấp hơn tầng T1 ≥ 8 dB. Đo trên bản WAV trước nén và đo lại sau nén một lần (đường 1 ở 2.6) | Script + test tạm | P1 |
| AC31 | `AudioRuntimeSet` trỏ `waitingRoomMusic` tới **bản nén của Waiting Room** (không còn dùng `loading_music` làm Waiting Room); mọi tham chiếu nhạc khác 0 (không rỗng) | Test dữ liệu | P1 |
| AC32 | **BGM tắt mặc định giữ nguyên:** sau ART-05, `musicEnabled = false` thì **0** AudioSource nhạc phát ở mọi `AppScreen`; bật qua F6 thì phát đúng bài trong ≤ 1 s mà không có "Missing AudioClip" (A23 AUD-01, chạy lại) | PlayMode AUD-01 | P0 |
| AC33 | **Không chồng nhạc, không che cảnh báo** với file thật: A24 (crossfade 1,0 ± 0,1 s, ≤ 2 nguồn nhạc, hết fade còn 1) và A22 (nhạc hạ ≥ 9 dB trong ≤ 0,2 s khi `ZoneBell3`/`DragonRoar`, trả về ≤ 1,0 s sau khi hết) chạy lại xanh | PlayMode AUD-01 | P1 |
| AC34 | **Điểm lặp** của mỗi bài (Match 123 s lặp ≥ 3 lần trong trận 420 s): PIC nghe một vòng lặp, ghi `không giật` / `giật` kèm bài và thời điểm. Bài `giật` thì ghi nhận, không chặn | Người nghe | P2 |

### 3.5. Ranh giới, dung lượng, nghe thử

| # | Tiêu chí (đo được) | Cách kiểm | Ưu tiên |
| --- | --- | --- | --- |
| AC35 | **Tổng dung lượng** file âm thanh mới thêm (SFX + nhạc + `.meta` không tính) ≤ **12 MB**; mỗi clip SFX ≤ 0,5 MB; không file nào mới ≥ 3,0 MB ngoài nhạc. Số liệu ghi vào báo cáo cuối | Script đếm | P1 |
| AC36 | **Người nghe** (PIC) nghe 11 mã ★ bằng F6 (từng mã, "Dồn 10 s", "Gửi lặp ×5") và ghi: hitmarker nghe rõ giữa nhiều âm; chuông bo nhận ra là cảnh báo; tiếng rồng phân biệt được; mức giữa các nhóm cân bằng (A28 của AUD-01, cùng đợt). Điều chưa ổn ghi thành việc chọn lại clip (đổi dữ liệu), không sửa mã | Người nghe | P1 |
| AC37 | **Hai việc AUD-01 bàn giao** được xử lý và ghi trong báo cáo cuối: (a) `countdown.mp3` 11,78 s và `bomb nổ.mp3` 5,41 s đã cắt (AC17, AC27); (b) kiểm lại AC-A28 sau khi thay clip. Việc còn dở ghi rõ lý do | Review báo cáo | P1 |
| AC38 | **Giấy phép của clip đang dùng:** manifest có dòng cho cả **12 clip SFX và 2 bài nhạc** AUD-01 đã chép, mỗi dòng ghi tên file gốc, md5 của bản chép **bằng** md5 của file gốc (kiểm bằng `copy_jsurviv_placeholders.py --check` hoặc đo lại), giấy phép `chưa kiểm chứng`, và dấu vết metadata (2.12). `TimeOver.wav` ghi rõ thẻ hollywoodedge.com | Test CSV + `--check` | P0 |
| AC39 | **Đưa lên `Final` không đổi mã và không làm hỏng công cụ cũ:** (a) `SfxCatalog.asset` đổi chỉ ở `clips` (GUID mới trong `Final/`), không đổi đường dẫn thư mục `Placeholder`; (b) `SfxCatalogTests.BeepsHaveDistinctFrequencies` và `copy_jsurviv_placeholders.py --check` vẫn chạy đúng (bíp còn lại vẫn ở `Placeholder/Beeps/`, bản chép còn tới khi dọn); (c) mỗi mã ở bảng 2.11(b) có đúng một kết luận trong manifest: `thử ứng viên` / `khoảng trống thật`, kèm nguồn đã chọn (`tự tạo`, nguồn tự do có bằng chứng, hay `giữ bíp`); không mã nào ghi nguồn "tự do" mà thiếu tệp bằng chứng | Test + review diff | P1 |

**Ánh xạ tiêu chí GDD:**

| Tiêu chí GDD | AC |
| --- | --- |
| 1. Bảng 26 SFX đối chiếu file thật, trạng thái dùng được/thiếu, nguồn và tình trạng quyền sử dụng; không khẳng định license khi chưa kiểm chứng | AC1–AC7, AC38 |
| 2. Event ưu tiên phát đúng clip, Master/SFX (và Music nếu bật) có hiệu lực, không lặp audio khi event gửi lại | AC8–AC15, AC16–AC28 (đặc biệt AC14, AC20–AC26) |
| 3. BGM chỉ tích hợp khi được duyệt; không nhạc chồng nhau/che cảnh báo; không missing AudioClip | AC29–AC34 |
| Ranh giới, dung lượng, nghe thử (bảo vệ ba tiêu chí trên) | AC11, AC12, AC35–AC39 |

## Câu hỏi và giả định chờ G1

**Giả định** (đã đưa vào các bảng và AC; mặc định đang theo):

- **G-1** Trạng thái theo quy tắc ở chú thích bảng 2.2; chưa ai nghe nên mọi trạng thái là **tạm** cho tới khi AC6.
- **G-2** Danh sách 26 mã và thứ tự theo `SfxId` của AUD-01; không thêm/bớt mã trong ART-05.
- **G-3** Clip cuối ở `Assets/_JungleMaze/Art/Audio/Final/` (Q1); tên `<sfx_snake>_<NN>.wav`; nhạc `Final/Music/`; manifest `Docs/Tickets/ART-05_audio_manifest.csv`; bằng chứng giấy phép ở `Docs/Licenses/Audio/`.
- **G-4** SFX cuối là WAV 16-bit 44,1 kHz **mono đã bake** (Q2); lý do: đo được bằng thư viện chuẩn, không mất chất lượng qua hai lần nén, và AUD-01 đã ép mono.
- **G-5** Đỉnh −3,0…−1,0 dBFS (đích −2,0); không chạm trần; fade 2–5 ms hai đầu; |x| ≤ −40 dBFS ở 2 ms đầu/cuối.
- **G-6** Tầng mức hiệu dụng E: T1 −16, T2 −20, T3 −24, T4 −29 dBFS ± 3 dB; BGM −26 ± 2 dBFS; HitMarker ≥ mọi T2–T4 + 3 dB và ≥ FootstepGrass + 12 dB (Q6).
- **G-7** Cách đo: trộn mono; mức hoạt động = RMS giữa mẫu đầu và mẫu cuối có |x| ≥ 0,003; E = L + 20·log10(`volume`).
- **G-8** Khoảng độ dài đích của 26 mã theo bảng 2.3 (cơ sở: cooldown/số bản tối đa của catalog, ngòi bom 1,0–1,5 s, thời gian dùng đồ 0,5–3,5 s, pool 16 voice).
- **G-9** Ngân sách dung lượng: ≤ 3,0 MB mỗi file nhạc, ≤ 0,5 MB mỗi clip SFX, ≤ 12 MB tổng file mới.
- **G-10** Import settings theo 2.7 (SFX Decompress On Load/ADPCM, `forceToMono` và `normalize` tắt; nhạc Streaming + Vorbis 0,7).
- **G-11** Từ vựng giấy phép: `chưa kiểm chứng` / `tự tạo (…)` / `có bằng chứng: <đường dẫn>`. Không bằng chứng thì không viết gì khác.
- **G-12** Với 14 mã đang là bíp (2.11): 3 mã thử ứng viên JSurviv (1, 13, 16), 11 mã là khoảng trống thật. Mã không ưu tiên giữ bíp tự sinh của AUD-01 (AUD-01 chạy bình thường); 4 mã ưu tiên (7, 14, 24, 25) cần clip riêng với nguồn theo Q3.

| # | Câu hỏi | Mặc định / khuyến nghị |
| --- | --- | --- |
| **Q1** | **Clip cuối nằm đâu**: sao chép/chuyển đổi vào `Art/Audio/Final/` hay tham chiếu thẳng `Assets/Imported/JSurviv/Sound`? | **Khuyến nghị: sao chép/chuyển đổi vào `Final/`** (2.4): import settings nằm trong `.meta`, phải cắt/chuẩn hóa, giấy phép chưa rõ có thể phải bỏ từng file, vùng nhập không sửa được |
| **Q2** | **Định dạng SFX cuối**: WAV 16-bit mono đã bake (đo được bằng thư viện chuẩn, không mất chất lượng) hay giữ MP3? `MatchEnd` giữ stereo hay mono? | **Khuyến nghị: WAV 16-bit 44,1 kHz mono** cho mọi SFX, kể cả `MatchEnd` (AUD-01 đã ép mono, `spatialBlend = 0`). Nhạc giữ stereo |
| **Q3** | **Nguồn cho 13 mã Thiếu** (4 ưu tiên: `HitMarker`, `ShootCrossbow`, `ZoneBell3`, `DragonRoar`). Lựa chọn: (a) giữ bíp tự sinh; (b) tự tổng hợp tốt hơn bằng script của repo (giấy phép sạch: `tự tạo`); (c) lấy từ nguồn có giấy phép ghi rõ: đã kiểm trang Kenney Impact Sounds và Interface Sounds (CC0, 27/10/2026), **chưa tải, chưa nghe** (2.11); (d) công cụ sinh âm thanh AI | **Khuyến nghị: (b) cho 4 mã ưu tiên (clip gọn sắc cho hitmarker, một tiếng chuông, tiếng gầm trầm riêng, một tiếng nỏ), (a) cho mã còn lại; thêm (c) thử hai gói Kenney cho `HitMarker`, `EmptyClick`, `UseCancel`, `TrapTriggered`, `ArmorAbsorb` nếu người dùng đồng ý cho tải** (lưu bằng chứng vào `Docs/Licenses/Audio/`, nghe rồi mới nhận). Không dùng (d) khi chưa rõ điều khoản |
| **Q4** | **BGM có thuộc MVP không?** GDD chưa chốt (Assets STT 14) | **Khuyến nghị: giữ mặc định tắt**, vẫn nén và ánh xạ ba bài (2.8) để bật bằng một công tắc khi PIC chốt; không làm UI mới |
| **Q5** | **Giấy phép**: cho phép phát hành bản mẫu LAN lớp học với clip `chưa kiểm chứng` hay chặn tới khi có bằng chứng? Ai kiểm bằng chứng? | **Khuyến nghị: được dùng trong bản mẫu nội bộ/LAN lớp học, chặn phát hành công khai** cho tới khi người dùng cung cấp bằng chứng bằng văn bản vào `Docs/Licenses/Audio/`; ART-05 xuất danh sách clip chưa kiểm chứng (AC7). Riêng `TimeOver.wav` (thẻ hollywoodedge.com) nên thay trước mọi phát hành |
| **Q6** | **Mục tiêu mức âm**: T1 −16 / T2 −20 / T3 −24 / T4 −29 dBFS và BGM −26 dBFS có hợp lý không? | **Khuyến nghị: dùng như G-6**, PIC chỉnh sau khi nghe (AC36). Các số này nằm trong asset (`volume`), đổi được không cần mã |
| **Q7** | **Xử lý `Art/Audio/Placeholder/`** sau khi thay | **Khuyến nghị: không đổi tên thư mục `Placeholder`, không di chuyển file** (2.13); giữ nguyên tới G3, sau G3 xóa các clip `Placeholder/JSurviv/*` không còn được catalog tham chiếu, giữ bíp của mã còn dùng bíp. Việc xóa là commit riêng |
| **Q8** | **Âm ngoài 26 mã** có trong thư mục nhưng không thuộc ticket này: `button.mp3` (tiếng nút UI), `revive.mp3` (hồi sinh: Tiên dược sự sống), `Bo co.mp3` (bo thu hẹp), Leather armor biến thể, `Pick impacts` biến thể | **Khuyến nghị: chỉ ghi vào manifest với phân loại "ngoài 26" và gợi ý ticket dùng** (UI-02/UI-03, ITM-01, ZON-01). Không thêm `SfxId` |
| **Q9** | **Ai nghe và khi nào** (AC6, AC36)? Máy và agent không nghe được; mọi trạng thái là tạm | **Khuyến nghị: PIC (V.Huy) nghe qua F6** sau khi có clip cuối, theo hướng dẫn mục 5 của `Docs/Reports/AUD-01_2026-10-27/QA_report.md`; kết quả ghi vào mục 7 |
| **Q10** | **`BombFuse`**: cắt còn 0,8–1,5 s (không lặp) hay làm clip lặp được dừng khi nổ? | **Khuyến nghị: cắt ngắn, không lặp.** Router AUD-01 đã dừng fuse khi nổ/gỡ cảnh báo; clip ngắn không cần vòng lặp |
| **Q11** | **Nguồn cho `WearArmor` và `PickupItem`**: dùng 4 tệp `Leather Armor Put On` (trong zip) làm biến thể `WearArmor`, và cắt `Gun/Helmet Put On` ≤ 0,80 s cho `PickupItem` | **Khuyến nghị: đúng như vậy**, vì tên và độ dài khớp sự kiện hơn "đặt súng/mũ" 2 s. Cần nghe để xác nhận |
| **Q12** | **Công cụ xử lý** (cắt, mono, chuẩn hóa, nén nhạc): máy này không có `ffmpeg`/`sox`. Cho phép cài và dùng công cụ ngoài (Audacity hoặc ffmpeg) cho bước xử lý, chỉ commit kết quả và tham số? | **Khuyến nghị: cho phép**, công cụ không đưa vào repo; manifest ghi đoạn cắt, độ lợi, công cụ, phiên bản. Phần đo (WAV) dùng script thư viện chuẩn |

**Rủi ro:**

- **R1 · Giấy phép chưa có bằng chứng.** Không file nào có giấy phép; `TimeOver.wav` mang thẻ thư viện thương mại (hollywoodedge.com). Cả 21 file JSurviv là `chưa kiểm chứng`. Phát hành công khai với chúng có thể vi phạm bản quyền. ART-05 báo cáo và xuất danh sách, không giải quyết được; cần người dùng.
- **R2 · Chỉ 4/26 mã có file dùng được ngay.** 13 mã Thiếu (4 ưu tiên) cần nguồn mới; nếu không có thì giữ bíp, hitmarker/chuông/rồng vẫn là tiếng tổng hợp thô.
- **R3 · Import `normalize` + `forceToMono` của AUD-01** (`normalize: 1`, `forceToMono: 1` ở mọi clip tạm) có thể chuẩn hóa mọi clip về đỉnh ~0 dBFS khi import, làm mất tác dụng của việc chuẩn hóa trong file. Chưa đo trong Unity ở ticket này. AC16 bắt buộc đo `GetData` sau import; nếu đúng thì ART-05 tắt `normalize` cho clip Final (dữ liệu).
- **R4 · Không có bộ giải mã trên máy** (`ffmpeg`, `sox` không có): chưa đo được mức của 14 file MP3. Đo nhạc phải qua bản WAV hoặc `GetData` trong Unity (QA đang giữ Unity Editor). Ảnh hưởng thời gian.
- **R5 · Không ai nghe được bằng máy.** Mọi trạng thái Có/Cần chỉnh là theo tên và độ dài; có thể sai (ví dụ `bleed.mp3` chưa chắc là tiếng trúng đòn, `bot di chuyển.mp3` chưa chắc là bước chân).
- **R6 · Test AUD-01 cố định sẵn clip tạm.** `SfxCatalogTests.cs:64` lọc bíp theo đường dẫn `/Placeholder/Beeps/` (test `BeepsHaveDistinctFrequencies`), và hai script `Tools/Audio` ghi cố định vào `Placeholder/` (2.13); thay clip có thể làm fail. ART-05 không sửa logic test của AUD-01 (AC12), chỉ báo CPO.
- **R7 · Clip dùng đồ dài hơn thời gian dùng.** Món dùng 0,5 s mà clip 1,5 s vẫn kêu sau khi dùng xong, và router chưa dừng `UseHeal`/`UseDrink` khi `UseCancel`. Cắt ≤ 1,5 s giảm vấn đề nhưng không xóa; dừng clip khi hủy là việc của AUD-01 (ticket sau), không phải ART-05.
- **R8 · Một clip chung cho nhiều vũ khí.** `ShootRanged` dùng một file cho Ná cao su, Quyền trượng và Shotgun (nhịp bắn 0,8–3 phát/s); người chơi không phân biệt được vũ khí bằng tai. Biến thể riêng là mở rộng (Q8).

## 4. Kế hoạch kỹ thuật

<tech-lead điền sau G1.>

## 5. Nhật ký build

<Dev ghi ngắn: đã làm gì, quyết định phát sinh, lệch kế hoạch và lý do.>

## 6. Review

<code-reviewer: lỗi theo mức Chặn / Nên sửa / Gợi ý, trạng thái đã xử lý.>

## 7. QA

| # | Kết quả | Bằng chứng |
| --- | --- | --- |
| AC1 | ✓ / ✗ / chưa kiểm chứng | |

## 8. Nghiệm thu

<CPO tổng hợp; người duyệt G3, ngày, ghi chú. Nhắc cập nhật Status trong GDD.>
