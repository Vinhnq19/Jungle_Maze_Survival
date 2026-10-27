"""Render the ART-05 clips from their untouched sources, following Tools/Audio/art05_recipe.csv.

SFX (one WAV per recipe row): mono mix, resample to 44.1 kHz with a windowed sinc when the source differs, cut
from the onset, fade-in 3 ms after 2 ms of silence, cosine fade-out followed by 2 ms of silence, then bring the
active level L (Docs/Tickets/ART-05.md 2.6, G-7) to the row's target through a look-ahead limiter: ceiling
-1.05 dBFS, attack 1 ms, release 50 ms, at most 6 dB of gain reduction unless the row allows more (max_reduction_db). Written as 16-bit PCM, so a rerun gives
the same bytes. Music: level only (stereo kept), then OGG Vorbis at the highest quality that stays <= 3.0 MB.

Needs numpy and soundfile (libsndfile >= 1.1 reads MP3 and writes Vorbis); no ffmpeg (ART-05 G1, T-1).

Usage: python Tools/Audio/process_clips.py [--out DIR] [--only out_file ...]
Default DIR is %TEMP%/art05_stage (outside Assets/). Writes <DIR>/Sfx/*.wav, <DIR>/Music/*.ogg and
<DIR>/process_report.csv (one row per file: lengths, levels before/after, gain, gain reduction).
"""
from pathlib import Path
import argparse
import csv
import io
import math
import os
import tempfile
import zipfile

import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parents[2]
RECIPE = ROOT / 'Tools/Audio/art05_recipe.csv'
DOWNLOADS = ROOT / 'Tools/Audio/Sources/ART-05'
JSURVIV = ROOT / 'Assets/Imported/JSurviv/Sound'
SR = 44100
CEILING_DB = -1.05            # 0.05 dB below the -1.0 dBFS rule, room for 16-bit rounding
MAX_REDUCTION_DB = 6.0        # default; a recipe row may allow more (sparse or decaying clips), never above 16
ACTIVE_FLOOR = 0.003          # G-7: L runs from the first to the last sample with |x| >= 0.003
PAD_S = 0.002
FADE_IN_S = 0.003
MUSIC_LIMIT_BYTES = 3_000_000

REPORT_FIELDS = ['out_file', 'sfx_id', 'duration_s', 'peak_dbfs', 'level_dbfs', 'head_2ms_dbfs', 'tail_2ms_dbfs',
                 'gain_db', 'reduction_db', 'trim_start_s', 'trim_end_s', 'source_peak_dbfs', 'source_level_dbfs',
                 'vorbis_quality', 'size_bytes']


def db(v):
    return 20.0 * math.log10(max(float(v), 1e-12))


def active_level(mono):
    """Mức hoạt động L (G-7): RMS between the first and the last sample with |x| >= 0.003, in dBFS."""
    idx = np.nonzero(np.abs(mono) >= ACTIVE_FLOOR)[0]
    if len(idx) == 0:
        return -120.0
    seg = mono[idx[0]:idx[-1] + 1]
    return db(np.sqrt(np.mean(seg * seg)))


def read_source(row):
    """Returns (samples [n, ch] float64, sample rate) of the row's untouched source."""
    kind, source, member = row['source_kind'], row['source'], row['zip_member']
    if kind == 'jsurviv':
        path = JSURVIV / source
    else:
        path = DOWNLOADS / source
    if member:
        with zipfile.ZipFile(path) as zf:
            name = next(n for n in zf.namelist() if n == member or n.endswith('/' + member))
            data = io.BytesIO(zf.read(name))
        x, sr = sf.read(data, always_2d=True, dtype='float64')
    else:
        x, sr = sf.read(str(path), always_2d=True, dtype='float64')
    return x, sr


def resample(x, sr_in, sr_out=SR, half_taps=32):
    """Windowed-sinc (Blackman) resampling of a mono signal; band-limited to the lower Nyquist."""
    if sr_in == sr_out:
        return x
    n_out = int(round(len(x) * sr_out / sr_in))
    cutoff = min(1.0, sr_out / sr_in) * 0.97
    out = np.empty(n_out)
    padded = np.concatenate([np.zeros(half_taps), x, np.zeros(half_taps + 1)])
    offsets = np.arange(-half_taps + 1, half_taps + 1)
    for start in range(0, n_out, 8192):
        t = np.arange(start, min(n_out, start + 8192)) * (sr_in / sr_out)
        base = np.floor(t).astype(np.int64)
        k = base[:, None] + offsets[None, :]
        d = t[:, None] - k
        window = 0.42 + 0.5 * np.cos(np.pi * d / half_taps) + 0.08 * np.cos(2 * np.pi * d / half_taps)
        window[np.abs(d) >= half_taps] = 0.0
        h = cutoff * np.sinc(cutoff * d) * window
        out[start:start + len(t)] = np.sum(padded[k + half_taps] * h, axis=1)
    return out


