using System;
using JungleMaze.App.MatchUi;
using JungleMaze.Domain.MatchUi;
using UnityEngine;

namespace JungleMaze.Presentation.UI.MatchOverlay
{
    /// <summary>
    /// Spectator camera. Writes <c>Camera.main</c> in LateUpdate at order 1000, after the demo's Update and
    /// <c>HeroCameraFollow.LateUpdate</c>, and only while spectating; otherwise it writes nothing and the previous owner
    /// takes the camera back the next frame. Keeps the current orthographic size (Q12) and the map clamp of
    /// <c>HeroCameraFollow</c> (AC24). Does not touch the live player's camera code.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class MatchOverlayCameraDriver : MonoBehaviour
    {
        private MatchOverlayFlow _flow;
        private Func<double> _clock;
        private float _mapSize = 50f;
        private Camera _camera;

        public bool Wrote { get; private set; }
        public Vector2 LastCenter { get; private set; }

        public void Construct(MatchOverlayFlow flow, Func<double> clock, float mapSizeCells, Camera cameraOverride = null)
        {
            _flow = flow;
            _clock = clock;
            _mapSize = mapSizeCells > 0f ? mapSizeCells : 50f;
            _camera = cameraOverride;
        }

        private void LateUpdate() => Drive();

        /// <summary>One camera step; public so PlayMode tests can drive it frame by frame.</summary>
        public void Drive()
        {
            Wrote = false;
            if (_flow == null || _flow.Phase != MatchOverlayPhase.Spectating) return;
            var spectator = _flow.Spectator;
            var target = _flow.Targets.Current;
            if (spectator == null || target == SpectatorTargets.None || !spectator.TryGetPosition(target, out var x, out var y)) return;
            var cam = _camera != null ? _camera : Camera.main;
            if (cam == null) return;
            _flow.Camera.Step(x, y, _clock());
            var cx = _flow.Camera.X;
            var cy = _flow.Camera.Y;
            var halfHeight = cam.orthographic ? cam.orthographicSize : 0f;
            SpectatorCameraMath.ClampToMap(ref cx, ref cy, halfHeight * cam.aspect, halfHeight, _mapSize);
            var p = cam.transform.position;
            cam.transform.position = new Vector3(cx, cy, p.z);
            LastCenter = new Vector2(cx, cy);
            Wrote = true;
        }
    }
}
