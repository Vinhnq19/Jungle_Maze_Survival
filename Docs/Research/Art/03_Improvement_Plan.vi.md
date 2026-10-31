# Kế hoạch cải thiện thẩm mỹ — Lastaway

Ngày 31/10/2026 · Art Director. Căn cứ: vấn đề V1–V18 trong [01_Visual_Audit](01_Visual_Audit.vi.md), token trong [02_Art_Direction](02_Art_Direction.vi.md). Owner theo [Team_Workflow](../../Team_Workflow.vi.md) và [Agent_Coordination](../../Agent_Coordination.vi.md) mục 2.

Công sức: **S** ≤ 1 ngày · **M** 2–3 ngày · **L** khoảng 1 tuần · **XL** > 1 tuần (một dev).

Đích điểm: sau Quick wins **Tốt 4 · Mượt 2 · Hấp dẫn 2**; sau Ngắn hạn **4 · 4 · 3**; sau Trung hạn **4 · 4 · 4**.

## Giả định

1. UI-02 đang sửa `LobbyUi.prefab`, `LobbyUiPrefabBuild.cs`, `RoomManagerView.cs`, `HudSpectatorMask.cs`… trong cùng checkout. Mọi hạng mục đụng các file đó chờ UI-02 commit rồi mới làm, hoặc CPO gộp vào vòng sửa của UI-02.
2. `HudPalette.cs`, `Art/Hud/**`, `Art/UI/**`, `UiMenuArtCatalog` thuộc Codex khi ADHOC-UI-ASSETS-A còn mở; hạng mục đụng các file đó ghi owner "Codex (ADHOC)", đổi sang unity-dev nếu ADHOC đã đóng.
3. Prefab lobby/match overlay được sinh bằng builder Editor (`Tools/Editor/Lobby/LobbyUiPrefabBuild.cs`, `Tools/Editor/MatchOverlay/MatchOverlayPrefabBuild.cs`); sửa bố cục ở builder rồi build lại prefab, không chỉnh tay prefab.
4. Chưa có `10_`–`13_` từ Codex; phần Tech Art & Motion ở mục 4 do phòng tự làm ở mức đủ dùng.

## 1. Quick wins (1–3 ngày)

### QW1 · Sửa chồng lấn hộp xác nhận xóa phòng (V1)
- **Trạng thái:** ✅ xong 01/11 (AES-01): modal giữa màn có Scrim, câu hỏi ghi tên phòng. Ảnh `captures/before|after/modal_room_delete.png`. Chưa có PlayMode test overlap riêng (hộp là modal có Scrim nên không còn cần né dòng phòng).
- **Mục tiêu:** xác nhận xóa không che dòng phòng.
- **Việc:** chuyển xác nhận xóa thành modal giữa màn có Scrim (dùng chung kiểu với `LogoutConfirmView`), ghi tên phòng trong câu hỏi "Xóa phòng «Phòng 2»?". File: `RoomManagerView.cs`, `LobbyUiPrefabBuild.cs` → `LobbyUi.prefab`.
- **Owner:** unity-dev. **Phụ thuộc:** UI-02 commit. **Công sức:** S.
- **Nghiệm thu:** PlayMode test mở xác nhận, kiểm `RectTransform` của hộp không giao với bất kỳ dòng phòng nào (overlap = 0); ảnh chụp mới thay `09_room-delete-confirm.png`.
- **Rủi ro:** thấp; đụng file UI-02 đang sửa.

### QW2 · Sửa chồng lấn chế độ Theo dõi (V2)
- **Trạng thái:** ⏳ chưa chụp được: ảnh spectator cần Play Mode có trận. Không sửa MatchOverlay; để QA chụp lại khi chạy trận thật.
- **Mục tiêu:** chỉ một bộ vitals hiển thị; dải gợi ý không đè quickbar.
- **Việc:** UI-02 vòng 2 (Agent_Coordination 31/10 18:55) đã đổi `HudSpectatorMask` sang tắt cả Canvas HUD khi Killcam/Spectator, nên phần đè vitals có thể đã hết. Việc còn lại: chụp lại sau khi UI-02 commit; nếu thẻ người được theo dõi hoặc dải phím gợi ý còn đè nhau thì sửa vị trí trong `MatchOverlayPrefabBuild.cs` → `MatchOverlay.prefab`.
- **Owner:** unity-dev. **Phụ thuộc:** UI-02 commit (file đang sửa). **Công sức:** S.
- **Nghiệm thu:** PlayMode test bật spectator, kiểm không có cặp RectTransform hiển thị nào giao nhau giữa `SpectatorRow`, vitals, quickbar, hint; ảnh `32_spectator.png` mới.
- **Rủi ro:** trung bình; liên quan logic ẩn/hiện HUD của UI-03.

