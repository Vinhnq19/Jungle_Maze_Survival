# Nghiệm thu năm task nền tảng Unity — 23/10/2026

Phạm vi: CLI-02 (serializer), CLI-03 (base/pattern), CLI-04 (balance config), MOV-01 (WASD/aim), ENV-01 (grid/Hard Cover). Dùng Unity **6000.3.15f1**, giữ URP và source JSurviv. Chưa bắt đầu MOV-02 hoặc combat multiplayer.

## 1. Thay đổi và tiêu chí

| Task | Hoàn thiện | Cách xác nhận |
| --- | --- | --- |
| CLI-02 — Serializer | Giữ ba packet struct, NetPacketProcessor và shared package; transport chỉ gọi registry. Không đổi wire format | Round-trip, frame, packet hỏng, nhiều packet và **10.000 vòng serialize/deserialize không GC sau warm-up** |
| CLI-03 — Base/pattern | Giữ Bootstrap duy nhất, DI, EventBus, StateMachine, ObjectPool/PoolService/Factory. Bổ sung ví dụ chạy thật cho đạn và impact VFX | Test DI/Observer/State/pool; bắn thử lấy từ pool, tự trả khi hết tầm/trúng cover; tái sử dụng cùng instance qua bốn cover |
| CLI-04 — Config | Match/18 weapons/16 equipments từ JSON. `ConfiguredShot` đọc tốc độ, tầm, damage metadata và nhịp từ provider; collider áp dụng cả khi inject sau Awake | Đổi JSON trong bản sao tạm rồi tạo lại provider: tốc độ 18→36, range/nhịp thay đổi; PlayMode xác nhận đạn đi 0,9→1,8 cell trong 0,05 giây |
| MOV-01 — Hero | Chuột được chiếu lên mặt phẳng Hero, không lấy tâm màn hình. Giữ đường chéo chuẩn hóa, 4 cell/s và aim exponential smoothing 0,08s; Rigidbody interpolate/continuous; camera theo Hero và clamp viewport | Test aim bốn hướng khi Hero lệch camera; physics đo đi chéo 4 cell/giây, radius đổi 0,4→0,65 sau Awake; kiểm ổn định khi đứng yên |
| ENV-01 — Map/cover | Grid 50×50, texture 48 PPU, một renderer tiled; bốn biên solid. Bốn prefab cover có hình hiển thị khớp collider. `CoverQuery` dùng chung cho đạn và LOS sau này | Kiểm kích thước/PPU; Hero bị chặn ở các biên và cover; raycast trúng cover; đạn quét cả bước lớn, không xuyên dù bước vượt toàn bề dày cover |

Layout hiện tại phục vụ nghiệm thu vật lý gồm bốn loại cover quanh vùng spawn; chưa phải nội dung mê cung hoàn chỉnh hoặc bản đồ cân bằng cho trận đấu. FOW/shader sẽ dùng lại collider/query ở task riêng.

## 2. Kiểm thử tự động

Mở **Window → General → Test Runner**:

1. Tab **EditMode**, chạy assembly `JungleMaze.Tests.EditMode`. Lượt 23/10: **125/125 PASS**, không skip; bao gồm serializer 0 GC và các kiểm tra cũ của client/network/account use case.
2. Tab **PlayMode**, chạy assembly `JungleMaze.Tests.PlayMode`. Lượt cuối: **5/5 PASS**, không skip. Tổng hai suite: **130/130 PASS**. Kết quả từng test nằm trong [dữ liệu nghiệm thu](Reports/Local_Capacity_Retest_2026-10-14/unity-foundation-tests.json).
3. Console phải không có lỗi compile, missing script hay exception ngoài dự kiến. Một số test chủ động gây lỗi dùng fake logger/LogAssert, không phải lỗi vận hành.

Test physics dùng prefab thật và Unity Physics2D ở chế độ simulation thủ công để đo lặp lại chính xác; tốc độ không phụ thuộc máy chạy test nhanh/chậm. Mỗi test khôi phục simulation mode và dọn object/pool, chỉ sửa bản sao JSON trong thư mục temp. Không sửa dữ liệu balance gốc và không mở EC2.

