"""Build Docs/Tickets/ART-05_audio_manifest.csv from the recipe, the JSurviv inventory and the files in the repo.

Three kinds of row (column row_type):
  sfx      26 rows in SfxId order: inventory status of 27/10 (2.2), JSurviv source facts, final clips,
           catalog volume and effective level E = mean L + 20*log10(volume).
  jsurviv  30 rows: the 21 files of Assets/Imported/JSurviv/Sound and the 9 zip members, with class, md5,
           and for the 14 AUD-01 copies whether the copy is still byte-identical (AC38).
  clip     one row per file in Art/Audio/Sfx and Art/Audio/Music: source, licence, cut, gain, levels before
           and after (after = the file in the repo), size and md5.

Needs numpy and soundfile (reads the MP3 sources). Usage:
  python Tools/Audio/build_manifest.py [--report %TEMP%/art05_stage/process_report.csv]
"""
from pathlib import Path
import argparse
import csv
import hashlib
import io
import math
import os
import tempfile
import zipfile

import numpy as np
import soundfile as sf

import measure_clips

ROOT = Path(__file__).resolve().parents[2]
RECIPE = ROOT / 'Tools/Audio/art05_recipe.csv'
INVENTORY = ROOT / 'Tools/Audio/art05_jsurviv_inventory.csv'
JSURVIV = ROOT / 'Assets/Imported/JSurviv/Sound'
COPIES = ROOT / 'Assets/_JungleMaze/Art/Audio/Placeholder/JSurviv'
AUDIO = ROOT / 'Assets/_JungleMaze/Art/Audio'
OUT = ROOT / 'Docs/Tickets/ART-05_audio_manifest.csv'

# SfxId order (AUD-01 enum), inventory status of 27/10 (ticket 2.2) and the JSurviv file it was judged on.
SFX = [
    ('FootstepGrass', 'Cần chỉnh', 'bot di chuyển.mp3'), ('FootstepSwamp', 'Thiếu', ''), ('BushEnter', 'Thiếu', ''),
    ('MeleeSwing', 'Có', 'melee chém.mp3'), ('MeleeHit', 'Cần chỉnh', 'Đập.wav'), ('ShootRanged', 'Có', 'súng bắn.mp3'),
    ('ShootCrossbow', 'Thiếu', ''), ('Throw', 'Thiếu', ''), ('Reload', 'Thiếu', ''), ('EmptyClick', 'Thiếu', ''),
    ('BombFuse', 'Cần chỉnh', 'countdown.mp3'), ('Explosion', 'Cần chỉnh', 'bomb nổ.mp3'), ('TrapTriggered', 'Thiếu', ''),
    ('HitMarker', 'Thiếu', ''), ('HurtLocal', 'Cần chỉnh', 'bleed.mp3'), ('ArmorAbsorb', 'Thiếu', ''),
    ('Eliminated', 'Thiếu', ''), ('PickupWeapon', 'Có', 'nhặt súng.mp3'), ('PickupItem', 'Cần chỉnh', 'Nhặt đồ/Gun Put On.mp3'),
    ('WearArmor', 'Cần chỉnh', 'bot di chuyển.zip'), ('UseHeal', 'Cần chỉnh', 'gauze.mp3'), ('UseDrink', 'Cần chỉnh', 'cola.mp3'),
    ('UseCancel', 'Thiếu', ''), ('ZoneBell3', 'Thiếu', ''), ('DragonRoar', 'Thiếu', ''), ('MatchEnd', 'Có', 'TimeOver.wav'),
]
# Catalog volume per tier (plan 4.1, G1 T-5); footsteps 0.45 keeps A19 (HitMarker >= 2 x FootstepGrass).
TIER_VOLUME = {'T1': 1.00, 'T2': 0.79, 'T3': 0.50, 'T4': 0.28}
VOLUME_OVERRIDE = {'FootstepGrass': 0.45, 'FootstepSwamp': 0.45}

FIELDS = ['row_type', 'sfx_id', 'status', 'file', 'source', 'zip_member', 'class', 'used_by', 'author', 'url',
          'download_date', 'licence', 'duration_s', 'codec', 'sample_rate', 'channels', 'size_bytes', 'md5',
          'copy_md5_match', 'tier', 'trim_start_s', 'trim_end_s', 'gain_db', 'reduction_db', 'peak_before_dbfs',
          'level_before_dbfs', 'peak_after_dbfs', 'level_after_dbfs', 'volume', 'effective_dbfs', 'heard', 'notes']


def md5(data):
    return hashlib.md5(data).hexdigest()


def volume_of(sfx_id, tier):
    return VOLUME_OVERRIDE.get(sfx_id, TIER_VOLUME[tier])


