using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using JungleBackend.Protocol;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Config;
using JungleMaze.App.Ports;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Network;
using JungleMaze.Infrastructure.Network;
using NUnit.Framework;

namespace JungleMaze.Tests.Network
{
    /// <summary>NetworkClient thật ↔ FakeGameServer trên loopback; mỗi test tự poll cả hai phía ở main thread.</summary>
    public sealed class NetworkClientTests
    {
        private FakeGameServer _server;
        private EventBus _bus;
        private NetworkClient _client;
        private readonly List<NetworkStateChanged> _changes = new List<NetworkStateChanged>();

        [SetUp]
        public void SetUp()
        {
            _server = new FakeGameServer();
            _bus = new EventBus();
            _client = new NetworkClient(_bus, new PacketRegistry());
            _changes.Clear();
            _bus.Subscribe<NetworkStateChanged>(e => _changes.Add(e));
        }

        [TearDown]
        public void TearDown()
        {
            _client.Dispose();
            _server.Dispose();
        }

        private ClientConfig Config(int port, ushort version = 1, int timeoutMs = 3000,
            JoinAuthMode auth = JoinAuthMode.RoomCode, string api = null) =>
            new ClientConfig(ConnectionMode.Local, api, "127.0.0.1", port, auth, version, timeoutMs);

        private bool Pump(Func<bool> until, int timeoutMs = 3000)
        {
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < timeoutMs)
            {
                _server.Poll();
                _client.Tick(0.016f);
                if (until()) return true;
                Thread.Sleep(5);
            }
            return until();
        }

        private void JoinRoom(ushort version = 1)
        {
            _client.Connect(new ConnectRequest(Config(_server.Port, version), "ROOM42", "Tester"));
            Assert.IsTrue(Pump(() => _client.State == NetworkState.Joined), $"Không Joined, state={_client.State} reason={_client.LastReason}");
        }

        [Test]
        public void RoomCode_JoinsAndPublishesStateChanges()
        {
            JoinRoom();
            Assert.AreEqual("player-1", _client.PlayerId);
            Assert.AreEqual(NetworkState.Connecting, _changes[0].Current);
            Assert.AreEqual(NetworkState.Joined, _changes[_changes.Count - 1].Current);
            Assert.AreEqual(1, _server.LastRequest.Version);
            Assert.AreEqual("Tester", _server.LastRequest.DisplayName);
        }

        [Test]
        public void ProtocolV2_SendsRandomAttemptId()
        {
            JoinRoom(version: 2);
            Assert.AreEqual(2, _server.LastRequest.Version);
            Assert.AreNotEqual(Guid.Empty, _server.LastRequest.AttemptId);
        }

        [Test]
        public void Joined_PingPong_RecordsRealRtt()
        {
            JoinRoom();
            Assert.IsTrue(Pump(() => _client.Stats.PongsReceived >= 2, 4000), "Không nhận đủ pong");
            var stats = _client.Stats;
            Assert.Greater(stats.LastRttMs, 0);
            Assert.Greater(stats.P95RttMs, 0);
            Assert.AreEqual(0, stats.PongsLost);
            Assert.Greater(stats.PacketsReceived, 0);
        }

        [Test]
        public void WrongRoomCode_FailsWithServerReason()
        {
            _client.Connect(new ConnectRequest(Config(_server.Port), "WRONG", "Tester"));
            Assert.IsTrue(Pump(() => _client.State == NetworkState.Failed));
            Assert.AreEqual(DisconnectReason.InvalidRoomCode, _client.LastReason);
            Assert.AreEqual(DisconnectReason.InvalidRoomCode, _changes[_changes.Count - 1].Reason);
        }

        [Test]
        public void RoomFull_FailsWithRoomFull()
        {
            _server.RoomFull = true;
            _client.Connect(new ConnectRequest(Config(_server.Port), "ROOM42", "Tester"));
            Assert.IsTrue(Pump(() => _client.State == NetworkState.Failed));
            Assert.AreEqual(DisconnectReason.RoomFull, _client.LastReason);
        }

        [Test]
        public void NoServer_FailsWithConnectTimeout()
        {
            var port = _server.Port;
            _server.Dispose();
            _server = new FakeGameServer(); // giữ TearDown hợp lệ; port cũ không còn ai nghe
            _client.Connect(new ConnectRequest(Config(port, timeoutMs: 600), "ROOM42", "Tester"));
            Assert.IsTrue(Pump(() => _client.State == NetworkState.Failed, 4000));
            Assert.That(_client.LastReason, Is.EqualTo(DisconnectReason.ConnectTimeout).Or.EqualTo(DisconnectReason.SocketError));
        }

