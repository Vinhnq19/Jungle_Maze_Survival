# Sổ điều phối CPO — 27/10/2026

Đây là trạng thái làm việc của CPO, viết để **một phiên CPO mới (hoặc phiên tự khởi động lại) đọc và làm tiếp** mà không cần lịch sử hội thoại. Cập nhật file này và commit mỗi khi xong một mốc.

## 1. Quyền hạn

Người dùng (HuyCV, PIC V.Huy), ngày 27/10/2026, khoảng 02:00, nguyên văn:

> "CPO nhận nhiệm vụ quan trọng này cho tôi, bạn được giao quyền điều phối, lên kế hoạch và bàn giao task để đảm bảo task dc hoàn thành, bạn được toàn quyền thay tôi đưa ra các quyết định, tôi sẽ để bạn quyết định toàn bộ cho tới 12h trưa nay. Nếu hết token thì lên kế hoạch để tự khởi động và chạy lại khi làm mới. nhớ là commit đều nhé."

**Gia hạn** 27/10/2026 khoảng 08:50, nguyên văn: "Bạn được quyền quyết thay thôi. ko đợi tôi quyết"

- **Hiệu lực: không còn hạn 12:00.** CPO được duyệt G1/G2/G3 và các quyết định trong ticket thay người dùng, không chờ người dùng, cho tới khi người dùng thu hồi. Mỗi quyết định ghi vào mục 4, có lý do và bằng chứng.
- **Sau mỗi mốc:** báo kết quả cho người dùng; không dừng lại chờ duyệt.
- **Không làm dù có quyền:** push, deploy, dùng AWS, xóa dữ liệu, sửa vùng code của Hào (Hero/map/balance), Vinh (backend) hay ITM-01 của Quang Huy.
- **Commit đều:** mỗi phần xong commit ngay. Commit kèm đường dẫn, tiếng Anh, không ghi AI.

## 2. Các luồng việc

| # | Luồng | Ai làm | Trạng thái | Bước tiếp theo |
| --- | --- | --- | --- | --- |
| A | **ADHOC-UI-ASSETS-A**: gắn bộ asset hướng A (34 icon, 7 icon hiệu ứng, khung HUD, art menu) vào HUD và màn kết nối | Subagent unity-dev trong phiên CPO | **Build xong** (`49c4943b`…`dd84914a`): EditMode 274/274, PlayMode 43/43, T-1 sửa bằng `UiArtSet` + `Resources.Load` một lần. Code Reviewer đang review | **Done (G3 06:30)**. Follow-up ghi ở mục 8 của ticket |
| B | **ART-04** icon HUD/Inventory | Phần build nằm trong luồng A | Spec `24eedbf4`, đối chiếu ADHOC `f208314d`, **G1 đã duyệt** (mục 4) | **Done (G3 08:20)**. Follow-up F1–F7 ở mục 8 của ticket. AC20, Q11, Q12, Status GDD là việc của người dùng |
| C | **Vẽ lại icon theo lưới 32×32** | Codex, chạy không tương tác | Hẹn chạy lúc 05:45 (script nền trong phiên CPO, thử lại mỗi 15 phút), nhiệm vụ trong `Docs/Art/CODEX_TASK_32px_Restyle.md` | Khi xong: kiểm commit, báo cáo `Restyle32_Report.vi.md`, refresh Unity, chạy lại test HUD |
| D | **AUD-01** AudioManager | Phiên **Game Designer** → **Tech Lead** → **Unity Dev** | Spec `560346dd`, plan `de02fcff`, **G1 đã duyệt** `a5d53cde`. Unity Dev đang làm pha 0 (âm thanh tạm, không Unity) | **Done (G3 03:40)**. Còn việc của người dùng: nghe bằng panel F6 (AC-A28) → (không còn bước build) (vùng `Audio/**`; `Bootstrap.cs` chỉ sửa sau khi luồng A đã commit file này) → review → QA → **G3** |
| E | **UI-03** ESC/Settings, Spectator, Killcam (người dùng giao 08:30, PIC V.Huy, P2) | Phiên **Game Designer** → **Tech Lead** | UI-01 Done, AUD-01 Done; SRV-03/UI-02/MOV-02 chưa xong → dùng state giả theo GDD. Spec `8d9082a7`, plan `a73974dd`, **G1 đã duyệt 08:40** (mục 4.10 của ticket) | Build: unity-dev, 11 commit, khoảng 2,5–3 ngày, hạn 30/10. **Build xong 11 lát 09:30** (`0aca6241`…`8aef8a3b`, EditMode 437/437, PlayMode 91/91 theo dev). Review lát 1–6 Qua, đã sửa (`1536b9e9`, `c83d79f5`). Review lát 7–11 Qua (`f87bff7c`, 2 Nên sửa, 8 Gợi ý) → Unity Dev đang sửa, hạn 13:00 → QA Tester → G3 (CPO tự quyết) |
| F | **Art pack UI-03 overlay** (hướng A; người dùng 11:15: "điều khiển Codex tiếp tục tạo Assets cần") | Codex, chạy không tương tác (`scratchpad/codex/run_codex_ui03.ps1`) | Nhiệm vụ `Docs/Art/CODEX_TASK_UI03_Overlay.md` (`2fd7b277`): 18 PNG mới (mask slider/toggle/vòng bo/lưới/chấm Killcam, 5 icon nguyên nhân, 6 icon hàng ESC), chỉ file PNG, không `.meta`/C#/prefab, không dùng Unity | **Codex xong 11:27** (`d79874b2`, `4be7cd97`, `16b6dc74`, `52fc5e5b`): 19 PNG, kiểm 19/19, test 8/8, dựng lại giống từng byte. CPO đã xem contact sheet: đạt; `CauseLeft` và `IconExit` gần giống nhau (cùng nghĩa rời trận), chấp nhận. Unity tự tạo 19 `.meta` mặc định, chưa commit. Sau G3 UI-03 giao Unity Dev đặt import Point/không mipmap, commit `.meta`, gắn trên prefab (chỉ prefab). Asset world (ART-02 của Hào, ART-03 của Quang Huy) **không** giao Codex vì thuộc vùng đồng đội |

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

