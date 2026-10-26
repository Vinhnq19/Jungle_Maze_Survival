# UI-01 — HUD & Feedback FX

## Hoàn tất phần 2 — G3 ngày 25/10/2026

- Người dùng nghiệm thu bằng "Oke rồi" sau báo cáo G3; UI-01 toàn ticket Done. Ticket chi tiết: `Docs/Tickets/UI-01-P2.md`; báo cáo: `Docs/Reports/UI-01-P2_2026-10-25/`. EditMode 247/247, PlayMode 37/37; review PASS và delta Hint `aa7d36b8` PASS, QA `33620171`.
- Giữ ba port; thêm event feedback tách khỏi Changed lõi để không vẽ lại cả quickbar. Store tick thời gian; CompleteUse do nguồn quyết định. Không ghi HP/khe/số lượng từ presenter; không suy hướng đối thủ.
- Vòng biên chữ nhật theo góc thật (T-21); chấp nhận ngoại lệ chồng khoảng 15 px ở 195°–212° đã duyệt. Adrenaline không thành chip. F5 riêng, F4 lõi giữ nguyên, mock chỉ Editor/Development.
- H là art được chọn. Cell48 là mask dùng chung, phải trắng; Ground96 riêng, MapGridView chỉ floor dùng Sprite optional (user duyệt), giữ walls/collider/fallback. HudPreview chỉ renderer art, không thêm map gameplay.
- Buffer danger ưu tiên active thay ghost; ghost mới không nhận ngược delta của frame expiry. Chữ banner bố trí preferredWidth; pickup hai dòng font13 trong row31; Hint112×18 vì112×16 làm Ellipsis cắt sạch dù enabled=true. Test phải kiểm parsed glyph/height, không chỉ enabled/width.
- Người dùng yêu cầu tối đa3vòng cho mỗi lỗi, tới giới hạn thì log để sửa sau. Các issue QA không vượt3vòng; RetryLog có bằng chứng. Không chạy lặp fullsuite xanh khi delta không ảnh hưởng.
- GC realviews đo 0B cho idle6000/active600 Tick sau warmup120; chưa gồm deferred Canvas rebuild/toàn frame. Release stripping trên player và thao tác phím F5 thật chưa kiểm chứng. G3 chấp nhận phạm vi này, không biến giới hạn thành phép đo đã qua.
- Icon thật/catalog item thuộc ART-04, gameplay/network thật do ITM-01/Game-10/WPN/FOW ghi vào writer ports; chưa làm các ticket đó. Không push nếu chưa được yêu cầu.

- **Trạng thái**: Done toàn UI-01; lõi G3 ngày 2026-10-24, phần 2 G3 ngày 2026-10-25. Các giới hạn Release và đo GC toàn frame vẫn giữ trong báo cáo.
- **Ticket**: `Docs/Tickets/UI-01.md` (spec, kế hoạch, nhật ký build, review, QA, nghiệm thu). Bằng chứng QA: `Docs/Reports/UI-01_2026-10-24/`.
- **Phần 2** (phản hồi: tiến trình dùng đồ, buff/debuff, thông báo nhặt, sát thương, bom/bẫy, hướng nguy hiểm): `Docs/Tickets/UI-01-P2.md`, đã nghiệm thu.

## Quyết định không suy ra được từ code

1. **Bố cục theo Figma v2, không theo v1.** HUD `41:2`, chú thích `41:180`, style guide `39:3` (file `oeZgisxdiR80hRqbikUwPb`). Frame v1 `3:2` vẽ trên khung PUBG (squad, 100 người) nên bỏ. Nguyên tắc: Figma quyết bố cục, GDD quyết nội dung và số liệu.
2. **Vitals rộng 296 px, không phải 300.** Người dùng thu lại trong Figma để Vitals chỉ chạm (x = 320), không giao vùng giữa sạch 320,180 · 640×360.
3. **Ba trạng thái tách riêng `IHudState` / `IInventoryState` / `IEffectState`**, mỗi cái có port ghi và store mặc định. Lý do: dòng UI-01 mới trong GDD online yêu cầu "dùng mock HUDState, InventoryState, EffectState để demo khi gameplay chưa có" và "tách lớp UI binding khỏi nguồn dữ liệu". Dùng store (không để mỗi ticket tự implement port đọc) vì một trạng thái có nhiều chủ ghi: HP từ WPN-02, đạn từ WPN-01, bo từ ZON-01, còn sống từ SRV-03.
4. **`EffectState` là danh sách có thời lượng, tra theo `EffectKind`**, mảng cố định 16 mục. Phần 2 thêm buff/debuff bằng cách thêm giá trị enum, không đổi port.
5. **Match status (Q13 = b):** UI-01 chỉ hiển thị giá trị nguồn gửi, không tự đếm bo. Mẫu số "Còn sống" là số người lúc vào trận (trận 8 người hiện "7/8"), không cố định `maxPlayers`.
6. **Tên món dùng `displayName` tiếng Anh tạm (Q10).** Tên tiếng Việt cần thêm trường vào balance JSON (đổi schema CLI-04), là ticket riêng chưa có ID. Hệ quả đã biết: tên dài bị cắt "…" ở 13 px.
7. **Art là bản tạm** (người dùng: "các phần UI chúng ta sẽ triển khai dùng AI render sau cứ làm tạm thời trước"). Màu, sprite, font chỉ gán trên `Hud.prefab` và `HudPalette`; thay art thì sửa prefab rồi chạy "Apply palette" trên `HudRoot`. AC phải kiểm lại khi có art thật: AC1, AC14, AC17, AC20, AC21, AC22, AC24, AC26.
8. **Font:** Be Vietnam Pro cho chữ, Pixelify Sans chỉ cho số và phím (thiếu 92/178 chữ tiếng Việt, fallback sang `HudText SDF`). Pixelify là variable font nên TMP chỉ lấy weight Regular. Không đi tìm bản static. Giấy phép SIL OFL 1.1, file OFL nằm cạnh font.
9. **Chữ HUD tối thiểu 13 px**, không auto-size xuống thấp hơn (quy tắc HUD 5 của style guide).
10. **Canvas con cho Vitals và Minimap** để thanh nội suy và marker đổi mỗi frame không làm rebuild cả HUD. HUD không có `GraphicRaycaster`, mọi Graphic `raycastTarget = false` để không chặn chuột ngắm.
11. **Test PlayMode không load scene Game** (vì `MainMenuState` đẩy về Boot khi chưa join). Test dựng prefab trực tiếp; thử tay dùng `Scenes/Dev/HudPreview.unity` (không có trong Build Settings) + F4.

## Giới hạn còn lại

- `PlayerStatePacket.Hp` là `byte` và `JoinAccepted` chưa có NetId local: WPN-02 phải xử lý trước khi gọi `SetHp`.
- Chạy PlayMode qua Unity MCP làm Unity tự đổi `ProjectSettings/EditorSettings.asset` `m_EnterPlayModeOptions` 0→1. Người dùng chọn giữ 0: không commit thay đổi này, sau mỗi lần chạy PlayMode qua MCP phải `git checkout -- ProjectSettings/EditorSettings.asset`.
- Lỗi có sẵn ngoài UI-01: thoát Play lúc đang kết nối ở scene Game thì báo `[Scene] Không load được Boot`.
