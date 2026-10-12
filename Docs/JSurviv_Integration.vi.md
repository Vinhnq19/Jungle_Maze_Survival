# JSurvivUnity đã nhập vào Jungle Maze Survival

Mã nguồn và assets tham khảo hiện nằm tại `Assets/Imported/JSurviv`; thư mục nguồn riêng `JSurvivUnity` không còn trong workspace hiện tại. Dự án gốc tiếp tục dùng Unity **6000.3.15f1** và URP. Phần nhập assets chưa chuyển Socket.IO sang LiteNetLib và chưa cung cấp demo gameplay offline hoàn chỉnh.

Backend mới và AWS được mô tả trong [Network_Architecture.vi.md](Network_Architecture.vi.md). Setup, phát triển, chạy lại server và test theo [hướng dẫn vận hành chung](Phase1_First4Tasks.vi.md); Unity client hiện chưa được nối với backend LiteNetLib đó. Tài liệu này chỉ hướng dẫn sử dụng assets/scene tham khảo.

## Mở và kiểm tra

Mở project gốc bằng Unity Hub. Đợi Package Manager và lần import đầu hoàn thành. Nếu editor đã mở trước khi đổi Active Input Handling thành **Both**, khởi động lại editor khi Unity yêu cầu để cấu hình input có hiệu lực đầy đủ.

Các scene tham khảo:

- `Assets/Imported/JSurviv/Scripts/Scenes/GameConfig.unity`: UI cấu hình cũ.
- `Assets/Imported/JSurviv/Scripts/Scenes/GameMain.unity`: scene chính, lobby/gameplay tham khảo.
- `Assets/Imported/JSurviv/Scripts/Scenes/GameMainTrial.unity`: scene thử của nguồn.
- `Assets/Imported/JSurviv/Scripts/Scenes/CreateMap.unity`: công cụ/scene tạo map cũ.

`GameConfig` và `GameMain` được thêm sau scene gốc trong danh sách build. `SampleScene` vẫn là scene đầu. Các build profile trong vùng nhập đã đổi đường dẫn đến scene mới.

Trong `NetworkManager`, **Auto Connect Legacy** mặc định tắt. Game View hiển thị chế độ tham khảo, không tự gửi request đến server cũ. Lobby không bắt đầu trận nếu không có backend/dữ liệu người chơi; đây là hành vi dự kiến, không phải demo offline. Chỉ bật Auto Connect Legacy khi chủ động thử backend Socket.IO phù hợp.

## Module có thể tái sử dụng

Các đường dẫn trong bảng tính từ `Assets/Imported/JSurviv/Scripts`.

| Module | Vị trí | Cách tái sử dụng |
| --- | --- | --- |
| Pooling | `[Codebase]/ObjectPool` | Tham khảo pool, quản lý đối tượng và VFX; kiểm tra reset/release trước dùng trong game mới |
| Command | `[Codebase]/Command`, `Command` | Tách hành động move/attack/pickup khỏi input; giữ lớp mô phỏng mới độc lập với transport |
| State | `Game` | Tham khảo Enter/Tick/Exit và luồng màn hình; state cũ phụ thuộc manager/network, cần adapter khi tái dùng |
| Config | `[Codebase]/Configs`, `[Concrete]/Configs` | ScriptableObject cho player/map/weapon/effect; config asset nằm trong Configs và Resources |
| Nhân vật | `[Codebase]/Entity/Player` | Animation, visual, armor, weapon, effect; partial class và singleton cũ cần tách dần khi port |
| Vũ khí/đạn | `[Codebase]/Entity/Weapon`, `[Codebase]/Entity/Projectile`, `[Concrete]/Entities/Projectile` | Tham khảo cơ chế melee/ranged/throwable; damage authoritative mới vẫn thuộc server GDD |
| Effect | `[Codebase]/EffectSystem`, `[Concrete]/Effects` | Tái dùng cơ chế thời gian và trạng thái; kiểm tra các tham chiếu PlayerEntity |
| Item/inventory | `[Codebase]/Entity/Item`, `Screen/Managers/ItemManager.cs`, các partial Player | Tham khảo UI và quy tắc; API đổi inventory qua Socket.IO không dùng trực tiếp cho LiteNetLib |
| Map/môi trường | `Map`, `Obstacles`, `Screen/Managers/MapManager.cs` | Tái sử dụng tile, prefab, obstacle và cấu hình phân bố |
| UI | `Screen`, `Screen/Components` | Tái dùng layout/prefab; chuyển event dữ liệu sang nền tảng game mới |
| Mạng cũ | `Screen/Managers/Network`, `Game`, `Screen/Managers/LobbyManager.cs` | Giữ để tham khảo; transport là Socket.IO/WebSocket và schema cũ, không phải LiteNetLib |

