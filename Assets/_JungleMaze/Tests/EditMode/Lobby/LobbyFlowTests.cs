using System;
using System.Linq;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Lobby;
using JungleMaze.App.MatchUi;
using JungleMaze.App.Ports;
using JungleMaze.Domain.Lobby;
using JungleMaze.Domain.Network;
using NUnit.Framework;

namespace JungleMaze.Tests.Lobby
{
    /// <summary>AC20–AC27, AC32–AC33, AC38–AC39, AC42 on the screen flow, all with mocks and a fake clock.</summary>
    public sealed class LobbyFlowTests
    {
        [Test]
        public void Lobby_CountdownFromServer_NoTickNoChange_CancelHides()
        {
            using var rig = new LobbyRig();
            rig.JoinLobby(countdown: 15);
            Assert.AreEqual(LobbyScreen.Lobby, rig.Flow.Screen);
            Assert.IsTrue(rig.Flow.HasCountdown);
            Assert.AreEqual(15, rig.Flow.CountdownSeconds);
            rig.Lobby.ServerTick += 31;
            Assert.AreEqual(14, rig.Flow.CountdownSeconds, "ceil((end − tick) / 30)");
            rig.Clock.Advance(3);
            rig.Tick(180);
            Assert.AreEqual(14, rig.Flow.CountdownSeconds, "3 s without a server tick: the number does not move");
            rig.Phase(MatchPhase.Waiting);
            Assert.IsFalse(rig.Flow.HasCountdown, "server cancelled the countdown");
            Assert.AreEqual("Chờ đủ 6 người", rig.Flow.LobbyStatusText);
        }

