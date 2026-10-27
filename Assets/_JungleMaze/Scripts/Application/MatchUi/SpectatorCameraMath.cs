using System;

namespace JungleMaze.App.MatchUi
{
    /// <summary>Pure camera maths for the spectator and killcam cameras (G-5, AC24, AC25).</summary>
    public static class SpectatorCameraMath
    {
        /// <summary>Ease-out quad: fast start, settles on the target without overshoot.</summary>
        public static float EaseOut(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            var inv = 1f - t;
            return 1f - inv * inv;
        }

        /// <summary>
        /// Same rule as <c>HeroCameraFollow</c>: keep the view inside the square map; a view wider than the map stays
        /// centred on that axis.
        /// </summary>
        public static void ClampToMap(ref float x, ref float y, float halfWidth, float halfHeight, float mapSize)
        {
            x = halfWidth * 2f >= mapSize ? mapSize / 2f : Clamp(x, halfWidth, mapSize - halfWidth);
            y = halfHeight * 2f >= mapSize ? mapSize / 2f : Clamp(y, halfHeight, mapSize - halfHeight);
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }

    /// <summary>
    /// Where the spectator camera looks. After <see cref="Retarget"/> it eases from where it was to the (possibly
    /// moving) target in <see cref="SwitchSeconds"/>, then sticks to the target. Each step only covers part of the
    /// distance still left, so the camera never jumps past its target (AC25). Times come from an unscaled clock.
    /// </summary>
    public sealed class SpectatorCameraFollow
    {
        private float _fromX, _fromY;
        private double _switchStart = double.NegativeInfinity;
        private bool _hasPosition;

        public SpectatorCameraFollow(float switchSeconds) => SwitchSeconds = Math.Max(0f, switchSeconds);

        public float SwitchSeconds { get; }
        public float X { get; private set; }
        public float Y { get; private set; }
        public bool HasPosition => _hasPosition;

        public bool IsSwitching(double now) => now - _switchStart < SwitchSeconds;

        /// <summary>Starts a transition from the current position; the first target ever is taken at once.</summary>
        public void Retarget(double now)
        {
            if (!_hasPosition) return;
            _fromX = X;
            _fromY = Y;
            _switchStart = now;
        }

        /// <summary>Puts the camera somewhere without a transition (start of spectating, killcam).</summary>
        public void Snap(float x, float y)
        {
            X = x;
            Y = y;
            _hasPosition = true;
            _switchStart = double.NegativeInfinity;
        }

        public void Step(float targetX, float targetY, double now)
        {
            if (!_hasPosition) { Snap(targetX, targetY); return; }
            if (SwitchSeconds <= 0f || !IsSwitching(now)) { X = targetX; Y = targetY; return; }
            var e = SpectatorCameraMath.EaseOut((float)((now - _switchStart) / SwitchSeconds));
            X = _fromX + (targetX - _fromX) * e;
            Y = _fromY + (targetY - _fromY) * e;
        }

        public void Clear()
        {
            _hasPosition = false;
            _switchStart = double.NegativeInfinity;
        }
    }
}
