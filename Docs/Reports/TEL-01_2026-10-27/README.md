# TEL-01: kết quả tham chiếu 27/10/2026

Máy dev: Ryzen 7 5800H (16 luồng), Windows 11, .NET 8.0.33, Python 3.12. Cách chạy lại xem [QA_Guide.vi.md](../../Telemetry/QA_Guide.vi.md).

| File | Nội dung |
| --- | --- |
| `seed1/` | `report.md` và 9 CSV của `fake --seed 1 --cfg A`, `report --run FAKE-seed1`; giống từng byte `Tools/Telemetry/golden/seed1/`. **Số giả**, chỉ minh họa cấu trúc. `merge_summary.json` là tóm tắt gộp |
| `bench.txt` | `TelemetryChecks --bench` (AC32): `TryWrite` p99 0,2 µs khi ring được rút, 0,1 µs khi đầy, 0 byte cấp phát |
| `tick_budget.csv` | `TelemetryChecks --tick-budget --runs 3 --seconds 60 --load 70,1000` (AC33/AC34, proxy, tải giả trong tiến trình): trung vị chênh `tickP95Ms` bật − tắt là 0,014 ms (70/s) và 0,023 ms (1 000/s); `droppedTicks` = 0; overruns 0 |
