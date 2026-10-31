# Audit thẩm mỹ UI — Lastaway

Ngày 31/10/2026 · Art Director (kiêm UI/UX Researcher, Player Researcher). Thang chấm theo [00_Charter](00_Charter.vi.md) mục 3.

## 1. Nguồn và cách làm

- 34 ảnh Unity 1920×1080 trong `Docs/Figma/Screens_2026-10-31/` (danh mục `index.txt`), xem ở cỡ 640×360 ghép 4 ảnh/tấm.
- 5 màn luồng mới trong `Docs/UX/Round2_Captures/` (đăng nhập, home, xác nhận đăng xuất, cài đặt máy chủ, modal vào phòng).
- Figma `oeZgisxdiR80hRqbikUwPb`, trang `116:3` (36 frame, hàng A–J), chụp `get_screenshot` 800 px toàn trang và frame `03_home` (`121:2`). Không dùng `use_figma` nên không cần ghi gì vào Figma.
- Quy tắc tham chiếu: skill `ui-ux-pro-max` (tương phản 4,5:1, easing theo mục đích, không animation vô hạn trang trí, phong cách pixel-art cần chữ tương phản và giảm chuyển động).
- Code đọc để kiểm motion: `Presentation/UI/Lobby/LobbyPanelView.cs` (panel bật/tắt bằng `CanvasGroup.alpha` 0/1), `Presentation/UI/Hud/HudPalette.cs` (màu).

**Giới hạn:** ảnh HUD/ESC/Bị loại là lớp UI trên nền xanh phẳng, không có world, nhân vật, VFX. Không vào Play mode (ràng buộc phòng), nên mọi điểm "Mượt" trong trận dựa vào code và ảnh tĩnh, chưa xem chuyển động thật.

## 2. Số đo đã có

Tương phản WCAG tính từ hex trong `HudPalette.cs` và [A_Recolor_Review](../../Art/A_Recolor_Review.vi.md):

| Cặp màu | Dùng ở | Tỉ lệ | Đạt 4,5:1 (chữ thường)? |
| --- | --- | ---: | --- |
| Text `#EDF2EE` / Surface `#202A2D` | Chữ chính trên panel | 12,96 | Đạt |
| Muted `#A6BBB7` / Surface | Nhãn phụ | 7,28 | Đạt |
| Green `#63BAA5` / Surface | "Bắt đầu sau 12 s", "Còn 24 s" | 6,36 | Đạt |
| Amber `#C6AE78` / Surface | Mã phòng, hạng, cảnh báo | 6,80 | Đạt |
| **Faint `#77908A` / Surface** | "API sẵn sàng", gợi ý, chữ muted | **4,29** | **Không** |
| **Text `#EDF2EE` / nút chính `#3B8D7B`** | Nhãn mọi nút chính | **3,51** | **Không** (chỉ đạt mức 3:1 cho chữ lớn) |
| **Red `#E5484D` / Surface** | Nhãn "Thoát trận", lỗi mã phòng | **3,75** | **Không** cho chữ nhỏ |

Chưa đo (ghi cách đo):

| Chỉ số | Cách đo |
| --- | --- |
| Cỡ chữ nhỏ nhất thực tế | Đọc `fontSize` của mọi TMP_Text trong `LobbyUi.prefab`, `Hud.prefab`, `MatchOverlay.prefab` (script Editor chỉ đọc hoặc grep YAML `m_fontSize`); chuẩn ≥ 16 px ở 1080p cho chữ đọc, ≥ 14 px cho nhãn phụ |
| Thời gian chuyển màn | Đọc code: hiện là 0 ms (alpha 0/1). Sau khi thêm motion: ghi thời lượng token trong test EditMode |
| fps / frame time UI | Unity Profiler, cảnh `Game.unity`, 10 người, 60 s; ghi trung bình và p99 frame time |
| Thời gian nhìn-hiểu | Playtest 5 người, hỏi "màn này làm gì" sau 5 giây, đếm số trả lời đúng |
| Cảm nhận | Khảo sát 5 câu ([00_Charter](00_Charter.vi.md) mục 6) |

