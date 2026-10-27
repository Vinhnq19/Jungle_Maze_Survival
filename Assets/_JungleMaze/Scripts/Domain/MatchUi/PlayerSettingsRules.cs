using System;

namespace JungleMaze.Domain.MatchUi
{
    /// <summary>
    /// UI-03 G-9: mouse sensitivity 0.5–2.0 in 0.1 steps, default 1.0; screen shake on by default (Q5).
    /// Every value that reaches a reader goes through <see cref="ClampSensitivity"/> first (AC14).
    /// </summary>
    public static class PlayerSettingsRules
    {
        public const float MinSensitivity = 0.5f;
        public const float MaxSensitivity = 2f;
        public const float SensitivityStep = 0.1f;
        public const float DefaultSensitivity = 1f;
        public const bool DefaultScreenShake = true;

        /// <summary>NaN/∞ → default; otherwise clamped into range and rounded to the nearest 0.1 step.</summary>
        public static float ClampSensitivity(float raw)
        {
            if (float.IsNaN(raw) || float.IsInfinity(raw)) return DefaultSensitivity;
            if (raw <= MinSensitivity) return MinSensitivity;
            if (raw >= MaxSensitivity) return MaxSensitivity;
            var steps = Math.Round(raw / (double)SensitivityStep, MidpointRounding.AwayFromZero);
            return (float)(steps * SensitivityStep);
        }

        /// <summary>True when two clamped values are the same step (avoids float noise deciding "changed").</summary>
        public static bool SameStep(float a, float b) => Math.Abs(a - b) < SensitivityStep * 0.5f;
    }
}
