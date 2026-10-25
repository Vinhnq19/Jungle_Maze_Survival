# Retry log — QA UI-01 P2

Quy tắc người dùng: tối đa3vòng cho mỗi lỗi; tới3 thì ghi lại để sửa sau và chuyển việc. Không vượt giới hạn. Không chạy lại bộ test xanh.

| Issue | Vòng / kết quả | Quyết định |
| --- | --- | --- |
| Probe reflection compile | 1: `Screen.width/height` bị hiểu thành namespace Screen; 2: dùng đầy đủ `UnityEngine.Screen`, probe thành công | Đã giải quyết, không đổi source production |
| HudPreview lifecycle sau reload | 1: Bootstrap tồn tại nhưng IsComposed=false, HUD Presenter=null, frame cũ; 2: Stop rồi Play sạch, frame2, IsComposed=true và Presenter=true | Đã hồi, không cưỡng bức Awake/Start/Construct để che lỗi |
| Seed snapshot compile | 1: gọi property không có `DangerBannerView.Prefix`; 2: đọc source thật và dùng `Label`, seed thành công | Đã giải quyết, không đổi production |
| ScreenCapture stale khi Unity mất focus | 1: frame2 không tiến, ảnh cũ dù dữ liệu port đã đổi; 2: tạm `Application.runInBackground=true`, frame4144, ảnh HUD phản hồi cập nhật và đủ ba kích thước | Đã giải quyết; khôi phục runInBackground=false sau đo |
| Hint dùng đồ biến mất (FB11/FB47) | 1: phát hiện trên ảnh thật, rect112×16 < preferredHeight16,45, parsed/vertices rỗng; 2: dev sửa aa7d36b8, delta review PASS; QA đo height18, parsed đủ, overflow=false và chụp lại ba size | Đã giải quyết ở vòng2; QA không sửa production/test |
| Dọn custom GameView size | 1: guard tên/count quá chặt nên bỏ qua khi có hai entry QA; 2: RemoveCustomSize dùng custom index0/1 không xoá; 3: truyền total index đúng8/7, còn đúng7mục ban đầu, không còn QA size | Đã giải quyết ở vòng3, không thử thêm. QA_Cleanup.json ghi kết quả cuối |

Ảnh khởi động/stale trong `Attempts/` không được dùng làm bằng chứng đạt. Các ảnh `*_final.png` ở thư mục chính được giữ như **before-fix** cho lỗi Hint; tên final chỉ là tên capture trong lượt đó, không phải kết luận nghiệm thu.
