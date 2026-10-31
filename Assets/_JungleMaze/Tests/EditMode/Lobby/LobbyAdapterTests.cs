using System;
using System.IO;
using System.Linq;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Lobby;
using JungleMaze.App.Lobby.Adapters;
using JungleMaze.App.Lobby.Mocks;
using JungleMaze.App.MatchUi;
using JungleMaze.Domain.Lobby;
using JungleMaze.Domain.Network;
using JungleMaze.Tests.MatchUi;
using NUnit.Framework;

namespace JungleMaze.Tests.Lobby
{
    /// <summary>AC11, AC20 on the live adapters over a real MatchSession and fake network.</summary>
    public sealed class MatchSessionLobbySourceTests
    {
        [Test]
        public void DuplicateNames_TwoSlots_SelfByNetId()
        {
            using var h = new LiveMatchHarness();
            h.Network.Push(new MatchPhasePacket { MatchId = h.MatchId, Phase = MatchPhase.Lobby, ServerTick = 100, PhaseEndTick = 550, MinPlayers = 2, MaxPlayers = 10, PlayerCount = 3 });
            h.Network.Push(new ResumeTokenPacket { MatchId = h.MatchId, NetId = 2, ResumeToken = Guid.NewGuid() });
            h.Network.Push(new MatchRosterPacket
            {
                MatchId = h.MatchId,
                Players = new[]
                {
                    new MatchParticipantState { NetId = 1, PlayerSessionId = "a", DisplayName = "Huy" },
                    new MatchParticipantState { NetId = 2, PlayerSessionId = "b", DisplayName = "Huy" },
                    new MatchParticipantState { NetId = 4, PlayerSessionId = "c", DisplayName = "Mai" }
                }
            });
            var source = new MatchSessionLobbySource(h.Match, () => "K7M2Q9");
            Assert.AreEqual(3, source.Slots.Count);
            Assert.AreEqual(2, source.Slots.Count(s => s.Name == "Huy"));
            Assert.AreEqual(2, source.Slots.Single(s => s.IsSelf).NetId, "'Bạn' by OwnNetId, not by name");
            Assert.AreSame(source.Slots, source.Slots, "no rebuild without a new roster");

            h.Network.Push(new MatchRosterPacket
            {
                MatchId = h.MatchId,
                Players = new[]
                {
                    new MatchParticipantState { NetId = 2, PlayerSessionId = "b", DisplayName = "Huy" },
                    new MatchParticipantState { NetId = 4, PlayerSessionId = "c", DisplayName = "Mai" }
                }
            });
            Assert.AreEqual(2, source.Slots.Count, "only the leaver's row is gone");
            Assert.IsTrue(source.Slots.Any(s => s.NetId == 2 && s.IsSelf));
        }

        [Test]
        public void Slots_MinFromState_CountdownFromTicks()
        {
            using var h = new LiveMatchHarness();
            h.Network.Push(new MatchPhasePacket { MatchId = h.MatchId, Phase = MatchPhase.Lobby, ServerTick = 100, PhaseEndTick = 550, MinPlayers = 2, MaxPlayers = 10, PlayerCount = 1 });
            var source = new MatchSessionLobbySource(h.Match, () => "K7M2Q9");
            Assert.AreEqual(2, source.MinPlayers, "profile test: 2, not a hard-coded 6");
            Assert.AreEqual(10, source.MaxPlayers);
            Assert.AreEqual(15, LobbyCountdown.Seconds(source.PhaseEndTick, source.ServerTick));
            Assert.AreEqual("K7M2Q9", source.RoomCode);
            Assert.IsFalse(source.ReadySupported);
        }

        [Test]
        public void ResultSource_ViewPerMatch()
        {
            using var h = new LiveMatchHarness();
            h.Start(0, 2);
            var source = new MatchSessionResultSource(h.Match);
            Assert.IsFalse(source.HasResult);
            h.Network.Push(new MatchResultPacket { MatchId = h.MatchId, Reason = MatchEndReason.LastAlive, WinnerNetId = 0, ParticipantCount = 2,
                Participants = new[] { new MatchParticipantState { NetId = 0, Rank = 1 }, new MatchParticipantState { NetId = 1, Rank = 2 } } });
            Assert.IsTrue(source.HasResult);
            Assert.AreEqual(0, source.Current.OwnNetId);
            Assert.AreEqual(ResultHeadline.Victory, ResultModel.HeadlineFor(source.Current));
            Assert.AreSame(source.Current, source.Current);
        }

