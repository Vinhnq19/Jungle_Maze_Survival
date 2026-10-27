using System;
using JungleMaze.App.Flow;
using JungleMaze.App.MatchUi;
using JungleMaze.App.MatchUi.Mocks;
using JungleMaze.Core.Events;
using JungleMaze.Domain.MatchUi;
using NUnit.Framework;

namespace JungleMaze.Tests.MatchUi
{
    public sealed class KillcamSessionTests
    {
        private static readonly Guid Match = new Guid("0b7d4a10-2222-4c1e-8f00-00000000000c");
        private EventBus _bus;
        private FakeClock _clock;
        private MockEliminationSource _elimination;
        private MockSpectatorSource _spectator;
        private MockKillcamReplaySource _replay;
        private KillcamSession _killcam;
        private MatchOverlayFlow _flow;
        private double _eliminatedAt;

        private void Build(bool enabled, MockKillcamReplaySource.Case mode = MockKillcamReplaySource.Case.Full)
        {
            _bus = new EventBus();
            _clock = new FakeClock { Now = 50 };
            _elimination = new MockEliminationSource();
            _spectator = new MockSpectatorSource();
            _spectator.Populate(5, localNetId: 0);
            _replay = new MockKillcamReplaySource(_clock.Read) { Mode = mode };
            _killcam = new KillcamSession(_clock.Read, new KillcamOptions(enabled, 5f, 1f), _replay);
            _flow = new MatchOverlayFlow(_bus, _clock.Read, _elimination, _spectator, AppScreen.InMatch, null, _killcam);
        }

        [TearDown]
        public void TearDown() => _flow?.Dispose();

        private void Eliminate(EliminationCause cause = EliminationCause.Player)
        {
            _eliminatedAt = _clock.Now;
            _elimination.Eliminate(MockEliminationSource.Sample(cause, Match));
        }

        private void Frames(int count, double frameSeconds = 1.0 / 60.0)
        {
            for (var i = 0; i < count; i++)
            {
                _clock.Advance(frameSeconds);
                _flow.Tick((float)frameSeconds);
            }
        }

        /// <summary>Runs 60 fps frames until the phase leaves Killcam (or a cap), returns the unscaled time it took.</summary>
        private double RunUntilEliminated(int capFrames = 1000)
        {
            for (var i = 0; i < capFrames && _flow.Phase == MatchOverlayPhase.Killcam; i++) Frames(1);
            return _clock.Now - _eliminatedAt;
        }

        // AC31
        [Test]
        public void Disabled_NoRequest_NoNotice()
        {
            Build(enabled: false);
            Eliminate();
            Assert.AreEqual(MatchOverlayPhase.Eliminated, _flow.Phase);
            Assert.AreEqual(0, _replay.RequestCount);
            Assert.IsNull(_flow.Notice, "off is normal: no error line");
        }

        // AC32: 5.0 s ± 0.1 s by the clock at 60 fps, also with one 100 ms hitch.
        [TestCase(false)]
        [TestCase(true)]
        public void FiveSeconds_At60fps_WithOptionalHitch(bool hitch)
        {
            Build(enabled: true);
            Eliminate();
            Assert.AreEqual(MatchOverlayPhase.Killcam, _flow.Phase);
            Assert.AreEqual(KillcamSession.SessionState.Playing, _killcam.State, "full data is taken in the same call");
            var start = _killcam.PlayStartedAt;
            Assert.AreEqual("Killcam 5,0 s", KillcamSession.Countdown(_killcam.Remaining));
            Frames(108);
            Assert.AreEqual("Killcam 3,2 s", KillcamSession.Countdown(_killcam.Remaining));
            if (hitch) Frames(1, 0.1);
            RunUntilEliminated();
            Assert.AreEqual(MatchOverlayPhase.Eliminated, _flow.Phase);
            Assert.AreEqual(5.0, _killcam.EndedAt - start, 0.1, "played 5.0 ± 0.1 s");
            Assert.IsFalse(_killcam.Skipped);
        }

