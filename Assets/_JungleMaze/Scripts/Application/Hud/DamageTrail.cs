namespace JungleMaze.App.Hud
{
    /// <summary>
    /// "Recently lost HP" layer behind the HP fill (AC17). On a decrease it holds the previous level for
    /// <see cref="HoldSeconds"/>, then retracts linearly to the new value in <see cref="RetractSeconds"/>.
    /// A hit while the trail is still visible keeps the highest level and restarts the hold. Increases (healing)
    /// show no trail.
    /// </summary>
    public sealed class DamageTrail
    {
        private float _level, _target, _elapsed;

        public DamageTrail(float holdSeconds, float retractSeconds)
        {
            HoldSeconds = holdSeconds;
            RetractSeconds = retractSeconds;
        }

        public float HoldSeconds { get; set; }
        public float RetractSeconds { get; set; }
        /// <summary>Trail level 0..1 to draw while <see cref="IsActive"/>; equals the latest target otherwise.</summary>
        public float Value { get; private set; }
        /// <summary>True while holding or retracting; when false the trail should be drawn under the fill.</summary>
        public bool IsActive { get; private set; }

        public void Snap(float value)
        {
            _level = _target = Value = SmoothedBar.Clamp01(value);
            IsActive = false;
        }

        /// <summary>
        /// Call when the bar receives a new target. <paramref name="shownBefore"/> is the fill value displayed
        /// just before the change.
        /// </summary>
        public void OnTargetChanged(float shownBefore, float newTarget)
        {
            newTarget = SmoothedBar.Clamp01(newTarget);
            shownBefore = SmoothedBar.Clamp01(shownBefore);
            var top = IsActive && Value > shownBefore ? Value : shownBefore;
            if (newTarget >= top)
            {
                // Healing (or no loss): no trail.
                _level = _target = Value = newTarget;
                IsActive = false;
                return;
            }
            _level = top;
            _target = newTarget;
            _elapsed = 0f;
            Value = top;
            IsActive = true;
        }

        /// <summary>Advances time; returns true if <see cref="Value"/> or <see cref="IsActive"/> changed.</summary>
        public bool Tick(float deltaSeconds)
        {
            if (!IsActive) return false;
            if (deltaSeconds > 0f) _elapsed += deltaSeconds;
            var previous = Value;
            if (_elapsed < HoldSeconds) Value = _level;
            else if (RetractSeconds > 0f && _elapsed < HoldSeconds + RetractSeconds)
                Value = _level + (_target - _level) * ((_elapsed - HoldSeconds) / RetractSeconds);
            else
            {
                Value = _target;
                IsActive = false;
                return true;
            }
            return Value != previous;
        }
    }
}
