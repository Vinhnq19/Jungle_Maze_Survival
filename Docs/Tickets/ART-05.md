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

**Cách bổ sung đã chọn (CPO 27/10/2026, theo chỉ đạo của người dùng):** 14 mã hiện chỉ có bíp được thay bằng clip **CC0** tải từ thư viện tự do (Kenney, OpenGameArt) với nguồn ghi đủ; 12 mã đang dùng JSurviv được cắt và chuẩn hóa cùng thang mức; tất cả vào `Assets/_JungleMaze/Art/Audio/Sfx/`.

Ticket **không** quyết định quyền sử dụng: mọi file JSurviv ghi "chưa kiểm chứng" cho tới khi có bằng chứng bằng văn bản trong repo.

## 2. Phạm vi và ranh giới

**Trong phạm vi:**

1. **Kiểm kê** 21 file trong `Assets/Imported/JSurviv/Sound` (19 file âm thanh + 2 file `.zip` chứa 9 clip): định dạng, tần số lấy mẫu, kênh, độ dài, kích thước, mức đỉnh/mức trung bình (khi đo được), nguồn và giấy phép. Kết quả ghi vào manifest `Docs/Tickets/ART-05_audio_manifest.csv` (một dòng cho mỗi clip có trong repo, xem 2.5).
2. **Đối chiếu 26 SfxId với file thật** (bảng 2.2): trạng thái **Có / Cần chỉnh / Thiếu**. Chỉ "Có" khi có file đúng loại sự kiện, độ dài trong khoảng đích (bảng 2.3) và không cần ghép/cắt. Chưa ai nghe file: mọi trạng thái là **tạm** cho tới khi PIC nghe (AC6).
3. **Xử lý clip** cho các mã "Cần chỉnh" và "Thiếu" có nguồn: giải nén (zip), cắt, fade hai đầu, đổi mono, chuẩn hóa mức, xuất WAV 16-bit. Chuẩn hóa theo mục 2.6. Mọi thao tác nằm ngoài `Assets/Imported`.
4. **Nơi lưu clip cuối** (quyết định ở 2.4): sao chép/chuyển đổi vào `Assets/_JungleMaze/Art/Audio/Sfx/` (SFX) và `Assets/_JungleMaze/Art/Audio/Music/` (nhạc), không tham chiếu `Assets/Imported`.
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
| `Assets/_JungleMaze/Art/Audio/Sfx/**`, `Assets/_JungleMaze/Art/Audio/Music/**` | Clip cuối (SFX, nhạc) và `.meta` |
| `Docs/Art/Audio/ART05_Sources.vi.md` | Bảng nguồn của clip tải về (do CPO lập; thư mục `Docs/Art/**` trước đây thuộc vùng của Codex, nên ghi một dòng ở `Agent_Coordination.vi.md` mục 4 khi thêm) |
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

**Khuyến nghị (Q1): sao chép/chuyển đổi sang `Assets/_JungleMaze/Art/Audio/Sfx/` và `…/Music/`, không tham chiếu `Assets/Imported`.** CPO đã chọn đích `Art/Audio/Sfx/` (27/10). Lý do:

1. **Cài đặt import nằm trong `.meta`.** Đổi nén, mono, tải-khi-nạp của file trong `Assets/Imported/**` là sửa vùng nhập (cấm). Bản chép có `.meta` riêng.
2. **Phải cắt và chuẩn hóa**: mọi clip "Cần chỉnh" cần bản dẫn xuất (cắt, mono, mức), nên không thể dùng file gốc nguyên bản.
3. **Giấy phép chưa rõ**: nếu một file JSurviv phải bỏ, chỉ cần xóa bản chép và dòng manifest; thay bằng file khác không chạm vùng nhập.
4. **Thư mục JSurviv là vùng tham khảo** (`JSurviv_Integration.vi.md`), có thể bị dọn sau này.
5. Phù hợp cách AUD-01 đã làm với `Placeholder/` (chép, đổi tên ASCII, **không chép `.meta`** để Unity cấp GUID mới).

Cái giá: lặp byte (khoảng 1–2 MB SFX sau khi cắt; nhạc đã nén nhỏ hơn bản gốc). Chấp nhận được. **Không** chép `Waiting Room Music.wav` (25,7 MB) vào `_JungleMaze`; chỉ chép bản đã nén.

Bố cục đề xuất (G-3):

```
Assets/_JungleMaze/Art/Audio/
  Sfx/<sfx_snake>_<NN>.wav        # vd. melee_swing_01.wav, wear_armor_02.wav (1–4 biến thể mỗi mã)
  Music/<track>.<ogg|mp3>         # waiting_room, loading, match
  Placeholder/…                         # của AUD-01; bíp vẫn là clip của mã Thiếu
Docs/Tickets/ART-05_audio_manifest.csv  # một dòng cho mỗi clip, xem 2.5
Docs/Licenses/Audio/                    # bằng chứng giấy phép, nếu có
```

**Phương án còn lại (không khuyến nghị): tham chiếu thẳng `Assets/Imported/JSurviv/Sound`.** Nhanh hơn (không chép), nhưng không cắt/chuẩn hóa được, không đổi được import, và buộc game phụ thuộc vùng nhập.

### 2.5. Cách một clip thay một clip tạm, không đổi mã

1. **Chọn nguồn** → thêm dòng manifest (nguồn gốc, giấy phép "chưa kiểm chứng" hoặc bằng chứng).
2. **Xử lý ngoài Unity** từ file gốc (giữ nguyên): giải nén nếu cần, cắt đầu/cuối, fade 2–5 ms hai đầu (không click), đổi mono, chuẩn hóa **RMS** theo 2.6 (đỉnh ≤ −1,0 dBFS), xuất WAV 16-bit 44,1 kHz. Ghi vào manifest: đoạn cắt (giây), độ lợi (dB), công cụ.
3. **Đặt file** vào `Sfx/` với tên ASCII chữ thường. Unity sinh `.meta` mới; cài đặt import theo 2.7.
4. **Sửa dữ liệu** trong `SfxCatalog.asset`: thêm/thay clip trong `clips` của **đúng** `SfxId` (catalog nhận nhiều clip cho một mã, `ClipPicker` chọn không lặp liền nhau), chỉnh `volume` (và `pitchJitter` nếu cần) cho đúng tầng mức. Không sửa cột khác. Nhạc: sửa `waitingRoomMusic`, `loadingMusic`, `matchMusic` trong `AudioRuntimeSet.asset`.
5. **Kiểm**: chạy bộ test Audio của AUD-01 (phải xanh nguyên), phát bằng panel F6 (từng mã, "Dồn 10 s", "Gửi lặp ×5"), đo mức và độ dài (AC).
6. **Commit** kèm đường dẫn: clip + `.meta` + `SfxCatalog.asset`/`AudioRuntimeSet.asset` + manifest cùng một commit cho mỗi nhóm (vd. nhóm ưu tiên trước). Không có `.cs` nào trong diff (AC11).

Đổi lại dễ: mỗi lần thay chỉ là đổi tham chiếu clip trong asset; bản bíp/`Placeholder` còn nguyên cho tới khi bước dọn dẹp (Q7).

### 2.6. Chuẩn hóa âm lượng: mục tiêu và cách đo

**Nguyên tắc:** chuẩn hóa **trong file** (RMS theo bảng dưới, đỉnh ≤ −1,0 dBFS), thứ tự to-nhỏ giữa các sự kiện đặt bằng `volume` trong catalog (dữ liệu). Không dùng LUFS theo ITU-R BS.1770 vì nhiều clip ngắn hơn khối 400 ms (hitmarker 0,06 s, click 0,06 s) nên không đo được.

**Định nghĩa đo** (G-7). Tất cả tính trên tín hiệu **đã trộn mono**, mẫu −1…1:

- **Đỉnh** = max |x| của clip, dBFS.
- **Mức hoạt động L** = RMS (dBFS) tính trên đoạn từ mẫu đầu tiên đến mẫu cuối cùng có |x| ≥ 0,003 (−50 dBFS), tức bỏ im lặng hai đầu.
- **Mức hiệu dụng E** = L + 20·log10(`volume` trong catalog). E là con số nghe thấy thật; `volume` ≤ 1 nên E ≤ L.

**Quy tắc cho mọi clip SFX cuối (G-5, G-6):**

| Quy tắc | Giá trị |
| --- | --- |
| Đỉnh | **≤ −1,0 dBFS**; không có chuỗi ≥ 3 mẫu liên tiếp chạm trần |
| Mức hoạt động L đã nướng trong file (CPO 27/10) | **bước chân (`FootstepGrass`, `FootstepSwamp`) −24 dBFS ± 2**; **mọi SFX khác −20…−15 dBFS**. Cắt im lặng hai đầu trước khi đo |
| Chống click | |x| trong 2 ms đầu và 2 ms cuối ≤ −40 dBFS (có fade) |
| Mức hiệu dụng E | trong tầng của mã ± 3 dB (bảng 2.3): **T1 −16** (HitMarker, HurtLocal, ZoneBell3, DragonRoar, MatchEnd) · **T2 −20** (bắn, chém, trúng cận chiến, bom, bẫy, giáp đỡ, bị loại, ném) · **T3 −24** (nhặt, mặc, dùng đồ, nạp, hết đạn, hủy) · **T4 −29** (bước chân, vào bụi) |
| Quan hệ ưu tiên | E của `HitMarker` ≥ E của **mọi mã thuộc T2–T4** + 3 dB; E của `HitMarker` − E của `FootstepGrass` ≥ 12 dB |
| BGM | đỉnh ≤ −1,0 dBFS; mức hoạt động (cả bài) **−26 ± 2 dBFS** ở `MusicVolume` = 1 (nên thấp hơn T1 ≥ 8 dB, trước khi AUD-01 hạ thêm −9 dB lúc có cảnh báo) |

Lý do T1 > T2 > T3 > T4: người chơi cần nghe được hitmarker, tiếng bị đánh, chuông bo và rồng **trong lúc** súng nổ và bước chân dày; bước chân rất thường xuyên nên ở tầng thấp nhất (AUD-01 A19: HitMarker ≥ 2 × FootstepGrass, vẫn thỏa: 1,0 so với 0,4).

**Công cụ đo** (hai đường, kết quả lệch ≤ 0,5 dB):

1. **Trong Unity**: test EditMode đọc `AudioClip.GetData` của từng clip trong catalog (clip SFX nạp kiểu Decompress On Load). Cách này đo đúng dữ liệu **sau import**, nên bắt được hiệu ứng của cờ `normalize`/`forceToMono` (R3).
2. **Ngoài Unity**: `Tools/Audio/measure_clips.py` (thư viện chuẩn, đọc WAV bằng `wave`) cho các file WAV nguồn của `Sfx`. File MP3/OGG (nhạc) không đọc được bằng thư viện chuẩn: đo trên bản WAV trước khi nén và đo lại một lần sau nén bằng đường 1 (R4).

Chưa có số đo mức cho các file MP3 (14 file) vì máy này không có bộ giải mã (`ffmpeg`/`sox` không có). Các số mức ở bảng 2.2 chỉ cho WAV.

### 2.7. Cài đặt import cho clip cuối

| Loại | Cài đặt (G-10) |
| --- | --- |
| SFX (`Sfx/*.wav`) | WAV 16-bit **mono đã bake**, 44,1 kHz; Load Type **Decompress On Load**; nén **ADPCM** (hoặc PCM với clip ≤ 0,3 s); `forceToMono` **tắt**, `normalize` **tắt**; `preloadAudioData` bật; `loadInBackground` tắt; `3D` giữ mặc định (AUD-01 để `spatialBlend` = 0) |
| Nhạc (`Music/*`) | giữ stereo; Load Type **Streaming**; Vorbis chất lượng 0,7; `loadInBackground` bật; `normalize` tắt; tệp nguồn ≤ 3,0 MB |

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

**Tổng (phân tích JSurviv, trước khi có hướng nguồn mới):** trong 14 bíp, 3 mã có ứng viên JSurviv chưa nghe (1, 13, 16) và 11 mã là khoảng trống thật (2, 3, 7, 8, 9, 10, 14, 17, 23, 24, 25). Khớp bảng 2.2: 13 mã "Thiếu" = 11 khoảng trống thật + 2 mã thử ứng viên (13, 16); mã 1 nằm ở "Cần chỉnh".

**Hướng đã chọn (CPO 27/10/2026, theo chỉ đạo của người dùng "thiếu cái gì thì tự tìm thư viện rồi tự bổ sung"): lấp cả 14 mã bíp bằng clip CC0 từ thư viện tự do, có ghi nguồn.** Đây là cách giải quyết chính, không còn là câu hỏi mở. Các ứng viên JSurviv chưa nghe ở trên (`bot di chuyển.mp3`, `Pick_Impacts_…wav`) **không dùng** cho 14 mã này; hai file đó ghi "ngoài 26" (Q8). Nguồn (theo bảng của CPO; **chưa có trong repo tại thời điểm viết spec**, `Docs/Art/Audio/ART05_Sources.vi.md` chưa tồn tại, nên tên clip dưới đây chưa được tôi kiểm chéo với gói gốc):

