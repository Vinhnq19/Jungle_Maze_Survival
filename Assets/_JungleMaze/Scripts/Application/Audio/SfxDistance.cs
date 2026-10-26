using System;

namespace JungleMaze.App.Audio
{
    /// <summary>Linear distance falloff (G-3): gain = 1 − d/R, silent at d ≥ R. Range 0 = 2D (always 1).</summary>
    public static class SfxDistance
    {
        public static float Gain(float sourceX, float sourceY, float listenerX, float listenerY, float rangeCells)
        {
            if (rangeCells <= 0f) return 1f;
            var dx = sourceX - listenerX;
            var dy = sourceY - listenerY;
            var d = (float)Math.Sqrt(dx * dx + dy * dy);
            return d >= rangeCells ? 0f : 1f - d / rangeCells;
        }
    }
}
