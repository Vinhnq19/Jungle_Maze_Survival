using System;
using System.Collections.Generic;
using JungleMaze.App.Flow;
using JungleMaze.App.MatchUi;
using JungleMaze.App.MatchUi.Mocks;
using JungleMaze.Core.Events;
using JungleMaze.Domain.MatchUi;
using NUnit.Framework;

namespace JungleMaze.Tests.MatchUi
{
    public sealed class SpectatorCameraMathTests
    {
        // AC25: 0.25 s ease-out, monotonic, never past the target, each step ≤ the distance left.
        [Test]
        public void EaseOut0_25_NoOvershoot()
        {
            var cam = new SpectatorCameraFollow(0.25f);
            cam.Snap(0f, 0f);
            cam.Retarget(10.0);
            var previousLeft = 20f;
            for (var frame = 1; frame <= 30; frame++)
            {
                var now = 10.0 + frame / 60.0;
                var beforeX = cam.X;
                cam.Step(20f, 0f, now);
                var left = 20f - cam.X;
                Assert.GreaterOrEqual(left, -1e-5f, "never past the target");
                Assert.LessOrEqual(left, previousLeft + 1e-5f, "monotonic");
                Assert.LessOrEqual(cam.X - beforeX, previousLeft + 1e-5f, "step ≤ distance left");
                previousLeft = left;
                if (now - 10.0 >= 0.25) Assert.AreEqual(20f, cam.X, 1e-4f, "arrived by 0.25 s");
            }
            Assert.IsFalse(cam.IsSwitching(10.3));
        }

        // AC24 (logic): after the switch the camera sits on the target, standing or running at 4 cells/s.
        [Test]
        public void FollowsStaticAndRunningTarget()
        {
            var cam = new SpectatorCameraFollow(0.25f);
            cam.Snap(5f, 5f);
            cam.Retarget(0.0);
            // The target runs at 4 cells/s from the first frame, during the switch as well.
            var x = 30f;
            var maxDeviation = 0f;
            var previousGap = float.MaxValue;
            for (var frame = 1; frame <= 120; frame++)
            {
                var now = frame / 60.0;
                x += 4f / 60f;
                cam.Step(x, 20f, now);
                var gap = Math.Abs(cam.X - x) + Math.Abs(cam.Y - 20f);
                if (now <= 0.25)
                {
                    Assert.LessOrEqual(cam.X, x + 1e-4f, "never ahead of the running target");
                    Assert.LessOrEqual(gap, previousGap + 1e-4f, "the gap only closes during the switch");
                    previousGap = gap;
                }
                else maxDeviation = Math.Max(maxDeviation, gap);
            }
            Assert.LessOrEqual(maxDeviation, 0.5f, "AC24: ≤ 0.5 cell while the target runs");
            Assert.LessOrEqual(maxDeviation, 0.1f);
        }

        [Test]
        public void ClampToMap_SameRuleAsHeroCamera()
        {
            float x = 1f, y = 49f;
            SpectatorCameraMath.ClampToMap(ref x, ref y, 8f, 4.5f, 50f);
            Assert.AreEqual(8f, x);
            Assert.AreEqual(45.5f, y);
            x = 10f; y = 10f;
            SpectatorCameraMath.ClampToMap(ref x, ref y, 49.8f, 28f, 50f);
            Assert.AreEqual(25f, x, "wider than the map: centred");
            Assert.AreEqual(25f, y);
        }
    }

    public sealed class SpectatorTargetsTests
    {
        private MockSpectatorSource _source;
        private SpectatorTargets _targets;

        [SetUp]
        public void SetUp()
        {
            _source = new MockSpectatorSource();
            _source.Populate(9);
            _targets = new SpectatorTargets(_source);
        }

        [Test]
        public void NextPrevious_Wrap_SelectRow()
        {
            Assert.IsTrue(_targets.Begin());
            Assert.AreEqual(1, _targets.Current);
            _targets.Previous();
            Assert.AreEqual(9, _targets.Current, "wraps backwards");
            _targets.Next();
            Assert.AreEqual(1, _targets.Current, "wraps forwards");
            for (var i = 0; i < 9; i++) _targets.Next();
            Assert.AreEqual(1, _targets.Current, "nine steps back to the start");
            Assert.IsTrue(_targets.Select(6));
            Assert.AreEqual(6, _targets.Current);
            Assert.IsFalse(_targets.Select(0), "self is not selectable");
        }