- **27/10 02:45 · Review ADHOC: Qua** (Code Reviewer, không lỗi Chặn). Giao xử lý: A8 test menu + hover nút + chiều cao thanh in-game → Unity Dev; icon effect 256→64 px và KeyArt alpha → Codex; manifest "imported, not bound" → CPO đã ghi.
- **27/10 02:45 · AUD-01 pha 1 build xong** (`485c10ab`…`16df4be5`, EditMode 326/326, PlayMode 56/56). Chấp nhận `AudioListener` dự phòng trong `AudioManager` vì không scene nào có listener. Follow-up: gắn listener vào camera, thuộc người sở hữu scene/camera. `bomb_fuse` 11,8 s giao ART-05. Code Reviewer đang review AUD-01.
- **Thứ tự Unity tiếp theo:** Unity Dev sửa ADHOC → QA Tester QA AUD-01 → (sau khi có icon 32 px của Codex) QA Tester QA ADHOC → CPO G3.

- **27/10 03:00 · ADHOC: đã sửa xong các điểm review** (Unity Dev `054b8a76`, `a9c89458`, `cc283d4a`; EditMode 326/326, PlayMode 57/57; ảnh Boot và InRoom lưu trong `Docs/Reports/ADHOC-UI-ASSETS-A/`). Chờ icon 32 px rồi QA.
- **27/10 03:00 · Review AUD-01: Qua.** Sửa ngay 4 điểm Nên sửa: không ghi PlayerPrefs mỗi lần kéo slider; dừng âm khi rời trận; kiểm listener dự phòng định kỳ; dừng BombFuse khi nổ. Lý do: lỗi thật, sửa nhỏ, nằm trong vùng AUD-01, nên làm trước QA để không phải QA hai lần.

- **27/10 03:40 · G3 AUD-01: nghiệm thu.** QA "Một phần" nhưng mọi AC P0 đạt, QA tự chạy lại 333/333 và 59/59, review Qua. Phần chưa kiểm (nghe bằng tai, Release, Profiler, Game với server) chuyển cho người dùng, BLD-01 và QA-01; follow-up ghi ở mục 8 của AUD-01.md. `EditorSettings.asset` bị PlayMode của QA đổi về 1; CPO đã hoàn về 0 theo quyết định trước đó của người dùng ("Hoàn về 0"), vì phiên QA không có quyền chạy lệnh này.

- **27/10 06:30 · G3 ADHOC-UI-ASSETS-A: nghiệm thu** (`3494d6be`, chi tiết ở mục 8 của ticket). QA "Một phần" (`7eb3f7ad`): 333/333, 59/59, A2–A9 và A11 Đạt. CPO xử lý A1 (manifest `48165122`) và A12 (ghi mục Review). A10 (scene Game cần server) chuyển QA-01. 6 follow-up giao chủ sở hữu.
- **27/10 06:30 · ART-04 chưa đóng.** R1 xong (`29cf9797`: 34 dòng `Đã chuẩn hóa`, cột `nguon_thuc`). R2-a/R2-b (test) giao phiên Unity Dev, hạn commit 09:30. Sau đó Code Reviewer xem nhanh, QA Tester chạy lại, rồi CPO duyệt G3 trước 12:00. **Quyết AC17:** chỉ cấm ảnh/sprite dưới `Assets/Imported/`. Shader TMP JSurviv có từ UI-01, được loại trừ và ghi follow-up. Lý do: ý của AC17 là không dùng sprite JSurviv trực tiếp; dời TMP Essentials là việc dọn dẹp riêng, ngoài phạm vi icon.
- **27/10 06:20 · ART-04 R2 build xong** (`e8b57415`, `8e047049`, `6c1ea276`): EditMode 337/337, PlayMode 64/65. **Quyết AC11: phương án (b), đo rect nội dung nhìn thấy.** RectTransform icon lấn số phím/`×n` 1 px (2–27 px²) do bố cục Figma UI-01, phần ảnh nhìn thấy không chạm (khoảng trong suốt khoảng 1,75 px). Lý do: AC11 nhằm giữ chữ đọc được; dời nhãn sẽ đổi hình học UI-01 đã nghiệm thu chỉ để sửa phần trong suốt. Unity Dev sửa test, ghi follow-up cho UI-01.
- 06:20 · CPO hoàn `EditorSettings.asset` về 0 sau PlayMode của QA (theo quyết định "Hoàn về 0" của người dùng).

