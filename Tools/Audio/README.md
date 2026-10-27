# Tools/Audio

Công cụ tạo âm thanh **tạm** cho AUD-01. Mọi file sinh ra hoặc chép ra chỉ dùng để demo, ART-05 sẽ thay bằng asset cuối qua `SfxCatalog`, không phải sửa code.

```powershell
python -I Tools/Audio/make_placeholder_sfx.py          # sinh 14 file WAV vào Assets/_JungleMaze/Art/Audio/Placeholder/Beeps/
python -I Tools/Audio/copy_jsurviv_placeholders.py     # chép 12 SFX + 2 nhạc JSurviv vào Art/Audio/Placeholder/JSurviv/
python -I Tools/Audio/copy_jsurviv_placeholders.py --check   # chỉ kiểm bản chép còn giống bản gốc
```

- `make_placeholder_sfx.py` chỉ dùng thư viện chuẩn Python 3. File xuất ra là mono 16-bit 44,1 kHz, có fade hai đầu nên không bị click, chuẩn hóa đỉnh khoảng −3 dBFS. Chạy lại luôn ra file giống hệt. Tên file chứa tần số gốc (`<tên>_<Hz>hz.wav`). Các âm đánh số k = 0…11 dùng tần số 330·2^(k/4) Hz. Chuông bo dùng 880 Hz (0,7 s), tiếng rồng là tiếng gầm trầm từ 70 Hz (2,0 s).
- `copy_jsurviv_placeholders.py` chỉ chép file âm thanh, đổi sang tên ASCII. Script **không** chép `.meta` để Unity cấp GUID mới, và không sửa `Assets/Imported/JSurviv/`. Script cũng không chép `Waiting Room Music.wav` (25,7 MB): màn hình Waiting Room tạm dùng `loading_music.mp3` cho tới khi ART-05 nén file này.
- Bảng ánh xạ SfxId → file nằm trong hai script (`SOUNDS`, `CLIPS`) và ở mục 5 của `Docs/Tickets/AUD-01.md`.

## ART-05: clip cuối (`Art/Audio/Sfx/`, `Art/Audio/Music/`)

Cần Python có `numpy` và `soundfile` (libsndfile ≥ 1.1 đọc MP3, ghi OGG Vorbis); không dùng ffmpeg (G1, T-1). Tạo venv ngoài repo:

```powershell
python -m venv %TEMP%\audio_venv; %TEMP%\audio_venv\Scripts\pip install soundfile numpy
```

| Bước | Lệnh | Kết quả |
| --- | --- | --- |
| 1. Dựng clip | `<venv>\python Tools/Audio/process_clips.py` | `%TEMP%/art05_stage/Sfx/*.wav`, `Music/*.ogg`, `process_report.csv`. Chỉ đọc nguồn (`Tools/Audio/Sources/ART-05/`, `Assets/Imported/JSurviv/Sound/`), không ghi vào `Assets/` |
| 2. Chép vào Unity | Chép `Sfx/`, `Music/` vào `Assets/_JungleMaze/Art/Audio/` khi đang giữ Unity Editor (ghi đè đúng tên để giữ GUID) | |
| 3. Đo | `python -I Tools/Audio/measure_clips.py [thư mục] [--csv out.csv]` | Đỉnh, L, 2 ms đầu/cuối, chuỗi chạm trần (thư viện chuẩn, WAV 16-bit) |
| 4. Manifest | `<venv>\python Tools/Audio/build_manifest.py` | `Docs/Tickets/ART-05_audio_manifest.csv` từ công thức, kiểm kê và file trong repo |
| Kiểm | `<venv>\python Tools/Audio/test_art05_audio.py` | unittest của pipeline |

- `art05_recipe.csv` là nguồn duy nhất: một dòng cho mỗi file cuối (nguồn, đoạn cắt, fade, L đích, giới hạn limiter, giấy phép). Đổi clip hay điểm cắt thì sửa dòng này rồi chạy lại; WAV ra giống từng byte. OGG có số serial ngẫu nhiên nên không giống byte giữa hai lần chạy.
- `art05_jsurviv_inventory.csv`: 30 nguồn JSurviv (21 file + 9 thành viên zip), phân loại và dấu vết metadata.
- Thay cho `art05_make_sfx.py` (bản đầu của CPO, đã bỏ).

## Sau G3 của ART-05 (F4)

`Assets/_JungleMaze/Art/Audio/Placeholder/` đã bị xóa: không clip, catalog hay scene nào còn trỏ tới (kiểm GUID trước khi xóa; `copy_jsurviv_placeholders.py --check` đạt 14/14 ngay trước đó, kết quả giữ trong cột `copy_md5_match` của manifest). Vì vậy `copy_jsurviv_placeholders.py --check` giờ báo thiếu bản chép, và `make_placeholder_sfx.py` sẽ tạo lại thư mục nếu chạy: đó là hệ quả mong đợi, không cần chạy hai script này nữa.
