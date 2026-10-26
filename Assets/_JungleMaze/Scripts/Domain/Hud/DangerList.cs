using System;

namespace JungleMaze.Domain.Hud
{
    /// <summary>Fixed-capacity warning storage; ticking allocates no memory.</summary>
    public sealed class DangerList
    {
        public const int Capacity = 16;
        private readonly DangerWarning[] _items = new DangerWarning[Capacity];
        public int Count { get; private set; }
        public DangerWarning GetAt(int index) => index >= 0 && index < Count ? _items[index] : default;
        public bool TryGet(int id, out DangerWarning warning) { var i = IndexOf(id); warning = i < 0 ? default : _items[i]; return i >= 0; }
        public bool Apply(in DangerWarning warning, out DangerChangeType type)
        {
            type = DangerChangeType.Added;
            if (!Finite(warning.TotalSeconds) || !Finite(warning.RemainingSeconds) || warning.TotalSeconds < 0f || warning.RemainingSeconds < 0f || (warning.IsTimed && warning.RemainingSeconds <= 0f)) return false;
            var index = IndexOf(warning.Id);
            if (index >= 0) { _items[index] = warning; type = DangerChangeType.Refreshed; return true; }
            if (Count == Capacity) return false;
            _items[Count++] = warning;
            return true;
        }
        public bool Remove(int id) { var i = IndexOf(id); if (i < 0) return false; RemoveAt(i); return true; }
        public void Clear() { Array.Clear(_items, 0, Count); Count = 0; }
        public void Tick(float dt, Action<int> expired)
        {
            if (!(dt > 0f) || !Finite(dt)) return;
            for (var i = 0; i < Count; i++)
            {
                var w = _items[i]; if (!w.IsTimed) continue;
                var left = w.RemainingSeconds - dt;
                if (left > 0f) { _items[i] = new DangerWarning(w.Id, w.Kind, w.TotalSeconds, left, w.HasSource, w.SourceX, w.SourceY); continue; }
                RemoveAt(i--); expired?.Invoke(w.Id);
            }
        }
        private int IndexOf(int id) { for (var i = 0; i < Count; i++) if (_items[i].Id == id) return i; return -1; }
        private void RemoveAt(int i) { for (; i < Count - 1; i++) _items[i] = _items[i + 1]; _items[--Count] = default; }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