def moving_min(x, width):
    """Minimum over [i, i + width) for every i (van Herk/Gil-Werman, O(n) memory); x is padded by the caller."""
    n = len(x) - width + 1
    blocks = -(-len(x) // width)
    padded = np.concatenate([x, np.full(blocks * width - len(x), np.inf)]).reshape(blocks, width)
    prefix = np.minimum.accumulate(padded, axis=1).ravel()
    suffix = np.minimum.accumulate(padded[:, ::-1], axis=1)[:, ::-1].ravel()
    return np.minimum(suffix[:n], prefix[width - 1:width - 1 + n])


def limit(y, ceiling):
    """Look-ahead peak limiter on [n, ch]: per-sample gain <= the need of any peak within 1 ms, release 50 ms."""
    need = np.minimum(1.0, ceiling / np.maximum(np.max(np.abs(y), axis=1), 1e-12))
    if need.min() >= 1.0:
        return y, 0.0
    attack = int(0.001 * SR)
    padded = np.concatenate([np.ones(attack), need, np.ones(attack)])
    held = moving_min(padded, 2 * attack + 1)
    coeff = 1.0 - math.exp(-1.0 / (0.050 * SR))
    gain = np.empty_like(held)
    g = 1.0
    for i, h in enumerate(held):
        g = min(h, g + (1.0 - g) * coeff)
        gain[i] = g
    half = attack // 2
    smooth = np.convolve(np.concatenate([np.full(half, gain[0]), gain, np.full(half, gain[-1])]),
                         np.ones(2 * half + 1) / (2 * half + 1), mode='valid')
    gain = np.minimum(smooth, gain)  # an average of mins stays under every peak's need
    out = y * gain[:, None]
    out = np.clip(out, -ceiling, ceiling)
    return out, -db(gain.min())


def level_to(y, target_db, max_reduction_db=MAX_REDUCTION_DB):
    """Gain + limiter so L reaches target_db; never more than max_reduction_db of limiting. Returns (y, gain, gr)."""
    ceiling = 10 ** (CEILING_DB / 20)
    peak = np.max(np.abs(y))
    gain = 0.0
    for _ in range(4):  # plain gain first; L's -50 dBFS bounds move with the gain, so refine a few times
        gain += target_db - active_level((y * 10 ** (gain / 20)).mean(axis=1))
    if db(peak) + gain <= CEILING_DB:
        return y * 10 ** (gain / 20), gain, 0.0
    cap = CEILING_DB - db(peak) + max_reduction_db
    def render(gain_db):
        out, gr = limit(y * 10 ** (gain_db / 20), ceiling)
        return out, gr, active_level(out.mean(axis=1))
    out, gr, level = render(cap)
    if level <= target_db:
        return out, cap, gr
    lo, hi = cap - 60.0, cap
    for _ in range(40):
        mid = (lo + hi) / 2
        out, gr, level = render(mid)
        if abs(level - target_db) < 0.01:
            break
        if level < target_db:
            lo = mid
        else:
            hi = mid
    return out, mid, gr


def cosine(n, rising):
    ramp = 0.5 - 0.5 * np.cos(np.linspace(0.0, np.pi, n, endpoint=False) + (np.pi / n))
    return ramp if rising else ramp[::-1]


def render_sfx(row):
    x, sr = read_source(row)
    mono = x.mean(axis=1)
    source_peak, source_level = db(np.max(np.abs(mono))), active_level(mono)
    a = np.abs(mono)
    peak = a.max()
    if row['trim_start_s']:
        start = int(float(row['trim_start_s']) * sr)
    else:  # onset: first sample within 40 dB of the peak, 1 ms earlier
        start = max(0, int(np.nonzero(a >= peak * 10 ** (-40 / 20))[0][0]) - int(0.001 * sr))
    end = len(mono) if row['keep_tail'] == '1' else int(np.nonzero(a >= peak * 10 ** (-50 / 20))[0][-1]) + 1
    end = min(end, start + int(float(row['max_s']) * sr))
    seg = resample(mono[start:end], sr)
    fade_in = int(FADE_IN_S * SR)
    fade_out = min(len(seg) // 2, int(float(row['fade_out_ms']) / 1000 * SR))
    seg[:fade_in] *= cosine(fade_in, True)
    seg[len(seg) - fade_out:] *= cosine(fade_out, False)
    pad = np.zeros(int(PAD_S * SR))
    y = np.concatenate([pad, seg, pad])[:, None]
    y, gain, gr = level_to(y, float(row['target_l_dbfs']), float(row['max_reduction_db'] or MAX_REDUCTION_DB))
    return y, sr, dict(gain_db=gain, reduction_db=gr, trim_start_s=start / sr, trim_end_s=end / sr,
                       source_peak_dbfs=source_peak, source_level_dbfs=source_level)


def render_music(row):
    x, sr = read_source(row)
    if sr != SR:
        raise ValueError(f'{row["out_file"]}: music must already be {SR} Hz, got {sr}')
    source_peak, source_level = db(np.max(np.abs(x))), active_level(x.mean(axis=1))
    y, gain, gr = level_to(x, float(row['target_l_dbfs']))
    return y, sr, dict(gain_db=gain, reduction_db=gr, trim_start_s=0.0, trim_end_s=len(x) / sr,
                       source_peak_dbfs=source_peak, source_level_dbfs=source_level)


def write_vorbis(path, y):
    """Highest Vorbis quality (0.7 down in 0.1 steps) whose file stays <= 3.0 MB."""
    for quality in (0.7, 0.6, 0.5, 0.4, 0.3):
        # Block writes: one large Vorbis write overflows libsndfile's stack on Windows.
        with sf.SoundFile(str(path), 'w', SR, y.shape[1], format='OGG', subtype='VORBIS',
                          compression_level=1.0 - quality) as f:
            for start in range(0, len(y), 4096):
                f.write(y[start:start + 4096])
        if path.stat().st_size <= MUSIC_LIMIT_BYTES:
            return quality
    raise ValueError(f'{path.name}: over 3.0 MB even at quality 0.3')


def edges_db(mono):
    n = int(PAD_S * SR)
    return db(np.max(np.abs(mono[:n]))), db(np.max(np.abs(mono[-n:])))


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('--out', default=os.path.join(tempfile.gettempdir(), 'art05_stage'))
    parser.add_argument('--only', nargs='*', help='render only these out_file values')
    args = parser.parse_args()
    out_dir = Path(args.out)
    with open(RECIPE, encoding='utf-8', newline='') as f:
        rows = list(csv.DictReader(f))
    report = []
    for row in rows:
        if args.only and row['out_file'] not in args.only:
            continue
        target = out_dir / row['out_file']
        target.parent.mkdir(parents=True, exist_ok=True)
        music = row['sfx_id'] == 'music'
        y, _, info = (render_music if music else render_sfx)(row)
        quality = ''
        if music:
            quality = write_vorbis(target, y)
            y, _ = sf.read(str(target), always_2d=True, dtype='float64')  # measure what ships
        else:
            sf.write(str(target), y, SR, subtype='PCM_16')
            y, _ = sf.read(str(target), always_2d=True, dtype='float64')
        mono = y.mean(axis=1)
        head, tail = edges_db(mono)
        info.update(out_file=row['out_file'], sfx_id=row['sfx_id'], duration_s=len(mono) / SR,
                    peak_dbfs=db(np.max(np.abs(y))), level_dbfs=active_level(mono), head_2ms_dbfs=head,
                    tail_2ms_dbfs=tail, vorbis_quality=quality, size_bytes=target.stat().st_size)
        report.append(info)
        print(f"{row['out_file']:28} {info['duration_s']:6.3f}s L {info['level_dbfs']:6.1f} peak {info['peak_dbfs']:5.1f} "
              f"gain {info['gain_db']:+5.1f} GR {info['reduction_db']:4.1f} head {head:6.1f} tail {tail:6.1f}")
    path = out_dir / 'process_report.csv'
    with open(path, 'w', encoding='utf-8', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=REPORT_FIELDS, lineterminator='\n')
        writer.writeheader()
        for info in report:
            writer.writerow({k: (f'{v:.4f}' if isinstance(v, float) else v) for k, v in info.items()})
    print('report:', path)


if __name__ == '__main__':
    main()
