using JungleMaze.Domain.Audio;

namespace JungleMaze.App.Audio
{
    /// <summary>
    /// Bookkeeping of the fixed SFX voice pool (G-2). A request takes a free voice; when none is free it evicts the
    /// lowest-priority voice that is strictly below the request (oldest first on ties), otherwise it is dropped.
    /// So a Critical voice is never cut by anything lower.
    /// </summary>
    public sealed class VoiceAllocator
    {
        private readonly bool[] _active;
        private readonly SfxId[] _ids;
        private readonly SfxPriority[] _priorities;
        private readonly AudioBus[] _buses;
        private readonly int[] _sourceKeys;
        private readonly long[] _order;
        private long _sequence;

        public VoiceAllocator(int voiceCount)
        {
            var n = voiceCount < 1 ? 1 : voiceCount;
            _active = new bool[n];
            _ids = new SfxId[n];
            _priorities = new SfxPriority[n];
            _buses = new AudioBus[n];
            _sourceKeys = new int[n];
            _order = new long[n];
        }

        public int VoiceCount => _active.Length;

        public int ActiveCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _active.Length; i++) if (_active[i]) count++;
                return count;
            }
        }

        public bool IsActive(int voice) => _active[voice];
        public SfxId IdAt(int voice) => _ids[voice];
        public SfxPriority PriorityAt(int voice) => _priorities[voice];
        public int SourceAt(int voice) => _sourceKeys[voice];

        public int CountActive(SfxId id)
        {
            var count = 0;
            for (var i = 0; i < _active.Length; i++) if (_active[i] && _ids[i] == id) count++;
            return count;
        }

        public bool AnyActiveOn(AudioBus bus)
        {
            for (var i = 0; i < _active.Length; i++) if (_active[i] && _buses[i] == bus) return true;
            return false;
        }

        /// <summary>
        /// Picks a voice for a request of <paramref name="priority"/>. Returns -1 when the request must be dropped.
        /// <paramref name="evicted"/> is true when the returned voice was still playing and must be stopped first.
        /// </summary>
        public int Acquire(SfxPriority priority, out bool evicted)
        {
            evicted = false;
            for (var i = 0; i < _active.Length; i++) if (!_active[i]) return i;
            var victim = -1;
            for (var i = 0; i < _active.Length; i++)
            {
                if (_priorities[i] >= priority) continue;
                if (victim < 0 || _priorities[i] < _priorities[victim]
                               || (_priorities[i] == _priorities[victim] && _order[i] < _order[victim])) victim = i;
            }
            evicted = victim >= 0;
            return victim;
        }

        public void Assign(int voice, SfxId id, SfxPriority priority, AudioBus bus, int sourceKey = 0)
        {
            _sourceKeys[voice] = sourceKey;
            _active[voice] = true;
            _ids[voice] = id;
            _priorities[voice] = priority;
            _buses[voice] = bus;
            _order[voice] = ++_sequence;
        }

        public void Release(int voice) => _active[voice] = false;
    }
}