## 3. Chấm điểm theo nhóm màn

Điểm cho bản hiện tại (Unity), đã tính các sửa luồng của [Flow_Proposal](../../UX/Flow_Proposal.vi.md) cho nhóm A–C vì Figma đã có và UI-02 đang làm.

| Nhóm | Màn | Tốt | Mượt | Hấp dẫn | Ghi chú chính |
| --- | --- | :-: | :-: | :-: | --- |
| A · Đăng nhập/Đăng ký | 06, 07; R2 01 | 3 | 2 | 2 | Luồng rõ, lỗi ngay dưới ô. Hộp phẳng giữa màn, không key art |
| B · Home | 01; R2 03–05 | 3 | 2 | 3 | Logo pixel + rừng pixel tạo bản sắc. Nút Đăng xuất trong hộp xác nhận dùng kiểu nút chính |
| C · Vào phòng, Kết nối | 02–05; R2 06 | 3 | 2 | 2 | Lỗi cụ thể, dễ sửa. "Đang kết nối (UDP)" chỉ có chữ tĩnh, không chỉ báo tiến trình |
| D · Quản lý phòng | 08–10 | **2** | 2 | 2 | Xác nhận xóa đè chữ lên dòng "Phòng 2" (lỗi bố cục). Nửa panel trống |
| E · Phòng chờ, Tải | 11–14 | 3 | 3 | 2 | Có đếm ngược, danh sách 10 chỗ, tag "Bạn". Nền xanh phẳng, mất rừng pixel; không có nhân vật |
| F · HUD, ESC | 26–30 | **2** | 3 | 2 | Nhãn bị cắt ("Spiked Ar…", "Crossb…"). ESC: hộp "Thoát trận?" nằm trên bảng cài đặt không có scrim, chữ phía sau lộ ra |
| G · Bị loại, Killcam, Theo dõi | 31–34 | **2** | 3 | 2 | Theo dõi: thẻ máu người được theo dõi đè lên vitals của mình, dải gợi ý đè lên quickbar. Killcam là lưới debug + 2 chấm |
| H · Kết quả | 15–17 | 3 | 2 | **2** | Khoảnh khắc thưởng quan trọng nhất nhưng chỉ có chữ vàng trên hộp xám; "#1 / 5" nhỏ; không có số hạ gục |
| I · Mất kết nối | 18, 19 | 3 | 3 | 2 | Có đếm giờ, có lối về. Nền tối phẳng |
| J · Lịch sử, Bảng xếp hạng | 20–25 | **2** | 2 | **1** | Lịch sử là một dòng chữ nhỏ dồn mọi cột; trạng thái trống là panel lớn trống, không minh họa |

**Kết luận 3 trục (trung vị nhóm): Tốt 3 · Mượt 2 · Hấp dẫn 2.**
Đọc nhanh: luồng và thông tin đã đúng; game chưa "chuyển động" và chưa có art, nên trông như bản công cụ hơn là game.

## 4. Bảng vấn đề

Mức độ: **Chặn** (lỗi hiển thị sai/che thông tin) · **Cao** (ảnh hưởng mọi người chơi hoặc màn chủ chốt) · **Trung bình** · **Thấp**.

