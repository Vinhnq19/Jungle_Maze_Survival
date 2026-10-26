using JungleMaze.Domain.Audio;

namespace JungleMaze.App.Audio
{
    /// <summary>
    /// Per-id cooldown counted per source (G-5). Each id remembers its last <see cref="SourcesPerId"/> sources in
    /// fixed arrays; when full, the least recently played source is forgotten. No allocation after construction.
    /// </summary>
    public sealed class SfxGate
    {
        public const int SourcesPerId = 64;

        private readonly int[] _sources = new int[SfxIds.TableSize * SourcesPerId];
        private readonly float[] _times = new float[SfxIds.TableSize * SourcesPerId];
        private readonly bool[] _used = new bool[SfxIds.TableSize * SourcesPerId];

        /// <summary>True when <paramref name="id"/> from <paramref name="sourceKey"/> played less than <paramref name="cooldown"/> ago.</summary>
        public bool IsCoolingDown(SfxId id, int sourceKey, float now, float cooldown)
        {
            if (cooldown <= 0f || !SfxIds.IsValid(id)) return false;
            var slot = Find(id, sourceKey);
            return slot >= 0 && now - _times[slot] < cooldown;
        }

        /// <summary>Records a play of <paramref name="id"/> from <paramref name="sourceKey"/> at <paramref name="now"/>.</summary>
        public void Mark(SfxId id, int sourceKey, float now)
        {
            if (!SfxIds.IsValid(id)) return;
            var slot = Find(id, sourceKey);
            if (slot < 0)
            {
                var start = (int)id * SourcesPerId;
                slot = start;
                for (var i = start; i < start + SourcesPerId; i++)
                {
                    if (!_used[i]) { slot = i; break; }
                    if (_times[i] < _times[slot]) slot = i;
                }
                _used[slot] = true;
                _sources[slot] = sourceKey;
            }
            _times[slot] = now;
        }

        public void Clear() => System.Array.Clear(_used, 0, _used.Length);

        private int Find(SfxId id, int sourceKey)
        {
            var start = (int)id * SourcesPerId;
            for (var i = start; i < start + SourcesPerId; i++)
                if (_used[i] && _sources[i] == sourceKey) return i;
            return -1;
        }
    }
}
