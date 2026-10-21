using System;
using LiteNetLib.Utils;

namespace JungleBackend.Protocol.Gameplay
{
    [Flags]
    public enum PlayerStateFlags : byte
    {
        None = 0,
        Dead = 1 << 0,
        Invulnerable = 1 << 1,
        Attacking = 1 << 2,
        Dashing = 1 << 3
    }

    /// <summary>
    /// Server → client, trạng thái authoritative của một người chơi tại Tick. Unreliable/Sequenced.
    /// NetId là slot 0..9 do server cấp (cần bổ sung vào JoinAccepted ở bản server tiếp theo).
    /// </summary>
    public struct PlayerStatePacket : INetSerializable
    {
        public const int PayloadBytes = 19;
        private const float RotationScale = 65536f / 360f;

        public uint Tick;
        public byte NetId;
        public float X, Y;
        /// <summary>Góc quay 0..65535 ứng với 0..360 độ.</summary>
        public ushort Rotation;
        /// <summary>InputSeq cuối server đã xử lý; client dùng để reconcile prediction.</summary>
        public ushort LastProcessedInputSeq;
        public byte Hp;
        public PlayerStateFlags Flags;

        public float RotationDegrees => Rotation / RotationScale;
        public bool IsDead => (Flags & PlayerStateFlags.Dead) != 0;
        public bool IsValid => !float.IsNaN(X) && !float.IsNaN(Y) && !float.IsInfinity(X) && !float.IsInfinity(Y);

        public void SetRotationDegrees(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees)) degrees = 0f;
            var wrapped = degrees % 360f;
            if (wrapped < 0f) wrapped += 360f;
            Rotation = (ushort)((int)Math.Round(wrapped * RotationScale) & 0xFFFF);
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Tick);
            writer.Put(NetId);
            writer.Put(X);
            writer.Put(Y);
            writer.Put(Rotation);
            writer.Put(LastProcessedInputSeq);
            writer.Put(Hp);
            writer.Put((byte)Flags);
        }

        public void Deserialize(NetDataReader reader)
        {
            Tick = reader.GetUInt();
            NetId = reader.GetByte();
            X = reader.GetFloat();
            Y = reader.GetFloat();
            Rotation = reader.GetUShort();
            LastProcessedInputSeq = reader.GetUShort();
            Hp = reader.GetByte();
            Flags = (PlayerStateFlags)reader.GetByte();
        }
    }
}
