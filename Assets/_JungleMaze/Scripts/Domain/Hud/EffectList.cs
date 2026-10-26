using System;

namespace JungleMaze.Domain.Hud
{
    /// <summary>
    /// Active status effects of the local player in a fixed array (no allocation on apply, tick or read).
    /// One entry per <see cref="EffectKind"/>; order is the order effects were added. Timed effects count down in
    /// <see cref="Tick"/> and are removed when they reach zero.
    /// </summary>
    public sealed class EffectList
    {
        public const int Capacity = 16;

        private readonly ActiveEffect[] _effects = new ActiveEffect[Capacity];

        public int Count { get; private set; }

        /// <summary>Effect at <paramref name="index"/> (0 = oldest); default when out of range.</summary>
        public ActiveEffect GetAt(int index) => index >= 0 && index < Count ? _effects[index] : default;

        public bool TryGet(EffectKind kind, out ActiveEffect effect)
        {
            var index = IndexOf(kind);
            effect = index >= 0 ? _effects[index] : default;
            return index >= 0;
        }

        /// <summary>
        /// Adds or refreshes <paramref name="kind"/>. Duration 0 = untimed. Invalid input (None kind, NaN, negative
        /// numbers, a timed effect with nothing remaining, full list) is ignored and returns false.
        /// Remaining is clamped to the duration.
        /// </summary>
        public bool Apply(EffectKind kind, float durationSeconds, float remainingSeconds, out EffectChangeType type)
        {
            type = EffectChangeType.Added;
            if (kind == EffectKind.None || float.IsNaN(durationSeconds) || float.IsNaN(remainingSeconds)
                || durationSeconds < 0f || remainingSeconds < 0f) return false;
            if (durationSeconds > 0f)
            {
                if (remainingSeconds <= 0f) return false;
                if (remainingSeconds > durationSeconds) remainingSeconds = durationSeconds;
            }
            else remainingSeconds = 0f;
            var effect = new ActiveEffect(kind, durationSeconds, remainingSeconds);
            var index = IndexOf(kind);
            if (index >= 0)
            {
                _effects[index] = effect;
                type = EffectChangeType.Refreshed;
                return true;
            }
            if (Count >= Capacity) return false;
            _effects[Count++] = effect;
            return true;
        }

        /// <summary>Removes <paramref name="kind"/>; false if it was not active.</summary>
        public bool Remove(EffectKind kind)
        {
            var index = IndexOf(kind);
            if (index < 0) return false;
            RemoveAt(index);
            return true;
        }

        /// <summary>Removes every effect; returns how many were removed.</summary>
        public int Clear()
        {
            var removed = Count;
            for (var i = 0; i < Count; i++) _effects[i] = default;
            Count = 0;
            return removed;
        }

        /// <summary>
        /// Counts timed effects down and removes the expired ones, calling <paramref name="onExpired"/> for each
        /// (pass a cached delegate to stay allocation-free).
        /// </summary>
        public void Tick(float deltaSeconds, Action<EffectKind> onExpired)
        {
            if (!(deltaSeconds > 0f)) return;
            for (var i = 0; i < Count; i++)
            {
                var effect = _effects[i];
                if (!effect.IsTimed) continue;
                var remaining = effect.RemainingSeconds - deltaSeconds;
                if (remaining > 0f)
                {
                    _effects[i] = new ActiveEffect(effect.Kind, effect.DurationSeconds, remaining);
                    continue;
                }
                RemoveAt(i);
                i--;
                onExpired?.Invoke(effect.Kind);
            }
        }

        private int IndexOf(EffectKind kind)
        {
            for (var i = 0; i < Count; i++) if (_effects[i].Kind == kind) return i;
            return -1;
        }

        private void RemoveAt(int index)
        {
            for (var i = index; i < Count - 1; i++) _effects[i] = _effects[i + 1];
            _effects[--Count] = default;
        }
    }
}
