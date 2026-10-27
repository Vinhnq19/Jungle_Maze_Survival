import csv, sys
p = sys.argv[1]
rows = list(csv.DictReader(open(p, encoding='utf-8', newline='')))
fields = list(rows[0].keys())
for r in rows:
    if r['row_type'] == 'sfx' and r['sfx_id'] == 'UseHeal': r['heard'] = 'có: QA-test, 2026-10-27'
    if r['row_type'] == 'clip' and r['file'] == 'Sfx/use_heal.wav': r['heard'] = 'có: QA-test, 2026-10-27'
    if r['row_type'] == 'sfx' and r['sfx_id'] == 'Explosion': r['heard'] = 'giật: QA-test'
    if r['row_type'] == 'clip' and r['file'] == 'Sfx/footstep_grass_2.wav': r['heard'] = 'có: QA-test clip only'
    if r['row_type'] == 'clip' and r['file'] == 'Music/match.ogg': r['heard'] = 'có: QA-test music'
w = csv.DictWriter(open(p, 'w', encoding='utf-8', newline=''), fieldnames=fields, lineterminator='\n'); w.writeheader(); [w.writerow(r) for r in rows]
