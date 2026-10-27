"""Pick, trim and loudness-normalise CC0 clips for the 14 SfxIds that were still beeps."""
import sys, json
from pathlib import Path
import numpy as np, soundfile as sf

S = Path(sys.argv[1]); OUT = Path(sys.argv[2]); OUT.mkdir(parents=True, exist_ok=True)
IMP = S/'sfx_dl/kenney_impact-sounds/x'; UI = S/'sfx_dl/kenney_interface-sounds/x'
RPG = S/'sfx_dl/kenney_rpg-audio/x'; ROAR = S/'sfx_dl/oga_roar/monster_roar.wav'
def find(root, name): return next(root.rglob(name))
K = {'impact': ('Kenney Impact Sounds', 'https://kenney.nl/assets/impact-sounds'),
     'ui': ('Kenney Interface Sounds', 'https://kenney.nl/assets/interface-sounds'),
     'rpg': ('Kenney RPG Audio', 'https://kenney.nl/assets/rpg-audio'),
     'oga': ('OpenGameArt "CC0 deep monster roar" by trazzz123', 'https://opengameart.org/content/cc0-deep-monster-roar')}
PLAN = [  # sfx id, out stem, pack, source files, target RMS dBFS, max seconds
    ('FootstepGrass', 'footstep_grass', 'impact', [f'footstep_grass_00{i}.ogg' for i in range(5)], -24, 0.5),
    ('FootstepSwamp', 'footstep_swamp', 'impact', [f'impactSoft_medium_00{i}.ogg' for i in range(5)], -24, 0.5),
    ('BushEnter', 'bush_enter', 'rpg', ['cloth1.ogg', 'cloth3.ogg'], -22, 0.8),
    ('ShootCrossbow', 'shoot_crossbow', 'ui', ['pluck_001.ogg'], -18, 0.6),
    ('Throw', 'throw', 'rpg', ['drawKnife2.ogg'], -20, 0.7),
    ('Reload', 'reload', 'rpg', ['metalLatch.ogg'], -20, 1.0),
    ('EmptyClick', 'empty_click', 'rpg', ['metalClick.ogg'], -20, 0.4),
    ('TrapTriggered', 'trap_triggered', 'impact', ['impactMetal_heavy_000.ogg'], -17, 1.0),
    ('HitMarker', 'hit_marker', 'ui', ['glass_005.ogg'], -16, 0.25),
    ('ArmorAbsorb', 'armor_absorb', 'impact', [f'impactPlate_medium_00{i}.ogg' for i in range(3)], -18, 0.6),
    ('Eliminated', 'eliminated', 'ui', ['minimize_006.ogg'], -16, 1.5),
    ('UseCancel', 'use_cancel', 'ui', ['back_002.ogg'], -20, 0.5),
    ('ZoneBell3', 'zone_bell', 'impact', ['impactBell_heavy_000.ogg'], -15, 0.75),
    ('DragonRoar', 'dragon_roar', 'oga', ['monster_roar.wav'], -15, 3.0),
]
SR = 44100
def load(path):
    x, sr = sf.read(path, always_2d=True, dtype='float64'); x = x.mean(axis=1)
    if sr != SR:
        t = np.arange(int(len(x)*SR/sr))*sr/SR; x = np.interp(t, np.arange(len(x)), x)
    return x
def trim(x, max_s):
    env = np.abs(x); thr = env.max()*10**(-45/20); idx = np.nonzero(env > thr)[0]
    x = x[max(0, idx[0]-int(0.002*SR)): idx[-1]+1]
    x = x[:int(max_s*SR)]
    n = min(len(x)//4, int(0.03*SR)); x[-n:] *= np.linspace(1, 0, n)
    return x
def rms_db(x): return 20*np.log10(np.sqrt(np.mean(x**2))+1e-12)
rows = []
for sfx, stem, pack, files, target, max_s in PLAN:
    for i, name in enumerate(files):
        src = ROAR if pack == 'oga' else find({'impact': IMP, 'ui': UI, 'rpg': RPG}[pack], name)
        x = trim(load(src), max_s)
        x *= 10**((target-rms_db(x))/20)
        peak = np.abs(x).max(); limit = 10**(-1/20)
        if peak > limit: x *= limit/peak
        out = OUT/(f'{stem}_{i}.wav' if len(files) > 1 else f'{stem}.wav')
        sf.write(out, x, SR, subtype='PCM_16')
        rows.append(dict(sfx=sfx, file=out.name, source=f'{K[pack][0]}: {name}', url=K[pack][1], license='CC0 1.0',
                         seconds=round(len(x)/SR, 3), rms_db=round(rms_db(x), 1), peak_db=round(20*np.log10(np.abs(x).max()), 1)))
(OUT/'sources.json').write_text(json.dumps(rows, indent=1), encoding='utf-8')
for r in rows: print(f"{r['sfx']:14} {r['file']:22} {r['seconds']:6}s rms {r['rms_db']:6} peak {r['peak_db']:5}  {r['source']}")
