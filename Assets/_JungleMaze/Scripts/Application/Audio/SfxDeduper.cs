namespace JungleMaze.App.Audio
{
    /// <summary>
    /// Remembers recent event keys so the same event arriving several times (server resend, repeated snapshot,
    /// reconnect) plays once (G-6: keys kept 2 s, at most 128; the oldest is dropped when full). Key 0 is never a duplicate.
    /// </summary>
    public sealed class SfxDeduper
    {
        public const float DefaultMemorySeconds = 2f;
        public const int DefaultCapacity = 128;

        private readonly ulong[] _keys;
        private readonly float[] _times;
        private readonly float _memory;
        private int _next, _count;

        public SfxDeduper(float memorySeconds = DefaultMemorySeconds, int capacity = DefaultCapacity)
        {
            _memory = memorySeconds;
            _keys = new ulong[capacity < 1 ? 1 : capacity];
            _times = new float[_keys.Length];
        }

        public int Capacity => _keys.Length;

        /// <summary>True if <paramref name="key"/> was seen within the memory window; otherwise remembers it and returns false.</summary>
        public bool IsDuplicate(ulong key, float now)
        {
            if (key == 0) return false;
            for (var i = 0; i < _count; i++)
                if (_keys[i] == key && now - _times[i] < _memory) { return true; }
            _keys[_next] = key;
            _times[_next] = now;
            _next = (_next + 1) % _keys.Length;
            if (_count < _keys.Length) _count++;
            return false;
        }

        public void Clear() { _next = 0; _count = 0; }

        /// <summary>Builds a non-zero key from an event kind and up to three payload values.</summary>
        public static ulong Key(byte kind, uint a, uint b = 0, uint c = 0)
        {
            unchecked
            {
                var h = 1469598103934665603UL;
                h = (h ^ kind) * 1099511628211UL;
                h = (h ^ a) * 1099511628211UL;
                h = (h ^ b) * 1099511628211UL;
                h = (h ^ c) * 1099511628211UL;
                return h == 0 ? 1UL : h;
            }
        }
    }
}
