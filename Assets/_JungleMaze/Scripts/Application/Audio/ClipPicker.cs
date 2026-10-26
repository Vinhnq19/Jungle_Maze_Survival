using System;
using JungleMaze.Domain.Audio;

namespace JungleMaze.App.Audio
{
    /// <summary>
    /// Picks a clip variant at random without playing the same variant twice in a row, and a pitch within ±jitter
    /// (G-5). Uses one pre-made <see cref="Random"/>, so picking does not allocate.
    /// </summary>
    public sealed class ClipPicker
    {
        private readonly Random _random;
        private readonly int[] _last = new int[SfxIds.TableSize];

        public ClipPicker(int seed = 12345)
        {
            _random = new Random(seed);
            for (var i = 0; i < _last.Length; i++) _last[i] = -1;
        }

        public int PickClip(SfxId id, int clipCount)
        {
            if (clipCount <= 1) return 0;
            var slot = SfxIds.IsValid(id) ? (int)id : 0;
            var last = _last[slot];
            var hasLast = last >= 0 && last < clipCount;
            var pick = _random.Next(hasLast ? clipCount - 1 : clipCount);
            if (hasLast && pick >= last) pick++;
            _last[slot] = pick;
            return pick;
        }

        public float PickPitch(float jitter) => jitter <= 0f ? 1f : 1f + ((float)_random.NextDouble() * 2f - 1f) * jitter;
    }
}
