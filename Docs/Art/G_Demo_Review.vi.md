# G · Biên giới Ngọc — demo art mới

Ngày: 24/10/2026. Trạng thái: **người dùng không chọn — quá chi tiết; chưa thay asset Unity**.

Phản hồi tiếp theo của người dùng: "Nó bị quá quá chi tiết rồi. Bạn cần liên kết với figma đi". Dừng hướng G; demo kế tiếp cần đơn giản hơn và bám thiết kế Figma hiện tại. Plugin Figma đã kết nối và đọc được file; ghi frame mới bị từ chối do read-only. Xem [H_Demo_Review.vi.md](H_Demo_Review.vi.md) và demo H. File thiết kế tham chiếu trong bàn giao: `oeZgisxdiR80hRqbikUwPb`, HUD v2 `41:2`, feedback `48:239`, style guide `39:3`.

Người dùng không ưng các demo trước và yêu cầu demo mới, rồi sau phần art build UI-01 phần 2 theo kế hoạch đã duyệt, review → QA → dừng G3.

- Demo: [G_jade_frontier.png](directions/G_jade_frontier.png).
- Prompt đầy đủ: [G_jade_frontier.prompt.txt](directions/G_jade_frontier.prompt.txt).
- Công cụ: ImageGen tích hợp, tạo ảnh mới. Ảnh gồm gameplay ở trên và menu ở dưới.
- Hướng đề xuất: pixel art rừng rậm, nền UI xanh ngọc, góc viền đồng mảnh, chữ ngà. Bố cục triển khai vẫn phải bám Figma v2 và các AC đã duyệt.

## Giới hạn của demo

Đây là ảnh concept, không phải ảnh chụp Unity hay bằng chứng QA. Góc nhìn nhân vật, tỷ lệ khối HUD và chữ còn cần chuẩn hoá khi triển khai. Các con vật, món đồ và cảnh vật minh hoạ không thêm gameplay, item hay phạm vi ticket. Tên và số liệu vật phẩm thật phải lấy từ GDD/manifest. Tiếng Việt trong UI thật giữ Be Vietnam Pro.

Chưa ghi hướng G là hướng đã chọn vào Asset_Pipeline.vi.md, chưa đổi trạng thái manifest sang done. Chờ người dùng chọn/chỉnh concept trước khi sinh asset sản xuất theo CODEX_HANDOFF.md mục 0.

## Chuẩn bị UI-01 phần 2

Đã đọc UI-01_HANDOFF.md, UI-01-P2.md và Team_Workflow.vi.md. G1 phần 2 đã duyệt, G3 lõi ngày 24/10/2026 đã đạt, không cần duyệt lại G1. Unity MCP kết nối được; chưa chạy test, chưa bắt đầu build phần 2 trong đợt demo.

Kiểm tra read-only của tech-lead:

- HudRoot.Palette đã có; không cần thêm getter như kế hoạch cũ.
- Hai store HUD/inventory cần ITickable và đăng ký tick trong Bootstrap.
- Cập nhật hai store và fake cùng lượt khi mở rộng port.
- Test lõi dựng prefab chỉ inject HudRoot; kiểm regression khi thêm HudFeedbackRoot.
- AC-FB19 yêu cầu tám hiệu ứng nhưng enum chỉ có bảy loại lên chip: dùng dữ liệu synthetic để kiểm overflow, không tự thêm hiệu ứng gameplay.
- Cần snapshot phần tử trước khi store xoá để giữ bóng chip và bom 0s.

Sau khi art được chọn: thay asset theo manifest, kiểm palette/PPU/9-slice, chạy test liên quan; tiếp tục build phần 2 theo 11 commit mục 4.6, review và QA tối đa hai vòng sửa, trình G3 với AC và bằng chứng. Chỉ đánh dấu Done sau khi người dùng nghiệm thu. Không push; giữ các ProjectSettings đang sửa và .agents/.codex ngoài commit, phục hồi EditorSettings sau PlayMode.
