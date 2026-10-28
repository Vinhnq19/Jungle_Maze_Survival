import csv, json, os, re, zipfile, io, hashlib
import soundfile as sf
os.chdir(os.path.abspath(os.path.join(os.path.dirname(__file__), '../../../..')))
r = json.load(open('Docs/Reports/ART-05_QA_2026-10-27/qa_measure.json', encoding='utf-8'))
sfx = r['sfx']
paths = {k: {c['path'] for c in v['clips']} for k, v in sfx.items()}
# AC13 pairs
groups = [['HitMarker', 'HurtLocal', 'ZoneBell3', 'DragonRoar', 'MatchEnd', 'Explosion'], ['ShootRanged', 'ShootCrossbow'], ['MeleeSwing', 'MeleeHit'], ['PickupWeapon', 'PickupItem', 'WearArmor']]
bad = []
for g in groups:
    for i in range(len(g)):
        for j in range(i + 1, len(g)):
            if paths[g[i]] & paths[g[j]]:
                bad.append((g[i], g[j]))
print('AC13 overlapping pairs:', bad)
allp = [p for v in paths.values() for p in v]
print('AC10 clips total', len(allp), 'unique', len(set(allp)), 'all under Sfx/:', all(p.startswith('Assets/_JungleMaze/Art/Audio/Sfx/') for p in allp), 'ids with >=1 clip:', sum(1 for v in paths.values() if v))
md5s = {}
for p in allp:
    md5s.setdefault(hashlib.md5(open(p, 'rb').read()).hexdigest(), []).append(p)
print('AC13 byte-identical clips shared:', [v for v in md5s.values() if len(v) > 1])
# AC8 names
names = [os.path.basename(p) for p in allp] + [f for f in os.listdir('Assets/_JungleMaze/Art/Audio/Music') if not f.endswith('.meta')]
print('AC8 bad names:', [n for n in names if not re.fullmatch(r'[a-z0-9_]+\.(wav|ogg|mp3)', n)], 'dup names:', len(names) - len(set(names)))
print('AC8 max variants per id:', max(len(v) for v in paths.values()))
# AC9 guid collisions
gi = {}
for base in ('Assets/Imported', 'Assets/_JungleMaze/Art/Audio'):
    for dp, _, fs in os.walk(base):
        for f in fs:
            if f.endswith('.meta'):
                m = re.search(r'^guid: (\w+)', open(os.path.join(dp, f), encoding='utf-8', errors='replace').read(), re.M)
                if m:
                    gi.setdefault(m.group(1), []).append(os.path.join(dp, f))
print('AC9 duplicate guids across Imported + Art/Audio:', {g: v for g, v in gi.items() if len(v) > 1})
# AC35 sizes
tot = 0
big = []
for d in ('Sfx', 'Music'):
    for f in os.listdir('Assets/_JungleMaze/Art/Audio/' + d):
        if f.endswith('.meta'):
            continue
        s = os.path.getsize('Assets/_JungleMaze/Art/Audio/%s/%s' % (d, f))
        tot += s
        if d == 'Sfx' and s > 500000:
            big.append((f, s))
        if d == 'Music' and s > 3_000_000:
            big.append((f, s))
print('AC35 total bytes', tot, '= %.2f MB' % (tot / 1e6), 'files over per-file limit:', big)
sizes = {f: os.path.getsize('Assets/_JungleMaze/Art/Audio/Music/' + f) for f in os.listdir('Assets/_JungleMaze/Art/Audio/Music') if not f.endswith('.meta')}
print('Music sizes', sizes)
# any >=5MB under _JungleMaze added? any file >=3MB outside Music in Art/Audio
allbig = []
for dp, _, fs in os.walk('Assets/_JungleMaze'):
    for f in fs:
        if not f.endswith('.meta') and os.path.getsize(os.path.join(dp, f)) >= 5_000_000:
            allbig.append((os.path.join(dp, f), os.path.getsize(os.path.join(dp, f))))
print('files >= 5MB under Assets/_JungleMaze:', allbig)
print('Waiting Room wav copied?', os.path.exists('Assets/_JungleMaze/Art/Audio/Music/Waiting Room Music.wav'), [p for dp, _, fs in os.walk('Assets/_JungleMaze') for p in fs if p.lower().startswith('waiting room')])
# AC29 durations vs originals
orig = {'waiting_room.ogg': 145.53, 'loading.ogg': 43.65, 'match.ogg': 123.48}
for k, v in orig.items():
    i = sf.info('Assets/_JungleMaze/Art/Audio/Music/' + k)
    print('AC29', k, '%.3f s' % i.duration, 'vs', v, 'delta %.3f' % (i.duration - v))
for k, src in (('waiting_room', 'Waiting Room Music.wav'), ('loading', 'Loading Music.mp3'), ('match', 'ParkingMapMusic.mp3')):
    i = sf.info('Assets/Imported/JSurviv/Sound/' + src)
    j = sf.info('Assets/_JungleMaze/Art/Audio/Music/%s.ogg' % k)
    print('AC29 vs real original', k, 'orig %.3f new %.3f delta %.3f' % (i.duration, j.duration, j.duration - i.duration))
# AC2: manifest facts vs re-measure
rows = list(csv.DictReader(open('Docs/Tickets/ART-05_audio_manifest.csv', encoding='utf-8', newline='')))
sd = 'Assets/Imported/JSurviv/Sound'
bad2 = []
n = 0
for row in rows:
    if row['row_type'] != 'jsurviv':
        continue
    if row['file'].endswith('.zip') and not row['zip_member']:
        if os.path.getsize(os.path.join(sd, row['file'])) != int(row['size_bytes']):
            bad2.append(('zipsize', row['file']))
        n += 1
        continue
    n += 1
    if row['zip_member']:
        z = zipfile.ZipFile(os.path.join(sd, row['file']))
        data = z.read(row['zip_member'])
        size = len(data)
        info = sf.info(io.BytesIO(data))
    else:
        size = os.path.getsize(os.path.join(sd, row['file']))
        info = sf.info(os.path.join(sd, row['file']))
    if size != int(row['size_bytes']):
        bad2.append(('size', row['file'], row['zip_member'], size, row['size_bytes']))
    if abs(info.duration - float(row['duration_s'])) > 0.01:
        bad2.append(('dur', row['file'], row['zip_member'], round(info.duration, 3), row['duration_s']))
    if info.samplerate != int(row['sample_rate']) or info.channels != int(row['channels']):
        bad2.append(('fmt', row['file'], row['zip_member'], info.samplerate, info.channels, row['sample_rate'], row['channels']))
print('AC2 jsurviv rows re-measured:', n, 'mismatches:', bad2)
# AC5 zip notes
inv = list(csv.DictReader(open('Tools/Audio/art05_jsurviv_inventory.csv', encoding='utf-8', newline='')))
print('AC5 notes on zips:', [(i['path'], i['zip_member'][:30], i['notes'][:90]) for i in inv if i['path'].endswith('.zip')][:3])
print('AC5 sfx row ZIP note M5:', [x['notes'][:120] for x in rows if x['row_type'] == 'jsurviv' and 'bot di chuyển.zip' in x['file']][:2])
