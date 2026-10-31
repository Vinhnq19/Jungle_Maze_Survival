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

        /// <summary>AC27: UI-02 sends no LoadAck; the count with UI-02 equals the count without it (zero).</summary>
        [Test]
        public void LoadAckCount_Unchanged()
        {
            var bus = new JungleMaze.Core.Events.EventBus();
            var network = new JungleMaze.Tests.App.FakeNetwork(bus);
            var match = new JungleMaze.App.Match.MatchSession(network, network, network, bus, new JungleMaze.Domain.Player.PlayerRegistry());
            match.Initialize();
            network.SetState(NetworkState.Joined, DisconnectReason.None);
            var matchId = Guid.NewGuid();
            network.Push(new MatchPhasePacket { MatchId = matchId, Phase = MatchPhase.Loading, ServerTick = 10, PhaseEndTick = 910, MinPlayers = 2, MaxPlayers = 10 });
            var before = network.Sent;
            var commands = new JungleMaze.App.Lobby.Adapters.SessionLobbyCommands((c, n) => { }, () => { }, match, null);
            var clock = new JungleMaze.Tests.MatchUi.FakeClock();
            var flow = new LobbyFlow(new JungleMaze.App.Lobby.Adapters.MatchSessionLobbySource(match, () => "K7M2Q9"),
                new JungleMaze.App.Lobby.Adapters.MatchSessionResultSource(match),
                new JungleMaze.App.Lobby.Mocks.MockConnectionStateSource { State = NetworkState.Joined }, commands,
                new JoinController(commands, clock.Read), null, null, null, null, null, clock.Read);
            for (var i = 0; i < 120; i++) { flow.Tick(1 / 60f); clock.Advance(1 / 60.0); }
            Assert.AreEqual(LobbyScreen.MainMenu, flow.Screen, "flow untouched without a join");
            Assert.AreEqual(before, network.Sent, "no packet (LoadAck or other) from UI-02");
            Assert.IsFalse(System.IO.Directory.GetFiles("Assets/_JungleMaze/Scripts", "*.cs", System.IO.SearchOption.AllDirectories)
                .Where(f => f.Replace('\\', '/').Contains("/Lobby/"))
                .Any(f => System.IO.File.ReadAllText(f).Contains("ConfirmLoaded") || System.IO.File.ReadAllText(f).Contains("LoadAckPacket")));
            match.Dispose();
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
            using var rig = new LobbyRig();
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
            using var rig = new LobbyRig();
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
}