| # | SfxId | Nguồn đã chọn (CC0 1.0 theo CPO) | Ghi chú |
| --- | --- | --- | --- |
| 1 | `FootstepGrass` | Kenney Impact Sounds: `footstep_grass` × 5 | 5 biến thể |
| 2 | `FootstepSwamp` | Kenney Impact Sounds: `impactSoft_medium` × 5 | 5 biến thể |
| 3 | `BushEnter` | Kenney RPG Audio: `cloth1`, `cloth3` | |
| 7 ★ | `ShootCrossbow` | Kenney Interface Sounds: `pluck_001` | |
| 8 | `Throw` | Kenney RPG Audio: `drawKnife2` | |
| 9 | `Reload` | Kenney RPG Audio: `metalLatch` | |
| 10 | `EmptyClick` | Kenney RPG Audio: `metalClick` | |
| 13 | `TrapTriggered` | Kenney Impact Sounds: `impactMetal_heavy` | |
| 14 ★ | `HitMarker` | Kenney Interface Sounds: `glass_005` | Phải ≤ 0,15 s sau khi cắt |
| 16 | `ArmorAbsorb` | Kenney Impact Sounds: `impactPlate_medium` × 3 | 3 biến thể |
| 17 | `Eliminated` | Kenney Interface Sounds: `minimize_006` | |
| 23 | `UseCancel` | Kenney Interface Sounds: `back_002` | |
| 24 ★ | `ZoneBell3` | Kenney Impact Sounds: `impactBell_heavy` | Một tiếng 0,40–0,75 s (catalog lặp 3), hoặc theo AC25 |
| 25 ★ | `DragonRoar` | OpenGameArt "CC0 deep monster roar" của trazzz123, cắt còn 3,0 s | Trong khoảng 1,5–4,0 s |

**Xử lý (CPO):** mono, 44,1 kHz, WAV; cắt im lặng; chuẩn hóa RMS (bước chân −24 dBFS, còn lại −20…−15); đỉnh ≤ −1 dBFS; `normalize` của importer tắt; đặt ở `Assets/_JungleMaze/Art/Audio/Sfx/`. Đổi GUID trong catalog, không đổi mã. Test `SfxCatalogTests` đếm "14 bíp" thành **0** (xem AC12, 2.13).

**Quy tắc ghi nguồn:** mỗi clip tải về có dòng trong manifest và trong `ART05_Sources.vi.md`: URL trang nguồn, tên file gốc, tác giả, ngày tải, đoạn cắt, độ lợi. Giấy phép chỉ ghi `có bằng chứng: <đường dẫn>` khi tệp bằng chứng nằm trong `Docs/Licenses/Audio/` (tệp license đi kèm gói Kenney; bản chụp trang OpenGameArt). Tôi đã đọc trang Kenney ngày 27/10: Impact Sounds (130 file) và Interface Sounds (100 file) ghi "Creative Commons CC0", trang hỗ trợ ghi dùng thương mại được, không bắt buộc ghi công. **Chưa kiểm** trang Kenney RPG Audio và trang OpenGameArt của trazzz123. Cho tới khi tệp bằng chứng có trong repo, dòng đó ghi `chưa kiểm chứng` kèm ghi chú "nguồn khai CC0".

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

Hệ quả: (1) manifest ghi `chưa kiểm chứng` cho cả 14 dòng; (2) danh sách ở AC7 gồm 14 clip này và mọi clip dẫn xuất; (3) `TimeOver.wav` nên được thay trước phát hành công khai (Q5), chưa có clip thay thế; (4) bản chép trong `Placeholder/JSurviv/` và bản dẫn xuất trong `Sfx/` đều mang cùng trạng thái cho tới khi có bằng chứng.

### 2.13. Thư mục `Placeholder` và việc đưa clip lên `Sfx/`: có cần đổi catalog không

Đã kiểm code và dữ liệu (27/10):

- **Catalog tham chiếu clip bằng GUID** (`clips: {fileID: 8300000, guid: …}`), không bằng đường dẫn. Tên thư mục `Placeholder` **không** xuất hiện trong `SfxCatalog.asset` hay `AudioRuntimeSet.asset`. Không có mã runtime nào (`Scripts/**`) dùng chuỗi `Placeholder` cho âm thanh.
- **Hai nơi phụ thuộc đường dẫn:** `Tests/EditMode/Audio/SfxCatalogTests.cs:64` lọc bíp theo `"/Placeholder/Beeps/"` (test `BeepsHaveDistinctFrequencies`), và hai script `Tools/Audio/copy_jsurviv_placeholders.py` (`TARGET`) và `make_placeholder_sfx.py` (`DEFAULT_OUT`) ghi/kiểm ở `Placeholder/…`.

Kết luận và quyết định (Q7):

1. **Không đổi tên thư mục `Placeholder`, không "chuyển" file.** Clip cuối là **file dẫn xuất mới** (mono, cắt, chuẩn hóa) trong `Sfx/`, nên không có phép "thăng cấp" nguyên file. Đổi tên/di chuyển trong Unity giữ GUID nên catalog không đổi, nhưng sẽ làm test ở dòng 64 và hai script sai đường dẫn.
2. **Catalog có thay đổi, nhưng chỉ là dữ liệu cho phép:** `clips` của mã đó trỏ sang GUID file mới trong `Sfx/` (đúng thao tác 2.5, bước 4). Không thêm cột, không đổi mã.
3. **Bíp:** theo hướng nguồn đã chọn (2.11) cả 14 mã bíp đổi sang clip CC0 trong `Sfx/`, nên sau ART-05 **không còn mã nào tham chiếu `Placeholder/Beeps/`** và test `SfxCatalogTests` đổi kỳ vọng "14 bíp" thành 0 (AC12). Nếu một clip CC0 không đạt kiểm (độ dài, mức, nghe), mã đó tạm trỏ lại bíp và ghi vào báo cáo; khi đó kỳ vọng của test tính theo số mã còn dùng bíp.
4. Sau G3: xóa các clip `Placeholder/JSurviv/*` và `Placeholder/Beeps/*` không còn được tham chiếu (commit riêng). Hai script `Tools/Audio/copy_jsurviv_placeholders.py` và `make_placeholder_sfx.py` ghi cố định vào `Placeholder/`: nếu xóa file thì ghi chú vào `Tools/Audio/README.md`; `--check` sẽ báo thiếu bản chép, đó là hệ quả mong đợi.

### 2.14. Việc còn mở sau khi chọn hướng nguồn (CPO 27/10) và AC tương ứng

| Việc còn mở | AC | Ghi chú |
| --- | --- | --- |
| Bằng chứng giấy phép cho 12 clip JSurviv đang dùng (và 2 bài nhạc) | **AC38, AC42** | Ghi nhận trạng thái; chỉ người dùng cung cấp được bằng chứng. `TimeOver.wav` rủi ro cao nhất |
| Chuẩn hóa mức cho 12 clip JSurviv cho khớp 14 clip mới | **AC40** (cùng AC18, AC19, AC20) | Cùng quy tắc L và đỉnh; đo trước/sau |
| Nén `Waiting Room Music.wav` (25,7 MB) | **AC29, AC30, AC31** | ≤ 3,0 MB; thay tham chiếu tạm `loading_music` |
| Cắt `bomb_fuse` 11,8 s (và `bomb nổ` 5,4 s) | **AC17, AC27** | `BombFuse` 0,8–1,5 s, `Explosion` ≤ 3,0 s; P0 cho độ dài (AC17) |
| BGM mặc định tắt | **AC32** (cùng AC33) | Giữ như AUD-01; bật qua F6 |
| Nghe thử bằng panel F6 (AC-A28 của AUD-01) | **AC36** | PIC nghe 11 mã ★ và các clip CC0 mới; không máy nào thay được |
| Nhập 14 clip CC0 vào repo, truy nguồn | **AC41** (cùng AC9, AC10, AC12, AC39) | Chờ QA trả Unity Editor |

## 3. Tiêu chí nghiệm thu

Ưu tiên: **P0** phải đạt để qua G3; **P1** nên đạt, bỏ phải có PIC đồng ý; **P2** không chặn. Số đánh dấu giả định (G-1…G-12) có thể đổi ở G1. "Ưu tiên ★" là 11 mã ở bảng 2.2. Mọi AC kiểm bằng dữ liệu, test dữ liệu và panel F6 của AUD-01, không cần gameplay thật. Mọi chỗ ghi "người nghe" là PIC nghe, không máy kiểm được.

### 3.1. Kiểm kê và đối chiếu (tiêu chí GDD 1)

| # | Tiêu chí (đo được) | Cách kiểm | Ưu tiên |
| --- | --- | --- | --- |
| AC1 | Manifest `Docs/Tickets/ART-05_audio_manifest.csv` có **đúng 26 dòng SFX** khớp enum `SfxId` (cùng tên, cùng thứ tự 1–26, không thiếu, không thừa, không trùng). Mỗi dòng có đúng một trạng thái trong {Có, Cần chỉnh, Thiếu} | Test dữ liệu EditMode đọc CSV và `Enum.GetNames(typeof(SfxId))` | P0 |
| AC2 | Mỗi mã không phải "Thiếu" ghi đủ: file nguồn, độ dài (s, 2 chữ số), codec, tần số, kênh, kích thước (byte). Chạy `Tools/Audio/measure_clips.py` hoặc đo lại trên Unity cho kết quả khớp: độ dài ± 0,01 s, kích thước đúng từng byte. Tổng Có + Cần chỉnh + Thiếu = 26. Số hiện có ở bảng 2.2 là mốc so sánh | Script + review CSV | P0 |
| AC3 | Mỗi trong **21 file** của `Assets/Imported/JSurviv/Sound` và mỗi **thành viên của hai zip** (8 + 1) có đúng 1 dòng manifest với phân loại: mã SFX (26) / BGM / UI / ngoài 26 / không dùng. Số dòng "nguồn JSurviv" = 21 + 9 = 30. Không file nào bị bỏ sót | Test đếm `Directory` + CSV | P0 |
| AC4 | Cột giấy phép của **mọi** dòng chỉ nhận một trong ba giá trị ở 2.9. Mọi dòng nguồn JSurviv (và clip dẫn xuất) ghi `chưa kiểm chứng`; dòng `có bằng chứng` phải có tệp ở `Docs/Licenses/Audio/` (đường dẫn tồn tại); không dòng nào chứa "miễn phí", "CC0", "royalty-free", "được phép" mà thiếu đường dẫn bằng chứng. Ghi dấu vết `hollywoodedge.com` của `TimeOver.wav` vào cột ghi chú nguồn | Test CSV + review | P0 |
| AC5 | Hai file `.zip` được liệt kê nội dung đủ (8 + 1 thành viên), tên zip sai lệch được ghi (M5). Không file zip nào được tham chiếu trong catalog. Phần giải nén nằm ở `Sfx/`/thư mục tạm, **không** ở `Assets/Imported/` | Review + `git diff` | P0 |
| AC6 | Mỗi mã "Có" hoặc "Cần chỉnh" có cột "Đã nghe": `có` (PIC hoặc người được chỉ định nghe qua F6, ghi tên và ngày) hoặc `chưa`. Dòng `chưa` coi là **tạm**; báo cáo cuối ghi số mã đã nghe / tổng. Một mã đã nghe và **không khớp** sự kiện thì đổi trạng thái thành Thiếu (hoặc Cần chỉnh) và ghi lý do | Review CSV | P1 |
| AC7 | Báo cáo cuối có **danh sách clip chưa kiểm chứng** (đường dẫn, mã SFX dùng nó) và danh sách "Thiếu" kèm nguồn tạm (bíp tự sinh); số liệu khớp manifest | Review | P1 |

### 3.2. Đưa clip vào catalog bằng dữ liệu (tiêu chí GDD 1–2)