        [Test]
        public void Lobby_MinimumFromState()
        {
            using var rig = new LobbyRig();
            rig.Lobby.MinPlayers = 2; // profile test
            rig.JoinLobby();
            rig.Phase(MatchPhase.Waiting);
            Assert.AreEqual("Chờ đủ 2 người", rig.Flow.LobbyStatusText);
            Assert.AreEqual("Tự bắt đầu sau 15 s khi đủ 2 người", rig.Flow.AutoStartText);
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void ReadyStartFlags(bool ready, bool start)
        {
            using var rig = new LobbyRig(o => { o.ReadyEnabled = ready; o.StartEnabled = start; });
            rig.JoinLobby();
            for (var i = 0; i < 5; i++) rig.Flow.Ready();
            Assert.AreEqual(ready ? 1 : 0, rig.Commands.Readies, "exactly one Ready when on, none when off");
            Assert.AreEqual(MatchPhase.Lobby, rig.Lobby.Phase, "UI never changes the phase");
            Assert.AreEqual(0, rig.Commands.Leaves + rig.Commands.Replays + rig.Commands.Resumes, "start sends nothing (T-6)");
        }

        [Test]
        public void LeaveLobby_OneLeave_HomeKeepsForm()
        {
            using var rig = new LobbyRig();
            rig.JoinLobby("ABCDEF");
            rig.Flow.GoHome();
            rig.Flow.GoHome();
            Assert.AreEqual(LobbyScreen.MainMenu, rig.Flow.Screen);
            Assert.AreEqual(1, rig.Commands.Leaves);
            Assert.AreEqual("ABCDEF", rig.Join.RoomCode);
            Assert.AreEqual("Huy", rig.Join.DisplayName);
            rig.Tick(5);
            Assert.AreEqual(LobbyScreen.MainMenu, rig.Flow.Screen, "still connected for a frame: no bounce back to Lobby");
            rig.Connection.Set(NetworkState.Disconnected, DisconnectReason.UserRequested);
            rig.Tick();
            Assert.AreEqual(LobbyScreen.MainMenu, rig.Flow.Screen, "no countdown left on the menu");
        }

        [Test]
        public void Loading_ShowsAndBackToWaiting()
        {
            using var rig = new LobbyRig();
            rig.JoinLobby();
            rig.Phase(MatchPhase.Loading, 30);
            Assert.AreEqual(LobbyScreen.Loading, rig.Flow.Screen);
            Assert.AreEqual(30, rig.Flow.CountdownSeconds);
            rig.Phase(MatchPhase.Waiting);
            Assert.AreEqual(LobbyScreen.Lobby, rig.Flow.Screen);
            Assert.AreEqual(LobbyScreenRules.NotEnoughPlayers, rig.Flow.Notice);
        }

        [Test]
        public void LoadError_MapData()
        {
            using var rig = new LobbyRig();
            rig.JoinLobby();
            rig.Phase(MatchPhase.Loading, 30);
            rig.Lobby.LoadError = "ConfigHash khác";
            rig.Tick();
            Assert.AreEqual(LobbyScreen.LoadError, rig.Flow.Screen);
            StringAssert.Contains("ConfigHash khác", rig.Flow.LoadErrorText);
            HomeWithinOneCall(rig);
        }

        [Test]
        public void LoadError_Disconnect()
        {
            using var rig = new LobbyRig();
            rig.JoinLobby();
            rig.Phase(MatchPhase.Loading, 30);
            rig.Connection.Set(NetworkState.Disconnected, DisconnectReason.ConnectionLost);
            rig.Tick();
            Assert.AreEqual(LobbyScreen.LoadError, rig.Flow.Screen);
            Assert.AreEqual(LobbyScreenRules.LoadLost, rig.Flow.LoadErrorText);
            HomeWithinOneCall(rig);
        }

        [Test]
        public void LoadError_Kicked()
        {
            using var rig = new LobbyRig();
            rig.JoinLobby();
            rig.Phase(MatchPhase.Loading, 30);
            rig.Connection.Set(NetworkState.Disconnected, DisconnectReason.ServerClosed);
            rig.Tick();
            Assert.AreEqual(LobbyScreenRules.LoadKicked, rig.Flow.LoadErrorText);
        }

        [Test]
        public void LoadError_Stuck35s()
        {
            using var rig = new LobbyRig();
            rig.JoinLobby();
            rig.Phase(MatchPhase.Loading, 30);
            rig.Clock.Advance(34.9);
            rig.Tick();
            Assert.AreEqual(LobbyScreen.Loading, rig.Flow.Screen);
            rig.Clock.Advance(0.2);
            rig.Tick();
            Assert.AreEqual(LobbyScreen.LoadError, rig.Flow.Screen);
            Assert.AreEqual(LobbyScreenRules.LoadStuck, rig.Flow.LoadErrorText);
        }

        private static void HomeWithinOneCall(LobbyRig rig)
        {
            rig.Flow.GoHome();
            Assert.AreEqual(LobbyScreen.MainMenu, rig.Flow.Screen);
            Assert.AreEqual(1, rig.Commands.Leaves);
        }

        [Test]
        public void Replay_OneAck_NewMatchClearsOld()
        {
            using var rig = new LobbyRig();
            rig.InGame();
            Assert.AreEqual(LobbyScreen.InGame, rig.Flow.Screen);
            rig.Results.Current = rig.Result(MatchEndReason.LastAlive, 0, (0, 1, "Huy"), (1, 2, "Mai"));
            rig.Phase(MatchPhase.Ending, 10);
            Assert.AreEqual(LobbyScreen.Results, rig.Flow.Screen);
            Assert.AreEqual(ResultModel.VictoryText, rig.Flow.Result.Title);
            for (var i = 0; i < 5; i++) rig.Flow.Replay();
            Assert.AreEqual(1, rig.Commands.Replays);
            Assert.IsTrue(rig.Flow.ReplayPending);
            var old = rig.Lobby.MatchId;
            rig.Lobby.MatchId = Guid.NewGuid();
            rig.Phase(MatchPhase.Lobby, 15);
            Assert.AreEqual(LobbyScreen.Lobby, rig.Flow.Screen);
            Assert.IsFalse(rig.Flow.Result.HasData, "old result cleared");
            Assert.IsFalse(rig.Flow.ReplayPending);
            rig.Results.Current = null;
            rig.Phase(MatchPhase.Loading, 30);
            rig.Phase(MatchPhase.InGame);
            rig.Phase(MatchPhase.Ending, 10);
            Assert.AreNotEqual(old, rig.Flow.Lobby.MatchId);
            Assert.IsFalse(rig.Flow.Result.HasData, "no stale rows while the new result is pending");
            rig.Flow.Replay();
            Assert.AreEqual(2, rig.Commands.Replays, "a new match opens the gate again");
        }

        [Test]
        public void EndingTimeout_FollowsPhase_HomeAlways()
        {
            using var rig = new LobbyRig();
            rig.InGame();
            rig.Results.Current = rig.Result(MatchEndReason.LastAlive, 1, (0, 2, "Huy"), (1, 1, "Mai"));
            rig.Phase(MatchPhase.Ending, 10);
            rig.Phase(MatchPhase.Cleanup);
            Assert.AreEqual(LobbyScreen.Lobby, rig.Flow.Screen, "no hang on Results");
            rig.Phase(MatchPhase.Ending, 10);
            rig.Flow.GoHome();
            Assert.AreEqual(LobbyScreen.MainMenu, rig.Flow.Screen);
        }

        [Test]
        public void Results_ShownOncePerMatch()
        {
            using var rig = new LobbyRig();
            rig.InGame();
            rig.Results.Current = rig.Result(MatchEndReason.LastAlive, 1, (0, 2, "Huy"), (1, 1, "Mai"));
            rig.Tick(); // result before Ending (spectating, AC34)
            rig.Phase(MatchPhase.Ending, 10);
            rig.Tick(30);
            Assert.AreEqual(LobbyScreen.Results, rig.Flow.Screen);
            Assert.AreEqual(1, rig.Flow.ResultsShown);
            Assert.AreEqual("HẠNG #2", rig.Flow.Result.Title);
        }

        [Test]
        public void LeaveMatchRequested_OneLeave_OneSubscriber()
        {
            using var rig = new LobbyRig();
            Assert.AreEqual(1, rig.Bus.HandlerCount<LeaveMatchRequested>(), "UI-02 is the only listener (bridge removed)");
            rig.InGame();
            rig.Bus.Publish(new LeaveMatchRequested(rig.Lobby.MatchId, LeaveMatchSource.MatchMenu));
            rig.Bus.Publish(new LeaveMatchRequested(rig.Lobby.MatchId, LeaveMatchSource.MatchMenu));
            Assert.AreEqual(1, rig.Commands.Leaves);
            Assert.AreEqual(LobbyScreen.MainMenu, rig.Flow.Screen);
            rig.Flow.Dispose();
            Assert.AreEqual(0, rig.Bus.HandlerCount<LeaveMatchRequested>());
        }

        [Test]
        public void BridgeFile_IsGone()
        {
            Assert.IsFalse(System.IO.File.Exists("Assets/_JungleMaze/Scripts/Bootstrap/MatchOverlay/LeaveMatchBridge.cs"));
            StringAssert.DoesNotContain("LeaveMatchBridge",
                System.IO.File.ReadAllText("Assets/_JungleMaze/Scripts/Bootstrap/MatchOverlay/Bootstrap.MatchOverlay.cs"));
        }

        [Test]
        public void Reconnect_Enabled_Success()
        {
            using var rig = new LobbyRig();
            rig.InGame();
            rig.Connection.CanResume = true;
            rig.Connection.Set(NetworkState.Disconnected, DisconnectReason.ConnectionLost);
            rig.Tick();
            Assert.AreEqual(LobbyScreen.Reconnecting, rig.Flow.Screen);
            Assert.AreEqual(15, rig.Flow.Reconnect.SecondsLeft);
            Assert.AreEqual(1, rig.Commands.Resumes);
            rig.Connection.Set(NetworkState.Connecting);
            rig.Clock.Advance(2);
            rig.Tick();
            Assert.AreEqual(13, rig.Flow.Reconnect.SecondsLeft);
            rig.Connection.Set(NetworkState.Joined);
            rig.Tick();
            Assert.AreEqual(LobbyScreen.InGame, rig.Flow.Screen);
            Assert.AreEqual(1, rig.Commands.Resumes);
        }

        [Test]
        public void Reconnect_Enabled_Timeout()
        {
            using var rig = new LobbyRig();
            rig.InGame();
            rig.Connection.CanResume = true;
            rig.Connection.Set(NetworkState.Disconnected, DisconnectReason.ConnectionLost);
            rig.Tick();
            rig.Clock.Advance(15.1);
            rig.Tick();
            Assert.AreEqual(LobbyScreen.Disconnected, rig.Flow.Screen);
            Assert.AreEqual(ReconnectController.FailedText, rig.Flow.Notice);
            Assert.AreEqual(1, rig.Commands.Resumes);
        }

        [Test]
        public void Reconnect_Enabled_Rejected()
        {
            using var rig = new LobbyRig();
            rig.InGame();
            rig.Connection.CanResume = true;
            rig.Connection.Set(NetworkState.Disconnected, DisconnectReason.ConnectionLost);
            rig.Tick();
            rig.Connection.Set(NetworkState.Connecting);
            rig.Tick();
            rig.Connection.Set(NetworkState.Failed, DisconnectReason.InvalidToken);
            rig.Tick();
            Assert.AreEqual(LobbyScreen.Disconnected, rig.Flow.Screen);
            Assert.AreEqual(ReconnectController.FailedText, rig.Flow.Notice);
        }

        [Test]
        public void Reconnect_Disabled_NoResume()
        {
            using var rig = new LobbyRig(o => o.ReconnectEnabled = false);
            rig.InGame();
            rig.Connection.CanResume = true;
            rig.Connection.Set(NetworkState.Disconnected, DisconnectReason.ConnectionLost);
            rig.Tick();
            Assert.AreEqual(LobbyScreen.Disconnected, rig.Flow.Screen);
            Assert.AreEqual(ReconnectController.LostText, rig.Flow.Notice);
            Assert.AreEqual(0, rig.Commands.Resumes);
        }

        [TestCase(LobbyScreen.Lobby)]
        [TestCase(LobbyScreen.Results)]
        public void LostOnConnectedScreen_DisconnectedWithHome(LobbyScreen at)
        {
            using var rig = new LobbyRig();
            rig.JoinLobby();
            if (at == LobbyScreen.Results)
            {
                rig.Phase(MatchPhase.Loading, 30);
                rig.Phase(MatchPhase.InGame);
                rig.Phase(MatchPhase.Ending, 10);
            }
            Assert.AreEqual(at, rig.Flow.Screen);
            rig.Connection.Set(NetworkState.Disconnected, DisconnectReason.ServerClosed);
            rig.Tick();
            Assert.AreEqual(LobbyScreen.Disconnected, rig.Flow.Screen);
            rig.Flow.GoHome();
            rig.Tick();
            Assert.AreEqual(LobbyScreen.MainMenu, rig.Flow.Screen);
        }

        /// <summary>
        /// AC27: the flow joins through the live adapters, reaches Loading and stays there for 2 s of ticks; UI-02 adds no
        /// packet (no LoadAck, nothing else). LoadAck belongs to MatchDemoView after Verify.
        /// </summary>
        [Test]
        public void LoadAckCount_Unchanged_ThroughLoading()
        {
            var bus = new JungleMaze.Core.Events.EventBus();
            var network = new JungleMaze.Tests.App.FakeNetwork(bus);
            var match = new JungleMaze.App.Match.MatchSession(network, network, network, bus, new JungleMaze.Domain.Player.PlayerRegistry());
            match.Initialize();
            var commands = new JungleMaze.App.Lobby.Adapters.SessionLobbyCommands(
                (c, n) => network.SetState(NetworkState.Joined, DisconnectReason.None), network.Disconnect, match, null);
            using var connection = new JungleMaze.App.Lobby.Adapters.NetworkConnectionStateSource(network, () => match.CanResume, bus);
            var clock = new JungleMaze.Tests.MatchUi.FakeClock();
            var flow = new LobbyFlow(new JungleMaze.App.Lobby.Adapters.MatchSessionLobbySource(match, () => "K7M2Q9"),
                new JungleMaze.App.Lobby.Adapters.MatchSessionResultSource(match), connection, commands,
                new JoinController(commands, clock.Read, null, "Huy", "K7M2Q9"), null, null, null, null, null, clock.Read);
            flow.OpenJoin();
            Assert.IsTrue(flow.SubmitJoin());
            flow.Tick(1 / 60f);
            var matchId = Guid.NewGuid();
            network.Push(new MatchPhasePacket { MatchId = matchId, Phase = MatchPhase.Lobby, ServerTick = 10, PhaseEndTick = 460, MinPlayers = 2, MaxPlayers = 10 });
            flow.Tick(1 / 60f);
            Assert.AreEqual(LobbyScreen.Lobby, flow.Screen);
            network.Push(new MatchPhasePacket { MatchId = matchId, Phase = MatchPhase.Loading, ServerTick = 460, PhaseEndTick = 1360, MinPlayers = 2, MaxPlayers = 10 });
            var before = network.Sent;
            for (var i = 0; i < 120; i++) { flow.Tick(1 / 60f); clock.Advance(1 / 60.0); }
            Assert.AreEqual(LobbyScreen.Loading, flow.Screen, "the flow really is in Loading");
            Assert.AreEqual(before, network.Sent, "no LoadAck (or any packet) from UI-02 while loading");
            Assert.IsFalse(System.IO.Directory.GetFiles("Assets/_JungleMaze/Scripts", "*.cs", System.IO.SearchOption.AllDirectories)
                .Where(f => f.Replace('\\', '/').Contains("/Lobby/"))
                .Any(f => System.IO.File.ReadAllText(f).Contains("ConfirmLoaded") || System.IO.File.ReadAllText(f).Contains("LoadAckPacket")));
            flow.Dispose();
            match.Dispose();
        }

        private static System.Collections.Generic.IEnumerable<TestCaseData> HomeMatrix()
        {
            var screens = new[] { LobbyScreen.Connecting, LobbyScreen.Lobby, LobbyScreen.Loading, LobbyScreen.InGame,
                LobbyScreen.Results, LobbyScreen.RoomManager, LobbyScreen.History };
            var errors = new[] { DisconnectReason.ConnectionLost, DisconnectReason.ConnectTimeout, DisconnectReason.ServerClosed };
            foreach (var s in screens)
            foreach (var e in errors)
                yield return new TestCaseData(s, e).SetName("FlowHomeAlways_" + s + "_" + e);
        }

        /// <summary>
        /// AC40 at flow level: each of 7 screens × 3 network failures (sudden drop, timeout, server closed) leaves a screen
        /// with "Về menu"; pressing it lands on the main menu with no live connection, no reconnect, no old notice or
        /// result, at most one leave, and the menu stays put on later ticks.
        /// </summary>
        [TestCaseSource(nameof(HomeMatrix))]
        public void HomeAlways_FlowLevel(LobbyScreen screen, DisconnectReason error)
        {
            using var rig = new LobbyRig();
            Reach(rig, screen);
            Assert.AreEqual(screen, rig.Flow.Screen, "setup");
            Fail(rig, screen, error);
            Assert.IsTrue(LobbyScreenRules.HasHome(rig.Flow.Screen), "home offered on " + rig.Flow.Screen);
            var sentBefore = rig.Commands.Leaves + rig.Commands.Cancels;
            rig.Flow.GoHome();
            Assert.AreEqual(LobbyScreen.MainMenu, rig.Flow.Screen);
            Assert.LessOrEqual(rig.Commands.Leaves + rig.Commands.Cancels - sentBefore, 1, "one leave at most");
            Assert.AreEqual(ReconnectState.Idle, rig.Flow.Reconnect.State, "no reconnect overlay left");
            Assert.IsEmpty(rig.Flow.Notice);
            Assert.IsEmpty(rig.Flow.LoadErrorText);
            Assert.IsFalse(rig.Flow.Result.HasData);
            Assert.IsFalse(rig.Flow.ReplayPending);
            Assert.IsFalse(rig.Join.Busy, "no join in flight");
            rig.Connection.Set(NetworkState.Disconnected, DisconnectReason.UserRequested); // what the leave does on a real connection
            rig.Tick(30);
            Assert.AreEqual(LobbyScreen.MainMenu, rig.Flow.Screen, "no bounce back");
        }

        private static void Reach(LobbyRig rig, LobbyScreen screen)
        {
            switch (screen)
            {
                case LobbyScreen.Connecting:
                    rig.Flow.OpenJoin();
                    rig.Join.SetCode("K7M2Q9");
                    rig.Flow.SubmitJoin();
                    rig.Connection.Set(NetworkState.Connecting);
                    rig.Tick();
                    break;
                case LobbyScreen.Lobby: rig.JoinLobby(); break;
                case LobbyScreen.Loading: rig.JoinLobby(); rig.Phase(MatchPhase.Loading, 30); break;
                case LobbyScreen.InGame: rig.InGame(); rig.Connection.CanResume = true; break;
                case LobbyScreen.Results:
                    rig.InGame();
                    rig.Results.Current = rig.Result(MatchEndReason.LastAlive, 0, (0, 1, "Huy"), (1, 2, "Mai"));
                    rig.Phase(MatchPhase.Ending, 10);
                    break;
                case LobbyScreen.RoomManager:
                    rig.Account.Username = "huycv";
                    rig.Account.Password = "secret123";
                    rig.Account.Submit();
                    rig.Flow.OpenRooms();
                    break;
                case LobbyScreen.History: rig.Flow.OpenHistory(); break;
            }
        }

        /// <summary>Network screens lose the connection with the reason; API screens get the same failure from the API.</summary>
        private static void Fail(LobbyRig rig, LobbyScreen screen, DisconnectReason error)
        {
            if (screen == LobbyScreen.RoomManager)
            {
                rig.Api.ListStatus = error == DisconnectReason.ServerClosed ? 503 : 0;
                rig.Rooms.Refresh();
                Assert.AreEqual(ListState.Unavailable, rig.Rooms.State);
                return;
            }
            if (screen == LobbyScreen.History)
            {
                rig.History.Fail = true;
                Assert.IsTrue(rig.Flow.History.Refresh().IsCompleted);
                Assert.AreEqual(ListState.Unavailable, rig.Flow.History.State);
                return;
            }
            rig.Connection.Set(error == DisconnectReason.ConnectTimeout ? NetworkState.Failed : NetworkState.Disconnected, error);
            rig.Tick();
            if (rig.Flow.Screen == LobbyScreen.Reconnecting) rig.Connection.Set(NetworkState.Connecting); // resume in flight
        }
    }

