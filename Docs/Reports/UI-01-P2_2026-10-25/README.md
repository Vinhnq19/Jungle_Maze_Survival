# UI-01 P2 — QA độc lập ngày 25/10/2026

Source full QA: `eaf33030`, sau formal review vòng 1 PASS; delta Hint `aa7d36b8` đã review PASS và QA kiểm lại hẹp. Chỉ QA, không sửa production/test. Chưa nghiệm thu G3 và chưa Done.

## Kết quả hiện tại

- Fresh EditMode: **247/247 pass, 0 fail, 0 skip**, job `61831ed7e78f43b79c6c2c932ac258f8`, 9,45 s. Raw: [QA_EditMode.json](QA_EditMode.json).
- Fresh PlayMode: **37/37 pass, 0 fail, 0 skip**, job `c23dd21b37f442e2bff84b704793ca27`, 15,36 s. Raw: [QA_PlayMode.json](QA_PlayMode.json).
- Console sau hai bộ test: **0 error/warning**. Khi vào Play thủ công có warning hạ tầng MCP về port 6400 bận và fallback 6401; không dùng các warning này làm bằng chứng lỗi gameplay.
- Bảng đủ 51 AC: [QA_AC51.json](QA_AC51.json) và mục 7 ticket. **50 dòng kỹ thuật đạt trong phạm vi ghi rõ; FB50 chờ PIC cảm nhận.** FB11/FB47 đã sửa và kiểm lại thành công. FB1 chưa kiểm chứng stripping trên Release player.

## Lỗi thực tế phát hiện ngoài bộ test

**FB11/FB47 — Hint dùng đồ không render.** Trong HudPreview, bơm God Leaf qua port với total 0,5 s, remaining 0,25 s, cancellable=true. Model `Hint=true`, TMP `enabled=true`, object active; nhưng chuỗi `Di chuyển để hủy` có `preferredHeight=16,45` trong rect `112×16`, overflow Ellipsis. Kết quả `GetParsedText()` rỗng, characterCount/vertexCount = 0, `isTextOverflowing=true`. Bộ test hiện chỉ kiểm enabled/width nên vẫn xanh. Không sửa code để né lỗi; đã chuyển CPO/dev sửa hẹp và chờ QA lại.

Raw trước sửa: [QA_Visual_Measurements_BeforeFix.json](QA_Visual_Measurements_BeforeFix.json). Delta `aa7d36b8` tăng height Hint từ16 lên18, giữ font13, strip/bar/timer/root. QA sau sửa đo parsedText đầy đủ, preferredHeight16,45 ≤18, overflow=false; [PostFix_Hint_Measurements.json](PostFix_Hint_Measurements.json), [PostFix_Visual_Measurements.json](PostFix_Visual_Measurements.json). Ảnh sau sửa xác nhận chữ hiện. Issue được giải quyết ở vòng2. [RetryLog](RetryLog.md) ghi các sự cố probe/capture đã gặp.

Postfix PlayMode job `af9e52c2cbae4260b2208000b0b15df6` được dev chạy sau sửa; [raw cache](PostFix_PlayMode.json). QA không chạy lại full247/37 hay GC vì chỉ đổi builder/prefab/test; QA recheck hẹp trên view thật.

## Ảnh Game view thật — sau sửa, dùng khi nghiệm thu

- [1280×720](PostFix_HudPreview_1280x720.png)
- [1920×1080](PostFix_HudPreview_1920x1080.png)
- [2560×1440](PostFix_HudPreview_2560x1440.png)
- [Panel F5 và banner Đông Bắc12ô+15](PostFix_F5Panel_2560x1440.png)

Pillow xác nhận đúng pixel từng PNG. TMP probe Hint chạy ngay sau đổi GameView nên field Screen trong PostFix_Hint_Measurements.json còn647×404; đó là số đo layout/font bằng canvas unit trước frame resize, **không** dùng để chứng minh kích thước PNG. Kích thước PNG thật được kiểm độc lập. Panel F5 là component Bootstrap thực tế; chỉ bật trường visible để chụp, chưa kiểm thao tác phím thật. Banner dài parsed đầy đủ `! Bom nổ sau 2s · hướng Đông Bắc, 12 ô +15`, ba TMP đều overflow=false. Actual màu Amber F2A33AFF/Red E5484DFF/Green5DD39EFF/HitFFFFFFFF và đúng vai trò font đã lưu trong PostFix_Visual_Measurements.json.