| # | Tiêu chí (đo được) | Cách kiểm | Ưu tiên |
| --- | --- | --- | --- |
| AC8 | Clip cuối nằm trong `Assets/_JungleMaze/Art/Audio/Sfx/` (SFX) và `Music/` (nhạc); tên chỉ gồm `[a-z0-9_]`, đuôi `.wav`/`.ogg`/`.mp3`, không dấu, không khoảng trắng, duy nhất. SFX theo dạng `<sfx_snake>_<NN>.wav`, tối đa **5 biến thể** mỗi mã (hướng nguồn đã chọn có 5 biến thể cho `FootstepGrass`, `FootstepSwamp`) | Test đường dẫn + review | P1 |
| AC9 | Không clip nào trong `SfxCatalog.asset` và `AudioRuntimeSet.asset` có GUID thuộc `Assets/Imported/**`; GUID của file trong `Sfx/` và `Music/` **không trùng** GUID nào trong `Assets/Imported/**` (kiểm bằng tập `.meta`). `git diff` của `Assets/Imported/**` **trống** (kể cả `.meta`) | Test dữ liệu + `git diff -- Assets/Imported` | P0 |
| AC10 | **Cả 26 mã** trong catalog có ≥ 1 clip nằm trong `Sfx/`, **không mã nào còn trỏ vào `Placeholder/`** (kể cả 14 mã từng là bíp và 12 mã JSurviv). Nguồn của mỗi clip ghi trong manifest (JSurviv `chưa kiểm chứng` / nguồn CC0 / `tự tạo`). Đếm bíp còn được tham chiếu = **0** | Test dữ liệu đọc catalog | P0 cho 11 mã ★, P1 cho 15 mã còn lại |
| AC11 | Mã nguồn **không đổi**: diff của toàn bộ công việc ART-05 không có tệp `.cs` nào trong `Assets/_JungleMaze/Scripts/**`, không có `Bootstrap.cs`, scene, prefab, `ProjectSettings/**`, `Backend/**`. Chỉ các đường dẫn trong "Vùng file của ART-05". Cột `priority`, `bus`, `rangeCells`, `maxInstances`, `cooldownSeconds`, `repeatCount`, `repeatIntervalSeconds` của 26 mục catalog giữ nguyên (diff YAML chỉ chạm `clips`, `volume`, `pitchJitter`), trừ ngoại lệ ZoneBell3 ở AC28 | Review diff + `git diff --stat` | P0 |
| AC12 | Sau khi thay clip, **bộ test Audio của AUD-01 xanh nguyên**: EditMode `JungleMaze.Tests.Audio.*` (59/59 khi G3) và PlayMode `AudioAcceptanceTests` (15/15 khi G3). Console 0 error, 0 warning, đặc biệt không có "Missing AudioClip" và không có cảnh báo một-lần-mỗi-mã của `SfxPlaybackService`. **Ngoại lệ duy nhất được sửa**: kỳ vọng "14 bíp" trong `SfxCatalogTests.cs` (lọc `/Placeholder/Beeps/`, test `BeepsHaveDistinctFrequencies`) đổi thành 0; ghi rõ dòng nào đổi và lý do trong báo cáo. Test khác fail vì cố định sẵn tên clip tạm thì ghi nguyên nhân và báo CPO, không sửa logic | Chạy test qua Unity MCP + review diff test | P0 |
| AC13 | Các mã cần phân biệt dùng **clip khác nhau đôi một**: `HitMarker`, `HurtLocal`, `ZoneBell3`, `DragonRoar`, `MatchEnd`, `Explosion`; `ShootRanged` ≠ `ShootCrossbow`; `MeleeSwing` ≠ `MeleeHit`; `PickupWeapon`, `PickupItem`, `WearArmor` đôi một khác nhau. Không dùng bản hạ cao độ của cùng một file làm hai mã | Test dữ liệu (so tham chiếu clip) | P0 |
| AC14 | **Không phát trùng và âm lượng hoạt động với clip cuối**: với catalog đã thay, PlayMode của AUD-01 cho kết quả: "Gửi lặp ×5" → ≤ 1 voice mới (A13); `SetVolume(Master|SFX|Music, 0,5)` → tham số mixer −6,02 dB ± 0,1 (A5); spam 100 → ≤ 16 voice SFX (A3). Số voice nhạc ≤ 2 | PlayMode AUD-01 + panel F6 | P0 |
| AC15 | Thay đổi **đảo ngược được bằng dữ liệu**: đổi tham chiếu của một mã ưu tiên về clip trong `Placeholder/` (chỉ sửa asset; bíp/bản chép còn tới khi dọn, Q7) thì test Audio vẫn xanh (trừ kỳ vọng "0 bíp" ở AC12 khi đang đảo ngược), và diff của lần đổi không có `.cs` | Thử một mã trên bản sao tạm | P2 |

### 3.3. Chất lượng clip, độ dài, mức âm (tiêu chí GDD 2–3)

| # | Tiêu chí (đo được) | Cách kiểm | Ưu tiên |
| --- | --- | --- | --- |
| AC16 | **Import settings** của mọi clip cuối theo 2.7: SFX Decompress On Load + ADPCM/PCM, `forceToMono` tắt, `normalize` tắt, mono thật (1 kênh sau import); nhạc Streaming + Vorbis. **Xác nhận bằng đo** rằng cờ `normalize` không đưa mức về 0 dBFS: đỉnh đo bằng `GetData` của clip đã import khớp đỉnh đo trên WAV nguồn ± 0,5 dB | Test EditMode đọc `AudioImporter` + `GetData` + script | P0 |
| AC17 | **Độ dài** (đo `AudioClip.length`) của mọi clip cuối nằm trong khoảng đích ở bảng 2.3. Riêng 4 mã bắt buộc: `BombFuse` 0,80–1,50 s; `Explosion` ≤ 3,00 s; `HitMarker` ≤ 0,15 s; `DragonRoar` ≥ 1,50 s | Test dữ liệu đọc catalog | P0 cho mã ★ và `BombFuse`, `Explosion`; P1 phần còn lại |
| AC18 | **Đỉnh và chống click:** mọi clip SFX cuối có đỉnh **≤ −1,0 dBFS**, không chuỗi ≥ 3 mẫu chạm trần, |x| ≤ −40 dBFS ở 2 ms đầu và 2 ms cuối | Script + test `GetData` | P0 (mã ★), P1 (còn lại) |
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
| AC29 | Ba bài có bản cuối trong `Music/`, **mỗi file ≤ 3,0 MB**, độ dài mỗi bài lệch ≤ 0,1 s so với bản gốc (145,53 / 43,65 / 123,48 s). **Không** có file ≥ 5 MB nào trong `Assets/_JungleMaze/` do ART-05 thêm. `Waiting Room Music.wav` (25,7 MB) không được chép | Test kích thước + độ dài | P1 |
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
| AC39 | **Đưa lên `Sfx/` không đổi mã và không làm hỏng công cụ cũ:** (a) `SfxCatalog.asset` đổi chỉ ở `clips` (GUID mới trong `Sfx/`) cộng `volume`/`pitchJitter`, không đổi đường dẫn thư mục `Placeholder`; (b) sau thay, test `SfxCatalogTests` xanh với kỳ vọng mới (0 bíp, AC12) và `copy_jsurviv_placeholders.py --check` vẫn đạt khi bản chép còn tồn tại; (c) mỗi mã trong bảng 2.11(b) có đúng một dòng trong manifest với nguồn đã chọn (clip CC0 có URL/tác giả/ngày tải, hoặc `tự tạo`, hoặc `giữ bíp` kèm lý do); không mã nào ghi nguồn "tự do" mà thiếu tệp bằng chứng | Test + review diff | P1 |
| AC40 | **Mức âm nướng sẵn và đợt chuẩn hóa cho 12 clip JSurviv:** mọi SFX cuối đo theo 2.6: mức hoạt động L = **−24 ± 2 dBFS** cho `FootstepGrass`, `FootstepSwamp`; **−20…−15 dBFS** cho mọi mã khác; đỉnh ≤ −1,0 dBFS. Điều này áp cho **cả 14 clip CC0 mới và 12 clip JSurviv dẫn xuất** (4, 5, 6, 11, 12, 15, 18, 19, 20, 21, 22, 26), để không clip cũ nào to hay nhỏ lệch hẳn clip mới. Manifest ghi L và đỉnh **trước và sau** xử lý của từng clip | Test dữ liệu (`GetData`) + script | P0 |
| AC41 | **Truy nguồn 14 clip tải về:** mỗi clip CC0 trong `Sfx/` có đúng một dòng trong `ART05_Sources.vi.md` và manifest: URL trang nguồn, tên file gốc, tác giả, ngày tải, đoạn cắt, độ lợi, giấy phép. Số clip trong `Sfx/` có nguồn tải về = số dòng nguồn (khớp một-một, không clip mồ côi, không dòng không clip). Giấy phép chỉ là `có bằng chứng: <đường dẫn>` khi tệp ở `Docs/Licenses/Audio/` tồn tại; nếu chưa có thì `chưa kiểm chứng` kèm "nguồn khai CC0" | Test CSV + review | P0 |
| AC42 | **Bằng chứng giấy phép của 12 clip JSurviv đang dùng (và 2 bài nhạc):** mỗi trong 14 mục có đúng một trạng thái: `có bằng chứng: <đường dẫn>` / `chưa có: cần thay` / `chưa có: giữ tạm, người dùng đồng ý` (ghi ngày). Báo cáo cuối nêu số mục mỗi loại và mã SFX bị ảnh hưởng. **ART-05 không tự xác lập giấy phép**: không có bằng chứng thì không đổi trạng thái; `TimeOver.wav` (thẻ hollywoodedge.com) luôn nằm trong danh sách cần quyết định | Review manifest + báo cáo | P0 (ghi nhận), việc có bằng chứng thật do người dùng |

**Ánh xạ tiêu chí GDD:**

| Tiêu chí GDD | AC |
| --- | --- |
| 1. Bảng 26 SFX đối chiếu file thật, trạng thái dùng được/thiếu, nguồn và tình trạng quyền sử dụng; không khẳng định license khi chưa kiểm chứng | AC1–AC7, AC38, AC41, AC42 |
| 2. Event ưu tiên phát đúng clip, Master/SFX (và Music nếu bật) có hiệu lực, không lặp audio khi event gửi lại | AC8–AC15, AC16–AC28, AC39, AC40 (đặc biệt AC14, AC20–AC26) |
| 3. BGM chỉ tích hợp khi được duyệt; không nhạc chồng nhau/che cảnh báo; không missing AudioClip | AC29–AC34 |
| Ranh giới, dung lượng, nghe thử (bảo vệ ba tiêu chí trên) | AC11, AC12, AC35–AC37 |

## Câu hỏi và giả định chờ G1

**Giả định** (đã đưa vào các bảng và AC; mặc định đang theo):

- **G-1** Trạng thái theo quy tắc ở chú thích bảng 2.2; chưa ai nghe nên mọi trạng thái là **tạm** cho tới khi AC6.
- **G-2** Danh sách 26 mã và thứ tự theo `SfxId` của AUD-01; không thêm/bớt mã trong ART-05.
- **G-3** Clip cuối ở `Assets/_JungleMaze/Art/Audio/Sfx/` (SFX) và `Music/` (nhạc), đích do CPO chọn 27/10 (Q1); tên `<sfx_snake>_<NN>.wav`; nhạc `Music/`; manifest `Docs/Tickets/ART-05_audio_manifest.csv`; bằng chứng giấy phép ở `Docs/Licenses/Audio/`.
- **G-4** SFX cuối là WAV 16-bit 44,1 kHz **mono đã bake** (Q2); lý do: đo được bằng thư viện chuẩn, không mất chất lượng qua hai lần nén, và AUD-01 đã ép mono.
- **G-5** Đỉnh ≤ −1,0 dBFS (CPO 27/10); không chạm trần; fade 2–5 ms hai đầu; |x| ≤ −40 dBFS ở 2 ms đầu/cuối.
- **G-6** Mức nướng sẵn L: bước chân −24 ± 2, SFX khác −20…−15 dBFS (CPO 27/10). Tầng mức hiệu dụng E (sau `volume`): T1 −16, T2 −20, T3 −24, T4 −29 dBFS ± 3 dB; BGM −26 ± 2 dBFS; HitMarker ≥ mọi T2–T4 + 3 dB và ≥ FootstepGrass + 12 dB (Q6).
- **G-7** Cách đo: trộn mono; mức hoạt động = RMS giữa mẫu đầu và mẫu cuối có |x| ≥ 0,003; E = L + 20·log10(`volume`).
- **G-8** Khoảng độ dài đích của 26 mã theo bảng 2.3 (cơ sở: cooldown/số bản tối đa của catalog, ngòi bom 1,0–1,5 s, thời gian dùng đồ 0,5–3,5 s, pool 16 voice).
- **G-9** Ngân sách dung lượng: ≤ 3,0 MB mỗi file nhạc, ≤ 0,5 MB mỗi clip SFX, ≤ 12 MB tổng file mới.
- **G-10** Import settings theo 2.7 (SFX Decompress On Load/ADPCM, `forceToMono` và `normalize` tắt; nhạc Streaming + Vorbis 0,7).
- **G-11** Từ vựng giấy phép: `chưa kiểm chứng` / `tự tạo (…)` / `có bằng chứng: <đường dẫn>`. Không bằng chứng thì không viết gì khác.
- **G-12** (đã chốt, CPO 27/10) Cả 14 mã đang là bíp (2.11) được thay bằng clip CC0 tải từ thư viện tự do, nguồn ghi đủ; không còn mã nào chờ "có thể giữ bíp", trừ khi một clip không đạt kiểm.

