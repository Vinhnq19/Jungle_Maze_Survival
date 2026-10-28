using LiteNetLib.Utils;

namespace JungleBackend.Protocol.Gameplay
{
    /// <summary>The ten GDD weapons (six melee including bare hands, four ranged). Order matches balance/weapons.json; append new ids only.</summary>
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

    /// <summary>Outcome of one attack or reload request. Only <see cref="Accepted"/> takes effect on the server.</summary>
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

    /// <summary>Client → server, ReliableOrdered on GameplayChannel. RequestSeq increases per player and guards against duplicate processing.</summary>
    public struct AttackRequestPacket : INetSerializable
    {
        public const int PayloadBytes = 5;
        public ushort RequestSeq;
        public CombatWeaponId Weapon;
        /// <summary>Aim angle in 0..65535 for 0..360 degrees, same scale as PlayerInputPacket.Aim.</summary>
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

    /// <summary>Client → server, ReliableOrdered. Reloads the ranged weapon being held; the server rejects melee weapons.</summary>
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

    /// <summary>Server → client, reply to one AttackRequest. The client only confirms or cancels its predicted effects and never computes damage.</summary>
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

    /// <summary>Server → client, reply to one ReloadRequest. ReloadEndTick is the tick when the server finishes the reload.</summary>
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