        [Test]
        public void ConnectionSource_LocalFailures()
        {
            var bus = new JungleMaze.Core.Events.EventBus();
            var network = new JungleMaze.Tests.App.FakeNetwork(bus);
            using var source = new NetworkConnectionStateSource(network, () => false, bus);
            Assert.AreEqual(NetworkState.Disconnected, source.State);
            Assert.AreEqual(-1, source.RttMs, "no fake RTT");
            Assert.AreEqual("Auto", source.Step);
            bus.Publish(new JungleMaze.App.Flow.ConnectionError("login"));
            Assert.AreEqual(NetworkState.Failed, source.State);
            Assert.AreEqual(DisconnectReason.LoginFailed, source.LastReason);
            source.ClearLoginFailure();
            source.ReportLocalFailure();
            Assert.AreEqual(DisconnectReason.InvalidConfig, source.LastReason);
            network.SetState(NetworkState.Connecting, DisconnectReason.None);
            Assert.AreEqual(NetworkState.Connecting, source.State, "a new attempt clears it");
        }
    }

    /// <summary>4.4: mocks, scenarios and the F9 panel are dev-only; composition registers them only in dev builds.</summary>
    public sealed class LobbyMockGuardTests
    {
        private const string Guard = "#if UNITY_EDITOR || DEVELOPMENT_BUILD";

        [Test]
        public void MocksAndPanel_AreDevOnly()
        {
            var files = Directory.GetFiles("Assets/_JungleMaze/Scripts/Application/Lobby/Mocks", "*.cs")
                .Concat(new[] { "Assets/_JungleMaze/Scripts/Presentation/UI/Lobby/LobbyDebugPanel.cs" }).ToArray();
            Assert.GreaterOrEqual(files.Length, 3);
            foreach (var file in files)
            {
                var lines = File.ReadAllLines(file).Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
                Assert.AreEqual(Guard, lines.First(), file);
                Assert.AreEqual("#endif", lines.Last(), file);
            }
        }

        [Test]
        public void Composition_RegistersMocksOnlyInDevBuilds()
        {
            var text = File.ReadAllText("Assets/_JungleMaze/Scripts/Bootstrap/Lobby/Bootstrap.Lobby.cs");
            foreach (var devOnly in new[] { "new LobbyScenarios", "AddComponent<LobbyDebugPanel>" })
            {
                var at = text.IndexOf(devOnly, StringComparison.Ordinal);
                Assert.Greater(at, 0, devOnly);
                Assert.Greater(at, text.LastIndexOf(Guard, at, StringComparison.Ordinal), devOnly + " inside a dev-only block");
                Assert.Greater(text.IndexOf("#endif", at, StringComparison.Ordinal), at);
            }
        }