    /// <summary>AC28–AC30 on the result model.</summary>
    public sealed class ResultModelTests
    {
        private static MatchResultView Result(MatchEndReason reason, byte winner, byte own = 0) => new MatchResultView
        {
            MatchId = Guid.NewGuid(), Reason = reason, WinnerNetId = winner, OwnNetId = own, ParticipantCount = 4,
            Participants = new[]
            {
                new MatchParticipantState { NetId = 3, Rank = 3, DisplayName = "Huy", Kills = 1, SurvivalTicks = 3000 },
                new MatchParticipantState { NetId = 0, Rank = 2, DisplayName = "Huy", Kills = 0, SurvivalTicks = 30 * 125 },
                new MatchParticipantState { NetId = 1, Rank = 1, DisplayName = "Mai", Kills = 4, SurvivalTicks = 9000 },
                new MatchParticipantState { NetId = 2, Rank = 3, DisplayName = "Lan", Kills = 0, SurvivalTicks = 3000 }
            }
        };

        [TestCase(MatchEndReason.LastAlive, (byte)0, ResultHeadline.Victory, ResultModel.VictoryText)]
        [TestCase(MatchEndReason.LastAlive, (byte)1, ResultHeadline.Ranked, "HẠNG #2")]
        [TestCase(MatchEndReason.Tie, (byte)255, ResultHeadline.Tie, ResultModel.TieText)]
        [TestCase(MatchEndReason.Cancelled, (byte)255, ResultHeadline.Cancelled, ResultModel.CancelledText)]
        [TestCase(MatchEndReason.Timeout, (byte)0, ResultHeadline.Ranked, "HẠNG #2")]
        public void Headline_FromStateOnly(MatchEndReason reason, byte winner, ResultHeadline headline, string title)
        {
            var model = new ResultModel();
            model.Show(Result(reason, winner), null, false);
            Assert.AreEqual(headline, model.Headline);
            Assert.AreEqual(title, model.Title);
        }

