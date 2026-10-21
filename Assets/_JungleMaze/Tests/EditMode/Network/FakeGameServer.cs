using System;
using JungleBackend.Protocol;
using JungleBackend.Protocol.Gameplay;
using LiteNetLib;
using LiteNetLib.Utils;

namespace JungleMaze.Tests.Network
{
    /// <summary>
    /// Server LiteNetLib tối giản trên loopback, làm theo handshake của GameServer (JoinRejected trong reject data,
    /// AuthRequired → Auth cho Jwt, Ping → Pong). Channel 1 echo PlayerStatePacket để test packet gameplay.
    /// </summary>
    public sealed class FakeGameServer : IDisposable
    {
        private readonly NetManager _manager;
        private readonly PacketRegistry _registry = new PacketRegistry();
        private readonly NetDataWriter _writer = new NetDataWriter();
        private NetPeer _peer;

        public string RoomCode = "ROOM42";
        public bool RoomFull;
        public bool RequireJwt;
        public bool AnswerPings = true;
        public string ReceivedToken;
        public JoinRequest LastRequest;
        public int Pings, GameplayPackets, Disconnects;
        public PlayerInputPacket LastInput;

        public FakeGameServer()
        {
            var listener = new EventBasedNetListener();
            _manager = new NetManager(listener) { AutoRecycle = true, IPv6Enabled = false, ChannelsCount = GameplayProtocol.ChannelsCount };
            listener.ConnectionRequestEvent += request =>
            {
                if (!NetworkProtocol.TryReadConnect(request.Data, out var join, out var reason))
                { request.Reject(NetworkProtocol.StringMessage(NetworkProtocol.JoinRejected, reason)); return; }
                LastRequest = join;
                if (join.RoomCode != RoomCode) { request.Reject(NetworkProtocol.StringMessage(NetworkProtocol.JoinRejected, "InvalidRoomCode")); return; }
                if (RoomFull) { request.Reject(NetworkProtocol.StringMessage(NetworkProtocol.JoinRejected, "RoomFull")); return; }
                _peer = request.Accept();
            };
            listener.PeerConnectedEvent += peer =>
            {
                if (RequireJwt) peer.Send(new[] { NetworkProtocol.AuthRequired }, DeliveryMethod.ReliableOrdered);
                else peer.Send(NetworkProtocol.StringMessage(NetworkProtocol.JoinAccepted, "player-1"), DeliveryMethod.ReliableOrdered);
            };
            listener.PeerDisconnectedEvent += (_, __) => Disconnects++;
            listener.NetworkReceiveEvent += (peer, reader, channel, method) =>
            {
                if (PacketRegistry.IsFrame(reader)) { _registry.TryReadFrame(reader, peer); return; }
                var type = reader.PeekByte();
                if (type == NetworkProtocol.Auth
                    && NetworkProtocol.TryReadStringMessage(reader, NetworkProtocol.Auth, NetworkProtocol.MaxAuthBytes, NetworkProtocol.MaxTokenChars, out var token))
                {
                    ReceivedToken = token;
                    peer.Send(NetworkProtocol.StringMessage(NetworkProtocol.JoinAccepted, "player-jwt"), DeliveryMethod.ReliableOrdered);
                    return;
                }
                if (NetworkProtocol.TryRead(reader, NetworkProtocol.Ping, out var sequence))
                {
                    Pings++;
                    if (!AnswerPings) return;
                    NetworkProtocol.Write(_writer, NetworkProtocol.Pong, sequence);
                    peer.Send(_writer, DeliveryMethod.Unreliable);
                }
            };
            _registry.Subscribe<PlayerInputPacket, NetPeer>((input, sender) =>
            {
                GameplayPackets++;
                LastInput = input;
                var state = new PlayerStatePacket { Tick = input.Tick, NetId = 0, X = input.MoveXNormalized, Y = input.MoveYNormalized, Hp = 100, LastProcessedInputSeq = input.InputSeq };
                _registry.WriteFrame(_writer, ref state);
                sender.Send(_writer, GameplayProtocol.GameplayChannel, DeliveryMethod.Unreliable);
            });
            if (!_manager.Start(System.Net.IPAddress.Loopback, System.Net.IPAddress.IPv6Loopback, 0))
                throw new InvalidOperationException("Không mở được socket server test.");
        }

        public int Port => _manager.LocalPort;
        public int ConnectedPeers => _manager.ConnectedPeersCount;

        public void Poll() => _manager.PollEvents();

        public void Kick() => _peer?.Disconnect();

        public void Dispose() => _manager.Stop();
    }
}
