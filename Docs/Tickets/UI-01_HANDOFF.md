# UI-01 — Bàn giao để hoàn tất ticket (24/10/2026)

Mục tiêu duy nhất: **hoàn tất ticket UI-01 (HUD & Feedback FX)**. Không làm ticket khác.

UI-01 gồm hai phần:

| Phần | File | Trạng thái |
| --- | --- | --- |
| Phần lõi (HUD: Vitals, Quick bar, Minimap, Match status, 3 state + mock) | `Docs/Tickets/UI-01.md` | **Done**, G3 ngày 24/10/2026. Còn 3 việc dọn nhỏ + 2 việc người dùng tự kiểm (xem A) |
| Phần 2 (lớp phản hồi: dùng đồ, hiệu ứng, nhặt đồ, hit/nhận sát thương, cảnh báo bom/bẫy) | `Docs/Tickets/UI-01-P2.md` | **Đã duyệt G1**, điều kiện "chờ G3 phần lõi" đã thoả → **sẵn sàng build** (xem B) |

Nhánh `huycv`, HEAD sau merge main: EditMode 193/193, PlayMode 20/20, console sạch. Quyết định khó suy ra từ code: `.claude/memory/ui-01-hud.md`. Cách đẩy dữ liệu lên HUD: `Docs/Client_Developer_Guide.vi.md` mục 4.7.

## Quy tắc của người dùng (bắt buộc)

1. **Commit** xong phần nào tự commit phần đó; message tiếng Anh ngắn kiểu `feat(ui-01): …`; **không** thêm Co-Authored-By hay nhắc AI/Claude/Codex. Chỉ `git add` đúng file + `.meta`, commit kèm đường dẫn `git commit -m "…" -- <files>`. Không push nếu người dùng không bảo.
2. **Không đụng vùng của thành viên khác** (Hào: Hero/map/balance; Vinh: backend, Phase 2, `MatchSession`, `MatchDemoView`). Chỉ thêm móc nối tối thiểu, hoặc hỏi trước.
3. **Không commit** 3 file `ProjectSettings/*` đang sửa dở từ trước (`EditorBuildSettings`, `ProjectSettings`, `ShaderGraphSettings`). Thư mục `.agents/` chưa track không thuộc UI-01.
4. `ProjectSettings/EditorSettings.asset` phải giữ `m_EnterPlayModeOptions: 0`. Chạy PlayMode test qua Unity MCP làm Unity ghi lại `1` → sau mỗi lần chạy: `git checkout -- ProjectSettings/EditorSettings.asset`.
5. **Art là bản tạm** (sau này AI render): màu phẳng theo style guide `39:3`, ô icon giữ chỗ; màu/sprite/font chỉ đặt trên prefab + `HudPalette`, không đưa vào code.
6. Không AWS, không deploy; test mạng dùng GameServer local.
7. Quy trình: build → review → QA → dừng ở **G3** hỏi người dùng nghiệm thu (bảng AC ✓/✗/chưa kiểm chứng kèm bằng chứng). Xem `Docs/Team_Workflow.vi.md`.

## A. Phần lõi — việc còn lại

Dọn nhỏ (một commit `docs`/`chore`):

1. `Docs/Tickets/UI-01.md` mục 8 và `.claude/memory/ui-01-hud.md` "Giới hạn còn lại": đổi câu `EditorSettings.asset … chờ người quyết` thành "đã revert về 0 theo người dùng; chạy PlayMode qua MCP sẽ đổi lại, cần revert sau mỗi lần".
2. `Docs/Client_Developer_Guide.vi.md` mục 5: số test sau merge main là **EditMode 193/193**, PlayMode 20/20 (đang ghi 181). Cập nhật lại khi phần 2 thêm test.
3. `Assets/_JungleMaze/Scenes/Game.unity`: bỏ khoảng trắng cuối Unity thêm ở 3 dòng `m_Name` (kiểm `git diff c37a8673 -- Assets/_JungleMaze/Scenes/Game.unity`). Chỉ 3 dòng đó.

Người dùng tự kiểm (đã chấp nhận kiểm sau):

4. **AC21**: `Scenes/Dev/HudPreview.unity` → Play → F4 → "HP 100→0 /10", "Heal 0→100 /10".
5. **AC16 phần Release**: build Release, xác nhận không có panel F4/mock (`#if UNITY_EDITOR || DEVELOPMENT_BUILD`).

## B. Phần 2 — build tới G3

1. Đọc `Docs/Tickets/UI-01-P2.md` toàn bộ: spec mục 1–3 (AC-FB…), câu hỏi đã trả lời ở G1, **kế hoạch mục 4** (bảng file, commit), "Điểm nối với phần lõi". Wireframe: `Docs/Wireframes/v2/48-239_hud_feedback.png`, `48-461_hud_feedback_annotated.png`; `Docs/UI_Wireframes.vi.md` mục 5.1.
2. Đổi dòng Trạng thái của `UI-01-P2.md` sang `Build`, ghi vào mục 8 rằng điều kiện G3 phần lõi đã đạt (24/10/2026).
3. Build theo mục 4: logic C# thuần + EditMode test trước, rồi view/prefab qua Unity MCP, rồi PlayMode test. Dữ liệu đi qua ba port sẵn có (`IHudState`, `IInventoryState`, `IEffectState`, ở `Assets/_JungleMaze/Scripts/Application/Hud/State/`); mở rộng bằng cách thêm vào port/`EffectKind` như kế hoạch, mock thêm preset cho F4. Không sửa khối HUD lõi ngoài "Điểm nối với phần lõi".
4. Ghi mục 5 "Nhật ký build" (lệch kế hoạch + lý do, hash commit, số test).
5. Tự review theo `Docs/Team_Workflow.vi.md` mục 6 (lớp, Bootstrap/Inject, mock chỉ trong Editor/Development, 0 GC khi không đổi) → ghi mục 6. Sửa lỗi Chặn/Nên sửa, tối đa 2 vòng.
6. QA từng AC → mục 7 (tên test, số đo, ảnh trong `Docs/Reports/UI-01-P2_<ngày>/`). Đánh dấu AC phải kiểm lại khi có art thật.
7. Dừng ở **G3**, trình người dùng. Khi nghiệm thu: ghi mục 8, Trạng thái `Done`, cập nhật `.claude/memory/ui-01-hud.md` (thêm quyết định phần 2), `Client_Developer_Guide` mục 4.7 nếu có port mới, và nhắc người dùng cập nhật Status UI-01 trong sheet PROJECT TRACKING.

## Ghi nhận, không làm trong UI-01

Kiểm lại AC phụ thuộc art khi có AI render; tên món tiếng Việt (Q10, đổi schema CLI-04); nối `MatchSession` → `IHudStateWriter` (WPN-02/ZON-01/Phase 2); lỗi thoát Play lúc đang kết nối báo `[Scene] Không load được Boot`; mục 6 `Client_Developer_Guide` lỗi mã hoá dấu (commit của Vinh).

## Kiểm tra nhanh

- Unity Test Runner: EditMode `JungleMaze.Tests.EditMode`, PlayMode `JungleMaze.Tests.PlayMode` (HUD: `Tests/EditMode/Hud/`, `Tests/PlayMode/HudAcceptanceTests.cs`).
- Thử HUD không cần server: `Scenes/Dev/HudPreview.unity` + F4.
