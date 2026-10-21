using System;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Network;
using JungleMaze.Domain.Player;

namespace JungleMaze.App.Network
{
    /// <summary>
    /// PlayerStatePacket → Domain PlayerModel → event PlayerStateUpdated. View chỉ đọc model, không đọc packet,
    /// nên đổi wire format không phải sửa Presentation. Rời phòng thì xóa model và báo PlayerRemoved.
    /// </summary>
    public sealed class WorldSyncService : IInitializable, IDisposable
    {
        private readonly IPacketReceiver _packets;
        private readonly IEventBus _bus;
        private readonly Action<PlayerStatePacket> _onState;
        private readonly Action<NetworkStateChanged> _onNetwork;
        private readonly Action<byte> _publishRemoved;

        public WorldSyncService(IPacketReceiver packets, IEventBus bus, PlayerRegistry players)
        {
            _packets = packets ?? throw new ArgumentNullException(nameof(packets));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            Players = players ?? throw new ArgumentNullException(nameof(players));
            _onState = OnPlayerState;
            _onNetwork = OnNetworkStateChanged;
            _publishRemoved = id => _bus.Publish(new PlayerRemoved(id));
        }

        public PlayerRegistry Players { get; }
        public int IgnoredPackets { get; private set; }

        public void Initialize()
        {
            _packets.Subscribe(_onState);
            _bus.Subscribe(_onNetwork);
        }

        public void Dispose()
        {
            _packets.Unsubscribe(_onState);
            _bus.Unsubscribe(_onNetwork);
        }

        private void OnPlayerState(PlayerStatePacket packet)
        {
            if (!packet.IsValid) { IgnoredPackets++; return; }
            var snapshot = new PlayerSnapshot
            {
                Tick = packet.Tick,
                X = packet.X,
                Y = packet.Y,
                RotationDegrees = packet.RotationDegrees,
                Hp = packet.Hp,
                IsDead = packet.IsDead,
                LastProcessedInputSeq = packet.LastProcessedInputSeq
            };
            if (Players.GetOrAdd(packet.NetId).Apply(snapshot)) _bus.Publish(new PlayerStateUpdated(packet.NetId));
            else IgnoredPackets++;
        }

        private void OnNetworkStateChanged(NetworkStateChanged change)
        {
            if (change.Current == NetworkState.Disconnected || change.Current == NetworkState.Failed)
                Players.Clear(_publishRemoved);
        }
    }
}