| # | Mức | Vấn đề | Ảnh hưởng | Bằng chứng |
| --- | --- | --- | --- | --- |
| V1 | Chặn | Hộp xác nhận xóa phòng đè chữ lên dòng phòng, nút "Sao chép" mờ dưới chữ | Người chơi không chắc đang xóa phòng nào | `09_room-delete-confirm.png` |
| V2 | Chặn | Chế độ Theo dõi: thẻ máu người được theo dõi đè lên vitals của mình; dải phím gợi ý đè lên quickbar | Hai bộ số máu chồng nhau, đọc sai. UI-02 vòng 2 đã tắt Canvas HUD khi Spectator (chưa commit, cần chụp lại) | `32_spectator.png` |
| V3 | Cao | Hộp "Thoát trận?" mở trên bảng cài đặt không có scrim; thanh trượt và nhãn phía sau lộ qua | Rối, giảm tập trung vào quyết định phá hủy | `30_leave-confirm.png` |
| V4 | Cao | Nhãn nút chính tương phản 3,51:1; chữ Faint 4,29:1; chữ đỏ nhỏ 3,75:1 | Khó đọc, nhất là màn hình sáng/laptop | Mục 2 |
| V5 | Cao | Chuyển màn tức thì (alpha 0/1), không fade/slide; nút không có trạng thái nhấn rõ trong ảnh | Cảm giác giật, "rẻ" | `LobbyPanelView.cs` dòng 18 |
| V6 | Cao | Màn Kết quả thắng/thua không có dàn dựng: không hiệu ứng, không nhân vật, không số hạ gục | Mất khoảnh khắc thưởng, giảm động lực chơi lại | `15_results-victory.png`, `17_results-defeat.png` |
| V7 | Cao | Font pixel dùng cho số và chuỗi tiếng Việt gây nhầm: "Còn" đọc thành "0òn", "5" giống "S", "7" giống "ㄱ" | Đọc sai thời gian, hạng, số máu | `13_loading.png`, `18_reconnecting.png`, `15_results-victory.png`, `26_hud-in-match.png` |
| V8 | Cao | Nền không nhất quán: menu có rừng pixel, Phòng chờ/Tải/Kết quả/Mất kết nối dùng nền phẳng | Mất bản sắc ở đúng các màn chờ lâu nhất | `11`–`19` |
| V9 | Trung bình | Nhãn vật phẩm bị cắt và lẫn tiếng Anh ("Bare H…", "Spiked Ar…", "Wooden …", "Unicorn…") | Khó nhận vật phẩm; lẫn ngôn ngữ | `26_hud-in-match.png` |
| V10 | Trung bình | Icon vật phẩm và minimap là placeholder (lưới trống) | HUD thiếu điểm nhìn, khó đánh giá | `26`–`28` |
| V11 | Trung bình | Trạng thái trống/tạm ngừng là panel trống lớn với một dòng chữ nhỏ | Trông như lỗi, không gợi hành động | `21`, `22`, `24`, `25` |
| V12 | Trung bình | Lịch sử trận gộp mọi thông tin thành một chuỗi chữ nhỏ, không cột, không icon hạng | Khó quét | `20_history.png` |
| V13 | Trung bình | Màn Đang kết nối chỉ có chữ tĩnh, không chỉ báo tiến trình | Người chơi không biết game có treo không | `05_connecting.png` |
| V14 | Trung bình | Hành động phá hủy ("Đăng xuất", "Xóa") dùng kiểu nút chính xanh ngọc, giống "Vào phòng" | Giảm tín hiệu nguy hiểm | R2 `04_logout-confirm.png`, `09` |
| V15 | Trung bình | Killcam chỉ là lưới + vòng bo + 2 chấm | Không truyền được "ai hạ mình, bằng gì" bằng hình | `33_killcam.png` |
| V16 | Thấp | Panel Cài đặt máy chủ đè lên logo ở Home | Bố cục chưa gọn | R2 `05_settings-server.png` |
| V17 | Thấp | Nhãn ESC xuống 2 dòng ("Âm lượng / tổng", "Độ nhạy / chuột") | Lệch hàng với thanh trượt | `29_esc-menu.png` |
| V18 | Thấp | Nhãn quay lại còn lẫn "Về menu"/"Về sảnh"/"Quay lại" ở ảnh Unity | Đã có cách sửa trong Flow_Proposal #8, chờ UI-02 | `16`, `32` |

## 5. Điểm mạnh nên giữ