| # | Câu hỏi | Mặc định / khuyến nghị |
| --- | --- | --- |
| **Q1** | **Clip cuối nằm đâu**: sao chép/chuyển đổi vào `Art/Audio/Sfx/` hay tham chiếu thẳng `Assets/Imported/JSurviv/Sound`? | **Khuyến nghị (CPO đã chọn đích `Sfx/`): sao chép/chuyển đổi vào `Sfx/`** (2.4): import settings nằm trong `.meta`, phải cắt/chuẩn hóa, giấy phép chưa rõ có thể phải bỏ từng file, vùng nhập không sửa được |
| **Q2** | **Định dạng SFX cuối**: WAV 16-bit mono đã bake (đo được bằng thư viện chuẩn, không mất chất lượng) hay giữ MP3? `MatchEnd` giữ stereo hay mono? | **Khuyến nghị: WAV 16-bit 44,1 kHz mono** cho mọi SFX, kể cả `MatchEnd` (AUD-01 đã ép mono, `spatialBlend = 0`). Nhạc giữ stereo |
| **Q3** | **Nguồn cho 14 mã bíp:** *đã quyết* theo chỉ đạo của người dùng ("thiếu cái gì thì tự tìm thư viện rồi tự bổ sung") qua CPO 27/10: clip CC0 của Kenney (Impact, Interface, RPG Audio) và OpenGameArt (rồng), bảng ở 2.11 | **Đã chốt**, không còn là câu hỏi mở. Còn mở: tải/nhập vào repo chờ QA trả Unity Editor; xác nhận bằng chứng CC0 cho RPG Audio và trazzz123 |
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
- **R2 · Chỉ 4/26 mã có file JSurviv dùng được ngay.** 14 mã bíp sẽ được thay bằng clip CC0 (2.11), nhưng clip chưa nằm trong repo và chưa ai nghe: tên clip, độ dài và hợp ngữ cảnh chưa kiểm (ví dụ `impactBell_heavy` có thể dài hơn 0,75 s, `glass_005` hơn 0,15 s, `pluck_001` có thể không giống nỏ). AC17, AC40 và AC36 bắt buộc đo/nghe; clip không đạt thì cắt lại hoặc chọn clip khác trong cùng gói.
- **R3 · Import `normalize` + `forceToMono` của AUD-01** (`normalize: 1`, `forceToMono: 1` ở mọi clip tạm) có thể chuẩn hóa mọi clip về đỉnh ~0 dBFS khi import, làm mất tác dụng của việc chuẩn hóa trong file. Chưa đo trong Unity ở ticket này. AC16 bắt buộc đo `GetData` sau import; nếu đúng thì ART-05 tắt `normalize` cho clip cuối (dữ liệu).
- **R4 · Không có bộ giải mã trên máy** (`ffmpeg`, `sox` không có): chưa đo được mức của 14 file MP3. Đo nhạc phải qua bản WAV hoặc `GetData` trong Unity (QA đang giữ Unity Editor). Ảnh hưởng thời gian.
- **R5 · Không ai nghe được bằng máy.** Mọi trạng thái Có/Cần chỉnh là theo tên và độ dài; có thể sai (ví dụ `bleed.mp3` chưa chắc là tiếng trúng đòn, `bot di chuyển.mp3` chưa chắc là bước chân).
- **R6 · Test AUD-01 cố định sẵn clip tạm.** `SfxCatalogTests.cs:64` lọc bíp theo đường dẫn `/Placeholder/Beeps/` (test `BeepsHaveDistinctFrequencies`), và hai script `Tools/Audio` ghi cố định vào `Placeholder/` (2.13); thay clip có thể làm fail. ART-05 không sửa logic test của AUD-01 (AC12), chỉ báo CPO.
- **R7 · Clip dùng đồ dài hơn thời gian dùng.** Món dùng 0,5 s mà clip 1,5 s vẫn kêu sau khi dùng xong, và router chưa dừng `UseHeal`/`UseDrink` khi `UseCancel`. Cắt ≤ 1,5 s giảm vấn đề nhưng không xóa; dừng clip khi hủy là việc của AUD-01 (ticket sau), không phải ART-05.
- **R8 · Một clip chung cho nhiều vũ khí.** `ShootRanged` dùng một file cho Ná cao su, Quyền trượng và Shotgun (nhịp bắn 0,8–3 phát/s); người chơi không phân biệt được vũ khí bằng tai. Biến thể riêng là mở rộng (Q8).
- **R9 · Bằng chứng CC0 mới chỉ do CPO khai.** `Docs/Art/Audio/ART05_Sources.vi.md` chưa có trong repo; tôi chỉ kiểm được hai trang Kenney (Impact, Interface) ngày 27/10. Gói Kenney RPG Audio và clip rồng của trazzz123 (OpenGameArt) **chưa kiểm**; clip rồng phụ thuộc điều khoản trang đó (cần bản chụp). Không có bản chụp thì giấy phép ghi `chưa kiểm chứng`.
- **R10 · Hai thư mục chung.** `Docs/Art/**` ghi là vùng của Codex trong `Agent_Coordination.vi.md`; thêm `Docs/Art/Audio/` cần một dòng ở mục 4. `Art/Audio/Sfx/` và `Music/` nằm trong vùng AUD-01/ART-05 (cùng PIC).

## 4. Kế hoạch kỹ thuật

*Tech Lead, 27/10/2026, đọc repo tại `2d9d2fa3`. Số đo ở mục này do tôi đo lại bằng script thư viện chuẩn Python trên WAV trong repo, theo định nghĩa G-7 (trộn mono, L bỏ im lặng dưới −50 dBFS hai đầu). Chưa chạy Unity. ART-05 chỉ đổi dữ liệu, asset, script `Tools/Audio` và test dữ liệu: **không đổi protocol, `match.json`, `Scripts/**`**.*

### 4.0. Kiểm phần CPO đã làm (`3f32e1c4`) so với AC

**Đã đạt:**

- 25 WAV mono, 16-bit, 44,1 kHz trong `Sfx/`; 14 mã bíp trỏ sang; còn 0 bíp (AC12, chỉ đổi đúng dòng được phép trong `SfxCatalogTests.cs`).
- Diff catalog chỉ đổi GUID trong `clips` (AC11); không chạm `Assets/Imported` (AC9, phần diff).
- Import của cả 25 clip: Decompress On Load, ADPCM, `forceToMono: 0`, `normalize: 0` (AC16, phần cài đặt).
- Đỉnh ≤ −1,0 dBFS, không chạm trần (AC18, phần đỉnh). Bước chân L −23,1…−23,7 dBFS (AC40).
- `HitMarker` 0,064 s, `ZoneBell3` 0,75 s, `DragonRoar` 3,0 s nằm trong khoảng đích (AC17).

**Khoảng trống** (sửa được hoàn toàn bằng dữ liệu):

| # | Khoảng trống | Clip | AC · mức |
| --- | --- | --- | --- |
| K1 | **Không fade đầu**: \|x\| trong 2 ms đầu > −40 dBFS (cắt đúng điểm bắt đầu tiếng) | 20/25 clip. Tệ nhất ≈ −1 dBFS: `hit_marker`, `shoot_crossbow`, `zone_bell`, `armor_absorb_0/1/2`, `trap_triggered`; còn lại: 10 bước chân, `use_cancel`, `throw`, `empty_click` | AC18 · **P0** cho 3 mã ★ (HitMarker, ShootCrossbow, ZoneBell3) |
| K2 | Fade cuối chưa đủ | `eliminated` −38,7 dBFS | AC18 · P1 |
| K3 | Độ dài ngoài khoảng 2.3 | `shoot_crossbow` **0,082 s** (< 0,15) ★; `reload` 0,177 (< 0,20); `use_cancel` 0,059 (< 0,10); `empty_click` 0,358 (> 0,30); `armor_absorb_0/1/2` 0,49–0,59 (> 0,40) | AC17, AC22 · **P0** cho ShootCrossbow, P1 còn lại |
| K4 | Mức L ngoài −20…−15 dBFS | `zone_bell` **−23,2** ★; `shoot_crossbow` −20,5 ★; `armor_absorb` −23,7…−24,1; `empty_click` −26,5; `bush_enter` −21,9/−22,6; `reload` −20,9; `trap_triggered` −20,4. Nguyên nhân: tiếng va/chuông có hệ số đỉnh cao (chuông 22 dB), giữ đỉnh −1 dBFS thì RMS không lên được −20 nếu không có limiter | AC40 · **P0** |
| K5 | Mức hiệu dụng E sai tầng (`volume` chưa chỉnh theo clip mới) | `ZoneBell3` −23,2 (T1 cần ≥ −19); `Eliminated` −16,9 (T2 cần ≤ −17: to quá, làm `HitMarker` −16,7 hụt lề +3 dB của AC20); `Throw` −24,2, `TrapTriggered` −23,5, `ArmorAbsorb` −25,8 (T2 cần ≥ −23); `EmptyClick` −32,5 (T3 cần ≥ −27) | AC19, AC20 · **P0** |
| K6 | Bằng chứng giấy phép thiếu và sai chỗ | `LICENSE_Kenney_CC0.txt` là license của **riêng gói Impact Sounds** (15 clip). Thiếu tệp của Interface Sounds (4 clip), RPG Audio (5 clip) và bản chụp trang OpenGameArt (rồng). Tệp nằm ở `Docs/Art/Audio/`, spec quy định `Docs/Licenses/Audio/` | AC4, AC41 · **P0** |
| K7 | `ART05_Sources.vi.md` thiếu cột tác giả, ngày tải, đoạn cắt, độ lợi, trạng thái giấy phép theo G-11; câu "Tất cả file … là CC0 1.0" khẳng định khi 10/25 clip chưa có tệp bằng chứng | AC41, G-11 · **P0** |
| K8 | Chưa có manifest `Docs/Tickets/ART-05_audio_manifest.csv` | AC1–AC4, AC6, AC38, AC40 · **P0** |
| K9 | Tên `<sfx_snake>.wav` / `<sfx_snake>_<n>.wav` đánh số từ 0, khác dạng `<sfx_snake>_<NN>.wav` của AC8 | AC8 · P1, xem T-4 |
| K10 | "EditMode 456/456" chưa phủ AC16–AC20, AC40: chưa có test đo mức bằng `GetData` | — · đưa vào 4.4 |

Bảng E hiện tại (E = L + 20·log10(`volume`)): BushEnter −28,3 · ShootCrossbow −23,0 · Throw −24,2 · Reload −26,9 · EmptyClick −32,5 · TrapTriggered −23,5 · HitMarker −16,7 · ArmorAbsorb −25,8 · Eliminated −16,9 · UseCancel −24,1 · ZoneBell3 −23,2 · DragonRoar −15,0 · FootstepGrass −31,5 · FootstepSwamp −30,4.

**Kết luận:** CPO làm đúng hướng và đúng ranh giới, nhưng **3 mã ★ chưa qua AC P0** (HitMarker và ZoneBell3 thiếu fade, ZoneBell3 nhỏ quá tầng, ShootCrossbow ngắn quá). Sửa bằng cách xử lý lại và **ghi đè đúng tên file**: `.meta` giữ nguyên nên GUID giữ nguyên, `clips` trong catalog không đổi, chỉ đổi `volume`.

### 4.1. Hướng làm: hai giai đoạn

**Giai đoạn A (Python, không cần Unity Editor).** Mọi file gốc chỉ đọc. Kết quả ghi ra thư mục tạm **ngoài `Assets/`** (`%TEMP%/art05_stage/`, không commit). Không thả file vào `Assets/` khi phiên khác đang giữ Editor: Unity sẽ tự sinh `.meta` mặc định (`normalize: 1`) mà không ai sở hữu (TR7).

1. **Giải mã** MP3/OGG/zip ra WAV trong thư mục làm việc tạm. Zip bằng `zipfile`. MP3/OGG bằng ffmpeg nếu được duyệt (T-1). Nếu không có ffmpeg: unity-dev chạy một đoạn C# một lần qua Unity MCP (`AudioClip.GetData` trên bản chép `Placeholder/JSurviv/*.mp3`, ghi WAV ra thư mục tạm, **không commit mã**). Lưu ý: bản chép đang `normalize: 1`, nên số "trước xử lý" ghi trong manifest là số sau import; ghi rõ.
2. **Công thức** `Tools/Audio/art05_recipe.csv`, một dòng cho mỗi file cuối: `out_file, sfx_id|music, source_kind (jsurviv|download), source_path_or_url, zip_member, author, download_date, licence, trim_start_s, trim_end_s, fade_in_ms, fade_out_ms, target_L_dbfs, heard, notes`. Đây là nguồn duy nhất cho xử lý và manifest.
3. **`Tools/Audio/process_clips.py`** (thư viện chuẩn), chạy lại ra file giống từng byte:
   - Trộn mono, rồi đổi về 44,1 kHz bằng sinc có cửa sổ nếu nguồn khác (`súng bắn`, `countdown` là 24 kHz). Không "làm sáng" âm (M8).
   - Cắt theo công thức.
   - **Thêm 2 ms im lặng ở đầu và fade-in cosine ≥ 2 ms.** Quy tắc "\|x\| ≤ −40 dBFS trong 2 ms đầu" của AC18 chỉ đạt được theo cách này: fade-in 2 ms thôi thì mẫu cuối của 2 ms đầu đã gần đỉnh. Trễ thêm 2 ms, dưới 1/8 khung hình, không nghe thấy.
   - Fade-out cosine ≥ 5 ms (bom, nổ, dùng đồ: 50–300 ms).
   - Đưa L về đích (bảng dưới) rồi qua **limiter nhìn trước**: trần −1,0 dBFS, attack 1 ms, release 50 ms, giảm tối đa 6 dB. Lặp tối đa 3 lần cho tới khi L đúng đích ± 0,3 dB và đỉnh ≤ −1,0 dBFS.
   - Ghi WAV 16-bit 44,1 kHz mono.
