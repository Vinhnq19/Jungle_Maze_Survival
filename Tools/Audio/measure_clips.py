"""Measure 16-bit PCM WAV clips the way Docs/Tickets/ART-05.md 2.6 defines it (standard library only).

Per file, on the mono mix: peak (dBFS), active level L (RMS between the first and the last sample with
|x| >= 0.003, G-7), the loudest sample in the first and last 2 ms, the longest run of samples at full scale,
duration, sample rate, channels and size in bytes.

Usage: python -I Tools/Audio/measure_clips.py [FILE_OR_DIR ...] [--csv OUT.csv]
Default input: Assets/_JungleMaze/Art/Audio/Sfx.
"""
from pathlib import Path
import argparse
import array
import csv
import math
import sys
import wave

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_DIR = ROOT / 'Assets/_JungleMaze/Art/Audio/Sfx'
ACTIVE_FLOOR = 0.003
EDGE_S = 0.002
FIELDS = ['file', 'duration_s', 'sample_rate', 'channels', 'size_bytes', 'peak_dbfs', 'level_dbfs',
          'head_2ms_dbfs', 'tail_2ms_dbfs', 'full_scale_run']


def db(v):
    return 20.0 * math.log10(max(v, 1e-12))


def read_mono(path):
    """Samples of a 16-bit PCM WAV, mixed to mono, scaled to -1..1; plus (rate, channels)."""
    with wave.open(str(path), 'rb') as w:
        if w.getsampwidth() != 2:
            raise ValueError(f'{path}: only 16-bit PCM is supported')
        rate, channels, frames = w.getframerate(), w.getnchannels(), w.getnframes()
        data = array.array('h', w.readframes(frames))
    if sys.byteorder == 'big':
        data.byteswap()
    if channels == 1:
        mono = [s / 32768.0 for s in data]
    else:
        mono = [sum(data[i:i + channels]) / (32768.0 * channels) for i in range(0, len(data), channels)]
    return mono, rate, channels, data


def measure(path):
    path = Path(path)
    mono, rate, channels, raw = read_mono(path)
    peak = max((abs(s) for s in mono), default=0.0)
    active = [i for i, s in enumerate(mono) if abs(s) >= ACTIVE_FLOOR]
    level = -120.0
    if active:
        seg = mono[active[0]:active[-1] + 1]
        level = db(math.sqrt(sum(s * s for s in seg) / len(seg)))
    edge = max(1, int(EDGE_S * rate))
    run = best = 0
    for s in raw:
        run = run + 1 if s in (32767, -32768) else 0
        best = max(best, run)
    return {
        'file': path.name,
        'duration_s': round(len(mono) / rate, 4),
        'sample_rate': rate,
        'channels': channels,
        'size_bytes': path.stat().st_size,
        'peak_dbfs': round(db(peak), 2),
        'level_dbfs': round(level, 2),
        'head_2ms_dbfs': round(db(max(abs(s) for s in mono[:edge])), 2),
        'tail_2ms_dbfs': round(db(max(abs(s) for s in mono[-edge:])), 2),
        'full_scale_run': best,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('paths', nargs='*', default=[str(DEFAULT_DIR)])
    parser.add_argument('--csv', help='write the table to this CSV file')
    args = parser.parse_args()
    files = []
    for p in map(Path, args.paths):
        files.extend(sorted(p.glob('*.wav')) if p.is_dir() else [p])
    rows = [measure(f) for f in files]
    for r in rows:
        print(f"{r['file']:24} {r['duration_s']:7.3f}s {r['sample_rate']}Hz {r['channels']}ch peak {r['peak_dbfs']:6.1f} "
              f"L {r['level_dbfs']:6.1f} head {r['head_2ms_dbfs']:7.1f} tail {r['tail_2ms_dbfs']:7.1f} run {r['full_scale_run']}")
    if args.csv:
        with open(args.csv, 'w', encoding='utf-8', newline='') as f:
            writer = csv.DictWriter(f, fieldnames=FIELDS, lineterminator='\n')
            writer.writeheader()
            writer.writerows(rows)


if __name__ == '__main__':
    main()
