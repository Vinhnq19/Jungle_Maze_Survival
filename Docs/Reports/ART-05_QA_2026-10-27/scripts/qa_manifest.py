import csv, hashlib, os, re, zipfile, collections
os.chdir('E:/Github/Jungle_Maze_Survival')
rows = list(csv.DictReader(open('Docs/Tickets/ART-05_audio_manifest.csv', encoding='utf-8', newline='')))
print('rows', len(rows), dict(collections.Counter(r['row_type'] for r in rows)))
names = ['FootstepGrass','FootstepSwamp','BushEnter','MeleeSwing','MeleeHit','ShootRanged','ShootCrossbow','Throw','Reload','EmptyClick','BombFuse','Explosion','TrapTriggered','HitMarker','HurtLocal','ArmorAbsorb','Eliminated','PickupWeapon','PickupItem','WearArmor','UseHeal','UseDrink','UseCancel','ZoneBell3','DragonRoar','MatchEnd']
sfx = [r for r in rows if r['row_type'] == 'sfx']
print('AC1 order match:', [r['sfx_id'] for r in sfx] == names, dict(collections.Counter(r['status'] for r in sfx)))
js = [r for r in rows if r['row_type'] == 'jsurviv']
clips = [r for r in rows if r['row_type'] == 'clip']
sd = 'Assets/Imported/JSurviv/Sound'
files = []
for dp, _, fs in os.walk(sd):
    for f in fs:
        if not f.endswith('.meta'):
            files.append(os.path.relpath(os.path.join(dp, f), sd).replace(os.sep, '/'))
zm = 0
for f in files:
    if f.endswith('.zip'):
        zm += len(zipfile.ZipFile(os.path.join(sd, f)).namelist())
print('Imported files', len(files), 'zip members', zm)
print('jsurviv rows', len(js), dict(collections.Counter(r['class'] for r in js)))
print('jsurviv sample', {k: js[0][k] for k in ('file', 'source', 'zip_member', 'class', 'licence', 'md5')})
nonzip_rows = {r['file'] for r in js if not r['zip_member']}
print('files without row:', [f for f in files if f not in nonzip_rows and not f.endswith('.zip')])
for f in files:
    if f.endswith('.zip'):
        for m in zipfile.ZipFile(os.path.join(sd, f)).namelist():
            if not any(r['zip_member'] == m and r['file'] == f for r in js):
                print('zip member without row', f, m)
bad = []
vocab = collections.Counter()
forbid = re.compile(r'miễn phí|CC0|royalty|được phép', re.I)
for r in rows:
    l = r['licence']
    if l.startswith('có bằng chứng:'):
        p = l.split(':', 1)[1].strip()
        vocab['có bằng chứng'] += 1
        if not os.path.exists(p):
            bad.append(('evidence missing', r['row_type'], r['file'], p))
    elif l == 'chưa kiểm chứng':
        vocab['chưa kiểm chứng'] += 1
    elif l.startswith('chưa kiểm chứng ('):
        vocab[l] += 1
    elif l.startswith('tự tạo'):
        vocab['tự tạo'] += 1
    elif l == '':
        vocab['empty:' + r['row_type']] += 1
    else:
        vocab['OTHER:' + l] += 1
    if forbid.search(l) and not l.startswith('có bằng chứng:') and l != 'chưa kiểm chứng (nguồn tự khai CC0)':
        bad.append(('forbidden word', r['row_type'], r['file'], l))
print('licence vocab', dict(vocab))
print('licence problems', bad)
print('jsurviv rows not "chưa kiểm chứng":', [(r['file'], r['licence']) for r in js if r['licence'] != 'chưa kiểm chứng'])
print('derived JSurviv clip rows licence:', [(r['file'], r['licence']) for r in clips if r['source'] and not r['url'] and r['file'].startswith(('Sfx', 'Music'))])
dr = [r for r in rows if 'dragon' in (r['file'] + r['sfx_id']).lower()]
print('DragonRoar rows:', [(r['row_type'], r['licence']) for r in dr])
print('TimeOver note:', [r['notes'][:160] for r in js if 'TimeOver' in r['file']])
bad2 = []
for r in clips:
    p = os.path.join('Assets/_JungleMaze/Art/Audio', r['file'])
    if not os.path.exists(p):
        bad2.append(('missing', r['file']))
        continue
    if hashlib.md5(open(p, 'rb').read()).hexdigest() != r['md5'] or os.path.getsize(p) != int(r['size_bytes']):
        bad2.append(('md5/size', r['file']))
ondisk = [d + '/' + f for d in ('Sfx', 'Music') for f in os.listdir('Assets/_JungleMaze/Art/Audio/' + d) if not f.endswith('.meta')]
print('clip rows', len(clips), 'on disk', len(ondisk), 'row-without-file/mismatch', bad2, 'file-without-row', sorted(set(ondisk) - {r['file'] for r in clips}))
dl = [r for r in clips if r['url']]
print('downloaded clips (url):', len(dl), 'missing author/date/licence:', [r['file'] for r in dl if not r['author'] or not r['download_date'] or not r['licence']])
print('downloaded licence mix:', dict(collections.Counter(r['licence'] for r in dl)))
print('copy_md5_match:', dict(collections.Counter(r['copy_md5_match'] for r in js)))
print('heard clip:', dict(collections.Counter(r['heard'] for r in clips)))
print('heard sfx rows:', {r['sfx_id']: r['heard'] for r in sfx if r['heard'] != 'chưa'})
print('exceptions:', [(r['file'], r['exception'][:80]) for r in clips if r['exception']])
print('reduction_db>6:', [(r['file'], r['reduction_db']) for r in clips if r['reduction_db'] and float(r['reduction_db']) > 6.0001])
# before/after recorded for all 40 sfx clips (AC40)
sc = [r for r in clips if r['file'].startswith('Sfx/')]
print('sfx clip rows', len(sc), 'missing before/after:', [r['file'] for r in sc if not (r['peak_before_dbfs'] and r['level_before_dbfs'] and r['peak_after_dbfs'] and r['level_after_dbfs'])])
# used_by count of 12 jsurviv + 2 music
print('jsurviv rows used by SFX/BGM:', dict(collections.Counter(r['class'] for r in js)))