4. **`Tools/Audio/measure_clips.py`** (tên theo spec 2.6): đo đỉnh, L, \|x\| tối đa trong 2 ms đầu/cuối, chuỗi chạm trần, độ dài, tần số, kênh, kích thước; có `--csv`.
5. **`Tools/Audio/build_manifest.py`**: sinh `Docs/Tickets/ART-05_audio_manifest.csv` từ công thức, số đo trước/sau và `Tools/Audio/art05_jsurviv_inventory.csv`. Bảng kiểm kê này có 30 dòng (21 file + 9 thành viên zip), điền tay từ số đã đo ở 2.2/2.12, kèm md5 và dấu vết metadata. Manifest có cột `row_type` ∈ {`sfx` (26 dòng theo thứ tự enum), `jsurviv` (30), `clip` (một dòng cho mỗi file trong `Sfx/` và `Music/`)}.

**Giai đoạn B (Unity Editor, unity-dev giữ Editor và ghi một dòng ở `Agent_Coordination.vi.md` mục 4).**

1. Chép WAV từ thư mục tạm vào `Sfx/`: ghi đè 25 file cũ, thêm 15 file mới.
2. Đặt importer bằng API `AudioImporter` qua MCP theo 2.7, rồi `SaveAndReimport`.
3. Sửa `clips` và `volume` trong catalog bằng `SerializedObject`. Không chạm cột khác.
4. Nhạc: chép vào `Music/` (Streaming, Vorbis 0,7, `loadInBackground` bật, `normalize` tắt) và trỏ `AudioRuntimeSet`.
5. Chạy test.

**Đích mức âm** (đáp ứng G-6, AC19, AC20, AC40 và test A19 sẵn có `HitMarker.volume ≥ 2 × FootstepGrass.volume`):

| Tầng (E đích) | Mã | L nướng trong file | `volume` = 10^((E − L)/20) |
| --- | --- | --- | --- |
| T1 (−16) | HitMarker, HurtLocal, ZoneBell3, DragonRoar, MatchEnd | −16 (riêng ZoneBell3 cho phép tới −18 nếu limiter phải giảm > 6 dB) | 1,00 |
| T2 (−20) | MeleeSwing, MeleeHit, ShootRanged, ShootCrossbow, Throw, BombFuse, Explosion, TrapTriggered, ArmorAbsorb, Eliminated | −18 | 0,79 |
| T3 (−24) | Reload, EmptyClick, PickupWeapon, PickupItem, WearArmor, UseHeal, UseDrink, UseCancel | −18 | 0,50 |
| T4 (−29) | BushEnter | −18 | 0,28 |
| T4 (−29) | FootstepGrass, FootstepSwamp | −24 | 0,45 (E −30,9; ≤ 0,5 để A19 còn xanh) |

Kiểm nhanh: E(HitMarker) −16 so với T2 cao nhất −20 → lề 4 dB (AC20 cần ≥ 3); so với bước chân −30,9 → lề 14,9 dB (cần ≥ 12). Các biến thể của cùng một mã có cùng L ± 0,5 dB vì `volume` là một số cho cả mã. `volume` cuối tính từ L đo thật sau xử lý và ghi trong manifest.

### 4.2. Công thức từng clip

**(a) Làm lại 25 clip CC0** (ghi đè, giữ `.meta`): mọi clip thêm 2 ms đầu + fade (K1) và đưa về L đích (K4). Riêng:

| Clip | Việc thêm |
| --- | --- |
| `shoot_crossbow.wav` | Cắt lại từ file gốc, giữ đuôi tự nhiên tới ≥ 0,15 s. Nếu `pluck_001` gốc ngắn hơn 0,15 s: chọn clip khác trong 3 gói Kenney (T-3). **Không** đệm im lặng để qua test |
| `reload.wav`, `use_cancel.wav` | Cắt lại từ gốc, giữ đuôi tới ≥ 0,20 / ≥ 0,10 s (T-2) |
| `empty_click.wav` | Cắt ≤ 0,30 s, fade-out 20 ms |
| `armor_absorb_0/1/2.wav` | Cắt ≤ 0,40 s, fade-out 40 ms |
| `eliminated.wav` | Fade-out 15 ms |

**(b) 12 mã JSurviv → 15 file mới trong `Sfx/`** (theo quyết định 2.11a; điểm cắt chọn theo đường bao năng lượng, tính từ tiếng đầu tiên; PIC nghe ở AC36 rồi chỉnh số trong công thức nếu cần, chỉ là dữ liệu):

| SfxId | File mới | Nguồn (chỉ đọc) | Cắt / fade-out | Ghi chú |
| --- | --- | --- | --- | --- |
| MeleeSwing | `melee_swing.wav` | `melee chém.mp3` | nguyên 0,37 s / 10 ms | |
| MeleeHit | `melee_hit.wav` | `Đập.wav` | nguyên 0,32 s / 10 ms | Đỉnh gốc −7,2: tăng độ lợi |
| ShootRanged | `shoot_ranged.wav` | `súng bắn.mp3` (24 kHz) | ≤ 1,20 s / 30 ms | Đổi về 44,1 kHz |
| BombFuse | `bomb_fuse.wav` | `countdown.mp3` (24 kHz, 11,78 s) | 1,20 s / 50 ms | Không lặp (Q10) |
| Explosion | `explosion.wav` | `bomb nổ.mp3` (5,41 s) | ≤ 2,80 s / 300 ms | |
| HurtLocal | `hurt_local.wav` | `bleed.mp3` | ≤ 0,55 s / 40 ms | Rủi ro nội dung (TR6) |
| PickupWeapon | `pickup_weapon.wav` | `nhặt súng.mp3` | nguyên 0,60 s / 20 ms | |
| PickupItem | `pickup_item.wav` | `Nhặt đồ/Gun Put On.mp3` | ≤ 0,75 s / 40 ms | |
| WearArmor | `wear_armor_0…3.wav` | `bot di chuyển.zip` → `Leather Armor Put On 01–04` | ≤ 1,15 s / 60 ms | Gốc nhỏ (−10…−15): đưa về L đích. Bỏ `Helmet Put On` |
| UseHeal | `use_heal.wav` | `gauze.mp3` | ≤ 1,40 s / 100 ms | |
| UseDrink | `use_drink.wav` | `cola.mp3` | ≤ 1,40 s / 100 ms | |
| MatchEnd | `match_end.wav` | `TimeOver.wav` | nguyên 2,82 s / 50 ms | Mono (Q2). Ghi thẻ hollywoodedge.com |

`ZoneBell3` giữ một tiếng và `repeatCount = 3` (AC28 không đổi cột nào).

**(c) Nhạc** (cần bộ nén, T-1). Ba file vào `Music/`, đo L trên WAV trước khi nén:

| Track | File | Nguồn | Đích |
| --- | --- | --- | --- |
| WaitingRoom | `waiting_room.ogg` | `Waiting Room Music.wav` (25,7 MB) | Stereo, L −26 ± 2, đỉnh ≤ −1; Vorbis ≈ 128 kbps → ≈ 2,3 MB |
| Loading | `loading.ogg` | `Loading Music.mp3` (mono) | Giữ mono, L −26; ≈ 0,7 MB |
| Match | `match.ogg` | `ParkingMapMusic.mp3` (4,9 MB) | Stereo, L −26; ≈ 160 kbps → ≈ 2,5 MB |

`AudioRuntimeSet`: `waitingRoomMusic` → `waiting_room`, `loadingMusic` → `loading`, `matchMusic` → `match`. `MusicEnabled` mặc định vẫn tắt (code AUD-01, không chạm). Điểm lặp không xử lý (AC34, PIC nghe).

Dung lượng dự kiến: SFX ≈ 2 MB, nhạc ≈ 5,5 MB; tổng < 12 MB (AC35).

### 4.3. Danh sách file

**Tạo mới:**

| Đường dẫn | Nội dung |
| --- | --- |
| `Tools/Audio/process_clips.py`, `measure_clips.py`, `build_manifest.py`, `test_art05_audio.py` | Xử lý, đo, sinh manifest, unittest (thư viện chuẩn) |
| `Tools/Audio/art05_recipe.csv`, `art05_jsurviv_inventory.csv` | Công thức 43 file cuối (25 + 15 + 3), kiểm kê 30 nguồn JSurviv |
| `Assets/_JungleMaze/Art/Audio/Sfx/` 15 WAV + `.meta` | 12 mã JSurviv (4.2b) |
| `Assets/_JungleMaze/Art/Audio/Music.meta`, `Music/` 3 OGG + `.meta` | Nhạc (4.2c) |
| `Assets/_JungleMaze/Tests/EditMode/Audio/ArtAudioSpec.cs`, `ArtAudioCatalogTests.cs`, `ArtAudioLevelTests.cs`, `ArtAudioManifestTests.cs`, `ArtAudioMusicTests.cs` + `.meta` | Test dữ liệu (4.4) |
| `Docs/Tickets/ART-05_audio_manifest.csv` | Manifest (sinh bằng script) |
| `Docs/Licenses/Audio/Kenney_ImpactSounds_License.txt` (chuyển từ `Docs/Art/Audio/LICENSE_Kenney_CC0.txt`), `Kenney_InterfaceSounds_License.txt`, `Kenney_RPGAudio_License.txt`, `OpenGameArt_cc0-deep-monster-roar.pdf` | Bằng chứng giấy phép (K6) |

**Sửa:**

- `Sfx/*.wav`: ghi đè 25 file, `.meta` không đổi.
- `SfxCatalog.asset`: `clips` của 12 mã JSurviv; `volume` của 26 mã. `pitchJitter`, `priority`, `bus`, `rangeCells`, `maxInstances`, `cooldownSeconds`, `repeatCount`, `repeatIntervalSeconds` không đổi.
- `Resources/AudioRuntimeSet.asset`: 3 tham chiếu nhạc.
- `Docs/Art/Audio/ART05_Sources.vi.md`: thêm cột tác giả, ngày tải, đoạn cắt, độ lợi, giấy phép theo G-11; sửa câu khẳng định CC0 (K7).
- `Tools/Audio/README.md`: cách chạy pipeline.
- `Docs/Agent_Coordination.vi.md`: dòng bắt đầu/kết thúc giữ Editor.

**Không sửa:** `Scripts/**`, `Bootstrap*`, scene, prefab, `ProjectSettings/**`, `Backend/**`, `Assets/Imported/**`, test của AUD-01 (đã đổi đúng dòng ở `3f32e1c4`), `Placeholder/**` (tới bước dọn sau G3).

**Sau G3** (Q7, commit riêng): xóa `Placeholder/JSurviv/**` và `Placeholder/Beeps/**` không còn được tham chiếu, ghi chú vào `Tools/Audio/README.md`. `BeepsHaveDistinctFrequencies` vẫn xanh (0 bíp). `copy_jsurviv_placeholders.py --check` sẽ báo thiếu bản chép: hệ quả mong đợi. Cần chạy AC38 (`--check`) **trước** bước này.

### 4.4. Test theo AC

**EditMode mới** (chỉ đọc dữ liệu; namespace `JungleMaze.Tests.Audio`, asmdef `JungleMaze.Tests.EditMode` sẵn có):

| Lớp | Kiểm | AC |
| --- | --- | --- |
| `ArtAudioSpec` | Bảng tĩnh của test: SfxId → tầng E, khoảng độ dài (2.3), khoảng L (2.6). Không phải mã runtime | — |
| `ArtAudioCatalogTests` | Tên `^[a-z0-9_]+\.wav$`, ≤ 5 clip/mã · không clip nào nằm dưới `Assets/Imported/`, GUID trong `Sfx/`/`Music/` không trùng `.meta` trong `Imported` · 26 mã, mọi clip dưới `Art/Audio/Sfx/`, 0 dưới `Placeholder/` · các cặp phải khác clip · độ dài từng mã trong khoảng · BombFuse ≤ 1,5, Explosion ≤ 3,0 | AC8, AC9, AC10, AC13, AC17, AC21–AC24, AC26, AC27 (phần dữ liệu) |
| `ArtAudioLevelTests` | `LoadAudioData` + `GetData` từng clip: mono · đỉnh ≤ −1,0 dBFS (dung sai ADPCM 0,2 dB), 0 chuỗi ≥ 3 mẫu chạm trần · 2 ms đầu/cuối ≤ −40 dBFS · L trong khoảng · E trong tầng ± 3 · lề của HitMarker · đỉnh sau import khớp số đo nguồn trong manifest ± 0,5 dB · importer: Decompress On Load, ADPCM/PCM, `forceToMono` tắt; `normalize` đọc từ dòng `normalize:` trong `.meta` (API công khai không lộ cờ này) | AC16, AC18, AC19, AC20, AC40 |
| `ArtAudioManifestTests` | 26 dòng `sfx` đúng thứ tự `Enum.GetNames(typeof(SfxId))`, trạng thái ∈ {Có, Cần chỉnh, Thiếu} · số file trong `Assets/Imported/JSurviv/Sound` = 21 và dòng `jsurviv` = 30 · giấy phép theo từ vựng G-11, `có bằng chứng:` trỏ tệp có thật, không có "CC0/miễn phí/royalty-free/được phép" nếu thiếu đường dẫn · dòng `clip` khớp một-một với file trong `Sfx/` + `Music/`, dòng tải về có URL/tác giả/ngày · cột `heard` ∈ {`có: <tên>, <ngày>`, `chưa`} | AC1, AC3, AC4, AC6, AC41 |
| `ArtAudioMusicTests` | Mỗi file ≤ 3,0 MB, độ dài lệch ≤ 0,1 s so với gốc · `waitingRoomMusic` ≠ `loadingMusic`, cả ba không rỗng và nằm dưới `Music/` · importer Streaming + Vorbis | AC29, AC31, AC16 (nhạc) |

