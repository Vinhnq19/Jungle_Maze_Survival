namespace JungleMaze.App.Hud
{
    /// <summary>
    /// A 0..1 bar value that eases to a new target in a fixed duration, independent of frame rate. A new target
    /// always starts from the value currently shown (never jumps back), and the ease-out curve is monotonic, so
    /// a bar that only receives decreasing targets never increases on any frame.
    /// </summary>
    public sealed class SmoothedBar
    {
        private float _from, _to, _elapsed;

        public SmoothedBar(float durationSeconds) => DurationSeconds = durationSeconds;

        public float DurationSeconds { get; set; }
        /// <summary>Value to draw this frame.</summary>
        public float Value { get; private set; }
        public float Target => _to;
        public bool IsAnimating => Value != _to;

        /// <summary>Jumps straight to <paramref name="value"/> (first draw).</summary>
        public void Snap(float value)
        {
            value = Clamp01(value);
            _from = _to = Value = value;
            _elapsed = 0f;
        }

        /// <summary>Starts easing from the shown value to <paramref name="target"/> (clamped to 0..1, NaN = 0).</summary>
        public void SetTarget(float target)
        {
            target = Clamp01(target);
            if (target == _to) return;
            _from = Value;
            _to = target;
            _elapsed = 0f;
            if (DurationSeconds <= 0f) Value = _to;
        }

        /// <summary>Advances time; returns true if <see cref="Value"/> changed.</summary>
        public bool Tick(float deltaSeconds)
        {
            if (Value == _to) return false;
            if (deltaSeconds > 0f) _elapsed += deltaSeconds;
            var previous = Value;
            if (DurationSeconds <= 0f || _elapsed >= DurationSeconds) Value = _to;
            else
            {
                var t = _elapsed / DurationSeconds;
                var eased = 1f - (1f - t) * (1f - t); // ease-out quad, monotonic on [0, 1]
                Value = _from + (_to - _from) * eased;
            }
            return Value != previous;
        }

        /// <summary>Clamps to 0..1 and maps NaN to 0.</summary>
        public static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value < 0f) return 0f;
            return value > 1f ? 1f : value;
        }
    }
}
