# Tools/Telemetry

Công cụ ngoại tuyến của TEL-01: kiểm schema, phát event giả, gộp file nhiều nguồn, báo cáo cân bằng và so hai config. Chỉ dùng thư viện chuẩn Python 3.12, chạy bằng `python -I`, không cần mạng hay `pip`. Schema là [Docs/Telemetry/telemetry.schema.v1.json](../../Docs/Telemetry/telemetry.schema.v1.json), bảng cho người đọc ở [Schema.vi.md](../../Docs/Telemetry/Schema.vi.md). Hướng dẫn từng bước cho QA-01 nằm ở `Docs/Telemetry/QA_Guide.vi.md` (commit 9 của TEL-01).

## Lệnh

```powershell
python -I Tools/Telemetry/telemetry.py validate --strict <file|thư mục>...           # kiểm từng dòng
python -I Tools/Telemetry/telemetry.py fake --out %TEMP%\tel_fake --seed 1 --cfg A    # 6 trận giả, đủ 18 loại
python -I Tools/Telemetry/telemetry.py perflog server.log --out <thư mục>              # [metrics] -> perf_server
python -I Tools/Telemetry/telemetry.py merge <file|thư mục>... --out <thư mục>         # events.jsonl, conflicts.jsonl
python -I Tools/Telemetry/telemetry.py report <file|thư mục>... --out <thư mục> --run buoi-1
python -I Tools/Telemetry/telemetry.py compare <A> <B> --out <thư mục> --a <cfgKey A> --b <cfgKey B>
python -I Tools/Telemetry/test_telemetry.py                                           # unittest
```

| Lệnh | Đầu vào | Đầu ra |
| --- | --- | --- |
| `validate` | file hoặc thư mục `*.jsonl` (tìm đệ quy) | Tóm tắt số dòng, lỗi theo mã (`errorCodes` của schema), 20 ví dụ đầu. `--pii-sample <chuỗi>` thêm chuỗi cấm (tên hiển thị, RoomCode mẫu) |
| `fake` | `--seed`, `--cfg A\|B`, `--matches` (6), `--players` (10), `--sample-hz` (1) | Mỗi trận một file server và một file cho mỗi client, một file `perf_server`, `inject_manifest.json`. Mọi dòng có `"fake": true` |
| `perflog` | log stdout của GameServer | `perf_server.<wid>.jsonl`; dòng `[metrics]` hỏng bị bỏ và đếm `malformed` |
| `merge` | mọi file của một hay nhiều buổi | `events.jsonl` (đã gộp, sắp theo `tick, src, seq, wid`), `conflicts.jsonl`, `merge_summary.json` |
| `report` | như `merge` | thêm `report.md` và 9 CSV: `ttk_by_weapon`, `death_causes`, `zone_deaths_by_phase`, `loot_by_cfg`, `cc_by_cfg`, `item_use`, `hide`, `perf`, `coverage` |
| `compare` | dữ liệu chứa cả hai config | `compare.md`, `compare.csv`: A, B, `delta = B − A`, `nA`, `nB`, nhãn "mẫu thấp" khi n < 10. `--a`/`--b` nhận cfgKey đầy đủ hoặc tiền tố duy nhất; chỉ có đúng hai cfgKey thì được bỏ trống |

Cờ chung của `merge`, `report`, `compare`: `--strict` (thoát 2 khi có xung đột), `--require-real` (thoát 3 khi có bản ghi giả), `--pii-sample`. `report`/`compare` có `--gap-sec` (mặc định 5).

**Mã thoát:** 0 OK, kể cả khi thiếu nguồn (mục tương ứng ghi "không có dữ liệu"); 1 dùng sai lệnh; 2 xung đột với `--strict`; 3 có dữ liệu giả với `--require-real`; 4 lỗi schema với `validate --strict`.

## Cờ tiêm lỗi của `fake`

| Cờ | Tiêm | Kết quả `merge` mong đợi |
| --- | --- | --- |
| `--dup [PCT]` | PCT % dòng bản ghi chép lại y hệt (mặc định 5) | `dup` = số đã tiêm, `events.jsonl` giống lần chạy sạch |
| `--conflict [N]` | N bản ghi cùng khóa kỹ thuật, khác `utc` (mặc định 3) | `conflicts` = N, `--strict` thoát 2 |
| `--semantic-dup [N]` | N bản ghi loại "một lần" bị báo lại với `seq` mới (mặc định 6) | `semantic_dup` = N |
| `--gap K` | Bỏ K dòng sau khi ghi | `gaps` = K, `_trailer.written` lệch đúng K |
| `--reorder` | Xáo dòng trong mỗi file | Kết quả giống từng byte lần chạy sạch |
| `--reconnect` | Trận 0: một client rớt ở T, nối lại ở T+6 s (trong grace 15 s) | Một `DROPPED`, một `RESUMED`, không có `match_start`/`player_spawn` thứ hai, `seq` client liên tục |