**Python** `Tools/Audio/test_art05_audio.py` (unittest): fade đạt −40 dBFS ở 2 ms đầu/cuối; limiter không vượt trần; độ lợi đạt L ± 0,1 dB; chạy hai lần ra cùng byte; đổi tần số giữ độ dài ± 1 mẫu.

**Chạy lại, không sửa:** EditMode `JungleMaze.Tests.Audio.*` và PlayMode `AudioAcceptanceTests` (15/15). Bộ này phủ AC12, AC14 (A3, A5, A13), AC20 (A19), AC21 (A14), AC22, AC25 (A20), AC27 (dừng fuse), AC32 (A23), AC33 (A22, A24). Console 0 error/warning.

**Kiểm một lần, ghi vào báo cáo QA:**

- AC2: đo lại kiểm kê; MP3 qua `AudioClip.length` của bản chép trước khi dọn.
- AC5, AC7, AC11 (`git diff --stat`), AC35 (script cộng dung lượng), AC37, AC42.
- AC15: đảo một mã trên bản sao tạm.
- AC30: `GetData` không đọc được clip Streaming, nên đo nhạc sau nén bằng cách tạm đặt Decompress On Load trong lúc kiểm rồi trả lại, không commit.
- AC38: `copy_jsurviv_placeholders.py --check`, phải chạy trước khi dọn `Placeholder`.

**Người dùng (PIC):** AC6 (cột `heard`), AC34 (điểm lặp nhạc), AC36 = AC-A28 của AUD-01: nghe 11 mã ★ qua F6 ("Dồn 10 s", "Gửi lặp ×5"), bật nhạc qua F6 để nghe ba bài.

### 4.5. Chia commit

Mỗi commit dùng `git commit -m "…" -- <đường dẫn>`, message tiếng Anh, không attribution, không push. Test xanh sau mỗi commit có asset.

| # | Commit | File | Cần Editor |
| --- | --- | --- | --- |
| 1 | `tools(art-05): add clip processing, measuring and manifest scripts` | `Tools/Audio/*.py`, 2 CSV, README | Không |
| 2 | `audio(art-05): refade, retrim and level the 25 CC0 clips` | 25 `Sfx/*.wav` (giữ `.meta`), `SfxCatalog.asset` (`volume` 14 mã) | Có |
| 3 | `audio(art-05): derive the 12 JSurviv clips into Sfx` | 15 WAV + `.meta`, `SfxCatalog.asset` (`clips` + `volume` 12 mã) | Có |
| 4 | `docs(art-05): add audio manifest and licence evidence` | manifest, `Docs/Licenses/Audio/**`, `ART05_Sources.vi.md` (kể cả xóa bản cũ ở `Docs/Art/Audio/`) | Không |
| 5 | `test(art-05): add audio data and loudness tests` | 4 file test SFX + `.meta` | Có (chạy test) |
| 6 | `audio(art-05): compress and level the three music tracks` | `Music/**`, `AudioRuntimeSet.asset`, `ArtAudioMusicTests.cs`, dòng nhạc trong manifest | Có · **cần T-1** |
| — | Sau G3: `chore(art-05): remove unreferenced placeholder clips` | `Placeholder/**`, README | Có |

Tổng: **6 commit** trước G3 (5 nếu nhạc lùi), cộng 1 commit dọn sau G3.

### 4.6. Agent build, song song, ước lượng

- **Agent build: unity-dev.** Không cần backend-dev: không đổi server, protocol hay `match.json`.
- **Song song:** Giai đoạn A (commit 1, xuất file vào thư mục tạm, bản nháp manifest) không cần Editor, nên chạy được **ngay bây giờ** trong lúc unity-dev gắn sprite UI-03. Người chạy là một phiên thứ hai chỉ dùng Python, vùng file là `Tools/Audio/**` và thư mục tạm ngoài repo, không ghi gì vào `Assets/`. Giai đoạn B (commit 2–6) chờ unity-dev trả Editor.
- **Ước lượng:** Giai đoạn A 0,5 ngày; Giai đoạn B 0,5 ngày; nhạc thêm 0,25 ngày nếu có bộ nén. Tổng **khoảng 1–1,25 ngày**, kịp 30/10. PIC nghe khoảng 30 phút.

### 4.7. Rủi ro kỹ thuật

- **TR1 · Limiter làm bẹt tiếng va.** Chuông và tấm giáp có hệ số đỉnh cao; kéo L lên −16/−18 cần giảm đỉnh tới 6 dB. Giới hạn 6 dB, cho ZoneBell3 tới −18, PIC nghe (AC36). Không đạt thì chọn clip khác cùng gói hoặc tạm trỏ lại bíp (T-7).
- **TR2 · `GetData` trên clip ADPCM** trả dữ liệu đã giải nén, có sai số. Đỉnh và \|x\| 2 ms đầu/cuối có thể lệch 0,2–0,5 dB. Test dùng dung sai; clip ≤ 0,3 s fail vì ADPCM thì chuyển sang PCM (2.7 cho phép).
- **TR3 · Không đọc được clip Streaming bằng `GetData`.** Mức nhạc kiểm một lần (4.4), không thành test cố định.
- **TR4 · Chưa có bộ giải mã/nén** (`ffmpeg`, `sox` không có trên máy). SFX vẫn làm được qua đường Unity. Nhạc (AC29–AC31, P1) không làm được nếu không có bộ nén.
- **TR5 · File gốc CC0 không nằm trong repo.** Cắt lại `shoot_crossbow`, `reload`, `use_cancel` và lấy license của 2 gói Kenney cần các gói CPO đã tải (T-2).
- **TR6 · Chọn đoạn cắt khi chưa nghe** (BombFuse từ đoạn đếm ngược 11,8 s, HurtLocal từ `bleed`, PickupItem từ "đặt súng"). Chọn theo đường bao năng lượng; AC36 có thể đổi số trong công thức, chỉ là dữ liệu.
- **TR7 · Hai phiên cùng dùng Editor.** File thả vào `Assets/` khi người khác giữ Editor sẽ sinh `.meta` mặc định (`normalize: 1`) lẫn vào diff của họ. Giai đoạn A chỉ ghi ra thư mục tạm; Giai đoạn B chỉ chép vào khi đã ghi giữ Editor.
- **TR8 · Đổi `volume` cả 26 mã đổi cảm giác âm thanh của AUD-01.** Có test A19 chặn bước chân > 0,5. Các số đều nằm trong asset, sửa lại không cần mã.
- **TR9 · Giấy phép JSurviv không giải quyết được trong ticket.** 12 clip và 2 bài nhạc vẫn `chưa kiểm chứng`; `TimeOver.wav` mang thẻ thư viện thương mại (AC42, Q5).

### 4.8. Câu hỏi kỹ thuật cho G1

| # | Câu hỏi | Mặc định khuyến nghị |
| --- | --- | --- |
| **T-1** | Cho dùng **ffmpeg** bản portable (ngoài repo, không commit) để giải mã MP3/OGG và nén nhạc? Cài hay tải phần mềm lên máy cần **người dùng** đồng ý | **Có** (spec Q12 đã khuyến nghị). Không có thì: SFX giải mã qua Unity; nhạc giữ như AUD-01, AC29–AC31 ghi "chưa đạt", commit 6 lùi. **Chỉ chặn phần nhạc (P1)** |
| **T-2** | Gói Kenney (Impact, Interface, RPG Audio) và file rồng CPO đã tải còn ở đâu? Cần để cắt lại 3 clip ngắn và lấy `License.txt` của 2 gói còn thiếu | CPO đưa đường dẫn thư mục cho người build. Nếu đã xóa: tải lại cần người dùng đồng ý. Thiếu thì K3 (P0 ShootCrossbow) và K6 không đóng được |
| **T-3** | ShootCrossbow: nếu `pluck_001` gốc < 0,15 s? | Chọn clip khác trong 3 gói Kenney đã có giấy phép, PIC nghe. Không đệm im lặng để qua test |
| **T-4** | Tên file: đổi 25 file sang `<sfx_snake>_<NN>.wav` như AC8, hay giữ tên CPO đặt? | **Giữ** tên hiện có; 15 file mới đặt cùng kiểu (`melee_swing.wav`, `wear_armor_0.wav`). CPO ghi cách hiểu AC8 ở G1. Đổi tên không lợi gì mà làm lệch `ART05_Sources.vi.md` |
| **T-5** | Đích L và `volume` theo bảng 4.1 (T1 −16, T2/T3/BushEnter −18, bước chân −24; `volume` 1,00/0,79/0,50/0,28/0,45)? | **Có** |
| **T-6** | Chuyển `LICENSE_Kenney_CC0.txt` sang `Docs/Licenses/Audio/` đúng spec và đổi tên theo gói? | **Có**; `ART05_Sources.vi.md` trỏ tới đường dẫn mới |
| **T-7** | Clip không đạt sau xử lý hoặc sau khi nghe: cho tạm trỏ lại bíp và ghi báo cáo (spec 2.13)? | **Có**; kỳ vọng "0 bíp" của `SfxCatalogTests` khi đó tính theo số mã còn dùng bíp, báo CPO trước khi đổi |

### 4.9. Kết quả G1

**G1 · 27/10/2026 · CPO** (quyền người dùng giao: "Bạn được quyền quyết thay thôi. ko đợi tôi quyết"): **Duyệt** kế hoạch 4.0–4.7.

- **T-1 → Không dùng ffmpeg.** Không tải và chạy file thực thi lạ. Dùng Python có `soundfile` (libsndfile 1.2.2, đã kiểm: đọc được MP3, ghi được OGG Vorbis) và `numpy`, cài trong venv ngoài repo. Nhạc nén sang **OGG Vorbis**, chất lượng tương đương 0,7, thay cho MP3/WAV. Vì vậy AC29–AC31 không bị chặn, giữ commit 6.
- **T-2 → Nguồn gốc đã vào repo** (`31b7aa24`): `Tools/Audio/Sources/ART-05/` (3 zip Kenney còn nguyên `License.txt` + `monster_roar.wav` + `README.vi.md`). Script lần đầu ở `Tools/Audio/art05_make_sfx.py`. Venv dựng sẵn trên máy: `C:\Users\Admin\AppData\Local\Temp\claude\E--Github-Jungle-Maze-Survival\78f61af7-a637-481e-8842-cbdd90ab6cb2\scratchpad\audio_venv\Scripts\python.exe`; nếu mất thì tạo lại bằng `python -m venv` + `pip install soundfile numpy`.
- **T-3 → Mặc định:** chọn clip Kenney khác dài ≥ 0,15 s, không đệm im lặng.
- **T-4 → Giữ tên file hiện có.** AC8 hiểu là: tên ASCII, chữ thường, có tiền tố theo SfxId và hậu tố biến thể `_N`. Như vậy 25 file của `3f32e1c4` đã đạt.
- **T-5, T-6, T-7 → Mặc định** như 4.8.
- **Test PlayMode:** `72e5e1e3` đã sửa `AudioAcceptanceTests` để chấp nhận mọi biến thể clip. Lỗi này do `3f32e1c4` gây ra; PlayMode hiện 106/106.

**Build:** unity-dev, 6 commit theo 4.5, Giai đoạn A có thể làm ngay. Hạn 30/10.

## 5. Nhật ký build

### Build ART-05 · 27/10/2026 · unity-dev

**Commit** (7, không push):

| # | Commit | Nội dung |
| --- | --- | --- |
| 1 | `176be71e` | `Tools/Audio/process_clips.py`, `measure_clips.py`, `build_manifest.py`, `test_art05_audio.py`, `art05_recipe.csv`, `art05_jsurviv_inventory.csv`, README; bỏ `art05_make_sfx.py` (bản đầu, đã thay) |
| 2 | `04aa1068` | Ghi đè 25 WAV CC0 (giữ `.meta`, GUID không đổi), `volume` 14 mã |
| 3 | `96f76179` | 15 WAV dẫn xuất từ 12 mã JSurviv, `clips` + `volume` 12 mã |
| 6 | `8d0b9063` | `Music/` 3 OGG (Streaming, Vorbis 0,7), `AudioRuntimeSet` trỏ 3 bài |
| 6b | `83c20458` | 40 SFX import **PCM** thay ADPCM (lý do ở dưới) |
| 4 | `7f6f3f62` | Manifest, `Docs/Licenses/Audio/` (4 tệp), viết lại `ART05_Sources.vi.md` |
| 5 | `5495418e` | 5 lớp test EditMode `ArtAudio*` (19 test) |

Thứ tự khác 4.5: nhạc (6) trước manifest (4) và test (5), vì manifest có dòng nhạc và `ArtAudioMusicTests` dùng chung `ArtAudioSpec` với test SFX. Thêm commit 6b.

