using System;
using System.Collections.Generic;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.MatchUi;
using JungleMaze.App.MatchUi.Mocks;
using JungleMaze.App.Flow;
using JungleMaze.App.Ports;
using JungleMaze.Domain.MatchUi;
using JungleMaze.Domain.Player;
using NUnit.Framework;

namespace JungleMaze.Tests.MatchUi
{
    public sealed class EliminationTextTests
    {
        private static readonly Guid Match = Guid.NewGuid();

        // AC19: one case per cause.
        [Test]
        public void ByPlayer_ShowsNameWeaponDistance()
        {
            var info = MockEliminationSource.Sample(EliminationCause.Player, Match);
            Assert.AreEqual("#4 / 10", EliminationText.Rank(info));
            Assert.AreEqual("Bị Hào hạ bằng Nỏ · cách 6 ô", EliminationText.Cause(info));
            Assert.AreEqual("Hạ gục: 2", EliminationText.Kills(info));
            Assert.AreEqual("Sống sót: 05:12", EliminationText.Survival(info));
        }

        [TestCase(EliminationCause.Zone, "Bị bo thu hẹp loại")]
        [TestCase(EliminationCause.LeftMatch, "Bạn đã rời trận")]
        [TestCase(EliminationCause.Disconnected, "Mất kết nối quá lâu")]
        [TestCase(EliminationCause.Unknown, "Không rõ nguyên nhân")]
        public void OtherCauses_HaveTheirLine(EliminationCause cause, string expected)
        {
            var info = MockEliminationSource.Sample(cause, Match);
            Assert.AreEqual(expected, EliminationText.Cause(info));
            Assert.IsNotNull(EliminationText.Rank(info));
            Assert.IsNotNull(EliminationText.Survival(info));
        }

        [Test]
        public void ByPlayer_WithoutDetails_StillReadable()
        {
            var info = new EliminationInfo(Match, 1, 0, 0, EliminationCause.Player, 3, null, null, float.NaN, float.NaN, -1);
            Assert.AreEqual("Bị người chơi khác hạ", EliminationText.Cause(info));
        }

        // AC20: only rank and survival → kills hidden, nothing prints 0/null/dash.
        [Test]
        public void MissingFieldsHidden()
        {
            var info = new EliminationInfo(Match, 1, 5, 10, EliminationCause.Unknown, EliminationInfo.NoNetId, null, null, float.NaN, 75f, -1);
            Assert.AreEqual("#5 / 10", EliminationText.Rank(info));
            Assert.AreEqual("Sống sót: 01:15", EliminationText.Survival(info));
            Assert.IsNull(EliminationText.Kills(info));

            var empty = EliminationInfo.Unknown(Match, 1);
            Assert.IsNull(EliminationText.Rank(empty));
            Assert.IsNull(EliminationText.Kills(empty));
            Assert.IsNull(EliminationText.Survival(empty));
            foreach (var line in new[] { EliminationText.Cause(empty), EliminationText.Cause(info) })
            {
                StringAssert.DoesNotContain("null", line);
                StringAssert.DoesNotContain("—", line);
                StringAssert.DoesNotContain(" 0", line);
            }
        }

        [TestCase(0f, "00:00")]
        [TestCase(59.9f, "00:59")]
        [TestCase(600f, "10:00")]
        [TestCase(-5f, "00:00")]
        public void Clock_IsMinutesSeconds(float seconds, string expected) => Assert.AreEqual(expected, EliminationText.Clock(seconds));
    }

    public sealed class EliminationSourceTests
    {
        // AC18 (source side) and AC22.
        [Test]
        public void SharedRankShownAsGiven_OnlyForLocalPlayer()
        {
            using (var h = new LiveMatchHarness())
            {
                var source = new MatchSessionEliminationSource(h.Match, h.Network);
                source.Initialize();
                var events = new List<EliminationInfo>();
                source.LocalEliminated += events.Add;
                h.Start(ownNetId: 1, count: 6);
                source.Tick(0.016f);

                h.Entity(4, MatchEntityEvent.Eliminated);
                source.Tick(0.016f);
                Assert.IsFalse(source.IsLocalEliminated, "another player's elimination is ignored");
                Assert.AreEqual(0, events.Count);

                h.Entity(1, MatchEntityEvent.Eliminated);
                Assert.IsTrue(source.IsLocalEliminated);
                Assert.AreEqual(1, events.Count);
                Assert.IsFalse(source.Current.HasRank, "roster has not arrived yet");
                Assert.AreEqual(h.MatchId, source.Current.MatchId);
                Assert.IsFalse(source.Current.HasKills, "Kills are always 0 from the server: hidden (T-6)");

                var updates = 0;
                source.Updated += _ => updates++;
                // Players 1 and 4 out on the same tick: both rank 5 (tie decided by the server).
                h.Roster(6, id => id == 1 || id == 4 ? (byte)5 : (byte)0, id => id == 1 ? 1800u : 0u);
                source.Tick(0.016f);
                Assert.AreEqual(1, updates);
                Assert.AreEqual(5, source.Current.Rank);
                Assert.AreEqual(6, source.Current.StartCount, "N is the start roster, not maxPlayers");
                Assert.AreEqual(60f, source.Current.SurvivalSeconds, 1e-4f);
                Assert.AreEqual("#5 / 6", EliminationText.Rank(source.Current));

                h.Entity(1, MatchEntityEvent.Eliminated);
                Assert.AreEqual(1, events.Count, "once per match");
                source.Dispose();
            }
        }

