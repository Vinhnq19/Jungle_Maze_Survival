#if UNITY_EDITOR
using System.Collections;
using System.Diagnostics;
using System.Threading;
using JungleMaze.Domain.MatchUi;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace JungleMaze.Tests.PlayMode.MatchUi
{
    /// <summary>UI-03 budgets measured on real frames with <c>Time.unscaledTimeAsDouble</c>.</summary>
    public sealed class MatchOverlayTimingTests
    {
        private OverlayRig _rig;

        [TearDown]
        public void TearDown()
        {
            _rig?.Dispose();
            _rig = null;
            Time.timeScale = 1f;
        }

        private static int CountObjects() => Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        private static int CountCanvases() => Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        // AC1: first open after load ≤ 200 ms fully visible and clickable, close ≤ 150 ms, nothing instantiated on press.
        [UnityTest]
        public IEnumerator OpenWithin200ms_CloseWithin150ms_FirstOpenPrebuilt()
        {
            _rig = new OverlayRig();
            yield return null;
            var objects = CountObjects();
            var start = Time.unscaledTimeAsDouble;
            _rig.Menu.PressEscape();
            Assert.IsTrue(_rig.Root.Menu.Group.interactable, "clickable at once");
            while (_rig.Root.Menu.Group.alpha < 1f && Time.unscaledTimeAsDouble - start < 1.0) yield return null;
            var opened = Time.unscaledTimeAsDouble - start;
            Assert.AreEqual(objects, CountObjects(), "no Instantiate when opening");
            Assert.LessOrEqual(opened, 0.2, $"open took {opened * 1000:0} ms");

            start = Time.unscaledTimeAsDouble;
            _rig.Menu.PressEscape();
            Assert.IsFalse(_rig.Root.Menu.Group.interactable);
            while (_rig.Root.Menu.Group.alpha > 0f && Time.unscaledTimeAsDouble - start < 1.0) yield return null;
            var closed = Time.unscaledTimeAsDouble - start;
            Assert.LessOrEqual(closed, 0.15, $"close took {closed * 1000:0} ms");
            Debug.Log($"[UI-03 AC1] open {opened * 1000:0} ms, close {closed * 1000:0} ms");
        }

        // AC2 (frame side): the menu never touches timeScale and the game keeps ticking while it is open.
        [UnityTest]
        public IEnumerator MenuOpen_DoesNotPause()
        {
            _rig = new OverlayRig();
            yield return null;
            _rig.Menu.Open();
            var frames = _rig.Driver.Frames;
            var start = Time.unscaledTimeAsDouble;
            var fixedSteps = 0;
            while (Time.unscaledTimeAsDouble - start < 2.0)
            {
                Assert.AreEqual(1f, Time.timeScale);
                yield return new WaitForFixedUpdate();
                fixedSteps++;
            }
            Assert.IsTrue(_rig.Menu.IsOpen);
            Assert.Greater(_rig.Driver.Frames - frames, 30, "ticks continue while the menu is open");
            Assert.Greater(fixedSteps, 30, "physics steps continue while the menu is open");
        }

        // AC3: 20 open/close pairs, no frame over 33.4 ms, no leaked objects or canvases, no errors. Two numbers: the
        // code cost of each toggle (Stopwatch) and the real length of the frame the toggle happened in, read as
        // Time.unscaledDeltaTime on the next frame, so canvas/TMP rebuild and rendering are counted too.
        [UnityTest]
        public IEnumerator Toggle20Times_NoLongFrame_NoLeak()
        {
            _rig = new OverlayRig();
            for (var i = 0; i < 10; i++) yield return null; // first-frame warm-up is not the menu's
            var objects = CountObjects();
            var canvases = CountCanvases();
            var idleWorst = 0.0;
            for (var i = 0; i < 40; i++)
            {
                yield return null;
                idleWorst = System.Math.Max(idleWorst, Time.unscaledDeltaTime * 1000.0);
            }
            var watch = new Stopwatch();
            var worstCost = 0.0;
            var worstFrame = 0.0;
            for (var i = 0; i < 40; i++)
            {
                watch.Restart();
                _rig.Menu.PressEscape();
                _rig.Presenter.Tick(1f / 60f);
                watch.Stop();
                worstCost = System.Math.Max(worstCost, watch.Elapsed.TotalMilliseconds);
                yield return null;
                worstFrame = System.Math.Max(worstFrame, Time.unscaledDeltaTime * 1000.0);
            }
            Debug.Log($"[UI-03 AC3] 20 open/close pairs: worst toggle cost {worstCost:0.000} ms, worst toggle frame {worstFrame:0.0} ms (idle frames before: worst {idleWorst:0.0} ms)");
            Assert.LessOrEqual(worstCost, 33.4, $"worst toggle cost {worstCost:0.00} ms");
            Assert.LessOrEqual(worstFrame, 33.4, $"worst toggle frame {worstFrame:0.0} ms (idle worst {idleWorst:0.0} ms)");
            Assert.AreEqual(objects, CountObjects());
            Assert.AreEqual(canvases, CountCanvases());
        }

        // AC18 + AC21: eliminated screen ≤ 500 ms, spectator ≤ 500 ms after the click.
        [UnityTest]
        public IEnumerator Eliminated_Then_Spectate_Within500ms()
        {
            _rig = new OverlayRig();
            yield return null;
            var start = Time.unscaledTimeAsDouble;
            _rig.Eliminate();
            while (_rig.Root.Eliminated.Group.alpha < 1f && Time.unscaledTimeAsDouble - start < 2.0) yield return null;
            var shown = Time.unscaledTimeAsDouble - start;
            Assert.LessOrEqual(shown, 0.5, $"eliminated screen after {shown * 1000:0} ms");

            start = Time.unscaledTimeAsDouble;
            _rig.Root.Eliminated.Spectate.onClick.Invoke();
            while (_rig.Root.Spectator.Group.alpha < 1f && Time.unscaledTimeAsDouble - start < 2.0) yield return null;
            var spectating = Time.unscaledTimeAsDouble - start;
            Assert.AreEqual(MatchOverlayPhase.Spectating, _rig.Flow.Phase);
            Assert.LessOrEqual(spectating, 0.5, $"spectator after {spectating * 1000:0} ms");
        }

        // AC28 (view side): match end while spectating hands off ≤ 500 ms and no empty list stays on screen.
        [UnityTest]
        public IEnumerator MatchEnd_HandsOff_NoEmptyList()
        {
            _rig = new OverlayRig();
            yield return null;
            _rig.Eliminate();
            _rig.Flow.Spectate();
            yield return null;
            var start = Time.unscaledTimeAsDouble;
            _rig.Spectator.EndMatch();
            while (_rig.Root.Spectator.Group.alpha > 0f && Time.unscaledTimeAsDouble - start < 2.0) yield return null;
            Assert.AreEqual(MatchOverlayPhase.HandedOff, _rig.Flow.Phase);
            Assert.LessOrEqual(Time.unscaledTimeAsDouble - start, 0.5);
            Assert.IsFalse(_rig.Root.Spectator.Group.blocksRaycasts);
        }

        // AC32: 5.0 ± 0.1 s on real frames, with one 100 ms hitch.
        [UnityTest]
        public IEnumerator Killcam_FiveSeconds_WithOne100msHitch()
        {
            _rig = new OverlayRig(killcam: true);
            yield return null;
            _rig.Eliminate();
            Assert.AreEqual(MatchOverlayPhase.Killcam, _rig.Flow.Phase);
            var start = _rig.Killcam.PlayStartedAt;
            var hitched = false;
            while (_rig.Flow.Phase == MatchOverlayPhase.Killcam && Time.unscaledTimeAsDouble - start < 7.0)
            {
                if (!hitched && Time.unscaledTimeAsDouble - start > 2.0) { Thread.Sleep(100); hitched = true; }
                yield return null;
            }
            var played = _rig.Killcam.EndedAt - start;
            Assert.AreEqual(MatchOverlayPhase.Eliminated, _rig.Flow.Phase);
            Assert.AreEqual(5.0, played, 0.1, $"played {played:0.000} s");
            Debug.Log($"[UI-03 AC32] killcam played {played:0.000} s with a 100 ms hitch");
        }

        // AC33: Space skips, eliminated screen ≤ 500 ms, ESC still opens the menu during the killcam.
        [UnityTest]
        public IEnumerator Killcam_EscOpensMenu_SpaceSkips()
        {
            _rig = new OverlayRig(killcam: true);
            yield return null;
            _rig.Eliminate();
            yield return null;
            Assert.IsTrue(_rig.Menu.PressEscape());
            Assert.IsTrue(_rig.Menu.IsOpen, "ESC works during the killcam (AC6)");
            Assert.AreEqual(MatchOverlayPhase.Killcam, _rig.Flow.Phase);
            _rig.Menu.PressEscape();
            var keys = new System.Collections.Generic.HashSet<KeyCode> { KeyCode.Space };
            var input = new JungleMaze.Presentation.UI.MatchOverlay.MatchOverlayInput(_rig.Menu, _rig.Flow, keys.Contains);
            var start = Time.unscaledTimeAsDouble;
            input.Poll();
            while (_rig.Root.Eliminated.Group.alpha < 1f && Time.unscaledTimeAsDouble - start < 2.0) yield return null;
            Assert.LessOrEqual(Time.unscaledTimeAsDouble - start, 0.5);
            Assert.IsTrue(_rig.Killcam.Skipped);
        }
    }
}
#endif
