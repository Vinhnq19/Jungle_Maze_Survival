# Sổ điều phối CPO — 27/10/2026

Đây là trạng thái làm việc của CPO, viết để **một phiên CPO mới (hoặc phiên tự khởi động lại) đọc và làm tiếp** mà không cần lịch sử hội thoại. Cập nhật file này và commit mỗi khi xong một mốc.

## 1. Quyền hạn

Người dùng (HuyCV, PIC V.Huy), ngày 27/10/2026, khoảng 02:00, nguyên văn:

> "CPO nhận nhiệm vụ quan trọng này cho tôi, bạn được giao quyền điều phối, lên kế hoạch và bàn giao task để đảm bảo task dc hoàn thành, bạn được toàn quyền thay tôi đưa ra các quyết định, tôi sẽ để bạn quyết định toàn bộ cho tới 12h trưa nay. Nếu hết token thì lên kế hoạch để tự khởi động và chạy lại khi làm mới. nhớ là commit đều nhé."

- **Hiệu lực: tới 12:00 ngày 27/10/2026.** Trong thời gian này, CPO được duyệt G1/G3 thay người dùng. Mỗi quyết định ghi vào mục 4, có lý do.
- **Sau 12:00:** không duyệt thêm; chỉ báo cáo và chờ người dùng.
- **Không làm dù có quyền:** push, deploy, dùng AWS, xóa dữ liệu, sửa vùng code của Hào (Hero/map/balance), Vinh (backend) hay ITM-01 của Quang Huy.
- **Commit đều:** mỗi phần xong commit ngay. Commit kèm đường dẫn, tiếng Anh, không ghi AI.

## 2. Các luồng việc

| # | Luồng | Ai làm | Trạng thái | Bước tiếp theo |
| --- | --- | --- | --- | --- |
| A | **ADHOC-UI-ASSETS-A**: gắn bộ asset hướng A (34 icon, 7 icon hiệu ứng, khung HUD, art menu) vào HUD và màn kết nối | Subagent unity-dev trong phiên CPO | **Build xong** (`49c4943b`…`dd84914a`): EditMode 274/274, PlayMode 43/43, T-1 sửa bằng `UiArtSet` + `Resources.Load` một lần. Code Reviewer đang review | Build xong → phiên **Code Reviewer** review → phiên **QA Tester** kiểm A1–A12 (sau khi icon 32 px của luồng C xong) → CPO duyệt **G3** |
| B | **ART-04** icon HUD/Inventory | Phần build nằm trong luồng A | Spec `24eedbf4`, đối chiếu ADHOC `f208314d`, **G1 đã duyệt** (mục 4) | Đóng cùng G3 của luồng A. AC14 (gợi ý nhặt dưới sàn) hoãn sang ITM-01. AC20 (thử nhận diện với 3 người) là việc của người dùng |
| C | **Vẽ lại icon theo lưới 32×32** | Codex, chạy không tương tác | Hẹn chạy lúc 05:45 (script nền trong phiên CPO, thử lại mỗi 15 phút), nhiệm vụ trong `Docs/Art/CODEX_TASK_32px_Restyle.md` | Khi xong: kiểm commit, báo cáo `Restyle32_Report.vi.md`, refresh Unity, chạy lại test HUD |
| D | **AUD-01** AudioManager | Phiên **Game Designer** → **Tech Lead** → **Unity Dev** | Spec `560346dd`, plan `de02fcff`, **G1 đã duyệt** `a5d53cde`. Unity Dev đang làm pha 0 (âm thanh tạm, không Unity) | Pha 1 khi luồng A trả Unity Editor → Unity Dev build (vùng `Audio/**`; `Bootstrap.cs` chỉ sửa sau khi luồng A đã commit file này) → review → QA → **G3** |

Phiên vai trò (sidebar): Game Designer `local_3972f73b…`, Tech Lead `local_56f66202…`, Unity Dev `local_d782cbf2…`, Backend Dev `local_e781ffca…`, Code Reviewer `local_9ef9cabc…`, QA Tester `local_4916fe7a…`.

## 3. Thứ tự dùng Unity Editor (chỉ một bên mỗi lúc)

1. Subagent luồng A: build và test cho tới khi commit xong.
2. Unity Dev (luồng D): build AUD-01.
3. QA Tester: QA luồng A (sau luồng C), rồi QA AUD-01.