- **27/10 08:10 · Review test ART-04 R2: Qua**, không lỗi Chặn (2 Nên sửa, 6 Gợi ý). **QA ART-04:** Đạt 19, Một phần 2, Không kiểm 2, Không đạt 0 (`f8ad29df`, `489cbae4`). Giao Unity Dev sửa 2 Nên sửa cùng Gợi ý 1, 5 trước G3. Lý do: chỉ là test thêm, nhỏ, trong vùng ART-04, và giữ AC11/AC17 khi icon được vẽ lại.
- **27/10 08:20 · G3 ART-04: nghiệm thu.** Unity Dev sửa xong (`e43e7727`, `1712d3ef`; EditMode 338/338, PlayMode 65/65). Mọi AC P0 đo được đều Đạt. AC14 chuyển ITM-01, AC20 cho người dùng. Chấp nhận AC23 không thêm cột nguồn vào manifest dùng chung, vì nguồn đã có ở CSV và `Provenance.csv`. Follow-up F1–F7 ở mục 8 của ticket.
- **27/10 08:40 · G1 UI-03: duyệt** (người dùng giao 08:30: "spec, kế hoạch, G1"). Nhận toàn bộ mặc định Q1–Q16, G-1…G-14, T-1…T-12; T-1 = (A) không sửa file của Vinh/Vũ. Không đổi protocol/`match.json`, không cần G2. Lý do: mọi mặc định đo được, giữ đúng ranh giới SRV-03/UI-02/MOV-02, chỉ chạm `Bootstrap.cs` 1 dòng và 2 dòng trong file AUD-01. Đã báo Vinh/Vũ trong `Agent_Coordination.vi.md` mục 4.

## 5. Khởi động lại khi hết token

Đã hẹn các lượt tự chạy lại (scheduled task "CPO resume"). Mỗi lượt làm:
1. Đọc file này, `Docs/Agent_Coordination.vi.md` mục 4, `git log --oneline -20`, `git status`.
2. Nếu mục 4 của file phối hợp có dòng "đang dùng Unity" chưa đóng trong 30 phút gần nhất, hoặc có commit trong 20 phút gần nhất: có phiên khác đang chạy. Chỉ ghi nhận xét vào mục 6 rồi dừng.
3. Nếu không: làm tiếp "Bước tiếp theo" ở mục 2 theo đúng quy trình `Docs/Team_Workflow.vi.md`. Giao việc cho phiên vai trò, hoặc tự làm khi phiên đó không phản hồi. Commit đều.
4. Quyền quyết thay người dùng không còn hạn 12:00 (mục 1, gia hạn 08:50); vẫn ghi mọi quyết định vào mục 4.

## 6. Ghi chú các lượt chạy

- 27/10 02:05 · CPO · Tạo sổ điều phối. Luồng A đang build, C chờ 05:45, D đang lập kế hoạch.
- 27/10 02:10 · CPO · Hẹn 4 lượt tự chạy lại (scheduled task): `cpo-resume-0550`, `cpo-resume-0730`, `cpo-resume-0930`, `cpo-resume-1115`. Lượt 11:15 viết `Docs/CPO_Handback_2026-10-27.vi.md` để trao lại quyền lúc 12:00.
- 27/10 05:45 · CPO · Lần chạy Codex lúc 05:45 lỗi ngay (exit 2): `--approve-for-me` không đi cùng `-s workspace-write` được. Đã bỏ `-s` (cờ này vốn đã dùng sandbox workspace-write) và chạy lại lúc 05:45:34. Luồng C đang chạy.
- 27/10 06:05 · CPO · **Luồng C xong** (Codex `01e477fa`, `b96db211`, `f7ea7a13`). CPO đo lại: 41/41 icon đạt, không có `.meta`/C#/prefab/scene nào đổi. Codex để lại KeyArt/EffectFrame vì prompt giới hạn chỉ sửa `Icons/**`; CPO tự làm (`81de7d82`). Đã giao phiên QA Tester QA ADHOC A1–A12 cùng các AC build của ART-04, hạn 11:00. Bước tiếp theo: đọc kết quả QA rồi duyệt G3 ADHOC + ART-04.
