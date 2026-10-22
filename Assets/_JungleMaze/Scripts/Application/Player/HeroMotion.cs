using System;

namespace JungleMaze.App.Player
{
    /// <summary>
    /// Pure kinematics for the Hero Controller (MOV-01): diagonal-normalized WASD movement and
    /// exponential aim smoothing. No UnityEngine dependency, so it is fully covered by EditMode tests.
    /// Deliberately does not know about the network: prediction/reconciliation against server state
    /// is the scope of MOV-02, not this type.
    /// </summary>
    public static class HeroMotion
    {
        /// <summary>
        /// Normalizes diagonal input so moving on two axes at once is never faster than one axis
        /// (e.g. WASD held as W+D), then scales by <paramref name="speed"/> and <paramref name="deltaTime"/>.
        /// </summary>
        public static (float X, float Y) ComputeMoveDelta(float moveX, float moveY, float speed, float deltaTime)
        {
            var sqrMagnitude = moveX * moveX + moveY * moveY;
            if (sqrMagnitude > 1f)
            {
                var inverseLength = 1f / (float)Math.Sqrt(sqrMagnitude);
                moveX *= inverseLength;
                moveY *= inverseLength;
            }
            var distance = speed * deltaTime;
            return (moveX * distance, moveY * distance);
        }

        /// <summary>
        /// Smoothly rotates <paramref name="currentDegrees"/> toward <paramref name="targetDegrees"/> using an
        /// exponential decay with time constant <paramref name="aimLerpTime"/> (0 snaps instantly), always
        /// turning the shorter way around the circle.
        /// </summary>
        public static float SmoothAimDegrees(float currentDegrees, float targetDegrees, float aimLerpTime, float deltaTime)
        {
            if (aimLerpTime <= 0f) return NormalizeDegrees(targetDegrees);
            var delta = NormalizeDegrees(targetDegrees - currentDegrees);
            var t = 1f - (float)Math.Exp(-deltaTime / aimLerpTime);
            return NormalizeDegrees(currentDegrees + delta * t);
        }

        /// <summary>Wraps an angle to (-180, 180].</summary>
        public static float NormalizeDegrees(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f) degrees -= 360f;
            else if (degrees <= -180f) degrees += 360f;
            return degrees;
        }
    }
}