        /// <summary>AC43 review aid: no UI-02 file writes a phase, a rank, a token to disk, or touches Backend/protocol.</summary>
        [Test]
        public void NoAuthorityInUi02()
        {
            var files = Directory.GetFiles("Assets/_JungleMaze/Scripts", "*.cs", SearchOption.AllDirectories)
                .Where(f => f.Replace('\\', '/').Contains("/Lobby/") && !f.Replace('\\', '/').Contains("/Mocks/"));
            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(text, @"\bPhase\s*=[^=>]"), file + " assigns a phase");
                StringAssert.DoesNotContain("ServerTick +=", text, file);
                StringAssert.DoesNotContain("File.Write", text, file);
                StringAssert.DoesNotContain("SetString(\"jm.menu.token", text, file);
            }
        }
    }

    /// <summary>AC44 in EditMode: the scripted mock server drives the real flow through a full run and the DoD cases.</summary>
    public sealed class LobbyScenarioScriptTests
    {
        private static (LobbyScenarios s, LobbyFlow f, FakeClock c) Make(LobbyCase c)
        {
            var clock = new FakeClock { Now = 1 };
            var s = new LobbyScenarios(clock.Read) { JoinDelay = 0.1f, LobbySeconds = 1, LoadingSeconds = 1, InGameSeconds = 1, EndingSeconds = 3, DropAfter = 0.2f };
            s.Use(c);
            var p = s.Ports;
            var account = new AccountSession(p, () => DateTime.UtcNow);
            var flow = new LobbyFlow(p, p, p, p, new JoinController(p, clock.Read, null, "Huy"), account, new RoomListModel(p, account),
                null, new LeaderboardModel(p), new ApiStatusModel(p), clock.Read);
            return (s, flow, clock);
        }

        private static void Run(LobbyScenarios s, LobbyFlow f, FakeClock c, double seconds)
        {
            for (var t = 0.0; t < seconds; t += 0.05) { c.Advance(0.05); s.Tick(0.05f); f.Tick(0.05f); }
        }

        private static void StartJoin(LobbyFlow f)
        {
            f.OpenJoin();
            f.Join.SetCode("K7M2Q9");
            Assert.IsTrue(f.SubmitJoin());
        }

        [Test]
        public void HappyPath_FullLoop_ReplayThenHome()
        {
            var (s, f, c) = Make(LobbyCase.HappyPath);
            StartJoin(f);
            Run(s, f, c, 0.3);
            Assert.AreEqual(LobbyScreen.Lobby, f.Screen);
            Assert.IsTrue(f.HasCountdown);
            Run(s, f, c, 1.1);
            Assert.AreEqual(LobbyScreen.Loading, f.Screen);
            Run(s, f, c, 1.1);
            Assert.AreEqual(LobbyScreen.InGame, f.Screen);
            Run(s, f, c, 1.1);
            Assert.AreEqual(LobbyScreen.Results, f.Screen);
            Assert.AreEqual(ResultModel.VictoryText, f.Result.Title);
            Assert.IsTrue(f.Replay());
            Run(s, f, c, 0.3);
            Assert.AreEqual(LobbyScreen.Lobby, f.Screen);
            f.GoHome();
            Run(s, f, c, 0.2);
            Assert.AreEqual(LobbyScreen.MainMenu, f.Screen);
            Assert.AreEqual(1, s.Commands.Leaves);
        }

        [TestCase(LobbyCase.RoomFull, "Phòng đã đủ 10 người")]
        [TestCase(LobbyCase.WrongCode, JoinRejectText.WrongCode)]
        public void JoinRejected(LobbyCase c, string message)
        {
            var (s, f, clock) = Make(c);
            StartJoin(f);
            Run(s, f, clock, 0.3);
            Assert.AreEqual(LobbyScreen.JoinForm, f.Screen);
            Assert.AreEqual(message, f.Join.Message);
        }

        [Test]
        public void LoadError_Case()
        {
            var (s, f, c) = Make(LobbyCase.LoadError);
            StartJoin(f);
            Run(s, f, c, 1.5);
            Assert.AreEqual(LobbyScreen.LoadError, f.Screen);
        }

        [TestCase(LobbyCase.Disconnect, LobbyScreen.InGame)]
        [TestCase(LobbyCase.DisconnectRejected, LobbyScreen.Disconnected)]
        public void Disconnect_Cases(LobbyCase c, LobbyScreen end)
        {
            var (s, f, clock) = Make(c);
            StartJoin(f);
            Run(s, f, clock, 2.45);
            Assert.AreEqual(LobbyScreen.Reconnecting, f.Screen);
            Run(s, f, clock, 0.5);
            Assert.AreEqual(end, f.Screen);
            Assert.AreEqual(1, s.Commands.Resumes);
        }

        [Test]
        public void JwtExpired_Case()
        {
            var (s, f, c) = Make(LobbyCase.JwtExpired);
            f.OpenRooms();
            f.Account.Username = "huycv";
            f.Account.Password = "secret123";
            f.Account.Submit();
            Assert.AreEqual(LobbyScreen.Login, f.Screen, "the rooms call answered 401");
            Assert.AreEqual(ApiMessages.SessionExpired, f.Account.Message);
        }
    }
}
