using System.Collections.Generic;
using JungleBackend.Protocol;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Config;
using JungleMaze.App.Flow;
using JungleMaze.App.Network;
using JungleMaze.App.Player;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Network;
using JungleMaze.Domain.Player;
using NUnit.Framework;

namespace JungleMaze.Tests.App
{
    public sealed class GameFlowTests
    {
        private EventBus _bus;
        private FakeNetwork _network;
        private FakeScenes _scenes;
        private PlayerInputService _input;
        private GameFlow _flow;
        private readonly List<AppScreen> _screens = new List<AppScreen>();

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _network = new FakeNetwork(_bus);
            _scenes = new FakeScenes();
            _input = new PlayerInputService(_network);
            _flow = new GameFlow(_bus, _network, _network, _scenes, _input, new GameFlowSettings { ResultsSeconds = 1f });
            _screens.Clear();
            _bus.Subscribe<AppScreenChanged>(e => _screens.Add(e.Current));
            _flow.Initialize();
        }

        [TearDown]
        public void TearDown() => _flow.Dispose();

        private void Join()
        {
            _network.SetState(NetworkState.Connecting, DisconnectReason.None);
            _network.SetState(NetworkState.Joined, DisconnectReason.None);
        }

        [Test]
        public void Boot_GoesToMainMenu_WithoutLoadingScene()
        {
            Assert.AreEqual(AppScreen.MainMenu, _flow.Screen);
            CollectionAssert.AreEqual(new[] { AppScreen.Boot, AppScreen.MainMenu }, _screens);
            Assert.IsEmpty(_scenes.Loads);
        }

        [Test]
        public void LocalPreview_StartsInMatchWithoutLoadingScene()
        {
            _flow.Dispose();
            _scenes.ActiveScene = "Game";
            _flow = new GameFlow(_bus, _network, _network, _scenes, _input,
                new GameFlowSettings { StartLocalPreview = true });
            _flow.Initialize();

            Assert.AreEqual(AppScreen.InMatch, _flow.Screen);
            Assert.IsTrue(_input.Enabled);
            Assert.IsEmpty(_scenes.Loads);
        }

        [Test]
        public void Join_GoesToInRoom_AndLoadsGameScene()
        {
            Join();
            Assert.AreEqual(AppScreen.InRoom, _flow.Screen);
            CollectionAssert.Contains(_scenes.Loads, "Game");
        }

        [Test]
        public void Failure_WhileConnecting_ReturnsToMenuWithReason()
        {
            _network.SetState(NetworkState.Connecting, DisconnectReason.None);
            _network.SetState(NetworkState.Failed, DisconnectReason.RoomFull);
            Assert.AreEqual(AppScreen.MainMenu, _flow.Screen);
            Assert.AreEqual(DisconnectReasons.Describe(DisconnectReason.RoomFull), _flow.Message);
        }

        [Test]
        public void ConnectionLost_InGame_LoadsMenuScene()
        {
            Join();
            _network.SetState(NetworkState.Failed, DisconnectReason.ConnectionLost);
            Assert.AreEqual(AppScreen.MainMenu, _flow.Screen);
            Assert.AreEqual("Boot", _scenes.ActiveScene);
            StringAssert.Contains("Mất kết nối", _flow.Message);
        }

        [Test]
        public void MatchEvents_DriveInMatchResultsInRoom_AndToggleInput()
        {
            Join();
            _network.Push(new EventPacket { EventType = GameEventType.MatchEnded }); // bỏ qua khi chưa vào trận
            Assert.AreEqual(AppScreen.InRoom, _flow.Screen);

            _network.Push(new EventPacket { EventType = GameEventType.MatchStarted });
            Assert.AreEqual(AppScreen.InMatch, _flow.Screen);
            Assert.IsTrue(_input.Enabled);

            _network.Push(new EventPacket { EventType = GameEventType.MatchEnded });
            Assert.AreEqual(AppScreen.Results, _flow.Screen);
            Assert.IsFalse(_input.Enabled);

            _flow.Tick(0.6f);
            Assert.AreEqual(AppScreen.Results, _flow.Screen);
            _flow.Tick(0.6f);
            Assert.AreEqual(AppScreen.InRoom, _flow.Screen);
        }

