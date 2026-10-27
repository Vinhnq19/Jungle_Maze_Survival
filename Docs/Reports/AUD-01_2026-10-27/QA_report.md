# AUD-01 · QA · 27/10/2026

Người kiểm: QA Tester (phiên riêng). Nhánh `huycv`, HEAD `50eb7e28`. Unity 6000.3.15f1, Editor, scene `Scenes/Dev/HudPreview.unity` (không lưu). Không sửa code, không build Release, không bật GameServer (AUD-01 không đổi luồng mạng).

## 1. Test tự động (chạy lại, không dựa log của dev)

| Lượt | Kết quả |
| --- | --- |
| EditMode `JungleMaze.Tests.EditMode` (toàn bộ) | **333/333** đạt, 0 fail, 0 skip, 11,4 s |
| EditMode `JungleMaze.Tests.Audio` (59 test, có chi tiết) | **59/59** đạt |
| PlayMode `JungleMaze.Tests.PlayMode` (toàn bộ) | **59/59** đạt, 0 fail, 0 skip, 22,2 s |
| PlayMode `AudioAcceptanceTests` | **15/15** đạt ([playmode_audio_results.json](playmode_audio_results.json)) |
| Console sau mỗi lượt và sau Play Mode | 0 error, 0 warning |

Dòng `[Audio] No clip for SFX 'Throw'…` trong output là phần dự kiến của test `MissingClip_WarnOnce_NoThrow` (đúng một lần cho mỗi mã).

## 2. Đo trực tiếp trong Play Mode (`HudPreview`, đi qua Bootstrap thật)

Cách đo: Play Mode, đặt callback `EditorApplication.update` ghi từng frame trạng thái 16 AudioSource voice, tham số AudioMixer (`GetFloat`) và `Time.realtimeSinceStartup`; kích sự kiện bằng đúng hàm của panel F6 (`PlayButton`, `SpamButton`, `RepeatButton`) hoặc qua port ghi thật `IHudStateWriter` và `IEventBus` lấy từ registry của `Bootstrap`. Panel IMGUI không được bấm bằng chuột (Unity MCP không click IMGUI).

### 2.1. 26 SFX, từng mã một (AC-A2, A9, A17, A19)

Mỗi mã: `StopAll`, bấm nút panel, đúng **1** voice bắt đầu, nhóm mixer đúng.

| # | SfxId | Clip | Nhóm | Âm lượng |
| --- | --- | --- | --- | --- |
| 1 | FootstepGrass | footstep_grass_330hz | SFX | 0,40 |
| 2 | FootstepSwamp | footstep_swamp_392hz | SFX | 0,45 |
| 3 | BushEnter | bush_enter_467hz | SFX | 0,50 |
| 4 | MeleeSwing | melee_slash | SFX | 0,70 |
| 5 | MeleeHit | hit_thud | SFX | 0,80 |
| 6 | ShootRanged | gun_shot | SFX | 0,75 |
| 7 | ShootCrossbow | shoot_crossbow_555hz | SFX | 0,75 |
| 8 | Throw | throw_660hz | SFX | 0,60 |
| 9 | Reload | reload_785hz | SFX | 0,50 |
| 10 | EmptyClick | empty_click_933hz | SFX | 0,50 |
| 11 | BombFuse | bomb_fuse_countdown | SFX | 0,70 |
| 12 | Explosion | bomb_explosion | SFX | 0,90 |
| 13 | TrapTriggered | trap_triggered_1110hz | SFX | 0,70 |
| 14 | HitMarker | hit_marker_2220hz | SFX | 1,00 |
| 15 | HurtLocal | hurt_bleed | SFX | 0,90 |
| 16 | ArmorAbsorb | armor_absorb_1320hz | SFX | 0,80 |
| 17 | Eliminated | eliminated_1570hz | SFX | 0,90 |
| 18 | PickupWeapon | pickup_weapon | SFX | 0,80 |
| 19 | PickupItem | pickup_item_gun_put_on | SFX | 0,70 |
| 20 | WearArmor | wear_armor_helmet_put_on | SFX | 0,70 |
| 21 | UseHeal | use_heal_gauze | SFX | 0,70 |
| 22 | UseDrink | use_drink_cola | SFX | 0,70 |
| 23 | UseCancel | use_cancel_1867hz | SFX | 0,60 |
| 24 | ZoneBell3 | zone_bell_880hz | **Alert** | 1,00 |
| 25 | DragonRoar | dragon_roar_70hz | **Alert** | 1,00 |
| 26 | MatchEnd | match_end_time_over | SFX | 1,00 |

26 clip đều khác tên nhau; các bíp mang tần số khác nhau trong tên. Pitch ngẫu nhiên trong ±5% (ví dụ 0,952 đến 1,039), HitMarker ±3%, âm dài và cảnh báo pitch 1,000.

### 2.2. Giới hạn voice, gửi lặp (AC-A3, A13)

