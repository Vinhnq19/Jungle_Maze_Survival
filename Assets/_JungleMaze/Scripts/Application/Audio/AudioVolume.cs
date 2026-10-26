using System;

namespace JungleMaze.App.Audio
{
    /// <summary>Slider value to mixer decibels (G-1): dB = 20·log10(v), v ≤ 0.0001 → −80 dB.</summary>
    public static class AudioVolume
    {
        public const float MinDecibels = -80f;

        public static float Clamp01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;

        public static float ToDecibels(float volume01)
        {
            var v = Clamp01(volume01);
            return v <= 0.0001f ? MinDecibels : Math.Max(MinDecibels, 20f * (float)Math.Log10(v));
        }
    }
}