### QW3 · Scrim cho mọi modal (V3)
- **Trạng thái:** ✅ xong 01/11 (AES-01): Scrim `#0B1114` @0,72 cho Đăng xuất, Xóa phòng, Vào phòng, Thoát trận; hộp Thoát trận nền đặc. Ảnh `captures/`.
- **Mục tiêu:** modal tách khỏi nội dung phía sau.
- **Việc:** thêm Image Scrim `#0B1114` @0,72 toàn màn sau: Thoát trận (ESC), Đăng xuất, Xóa phòng, modal Vào phòng. Panel ESC phía sau không hiện chữ xuyên qua.
- **Owner:** unity-dev. **Phụ thuộc:** UI-02 commit. **Công sức:** S.
- **Nghiệm thu:** 4 ảnh chụp có Scrim; chặn click xuyên (test PlayMode: click vào nút phía sau không gọi handler).
- **Rủi ro:** thấp.

### QW4 · Token tương phản và nút phá hủy (V4, V14)
- **Trạng thái:** ✅ xong 01/11 (AES-02): Faint `#859E98`, nút chính `#2E7263`, thêm `RedText`, `Scrim`, sprite `ButtonDanger`; 3 nút Xóa/Đăng xuất/Thoát dùng Danger. Test `HudPaletteContrastTests`. `DangerFill` giữ giá trị cũ (đang dùng cho dải cảnh báo HUD); nền nút Danger là sprite `#2E1A1C`. `Hud.prefab` chỉ đổi giá trị Faint.
- **Mục tiêu:** mọi chữ đạt ≥ 4,5:1; hành động phá hủy khác nút chính.
- **Việc:** đổi `Faint` → `#859E98`, nền nút chính → `#2E7263`, thêm `RedText #F06A6E`, `DangerFill #2E1A1C`, `Scrim`; áp kiểu "Danger" cho Xóa, Đăng xuất (trong hộp xác nhận), Thoát trận. File: `HudPalette.cs`, `UiArtSet.cs`, builder lobby/overlay.
- **Owner:** Codex (ADHOC) cho `HudPalette`/`UiArtSet`; unity-dev cho builder. **Phụ thuộc:** QW3 (cùng builder). **Công sức:** S.
- **Nghiệm thu:** test EditMode tính tỉ lệ tương phản từng cặp (chữ, nền) khai báo trong palette, mọi cặp chữ ≥ 4,5:1; ảnh 3 nút phá hủy dùng `DangerFill`.
- **Rủi ro:** thấp; đổi màu có thể lệch art AI đã sinh theo màu cũ, cần Art Director soát lại.

### QW5 · Số dùng Be Vietnam Pro (V7)
- **Trạng thái:** 🟡 một phần (AES-02): lobby và overlay đã dùng Be Vietnam Pro Bold. HUD (`Hud*.prefab`) để lại Pixelify: đổi font làm "×6", "3s" bị cắt thành rỗng ở ô nhỏ, và test HUD quy định số dùng Pixelify. Cần Codex/chủ HUD nới ô rồi đổi.
- **Mục tiêu:** không còn nhầm 5/S, 7/ㄱ, C/0.
- **Việc:** đổi font của mọi TMP hiển thị số và chuỗi tiếng Việt từ Pixelify Sans sang Be Vietnam Pro Bold; số đổi liên tục dùng `<mspace>`. Pixelify chỉ còn ở logo. File: builder lobby/overlay, `Hud.prefab` (Codex nếu thuộc ADHOC).
- **Owner:** unity-dev + Codex (ADHOC) cho HUD. **Phụ thuộc:** không. **Công sức:** S.
- **Nghiệm thu:** script Editor liệt kê TMP dùng font Pixelify: chỉ còn node logo; ảnh `13`, `15`, `18`, `26` mới đọc đúng.
- **Rủi ro:** thấp; chiều rộng chữ đổi, cần kiểm cắt chữ.

