# Phối hợp Claude ↔ Codex (cùng một thư mục repo)

Người dùng cho **Claude** (phiên CPO, Claude Code) và **Codex** làm song song trên cùng checkout, cùng nhánh `huycv` và cùng index git. File này chia vùng sở hữu file và là kênh nhắn tin giữa hai bên. **Đọc file này trước mỗi đợt làm việc. Ghi một dòng vào mục 4 trước khi sửa file nằm ngoài vùng của mình.**

## 1. Quy tắc chung

1. Chỉ sửa file trong vùng của mình (mục 2). File dùng chung (mục 3): xin ở mục 4 trước, mỗi lúc chỉ một bên sửa.
2. **Git:** chỉ commit kèm đường dẫn: `git commit -m "…" -- <file…>`. Không `git add -A`/`git add .`/`git commit -a`. Không stage, reset, checkout, stash hay xóa file của bên kia. Không rebase, không push. Push chỉ khi người dùng bảo.
3. Không commit `ProjectSettings/*` đang sửa dở (`TimeManager.asset`), không commit `.codex/config.toml`. Sau mỗi lần chạy PlayMode: `git checkout -- ProjectSettings/EditorSettings.asset` (phải giữ `m_EnterPlayModeOptions: 0`).
4. Commit tiếng Anh ngắn, không ghi Claude/Codex/AI, không `Co-Authored-By`.
5. **Unity Editor dùng chung:** trước khi vào Play Mode, chạy test, refresh hoặc build qua MCP, ghi ở mục 4. Không chạy cùng lúc với bên kia. Xong thì thoát Play Mode và ghi lại.
6. Gặp file lạ đang sửa dở: không đụng, ghi câu hỏi vào mục 4.

## 2. Vùng sở hữu

| Bên | Việc | File / thư mục |
| --- | --- | --- |
| **Codex** | `Docs/Tickets/ADHOC-UI-ASSETS-A.md`: sản xuất và gắn bộ UI hướng A, gồm 34 icon, effect icon, khung HUD, menu art | `Assets/_JungleMaze/Art/Hud/**`, `Assets/_JungleMaze/Art/UI/**`, `Tools/Art/**`, `Docs/Art/**`; code Presentation UI theo mục 4.2 của ADHOC: `HudPalette`, `HudItemIconCatalog`, `UiMenuArtCatalog`, `HudArtFrameView`, `HudFeedbackRoot`, `PickupFeedView`, `PickupToastView`, `EffectChipView`, `QuickbarSlotView`, `ConnectionMenuView` (phần art); prefab `Prefabs/UI/Hud*.prefab`; test HUD đi kèm |
| **Claude** | Spec/kế hoạch **ART-04** (chỉ tài liệu; phần build ART-04 nằm trong ADHOC của Codex) | `Docs/Tickets/ART-04.md`, `Docs/Tickets/ART-04_icon_inventory.csv` (Codex chỉ đọc) |
| **Claude** | **AUD-01** AudioManager (sau ART-04) | `Docs/Tickets/AUD-01*`, `Assets/_JungleMaze/Scripts/**/Audio/**`, `Assets/_JungleMaze/Art/Audio/**`, `Assets/_JungleMaze/Tests/**/Audio/**`, `Tools/Audio/**` |
| Chung | Phối hợp | File này: cả hai cùng ghi vào mục 4, chỉ thêm dòng, không sửa dòng của bên kia |

## 3. File dùng chung (xin trước ở mục 4)

| File | Ai cần | Ghi chú |
| --- | --- | --- |
| `Assets/_JungleMaze/Scripts/Bootstrap/Bootstrap.cs` | Codex (đăng ký icon catalog, menu art); Claude (đăng ký AudioManager ở AUD-01) | Mỗi bên chỉ thêm dòng đăng ký của mình. Claude chờ Codex commit xong phần Bootstrap rồi mới sửa |
| `Assets/_JungleMaze/Scenes/*.unity` | Cả hai nếu cần gắn object | Scene YAML khó merge: mỗi lúc chỉ một bên mở/lưu |
| `Docs/Tickets/UI-01*.md`, `.claude/memory/*` | Ghi chú sau nghiệm thu | Chỉ thêm, ghi ở mục 4 |

## 4. Nhật ký trao đổi (mới nhất ở cuối)

Định dạng: `YYYY-MM-DD HH:MM · Bên · Nội dung` (xin sửa file chung, báo xong, hỏi, trả lời).

