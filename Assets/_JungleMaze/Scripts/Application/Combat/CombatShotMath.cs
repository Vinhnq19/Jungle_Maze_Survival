using System;

namespace JungleMaze.App.Combat
{
    /// <summary>
    /// Pure aim and spread math shared by the request builder and the presentation. Aim uses the 0..65535 scale of
    /// AttackRequestPacket.Aim, which maps to 0..360 degrees.
    /// </summary>
    public static class CombatShotMath
    {
        private const double AimSteps = 65536.0;

        /// <summary>Wraps any angle into [0, 360) and quantises it to the wire scale.</summary>
        public static ushort EncodeAim(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees)) degrees = 0f;
            var wrapped = degrees % 360.0;
            if (wrapped < 0.0) wrapped += 360.0;
            var scaled = (long)Math.Round(wrapped * AimSteps / 360.0);
            return (ushort)(scaled & 0xFFFF);
        }

        /// <summary>Inverse of <see cref="EncodeAim"/>, returns degrees in [0, 360).</summary>
        public static float DecodeAim(ushort aim) => (float)(aim * 360.0 / AimSteps);

        /// <summary>
        /// Fills <paramref name="angles"/> with one direction per pellet. A spread weapon covers
        /// <paramref name="spreadDegrees"/> in total (edge to edge), centred on the aim. A single pellet fires straight.
        /// Returns the number of angles written (capped by the buffer length). Allocation-free.
        /// </summary>
        public static int PelletAngles(float aimDegrees, int pellets, float spreadDegrees, float[] angles)
        {
            if (angles == null) throw new ArgumentNullException(nameof(angles));
            var count = Math.Min(Math.Max(1, pellets), angles.Length);
            if (count == 1)
            {
                angles[0] = aimDegrees;
                return 1;
            }
            var step = spreadDegrees / (count - 1);
            var start = aimDegrees - spreadDegrees * 0.5f;
            for (var i = 0; i < count; i++) angles[i] = start + step * i;
            return count;
        }
    }
}
