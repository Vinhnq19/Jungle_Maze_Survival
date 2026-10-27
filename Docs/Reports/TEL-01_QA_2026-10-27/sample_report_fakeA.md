# Báo cáo telemetry  run=A  matches=6  cfgKey=1.0.0/bdd202c5/-

Kiểm: events=18986 dup=0 semantic_dup=0 conflicts=0 gaps=0 trailer_mismatch=0 unterminated=0 invalid=0 dropped(trailer)=0 truncated=0 fake=100.0%

perf_server ngoài khoảng mọi trận: 102 cửa sổ (không gộp vào cfgKey nào).

Định nghĩa đo theo TEL-01 mục 2.5: gapSec=5.0 s, phân vị nearest-rank, nhóm n<10 là mẫu thấp. Đây là số đo, không phải mục tiêu cân bằng: báo cáo không kết luận đạt/không đạt về TTK.

## cfgKey 1.0.0/bdd202c5/-

matches=6 records=18678 fake=100.0%

### TTK theo vũ khí (giây; giao tranh kết thúc bằng hạ gục, gap 5 s)

| weapon | kills | median | p90 | hitsToKill | oneShot | ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| CRYSTAL | 5 | 3.97 | 5.03 | 3.20 | 0 | mẫu thấp (n<10) |
| METEORITE_FRAGMENT | 5 | 2.37 | 3.90 | 2.40 | 2 | mẫu thấp (n<10) |
| KNIFE | 4 | 2.02 | 2.93 | 2.75 | 1 | mẫu thấp (n<10) |
| SAHUR_BAT | 4 | 3.92 | 5.23 | 2.75 | 1 | mẫu thấp (n<10) |
| BONE | 3 | 1.30 | 7.90 | 2.67 | 1 | mẫu thấp (n<10) |
| SCEPTER | 3 | 1.03 | 3.80 | 3.33 | 1 | mẫu thấp (n<10) |
| SHOTGUN | 3 | 1.67 | 6.43 | 2.00 | 1 | mẫu thấp (n<10) |
| TREE_BRANCH | 2 | 4.92 | 5.53 | 5.50 | 0 | mẫu thấp (n<10) |
| CROSSBOW | 1 | 4.07 | 4.07 | 3.00 | 0 | mẫu thấp (n<10) |
| HAND | 1 | 0.00 | 0.00 | 1.00 | 1 | mẫu thấp (n<10) |
| SEED | 1 | 1.03 | 1.03 | 2.00 | 0 | mẫu thấp (n<10) |

Hạ gục bằng vũ khí không có chuỗi damage của người hạ: 0

### Nguyên nhân chết

| cause | n | % |
| --- | --- | --- |
| WEAPON | 32 | 59.3 |
| ZONE | 16 | 29.6 |
| HAZARD | 0 | 0.0 |
| DISCONNECT | 3 | 5.6 |
| OTHER | 3 | 5.6 |

Tỷ lệ chết vì bo = 29.6%; người chơi bị ≥1 damage bo = 38.3% (23/60)
Chết vì bo theo phase (-1/0/1/2/3/4): 3 / 6 / 2 / 1 / 1 / 3

### Loot (mỗi người, mỗi trận)

vật chứa vỡ: 199 (AIRDROP_CRATE 6, CHEST_WOODEN 81, CORPSE 42, POT_CLAY 70) | món rơi: 915 | món nhặt: 240 (4.00/người)
có trang bị đầu tiên (trung vị): 40.5 s (n=50, không bao giờ có: 10) | chết khi chỉ có HAND: 15/54

### Khống chế (CC)

| ccKind | by | n | giây | lần/người | giây/người | CLEANSED % |
| --- | --- | --- | --- | --- | --- | --- |
| STUN | ALL | 20 | 31.27 | 0.33 | 0.52 | 15.0 |
| STUN | BANANA | 1 | 1.00 | 0.02 | 0.02 | 0.0 |
| STUN | MACE | 1 | 1.00 | 0.02 | 0.02 | 0.0 |
| STUN | SAHUR_BAT | 11 | 20.77 | 0.18 | 0.35 | 9.1 |
| STUN | SEED | 7 | 8.50 | 0.12 | 0.14 | 28.6 |
| ROOT | ALL | 13 | 14.97 | 0.22 | 0.25 | 15.4 |
| ROOT | ROPE | 7 | 8.97 | 0.12 | 0.15 | 28.6 |
| ROOT | SWAMP | 6 | 6.00 | 0.10 | 0.10 | 0.0 |

Kết thúc sớm (CLEANSED) trên tổng: 15.2% của 33 lần

### Thời gian dùng vật phẩm (chênh = thực − plannedMs, chỉ lượt DONE)

| item | n | DONE | chênh TB (ms) | CANCELLED % | INTERRUPTED % | ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| GOD_LEAF | 15 | 12 | 72.22 | 13.3 | 6.7 |  |
| MERMAID_TAIL | 22 | 20 | 53.33 | 4.5 | 4.5 |  |
| PHOENIX_FEATHERS | 11 | 10 | 26.67 | 0.0 | 9.1 |  |
| SPIRIT_TEAR | 13 | 12 | 47.22 | 0.0 | 7.7 |  |
| UNICORN_BLOOD | 4 | 3 | 55.56 | 25.0 | 0.0 | mẫu thấp (n<10) |

### Ẩn nấp

số đợt: 78 | dài TB: 10.74 s | người từng ẩn: 42/60 (70.0%) | bị loại khi đang ẩn: 4 | bị đánh khi ẩn: 12

### Hiệu năng

server (206 cửa sổ): tick p95 trung vị = 1.870 ms, max = 2.600 ms (mục tiêu < 33.3) | tick p99 max = 4.491 ms | droppedTicks = 0 | tickOverruns = 0 | CPU trung vị = 5.8% | RAM max = 95.8 MiB
client (3759 cửa sổ): FPS avg = 56.0 (mục tiêu ≥ 55) | 1% low trung vị = 39.6, min = 30.6 (mục tiêu ≥ 40) | frame p95 trung vị = 22.72 ms | RTT p95 trung vị = 57.9 ms | jitter trung vị = 3.5 ms | loss TB = 1.25% | transport: UDP 3446, WSS 313

## Nguồn dữ liệu theo loại (coverage.csv)

| # | type | nguồn | real | fake | trạng thái |
| --- | --- | --- | --- | --- | --- |
| 1 | match_start | S0 | 0 | 6 | fake-only |
| 2 | match_end | S0 | 0 | 6 | fake-only |
| 3 | player_spawn | S0 | 0 | 60 | fake-only |
| 4 | attack | W | 0 | 496 | fake-only |
| 5 | damage | S0 | 0 | 661 | fake-only |
| 6 | elimination | S0 | 0 | 54 | fake-only |
| 7 | cc_span | W | 0 | 33 | fake-only |
| 8 | zone_phase | S0 | 0 | 70 | fake-only |
| 9 | loot_drop | W | 0 | 199 | fake-only |
| 10 | item_pickup | W | 0 | 240 | fake-only |
| 11 | item_use | W | 0 | 65 | fake-only |
| 12 | equip_change | W | 0 | 93 | fake-only |
| 13 | hide_span | W | 0 | 78 | fake-only |
| 14 | position_sample | S0 | 0 | 12789 | fake-only |
| 15 | session_state | S0 | 0 | 9 | fake-only |
| 16 | replay_choice | S+ | 0 | 60 | fake-only |
| 17 | perf_server | S0 | 0 | 308 | fake-only |
| 18 | perf_client | C | 0 | 3759 | fake-only |
