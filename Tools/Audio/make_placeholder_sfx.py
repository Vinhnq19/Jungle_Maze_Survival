"""Generate placeholder SFX (AUD-01) as mono 16-bit 44.1 kHz WAV using only the standard library.

Every SFX without a usable JSurviv clip gets its own short, recognisable sound. File names carry the
base frequency (``<name>_<hz>hz.wav``) so tests can check that no two placeholders share a pitch.
All sounds are temporary: ART-05 replaces them through the SFX catalog without code changes.

Usage: python -I Tools/Audio/make_placeholder_sfx.py [--out DIR]
"""
from pathlib import Path
import argparse
import math
import random
import struct
import wave

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_OUT = ROOT / 'Assets/_JungleMaze/Art/Audio/Placeholder/Beeps'
RATE = 44100
PEAK = 0.7          # about -3 dBFS
EDGE_FADE = 0.004   # seconds of fade at both ends, avoids clicks
BASE_HZ = 330.0     # step k uses BASE_HZ * 2^(k/4)


def step_hz(k):
    return BASE_HZ * 2.0 ** (k / 4.0)


def silence(seconds):
    return [0.0] * int(seconds * RATE)


def mix(*tracks):
    out = [0.0] * max(len(t) for t in tracks)
    for track in tracks:
        for i, v in enumerate(track):
            out[i] += v
    return out


def offset(track, seconds):
    return silence(seconds) + track


def gain(track, g):
    return [v * g for v in track]


def env_decay(track, attack, decay):
    """Linear attack then exponential decay with time constant ``decay`` seconds."""
    out = []
    for i, v in enumerate(track):
        t = i / RATE
        a = t / attack if attack > 0 and t < attack else 1.0
        out.append(v * a * math.exp(-max(0.0, t - attack) / decay))
    return out


def env_swell(track, rise):
    """Rise as a half-sine over the whole length, peak at ``rise`` (0..1)."""
    n = len(track)
    out = []
    for i, v in enumerate(track):
        x = i / max(1, n - 1)
        shape = math.sin(math.pi * 0.5 * x / rise) if x < rise else math.cos(math.pi * 0.5 * (x - rise) / (1 - rise))
        out.append(v * max(0.0, shape))
    return out


def osc(seconds, f0, f1=None, shape='sine', vibrato_hz=0.0, vibrato_depth=0.0):
    """Oscillator with exponential pitch glide from f0 to f1."""
    f1 = f0 if f1 is None else f1
    n = int(seconds * RATE)
    phase = 0.0
    out = []
    for i in range(n):
        x = i / max(1, n - 1)
        f = f0 * (f1 / f0) ** x
        if vibrato_hz:
            f *= 1.0 + vibrato_depth * math.sin(2 * math.pi * vibrato_hz * i / RATE)
        phase += f / RATE
        p = phase % 1.0
        if shape == 'sine':
            v = math.sin(2 * math.pi * p)
        elif shape == 'square':
            v = 1.0 if p < 0.5 else -1.0
        elif shape == 'triangle':
            v = 4.0 * abs(p - 0.5) - 1.0
        else:  # saw
            v = 2.0 * p - 1.0
        out.append(v)
    return out


def noise(seconds, seed):
    rng = random.Random(seed)
    return [rng.uniform(-1.0, 1.0) for _ in range(int(seconds * RATE))]


def lowpass(track, cutoff_hz):
    alpha = 1.0 - math.exp(-2 * math.pi * cutoff_hz / RATE)
    out, y = [], 0.0
    for v in track:
        y += alpha * (v - y)
        out.append(y)
    return out


def highpass(track, cutoff_hz):
    low = lowpass(track, cutoff_hz)
    return [v - l for v, l in zip(track, low)]


def bandpass(track, low_hz, high_hz):
    return lowpass(highpass(track, low_hz), high_hz)


def finish(track):
    """Fade both ends (short at the start to keep transients sharp), then normalise to PEAK."""
    head = max(1, int(EDGE_FADE * RATE / 8))
    tail = max(1, int(EDGE_FADE * RATE))
    n = len(track)
    faded = [v * min(1.0, i / head, (n - 1 - i) / tail) for i, v in enumerate(track)]
    peak = max(abs(v) for v in faded) or 1.0
    return [v / peak * PEAK for v in faded]


# ---- one recipe per SfxId that has no JSurviv clip ------------------------------------------

def footstep_grass(f):
    thump = env_decay(osc(0.09, f, f * 0.7), 0.002, 0.02)
    rustle = env_decay(lowpass(noise(0.09, 1), 1400), 0.003, 0.025)
    return mix(gain(thump, 0.5), rustle)


def footstep_swamp(f):
    squelch = env_decay(osc(0.18, f, f * 0.55, vibrato_hz=28, vibrato_depth=0.06), 0.01, 0.06)
    mud = env_decay(lowpass(noise(0.18, 2), 700), 0.01, 0.07)
    return mix(squelch, gain(mud, 0.8))


def bush_enter(f):
    leaves = []
    for k, start in enumerate((0.0, 0.07, 0.15)):
        burst = env_decay(highpass(noise(0.1, 10 + k), 2500), 0.004, 0.03)
        leaves.append(offset(burst, start))
    tone = env_decay(osc(0.26, f), 0.01, 0.08)
    return mix(*leaves, gain(tone, 0.15))


def shoot_crossbow(f):
    twang = env_decay(osc(0.2, f, f * 0.75, 'triangle', vibrato_hz=35, vibrato_depth=0.02), 0.001, 0.06)
    snap = env_decay(highpass(noise(0.03, 3), 3000), 0.0005, 0.006)
    return mix(twang, gain(snap, 0.6))