- "Spam 100" ngay lúc bấm: `ActiveVoices` = 16/16, `AudioSource.isPlaying` = 16, số `AudioSource` trong scene **18 trước và 18 sau** (16 voice SFX + 2 nguồn nhạc). Cao nhất ở mọi frame lấy mẫu: 16.
- "Server event ×5": voice đang phát 0 → **1**.

### 2.3. Âm lượng và mixer (AC-A5)

Gọi `SetVolume` (đường gọi của thanh trượt F6) rồi đọc `AudioMixer.GetFloat`, cho cả bốn bus (Master/SFX/Music/Alert):

| v đặt | `GetVolume` | Mixer (dB) | Mong đợi (dB) |
| --- | --- | --- | --- |
| 0,5 | 0,5000 | −6,02 | −6,02 |
| 1 | 1,0000 | 0,00 | 0,00 |
| 0 | 0,0000 | −80,00 | −80,00 |
| 1,7 (kẹp) | 1,0000 | 0,00 | 0,00 |
| −0,3 (kẹp) | 0,0000 | −80,00 | −80,00 |
| 0,25 | 0,2500 | −12,04 | −12,04 |

Cấu trúc nhóm của mixer: `Master` → `SFX` → `Alert`; `Master` → `Music` → `MusicTrack`; 5 tham số expose đọc được (`MasterVolume`, `SfxVolume`, `AlertVolume`, `MusicVolume`, `MusicDuck`). Mức mặc định lúc vào Play: 0,00 dB cả năm.

### 2.4. Chuông bo (AC-A20)

Kích `ZoneBell3` ở một frame bình thường (không bị khựng), đo thời điểm mỗi voice bắt đầu:

| Tiếng | Thời gian thật | Đồng hồ sim của service | Nhóm |
| --- | --- | --- | --- |
| 1 | 0,000 s | 0,000 s | Alert |
| 2 | 0,802 s | 0,801 s | Alert |
| 3 | 1,602 s | 1,600 s | Alert |

Khoảng cách 0,802 và 0,800 s: đạt 0,8 ± 0,05. Lượt đo đầu có khoảng cách 0,47 s ở tiếng 2 vì lệnh `execute_code` làm Editor khựng 1,4 s ngay sau lúc kích (frame kế tiếp có `Time.deltaTime` bị kẹp 0,333 s). Đó là nhiễu của cách đo, không phải lỗi sản phẩm; lượt đo sạch ở trên là số chính. Ghi chú: lịch chuông chạy theo `Time.deltaTime` nên một cú khựng khi đang chuông sẽ rút ngắn khoảng nghe được tương ứng (nhẹ, không cần sửa).

### 2.5. Nhạc, hạ nhạc khi có cảnh báo (AC-A22, A23, A25)

| Thời điểm | Quan sát |
| --- | --- |
| Mặc định | `MusicEnabled = false`, 0 nguồn nhạc đang phát |
| Bật BGM | Nhạc (`loading_music`) bắt đầu **0,006 s** sau khi bật (≤ 1 s) |
| +2,000 s kích `ZoneBell3` | `MusicDuck` bắt đầu giảm ngay: −0,46 dB sau 0,011 s … **−8,87 dB sau 0,198 s**, tiếp tục về −9 (không ghi plateau, xem test PlayMode) |
| `SfxVolume` suốt thời gian đó | **0,00 dB**, không bị hạ |
| Hết âm Alert (+4,36 s) | `MusicDuck` bắt đầu nhả: −8,44 → −0,12 dB trong **0,93 s** (≤ 1,0 s) |
| +6,0 s tắt BGM | Hết nhạc sau **0,404 s** (≤ 0,5 s); sau 1 s 0 nguồn nhạc |
| Số nguồn nhạc phát cùng lúc | tối đa **1** (≤ 2) |

Số voice SFX tối đa ở mọi frame: 16.

### 2.6. Đường thật Bootstrap → router (AC-A14, A12; dừng âm)

Ghi qua `IHudStateWriter`/`IEventBus` của Bootstrap trong `HudPreview` (đã `SetPose(0,0,0)`):

| Bước | Kết quả |
| --- | --- |
| `NotifyHitDealt(Other)` | không phát (0 voice) |
| `NotifyHitDealt(Hero)` ×2 cùng frame | **1** voice `hit_marker_2220hz` |
| `NotifyDamageTaken(false, 0)` | `hurt_bleed` (1 voice; không giáp nên không có `ArmorAbsorb`) |
| `NotifyPickup("CROSSBOW", 1)` | `pickup_weapon` |
| `SetDanger(901, FusedBomb, 1,5 s, nguồn (3,0))` | `bomb_fuse_countdown` |
| 2,2 s sau (bom hết ngòi) | chỉ còn `bomb_explosion` (1 voice): `BombFuse` đã dừng khi nổ |
| `SetDanger(902)` rồi `ClearDanger(902)` | fuse dừng ngay (0 voice), **không** có `Explosion` |
| Bom 903 + `ZoneBell3` + `DragonRoar` đang phát (3 voice), rồi `AppScreenChanged(InMatch → Results)` | ngay sau: còn **1** voice `match_end_time_over`; 1,4 s sau vẫn 1 voice: chuông chờ bị hủy, không còn tiếng chuông lặp |

