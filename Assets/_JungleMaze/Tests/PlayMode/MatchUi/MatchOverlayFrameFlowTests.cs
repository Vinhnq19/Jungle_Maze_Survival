#if UNITY_EDITOR
using System.Collections;
using JungleMaze.App.Flow;
using JungleMaze.App.MatchUi;
using JungleMaze.Domain.MatchUi;
using JungleMaze.Presentation.UI.MatchOverlay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace JungleMaze.Tests.PlayMode.MatchUi
{
    /// <summary>
    /// UI-03 on real frames over a longer run (review lát 7–11, Gợi ý 7): the spectator camera through 20 switches in
    /// 5 s (AC25) and two matches in a row on the same overlay (AC42).
    /// </summary>
    public sealed class MatchOverlayFrameFlowTests
    {
        private OverlayRig _rig;
        private GameObject _camGo, _driverGo;

        [TearDown]
        public void TearDown()
        {
            _rig?.Dispose();
            _rig = null;
            if (_driverGo != null) Object.Destroy(_driverGo);
            if (_camGo != null) Object.Destroy(_camGo);
        }

        private static int CountObjects() => Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        private Camera MakeCamera()
        {
            _camGo = new GameObject("UI-03 spectator cam", typeof(Camera));
            var cam = _camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.aspect = 16f / 9f;
            cam.transform.position = new Vector3(25f, 25f, -10f);
            _driverGo = new GameObject("UI-03 camera driver");
            _driverGo.AddComponent<MatchOverlayCameraDriver>().Construct(_rig.Flow, _rig.Clock, 50f, cam);
            return cam;
        }

        // AC25: 20 switches in 5 s on real frames: no frame moves the camera further than the distance left to the
        // current target, the camera ends on the last target (not stuck), nothing is created along the way.
        [UnityTest]
        public IEnumerator Switch20In5s_NoOvershoot_NoLeak_NotStuck()
        {
            _rig = new OverlayRig();
            var cam = MakeCamera();
            // Everyone well inside the map, so the map clamp never bends the path.
            for (byte id = 1; id < 10; id++) _rig.Spectator.SetPosition(id, 15f + (id % 3) * 10f, 12f + (id / 3) * 8f);
            _rig.Eliminate();
            Assert.IsTrue(_rig.Flow.Spectate());
            for (var i = 0; i < 20; i++) yield return null; // settle on the first target
            var objects = CountObjects();

            var switches = 0;
            var start = Time.unscaledTimeAsDouble;
            var lastSwitch = start;
            var worstExcess = 0f;
            var frames = 0;
            while (switches < 20 && Time.unscaledTimeAsDouble - start < 6.0)
            {
                if (Time.unscaledTimeAsDouble - lastSwitch >= 0.25)
                {
                    _rig.Flow.Targets.Next();
                    lastSwitch = Time.unscaledTimeAsDouble;
                    switches++;
                }
                var before = (Vector2)cam.transform.position;
                Assert.IsTrue(_rig.Spectator.TryGetPosition(_rig.Flow.Targets.Current, out var tx, out var ty));
                var target = new Vector2(tx, ty);
                yield return null; // the driver steps the camera in this frame's LateUpdate
                var after = (Vector2)cam.transform.position;
                worstExcess = Mathf.Max(worstExcess, Vector2.Distance(before, after) - Vector2.Distance(before, target));
                frames++;
            }
            var took = Time.unscaledTimeAsDouble - start;
            Assert.AreEqual(20, switches);
            Assert.LessOrEqual(took, 5.3, $"20 switches took {took:0.00} s");
            Assert.LessOrEqual(worstExcess, 1e-3f, "a frame jumped past the target");

            var settle = Time.unscaledTimeAsDouble;
            while (Time.unscaledTimeAsDouble - settle < 0.4) yield return null;
            Assert.IsTrue(_rig.Spectator.TryGetPosition(_rig.Flow.Targets.Current, out var fx, out var fy));
            Assert.AreEqual(fx, cam.transform.position.x, 0.1f, "camera on the last target");
            Assert.AreEqual(fy, cam.transform.position.y, 0.1f);
            Assert.AreEqual(objects, CountObjects(), "no objects created by switching");
            Assert.AreEqual(MatchOverlayPhase.Spectating, _rig.Flow.Phase);
            Debug.Log($"[UI-03 AC25] 20 switches in {took:0.00} s over {frames} frames, worst step beyond the target {worstExcess:0.0000} cells");
        }

        // AC42 (+ AC37 view side): match 1 ends during the killcam → the replay is released at once, no frame is drawn
        // after the screen change, the spectator list is unbound; match 2 shows only its own players and data.
        [UnityTest]
        public IEnumerator TwoMatchesInARow_ReleaseReplay_NoOldData()
        {
            _rig = new OverlayRig(killcam: true);
            yield return null;
            var objects = CountObjects();

            // Match 1: killcam playing on real frames.
            _rig.Eliminate();
            Assert.AreEqual(MatchOverlayPhase.Killcam, _rig.Flow.Phase);
            for (var i = 0; i < 20; i++) yield return null;
            Assert.Greater(_rig.Replay.HeldFrames, 0, "the replay is held while playing");
            var countdown = _rig.Root.Killcam.Countdown.text;

            _rig.Bus.Publish(new AppScreenChanged(AppScreen.InMatch, AppScreen.Results, null));
            Assert.AreEqual(0, _rig.Replay.HeldFrames, "released in the same call");
            Assert.IsNull(_rig.Killcam.Clip);
            Assert.AreEqual(MatchOverlayPhase.Inactive, _rig.Flow.Phase);
            Assert.IsFalse(_rig.Root.Canvas.enabled, "canvas off outside the match");
            for (var i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(countdown, _rig.Root.Killcam.Countdown.text, "no killcam frame drawn after the screen change");

            // Match 2: three other players, a zone elimination, spectate.
            _rig.Elimination.Reset();
            _rig.Spectator.Reset();
            _rig.Spectator.Populate(3, localNetId: 5, startCount: 4);
            _rig.Bus.Publish(new AppScreenChanged(AppScreen.MainMenu, AppScreen.InMatch, null));
            Assert.AreEqual(MatchOverlayPhase.Playing, _rig.Flow.Phase);
            Assert.IsTrue(_rig.Root.Canvas.enabled);
            yield return null;
            Assert.IsFalse(_rig.Presenter.KillcamFade.Visible, "match 1's killcam is not on screen");
            Assert.IsFalse(_rig.Presenter.EliminatedFade.Visible);
            Assert.IsFalse(_rig.Presenter.SpectatorFade.Visible);

            _rig.Eliminate(EliminationCause.Zone);
            Assert.AreEqual(MatchOverlayPhase.Eliminated, _rig.Flow.Phase, "zone: no killer, no killcam");
            Assert.AreEqual(1, _rig.Replay.RequestCount, "only match 1 asked for a replay");
            Assert.IsTrue(_rig.Flow.Spectate());
            yield return null;
            var shown = 0;
            foreach (var row in _rig.Root.Spectator.Rows)
            {
                if (!row.gameObject.activeSelf) continue;
                shown++;
                Assert.IsTrue(_rig.Spectator.TryGetEntry(row.NetId, out var entry), $"row {row.NetId} belongs to match 2");
                Assert.AreEqual(entry.Name, row.Label.text);
            }
            Assert.AreEqual(3, shown, "three rows, none left over from match 1's nine");
            Assert.AreEqual("Còn sống 3/4", _rig.Root.Spectator.AliveCount.text);

            _rig.Bus.Publish(new AppScreenChanged(AppScreen.InMatch, AppScreen.Results, null));
            Assert.AreEqual(0, _rig.Replay.HeldFrames);
            for (var i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(objects, CountObjects(), "nothing created across two matches");
        }
    }
}
#endif