def throw(f):
    whoosh = env_swell(bandpass(noise(0.26, 4), 500, 3000), 0.6)
    rise = env_swell(osc(0.26, f, f * 1.5), 0.6)
    return mix(whoosh, gain(rise, 0.25))


def reload(f):
    click1 = env_decay(osc(0.04, f, shape='square'), 0.0005, 0.008)
    click2 = env_decay(osc(0.05, f * 1.5, shape='square'), 0.0005, 0.012)
    rattle = env_decay(bandpass(noise(0.05, 5), 1500, 6000), 0.0005, 0.01)
    return mix(click1, gain(rattle, 0.5), offset(click2, 0.16), offset(gain(rattle, 0.5), 0.16), silence(0.24))


def empty_click(f):
    return mix(env_decay(osc(0.045, f, shape='square'), 0.0003, 0.006), silence(0.06))


def trap_triggered(f):
    snap = env_decay(osc(0.22, f, f * 0.5, 'square'), 0.001, 0.05)
    crack = env_decay(noise(0.06, 6), 0.0005, 0.012)
    return mix(gain(snap, 0.6), crack)


def armor_absorb(f):
    partials = [env_decay(osc(0.3, f * r), 0.001, d) for r, d in ((1.0, 0.09), (2.76, 0.05), (5.4, 0.03))]
    hit = env_decay(lowpass(noise(0.03, 7), 2000), 0.0005, 0.008)
    return mix(partials[0], gain(partials[1], 0.6), gain(partials[2], 0.35), gain(hit, 0.5))


def eliminated(f):
    fall = env_decay(osc(0.75, f, f / 4, 'triangle'), 0.01, 0.35)
    sub = env_decay(osc(0.75, f / 4, f / 8), 0.02, 0.3)
    return mix(fall, gain(sub, 0.5))


def use_cancel(f):
    a = env_decay(osc(0.08, f, shape='triangle'), 0.002, 0.03)
    b = env_decay(osc(0.1, f / 2, shape='triangle'), 0.002, 0.04)
    return gain(mix(a, offset(b, 0.08)), 0.6)


def hit_marker(f):
    """Short, sharp tick: two high partials and a 1 ms transient, about 60 ms long."""
    body = env_decay(osc(0.06, f), 0.0005, 0.012)
    edge = env_decay(osc(0.06, f * 1.5), 0.0005, 0.007)
    tick = env_decay(highpass(noise(0.01, 8), 4000), 0.0002, 0.0015)
    return mix(body, gain(edge, 0.6), gain(tick, 0.5))


def zone_bell(f):
    """Struck bell: hum, fundamental and inharmonic partials with different decay rates."""
    length = 0.7
    parts = ((0.5, 0.35, 0.40), (1.0, 1.0, 0.30), (1.5, 0.55, 0.20), (2.0, 0.45, 0.14), (2.76, 0.3, 0.08), (4.07, 0.2, 0.05))
    tones = [gain(env_decay(osc(length, f * r), 0.001, d), a) for r, a, d in parts]
    strike = gain(env_decay(highpass(noise(0.02, 9), 3000), 0.0002, 0.003), 0.3)
    return mix(*tones, strike)


def dragon_roar(f):
    """Low growl: saw sweep up then down with 30 Hz amplitude growl and filtered breath noise, 2 s."""
    length = 2.0
    up = osc(length * 0.4, f, f * 3.0, 'saw')
    down = osc(length * 0.6, f * 3.0, f * 1.3, 'saw')
    voice = lowpass(up + down, 900)
    growl = [v * (0.65 + 0.35 * math.sin(2 * math.pi * 30 * i / RATE)) for i, v in enumerate(voice)]
    breath = lowpass(noise(length, 11), 1200)
    return env_swell(mix(growl, gain(breath, 0.35)), 0.35)


# (file stem, SfxId, frequency in Hz, recipe)
SOUNDS = [
    ('footstep_grass', 'FootstepGrass', step_hz(0), footstep_grass),
    ('footstep_swamp', 'FootstepSwamp', step_hz(1), footstep_swamp),
    ('bush_enter', 'BushEnter', step_hz(2), bush_enter),
    ('shoot_crossbow', 'ShootCrossbow', step_hz(3), shoot_crossbow),
    ('throw', 'Throw', step_hz(4), throw),
    ('reload', 'Reload', step_hz(5), reload),
    ('empty_click', 'EmptyClick', step_hz(6), empty_click),
    ('trap_triggered', 'TrapTriggered', step_hz(7), trap_triggered),
    ('armor_absorb', 'ArmorAbsorb', step_hz(8), armor_absorb),
    ('eliminated', 'Eliminated', step_hz(9), eliminated),
    ('use_cancel', 'UseCancel', step_hz(10), use_cancel),
    ('hit_marker', 'HitMarker', step_hz(11), hit_marker),
    ('zone_bell', 'ZoneBell3', 880.0, zone_bell),
    ('dragon_roar', 'DragonRoar', 70.0, dragon_roar),
]


def write_wav(path, samples):
    with wave.open(str(path), 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b''.join(struct.pack('<h', int(round(v * 32767))) for v in samples))


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('--out', type=Path, default=DEFAULT_OUT)
    args = parser.parse_args()
    args.out.mkdir(parents=True, exist_ok=True)
    for stem, sfx_id, hz, recipe in SOUNDS:
        samples = finish(recipe(hz))
        path = args.out / f'{stem}_{round(hz)}hz.wav'
        write_wav(path, samples)
        print(f'{sfx_id:14} {path.relative_to(ROOT) if path.is_relative_to(ROOT) else path}  {len(samples) / RATE:.2f}s')


if __name__ == '__main__':
    main()