        [Test]
        public void UserDisconnect_FromRoom_ReturnsToMenu()
        {
            Join();
            _network.Disconnect();
            Assert.AreEqual(AppScreen.MainMenu, _flow.Screen);
        }
    }

    public sealed class ConnectUseCaseTests
    {
        private EventBus _bus;
        private FakeNetwork _network;
        private FakeAuth _auth;
        private readonly List<string> _errors = new List<string>();

        private ConnectToServerUseCase UseCase(JoinAuthMode auth, string host = "127.0.0.1")
        {
            _bus = new EventBus();
            _network = new FakeNetwork(_bus);
            _auth = new FakeAuth();
            _errors.Clear();
            _bus.Subscribe<ConnectionError>(e => _errors.Add(e.Message));
            var config = new ClientConfig(ConnectionMode.Local, "http://127.0.0.1:5000", host, 7777, auth, 1, 5000);
            return new ConnectToServerUseCase(new FakeConfig(config), _network, _auth, _bus);
        }

        private static ConnectForm Form(string room = "ROOM42", string name = "Tester") =>
            new ConnectForm { RoomCode = room, DisplayName = name, Username = "player01", Password = "ClassDemo_123!" };

        [Test]
        public void RoomCode_ConnectsDirectly_TrimmedInput()
        {
            var useCase = UseCase(JoinAuthMode.RoomCode);
            Assert.IsNull(useCase.Execute(Form(" ROOM42 ", " Tester ")));
            Assert.AreEqual(1, _network.Connects.Count);
            Assert.AreEqual("ROOM42", _network.Connects[0].RoomCode);
            Assert.AreEqual("Tester", _network.Connects[0].DisplayName);
            Assert.AreEqual(0, _auth.Calls);
        }

        [Test]
        public void InvalidConfigOrMissingRoom_PublishesError_NoConnect()
        {
            var useCase = UseCase(JoinAuthMode.RoomCode, host: "http://bad");
            Assert.IsNotNull(useCase.Execute(Form()));
            Assert.AreEqual(1, _errors.Count);
            useCase = UseCase(JoinAuthMode.RoomCode);
            Assert.IsNotNull(useCase.Execute(Form(room: " ")));
            Assert.IsEmpty(_network.Connects);
        }

        [Test]
        public void Jwt_LogsInThenConnectsWithToken()
        {
            var useCase = UseCase(JoinAuthMode.Jwt);
            _auth.Defer = true;
            Assert.IsNull(useCase.Execute(Form()));
            Assert.IsTrue(useCase.IsLoggingIn);
            Assert.IsNotNull(useCase.Execute(Form()), "Không login hai lần song song");
            _auth.Pending(JungleMaze.App.Ports.LoginResult.Ok("jwt-token"));
            Assert.IsFalse(useCase.IsLoggingIn);
            Assert.AreEqual("jwt-token", _network.Connects[0].AccessToken);
        }

        [Test]
        public void Jwt_LoginFailure_PublishesError()
        {
            var useCase = UseCase(JoinAuthMode.Jwt);
            _auth.Result = JungleMaze.App.Ports.LoginResult.Fail("Sai mật khẩu");
            useCase.Execute(Form());
            CollectionAssert.Contains(_errors, "Sai mật khẩu");
            Assert.IsEmpty(_network.Connects);
        }
    }

    public sealed class WorldSyncAndInputTests
    {
        [Test]
        public void StatePacket_UpdatesModel_IgnoresOlderTick_ClearsOnDisconnect()
        {
            var bus = new EventBus();
            var network = new FakeNetwork(bus);
            var sync = new WorldSyncService(network, bus, new PlayerRegistry());
            sync.Initialize();
            var updated = new List<byte>();
            var removed = new List<byte>();
            bus.Subscribe<PlayerStateUpdated>(e => updated.Add(e.NetId));
            bus.Subscribe<PlayerRemoved>(e => removed.Add(e.NetId));

            network.Push(new PlayerStatePacket { Tick = 10, NetId = 3, X = 1, Y = 2, Hp = 90 });
            network.Push(new PlayerStatePacket { Tick = 9, NetId = 3, X = 9, Y = 9, Hp = 1 }); // đến muộn
            network.Push(new PlayerStatePacket { Tick = 11, NetId = 3, X = float.NaN });       // không hợp lệ
            Assert.IsTrue(sync.Players.TryGet(3, out var player));
            Assert.AreEqual(1f, player.Current.X);
            Assert.AreEqual(90, player.Current.Hp);
            CollectionAssert.AreEqual(new byte[] { 3 }, updated);
            Assert.AreEqual(2, sync.IgnoredPackets);

            network.SetState(NetworkState.Failed, DisconnectReason.ConnectionLost);
            Assert.AreEqual(0, sync.Players.Count);
            CollectionAssert.AreEqual(new byte[] { 3 }, removed);
            sync.Dispose();
        }

        [Test]
        public void Input_SendsAt30Hz_OnlyWhenEnabled()
        {
            var network = new FakeNetwork(new EventBus());
            var input = new PlayerInputService(network);
            input.SetIntent(new PlayerIntent { MoveX = 1f });
            input.Tick(1f);
            Assert.AreEqual(0, network.Sent, "Chưa Enabled thì không gửi");

            input.Enabled = true;
            for (var i = 0; i < 60; i++) input.Tick(1f / 60f); // 1 giây ở 60 FPS
            Assert.That(network.Sent, Is.InRange(29, 31));

            var before = network.Sent;
            input.Tick(5f); // khung hình rất dài: không gửi dồn quá 3 packet
            Assert.LessOrEqual(network.Sent - before, 3);
            Assert.AreEqual(network.Sent, input.LastInputSeq);
        }
    }
}
