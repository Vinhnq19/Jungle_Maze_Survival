using LiteNetLib.Utils;

namespace JungleBackend.Protocol.Gameplay
{
    /// <summary>Ten vũ khí trong GDD (6 cận chiến gồm tay không, 4 súng). Thứ tự khớp balance/weapons.json; chỉ thêm ở cuối.</summary>
    public enum CombatWeaponId : byte
    {
        Hand = 0,
        TreeBranch = 1,
        Knife = 2,
        Bone = 3,
        Axe = 4,
        Mace = 5,
        RubberGun = 6,
        Scepter = 7,
        Crossbow = 8,
        Shotgun = 9
    }

    /// <summary>Kết quả xử lý một request đánh/nạp. Chỉ <see cref="Accepted"/> mới có hiệu lực trên server.</summary>
    public enum CombatOutcome : byte
    {
        Accepted = 0,
        Duplicate = 1,
        Dead = 2,
        InvalidWeapon = 3,
        Cooldown = 4,
        NoAmmo = 5,
        Reloading = 6,
        MagazineFull = 7,
        NoReserve = 8,
        RateLimited = 9
    }

    /// <summary>Client → server, ReliableOrdered trên GameplayChannel. RequestSeq tăng dần theo từng người chơi để chống xử lý trùng.</summary>
    public struct AttackRequestPacket : INetSerializable
    {
        public const int PayloadBytes = 5;
        public ushort RequestSeq;
        public CombatWeaponId Weapon;
        /// <summary>Góc ngắm 0..65535 ứng với 0..360 độ, cùng scale với PlayerInputPacket.Aim.</summary>
        public ushort Aim;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(RequestSeq);
            writer.Put((byte)Weapon);
            writer.Put(Aim);
        }

        public void Deserialize(NetDataReader reader)
        {
            RequestSeq = reader.GetUShort();
            Weapon = (CombatWeaponId)reader.GetByte();
            Aim = reader.GetUShort();
        }
    }

    /// <summary>Client → server, ReliableOrdered. Nạp đạn cho súng đang cầm; vũ khí cận chiến bỏ qua server.</summary>
    public struct ReloadRequestPacket : INetSerializable
    {
        public const int PayloadBytes = 3;
        public ushort RequestSeq;
        public CombatWeaponId Weapon;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(RequestSeq);
            writer.Put((byte)Weapon);
        }

        public void Deserialize(NetDataReader reader)
        {
            RequestSeq = reader.GetUShort();
            Weapon = (CombatWeaponId)reader.GetByte();
        }
    }

    /// <summary>Server → client, phản hồi cho đúng một AttackRequest. Client chỉ xác nhận hoặc hủy hiệu ứng dự đoán, không tự tính damage.</summary>
    public struct AttackResultPacket : INetSerializable
    {
        public const int PayloadBytes = 9;
        public ushort RequestSeq;
        public CombatWeaponId Weapon;
        public CombatOutcome Outcome;
        public uint Tick;
        public byte Magazine;
        public byte NetId;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(RequestSeq);
            writer.Put((byte)Weapon);
            writer.Put((byte)Outcome);
            writer.Put(Tick);
            writer.Put(Magazine);
            writer.Put(NetId);
        }

        public void Deserialize(NetDataReader reader)
        {
            RequestSeq = reader.GetUShort();
            Weapon = (CombatWeaponId)reader.GetByte();
            Outcome = (CombatOutcome)reader.GetByte();
            Tick = reader.GetUInt();
            Magazine = reader.GetByte();
            NetId = reader.GetByte();
        }
    }

    /// <summary>Server → client, phản hồi một ReloadRequest. ReloadEndTick là tick server sẽ nạp xong.</summary>
    public struct ReloadResultPacket : INetSerializable
    {
        public const int PayloadBytes = 12;
        public ushort RequestSeq;
        public CombatWeaponId Weapon;
        public CombatOutcome Outcome;
        public uint Tick;
        public uint ReloadEndTick;
        public byte Magazine;
        public byte NetId;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(RequestSeq);
            writer.Put((byte)Weapon);
            writer.Put((byte)Outcome);
            writer.Put(Tick);
            writer.Put(ReloadEndTick);
            writer.Put(Magazine);
            writer.Put(NetId);
        }

        public void Deserialize(NetDataReader reader)
        {
            RequestSeq = reader.GetUShort();
            Weapon = (CombatWeaponId)reader.GetByte();
            Outcome = (CombatOutcome)reader.GetByte();
            Tick = reader.GetUInt();
            ReloadEndTick = reader.GetUInt();
            Magazine = reader.GetByte();
            NetId = reader.GetByte();
        }
    }
}