        [Test]
        public void InvalidConfig_FailsWithoutOpeningSocket()
        {
            _client.Connect(new ConnectRequest(Config(0), "ROOM42", "Tester"));
            Assert.AreEqual(NetworkState.Failed, _client.State);
            Assert.AreEqual(DisconnectReason.InvalidConfig, _client.LastReason);
            Assert.AreEqual(0, _client.LocalPort);
        }

        [Test]
        public void InvalidDisplayName_FailsBeforeConnect()
        {
            _client.Connect(new ConnectRequest(Config(_server.Port), "ROOM42", new string('x', 40)));
            Assert.AreEqual(DisconnectReason.InvalidDisplayName, _client.LastReason);
        }

        [Test]
        public void Jwt_AnswersAuthRequiredWithToken()
        {
            _server.RequireJwt = true;
            var config = Config(_server.Port, auth: JoinAuthMode.Jwt, api: "http://127.0.0.1:5000");
            _client.Connect(new ConnectRequest(config, "ROOM42", "Tester", "header.payload.signature"));
            Assert.IsTrue(Pump(() => _client.State == NetworkState.Joined));
            Assert.AreEqual("header.payload.signature", _server.ReceivedToken);
            Assert.IsTrue(_changes.Exists(c => c.Current == NetworkState.Authenticating));
        }

        [Test]
        public void Jwt_WithoutToken_FailsAuthenticationRequired()
        {
            var config = Config(_server.Port, auth: JoinAuthMode.Jwt, api: "http://127.0.0.1:5000");
            _client.Connect(new ConnectRequest(config, "ROOM42", "Tester"));
            Assert.AreEqual(DisconnectReason.AuthenticationRequired, _client.LastReason);
        }

        [Test]
        public void ServerKicks_WhileJoined_ReportsServerClosed()
        {
            JoinRoom();
            _server.Kick();
            Assert.IsTrue(Pump(() => _client.State == NetworkState.Failed));
            Assert.AreEqual(DisconnectReason.ServerClosed, _client.LastReason);
        }

        [Test]
        public void Disconnect_IsUserRequested_AndServerFreesSlot()
        {
            JoinRoom();
            _client.Disconnect();
            Assert.AreEqual(NetworkState.Disconnected, _client.State);
            Assert.AreEqual(DisconnectReason.UserRequested, _client.LastReason);
            Assert.IsTrue(Pump(() => _server.Disconnects == 1));
        }

        [Test]
        public void CanReconnectAfterFailure()
        {
            _client.Connect(new ConnectRequest(Config(_server.Port), "WRONG", "Tester"));
            Assert.IsTrue(Pump(() => _client.State == NetworkState.Failed));
            JoinRoom();
        }

        [Test]
        public void GameplayPacket_RoundTripOnChannel1_ManySubscribers()
        {
            var received = new List<PlayerStatePacket>();
            var second = 0;
            _client.Subscribe<PlayerStatePacket>(p => received.Add(p));
            _client.Subscribe<PlayerStatePacket>(_ => second++);
            var input = new PlayerInputPacket { Tick = 7, InputSeq = 3 };
            input.SetMove(1f, 0f);
            Assert.IsFalse(_client.Send(ref input, PacketDelivery.Unreliable), "Chưa Joined thì không được gửi");

            JoinRoom();
            Assert.IsTrue(_client.Send(ref input, PacketDelivery.ReliableOrdered));
            Assert.IsTrue(Pump(() => received.Count == 1),
                $"serverGot={_server.GameplayPackets} clientInvalid={_client.Stats.InvalidPackets} state={_client.State}");
            Assert.AreEqual(7u, received[0].Tick);
            Assert.AreEqual(3, received[0].LastProcessedInputSeq);
            Assert.AreEqual(1f, received[0].X, 0.01f);
            Assert.AreEqual(1, second);
            Assert.AreEqual(127, _server.LastInput.MoveX);
        }

        [Test]
        public void SteadyState_TickAndSend_DoNotAllocateOnMainThread()
        {
            var received = 0;
            _client.Subscribe<PlayerStatePacket>(_ => received++);
            JoinRoom();
            var input = new PlayerInputPacket();
            // Warm-up: buffer writer, pool packet của LiteNetLib, JIT.
            for (var i = 0; i < 50; i++) { _client.Send(ref input, PacketDelivery.Unreliable); Pump(() => false, 10); }

            long allocated = 0;
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < 1500)
            {
                _server.Poll(); // server ngoài vùng đo
                var before = GC.GetAllocatedBytesForCurrentThread();
                input.Tick++;
                _client.Send(ref input, PacketDelivery.Unreliable);
                _client.Tick(0.016f);
                allocated += GC.GetAllocatedBytesForCurrentThread() - before;
                Thread.Sleep(2);
            }
            Assert.Greater(received, 50);
            Assert.AreEqual(0, allocated, "Send/Tick/handler packet gameplay cấp phát trên main thread");
        }
    }
}
