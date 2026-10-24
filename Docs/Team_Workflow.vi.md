# Quy trình đội AI: CPO và các agent chuyên trách

Cập nhật **24/10/2026**. Tài liệu này mô tả cách dùng đội agent Claude Code trong repo: CPO điều phối, sáu agent chuyên trách thực hiện, mỗi ticket đi qua cùng một quy trình có cổng duyệt. Nguồn công việc là sheet **PROJECT TRACKING** trong [GDD](%5BGDD%5D%20-%20Lastaway.xlsx). Bố cục và luồng màn hình UX/UI lấy từ wireframe Figma, xem [UI_Wireframes.vi.md](UI_Wireframes.vi.md) (GDD vẫn quyết định nội dung và số liệu). Kiến trúc client đã chốt nằm trong [Client_Base_Plan.vi.md](Client_Base_Plan.vi.md), cách viết code hằng ngày trong [Client_Developer_Guide.vi.md](Client_Developer_Guide.vi.md), backend/protocol trong [Network_Architecture.vi.md](Network_Architecture.vi.md).

Đội agent **không thay người phụ trách (PIC)** ghi trong GDD. PIC vẫn quyết định phạm vi, duyệt spec và nghiệm thu. Agent làm phần phân tích, code, review và kiểm thử, rồi trình bằng chứng cho PIC.

## 1. Bắt đầu nhanh

Trong Claude Code (terminal hoặc app desktop) mở tại thư mục repo:

```text
/cpo UI-01
/cpo MOV-02 chỉ làm phần client, server để sau
/cpo Rà lại Giai đoạn 2, ticket nào sẵn sàng làm?
```

CPO đọc dòng ticket trong GDD, kiểm tra phụ thuộc, rồi chạy quy trình ở mục 3. Bạn sẽ được hỏi duyệt ở cổng G1 (spec + kế hoạch) và G3 (nghiệm thu). Muốn gọi riêng một agent thì nói thẳng, ví dụ "dùng code-reviewer review diff hiện tại".

Đọc GDD từ terminal (không cần Excel, chỉ cần Python 3):

```powershell
python -I Tools/read_gdd.py                                   # liệt kê sheet
python -I Tools/read_gdd.py "PROJECT TRACKING" --id UI-01     # đúng một ticket
python -I Tools/read_gdd.py Weapons                           # bảng số liệu
```

## 2. Đội ngũ

| Agent | File | Vai trò | Được sửa | Không làm |
| --- | --- | --- | --- | --- |
| **CPO** (phiên chính) | [.claude/skills/cpo/SKILL.md](../.claude/skills/cpo/SKILL.md) | Chọn ticket, kiểm tra phụ thuộc, giao việc, giữ cổng duyệt, tổng hợp nghiệm thu | `Docs/Tickets/`, tài liệu nghiệm thu, `.claude/memory/` | Tự code thay dev; tự quyết thay PIC |
| **game-designer** | [.claude/agents/game-designer.md](../.claude/agents/game-designer.md) | Đổi dòng ticket + sheet GDD thành spec: phạm vi, ranh giới, số liệu, tiêu chí đo được | `Docs/Tickets/<ID>.md` phần Spec | Code, quyết định kỹ thuật |
| **tech-lead** | [.claude/agents/tech-lead.md](../.claude/agents/tech-lead.md) | Lập kế hoạch kỹ thuật: lớp, file, asmdef, protocol, test, cách chia commit | `Docs/Tickets/<ID>.md` phần Kế hoạch | Code production |
| **unity-dev** | [.claude/agents/unity-dev.md](../.claude/agents/unity-dev.md) | Code client Unity theo Clean Architecture, dựng prefab/scene qua Unity MCP, viết EditMode/PlayMode test | `Assets/_JungleMaze/`, `Assets/StreamingAssets/JungleMaze/` | Sửa `Backend/`, sửa `Assets/Imported/JSurviv/` gốc |
| **backend-dev** | [.claude/agents/backend-dev.md](../.claude/agents/backend-dev.md) | GameServer tick/simulation, API, `Backend/Shared/Protocol`, test backend | `Backend/` | Sửa client Unity; deploy AWS |
| **code-reviewer** | [.claude/agents/code-reviewer.md](../.claude/agents/code-reviewer.md) | Review diff theo quy tắc kiến trúc, đúng phạm vi ticket, lỗi logic | Không sửa file | Tự sửa code |
| **qa-tester** | [.claude/agents/qa-tester.md](../.claude/agents/qa-tester.md) | Chạy test tự động, playtest với GameServer local, đo theo tiêu chí, ghi bằng chứng | `Docs/Reports/`, phần QA trong ticket | Sửa code để test qua |