### QW6 · Không cắt chữ, cỡ tối thiểu 14 (V9, V17)
- **Mục tiêu:** không còn "…" trong HUD và nhãn ESC không xuống dòng.
- **Việc:** tên vật phẩm ngắn tiếng Việt ≤ 10 ký tự trong catalog (cột tên ngắn), hoặc chỉ hiện tên khi hover/tooltip; nới cột nhãn ESC 160 px. Quét `m_fontSize < 14` trong prefab UI và nâng lên.
- **Owner:** game-designer (tên ngắn tiếng Việt), Codex (ADHOC) cho `HudItemIconCatalog`, unity-dev cho ESC. **Phụ thuộc:** không. **Công sức:** S–M.
- **Nghiệm thu:** test EditMode: mọi tên ngắn ≤ 10 ký tự; không TMP nào trong prefab UI có `fontSize < 14`; TMP nhãn ở ESC `isTextOverflowing == false`.
- **Rủi ro:** tên ngắn cần game-designer chốt. UI-02 vòng 2 đề xuất tên ô quickbar cỡ 13, 2 dòng; phòng giữ tối thiểu 14 và ưu tiên tên ngắn 1 dòng, Codex/CPO chốt khi làm.

## 2. Ngắn hạn (1–2 tuần)

### ST1 · Lớp motion cho UI (V5)
- **Mục tiêu:** Mượt từ 2 lên 4 ở các nhóm menu.
- **Việc:** tween alpha/vị trí trong `LobbyPanelView` theo token `panelIn`/`panelOut`/`modalIn`; component nút dùng chung cho `press`/`hover`; dùng `Time.unscaledDeltaTime`, không cấp phát mỗi frame. Tôn trọng cờ "Giảm chuyển động".
- **Owner:** tech-lead (chọn tweener), unity-dev. **Phụ thuộc:** UI-02 Done. **Công sức:** M.
- **Nghiệm thu:** test EditMode xác nhận thời lượng/easing đúng token; Profiler: 0 B GC/frame khi chuyển panel; quay màn hình 60 fps cho 3 chuyển cảnh.
- **Rủi ro:** test UI-02 hiện kiểm `Visible` qua alpha > 0,5; tween có thể làm test cần đợi, cần chế độ `instant` trong test.

### ST2 · Nền thống nhất và không khí (V8)
- **Mục tiêu:** mọi màn ngoài trận có rừng pixel.
- **Việc:** một prefab nền dùng chung (3 lớp parallax hiện có + lớp tối 40% khi cần đọc chữ) cho Phòng chờ, Tải, Kết quả, Mất kết nối; thêm hạt đom đóm ngọc (≤ 30 hạt, chậm, tắt khi Giảm chuyển động).
- **Owner:** unity-dev; Codex (ADHOC) nếu sprite nền thuộc `Art/UI/**`. **Phụ thuộc:** ST1 không bắt buộc. **Công sức:** M.
- **Nghiệm thu:** ảnh 11–19 mới có nền rừng; Profiler màn Phòng chờ: thời gian render UI tăng < 0,5 ms so với trước (đo trước/sau).
- **Rủi ro:** hạt làm rối nếu nhiều; giới hạn số hạt.

### ST3 · Dàn dựng màn Kết quả (V6)
- **Mục tiêu:** Hấp dẫn nhóm H từ 2 lên 4.
- **Việc:** tiêu đề Display 72 với `emphasis`; khối 3 số lớn (Hạng, Hạ gục, Sống sót); bảng xếp hạng stagger 40 ms; ánh ngọc bùng (thắng) / sương tím (thua); chỗ cho key art nhân vật (placeholder tới MT1).
- **Owner:** game-designer (số hiển thị, nguồn dữ liệu hạ gục), tech-lead, unity-dev. **Phụ thuộc:** dữ liệu hạ gục có ở client (màn Bị loại đã có "Hạ gục"); ST1. **Công sức:** M–L.
- **Nghiệm thu:** chuỗi vào màn ≤ 1,2 s tổng; nút Chơi lại bấm được sau ≤ 600 ms; playtest 5 người, câu 5 khảo sát trung bình ≥ 3,5.
- **Rủi ro:** số hạ gục ở màn kết quả có thể cần server gửi thêm; nếu vậy tách phần số liệu.

### ST4 · Trạng thái chờ có tiến trình (V13)
- **Việc:** spinner `spinner` cho Đang kết nối, Đang tải, Đang kết nối lại; thanh tiến trình theo 3 bước tải.
- **Owner:** unity-dev. **Phụ thuộc:** ST1. **Công sức:** S.
- **Nghiệm thu:** mọi màn chờ có phần tử chuyển động liên tục; test kiểm spinner tắt khi rời màn.
- **Rủi ro:** thấp.