        [Test]
        public void DeadSnapshot_AlsoCounts_AndKillerFromPlayerDied()
        {
            using (var h = new LiveMatchHarness())
            {
                var source = new MatchSessionEliminationSource(h.Match, h.Network);
                source.Initialize();
                h.Start(ownNetId: 0, count: 3);
                source.Tick(0.016f);
                h.Network.Push(new EventPacket { EventType = GameEventType.PlayerDied, SourceNetId = 2, TargetNetId = 0 });
                h.Snapshot(new byte[] { 1, 2 }, new byte[] { 0 });
                source.Tick(0.016f);
                Assert.IsTrue(source.IsLocalEliminated);
                Assert.AreEqual(EliminationCause.Player, source.Current.Cause);
                Assert.AreEqual(2, source.Current.KillerNetId);
                Assert.AreEqual("P2", source.Current.KillerName);
                source.Dispose();
            }
        }
    }

    public sealed class SpectatorSourceTests
    {
        // AC27
        [Test]
        public void RowRemovedNextTick_CountMatches_NoLateJoiners()
        {
            using (var h = new LiveMatchHarness())
            {
                var source = new MatchSessionSpectatorSource(h.Match, h.Network, null, 100);
                source.Initialize();
                var gone = new List<byte>();
                source.PlayerEliminated += gone.Add;
                h.Start(ownNetId: 0, count: 8);
                source.Tick(0.016f);
                Assert.AreEqual(8, source.StartCount);
                Assert.AreEqual(7, source.AliveCount, "self excluded");
                for (var i = 0; i < source.AliveCount; i++) Assert.AreNotEqual(0, source.GetAlive(i).NetId);
                Assert.AreEqual(1, source.GetAlive(0).NetId, "ordered by NetId");
                Assert.AreEqual("P1", source.GetAlive(0).Name);
                Assert.IsTrue(source.GetAlive(0).HasHp);

                h.Entity(5, MatchEntityEvent.Eliminated);
                Assert.AreEqual(6, source.AliveCount, "row gone in the same call as the event");
                CollectionAssert.AreEqual(new byte[] { 5 }, gone);
                Assert.IsFalse(source.TryGetEntry(5, out _));

                h.Snapshot(new byte[] { 0, 1, 2, 3, 4, 6 }, new byte[] { 7 });
                source.Tick(0.016f);
                Assert.AreEqual(5, source.AliveCount, "dead in the snapshot");
                CollectionAssert.AreEqual(new byte[] { 5, 7 }, gone);

                // A late joiner (NetId 8, not in the start roster) is never listed.
                h.Roster(9);
                h.Snapshot(new byte[] { 0, 1, 2, 3, 4, 6, 8 });
                source.Tick(0.016f);
                Assert.AreEqual(5, source.AliveCount);
                Assert.IsFalse(source.TryGetEntry(8, out _));
                source.Dispose();
            }
        }

        [Test]
        public void MatchEnded_FromEvent()
        {
            using (var h = new LiveMatchHarness())
            {
                var source = new MatchSessionSpectatorSource(h.Match, h.Network, null, 100);
                source.Initialize();
                h.Start(ownNetId: 0, count: 3);
                source.Tick(0.016f);
                Assert.IsFalse(source.MatchEnded);
                h.Network.Push(new EventPacket { EventType = GameEventType.MatchEnded });
                Assert.IsTrue(source.MatchEnded);
                source.Dispose();
            }
        }

        [Test]
        public void MatchEnded_FromEndingPhase()
        {
            using (var h = new LiveMatchHarness())
            {
                var source = new MatchSessionSpectatorSource(h.Match, h.Network, null, 100);
                source.Initialize();
                h.Start(ownNetId: 0, count: 3);
                source.Tick(0.016f);
                h.Network.Push(new MatchPhasePacket { MatchId = h.MatchId, Phase = MatchPhase.Ending, ServerTick = h.Tick, MaxPlayers = 10, MinPlayers = 1 });
                Assert.IsFalse(source.MatchEnded, "seen on the next tick");
                source.Tick(0.016f);
                Assert.IsTrue(source.MatchEnded);
                source.Dispose();
            }
        }

        [Test]
        public void MatchEnded_FromResult()
        {
            using (var h = new LiveMatchHarness())
            {
                var source = new MatchSessionSpectatorSource(h.Match, h.Network, null, 100);
                source.Initialize();
                h.Start(ownNetId: 0, count: 3);
                source.Tick(0.016f);
                h.Network.Push(new MatchResultPacket { MatchId = h.MatchId, Participants = new MatchParticipantState[0] });
                source.Tick(0.016f);
                Assert.IsTrue(source.MatchEnded);
                source.Dispose();
            }
        }