## Ảnh Game view thật — trước sửa Hint

- [1280×720](QA_HudPreview_1280x720_final.png)
- [1920×1080](QA_HudPreview_1920x1080_final.png)
- [2560×1440](QA_HudPreview_2560x1440_final.png)

Kích thước pixel PNG đã kiểm bằng Pillow. Capture bằng Unity MCP `manage_camera`, `capture_source=game_view`, **không chỉ định camera** để ScreenCapture giữ Screen Space Overlay. Đây là scene **HudPreview**, có nền H/SpriteRenderer, HUD và dữ liệu mock; không phải cảnh gameplay Game/map hay phiên chơi server. ConnectionMenuView tạm tắt trong Play để không che HUD; F4/F5 đã có trên Bootstrap. Snapshot frozen `Time.timeScale=0`; HudRoot/HudFeedbackRoot LateUpdate tạm tắt sau khi tick thủ công bằng port thật. Không lưu các thay đổi runtime này vào scene.

God Leaf snapshot: total `0,5`, remaining `0,25`, fill `0,5`, text thực `Đang dùng God Leaf`, timer parsed `0.3s`. Số 3 của Pixelify có thể trông giống 8 khi ảnh thu nhỏ; raw seed ghi đúng 0.3s. Poison 3 s, Slow không thời lượng, Invisible 5 s; ba dòng nhặt và banner Tây/20 ô chỉ minh hoạ dữ liệu nguồn. Icon item vẫn placeholder theo ranh giới ART-04. Đối chiếu bố cục với `Docs/Wireframes/v2/48-239_hud_feedback.png`; nội dung demo theo spec/GDD thay vì chữ mẫu Figma.

`Attempts/` lưu ảnh khởi động/stale trước khi khắc phục việc Unity mất focus; không dùng làm bằng chứng đạt bố cục/phản hồi.

## Đo GC riêng lớp feedback trên view thật

Raw: [QA_Feedback_GC.json](QA_Feedback_GC.json). Probe tái lập: [QA_GC_Probe.cs.txt](QA_GC_Probe.cs.txt).

- API `GC.GetAllocatedBytesForCurrentThread`, delegate được tạo trước đo, warm-up **120 tick**.
- **Idle 6000 tick:** tổng **0 B**, trung bình **0 B/tick**.
- **Timer đang chạy 600 tick:** tick HudStateStore/InventoryStateStore/EffectStateStore và HudFeedbackPresenter nối prefab view thật; tổng **0 B**, max **0 B/tick**, **0/600** tick có allocation. Remaining dùng đồ đi từ 117,9996 xuống 107,9974 s.
- Duration 120 s trong phép đo là đầu vào tổng hợp để timer không hết giữa vòng đo, không phải thời gian God Leaf trong gameplay/GDD.
- Có marker `QA.HudFeedback.Idle.RealViews` và `QA.HudFeedback.ActiveTimers.RealViews`.
- **Giới hạn:** phép đo managed allocation đồng bộ của store/presenter/view setter; không gồm Canvas rebuild bị hoãn sau probe, toàn bộ Editor frame, GPU hay Release player. Không kết luận tổng GC Unity bằng 0. Stress PlayMode `Burst60s_NoErrorsOrMissingReferences` đã qua trong fresh 37/37.

## Trạng thái dọn dẹp / chờ kiểm lại

Đã thoát Play, giữ HudPreview mở. Khôi phục GameView selected index2, xoá các custom size QA1280×720, danh sách về đúng7mục ban đầu; `Time.timeScale=1`, `captureDeltaTime=0`, `Application.runInBackground=false`, `EditorSettings.m_EnterPlayModeOptions=0`. Raw [QA_Cleanup.json](QA_Cleanup.json). Không sửa ProjectSettings khác, không đụng `.codex/config.toml` của người dùng. Console cuối0error, chỉ2warning MCP suppressed-duplicate transport: [QA_Console_Final.json](QA_Console_Final.json). Không chạy lại toàn bộ suite hay GC khi App logic không đổi. Editor đã bàn giao về CPO, HudPreview sẵn sàng mở cho PIC.

FB50 cần PIC xem F5 preset Dồn và Giao tranh ở G3; QA không thay PIC quyết cảm giác đọc/chớp. Chưa kiểm input F5 bằng thao tác phím thật hay Release mock stripping trên player build. Không cần GameServer/backend cho scope mock-only này.