### 2.1. Hai cách chạy đội

- **Subagent** (mặc định, hợp khi làm một mình): phiên chạy `/cpo` gọi từng vai trò như subagent. Agent con xong việc là kết thúc, không giữ ngữ cảnh giữa các ticket.
- **Phiên thường trực** (trên app Claude desktop): mỗi vai trò là một phiên riêng trong sidebar, tên `Game Designer`, `Tech Lead`, `Unity Dev`, `Backend Dev`, `Code Reviewer`, `QA Tester`, và phiên điều phối tên `CPO`. Mỗi phiên đọc file vai trò trong `.claude/agents/` khi khởi động rồi chờ việc. CPO giao việc bằng tin nhắn giữa các phiên. Báo cáo cuối lượt của mỗi phiên tự chuyển về CPO. Bạn cũng có thể mở thẳng một phiên vai trò để hỏi hoặc giao việc. Các phiên dùng chung một thư mục, nên CPO chỉ giao song song khi các phiên không sửa chung file.

Ánh xạ module trong GDD sang agent thực hiện:

| Module GDD | Agent build |
| --- | --- |
| Client Core, Gameplay, World, UI/UX, Visual & Art, Audio, Combat (phía client) | unity-dev |
| Server Engine, Backend & Auth, Infra & DevOps | backend-dev |
| Netcode (MOV-02, MOV-03, WPN-02), Room/Match state có hiển thị client | backend-dev **và** unity-dev, protocol làm trước |
| Testing (BOT-01, TEL-01, QA-01) | qa-tester, cần thêm dev nếu phải viết tooling |

## 3. Vòng đời một ticket

```text
Intake ─► Spec ─► Plan ─► [G1 duyệt] ─► Build ─► Review ─► QA ─► [G3 nghiệm thu] ─► Done
  CPO   designer tech-lead   người     dev(s)  reviewer  qa-tester     người        CPO
                                        ▲         │         │
                                        └─ sửa ◄──┴─────────┘  (tối đa 2 vòng, quá thì báo người)
```

| Bước | Ai | Đầu vào | Đầu ra | Điều kiện qua bước |
| --- | --- | --- | --- | --- |
| 0. Intake | CPO | ID ticket hoặc yêu cầu tự do | Dòng ticket từ GDD, trạng thái phụ thuộc | **Definition of Ready** (mục 4) đạt; nếu phụ thuộc chưa Done thì dừng và báo |
| 1. Spec | game-designer | Dòng ticket + sheet GDD tham chiếu | `Docs/Tickets/<ID>.md` phần 1–3 | Mỗi tiêu chí nghiệm thu đo được bằng một test hoặc một phép đo cụ thể |
| 2. Plan | tech-lead | Spec + code hiện tại | Phần 4 của ticket: file, lớp, test, chia commit, rủi ro | Không vi phạm quy tắc ở mục 6; ranh giới với ticket khác rõ ràng |
| **G1** | Người (PIC) | Spec + Plan | Duyệt / sửa / hủy | CPO **không** bắt đầu build khi chưa có câu trả lời |
| 3. Build | unity-dev, backend-dev | Plan đã duyệt | Code + test, console sạch | Test của ticket xanh trên máy agent |
| 4. Review | code-reviewer | Diff so với nhánh gốc | Danh sách lỗi theo mức độ | Không còn lỗi mức Chặn |
| 5. QA | qa-tester | Build đã qua review | Bảng tiêu chí ✓/✗ kèm số đo, log | Mọi tiêu chí P0 đạt; tiêu chí chưa đo được phải ghi lý do |
| **G3** | Người (PIC) | Báo cáo QA + diff | Nghiệm thu / trả lại | — |
| 6. Done | CPO | Kết quả G3 | Ticket `Done`, ghi memory, nhắc người cập nhật Status trong GDD | Mọi phần đã commit (quy tắc 8); push chỉ khi người yêu cầu |

**G2 (tùy chọn):** ticket đụng protocol (`Backend/Shared/Protocol`) hoặc thay đổi wire format phải dừng sau khi backend-dev xong schema để người duyệt trước khi client dùng.

## 4. Definition of Ready / Definition of Done

**Ready** khi:

1. Ticket có trong PROJECT TRACKING với ID, module, tiêu chí hoàn thành.
2. Mọi phụ thuộc ở cột Dependencies đã `Done`, hoặc người chấp nhận làm song song và ghi rõ trong spec.
3. Sheet GDD tham chiếu có đủ số liệu. Thiếu số (ví dụ "Figma/Map Plan: chưa cung cấp") thì designer ghi giả định và đánh dấu để người chốt ở G1.

**Done** khi:

