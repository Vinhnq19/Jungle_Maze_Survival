# Hướng dẫn QA: telemetry trận (TEL-01)

Cập nhật 27/10/2026. Dành cho QA-01 và người chạy playtest. Mỗi bước có lệnh chép-dán (PowerShell, chạy tại thư mục gốc repo). Chỉ cần .NET 8 SDK, Python 3.12 (`python -I`, không `pip`) và một build client. Schema: [Schema.vi.md](Schema.vi.md). Chi tiết lệnh công cụ: [Tools/Telemetry/README.md](../../Tools/Telemetry/README.md).

Telemetry **tắt mặc định** ở cả server lẫn client. Không bật trên bản công khai (`PUBLIC_HOSTING=true` tự từ chối).

## 0. Kiểm nhanh bộ công cụ (khoảng 2 phút)

```powershell
python -I Tools/Telemetry/test_telemetry.py
dotnet run --project Backend/Tests/TelemetryChecks
```

Kỳ vọng: Python in `OK`; .NET in `TelemetryChecks: 70 passed, 0 failed`.

## 1. Bật telemetry phía server

**Hiện tại (trước H1 của Vinh), tiến trình GameServer thật chưa ghi telemetry.** Bằng chứng phía server lấy từ `TelemetryChecks` (bước 6). Khi Vinh đã gắn H1, chạy GameServer như hướng dẫn `Docs/Phase1_First4Tasks.vi.md` mục 12.4 và thêm:

```powershell
$env:TELEMETRY_PATH = "$PWD\data\telemetry"
$env:TELEMETRY_SAMPLE_HZ = "1"            # tùy chọn, 0,5–2
dotnet run --project Backend/GameServer 2>&1 | Tee-Object -FilePath "$env:TELEMETRY_PATH\server.log"
```

- File trận: `data\telemetry\<yyyy-MM-dd>\<matchId>.server.<wid>.jsonl`.
- `server.log` giữ các dòng `[metrics]`, dùng cho `perf_server` ở bước 3.
- Tắt server bằng Ctrl+C: telemetry flush tối đa 3 s và ghi `_trailer`.
- Xong buổi thì xóa biến: `Remove-Item Env:TELEMETRY_PATH, Env:TELEMETRY_SAMPLE_HZ`.

## 2. Bật telemetry phía client

Chạy build client với cờ dòng lệnh (không có cờ thì không ghi gì):

```powershell
.\<TenBuild>.exe -telemetry
.\<TenBuild>.exe -telemetry -telemetryDir D:\playtest\client1   # tùy chọn: chọn thư mục
```

- Mặc định file nằm ở `%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\Telemetry\<matchId>.client.<netId>.<wid>.jsonl`.
- Mỗi 5 s trong trận có một bản ghi `perf_client` (FPS, frame time, RTT, jitter, loss).

## 3. Thu file sau buổi chơi

Gom mọi file về một thư mục buổi, ví dụ `D:\playtest\buoi-1`:

```powershell
$S = "D:\playtest\buoi-1"
New-Item -ItemType Directory -Force $S | Out-Null
Copy-Item -Recurse data\telemetry\* $S\server\
# Trên từng máy client: chép thư mục Telemetry của client vào $S\client<N>\
python -I Tools/Telemetry/telemetry.py perflog "$S\server\server.log" --out "$S\perf"
```

Thu ngay sau buổi, vì thư mục `persistentDataPath` có thể bị xóa (R10). File thiếu `_trailer` (tiến trình bị kill) vẫn gộp được, báo cáo ghi `unterminated`.

## 4. Kiểm, gộp, báo cáo

```powershell
python -I Tools/Telemetry/telemetry.py validate --strict $S
python -I Tools/Telemetry/telemetry.py report $S --out "$S\report" --run buoi-1 --require-real
```

- `validate` phải in `errors=0`. Mã thoát 4 là có lỗi schema; đọc 20 ví dụ đầu in ra.
- `report` sinh `report.md` cùng 9 CSV. Đọc `report.md` trước: dòng "Kiểm" ở đầu phải có `conflicts=0 gaps=0`.
- `--require-real` thoát mã 3 nếu lẫn dữ liệu giả. `coverage.csv` cho biết loại nào là `real`, loại nào `fake-only`.
- Loại W (`attack`, `cc_span`, loot, item, `hide_span`) chỉ có dữ liệu thật khi WPN-02/ITM-01/WPN-03/hệ thống bụi đã gắn H7.

## 5. So hai phiên bản config

Gom dữ liệu của cả hai config vào một thư mục (hoặc truyền hai thư mục), rồi:

```powershell
python -I Tools/Telemetry/telemetry.py compare "D:\playtest\buoi-1" "D:\playtest\buoi-2" --out D:\playtest\compare
```

- Khi có đúng hai `cfgKey` thì không cần `--a`/`--b`. Nếu nhiều hơn, truyền `--a <cfgKey>` và `--b <cfgKey>` lấy từ `report.md`.
- `compare.md`/`compare.csv` có cột A, B, `delta = B − A` và `n`. Nhóm có n < 10 gắn "mẫu thấp", không dùng để kết luận.

Chưa có dữ liệu thật thì thử toàn bộ quy trình bằng dữ liệu giả:

```powershell
$T = "$env:TEMP\tel_demo"
python -I Tools/Telemetry/telemetry.py fake --out "$T\A" --seed 1 --cfg A
python -I Tools/Telemetry/telemetry.py fake --out "$T\B" --seed 1 --cfg B
python -I Tools/Telemetry/telemetry.py compare "$T\A" "$T\B" --out "$T\compare"
```

## 6. Đo ngân sách tick và kiểm chéo C#

```powershell
dotnet run --project Backend/Tests/TelemetryChecks -c Release -- --bench
dotnet run --project Backend/Tests/TelemetryChecks -c Release -- --tick-budget --runs 3 --seconds 60 --load 70,1000 --out "$env:TEMP\tick_budget.csv"
dotnet run --project Backend/Tests/TelemetryChecks -- --emit-sample "$env:TEMP\from_csharp"
python -I Tools/Telemetry/telemetry.py validate --strict "$env:TEMP\from_csharp"
```

- `--bench` (AC32): kỳ vọng p99 ≤ 10 µs và `allocatedBytes=0`.
- `--tick-budget` (AC33/AC34) mất khoảng 12 phút; chạy khi máy rảnh. Kỳ vọng mọi dòng `[PASS]`. Đây là **proxy, tải giả trong tiến trình**; số đo trên GameServer thật cần H1.
- Kết quả tham chiếu của máy dev: [Docs/Reports/TEL-01_2026-10-27/](../Reports/TEL-01_2026-10-27/).

## 7. Báo kết quả

Ghi vào mục QA của ticket: lệnh đã chạy, `errors`, `conflicts`, `gaps`, `dropped(trailer)`, bảng tick-budget, và loại nào còn `fake-only`. Thời gian chạy từ checkout sạch tới báo cáo (AC23) ghi bằng phút.
