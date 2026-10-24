using System.Net;
using GameServer.Configuration;
using GameServer.Diagnostics;
using GameServer.Rooms;
using JungleBackend.Protocol;
using JungleBackend.Protocol.Gameplay;
using LiteNetLib;

namespace GameServer.Networking;

/// <summary>UDP adapter; SessionHub owns all room/identity/capacity state.</summary>
public sealed class NetworkHost : IDisposable
{
    private sealed class UdpPeer(NetPeer peer) : IGamePeer
    {
        public string Transport => "Udp";
        public bool IsOpen => peer.ConnectionState != ConnectionState.Disconnected;
        public bool TrySend(byte[] packet, bool reliable) => TrySend(packet, reliable, 0);
        public bool TrySend(byte[] packet, bool reliable, byte channel)
        {
            if (!IsOpen) return false;
            peer.Send(packet, channel, reliable ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable);
            return true;
        }
        public void Close(string reason) => peer.Disconnect(NetworkProtocol.StringMessage(NetworkProtocol.JoinRejected, reason));
    }
    private readonly NetManager _manager;
    private readonly ServerOptions _options;
    private readonly Dictionary<int, UdpPeer> _peers = new();
    public SessionHub Hub { get; }
    public RoomManager Rooms => Hub.Rooms;
    public RoomCommands Commands => Hub.Commands;
    public int ReservedPeers => Hub.ReservedPeers;
    public int ActivePeers => Hub.ActivePeers;
    public int PendingPeers => Hub.PendingPeers;
    public NetworkHost(ServerOptions options, ServerMetrics metrics)
    {
        _options = options; Hub = new(options, metrics);
        var listener = new EventBasedNetListener();
        _manager = new(listener) { AutoRecycle = true, IPv6Enabled = false, DisconnectTimeout = 5000, UnconnectedMessagesEnabled = false, ChannelsCount = GameplayProtocol.ChannelsCount };
        listener.ConnectionRequestEvent += request =>
        {
            if (!NetworkProtocol.TryReadConnect(request.Data, out var join, out var reason))
            { request.Reject(NetworkProtocol.StringMessage(NetworkProtocol.JoinRejected, reason)); Hub.RecordReject(reason); return; }
            var invalid = Hub.Validate(join!, "Udp");
            if (invalid is not null)
            { request.Reject(NetworkProtocol.StringMessage(NetworkProtocol.JoinRejected, invalid)); Hub.RecordReject(invalid); return; }
            var peer = request.Accept();
            if (peer is null) return;
            var adapter = new UdpPeer(peer); _peers[peer.Id] = adapter;
            Hub.Join(join!, adapter);
        };
        listener.PeerConnectedEvent += peer => { if (_peers.TryGetValue(peer.Id, out var adapter)) Hub.Connected(adapter); };
        listener.PeerDisconnectedEvent += (peer, info) => {
            if (!_peers.Remove(peer.Id, out var adapter)) return;
            if (info.AdditionalData.AvailableBytes == 1 && info.AdditionalData.GetByte() == NetworkProtocol.ExplicitQuit) Hub.Quit(adapter);
            else Hub.Disconnect(adapter);
        };
        listener.NetworkReceiveEvent += (peer, reader, channel, method) =>
        {
            if (_peers.TryGetValue(peer.Id, out var adapter))
            {
                var raw = reader.GetRemainingBytes();
                var isGameplay = raw.Length > 0 && raw[0] == GameplayProtocol.FrameMarker;
                var validChannel = isGameplay
                    ? method == DeliveryMethod.Unreliable || (channel == GameplayProtocol.GameplayChannel && method == DeliveryMethod.ReliableOrdered)
                    : channel == GameplayProtocol.ControlChannel && method is DeliveryMethod.ReliableOrdered or DeliveryMethod.Unreliable;
                Hub.Receive(adapter, raw, method == DeliveryMethod.ReliableOrdered, validChannel);
            }
        };
        listener.NetworkErrorEvent += (_, error) => Console.Error.WriteLine($"[network-error] socket={error}");
    }
    public void Start()
    {
        if (!_manager.Start(_options.BindAddress, IPAddress.IPv6Any, _options.Port)) throw new InvalidOperationException("Không mở được UDP socket.");
        Console.WriteLine($"[ready] udp={_options.BindAddress}:{_options.Port} maxPlayers={_options.MaxPlayers} tickRate=30 authMode={_options.AuthMode} protocol=1 supported=1,2,3");
    }
    public void PollEvents() { _manager.PollEvents(); Hub.Poll(); }
    public void Dispose() { Hub.CloseAll(); _manager.Stop(); _peers.Clear(); Console.WriteLine("[stopped] UDP socket đã đóng."); }
}
