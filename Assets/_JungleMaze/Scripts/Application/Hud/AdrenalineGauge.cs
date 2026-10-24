namespace JungleMaze.App.Hud
{
    /// <summary>
    /// Adrenaline countdown shown on the HUD (AC12). The owner (Game-10) sets total and remaining; between writes
    /// the gauge counts down linearly on its own. Fill = remaining / total, clamped; total &lt;= 0 means empty.
    /// </summary>
    public sealed class AdrenalineGauge
    {
        public float TotalSeconds { get; private set; }
        public float RemainingSeconds { get; private set; }
        public bool IsActive => TotalSeconds > 0f && RemainingSeconds > 0f;
        public float Fill => IsActive ? RemainingSeconds / TotalSeconds : 0f;

        /// <summary>Applies a write from the owner. NaN is ignored; values are clamped to [0, total].</summary>
        public void Set(float totalSeconds, float remainingSeconds)
        {
            if (float.IsNaN(totalSeconds) || float.IsNaN(remainingSeconds)) return;
            TotalSeconds = totalSeconds > 0f ? totalSeconds : 0f;
            if (remainingSeconds < 0f) remainingSeconds = 0f;
            RemainingSeconds = remainingSeconds > TotalSeconds ? TotalSeconds : remainingSeconds;
        }

        /// <summary>Counts down; returns true if the gauge changed.</summary>
        public bool Tick(float deltaSeconds)
        {
            if (!IsActive || deltaSeconds <= 0f) return false;
            RemainingSeconds -= deltaSeconds;
            if (RemainingSeconds < 0f) RemainingSeconds = 0f;
            return true;
        }
    }
}
