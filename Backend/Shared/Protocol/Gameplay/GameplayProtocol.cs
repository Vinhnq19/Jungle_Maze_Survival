namespace JungleBackend.Protocol.Gameplay
{
    /// <summary>
    /// Frame gameplay = 1 byte FrameMarker + các packet NetPacketProcessor (hash 8 byte + payload).
    /// LiteNetLib không gửi số channel cho packet Unreliable (luôn tới channel 0), nên bên nhận phân biệt
    /// frame gameplay với message handshake (type 1..6) bằng byte đầu, không bằng channel.
    /// Channel 1 chỉ dùng cho gameplay Reliable/Sequenced để không chặn hàng với handshake.
    /// </summary>
    public static class GameplayProtocol
    {
        public const byte ControlChannel = 0;
        public const byte GameplayChannel = 1;
        public const byte ChannelsCount = 2;
        public const int TickRate = 30;
        /// <summary>Message control phải giữ type &lt; 0x80 để không trùng marker này.</summary>
        public const byte FrameMarker = 0x80;
    }

    /// <summary>Cách gửi không phụ thuộc kiểu LiteNetLib để lớp Application không chạm transport.</summary>
    public enum PacketDelivery : byte
    {
        Unreliable = 0,
        Sequenced = 1,
        ReliableOrdered = 2
    }
}