        // AC26
        [Test]
        public void AutoAdvance_KillerButtonDisabled()
        {
            var changes = new List<(byte, bool)>();
            _targets.TargetChanged += (id, auto) => changes.Add((id, auto));
            _targets.KillerNetId = 2;
            _targets.Begin();
            Assert.IsTrue(_targets.CanJumpToKiller);
            Assert.IsTrue(_targets.JumpToKiller());
            Assert.AreEqual(2, _targets.Current);
            Assert.AreEqual("Hào", _targets.CurrentName);

            _source.Eliminate(2);
            Assert.IsTrue(_targets.OnPlayerEliminated(2));
            Assert.AreEqual(3, _targets.Current, "next alive after the eliminated one");
            Assert.AreEqual((byte)3, changes[changes.Count - 1].Item1);
            Assert.IsTrue(changes[changes.Count - 1].Item2, "automatic");
            Assert.IsFalse(_targets.CanJumpToKiller, "killer is out");
            Assert.IsFalse(_targets.OnPlayerEliminated(7), "someone else");
        }
    }

    public sealed class MatchOverlayFlowTests
    {
        private static readonly Guid Match = Guid.NewGuid();
        private EventBus _bus;
        private FakeClock _clock;
        private MockEliminationSource _elimination;
        private MockSpectatorSource _spectator;
        private MatchOverlayFlow _flow;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _clock = new FakeClock { Now = 100 };
            _elimination = new MockEliminationSource();
            _spectator = new MockSpectatorSource();
            _spectator.Populate(9);
            _flow = new MatchOverlayFlow(_bus, _clock.Read, _elimination, _spectator, AppScreen.InMatch);
        }

        [TearDown]
        public void TearDown() => _flow.Dispose();

        private void Eliminate(EliminationCause cause = EliminationCause.Player) =>
            _elimination.Eliminate(MockEliminationSource.Sample(cause, Match));

        // AC18 (logic) + AC21 (logic): eliminated screen at once, spectate at once.
        [Test]
        public void Eliminated_ThenSpectate_SameCall()
        {
            Assert.AreEqual(MatchOverlayPhase.Playing, _flow.Phase);
            Eliminate();
            Assert.AreEqual(MatchOverlayPhase.Eliminated, _flow.Phase);
            Assert.AreEqual(100.0, _flow.PhaseChangedAt);
            Assert.AreEqual(4, _flow.Info.Rank);
            Assert.IsNull(_flow.SpectateBlockedReason);
            Assert.IsTrue(_flow.Spectate());
            Assert.AreEqual(MatchOverlayPhase.Spectating, _flow.Phase);
            Assert.AreEqual(1, _flow.Targets.Current);
            Assert.AreEqual(MockEliminationSource.KillerNetId, _flow.Targets.KillerNetId);
            Assert.IsTrue(_flow.Camera.HasPosition);
        }

        // AC21: nobody alive → disabled with a reason, no hang.
        [Test]
        public void SpectateDisabled_WhenNoneAlive()
        {
            _spectator.Populate(0);
            Eliminate();
            Assert.IsFalse(_flow.CanSpectate);
            Assert.IsNotNull(_flow.SpectateBlockedReason);
            Assert.IsFalse(_flow.Spectate());
            Assert.AreEqual(MatchOverlayPhase.Eliminated, _flow.Phase);
        }

        // AC26: the watched player is out → next one in the same call, notice ≤ 2 s.
        [Test]
        public void WatchedPlayerOut_AutoAdvance_NoticeTwoSeconds()
        {
            Eliminate();
            _flow.Spectate();
            _flow.Targets.Select(4);
            _spectator.Eliminate(4);
            Assert.AreEqual(5, _flow.Targets.Current);
            Assert.AreEqual("Lan đã bị loại", _flow.Notice);
            _clock.Advance(1.9);
            _flow.Tick(0.016f);
            Assert.IsNotNull(_flow.Notice);
            _clock.Advance(0.2);
            _flow.Tick(0.016f);
            Assert.IsNull(_flow.Notice, "gone after 2 s");
        }