### ST5 · Trạng thái trống và bảng Lịch sử (V11, V12)
- **Việc:** trạng thái trống/tạm ngừng có minh họa 256×256 (placeholder tới art AI), một câu giải thích, một nút hành động ("Vào phòng", "Thử lại"); Lịch sử thành bảng cột Thời gian · Hạng · Sống sót · Kết thúc, hạng #1 màu Amber.
- **Owner:** unity-dev; minh họa: Codex/art AI. **Phụ thuộc:** UI-02 Done. **Công sức:** M.
- **Nghiệm thu:** 6 ảnh 20–25 mới; mỗi trạng thái trống có đúng một nút hành động; dòng bảng cao 44.
- **Rủi ro:** thấp.

### ST6 · Đo nền (baseline)
- **Việc:** quét cỡ chữ prefab; Profiler `Game.unity` 10 người 60 s (trung bình, p99 frame time, GC UI); playtest nhìn-hiểu 5 người + khảo sát 5 câu.
- **Owner:** qa-tester; phòng soạn phiếu. **Phụ thuộc:** không. **Công sức:** M.
- **Nghiệm thu:** báo cáo `Docs/Reports/` có đủ 3 bảng số; điền cột "chưa đo" trong `01_Visual_Audit` mục 2.
- **Rủi ro:** cần đủ người playtest; thiếu thì dùng 3 người và ghi rõ.

## 3. Trung hạn (3–6 tuần)

### MT1 · Key art nhân vật cho Home, Phòng chờ, Kết quả
- **Việc:** art AI-render theo brief `02` mục 6 (một nhân vật, 3 tư thế); gắn vào slot key art trong `UiMenuArtCatalog`; Home hiển thị nhân vật cạnh nút chính.
- **Owner:** Codex/art AI, Art Director duyệt; unity-dev gắn slot. **Phụ thuộc:** ST3 (slot), ART-01 (Hero) để thống nhất ngoại hình. **Công sức:** L.
- **Nghiệm thu:** checklist `02` mục 6.2 đạt 7/7; khảo sát câu 4 ≥ 4,0.
- **Rủi ro:** lệch phong cách so với sprite Hero trong trận; duyệt cạnh nhau trước khi gắn.

### MT2 · Bị loại và Killcam có dàn dựng (V15)
- **Việc:** chuyển cảnh sương tím 400 ms khi bị loại; thẻ kẻ hạ gục gồm tên + icon vũ khí; killcam hiển thị sprite nhân vật thay cho chấm, khung viền phim.
- **Owner:** unity-dev (UI-03 tiếp nối). **Phụ thuộc:** icon vũ khí ADHOC xong; sprite Hero ART-01. **Công sức:** L.
- **Nghiệm thu:** playtest: ≥ 4/5 người nói đúng "ai hạ mình, bằng gì" sau 3 giây.
- **Rủi ro:** killcam phụ thuộc dữ liệu server.

### MT3 · Game-feel giao tranh
- **Việc:** phối hợp VFX-01: hit flash 80 ms, số sát thương `damageNumber`, rung màn theo giới hạn `02` mục 5, sương tím ở mép bo, vignette máu thấp.
- **Owner:** Vũ (VFX-01) runtime; unity-dev phần HUD; phòng chuẩn token. **Phụ thuộc:** VFX-01, CLI-03 (pool). **Công sức:** XL.
- **Nghiệm thu:** theo tiêu chí VFX-01 + 0 B GC/frame cho số sát thương (pool); p99 frame time không tăng quá 1 ms so với baseline ST6.
- **Rủi ro:** quá nhiều hiệu ứng che tầm nhìn; đặt trần số hiệu ứng đồng thời.

### MT4 · Khung UI chất liệu đá (pack UI-P01)
- **Việc:** thay khung phẳng bằng sprite 9-slice đá than chì mép vát theo [UI_Asset_Packs](../../Art/UI_Asset_Packs.vi.md).
- **Owner:** Codex (ADHOC). **Phụ thuộc:** QW4 (màu cuối). **Công sức:** L.
- **Nghiệm thu:** checklist `02` mục 6.2; ảnh 36 màn chụp lại không vỡ 9-slice ở mọi kích thước panel.
- **Rủi ro:** 9-slice sai border làm méo góc.

