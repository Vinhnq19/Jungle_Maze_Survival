"""Unit tests for the ART-05 audio pipeline (process_clips.py, measure_clips.py) on synthetic signals.

Usage (needs numpy and soundfile): python Tools/Audio/test_art05_audio.py
"""
from pathlib import Path
import math
import os
import sys
import tempfile
import unittest

import numpy as np
import soundfile as sf

sys.path.insert(0, str(Path(__file__).resolve().parent))
import measure_clips  # noqa: E402
import process_clips as pc  # noqa: E402

SR = pc.SR


def burst(seconds=0.3, freq=440.0, amp=0.9, decay=8.0):
    t = np.arange(int(seconds * SR)) / SR
    return amp * np.sin(2 * np.pi * freq * t) * np.exp(-decay * t)


def frame(x, fade_out_ms=20):
    """The SFX path without the source reader: fades, 2 ms pads."""
    seg = x.copy()
    fade_in = int(pc.FADE_IN_S * SR)
    fade_out = int(fade_out_ms / 1000 * SR)
    seg[:fade_in] *= pc.cosine(fade_in, True)
    seg[len(seg) - fade_out:] *= pc.cosine(fade_out, False)
    pad = np.zeros(int(pc.PAD_S * SR))
    return np.concatenate([pad, seg, pad])[:, None]


class PipelineTests(unittest.TestCase):
    def test_edges_are_silent_after_fades(self):
        y = frame(burst())
        head, tail = pc.edges_db(y[:, 0])
        self.assertLessEqual(head, -40.0)
        self.assertLessEqual(tail, -40.0)

    def test_limiter_never_passes_the_ceiling(self):
        y = frame(burst(amp=1.0, decay=1.0))
        out, gain, reduction = pc.level_to(y, -12.0)
        self.assertLessEqual(np.max(np.abs(out)), 10 ** (pc.CEILING_DB / 20) + 1e-9)
        self.assertLessEqual(reduction, pc.MAX_REDUCTION_DB + 1e-6)

    def test_gain_reaches_the_target_level(self):
        y = frame(burst(amp=0.2))
        out, _, reduction = pc.level_to(y, -18.0)
        self.assertAlmostEqual(pc.active_level(out[:, 0]), -18.0, delta=0.1)

    def test_reduction_is_capped(self):
        y = frame(burst(amp=1.0, decay=40.0))  # crest far too high for -10 dBFS
        out, _, reduction = pc.level_to(y, -10.0)
        self.assertLessEqual(reduction, 6.0 + 1e-6)
        self.assertLess(pc.active_level(out[:, 0]), -10.0)

    def test_same_input_same_bytes(self):
        with tempfile.TemporaryDirectory() as tmp:
            paths = []
            for name in ('a.wav', 'b.wav'):
                out, _, _ = pc.level_to(frame(burst(amp=0.3)), -18.0)
                path = os.path.join(tmp, name)
                sf.write(path, out, SR, subtype='PCM_16')
                paths.append(path)
            self.assertEqual(Path(paths[0]).read_bytes(), Path(paths[1]).read_bytes())

    def test_resample_keeps_duration(self):
        x = burst(seconds=1.0)[:24000]
        y = pc.resample(x, 24000, SR)
        self.assertLessEqual(abs(len(y) - round(len(x) * SR / 24000)), 1)
        self.assertAlmostEqual(len(y) / SR, len(x) / 24000, delta=1.0 / SR)

    def test_measure_matches_numpy(self):
        with tempfile.TemporaryDirectory() as tmp:
            out, _, _ = pc.level_to(frame(burst(amp=0.3)), -18.0)
            path = os.path.join(tmp, 'm.wav')
            sf.write(path, out, SR, subtype='PCM_16')
            m = measure_clips.measure(path)
            back, _ = sf.read(path)
            self.assertAlmostEqual(m['level_dbfs'], pc.active_level(back), delta=0.05)
            self.assertAlmostEqual(m['peak_dbfs'], pc.db(np.max(np.abs(back))), delta=0.05)
            self.assertLessEqual(m['head_2ms_dbfs'], -40.0)
            self.assertEqual(m['full_scale_run'], 0)


if __name__ == '__main__':
    unittest.main()