        // AC28
        [Test]
        public void NoAliveOrMatchEnded_HandOff_SameCall()
        {
            Eliminate();
            _flow.Spectate();
            for (byte id = 1; id <= 9; id++) _spectator.Eliminate(id);
            Assert.AreEqual(MatchOverlayPhase.HandedOff, _flow.Phase, "last alive gone → results flow");
            Assert.IsFalse(_flow.Targets.HasTarget);
            Assert.IsFalse(_flow.Camera.HasPosition);

            _bus.Publish(new AppScreenChanged(AppScreen.InMatch, AppScreen.Results, null));
            _bus.Publish(new AppScreenChanged(AppScreen.Results, AppScreen.InMatch, null));
            _elimination.Reset();
            _spectator.Populate(5);
            Eliminate();
            _flow.Spectate();
            _spectator.EndMatch();
            Assert.AreEqual(MatchOverlayPhase.HandedOff, _flow.Phase, "match ended → results flow");
        }

        [Test]
        public void MatchEndsOnEliminatedScreen_HandsOff()
        {
            Eliminate();
            _spectator.EndMatch();
            Assert.AreEqual(MatchOverlayPhase.HandedOff, _flow.Phase, "not stuck on the eliminated screen (AC43)");
        }

        // AC5/AC42: leaving the match resets everything.
        [Test]
        public void ScreenChange_ResetsAndReleases()
        {
            Eliminate();
            _flow.Spectate();
            _bus.Publish(new AppScreenChanged(AppScreen.InMatch, AppScreen.MainMenu, null));
            Assert.AreEqual(MatchOverlayPhase.Inactive, _flow.Phase);
            Assert.IsFalse(_flow.Targets.HasTarget);
            Assert.AreEqual(0, _flow.Info.Rank);
            _elimination.Reset();
            var raised = 0;
            _elimination.LocalEliminated += _ => raised++;
            Eliminate();
            Assert.AreEqual(1, raised, "the source did raise the event");
            Assert.AreEqual(MatchOverlayPhase.Inactive, _flow.Phase, "eliminations outside a match are ignored");
        }

        [Test]
        public void OtherPlayerEliminated_DoesNotShowEliminatedScreen()
        {
            _spectator.Eliminate(3);
            Assert.AreEqual(MatchOverlayPhase.Playing, _flow.Phase);
        }

        // AC6: ESC works in every sub-state of the match.
        [Test]
        public void EscWorksInAllPhases()
        {
            _flow.Dispose();
            var replay = new MockKillcamReplaySource(_clock.Read);
            var killcam = new KillcamSession(_clock.Read, new KillcamOptions(true, 5f, 1f), replay);
            _flow = new MatchOverlayFlow(_bus, _clock.Read, _elimination, _spectator, AppScreen.InMatch, null, killcam);
            var menu = new MatchMenuController(_bus, _clock.Read, () => Match, AppScreen.InMatch);
            var keys = new HashSet<UnityEngine.KeyCode>();
            var input = new JungleMaze.Presentation.UI.MatchOverlay.MatchOverlayInput(menu, _flow, keys.Contains);
            void Press(UnityEngine.KeyCode key)
            {
                keys.Clear();
                keys.Add(key);
                input.Poll();
            }
            void Check(MatchOverlayPhase expected)
            {
                Assert.AreEqual(expected, _flow.Phase);
                Press(UnityEngine.KeyCode.Escape);
                Assert.IsTrue(menu.IsOpen, expected.ToString());
                Press(UnityEngine.KeyCode.Space);
                Press(UnityEngine.KeyCode.D);
                Assert.AreEqual(expected, _flow.Phase, "other UI-03 keys are ignored while the menu is open");
                Press(UnityEngine.KeyCode.Escape);
                Assert.IsFalse(menu.IsOpen);
            }
            Check(MatchOverlayPhase.Playing);
            Eliminate();
            Check(MatchOverlayPhase.Killcam);
            Press(UnityEngine.KeyCode.Space);
            Check(MatchOverlayPhase.Eliminated);
            _flow.Spectate();
            Check(MatchOverlayPhase.Spectating);
            menu.Dispose();
        }
    }
}