        [Test]
        public void TryGetPosition_UsesTheInterpolatedPose()
        {
            using (var h = new LiveMatchHarness())
            {
                var source = new MatchSessionSpectatorSource(h.Match, h.Network, new Poses(), 100);
                Assert.IsTrue(source.TryGetPosition(2, out var x, out var y));
                Assert.AreEqual(12.5f, x);
                Assert.AreEqual(7.25f, y);
                Assert.IsFalse(source.TryGetPosition(4, out _, out _));
            }
        }

        // Gợi ý 1: a new match starts clean, not with "everybody from the old match was eliminated".
        [Test]
        public void NewMatch_DoesNotReportOldPlayersAsEliminated()
        {
            using (var h = new LiveMatchHarness())
            {
                var source = new MatchSessionSpectatorSource(h.Match, h.Network, null, 100);
                source.Initialize();
                h.Start(ownNetId: 0, count: 4);
                source.Tick(0.016f);
                Assert.AreEqual(3, source.AliveCount);
                var gone = new List<byte>();
                source.PlayerEliminated += gone.Add;
                h.Network.Push(new MatchPhasePacket { MatchId = Guid.NewGuid(), Phase = MatchPhase.Lobby, ServerTick = 1, MaxPlayers = 10, MinPlayers = 1 });
                source.Tick(0.016f);
                Assert.AreEqual(0, source.AliveCount);
                CollectionAssert.IsEmpty(gone);
                source.Dispose();
            }
        }

        private sealed class Poses : IPlayerPoseSource
        {
            public bool TryGetPose(byte netId, out PlayerPose pose)
            {
                pose = new PlayerPose { X = 12.5f, Y = 7.25f };
                return netId == 2;
            }
        }

        // Gợi ý 4: the killcam clip runs at the server tick rate.
        [Test]
        public void KillcamFramesPerSecond_IsTheProtocolTickRate() =>
            Assert.AreEqual(GameplayProtocol.TickRate, KillcamClip.FramesPerSecond);
    }

    /// <summary>
    /// AC30, measured on the real sender: the live <c>MatchSession.Tick</c> sends input while we are alive (control),
    /// and nothing while we are out and spectating, even if a move/attack intent is left behind and targets change.
    /// The leave request is an event; only the bridge (composition root) sends PlayerLeave.
    /// </summary>
    public sealed class SpectatorNoGameplayPacketsTests
    {
        [Test]
        public void WhileSpectating_MatchTickSendsNothing_LeaveIsAnEvent()
        {
            using (var h = new LiveMatchHarness())
            {
                var spectator = new MatchSessionSpectatorSource(h.Match, h.Network, null, 100);
                var elimination = new MatchSessionEliminationSource(h.Match, h.Network);
                spectator.Initialize();
                elimination.Initialize();
                var flow = new MatchOverlayFlow(h.Bus, () => 0d, elimination, spectator, AppScreen.InMatch);
                var menu = new MatchMenuController(h.Bus, () => 0d, () => h.MatchId, AppScreen.InMatch);
                h.Start(ownNetId: 0, count: 4);
                var moving = new PlayerIntent { MoveX = 1f, MoveY = 0f, Attack = true };

                // Control: alive, the same loop sends input packets.
                h.Match.SetIntent(moving);
                var aliveBefore = h.Network.Sent;
                for (var i = 0; i < 30; i++) h.Match.Tick(1f / 60f);
                Assert.Greater(h.Network.Sent, aliveBefore, "control: the sender is observed");

                h.Snapshot(new byte[] { 1, 2, 3 }, new byte[] { 0 });
                h.Entity(0, MatchEntityEvent.Eliminated);
                spectator.Tick(1f / 60f);
                elimination.Tick(1f / 60f);
                Assert.AreEqual(MatchOverlayPhase.Eliminated, flow.Phase);
                Assert.IsTrue(flow.Spectate());

                var sentBefore = h.Network.Sent;
                var inputsBefore = h.Match.InputsSent;
                h.Match.SetIntent(moving);
                for (var i = 0; i < 300; i++)
                {
                    if (i % 30 == 0) flow.Targets.Next();
                    h.Match.Tick(1f / 60f);
                    spectator.Tick(1f / 60f);
                    elimination.Tick(1f / 60f);
                    flow.Tick(1f / 60f);
                    menu.Tick(1f / 60f);
                }
                Assert.AreEqual(MatchOverlayPhase.Spectating, flow.Phase);
                Assert.AreEqual(sentBefore, h.Network.Sent, "no packet while spectating");
                Assert.AreEqual(inputsBefore, h.Match.InputsSent);

                var leaves = new Recorder<LeaveMatchRequested>();
                h.Bus.Subscribe(leaves.Handler);
                menu.LeaveWithoutConfirm(LeaveMatchSource.Spectator);
                Assert.AreEqual(1, leaves.Count);
                Assert.AreEqual(sentBefore, h.Network.Sent, "the leave request is an event; the bridge sends PlayerLeave");
                menu.Dispose();
                flow.Dispose();
                spectator.Dispose();
                elimination.Dispose();
            }
        }
    }
}