### 2.7. Listener dự phòng

| Thử | Kết quả |
| --- | --- |
| Thêm `Camera` + `AudioListener` lúc đang chạy | listener dự phòng tắt sau **1,896 s** (≤ 2 s); xóa camera thì bật lại sau **1,901 s**; luôn đúng 1 listener bật |
| Thêm `AudioListener` **không gắn camera** lúc đang chạy | dự phòng **không tắt** trong 12 s (hai listener cùng bật); khớp giới hạn đã ghi ở mục 5 ("chỉ nhận ra ở lần load scene sau"), nhưng ticket viết "tối đa 2 s" còn thực tế là tới khi load scene. Đọc console sau đó không có cảnh báo nào |

### 2.8. Panel F6

Ảnh Game view khi bật panel: [f6_panel_gameview.png](f6_panel_gameview.png) (Game view hiện tại của Editor 634×396). Thấy: tiêu đề "Audio · F6 (dev only)", `Voices 0/16`, thanh Master/SFX/Music, công tắc BGM và nút chọn màn hình, `Spam 100`, `Server event ×5`, chọn khoảng cách 0/6/12 cells, 26 nút. Ở Game view nhỏ thế này panel đè lên màn "Kết nối" và nút "Burst 10 s", "Fake pose" bị cắt bên phải (có thanh cuộn ngang). Chồng overlay đã nằm trong rủi ro 8 (UI-02 dời).

## 3. Dọn dẹp

- Thoát Play Mode, scene `HudPreview` không bị sửa (không dirty).
- Đã xóa 5 khóa `PlayerPrefs` `jm.audio.*` mà lượt đo tạo ra (trước lượt đo không có khóa nào).
- Gỡ mọi callback `EditorApplication.update` của lượt đo (thuộc domain Play Mode, mất khi thoát).
- `ProjectSettings/EditorSettings.asset` bị PlayMode đổi `m_EnterPlayModeOptions: 0 → 1`. **Chưa khôi phục được**: lệnh `git checkout -- ProjectSettings/EditorSettings.asset` bị bộ lọc quyền từ chối. Cần CPO hoặc người dùng cho phép chạy lệnh đó (hoặc tự chạy).
- `git status -- Assets/Imported/JSurviv` trống; `git diff 485c10ab^..HEAD -- Assets/Imported/JSurviv` trống.
- Không dùng GameServer, không dùng Docker/AWS.

## 4. Chưa kiểm chứng

- **AC-A28** và phần "nghe rõ/phân biệt được" của A19, A21: cần người nghe. Máy QA không nghe được.
- **Release build** (AC-A8: F6 không có): chỉ review `#if UNITY_EDITOR || DEVELOPMENT_BUILD` (`AudioDebugPanel.cs` toàn file, `MockAudioEventSource.cs` toàn file, `Bootstrap.Audio.cs` dòng 2–4, 41–46, 57–61). Không build.
- **Profiler GC** (AC-A27, P2) và `Is.Not.AllocatingGCMemory` trên máy chạy test đã đạt; chưa đo bằng Profiler.
- Chưa chạy scene `Game` với GameServer: `PlayerDied` và `InWarning` mới được kiểm qua test và packet giả. Bước chân, `WearArmor`, `ArmorAbsorb`, `UseHeal/UseDrink/UseCancel` chỉ có test tự động, chưa đo lại trong Play Mode.
- Cách nghe: xem mục 5.

## 5. Hướng dẫn nghe (AC-A28)

1. Mở `Scenes/Dev/HudPreview.unity`, bấm Play, bấm **F6** (panel góc trái dưới). Đảm bảo tai nghe/loa của Editor không bị tắt tiếng (Game view → Mute Audio bỏ chọn).
2. Bấm "Fake pose (0,0)", đặt "Positional at" = 0, rồi bấm lần lượt nút 1…26 để nghe từng mã. Chú ý: 14 `HitMarker` (tách ngắn, nghe rõ), 24 `ZoneBell3` (ba tiếng cách ~0,8 s), 25 `DragonRoar` (trầm, quét tần số), 17/23 các bíp khác tần số.
3. Bấm "Burst 10 s" (hoặc "Spam 100") và nghe xem hitmarker và chuông có chìm giữa các âm khác không.
4. Bật công tắc **BGM**, chọn màn hình "InMatch", bấm 24 (`ZoneBell3`) và nghe nhạc có hạ xuống khi chuông kêu rồi trả lại.
5. Kéo thanh Master/SFX/Music, xác nhận âm lượng đổi tương ứng.
6. Ghi lại điều chưa ổn cho ART-05 (chọn lại clip) thay vì sửa code.