**Quy trình.** `process_clips.py` đọc bản gốc chỉ-đọc (`Tools/Audio/Sources/ART-05/`, `Assets/Imported/JSurviv/Sound/`), ghi ra `%TEMP%/art05_stage`, rồi mới chép vào `Assets/` khi đã giữ Editor. Python có `soundfile` 0.14 (libsndfile 1.2.2) và `numpy` 2.5, không ffmpeg (G1 T-1). WAV ra giống từng byte khi chạy lại (đã kiểm md5 hai lần chạy). Đổi tần số bằng sinc cửa sổ Blackman (`súng bắn`, `countdown` 24 kHz; RPG Audio 48 kHz). Mọi clip: 2 ms im lặng + fade-in 3 ms, fade-out cosine rồi 2 ms im lặng, limiter nhìn trước trần −1,05 dBFS. `.meta` của 15 + 3 file mới chép từ mẫu `.meta` SFX sẵn có (GUID mới) trước khi Unity import, nên không lúc nào có `.meta` mặc định `normalize: 1`.

**Ba khoảng trống P0 của Tech Lead (4.0):**
- K1 fade-in: cả 40 SFX có 2 ms đầu và 2 ms cuối im hẳn (đo −240 dBFS trên WAV và qua `GetData`).
- ZoneBell3: đổi sang `impactBell_heavy_002` (0,68 s, không phải cắt), L −17,5, E −17,5 (T1 −16 ± 3). Limiter giảm 6 dB.
- ShootCrossbow: `pluck_001` (0,09 s) và `pluck_002` (0,13 s) đều < 0,15 s, nên chọn `chop.ogg` của Kenney RPG Audio (tiếng phập gỗ), giữ đuôi tự nhiên: 0,228 s, không đệm im lặng (T-3). **PIC cần nghe có hợp tiếng nỏ không.**
- Eliminated so với HitMarker: Eliminated E −20,05 (trước −16,9); HitMarker E −16,5, lề so với mã T2–T4 to nhất là 3,5 dB (AC20 ≥ 3), so với bước chân là 14,4 dB (≥ 12).

**Đổi clip so với lượt đầu:** `UseCancel` `back_002` (0,07 s < 0,10) → `close_001` (0,15 s). `Reload` giữ đuôi `metalLatch` tới 0,22 s (≥ 0,20). `EmptyClick` cắt còn 0,12 s (≤ 0,30). `ArmorAbsorb` cắt 0,37 s (≤ 0,40). `UseDrink` lấy 0,6 s đầu (mở lon) thay vì 1,4 s, vì đoạn rót sau đó chỉ −40 dBFS làm L tụt dưới −20.

**Lệch kế hoạch:**
1. **Limiter quá 6 dB ở 13 clip** (TR1). Clip thưa hoặc hệ số đỉnh cao (tích tắc ngòi bom cách 0,3 s, chốt kim loại, tấm giáp, giáp da, dùng đồ) không lên được L −18 trong 6 dB. Các dòng đó trong công thức có cột `max_reduction_db` tới 16 dB và đích L −19,5 (vẫn trong −20…−15 của AC40). Mức giảm thật của từng clip ghi ở cột `reduction_db` của manifest; lớn nhất 15,4 dB (`bomb_fuse`, `reload`). **PIC nghe xem tiếng va có bị bẹt không** (AC36).
2. **SFX import PCM, không ADPCM.** Đo bằng `GetData`: ADPCM đẩy đỉnh tiếng va lên tới +1 dB (`hurt_local` 0,0 dBFS, `use_drink` −0,07, `bomb_fuse` −0,24), vi phạm AC16/AC18. 2.7 cho PCM với clip ≤ 0,3 s; ở đây áp cho cả 40 clip (TR2), kể cả clip dài tới 3 s. Bộ nhớ lúc chạy không đổi (Decompress On Load vốn đã giải ra PCM); build tăng khoảng 1,6 MB.
3. **Vorbis nhạc:** chất lượng nguồn 0,7 làm `waiting_room` (145 s) và `match` (123 s) vượt 3,0 MB. Script chọn chất lượng cao nhất còn ≤ 3,0 MB: `waiting_room` 0,5 (2 695 023 B), `match` 0,5 (2 551 882 B), `loading` 0,7 (569 649 B). Import trong Unity vẫn Vorbis 0,7 theo 2.7. OGG có số serial ngẫu nhiên nên không giống byte giữa hai lần chạy.
4. **Bằng chứng giấy phép rồng** là bản ghi chữ nội dung trang OpenGameArt (tác giả trazzz123, License(s): CC0, đọc 27/10/2026) kèm md5 file tải về, không phải ảnh chụp/PDF.
5. `Docs/Art/Audio/ART05_Sources.vi.md` (vùng `Docs/Art/**`) được viết lại theo K7 như 4.3 đã ghi; đã ghi ở mục 4 của `Agent_Coordination`.

**Bảng mức âm** (đầy đủ từng clip: `Docs/Tickets/ART-05_audio_manifest.csv`, dòng `clip`; dưới đây là dòng `sfx`). L là mức hoạt động trung bình các biến thể (G-7), E = L + 20·log10(volume), "Giảm" là mức limiter giảm lớn nhất trong các biến thể:

| SfxId | Tầng | Clip | Dài (s) | L (dBFS) | volume | E (dBFS) | Giảm (dB) |
| --- | --- | --- | --- | --- | --- | --- | --- |
| FootstepGrass | T4 | 5 | 0.12–0.16 | -24.0 | 0.45 | -30.9 | 0.0 |
| FootstepSwamp | T4 | 5 | 0.12–0.19 | -24.0 | 0.45 | -30.9 | 0.0 |
| BushEnter | T4 | 2 | 0.47–0.55 | -18.2 | 0.28 | -29.3 | 6.0 |
| MeleeSwing | T2 | 1 | 0.30 | -18.0 | 0.79 | -20.1 | 0.0 |
| MeleeHit | T2 | 1 | 0.18 | -18.0 | 0.79 | -20.1 | 3.1 |
| ShootRanged | T2 | 1 | 0.98 | -18.0 | 0.79 | -20.1 | 0.0 |
| ShootCrossbow | T2 | 1 | 0.23 | -18.6 | 0.79 | -20.6 | 6.0 |
| Throw | T2 | 1 | 0.44 | -18.0 | 0.79 | -20.1 | 0.0 |
| Reload | T3 | 1 | 0.22 | -19.5 | 0.50 | -25.5 | 15.3 |
| EmptyClick | T3 | 1 | 0.12 | -19.5 | 0.50 | -25.5 | 14.0 |
| BombFuse | T2 | 1 | 0.95 | -19.5 | 0.79 | -21.6 | 15.4 |
| Explosion | T2 | 1 | 2.80 | -18.0 | 0.79 | -20.1 | 0.0 |
| TrapTriggered | T2 | 1 | 0.17 | -19.5 | 0.79 | -21.5 | 12.7 |
| HitMarker | T1 | 1 | 0.07 | -16.5 | 1.00 | -16.5 | 8.0 |
| HurtLocal | T1 | 1 | 0.55 | -16.0 | 1.00 | -16.0 | 4.7 |
| ArmorAbsorb | T2 | 3 | 0.37–0.37 | -19.0 | 0.79 | -21.1 | 10.3 |
| Eliminated | T2 | 1 | 0.37 | -18.0 | 0.79 | -20.1 | 0.0 |
| PickupWeapon | T3 | 1 | 0.55 | -18.0 | 0.50 | -24.0 | 0.0 |
| PickupItem | T3 | 1 | 0.55 | -19.5 | 0.50 | -25.5 | 10.3 |
| WearArmor | T3 | 4 | 0.79–1.15 | -19.5 | 0.50 | -25.5 | 12.8 |
| UseHeal | T3 | 1 | 1.40 | -19.5 | 0.50 | -25.5 | 8.5 |
| UseDrink | T3 | 1 | 0.60 | -19.5 | 0.50 | -25.5 | 7.0 |
| UseCancel | T3 | 1 | 0.15 | -18.0 | 0.50 | -24.0 | 0.0 |
| ZoneBell3 | T1 | 1 | 0.68 | -17.5 | 1.00 | -17.5 | 6.0 |
| DragonRoar | T1 | 1 | 3.00 | -16.0 | 1.00 | -16.0 | 0.0 |
| MatchEnd | T1 | 1 | 2.79 | -16.0 | 1.00 | -16.0 | 0.9 |

Nhạc (đo trên OGG đã giải mã bằng libsndfile): `waiting_room` L −26,0, đỉnh −10,2, 145,53 s · `loading` L −26,0, đỉnh −18,7, 43,65 s · `match` L −26,0, đỉnh −12,6, 123,43 s. Thấp hơn T1 10 dB (AC30). Tổng file âm thanh mới: 8 022 452 B (≤ 12 MB, AC35).

**Test:**
- Python `test_art05_audio.py` 7/7.
- EditMode **475/475** (456 + 19 `ArtAudio*`). Audio 78/78.
- PlayMode **106/106** (`AudioAcceptanceTests` 15/15).
- Console 0 Error/Warning. `EditorSettings.asset` đã `git checkout`.
- `git diff` của toàn đợt không có `Assets/Imported/**` hay `Scripts/**`; `.cs` chỉ là 5 file test.

**Còn mở, không tự đóng được:**
- AC6, AC34, AC36 (người nghe): cột `heard` = `chưa` ở mọi dòng. PIC nghe qua F6, nhất là ShootCrossbow (`chop`), các clip giảm > 6 dB, BombFuse (0,95 s tích tắc), UseDrink (mở lon), HurtLocal (`bleed`).
- AC42: 12 clip JSurviv và 3 bài nhạc vẫn `chưa kiểm chứng`; `TimeOver.wav` (MatchEnd) mang thẻ hollywoodedge.com.
- AC30 đường 1 (đo nhạc bằng `GetData` trong Unity) chưa làm, vì clip Streaming không đọc được; số trên đo bằng libsndfile.
- AC15 (đảo ngược trên bản sao tạm, P2) chưa làm.
- Sau G3: dọn `Placeholder/JSurviv/**` và `Placeholder/Beeps/**` (Q7); chạy `copy_jsurviv_placeholders.py --check` trước (manifest đã ghi md5 bản chép khớp gốc cho cả 14 file, AC38).

## 6. Review

### Review ART-05 · 27/10/2026 · code-reviewer

**Phạm vi:** `3f32e1c4` (CPO thay 14 bíp bằng clip CC0), `72e5e1e3` (sửa test PlayMode cho biến thể clip) và `git diff 7567fc34..e0bc9e4d` (build của Unity Dev, 8 commit `176be71e`…`e0bc9e4d`). Chỉ đọc; không chạy Unity. Có chạy lại `process_clips.py` bằng venv của dev, ghi ra thư mục tạm ngoài repo.

**Kết quả: Qua có điều kiện.** Chặn 0 · Nên sửa 3 · Gợi ý 8. Điều kiện trước G3: sửa Nên sửa 1 (nhãn giấy phép của `DragonRoar`, sửa dữ liệu và tài liệu). Nên sửa 2 và 3 không chặn build nhưng cần CPO ghi quyết định.

**Đã kiểm và đạt:**

- **Phạm vi:** mọi tệp của cả hai khoảng diff nằm trong "Vùng file của ART-05". Không có `Assets/Imported/**`, `Scripts/**`, scene, prefab, `ProjectSettings/**`, `Backend/**`. `.cs` chỉ là 5 file `ArtAudio*`, `SfxCatalogTests.cs` (đổi đúng một dòng `14 → 0` mà AC12 cho phép) và `AudioAcceptanceTests.cs`. Diff của `SfxCatalog.asset` (cả hai khoảng) chỉ chạm dòng `guid:` và `volume:`; không cột nào khác đổi (AC11). Không commit nào có Co-Authored-By hay nhắc AI.
- **Toàn vẹn GUID catalog:** 26 mục, mọi GUID trong `clips` khớp một `.meta` của `Sfx/`; không GUID trùng; 40 WAV trên đĩa mỗi file được tham chiếu đúng một lần (không mồ côi, không clip dùng chung hai mã). Biến thể giữ nguyên: Footstep ×5 + ×5, BushEnter 2, ArmorAbsorb 3, WearArmor 4, 21 mã còn lại 1. `AudioRuntimeSet` trỏ ba GUID nhạc khác nhau, cả ba khớp `Music/*.ogg.meta`.
- **Import:** cả 40 `.meta` SFX giống nhau: Decompress On Load, **PCM**, preload bật, `forceToMono: 0`, `normalize: 0`, `loadInBackground: 0`, không override nền tảng. Cả 3 nhạc: Streaming, Vorbis 0,7, `loadInBackground: 1`, `normalize: 0`. Khác G-10 ở chỗ PCM thay ADPCM cho cả 40 clip (commit `83c20458`); AC16 ghi "ADPCM/PCM", 2.7 cho PCM với clip ≤ 0,3 s, dev đã ghi lệch và lý do (đo `GetData`). Bộ nhớ runtime không đổi vì Decompress On Load vốn giải ra PCM.
- **Số trong manifest khớp repo:** 99 dòng (26 `sfx` + 30 `jsurviv` + 43 `clip`). md5 và kích thước của 43 dòng `clip` khớp file thật (0 lệch). 14 bản chép JSurviv ở `Placeholder/JSurviv/` giống từng byte file gốc và khớp md5 trong manifest (AC38). Tôi tính lại E từ manifest: mọi mã trong tầng ± 3 dB (lệch lớn nhất −1,94); HitMarker hơn mã to nhất của T2–T4 3,53 dB (≥ 3) và hơn FootstepGrass 14,42 dB (≥ 12); biến thể cùng mã lệch L ≤ 0,43 dB. Tổng file mới 8 022 452 B khớp nhật ký.
- **Tái lập:** chạy lại `process_clips.py` ra thư mục tạm: **40/40 WAV giống từng byte** file đã commit. Ba OGG cùng kích thước, khác byte (số serial ngẫu nhiên, dev đã ghi). Script chỉ ghi vào `--out` (mặc định `%TEMP%/art05_stage`); `build_manifest.py` chỉ ghi `Docs/Tickets/ART-05_audio_manifest.csv`; `measure_clips.py` chỉ ghi `--csv` do người dùng chỉ định. Không script nào ghi ngoài thư mục của nó.
- **Giấy phép JSurviv:** cả 30 dòng `jsurviv` và 18 dòng `clip` dẫn xuất (15 SFX + 3 nhạc) đều `chưa kiểm chứng`. Dấu vết `hollywoodedge.com` của `TimeOver.wav` có ghi ở dòng `jsurviv`. Ba tệp `Docs/Licenses/Audio/Kenney_*_License.txt` **giống byte** `License.txt` trong ba zip nguồn, nên bằng chứng cho 24 clip Kenney là thật.
- **Cân nặng repo:** `Tools/Audio/Sources/ART-05` 3,9 MB (3 zip Kenney 294 tệp, dùng 24 clip; `monster_roar.wav` 1,27 MB dùng 3 s) là cố ý theo G1 T-2, đúng chỗ để chọn lại clip cùng gói (T-3). Tổng thêm vào repo ≈ 11,9 MB (SFX 2,2 + nhạc 5,8 + nguồn 3,9), chưa tính 25 WAV bản đầu `3f32e1c4` còn trong lịch sử (≈ 0,6 MB). Không chặn.