def source_facts(path, member=''):
    """Duration, codec, rate, channels, size, md5 of a JSurviv file or zip member (read only)."""
    if member:
        with zipfile.ZipFile(path) as zf:
            data = zf.read(member)
    else:
        data = path.read_bytes()
    facts = {'size_bytes': len(data), 'md5': md5(data)}
    if (member or path.name).lower().endswith('.zip'):
        facts['codec'] = 'zip'
        return facts
    info = sf.info(io.BytesIO(data))
    kbps = round(len(data) * 8 / info.duration / 1000) if info.duration else 0
    facts.update(duration_s=f'{info.duration:.2f}', codec=f'{info.format} {info.subtype} {kbps} kbps',
                 sample_rate=info.samplerate, channels=info.channels)
    return facts


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('--report', default=os.path.join(tempfile.gettempdir(), 'art05_stage', 'process_report.csv'))
    args = parser.parse_args()
    with open(RECIPE, encoding='utf-8', newline='') as f:
        recipe = list(csv.DictReader(f))
    with open(args.report, encoding='utf-8', newline='') as f:
        report = {r['out_file']: r for r in csv.DictReader(f)}
    with open(INVENTORY, encoding='utf-8', newline='') as f:
        inventory = list(csv.DictReader(f))

    clips, levels = [], {}
    for r in recipe:
        path = AUDIO / r['out_file']
        rep = report[r['out_file']]
        row = {k: r[k] for k in ('sfx_id', 'source', 'zip_member', 'author', 'url', 'download_date', 'licence', 'heard', 'notes')}
        row.update(row_type='clip', file=r['out_file'], tier=r['tier'], size_bytes=path.stat().st_size,
                   md5=md5(path.read_bytes()), trim_start_s=f"{float(rep['trim_start_s']):.3f}",
                   trim_end_s=f"{float(rep['trim_end_s']):.3f}", gain_db=f"{float(rep['gain_db']):.2f}",
                   reduction_db=f"{float(rep['reduction_db']):.2f}",
                   peak_before_dbfs=f"{float(rep['source_peak_dbfs']):.2f}",
                   level_before_dbfs=f"{float(rep['source_level_dbfs']):.2f}")
        if r['sfx_id'] == 'music':
            info = sf.info(str(path))
            row.update(sfx_id='', used_by=Path(r['out_file']).stem, duration_s=f'{info.duration:.2f}',
                       codec=f"OGG Vorbis q{rep['vorbis_quality']}", sample_rate=info.samplerate, channels=info.channels,
                       peak_after_dbfs=f"{float(rep['peak_dbfs']):.2f}", level_after_dbfs=f"{float(rep['level_dbfs']):.2f}")
        else:
            m = measure_clips.measure(path)
            volume = volume_of(r['sfx_id'], r['tier'])
            row.update(duration_s=f"{m['duration_s']:.3f}", codec='WAV PCM_16', sample_rate=m['sample_rate'],
                       channels=m['channels'], peak_after_dbfs=f"{m['peak_dbfs']:.2f}", level_after_dbfs=f"{m['level_dbfs']:.2f}",
                       volume=f'{volume:.2f}', effective_dbfs=f"{m['level_dbfs'] + 20 * math.log10(volume):.2f}")
            levels.setdefault(r['sfx_id'], []).append((m['level_dbfs'], r))
        clips.append(row)

    sfx_rows = []
    for sfx_id, status, source in SFX:
        entries = levels[sfx_id]
        tier = entries[0][1]['tier']
        volume = volume_of(sfx_id, tier)
        level = sum(l for l, _ in entries) / len(entries)
        licences = sorted({e['licence'] for _, e in entries})
        row = dict(row_type='sfx', sfx_id=sfx_id, status=status, file=';'.join(e['out_file'] for _, e in entries),
                   source=source, tier=tier, licence=' | '.join(licences), level_after_dbfs=f'{level:.2f}',
                   volume=f'{volume:.2f}', effective_dbfs=f'{level + 20 * math.log10(volume):.2f}', heard='chưa')
        if source:
            row.update(source_facts(JSURVIV / source))
            row.pop('md5', None)
        jsurviv_used = any(e['source_kind'] == 'jsurviv' for _, e in entries)
        row['notes'] = 'clip cuối từ JSurviv' if jsurviv_used else ('ứng viên JSurviv không dùng; clip cuối tải về' if source else 'clip cuối tải về')
        sfx_rows.append(row)

    jsurviv_rows = []
    for inv in inventory:
        facts = source_facts(JSURVIV / inv['path'], inv['zip_member'])
        row = dict(row_type='jsurviv', file=inv['path'], zip_member=inv['zip_member'], **{'class': inv['class']},
                   used_by=inv['used_by'], licence='chưa kiểm chứng', notes=inv['notes'], **facts)
        if inv['placeholder_copy']:
            copy = COPIES / inv['placeholder_copy']
            row['copy_md5_match'] = ('có' if copy.is_file() and md5(copy.read_bytes()) == facts['md5'] else 'không') \
                + ': Placeholder/JSurviv/' + inv['placeholder_copy']
        jsurviv_rows.append(row)

    OUT.parent.mkdir(parents=True, exist_ok=True)
    with open(OUT, 'w', encoding='utf-8', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=FIELDS, lineterminator='\n')
        writer.writeheader()
        for row in sfx_rows + jsurviv_rows + clips:
            writer.writerow(row)
    print(f'{OUT}: {len(sfx_rows)} sfx, {len(jsurviv_rows)} jsurviv, {len(clips)} clip rows')


if __name__ == '__main__':
    main()
