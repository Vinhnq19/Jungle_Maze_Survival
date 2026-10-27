"""QA ART-05: independent measurement of the final clips (does not reuse Tools/Audio code).
Reads: SfxCatalog.asset, AudioRuntimeSet.asset, Sfx/*.wav, Music/*.ogg. Writes only the JSON/CSV given on the command line."""
import csv, hashlib, json, math, os, re, sys
import numpy as np
import soundfile as sf

ROOT = sys.argv[1]
OUT = sys.argv[2]
AUDIO = os.path.join(ROOT, 'Assets/_JungleMaze/Art/Audio')

NAMES = ['FootstepGrass', 'FootstepSwamp', 'BushEnter', 'MeleeSwing', 'MeleeHit', 'ShootRanged', 'ShootCrossbow', 'Throw',
         'Reload', 'EmptyClick', 'BombFuse', 'Explosion', 'TrapTriggered', 'HitMarker', 'HurtLocal', 'ArmorAbsorb',
         'Eliminated', 'PickupWeapon', 'PickupItem', 'WearArmor', 'UseHeal', 'UseDrink', 'UseCancel', 'ZoneBell3',
         'DragonRoar', 'MatchEnd']
# ticket 2.3: (min length, max length, tier E)
SPEC = {
    'FootstepGrass': (0.05, 0.40, -29), 'FootstepSwamp': (0.08, 0.50, -29), 'BushEnter': (0.10, 0.60, -29),
    'MeleeSwing': (0.15, 0.60, -20), 'MeleeHit': (0.10, 0.50, -20), 'ShootRanged': (0.15, 1.20, -20),
    'ShootCrossbow': (0.15, 1.00, -20), 'Throw': (0.10, 0.60, -20), 'Reload': (0.20, 1.50, -24),
    'EmptyClick': (0.03, 0.30, -24), 'BombFuse': (0.80, 1.50, -20), 'Explosion': (0.50, 3.00, -20),
    'TrapTriggered': (0.10, 0.80, -20), 'HitMarker': (0.03, 0.15, -16), 'HurtLocal': (0.20, 0.60, -16),
    'ArmorAbsorb': (0.10, 0.40, -20), 'Eliminated': (0.30, 1.50, -20), 'PickupWeapon': (0.15, 0.80, -24),
    'PickupItem': (0.15, 0.80, -24), 'WearArmor': (0.30, 1.20, -24), 'UseHeal': (0.50, 1.50, -24),
    'UseDrink': (0.50, 1.50, -24), 'UseCancel': (0.10, 0.40, -24), 'ZoneBell3': (0.40, 0.75, -16),
    'DragonRoar': (1.50, 4.00, -16), 'MatchEnd': (1.00, 3.00, -16)}
STAR = {'MeleeSwing', 'MeleeHit', 'ShootRanged', 'ShootCrossbow', 'HitMarker', 'HurtLocal', 'PickupWeapon', 'PickupItem',
        'WearArmor', 'ZoneBell3', 'DragonRoar'}


def db(x):
    return 20 * math.log10(x) if x > 0 else -240.0


def md5(p):
    return hashlib.md5(open(p, 'rb').read()).hexdigest()


# guid -> path (all audio .meta in _JungleMaze and Imported)
guid_path = {}
for base in ('Assets/_JungleMaze', 'Assets/Imported'):
    for dp, _, fs in os.walk(os.path.join(ROOT, base)):
        for f in fs:
            if f.endswith('.meta'):
                m = re.search(r'^guid: (\w+)', open(os.path.join(dp, f), encoding='utf-8', errors='replace').read(), re.M)
                if m:
                    guid_path[m.group(1)] = os.path.relpath(os.path.join(dp, f[:-5]), ROOT).replace('\\', '/')

# catalog
text = open(os.path.join(AUDIO, 'SfxCatalog.asset'), encoding='utf-8').read()
entries = {}
for blk in re.split(r'\n  - id: ', text)[1:]:
    idn = int(blk.split('\n')[0])
    guids = re.findall(r'clips:\n((?:    - .*\n)+)', blk)
    guids = re.findall(r'guid: (\w+)', guids[0]) if guids else []
    g = lambda k: re.search(k + r': ([-\d.eE]+)', blk).group(1)
    entries[idn] = dict(clips=guids, volume=float(g('volume')), pitchJitter=float(g('pitchJitter')),
                        priority=int(g('priority')), bus=int(g('bus')), rangeCells=float(g('rangeCells')),
                        maxInstances=int(g('maxInstances')), cooldown=float(g('cooldownSeconds')),
                        repeatCount=int(g('repeatCount')), repeatInterval=float(g('repeatIntervalSeconds')))