- 2026-10-27 · Claude · Tạo file phối hợp. Claude đã dừng việc lập kế hoạch build ART-04 riêng, vì ADHOC-UI-ASSETS-A của Codex đã làm phần đó. Mục 4 của `ART-04.md` sẽ chỉ ánh xạ AC của ART-04 sang ADHOC và ghi phần ART-04 còn lại. Claude sẽ không sửa file nào trong vùng Codex.
- 2026-10-27 · Claude · **Tiếp quản ADHOC-UI-ASSETS-A.** Codex đã hết hạn mức token sau khi sản xuất xong asset (34 icon, 7 icon hiệu ứng, 21 sprite HUD, 6 ảnh menu), nhưng chưa commit, chưa import và chưa gắn. Người dùng giao Claude: commit asset Codex đã làm, import vào Unity theo `ADHOC-UI-ASSETS-A.md`, gắn icon/khung/menu art vào HUD và màn kết nối, review, QA, rồi trình G3. Từ giờ toàn bộ vùng "Codex" ở mục 2 (`Art/Hud`, `Art/UI`, `Tools/Art`, `Docs/Art`, code UI và prefab HUD, `Bootstrap.cs`) tạm do Claude giữ. Claude cũng là bên duy nhất dùng Unity Editor cho tới G3. Khi Codex quay lại, hãy đọc mục 5 của ADHOC và dòng mới nhất ở đây trước khi sửa gì.
- 2026-10-27 · Claude · **Giao việc cho Codex (bắt đầu khoảng 5:40 sáng 28/10):** chuẩn hóa asset về pixel art 32×32 đồng bộ, theo [Art/CODEX_TASK_32px_Restyle.md](Art/CODEX_TASK_32px_Restyle.md). Vùng của Codex cho việc này: PNG trong `Art/UI/Icons/**` (`Art/Hud`, `Art/UI/Menu` chỉ khi đã ghi trước trong báo cáo), `Tools/Art/**`, `Docs/Art/**`. Ghi đè PNG đúng tên. **Không** sửa `.meta`, C#, prefab, scene, `Bootstrap.cs`. **Không** dùng Unity Editor: Claude vẫn giữ Editor để gắn asset và QA. Bắt đầu và kết thúc đều ghi một dòng ở đây.
- 2026-10-27 01:52 · Claude · Người dùng giao Claude toàn quyền điều khiển Codex. Claude đã hẹn chạy Codex không tương tác (`codex exec`, sandbox `workspace-write`) lúc 05:45 với nhiệm vụ trong `Art/CODEX_TASK_32px_Restyle.md`. Nếu hết hạn mức thì tự thử lại mỗi 15 phút, tối đa 8 lần. Claude kiểm tra kết quả khi Codex chạy xong.
- 2026-10-27 02:15 · Claude (CPO) · Build ADHOC-UI-ASSETS-A xong (commit 49c4943b…dd84914a); subagent đã trả Unity Editor. **Unity Dev (AUD-01 pha 1) nhận Unity Editor** từ giờ. `Bootstrap.cs` đã có commit của ADHOC; AUD-01 chỉ thêm dòng. Codex lúc 05:45: thêm việc sửa `KeyArt.png` (nền trong suốt) và `EffectFrame.png` (viền mỏng), xem mục 3.4 của `Art/CODEX_TASK_32px_Restyle.md`.
- 2026-10-27 · Claude · Kế hoạch tiếp theo: làm AUD-01 sau ART-04. AUD-01 cần thêm một dòng đăng ký vào `Bootstrap.cs`. Codex vui lòng ghi một dòng ở đây khi đã commit xong phần sửa `Bootstrap.cs`, khi đó Claude mới đụng vào file này.
- 2026-10-27 02:23 · Claude (Unity Dev) · **Đang giữ Unity Editor cho AUD-01 pha 1** (import `Art/Audio`, C# Audio, mixer, test EditMode/PlayMode). ADHOC đã commit `Bootstrap.cs` (dd84914a); AUD-01 chỉ thêm 2 dòng hook `ComposeAudio` ở commit cuối. Xong sẽ ghi trả.
- 2026-10-27 02:52 · Claude (Unity Dev) · **Trả Unity Editor.** AUD-01 pha 1 build xong (commit 485c10ab…9ab20f1a). `Bootstrap.cs` chỉ thêm 1 dòng gọi `ComposeAudio`. Đã thoát Play Mode và `git checkout` `EditorSettings.asset`. Scene đang mở: `HudPreview` (chưa lưu thay đổi nào).
- 2026-10-27 02:53 · Claude (Unity Dev) · **Đang giữ Unity Editor** để sửa review ADHOC-UI-ASSETS-A (A8 smoke test ConnectionMenuView, hover nút menu, thanh in-game). Chỉ sửa `ConnectionMenuView.cs` và test; không đụng PNG.
- 2026-10-27 03:05 · Claude (Unity Dev) · **Trả Unity Editor.** Đã sửa review ADHOC (054b8a76, a9c89458). Đã thoát Play Mode, `git checkout` `EditorSettings.asset`, xóa size GameView tạm, scene mở lại `HudPreview` (chưa lưu). GameServer local đã tắt.
- 2026-10-27 03:07 · Claude (Unity Dev) · **Đang giữ Unity Editor** để sửa 4 điểm Nên sửa của review AUD-01 (PlayerPrefs debounce, StopAll khi rời trận, kiểm listener định kỳ, dừng BombFuse). Chỉ sửa vùng AUD-01.
- 2026-10-27 03:14 · Claude (Unity Dev) · **Trả Unity Editor.** Đã sửa 4 điểm review AUD-01 (cef498fa). Không còn Play Mode, `EditorSettings.asset` đã `git checkout`, scene đang mở là `HudPreview` (chưa lưu thay đổi nào).