### MT5 · Cài đặt "Giảm chuyển động"
- **Việc:** thêm cờ vào Cài đặt (UI-03), mọi motion đọc cờ này.
- **Owner:** unity-dev. **Phụ thuộc:** ST1. **Công sức:** S.
- **Nghiệm thu:** bật cờ: không có scale/trượt, mọi fade ≤ 150 ms (test EditMode duyệt token).
- **Rủi ro:** thấp.

### MT6 · Hậu kỳ URP cho ánh ngọc
- **Việc:** Volume toàn cục: vignette nhẹ, color grading tông chạng vạng, bloom ngưỡng cao chỉ cho vật phát sáng ngọc.
- **Owner:** unity-dev (tech art). **Phụ thuộc:** ST6 baseline, ENV/MAP map thật. **Công sức:** M.
- **Nghiệm thu:** ảnh so trước/sau; GPU frame time tăng ≤ 1 ms trên máy tham chiếu (ghi cấu hình máy).
- **Rủi ro:** bloom làm nhòe pixel art; chỉ bật cho layer riêng.

## 4. Phần Tech Art & Motion (thay cho `10_`–`13_` khi chưa có Codex)

| Phần | Hiện trạng | Việc tối thiểu |
| --- | --- | --- |
| Kiểm kê asset (10) | [asset_manifest.csv](../../Art/asset_manifest.csv) có 73 dòng (64 UI + 9 World); chuẩn icon 32×32 trong [CODEX_TASK_32px_Restyle](../../Art/CODEX_TASK_32px_Restyle.md) | Dùng manifest làm kiểm kê; thêm cột "trạng thái duyệt thẩm mỹ" khi Codex mở lại |
| Motion (11) | Lobby bật/tắt tức thì; HUD feedback có view riêng (`Hud/Feedback/*View.cs`), chưa xem chạy thật | Token ở `02` mục 5; ST1, ST4, MT5 |
| Render/perf (12) | Chưa đo | ST6 baseline; ngưỡng: UI không thêm GC/frame, p99 frame time không tăng quá 1 ms mỗi hạng mục |
| Backlog tech art (13) | — | Atlas sprite UI theo màn (giảm draw call), Filter Point cho mọi sprite pixel, nén không mất màu cho icon; đo draw call UI trước/sau trong ST6 |

## 5. Ticket đề xuất cho CPO

Tiền tố `AES` (Aesthetics), không trùng ID đang có trong GDD.

| ID gợi ý | Tiêu đề | Phạm vi | Gộp từ | Ưu tiên |
| --- | --- | --- | --- | --- |
| AES-01 | Sửa chồng lấn và thêm scrim cho modal | Xác nhận xóa phòng thành modal; spectator không đè vitals/quickbar; scrim cho 4 modal | QW1, QW2, QW3 | P0 |
| AES-02 | Token tương phản, nút phá hủy, font số | Đổi 3 màu, thêm 3 token; kiểu Danger; số dùng Be Vietnam Pro; test tương phản | QW4, QW5 | P0 |
| AES-03 | Không cắt chữ, cỡ chữ tối thiểu | Tên ngắn tiếng Việt; quét fontSize ≥ 14; ESC không xuống dòng | QW6 | P1 |
| AES-04 | Lớp motion UI và chỉ báo chờ | Tween panel/modal/nút theo token; spinner, thanh tải | ST1, ST4 | P1 |
| AES-05 | Nền rừng thống nhất và đom đóm | Prefab nền chung cho màn ngoài trận | ST2 | P1 |
| AES-06 | Dàn dựng màn Kết quả | Tiêu đề, 3 số lớn, stagger, ánh ngọc/sương tím | ST3 | P1 |
| AES-07 | Trạng thái trống và bảng Lịch sử | Minh họa + CTA; bảng cột | ST5 | P2 |
| AES-08 | Đo nền thẩm mỹ và hiệu năng UI | Cỡ chữ, Profiler, playtest + khảo sát | ST6 | P1 |
| AES-09 | Key art nhân vật | Brief AI, 3 tư thế, gắn Home/Phòng chờ/Kết quả | MT1 | P2 |
| AES-10 | Bị loại và Killcam dàn dựng | Sương tím, thẻ kẻ hạ, sprite nhân vật | MT2 | P2 |
| AES-11 | Khung UI chất liệu đá | Pack UI-P01 | MT4 | P2 |
| AES-12 | Giảm chuyển động và hậu kỳ URP | Cờ cài đặt; Volume vignette/grading/bloom | MT5, MT6 | P3 |

Game-feel giao tranh (MT3) không tách ticket mới: đưa tiêu chí của phòng vào VFX-01.