1. **Palette hướng A** (than chì, xanh ngọc, vàng dịu) nhất quán ở mọi màn; màu ngữ nghĩa riêng (đỏ máu, xanh giáp, vàng Adrenaline, tím vòng bo) dễ nhận.
2. **Nút chính** xanh ngọc viền vàng: một nút chính mỗi màn, dễ tìm.
3. **Logo LASTAWAY pixel** và **nền rừng pixel nhiều lớp**: đây là hạt giống bản sắc, nên mở rộng ra mọi màn.
4. **Thông báo lỗi cụ thể và có lối thoát**: "Mã chỉ gồm chữ A–Z (trừ I, O) và số 2–9", "Phòng đã đủ 10 người", "Không kết nối lại được: bạn đã bị loại".
5. **Phòng chờ**: danh sách 10 chỗ, tag "Bạn", mã phòng lớn có nút Sao chép.
6. **HUD**: nhóm VŨ KHÍ / DƯỢC PHẨM, keycap phím, đồng hồ bo màu tím, máu đổi đỏ khi thấp; bố cục bốn góc chuẩn thể loại.
7. **Bị loại**: thứ hạng lớn, ai hạ, bằng gì, khoảng cách, số hạ gục — đủ thông tin.
8. **Hộp xác nhận** cho mọi hành động phá hủy đã có.

## 6. So sánh với game tham chiếu

Nhận định định tính từ hiểu biết chung về các game, không phải số đo.

| Khía cạnh | surviv.io | ZombsRoyale | Brawl Stars | Bullet Echo | Lastaway hiện tại |
| --- | --- | --- | --- | --- | --- |
| Góc nhìn / thể loại | 2D top-down BR, web | 2D top-down BR, hoạt hình | Top-down 3v3, mobile | Top-down chiến thuật, tầm nhìn hình nón | 2D top-down BR pixel, PC |
| HUD | Tối giản, máu giữa đáy, ô đồ rõ | Màu tươi, icon lớn | Rất lớn, nút cảm ứng | Gọn, tối, ít chữ | Bốn góc, chữ nhỏ, icon tạm |
| Menu / lobby | Đơn giản, nhân vật mặc skin ở giữa | Nhân vật nổi bật, sự kiện, emote | Nhân vật là trung tâm, nhiều chuyển động | Thẻ hero, nền tối | Hộp nút, không có nhân vật |
| Game-feel | Hit marker, rung nhẹ, âm thanh rõ | Hiệu ứng tươi | Rất "juicy": nút nảy, số bay, rung | Bắn có trọng lượng, sáng tối rõ | Có view phản hồi HUD, chưa xem chạy thật |
| Thắng / thua | Màn "Winner winner" kèm thống kê | Ăn mừng, nhảy | Chuỗi thưởng nhiều bước | Thống kê + hero | Chữ vàng trên hộp xám |
| Bản sắc | Phẳng, dễ đọc, khó nhớ | Cartoon tươi | Rất mạnh, nhận ra ngay | Tối, tactical | Pixel rừng + jade: có tiềm năng, chưa được khai thác |

Bài học áp dụng:
- **Từ surviv.io:** HUD tối giản, số lớn, chữ nằm trên nền tối đặc. Giữ HUD gọn, tăng cỡ số.
- **Từ Brawl Stars:** mọi nút có phản hồi nhấn (scale 0,95), khoảnh khắc thưởng được dàn dựng nhiều nhịp. Học có chọn lọc, không làm "kẹo" quá tay vì PC.
- **Từ ZombsRoyale và Brawl Stars:** đưa nhân vật lên Home và Phòng chờ; nhân vật là bản sắc.
- **Từ Bullet Echo:** tông tối, ánh sáng có chủ đích, tầm nhìn tạo căng thẳng; hợp với palette than chì và FOW-01.

## 7. Lịch sử điểm

| Ngày | Tốt | Mượt | Hấp dẫn | Ghi chú |
| --- | :-: | :-: | :-: | --- |
| 31/10/2026 | 3 | 2 | 2 | Audit đầu, 34 màn Unity + 5 màn luồng mới + Figma 116:3 |
