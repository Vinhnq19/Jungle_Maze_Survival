using System;

namespace JungleMaze.App.Hud
{
    /// <summary>
    /// HUD feel parameters (spec assumptions G-3, G-9). Serialized on the HUD prefab, not in balance JSON (T1),
    /// so designers can tweak them without touching gameplay data.
    /// </summary>
    [Serializable]
    public struct HudTuning
    {
        /// <summary>Time for HP/armor/helmet bars to reach a new value (G-3).</summary>
        public float BarTweenSeconds;
        /// <summary>Damage trail hold time before retracting (G-9).</summary>
        public float TrailHoldSeconds;
        /// <summary>Damage trail retract time (G-9).</summary>
        public float TrailRetractSeconds;
        /// <summary>HP at or below this fraction of max turns the HP number red; 0 disables (G-9).</summary>
        public float LowHpRatio;
        /// <summary>Show the damage trail on HP loss (G-9).</summary>
        public bool DamageTrailEnabled;

        /// <summary>Values approved at G1 (24/10/2026).</summary>
        public static HudTuning Default => new HudTuning
        {
            BarTweenSeconds = 0.25f,
            TrailHoldSeconds = 0.3f,
            TrailRetractSeconds = 0.3f,
            LowHpRatio = 0.25f,
            DamageTrailEnabled = true
        };
    }
}