Trước khi dùng Unity thì ghi vào mục 4 của `Docs/Agent_Coordination.vi.md`; xong cũng ghi lại. Sau PlayMode: `git checkout -- ProjectSettings/EditorSettings.asset`. Không commit `ProjectSettings/TimeManager.asset`, `.codex/config.toml`.

## 4. Nhật ký quyết định (CPO thay người dùng)

- **27/10 02:05 · G1 ART-04: duyệt.** Chấp nhận mặc định của spec cùng các quyết định ADHOC đã chốt thay (ART-04 mục 4.3):
  - JSurviv dùng trước; ID thiếu thì sinh mới. Sau đó toàn bộ được vẽ lại ở lưới 32 px theo luồng C.
  - Point thay Bilinear; icon 64², nội dung khoảng 56 px (lưới gốc 28 px ×2).
  - Vẽ lại SPIKE_HELMET, WOODEN_ARMOR, ADRENALINE (bỏ logo), SEED; chấp nhận Pillow cho script.
  - Ô icon trong khe giữ 28 px, toast 18 px.
  - Không làm màn Inventory toàn màn hình; không thêm icon vào hàng Vitals.
  - Icon đạn chờ ITM-01. Bản icon nằm trong `_JungleMaze/Art/UI/Icons`.
  - **AC14 (gợi ý nhặt dưới sàn) hoãn sang ITM-01**: chưa có nguồn dữ liệu, không bịa.
  - **AC20 để người dùng làm**, không chặn G3 kỹ thuật.
  - Q11/Q12 (sửa Ref GDD, cập nhật sheet Assets) giao người dùng.
  - T-1 (catalog bị null khi Bootstrap tự tạo) bắt buộc sửa trong luồng A.

- **27/10 02:25 · G1 AUD-01: duyệt** toàn bộ mặc định Q1–Q14, T-1…T-8 (commit `a5d53cde`, chi tiết cuối mục 4 của AUD-01.md). Lý do: các mặc định giữ đúng ranh giới (ART-05 lo license/asset, UI-03 chỉ gọi API, FOW-01/WPN-01 quyết luật nguồn), không phát đôi sự kiện, BGM tắt vì GDD chưa chốt. Đủ đo được để kiểm ở G3. Unity Dev làm pha 0 (âm thanh tạm, không Unity) ngay; pha 1 (C#/Unity) khi luồng A trả Unity Editor.

- **27/10 02:15 · Bổ sung việc cho Codex:** xuất lại `KeyArt.png` có alpha; làm viền `EffectFrame.png` mỏng hơn. Lý do: lỗi art subagent phát hiện khi gắn; sửa ảnh là việc của art, không sửa code.
- **27/10 02:15 · Thứ tự Unity:** Unity Dev (AUD-01 pha 1) dùng trước. QA ADHOC chạy sau khi có icon 32 px của Codex và Unity Dev trả Editor.

## 5. Khởi động lại khi hết token

Đã hẹn các lượt tự chạy lại (scheduled task "CPO resume"). Mỗi lượt làm:
1. Đọc file này, `Docs/Agent_Coordination.vi.md` mục 4, `git log --oneline -20`, `git status`.
2. Nếu mục 4 của file phối hợp có dòng "đang dùng Unity" chưa đóng trong 30 phút gần nhất, hoặc có commit trong 20 phút gần nhất: có phiên khác đang chạy. Chỉ ghi nhận xét vào mục 6 rồi dừng.
3. Nếu không: làm tiếp "Bước tiếp theo" ở mục 2 theo đúng quy trình `Docs/Team_Workflow.vi.md`. Giao việc cho phiên vai trò, hoặc tự làm khi phiên đó không phản hồi. Commit đều.
4. Sau 12:00 27/10: chỉ báo cáo, không duyệt thêm.

## 6. Ghi chú các lượt chạy

- 27/10 02:05 · CPO · Tạo sổ điều phối. Luồng A đang build, C chờ 05:45, D đang lập kế hoạch.
- 27/10 02:10 · CPO · Hẹn 4 lượt tự chạy lại (scheduled task): `cpo-resume-0550`, `cpo-resume-0730`, `cpo-resume-0930`, `cpo-resume-1115`. Lượt 11:15 viết `Docs/CPO_Handback_2026-10-27.vi.md` để trao lại quyền lúc 12:00.