Giữ namespace và assembly `SurvivTCP` để prefab/scene tiếp tục tìm script. Không tạo thêm class trùng tên đầy đủ trong code mới. Khi đưa một module thành nền tảng mới, tạo adapter hoặc namespace riêng thay vì đổi đồng loạt tên class đang được scene tham chiếu.

## Dependency và cấu hình đã hợp nhất

- Cinemachine 3.1.3.
- SocketIOUnity khóa commit `c9e06b15391449ad42fd9b0f39fea48054751bcd`.
- Newtonsoft.Json 3.2.2; giữ package Unity hiện có của project gốc.
- UniTask, DOTween và GameDevWare.Serialization từ thư mục Plugins nguồn.
- Assembly `DOTween.Modules` cung cấp các extension cho game assembly; bridge Addressables không biên dịch khi Addressables chưa được cài.
- Input Both; tag Item/Projectile/Weapon/Border; layer nguồn giữ chỉ số; sorting layer giữ ID.
- URP của project gốc được giữ. Material trong vùng nhập được xử lý riêng, texture/màu/blend được giữ khi chuyển.

## Kiểm chứng và chạy lại

Đã kiểm chứng trong Unity **6000.3.15f1** ngày 12/10/2026: Console không có lỗi, 759 prefab không có missing script, Resources tải được cả hai config và 127 material đã chuyển sang URP. Bốn scene tham khảo và scene gốc mở được. UI/font/sprite của GameConfig và GameMain hiển thị; ảnh kiểm tra không có material hồng. Kết quả và các lỗi có sẵn được ghi tại [JSurviv_Verification.vi.md](JSurviv_Verification.vi.md).

**GameMainTrial còn 4 missing script từ nguồn**, CreateMap và một số nội dung mẫu còn tham chiếu asset bị thiếu từ trước. Không xóa component hay tự đoán asset thay thế vì có thể làm mất dữ liệu tham khảo. Ưu tiên GameMain để xem UI chính.

Lỗi compile có sẵn trong nguồn đã xử lý ở bản nhập: `ThrowableEntity.canRotate` dùng `[SerializeField]` trên property. Bản nhập chuyển attribute sang backing field và thêm `FormerlySerializedAs("canRotate")` để giữ giá trị prefab cũ. Bản nguồn không bị sửa.

Menu Unity: **Tools → JSurviv → Convert Imported Materials and Validate**. Công cụ chỉ chuyển material trong vùng nhập; mở bốn scene tham khảo và SampleScene, kiểm tra missing script/reference, Resources và tạo ảnh đối chiếu. Không lưu thay đổi scene tạm thời dùng để chụp ảnh.

Kết quả máy đọc được tại `Library/JSurvivIntegration/validation.json`; ảnh tại cùng thư mục. Script sau là đối chiếu lịch sử với nguồn `JSurvivUnity/Assets` và snapshot `Library/JSurvivIntegration/Backup`, chạy từ **thư mục gốc project Unity**, chỉ khi hai phần đối chiếu còn tồn tại:

```powershell
python Tools/validate_jsurviv_import.py
```

Workspace hiện không còn thư mục nguồn `JSurvivUnity`, nên script đối chiếu này không phải bước setup/test thường ngày. Để kiểm tra vùng assets nhập hiện tại, dùng menu Unity ở trên; muốn đối chiếu nguồn gốc thì cần bản nguồn/snapshot phù hợp từ bản lưu riêng. Không đưa lại nguồn cũ vào Assets hoặc ghi đè bản nhập chỉ để chạy kiểm tra.

Snapshot các thiết lập trước nhập nằm tại `Library/JSurvivIntegration/Backup`; thư mục Library là cache nên không thay thế Git backup lâu dài. Các script chỉ phục vụ lần tích hợp và file kết nối tạm đã được dọn; giữ công cụ kiểm chứng để kiểm tra lại khi cần.

Không đánh dấu task gameplay/multiplayer Done chỉ vì scene compile được. Bước tiếp theo là chọn module cần port, nối input/config/event nền tảng mới và triển khai networking theo GDD.