1. Tất cả tiêu chí trong ticket đạt, có bằng chứng (tên test, số đo, log) trong phần QA.
2. EditMode + PlayMode test liên quan xanh; nếu đụng backend thì `Backend/Tests/run_all.cmd` hoặc phần check tương ứng xanh.
3. Console Unity không có lỗi compile, missing script, exception ngoài dự kiến.
4. Không còn lỗi review mức Chặn hoặc Nên sửa chưa được người chấp nhận bỏ qua.
5. Tài liệu cập nhật nếu đổi cách dùng: Client_Developer_Guide, Network_Architecture hoặc tài liệu nghiệm thu.
6. `.claude/memory/` có ghi chú quyết định không suy ra được từ code.

## 5. File ticket và cách bàn giao

Mỗi ticket có một file `Docs/Tickets/<ID>.md` tạo từ [mẫu](Tickets/_TEMPLATE.md). File này là nguồn sự thật duy nhất trong lúc làm: designer ghi phần Spec, tech-lead ghi Kế hoạch, dev ghi Nhật ký build, qa-tester ghi QA, CPO ghi Nghiệm thu. Status trong GDD vẫn do người cập nhật vì file xlsx là bản chung của cả team.

Mọi agent kết thúc bằng một báo cáo cùng khung để CPO ghép lại:

```text
## Kết quả: <Xong | Một phần | Bị chặn>
- Đã làm: …
- File thay đổi: …
- Bằng chứng: test/số đo/log
- Rủi ro hoặc giới hạn: …
- Câu hỏi cho người: …   (để trống nếu không có)
```

## 6. Quy tắc chung mọi agent phải giữ

1. **Phụ thuộc theo lớp.** Domain và Core là C# thuần (`noEngineReferences`), Application (namespace `JungleMaze.App`) khai báo port, Infrastructure implement, Presentation là MonoBehaviour/view. Không tham chiếu ngược. Chi tiết: Client_Base_Plan mục 1.
2. **Một composition root.** Đăng ký service trong `Bootstrap`, lấy dependency qua `[Inject]`. Không `FindObjectOfType`, không singleton tĩnh mới.
3. **Số liệu cân bằng lấy từ JSON** trong `Assets/StreamingAssets/JungleMaze/balance/` qua `IBalanceConfigProvider`, không hard-code.
4. **Server authoritative.** Damage, HP, zone, loot do server quyết. Client chỉ dự đoán và hiển thị.
5. **Protocol đổi thì sửa cả hai phía trong cùng lượt**; server chưa hỗ trợ nhiều version song song.
6. **Không sửa** `Assets/Imported/JSurviv/` gốc (chỉ tham khảo hoặc trích asset), không đụng scene thừa trong Build Settings nếu ticket không yêu cầu.
7. **Không tiêu credits AWS** và không deploy nếu người không yêu cầu rõ. Test trên GameServer local.
8. **Commit ngay khi xong mỗi phần**, không chờ hỏi: mỗi phần trong mục "Chia commit" của kế hoạch, compile được và test của phần đó xanh. Message tiếng Anh, ngắn, kiểu `feat(ui-01): …` như lịch sử repo. **Không ghi Claude/AI** trong commit, không thêm dòng `Co-Authored-By`. Chỉ `git add` đúng file của phần đó (kèm `.meta`), không gộp thay đổi sẵn có của người khác (ví dụ `ProjectSettings/*`). Nhiều phiên dùng chung index git, nên luôn commit kèm danh sách đường dẫn: `git commit -m "…" -- <file…>`. **Không push** nếu người không yêu cầu.
9. **Không bịa kết quả.** Không chạy được test thì ghi "chưa kiểm chứng" và lý do.

## 7. Chạy song song

CPO chỉ cho hai agent build cùng lúc khi chúng không sửa chung file và không chờ kết quả của nhau, ví dụ unity-dev làm HUD (UI-01) trong khi backend-dev làm Room state (SRV-03). Ticket Netcode luôn tuần tự: backend-dev chốt protocol → G2 → unity-dev nối client. code-reviewer và qa-tester chỉ đọc nên có thể chạy song song với nhau.

## 8. Giới hạn đã biết

- Unity MCP không giữ phím/di chuột thật nhiều frame, nên cảm giác điều khiển (WASD, aim) vẫn cần người tự chơi thử. qa-tester đo bằng physics/velocity và ghi rõ phần chưa kiểm chứng.
- Agent không ghi được vào file xlsx GDD. CPO báo trạng thái để người cập nhật sheet.
- Agent con không gọi được agent khác. Chỉ phiên chính (CPO) điều phối; agent cần thêm thông tin thì ghi vào "Câu hỏi cho người" trong báo cáo.
