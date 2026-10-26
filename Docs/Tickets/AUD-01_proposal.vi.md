# AUD-01 — Đề xuất hướng giải quyết (27/10/2026)

**Trạng thái:** đề xuất, chưa có spec, chưa qua G1. Người dùng đã chọn làm ART-04 trước, AUD-01 làm sau theo hướng này. Khi bắt đầu, game-designer viết `Docs/Tickets/AUD-01.md` từ mẫu và dùng file này làm đầu vào.

Ticket: AudioManager và phản hồi âm thanh trong game. PIC V.Huy, P1, Giai đoạn 2, deadline 30/10/2026, phụ thuộc CLI-03 (Done). Ranh giới: AUD-01 sở hữu audio runtime, mixer và playback; ART-05 chỉ chuẩn bị file, license và metadata.

## Hiện trạng (27/10)

- Chưa có code audio nào trong `Assets/_JungleMaze`.
- UI-01 phần 2 đã phát đủ sự kiện phản hồi qua `IHudState` (`ItemPicked`, `HitDealt`, `DamageTaken`, `DangerChanged`) và `IInventoryState` (`UseChanged`). Core có `EventBus` và `ObjectPool`.
- `Assets/Imported/JSurviv/Sound` có khoảng 20 file dùng tạm được: súng bắn, melee chém, Đập, bomb nổ, Bo co, countdown, TimeOver, nhặt súng, Gun Put On, Helmet Put On, gauze, cola, bleed, button, revive, và 3 bài nhạc (Loading, ParkingMap, Waiting Room).
- GDD **không có danh sách 26 SFX**. Sheet Assets dòng 9–14 chỉ ghi nhóm, nên danh sách do AUD-01 đề xuất.

## Kiến trúc

| Lớp | Thành phần | Vai trò |
| --- | --- | --- |
| Domain | `SfxId` (26 mã), `AudioBus` (Master/SFX/Music/Alert) | Định danh, C# thuần |
| Application | `IAudioService`: `PlaySfx(id, position)`, `SetVolume(bus, 0–1)` | Port duy nhất cho gameplay và UI |
| Application | `SfxRouter` | Nghe sự kiện HUD/inventory và `EventBus`, đổi thành `SfxId`. Không tạo luồng dữ liệu riêng |
| Application | `SfxDeduper` + giới hạn tần suất | Bỏ sự kiện server gửi lặp (theo khóa + số thứ tự, khoảng 150 ms). Mỗi SFX có số bản phát cùng lúc tối đa và cooldown |
| Presentation | `AudioManager` + AudioMixer | Pool khoảng 16 AudioSource; khi đầy thì dừng âm ưu tiên thấp. Âm lượng giảm theo khoảng cách ô tính từ Hero |
| Presentation | `SfxCatalog` (ScriptableObject) | `SfxId` → danh sách clip, âm lượng, cao độ, độ ưu tiên, bus, có định vị không gian hay không. ART-05 chỉ thay clip, không sửa code |
| Presentation | `MusicDirector` | Nhạc theo menu/phòng chờ/trận/kết quả, chuyển mượt, tự nhỏ đi khi có chuông bo hoặc rồng. Mặc định tắt |

## 26 SFX đề xuất

| Nhóm GDD | SFX | Âm thanh tạm |
| --- | --- | --- |
| Bước chân / môi trường | bước chân cỏ, bước chân đầm lầy, ẩn vào bụi | Thiếu → tiếng bíp |
| Vũ khí / bom / bẫy | chém, trúng cận chiến, bắn, bắn nỏ, ném, nạp đạn, hết đạn, ngòi bom, bom nổ, bẫy kích hoạt | súng bắn, melee chém, Đập, bomb nổ, countdown |
| Sát thương / bị loại | hitmarker, bị trúng, giáp đỡ, bị loại | bleed; còn lại thiếu |
| Nhặt / dùng đồ | nhặt vũ khí, nhặt đồ, mặc giáp, dùng thuốc, uống, hủy dùng | nhặt súng, Gun/Helmet Put On, gauze, cola |
| Cảnh báo / kết quả | chuông bo (3 tiếng), rồng gầm, kết thúc trận | Bo co, TimeOver; rồng thiếu |

SFX còn thiếu dùng tiếng bíp tạm, mỗi cái một âm khác nhau, sinh bằng Python thư viện chuẩn, để demo phát đủ 26 sự kiện ngay.

## Demo và kiểm thử

1. Panel thử phím F6: nút phát từng SFX, thanh trượt Master/SFX/Music, số AudioSource đang dùng, nút spam 100 sự kiện (chứng minh pool bị giới hạn), nút gửi lặp sự kiện server (chứng minh không phát trùng).
2. EditMode: router chọn đúng SFX, chống lặp, giới hạn tần suất, catalog đủ 26 mã có clip, đổi âm lượng sang dB.
3. PlayMode: pool không vượt giới hạn, đổi âm lượng thì tham số mixer đổi theo.
4. Người nghe thử: hitmarker rõ, chuông bo đúng 3 tiếng, tiếng rồng nhận ra được.

## Câu hỏi cần chốt ở G1

1. Duyệt danh sách 26 SFX?
2. BGM: mặc định tắt, chỉ dựng sẵn `MusicDirector`?
3. Phạm vi nghe tiếng người khác: tối đa 12 ô (bằng tầm nhìn)?
4. Tiếng rồng: dùng tiếng tạm cho đến khi có DRG-01 hoặc ART-05?
5. Giấy phép file JSurviv: chỉ dùng làm âm thanh tạm, việc xác minh thuộc ART-05?
