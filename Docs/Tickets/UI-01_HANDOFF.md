# UI-01 — Bàn giao (24/10/2026)

Bàn giao cho phiên làm việc tiếp theo (Codex). Đọc file này trước, rồi `Docs/Tickets/UI-01.md` khi cần chi tiết.

## Trạng thái

- **UI-01 phần lõi: Done**, người dùng nghiệm thu G3 ngày 24/10/2026. Chi tiết và bằng chứng: `Docs/Tickets/UI-01.md` mục 7–8, `Docs/Reports/UI-01_2026-10-24/`.
- Nhánh `huycv`, đã push. HEAD = `428b8722` (merge `origin/main` vào `huycv`). Sau merge: EditMode 193/193, PlayMode 20/20, console sạch.
- Quyết định khó suy ra từ code: `.claude/memory/ui-01-hud.md`. Cách đẩy dữ liệu lên HUD: `Docs/Client_Developer_Guide.vi.md` mục 4.7.
- **UI-01 phần 2** (`Docs/Tickets/UI-01-P2.md`) đã duyệt G1. Phiên CPO khác điều phối, không thuộc bàn giao này trừ khi người dùng giao.

## Quy tắc của người dùng (bắt buộc)

1. **Commit**: xong phần nào tự commit phần đó; message tiếng Anh ngắn kiểu `feat(ui-01): …`; **không** thêm dòng Co-Authored-By hay nhắc AI/Claude/Codex. Chỉ `git add` đúng file + `.meta`, commit kèm đường dẫn: `git commit -m "…" -- <files>` (index có thể dùng chung với phiên khác). Không push nếu người dùng không bảo.
2. **Không đụng vùng của thành viên khác** (Hào: Hero/map/balance; Vinh: backend, Phase 2, `MatchSession`, `MatchDemoView`). Chỉ thêm móc nối tối thiểu, hoặc hỏi trước. Không sửa tiện tay.
3. **Không commit** 3 file `ProjectSettings/*` đang sửa dở từ trước (`EditorBuildSettings`, `ProjectSettings`, `ShaderGraphSettings`).
4. `ProjectSettings/EditorSettings.asset`: người dùng muốn giữ `m_EnterPlayModeOptions: 0`. Chạy PlayMode test qua Unity MCP làm Unity ghi lại `1` — sau mỗi lần chạy, `git checkout -- ProjectSettings/EditorSettings.asset`.
5. Art HUD là **bản tạm**, sau này thay bằng AI render: màu/sprite/font chỉ đặt trên `Prefabs/UI/Hud.prefab` + `HudPalette`, không đưa vào code.
6. Không dùng AWS, không deploy. Test mạng dùng GameServer local (`dotnet run`).

## Việc còn lại

**Việc nhỏ, làm được ngay (một commit `docs`/`chore`):**

1. `Docs/Tickets/UI-01.md` mục 8 và `.claude/memory/ui-01-hud.md` mục "Giới hạn còn lại": đổi câu `EditorSettings.asset … chờ người quyết` thành "đã revert về 0 theo người dùng; chạy PlayMode qua MCP sẽ đổi lại, cần revert sau mỗi lần".
2. `Docs/Client_Developer_Guide.vi.md` mục 5: số test sau UI-01 + merge main là **EditMode 193/193**, PlayMode 20/20 (hiện ghi 181).
3. `Assets/_JungleMaze/Scenes/Game.unity`: Unity thêm khoảng trắng cuối ở 3 dòng `m_Name` (khoảng dòng 523, 553, 597 — kiểm bằng `git diff c37a8673 -- Assets/_JungleMaze/Scenes/Game.unity`). Bỏ để giảm xung đột khi Hào merge. Chỉ sửa 3 dòng đó.

**Người dùng tự làm:**

4. Cập nhật Status UI-01 trong sheet PROJECT TRACKING (GDD online).
5. AC21: xem `Scenes/Dev/HudPreview.unity` → Play → F4 → "HP 100→0 /10", "Heal 0→100 /10".
6. AC16 phần Release: build Release xác nhận không có panel F4/mock.

**Ticket khác / sau này (chỉ ghi nhận, không tự làm):**

7. Kiểm lại AC1, AC14, AC17, AC20, AC21, AC22, AC24, AC26 khi có art thật.
8. Tên món tiếng Việt trong balance JSON (Q10, đổi schema CLI-04) — ticket riêng chưa có ID; sẽ hết lỗi tên tiếng Anh bị cắt "…".
9. Nối dữ liệu thật: `MatchSession` (main) đã có HP/bo/NetId → nên ghi vào `IHudStateWriter`/`IEffectStateWriter`; hiện `MatchDemoView` hiển thị song song với HUD. Thuộc WPN-02/ZON-01 hoặc người làm Phase 2.
10. Tinh chỉnh hiển thị: số phím trong khe Selected khó đọc; khe Empty còn nền mờ.
11. Lỗi có sẵn ngoài UI-01: thoát Play khi đang kết nối ở scene Game → `[Scene] Không load được Boot` (`NetworkManager.OnApplicationQuit` → `MainMenuState` → `SceneLoader`).
12. `Docs/Client_Developer_Guide.vi.md` mục 6 (từ main, commit của Vinh) bị lỗi mã hoá dấu tiếng Việt ("K?t n?i Phase 2…") — báo Vinh, không tự sửa.
13. Cải tiến quy trình đội agent (template ticket thiếu chỗ câu hỏi G1/Figma node, `read_gdd.py` in deadline dạng số Excel và không có header, reviewer thiếu Edit, agent thiếu Figma MCP, quy ước mỗi vai chỉ sửa vùng của mình trong ticket): ghi chú đầy đủ ở thư mục ghi chú phiên CPO, `C:\Users\Admin\AppData\Local\Claude-Data\claude_pzs8sxrjxfjjc\side-session-notes\local_c804803e-3263-413f-8dda-e15c74f4b6c8\UI-01_trial_run_findings.md`.

## Kiểm tra nhanh

- Unity Test Runner: EditMode `JungleMaze.Tests.EditMode`, PlayMode `JungleMaze.Tests.PlayMode` (test HUD: `Tests/EditMode/Hud/`, `Tests/PlayMode/HudAcceptanceTests.cs`).
- Thử HUD không cần server: `Scenes/Dev/HudPreview.unity` + F4.