        [Test]
        public void OwnBlock_RankOfN_Survival_HidesMissing()
        {
            var model = new ResultModel();
            model.Show(Result(MatchEndReason.LastAlive, 1), null, showKills: false);
            Assert.AreEqual("#2 / 4", model.RankText);
            Assert.AreEqual("Sống sót: 02:05", model.SurvivalText);
            Assert.IsNull(model.KillsText, "T-4: hidden, not 0");
            Assert.IsNull(model.CauseText, "no N8 data: line hidden");
            model.Show(Result(MatchEndReason.LastAlive, 1), "Bị bo thu hẹp loại", showKills: true);
            Assert.AreEqual("Bị bo thu hẹp loại", model.CauseText);
            Assert.AreEqual("Hạ gục: 0", model.KillsText);
        }

        [Test]
        public void Table_SortedByServerRank_TiesAndDuplicateNamesKept()
        {
            var model = new ResultModel();
            model.Show(Result(MatchEndReason.LastAlive, 1), null, false);
            Assert.AreEqual(4, model.Rows.Count);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 3 }, model.Rows.Select(r => r.Rank).ToArray());
            CollectionAssert.AreEqual(new byte[] { 1, 0, 3, 2 }, model.Rows.Select(r => r.NetId).ToArray(), "stable among ties");
            Assert.AreEqual(2, model.Rows.Count(r => r.Name == "Huy"), "two rows, not merged");
            Assert.AreEqual(0, model.Rows.Single(r => r.IsSelf).NetId, "self by NetId, not by name");
        }
    }

    /// <summary>AC3, AC35–AC37 on the API status, history and leaderboard models.</summary>
    public sealed class ApiModelsTests
    {
        [TestCase(true, 200, ApiMessages.ApiReady)]
        [TestCase(true, 503, ApiMessages.ApiDown)]
        [TestCase(true, 0, ApiMessages.ApiDown)]
        [TestCase(false, 200, ApiMessages.NoApi)]
        public void ApiStatus_ThreeStates(bool configured, int status, string text)
        {
            var api = new JungleMaze.App.Lobby.Mocks.MockLobbyApi { Configured = configured, HealthStatus = status };
            var model = new ApiStatusModel(api);
            model.Check();
            Assert.AreEqual(text, model.Text);
        }

        [Test]
        public void History_ThreeStates_GuestOrJwt()
        {
            using var rig = new LobbyRig(login: false);
            rig.History.Data.Add(new PlayerMatchHistory { MatchId = Guid.NewGuid(), Rank = 2, SurvivalSeconds = 65, EndedAt = DateTimeOffset.UtcNow, Reason = "LastAlive" });
            rig.Flow.OpenHistory();
            Assert.AreEqual(ListState.Ready, rig.Flow.History.State);
            Assert.IsNull(rig.History.LastToken, "guest credential, no JWT, no nickname");
            StringAssert.Contains("Hạng #2", HistoryModel.Line(rig.Flow.History.Rows[0]));
            StringAssert.Contains("01:05", HistoryModel.Line(rig.Flow.History.Rows[0]));

            rig.History.Data.Clear();
            Assert.IsTrue(rig.Flow.History.Refresh().IsCompleted, "completed fakes finish in the same call");
            Assert.AreEqual(ListState.Empty, rig.Flow.History.State);
            Assert.AreEqual(ApiMessages.HistoryEmpty, rig.Flow.History.Message);

            rig.History.Fail = true;
            Assert.IsTrue(rig.Flow.History.Refresh().IsCompleted, "completed fakes finish in the same call");
            Assert.AreEqual(ListState.Unavailable, rig.Flow.History.State);
            Assert.AreEqual(ApiMessages.HistoryDown, rig.Flow.History.Message);

            rig.Account.Username = "huycv";
            rig.Account.Password = "secret123";
            rig.Account.Submit();
            rig.History.Fail = false;
            Assert.IsTrue(rig.Flow.History.Refresh().IsCompleted, "completed fakes finish in the same call");
            Assert.AreEqual(rig.Api.IssuedTokens.Single(), rig.History.LastToken, "JWT when logged in");
        }

        [Test]
        public void History_UnavailableWhenOffOrUnconfigured_JoinStillWorks()
        {
            using var rig = new LobbyRig(o => o.HistoryEnabled = false);
            rig.Flow.OpenHistory();
            Assert.AreEqual(ListState.Unavailable, rig.Flow.History.State);
            Assert.AreEqual(0, rig.History.Refreshes);
            rig.Flow.Back();
            rig.JoinLobby();
            Assert.AreEqual(LobbyScreen.Lobby, rig.Flow.Screen);
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator History_Timeout_Unavailable()
        {
            var rig = new LobbyRig();
            rig.History.Hang = true;
            var task = rig.Flow.History.Refresh();
            var until = System.DateTime.UtcNow.AddSeconds(3);
            while (!task.IsCompleted && System.DateTime.UtcNow < until) yield return null;
            Assert.AreEqual(ListState.Unavailable, rig.Flow.History.State, "the 10 s budget (200 ms here) gives up");
            rig.Dispose();
        }

        [Test]
        public void GuestLabel_UntilVerified()
        {
            using var rig = new LobbyRig(login: false);
            rig.History.Ready = false;
            Assert.AreEqual(ApiMessages.GuestUnsaved, rig.Flow.History.IdentityLabel);
            rig.Flow.OpenHistory();
            Assert.AreEqual(ListState.Unavailable, rig.Flow.History.State, "no unverified guest query");
            Assert.AreEqual(0, rig.History.Refreshes);
        }

        [TestCase(200, 2, ListState.Ready, "")]
        [TestCase(200, 0, ListState.Empty, ApiMessages.LeaderboardEmpty)]
        [TestCase(404, 0, ListState.Empty, ApiMessages.LeaderboardEmpty)]
        [TestCase(503, 0, ListState.Unavailable, "Bảng xếp hạng online tạm ngừng")]
        public void Leaderboard_States(int status, int rows, ListState state, string message)
        {
            var api = new JungleMaze.App.Lobby.Mocks.MockLobbyApi
            {
                LeaderboardStatus = status,
                Board = Enumerable.Range(0, rows).Select(i => new LeaderboardEntry("s" + i, "Huy", 1, 0, 30)).ToArray()
            };
            var model = new LeaderboardModel(api);
            model.Load(Guid.NewGuid());
            Assert.AreEqual(state, model.State);
            Assert.AreEqual(message, model.Message);
            Assert.AreEqual(rows, model.Rows.Length, "duplicate names stay separate rows");
        }
    }

    /// <summary>UI-02 round 2 (Flow_Proposal 6): login first, account card logout with confirm, server settings modal.</summary>
    public sealed class LoginFirstFlowTests
    {
        [Test]
        public void Starts_OnLogin_WithoutJwt_ThenHomeAfterLogin()
        {
            using var rig = new LobbyRig(login: false);
            Assert.AreEqual(LobbyScreen.Login, rig.Flow.Screen, "no JWT → Login is the first screen");
            rig.Flow.Back();
            Assert.AreEqual(LobbyScreen.Login, rig.Flow.Screen, "nothing before Login");
            rig.LogIn();
            Assert.AreEqual(LobbyScreen.MainMenu, rig.Flow.Screen, "login lands on Home, not the room manager");
            Assert.AreEqual(0, rig.Api.Lists, "Home does not load rooms");
        }

        [Test]
        public void ValidJwt_StartsOnHome()
        {
            using var first = new LobbyRig();
            var flow = new LobbyFlow(first.Lobby, first.Results, first.Connection, first.Commands, first.Join, first.Account,
                first.Rooms, null, null, null, first.Clock.Read);
            Assert.AreEqual(LobbyScreen.MainMenu, flow.Screen, "a JWT still valid in memory skips Login");
            flow.Dispose();
        }

        [Test]
        public void NoJwt_JoinBlockedOnClient()
        {
            using var rig = new LobbyRig(login: false);
            rig.Flow.OpenJoin();
            Assert.AreEqual(LobbyScreen.Login, rig.Flow.Screen);
            Assert.IsFalse(rig.Flow.SubmitJoin());
            Assert.AreEqual(0, rig.Commands.Joins, "no join packet without a login");
        }

        [Test]
        public void JwtExpiresOnHome_BackToLogin_JoinBlocked()
        {
            using var rig = new LobbyRig();
            rig.Utc = rig.Utc.AddHours(2); // past the mock's 1 h token
            rig.Tick();
            Assert.AreEqual(LobbyScreen.Login, rig.Flow.Screen);
            rig.LogIn();
            rig.Flow.OpenJoin();
            rig.Join.SetCode("K7M2Q9");
            rig.Utc = rig.Utc.AddHours(2);
            Assert.IsFalse(rig.Flow.SubmitJoin(), "expired between opening the modal and pressing join");
            Assert.AreEqual(LobbyScreen.Login, rig.Flow.Screen);
            Assert.AreEqual(0, rig.Commands.Joins);
        }

        [Test]
        public void Logout_AsksFirst_ThenDropsJwtAndShowsLogin()
        {
            using var rig = new LobbyRig();
            rig.Flow.RequestLogout();
            Assert.IsTrue(rig.Flow.LogoutConfirmOpen);
            rig.Flow.CancelLogout();
            Assert.IsFalse(rig.Flow.LogoutConfirmOpen);
            Assert.IsTrue(rig.Account.IsValid, "Hủy keeps the session");
            rig.Flow.RequestLogout();
            rig.Flow.ConfirmLogout();
            Assert.IsFalse(rig.Account.IsValid);
            Assert.AreEqual(LobbyScreen.Login, rig.Flow.Screen);
            Assert.IsFalse(rig.Flow.LogoutConfirmOpen);
        }

        [Test]
        public void ServerSettings_FromHomeAndLogin_ClosedByNavigation()
        {
            using var rig = new LobbyRig(login: false);
            rig.Flow.OpenServerSettings();
            Assert.IsTrue(rig.Flow.ServerSettingsOpen, "Login 'Máy chủ (nâng cao)'");
            rig.LogIn();
            Assert.AreEqual(LobbyScreen.MainMenu, rig.Flow.Screen);
            rig.Flow.CloseServerSettings();
            rig.Flow.OpenServerSettings();
            Assert.IsTrue(rig.Flow.ServerSettingsOpen, "Home 'Cài đặt'");
            rig.Flow.OpenJoin();
            Assert.IsFalse(rig.Flow.ServerSettingsOpen, "leaving Home closes the modal");
            rig.Flow.OpenServerSettings();
            Assert.IsFalse(rig.Flow.ServerSettingsOpen, "only from Home or Login");
        }

        [Test]
        public void DisplayName_PrefilledFromAccount_WhenEmpty()
        {
            using var rig = new LobbyRig(login: false);
            rig.Join.SetName("");
            rig.LogIn("huycv");
            Assert.AreEqual("huycv", rig.Join.DisplayName);
            rig.Join.SetName("Huy");
            rig.LogIn("huycv");
            Assert.AreEqual("Huy", rig.Join.DisplayName, "a name the player typed is kept");
        }

        [Test]
        public void HomeLeaderboard_OpensNewestMatchFromHistory()
        {
            using var rig = new LobbyRig();
            var newest = Guid.NewGuid();
            rig.History.Data.Add(new PlayerMatchHistory { MatchId = Guid.NewGuid(), EndedAt = DateTimeOffset.UtcNow.AddDays(-1) });
            rig.History.Data.Add(new PlayerMatchHistory { MatchId = newest, EndedAt = DateTimeOffset.UtcNow });
            Assert.IsTrue(rig.Flow.History.Refresh().IsCompleted);
            rig.Flow.Back();
            rig.Flow.OpenLatestLeaderboard();
            Assert.AreEqual(LobbyScreen.Leaderboard, rig.Flow.Screen);
            Assert.AreEqual(newest, rig.Flow.Leaderboard.MatchId);
            rig.Flow.Back();
            Assert.AreEqual(LobbyScreen.MainMenu, rig.Flow.Screen, "Quay lại returns to Home");
        }

        /// <summary>Review M1: the dev skip works only when the composition turns it on (Editor/dev builds).</summary>
        [Test]
        public void DevSkip_OnlyWhenEnabled_JoinByCode_RoomsStillNeedLogin()
        {
            using (var release = new LobbyRig(login: false))
            {
                release.Flow.DevSkipLogin();
                Assert.AreEqual(LobbyScreen.Login, release.Flow.Screen, "off by default: release has no way past Login");
                Assert.IsFalse(LobbyOptions.Default.DevSkipLogin);
            }
            using var rig = new LobbyRig(o => o.DevSkipLogin = true, login: false);
            rig.Flow.DevSkipLogin();
            Assert.AreEqual(LobbyScreen.MainMenu, rig.Flow.Screen);
            Assert.IsTrue(rig.Flow.DevSkipped);
            rig.Flow.OpenJoin();
            rig.Join.SetCode("K7M2Q9");
            Assert.IsTrue(rig.Flow.SubmitJoin(), "room code without an account");
            Assert.AreEqual(1, rig.Commands.Joins);
            rig.Flow.CancelJoin();
            rig.Flow.Back();
            rig.Flow.OpenRooms();
            Assert.AreEqual(LobbyScreen.Login, rig.Flow.Screen, "the room manager still needs a JWT");
            Assert.IsFalse(rig.Flow.DevSkipped, "back on Login ends the dev skip");
        }

        /// <summary>Review m1: logout and JWT expiry forget the last account's history and last match.</summary>
        [Test]
        public void LogoutOrExpiry_ClearsHistoryAndLastMatch()
        {
            using var rig = new LobbyRig();
            rig.InGame();
            rig.Results.Current = rig.Result(MatchEndReason.LastAlive, 0, (0, 1, "Huy"), (1, 2, "Mai"));
            rig.Phase(MatchPhase.Ending, 10);
            Assert.AreEqual(LobbyScreen.Results, rig.Flow.Screen);
            rig.Flow.GoHome();
            rig.Connection.Set(NetworkState.Disconnected, DisconnectReason.UserRequested);
            rig.Tick();
            rig.History.Data.Add(new PlayerMatchHistory { MatchId = Guid.NewGuid(), EndedAt = DateTimeOffset.UtcNow });
            rig.Flow.OpenHistory();
            Assert.AreEqual(1, rig.Flow.History.Rows.Count);
            rig.Flow.Back();
            rig.Flow.RequestLogout();
            rig.Flow.ConfirmLogout();
            Assert.AreEqual(0, rig.Flow.History.Rows.Count, "no rows of the previous account");
            Assert.AreEqual(ListState.Idle, rig.Flow.History.State);
            rig.LogIn("mai01");
            rig.Flow.OpenLatestLeaderboard();
            Assert.AreEqual(Guid.Empty, rig.Flow.Leaderboard.MatchId, "neither the old history row nor the old result");

            rig.Flow.Back();
            rig.Flow.OpenHistory();
            Assert.AreEqual(1, rig.Flow.History.Rows.Count);
            rig.Flow.Back();
            rig.Utc = rig.Utc.AddHours(2);
            rig.Tick();
            Assert.AreEqual(LobbyScreen.Login, rig.Flow.Screen);
            Assert.AreEqual(0, rig.Flow.History.Rows.Count, "expiry clears too");
        }

        [Test]
        public void HomeLeaderboard_NoMatchYet_IsEmptyNotDown()
        {
            using var rig = new LobbyRig();
            rig.Flow.OpenLatestLeaderboard();
            Assert.AreEqual(ListState.Empty, rig.Flow.Leaderboard.State);
            Assert.AreEqual(ApiMessages.HistoryEmpty, rig.Flow.Leaderboard.Message);
            Assert.AreEqual(0, rig.Api.Boards, "no request for an empty id");
        }
    }
}