## 3. Chạy thử bằng người chơi

1. Chạy GameServer local theo [Client Developer Guide, mục 1](Client_Developer_Guide.vi.md#1-chạy-thử-trong-5-phút) hoặc hướng dẫn Docker Phase 1. Giữ mã phòng đúng với server của bạn.
2. Mở scene `Assets/_JungleMaze/Scenes/Boot.unity` → Play → chọn Local → nhập endpoint/mã phòng → **Vào phòng**. Sau JoinAccepted, scene `Game` được tải.
3. Click Game View. Dùng WASD/mũi tên: đi thẳng, đi chéo, ép vào cả bốn vật cản. Hero phải dừng trước mặt collider; đi đến mép map thì dừng trong biên.
4. Rê chuột quanh Hero, nhất là sát mép map khi camera không còn đặt Hero ở tâm: hướng Hero phải trỏ về chuột, không giật về góc 0 khi chuột nằm ngay trên Hero.
5. Giữ chuột trái: đạn vàng bị cover/biên chặn, có impact ngắn và được thu hồi; không có damage/combat. Để xem trọn map, dừng Play và dùng Scene View; Game View có camera theo Hero.
6. Stop Play, đổi `RUBBER_GUN.projectileSpeed` 18→36 trong `weapons.json`, Play/join lại: tốc độ bắn thử đổi mà không sửa script. Đổi nhịp qua `fireRatePerSecond`, tầm qua `range`. Khôi phục JSON sau khi thử.

Lượt kiểm tra này đã join **GameServer thật trên loopback 127.0.0.1:17777** bằng cấu hình chỉ giữ trong RAM, quan sát scene và aim bằng chuột, ghi nhận một đạn trúng cover và 16 projectile đã prewarm. Server thử được dừng khi kết thúc; không đổi endpoint lưu trên máy, không bật/deploy AWS.

**Giới hạn thao tác tay:** công cụ Windows chỉ gửi được lần nhấn phím ngắn, chưa xác nhận riêng trải nghiệm giữ WASD kéo dài. Chuyển động/va chạm/tốc độ đã được đo qua cùng phương thức kinematics trong PlayMode; phần cảm giác điều khiển với người chơi thật nên thực hiện theo bước 3–4 trước buổi demo. Không ghi phép đo tự động thành thử nghiệm bàn phím kéo dài.

## 4. Vị trí code và ranh giới phase sau

- `Backend/Shared/Protocol/Gameplay/`: schema và serializer dùng chung.
- `Assets/_JungleMaze/Scripts/Core/`, `Infrastructure/Pooling/`, `Application/Pooling/`, `Bootstrap/`: bộ nền.
- `Infrastructure/Config/BalanceConfigProvider.cs`: bộ nạp JSON một lần khi Bootstrap tạo; không hot reload hay tự cập nhật Google Sheets.
- `Application/Player/ConfiguredShot.cs`: consumer config độc lập transport.
- `Presentation/Player/HeroController.cs`, `HeroAim.cs`, `HeroCameraFollow.cs`: MOV-01 local.
- `Presentation/World/MapGridView.cs`, `HardCoverTag.cs`, `CoverQuery.cs`: grid/cover.
- `Presentation/World/LocalWeaponProbe.cs`, `CoverProbeProjectile.cs`, `Prefabs/Diagnostics/`: công cụ nghiệm thu, có thể tắt component LocalWeaponProbe trong scene khi không dùng.

Không kết luận multiplayer từ scene này: mỗi Hero vẫn chạy mô phỏng local, đạn thử không gửi mạng, `damage` chỉ là metadata. Movement authoritative, snapshot/prediction/reconciliation MOV-02, combat đầy đủ, WSS Unity, map art/maze chi tiết và FOW là công việc tiếp theo. Kết quả backend/WSS/capacity trước đây trong báo cáo chung giữ nguyên phạm vi đo của chúng.