**Nên sửa**

1. **Nhãn giấy phép `DragonRoar` nói nhiều hơn bằng chứng có.**
   - `Tools/Audio/art05_recipe.csv:40`, hai dòng manifest (`clip` `Sfx/dragon_roar.wav` và `sfx` `DragonRoar`) và dòng cuối bảng ở `Docs/Art/Audio/ART05_Sources.vi.md:35` ghi `có bằng chứng: Docs/Licenses/Audio/OpenGameArt_cc0-deep-monster-roar.txt`.
   - Tệp đó là chữ do Unity Dev chép lại từ trang OpenGameArt, tự ghi "không phải ảnh chụp hay PDF" và "file WAV không kèm license". Người sửa dữ liệu hoặc người nhận bàn giao không thể kiểm lại nội dung bản chép đó.
   - Spec 2.11 và R9 nói rõ: không có bản chụp trang thì giấy phép ghi `chưa kiểm chứng`. Nhật ký (Lệch kế hoạch 4) đã nêu lệch này nhưng chọn cách đọc dễ dãi hơn. Test `Licences_UseTheVocabulary_EvidenceExists` chỉ kiểm tệp tồn tại, nên không bắt được.
   - Kịch bản lỗi: danh sách "đã có bằng chứng" đưa cho người dùng/CPO quyết định phát hành có 25 clip tải về, trong đó 1 clip chỉ dựa vào lời khai của chính agent đọc trang.
   - Hướng sửa: đổi dòng recipe thành `chưa kiểm chứng` rồi chạy lại `build_manifest.py`, sửa dòng bảng nguồn; hoặc người (không phải agent) lưu ảnh chụp/PDF trang vào `Docs/Licenses/Audio/` rồi giữ `có bằng chứng`. Lưu ý: `Forbidden` của test chặn chữ "CC0" ở mọi dòng không có đường dẫn bằng chứng, trong khi spec AC41 muốn ghi "nguồn khai CC0": khi đổi nhãn, ghi "nguồn tự khai giấy phép tự do" hoặc nới test để cho phép "khai CC0".
2. **13 clip vượt giới hạn limiter 6 dB của kế hoạch (TR1) mà chưa qua G1.** Plan 4.1 cho tối đa 6 dB và, nếu không đạt, đưa ra hai đường lui: chọn clip khác cùng gói hoặc tạm trỏ lại bíp (T-7). Dev chọn đường thứ ba: nâng `max_reduction_db` tới 12–16 và hạ đích L xuống −19,5 (`art05_recipe.csv`, cột `max_reduction_db`). Giảm thật: `bomb_fuse` 15,41, `reload` 15,30, `empty_click` 14,01, `wear_armor_3` 12,81, `trap_triggered` 12,72, `wear_armor_0` 12,46, `armor_absorb_1` và `pickup_item` 10,30, `wear_armor_1` 8,75, `use_heal` 8,46, `hit_marker` 8,00, `use_drink` 7,02, `armor_absorb_0` 6,26 dB.
   - Các con số đạt AC18 và AC40 (đỉnh −1,05, L −19,5…−16), nhưng 15 dB limiter trên tiếng gõ/tích tắc thưa nghĩa là đỉnh bị nén gần như thành sóng phẳng. Test chỉ đo đỉnh và RMS nên không thấy được. `hit_marker` (mã ưu tiên cao nhất) bị giảm 8 dB.
   - Kịch bản lỗi: mọi AC đo được xanh nhưng PIC nghe thấy `BombFuse`, `Reload`, `EmptyClick` bẹt và rè; lúc đó phải chọn lại clip sau G3.
   - Hướng sửa: CPO ghi ngoại lệ vào 4.9 (hoặc trả lời "giữ 6 dB"); PIC nghe nhóm này trước (AC36 đã nhắc, nên đặt nó làm điều kiện G3 chứ không chỉ P1); dev chuẩn bị sẵn một biến thể 6 dB cho 3 clip lớn nhất (đổi dòng recipe, chạy lại, đổi một tham chiếu).
3. **`build_manifest.py` ghi đè cột `heard`, nên kết quả nghe của PIC sẽ mất.** `build_manifest.py:129` gán cứng `heard='chưa'` cho 26 dòng `sfx`; dòng `clip` lấy `heard` từ recipe (`:99`). Cả hai nơi đều là nguồn mà lần chạy kế tiếp sẽ ghi lại. README nói recipe là "nguồn duy nhất" nhưng không có chỗ nào để ghi `có: <tên>, <ngày>` cho dòng `sfx` (AC6 tính theo mã). Kịch bản: PIC nghe, ai đó sửa tay CSV; lần sau đổi một điểm cắt và chạy lại `build_manifest.py`, mọi dòng quay về `chưa`. Cùng chỗ: cột `gain_db`, `trim_*`, `reduction_db`, `*_before_dbfs` lấy từ `process_report.csv` trong `%TEMP%` (`:86-91`) và md5/kích thước lấy từ repo, mà không có bước nào kiểm hai bên cùng một lần chạy. Hướng sửa: lấy `heard` của dòng `sfx` từ các dòng `clip` của mã đó; kiểm `peak_after_dbfs` của report khớp phép đo file trong repo (hoặc dừng với lỗi nếu lệch).

**Gợi ý**

1. **Nhạc: Unity có thể mã hóa lại OGG ở 0,7 trên nguồn đã nén 0,5.** `waiting_room` và `match` được `process_clips.py` nén ở chất lượng 0,5 để vừa 3,0 MB, nhưng `.meta` đặt Vorbis 0,7. Nếu importer mã hóa lại (tôi tin là có, chưa kiểm chứng) thì bản đóng gói mất chất lượng hai lần và có thể lớn hơn 3,0 MB; AC29 chỉ đo file nguồn trong repo. Nên đo kích thước nhạc sau import (hoặc đặt quality của importer bằng quality đã dùng khi nén).
2. **Mức nhạc (AC30) không có kiểm tra tự động.** Số −26 dBFS chỉ đo ngoài Unity bằng libsndfile; `ArtAudioMusicTests` kiểm kích thước, độ dài, importer và tham chiếu nhưng không mức. Dev đã ghi AC30 đường 1 chưa làm. Thêm một kiểm tra trên manifest (`level_after_dbfs` nhạc trong −26 ± 2 và khớp file) hoặc một test Editor đặt Decompress On Load tạm thời.
3. **Dung sai test rộng hơn câu chữ AC và hơn dữ liệu thật.** `ArtAudioLevelTests.cs:19` cho đỉnh ≤ −0,95 (AC18 nói −1,0); `:79` mở khoảng L thêm 0,2 dB; `:127` so đỉnh import với manifest ± 0,5 dB trên clip PCM (chính xác tới 0,02 dB). Dữ liệu hiện đạt cả dung sai chặt (đỉnh ≤ −1,05; L −19,51…−16,00; bước chân đúng −24,00). Thu hẹp để một lần nâng/hạ vài phần dB không lọt qua.
4. **Cột catalog "không đổi" (AC11) chưa có test giữ.** Comment ở `ArtAudioCatalogTests.cs:101` nói "ART-05 changes clips and volume only" nhưng test chỉ kiểm `volume` trong khoảng và `repeatCount`/`repeatIntervalSeconds` của chuông. Tôi đã tự đối chiếu diff YAML (đạt), nhưng `priority`, `bus`, `rangeCells`, `maxInstances`, `cooldownSeconds` của 26 mục có thể đổi mà test vẫn xanh. Thêm một bảng giá trị cố định cho 26 mục.
5. **Test gắn vào thư mục tạm thời, và một test đã rỗng.** `ArtAudioManifestTests.cs:39` và `ArtAudioCatalogTests.cs:53` đọc `Assets/Imported/...` (`GetFiles` ném lỗi nếu thư mục bị dọn, mà spec 2.4 nói thư mục JSurviv có thể bị dọn), nên sẽ đỏ thay vì bỏ qua. `SfxCatalogTests.BeepsHaveDistinctFrequencies` giờ đếm 0 bíp nên vòng kiểm tần số không chạy; đổi tên hoặc bỏ khi dọn `Placeholder` (Q7).
6. **Test import chỉ đọc `defaultSampleSettings`.** `ArtAudioLevelTests.cs:40` và `ArtAudioMusicTests.cs:28` không kiểm `GetOverrideSampleSettings` theo nền tảng; hôm nay `platformSettingOverrides: {}` nên đúng, nhưng một override Android/iOS sau này đi qua mà không bị bắt.
7. **Cột `status` của dòng `sfx` là ảnh chụp trước ART-05.** Ví dụ `FootstepSwamp` và `ShootCrossbow` còn `Thiếu` dù đã có clip cuối; chỉ cột `notes` ("clip cuối tải về") cho biết khác. Đổi tên thành trạng thái trước xử lý hoặc thêm cột trạng thái hiện tại.
8. **Vệ sinh công cụ.** (a) `Tools/Audio/README.md:20,25` dùng `%TEMP%` trong khối ```powershell```, PowerShell không bung biến này (lệnh tạo venv sẽ tạo thư mục tên chữ `%TEMP%`); (b) phiên bản `numpy` 2.5, `soundfile` 0.14, libsndfile 1.2.2 chỉ ghi trong nhật ký, không ở README, trong khi "giống từng byte" phụ thuộc chúng; (c) `Tools/Audio/Sources/ART-05/README.vi.md:12` vẫn trỏ tới `art05_make_sfx.py` đã bị xóa ở `176be71e`; (d) `process_clips.py --out` không từ chối đường dẫn nằm trong `Assets/`, đúng loại lỗi TR7 (thả file làm Unity sinh `.meta` mặc định `normalize: 1`).

**Không kiểm chứng được (không có Unity, không nghe):**

- EditMode 475/475, PlayMode 106/106, console sạch: số của dev. Tôi chỉ đọc mã test và đối chiếu với dữ liệu.
- Đỉnh/mức sau import đo bằng `GetData` trong Editor, và nhận định "ADPCM đẩy đỉnh lên +1 dB" (lý do của `83c20458`). Tôi chỉ kiểm được cài đặt `.meta` và số đo trên WAV.
- Mọi cột `heard` đều là `chưa` (43 dòng clip và 26 dòng `sfx`): chưa ai nghe `ShootCrossbow` (`chop`), `HurtLocal` (`bleed`), `BombFuse` (tích tắc 0,95 s), `UseDrink`, các clip bị limiter nặng. AC6, AC34, AC36 còn mở, đúng như nhật ký ghi.
- Hành vi AC14, AC20, AC21, AC25–AC27, AC32, AC33 với clip thật chạy qua PlayMode của AUD-01 (tôi chỉ xem được đổi `IsClipOf`/`CountLow` ở `72e5e1e3`: clip các mã rời nhau nên một voice không bị tính nhầm sang mã khác, và vòng lặp kết thúc vì `GetClip` trả null khi hết biến thể).
- Giấy phép thật của 12 clip và 3 nhạc JSurviv, và nội dung trang OpenGameArt: ngoài khả năng của review.

## 7. QA

| # | Kết quả | Bằng chứng |
| --- | --- | --- |
| AC1 | ✓ / ✗ / chưa kiểm chứng | |

## 8. Nghiệm thu

<CPO tổng hợp; người duyệt G3, ngày, ghi chú. Nhắc cập nhật Status trong GDD.>