Con số đã tiêm và tổng bản ghi mong đợi sau gộp (`expectedMerged`) nằm trong `inject_manifest.json`.

## Quy tắc của công cụ

- **Xác định:** cùng tham số cho file giống từng byte. Bộ phát giả không đọc đồng hồ: `utc` = mốc cố định + tick × 33 ms (config B lệch một ngày để `perf_server` của A và B không chồng nhau). Báo cáo không chứa thời điểm chạy hay đường dẫn tuyệt đối.
- **Gộp:** khóa kỹ thuật (`matchId, src, wid, seq`) trùng nội dung thì giữ một; khác nội dung là xung đột, mọi biến thể vào `conflicts.jsonl` và `events.jsonl` giữ biến thể có JSON chuẩn hóa nhỏ nhất để kết quả không phụ thuộc thứ tự đọc. Loại `once` trùng khóa ngữ nghĩa thì giữ bản đầu theo `(tick, src, seq, wid)`. Hở `seq` tính tới `max(seq lớn nhất, _trailer.written)`; nguồn thiếu `_trailer` báo `unterminated` nhưng vẫn gộp.
- **cfgKey:** `cfg/cfgHash[0:8]/balHash[0:8 hoặc -]`, lấy từ `match_start` của trận (envelope server). Bản ghi client theo `matchId` vào cùng nhóm với trận. `perf_server` gắn vào trận khi `utc` nằm trong `[match_start.utc, match_end.utc + windowSec]`; dòng ngoài mọi trận chỉ được đếm ở đầu báo cáo.
- **Số đo** theo TEL-01 mục 2.5: TTK là chuỗi `damage` `WEAPON` của chính người hạ, hai lần liên tiếp cách nhau ≤ `gapSec`, tính tới tick bị loại; gán cho vũ khí của đòn kết liễu. Chênh thời gian dùng vật phẩm chỉ tính lượt `DONE`. Phân vị nearest-rank. Báo cáo không kết luận đạt/không đạt về TTK.
- **PII:** bộ kiểm từ chối khóa cấm và chuỗi giống JWT/IP ở mọi độ sâu. Bộ phát giả giữ tên hiển thị và RoomCode mẫu chỉ trong bộ nhớ; test quét toàn bộ đầu ra để chắc chúng không lọt ra. File cố ý chứa PII duy nhất là `fixtures/invalid/pii__*.jsonl`.

## Fixture, golden và test

| Thư mục | Nội dung |
| --- | --- |
| `fixtures/valid/` | 18 file, mỗi loại một file: `_header`, bản ghi, `_trailer`, đúng định dạng bộ ghi thật (không có `fake`) |
| `fixtures/invalid/` | `<mã lỗi>__<mô tả>.jsonl`, mỗi file một dòng phải ra đúng mã lỗi đó: thiếu từng trường envelope, mỗi trường liệt kê ngoài miền, `BAD_ID`, PII, quy tắc chéo trường |
| `fixtures/hand/` | Giao tranh và số liệu dựng tay cho AC13–AC15; kết quả kỳ vọng viết sẵn trong test |
| `fixtures/perflog/` | Dòng `[metrics]` theo đúng chuỗi định dạng của `ServerMetrics.cs`, cùng 4 dòng hỏng |
| `golden/seed1/` | `report.md` và 9 CSV của `fake --seed 1 --cfg A`, chạy `report --run FAKE-seed1` |

`fixtures/make_fixtures.py` sinh lại `valid/` và `invalid/` từ schema. `golden/seed1/` chỉ cập nhật bằng `python -I Tools/Telemetry/test_telemetry.py --update-golden`, xem diff rồi commit cùng thay đổi gây ra nó. `--perf` chạy thêm phép đo AC19 (50 trận giả, khoảng 415 000 dòng).

File trong thư mục này luôn là LF (`.gitattributes`), để so sánh từng byte với golden đúng trên mọi máy.