        // AC33
        [Test]
        public void SkipEndsOnce_EliminatedSameCall()
        {
            Build(enabled: true);
            Eliminate();
            Frames(30);
            Assert.IsTrue(_flow.SkipKillcam());
            Assert.AreEqual(MatchOverlayPhase.Eliminated, _flow.Phase);
            Assert.IsTrue(_killcam.Skipped);
            Assert.IsFalse(_flow.SkipKillcam(), "no replay after skipping");
            Frames(400);
            Assert.AreEqual(MatchOverlayPhase.Eliminated, _flow.Phase);
            Assert.AreEqual(1, _replay.RequestCount);
        }

        // AC34: the view centre is the killer's replay position (≤ 0.1 cell), interpolated between frames.
        [Test]
        public void CenterFollowsKiller()
        {
            Build(enabled: true);
            Eliminate();
            for (var i = 0; i < 200; i++)
            {
                Frames(1);
                if (_flow.Phase != MatchOverlayPhase.Killcam) break;
                Assert.IsTrue(_killcam.TryGetKillerPosition(out var x, out var y));
                var expectedX = MockKillcamReplaySource.KillerStartX + MockKillcamReplaySource.KillerSpeed * _killcam.Elapsed;
                Assert.AreEqual(expectedX, x, 0.1f);
                Assert.AreEqual(MockKillcamReplaySource.KillerY, y, 0.1f);
            }
            Assert.AreEqual(MockEliminationSource.KillerNetId, _killcam.LastRequest.KillerNetId);
        }

        // AC35: each fallback → notice, then the eliminated screen ≤ 1.5 s after the elimination.
        [TestCase(MockKillcamReplaySource.Case.None, KillcamUnavailableReason.NoData)]
        [TestCase(MockKillcamReplaySource.Case.Short, KillcamUnavailableReason.TooShort)]
        [TestCase(MockKillcamReplaySource.Case.Slow, KillcamUnavailableReason.Timeout)]
        [TestCase(MockKillcamReplaySource.Case.WrongMatch, KillcamUnavailableReason.WrongMatch)]
        [TestCase(MockKillcamReplaySource.Case.WrongPlayer, KillcamUnavailableReason.WrongPlayer)]
        public void Fallback(MockKillcamReplaySource.Case mode, KillcamUnavailableReason expected)
        {
            Build(enabled: true, mode);
            Eliminate();
            var took = RunUntilEliminated();
            Assert.AreEqual(MatchOverlayPhase.Eliminated, _flow.Phase);
            Assert.AreEqual(expected, _killcam.Reason);
            Assert.LessOrEqual(took, 1.5, "eliminated screen ≤ 1.5 s after the elimination");
            StringAssert.StartsWith("Killcam không khả dụng: ", _flow.Notice);
            Assert.AreEqual(KillcamSession.ReasonText(expected), _flow.Notice.Substring("Killcam không khả dụng: ".Length));
            Frames(185);
            Assert.IsNull(_flow.Notice, "notice gone after 3 s");
        }

        [TestCase(EliminationCause.Zone)]
        [TestCase(EliminationCause.LeftMatch)]
        public void Fallback_UnknownKiller_NoRequest(EliminationCause cause)
        {
            Build(enabled: true);
            Eliminate(cause);
            Assert.AreEqual(MatchOverlayPhase.Eliminated, _flow.Phase);
            Assert.AreEqual(KillcamUnavailableReason.UnknownKiller, _killcam.Reason);
            Assert.AreEqual(0, _replay.RequestCount);
            Assert.AreEqual("Killcam không khả dụng: không rõ kẻ hạ", _flow.Notice);
        }

        // AC36
        [Test]
        public void LateDataDropped_AfterTimeoutOrPhaseChange()
        {
            Build(enabled: true, MockKillcamReplaySource.Case.Slow);
            Eliminate();
            RunUntilEliminated();
            Assert.AreEqual(KillcamUnavailableReason.Timeout, _killcam.Reason);
            Assert.GreaterOrEqual(_replay.CancelCount, 1);
            Frames(60);
            Assert.IsFalse(_replay.TryTake(out _, out _), "the late answer was cancelled at the source");
            Assert.AreEqual(MatchOverlayPhase.Eliminated, _flow.Phase, "never pulled back into the killcam");
            _flow.Spectate();
            Frames(60);
            Assert.AreEqual(MatchOverlayPhase.Spectating, _flow.Phase);
            Assert.AreEqual(1, _replay.RequestCount);
        }

