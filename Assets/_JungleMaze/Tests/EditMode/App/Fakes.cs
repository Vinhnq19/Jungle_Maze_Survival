using System;
using System.Collections.Generic;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Config;
using JungleMaze.App.Ports;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Network;
using LiteNetLib.Utils;

namespace JungleMaze.Tests.App
{
    /// <summary>Port mạng giả: ghi lại lệnh, cho test tự đẩy state/packet. Cùng EventBus với code thật.</summary>
    public sealed class FakeNetwork : INetworkClient, IPacketSender, IPacketReceiver
    {
        private readonly IEventBus _bus;
        private readonly EventBus _packets = new EventBus();
        public readonly List<ConnectRequest> Connects = new List<ConnectRequest>();
        public int Sent;
        public bool AcceptSends = true;

        public FakeNetwork(IEventBus bus) => _bus = bus;

        public NetworkState State { get; private set; }
        public DisconnectReason LastReason { get; private set; }
        public string PlayerId => State == NetworkState.Joined ? "p1" : null;
        public string Endpoint => "127.0.0.1:7777";
        public NetworkStats Stats => default;

        public void Connect(in ConnectRequest request)
        {
            Connects.Add(request);
            SetState(NetworkState.Connecting, DisconnectReason.None);
        }

        public void Disconnect() => SetState(NetworkState.Disconnected, DisconnectReason.UserRequested);

        public void SetState(NetworkState state, DisconnectReason reason)
        {
            var previous = State;
            State = state;
            LastReason = reason;
            _bus.Publish(new NetworkStateChanged(previous, state, reason));
        }

        public bool Send<T>(ref T packet, PacketDelivery delivery) where T : struct, INetSerializable
        {
            if (!AcceptSends) return false;
            Sent++;
            return true;
        }

        public void Subscribe<T>(Action<T> handler) where T : struct, INetSerializable => _packets.Subscribe(handler);
        public void Unsubscribe<T>(Action<T> handler) where T : struct, INetSerializable => _packets.Unsubscribe(handler);
        public void Push<T>(T packet) where T : struct, INetSerializable => _packets.Publish(packet);
    }

    public sealed class FakeScenes : ISceneLoader
    {
        public readonly List<string> Loads = new List<string>();
        public string ActiveScene { get; set; } = "Boot";

        public void Load(string sceneName, Action onLoaded = null)
        {
            Loads.Add(sceneName);
            ActiveScene = sceneName;
            onLoaded?.Invoke();
        }
    }

    public sealed class FakeConfig : IConfigProvider
    {
        public FakeConfig(ClientConfig config) => Current = config;
        public ClientConfig Current { get; set; }
        public IReadOnlyList<string> Profiles => new[] { "local" };
        public string SelectProfile(string profile) => null;
        public string SaveUserConfig(ClientConfig config) { Current = config; return null; }
        public string CommandLineRoomCode => null;
        public string CommandLineDisplayName => null;
    }

    public sealed class FakeAuth : IAuthApi
    {
        public LoginResult Result = LoginResult.Ok("jwt-token");
        public int Calls;
        public Action<LoginResult> Pending;
        public bool Defer;

        public void Login(ClientConfig config, string username, string password, Action<LoginResult> onComplete)
        {
            Calls++;
            if (Defer) Pending = onComplete;
            else onComplete(Result);
        }
    }
}
