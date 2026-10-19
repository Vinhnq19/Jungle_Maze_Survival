namespace JungleBackend.Protocol.Gameplay
{
    /// <summary>
    /// Packet gameplay đi channel riêng qua NetPacketProcessor; channel 0 giữ nguyên handshake v1/v2.
    /// Server cần ChannelsCount = 2 trước khi nhận packet gameplay.
    /// </summary>
    public static class GameplayProtocol
    {
        public const byte ControlChannel = 0;
        public const byte GameplayChannel = 1;
        public const byte ChannelsCount = 2;
        public const int TickRate = 30;
    }

    /// <summary>Cách gửi không phụ thuộc kiểu LiteNetLib để lớp Application không chạm transport.</summary>
    public enum PacketDelivery : byte
    {
        Unreliable = 0,
        Sequenced = 1,
        ReliableOrdered = 2
    }
}