def measure(path):
    data, sr = sf.read(os.path.join(ROOT, path), dtype='float64', always_2d=True)
    ch = data.shape[1]
    x = data.mean(axis=1)
    peak = float(np.max(np.abs(x))) if len(x) else 0.0
    idx = np.nonzero(np.abs(x) >= 0.003)[0]
    if len(idx):
        seg = x[idx[0]:idx[-1] + 1]
        L = db(float(np.sqrt(np.mean(seg ** 2))))
    else:
        L = -240.0
    n2 = int(round(sr * 0.002))
    head = float(np.max(np.abs(x[:n2]))) if len(x) else 0
    tail = float(np.max(np.abs(x[-n2:]))) if len(x) else 0
    ceil = peak * 0.9999
    run, best = 0, 0
    for v in np.abs(x) >= ceil:
        run = run + 1 if v else 0
        best = max(best, run)
    return dict(path=path, sr=sr, ch=ch, frames=len(x), dur=len(x) / sr, size=os.path.getsize(os.path.join(ROOT, path)),
                peak_db=db(peak), L_db=L, head_db=db(head), tail_db=db(tail), ceil_run=best,
                md5=md5(os.path.join(ROOT, path)))


res = dict(sfx={}, music={}, problems=[])
all_guids = []
for i, name in enumerate(NAMES, start=1):
    e = entries.get(i)
    if not e:
        res['problems'].append(f'{name}: no catalog entry')
        continue
    clips = []
    for g in e['clips']:
        all_guids.append(g)
        p = guid_path.get(g)
        if not p:
            res['problems'].append(f'{name}: guid {g} resolves to no asset')
            continue
        clips.append(measure(p))
    lo, hi, tier = SPEC[name]
    Ls = [c['L_db'] for c in clips]
    Lmean = float(np.mean(Ls)) if Ls else None
    vol = e['volume']
    E = (Lmean + 20 * math.log10(vol)) if Lmean is not None and vol > 0 else None
    res['sfx'][name] = dict(entry=e, clips=clips, len_range=[lo, hi], tier=tier, L_mean=Lmean, E=E,
                            E_per_clip=[c['L_db'] + 20 * math.log10(vol) for c in clips])

# music
rs = open(os.path.join(AUDIO, 'Resources/AudioRuntimeSet.asset'), encoding='utf-8').read()
for key in ('waitingRoomMusic', 'loadingMusic', 'matchMusic'):
    g = re.search(key + r': \{fileID: \d+, guid: (\w+)', rs).group(1)
    p = guid_path.get(g)
    all_guids.append(g)
    if not p:
        res['problems'].append(f'{key}: guid {g} resolves to no asset')
        continue
    data, sr = sf.read(os.path.join(ROOT, p), dtype='float64', always_2d=True)
    x = data.mean(axis=1)
    peak = float(np.max(np.abs(x)))
    idx = np.nonzero(np.abs(x) >= 0.003)[0]
    seg = x[idx[0]:idx[-1] + 1]
    res['music'][key] = dict(path=p, sr=sr, ch=data.shape[1], dur=len(x) / sr, size=os.path.getsize(os.path.join(ROOT, p)),
                             peak_db=db(peak), L_db=db(float(np.sqrt(np.mean(seg ** 2)))))

res['all_guids_in_imported'] = [g for g in all_guids if guid_path.get(g, '').startswith('Assets/Imported')]
res['all_guid_paths'] = sorted({guid_path.get(g, '?') for g in all_guids})
res['beep_refs'] = [p for p in res['all_guid_paths'] if '/Placeholder/' in p]
json.dump(res, open(OUT, 'w', encoding='utf-8'), indent=1)
print('wrote', OUT, 'sfx', len(res['sfx']), 'music', len(res['music']), 'problems', res['problems'])