        // AC37
        [Test]
        public void MatchEndDuringKillcam_StopsAndHandsOff()
        {
            Build(enabled: true);
            Eliminate();
            Frames(60);
            _spectator.EndMatch();
            Assert.AreEqual(MatchOverlayPhase.HandedOff, _flow.Phase);
            Assert.AreEqual(KillcamUnavailableReason.MatchEnded, _killcam.Reason);
            Assert.IsNull(_killcam.Clip, "no frame after the hand-off");
            Assert.AreEqual(0, _replay.HeldFrames);
        }

        // AC40 (UI-03 part): the request names only this match, ourselves and the killer the server gave.
        [Test]
        public void KillcamRequestContainsOnlyOwnIds()
        {
            Build(enabled: true);
            Eliminate();
            var r = _replay.LastRequest;
            Assert.AreEqual(Match, r.MatchId);
            Assert.AreEqual(_spectator.LocalNetId, r.VictimNetId);
            Assert.AreEqual(MockEliminationSource.KillerNetId, r.KillerNetId);
            Assert.AreEqual(150, r.FrameCount);
        }

        // AC42 (UI-03 part): leaving the match frees the replay; the next match starts clean.
        [Test]
        public void ReleaseOnLeave_TwoMatchesInARow()
        {
            Build(enabled: true);
            Eliminate();
            Frames(30);
            Assert.AreEqual(150, _replay.HeldFrames);
            _bus.Publish(new AppScreenChanged(AppScreen.InMatch, AppScreen.MainMenu, null));
            Assert.AreEqual(0, _replay.HeldFrames);
            Assert.IsNull(_killcam.Clip);
            _bus.Publish(new AppScreenChanged(AppScreen.MainMenu, AppScreen.InMatch, null));
            Assert.AreEqual(MatchOverlayPhase.Playing, _flow.Phase);
            Assert.AreEqual(0, _flow.Info.Rank, "no data from the previous match");
            _elimination.Reset();
            Eliminate();
            Assert.AreEqual(MatchOverlayPhase.Killcam, _flow.Phase);
            Assert.AreEqual(2, _replay.RequestCount);
        }

        // AC41 (UI-03 part): only the last 5 s of a longer clip are shown.
        [Test]
        public void LongerClip_OnlyLastWindowPlayed()
        {
            var clock = new FakeClock();
            var source = new FixedClipSource(180);
            var session = new KillcamSession(clock.Read, new KillcamOptions(true, 5f, 1f), source);
            var info = MockEliminationSource.Sample(EliminationCause.Player, Match);
            Assert.IsTrue(session.Begin(info, 0));
            session.Tick();
            Assert.AreEqual(KillcamSession.SessionState.Playing, session.State);
            Assert.AreEqual(30f, session.Frame, 1e-4f, "starts 150 frames before the end");
            clock.Advance(4.99);
            session.Tick();
            Assert.LessOrEqual(session.Frame, 179f);
        }

        private sealed class FixedClipSource : IKillcamReplaySource
        {
            private readonly KillcamClip _clip;
            private bool _pending;
            public FixedClipSource(int frames) => _clip = new KillcamClip(frames) { FrameCount = frames };
            public void Request(in KillcamRequest request)
            {
                _pending = true;
                _clip.MatchId = request.MatchId;
                _clip.VictimNetId = request.VictimNetId;
                for (var f = 0; f < _clip.FrameCount; f++) _clip.Set(f, request.KillerNetId, f, 0f, 50);
            }
            public bool TryTake(out KillcamClip clip, out KillcamUnavailableReason reason)
            {
                clip = _pending ? _clip : null;
                reason = KillcamUnavailableReason.None;
                var answered = _pending;
                _pending = false;
                return answered;
            }
            public void Cancel() => _pending = false;
            public void Release() { }
        }
    }

    public sealed class UnavailableKillcamReplaySourceTests
    {
        [Test]
        public void AnswersNoDataAtOnce()
        {
            var source = new UnavailableKillcamReplaySource();
            Assert.IsFalse(source.TryTake(out _, out _));
            source.Request(new KillcamRequest(Guid.NewGuid(), 0, 1, 10, 150));
            Assert.IsTrue(source.TryTake(out var clip, out var reason));
            Assert.IsNull(clip);
            Assert.AreEqual(KillcamUnavailableReason.NoData, reason);
        }
    }
}
