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
