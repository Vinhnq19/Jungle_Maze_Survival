using LiteNetLib.Utils;

namespace JungleBackend.Protocol.Gameplay
{
    /// <summary>Loại sự kiện; chỉ thêm giá trị mới ở cuối để không đổi nghĩa packet cũ.</summary>
    public enum GameEventType : byte
    {
        None = 0,
        MatchStarted = 1,
        MatchEnded = 2,
        PlayerSpawned = 3,
        PlayerHit = 4,
        PlayerDied = 5,
        ItemPickedUp = 6
    }

    /// <summary>
    /// Server → client, sự kiện rời rạc, ReliableOrdered. Hai tham số int dùng tùy loại
    /// (ví dụ PlayerHit: Param1 = damage). Sự kiện cần nhiều dữ liệu hơn thì tạo packet riêng.
    /// </summary>
    public struct EventPacket : INetSerializable
    {
        public const int PayloadBytes = 15;
        public const byte NoTarget = byte.MaxValue;

        public uint Tick;
        public GameEventType EventType;
        public byte SourceNetId;
        public byte TargetNetId;
        public int Param1;
        public int Param2;

        public bool IsValid => EventType != GameEventType.None;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Tick);
            writer.Put((byte)EventType);
            writer.Put(SourceNetId);
            writer.Put(TargetNetId);
            writer.Put(Param1);
            writer.Put(Param2);
        }

        public void Deserialize(NetDataReader reader)
        {
            Tick = reader.GetUInt();
            EventType = (GameEventType)reader.GetByte();
            SourceNetId = reader.GetByte();
            TargetNetId = reader.GetByte();
            Param1 = reader.GetInt();
            Param2 = reader.GetInt();
        }
    }
}
