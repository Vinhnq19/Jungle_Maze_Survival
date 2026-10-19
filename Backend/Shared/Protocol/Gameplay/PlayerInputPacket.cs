using System;
using LiteNetLib.Utils;

namespace JungleBackend.Protocol.Gameplay
{
    [Flags]
    public enum InputButtons : byte
    {
        None = 0,
        Attack = 1 << 0,
        Interact = 1 << 1,
        Reload = 1 << 2,
        Dash = 1 << 3,
        UseItem = 1 << 4
    }

    /// <summary>Client → server mỗi tick (30Hz), Unreliable/Sequenced. Payload 11 byte, chỉ kiểu cố định.</summary>
    public struct PlayerInputPacket : INetSerializable
    {
        public const int PayloadBytes = 11;
        private const float MoveScale = 127f;
        private const float AimScale = 65536f / 360f;

        public uint Tick;
        public ushort InputSeq;
        /// <summary>Hướng di chuyển lượng tử −127..127; −128 bị coi là không hợp lệ.</summary>
        public sbyte MoveX, MoveY;
        /// <summary>Góc ngắm 0..65535 ứng với 0..360 độ.</summary>
        public ushort Aim;
        public InputButtons Buttons;

        public float MoveXNormalized => MoveX / MoveScale;
        public float MoveYNormalized => MoveY / MoveScale;
        public float AimDegrees => Aim / AimScale;

        public bool IsValid => MoveX != sbyte.MinValue && MoveY != sbyte.MinValue;

        /// <summary>Clamp về [-1, 1] rồi lượng tử; NaN coi như 0 để không gửi rác lên server.</summary>
        public void SetMove(float x, float y)
        {
            MoveX = Quantize(x);
            MoveY = Quantize(y);
        }

        public void SetAimDegrees(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees)) degrees = 0f;
            var wrapped = degrees % 360f;
            if (wrapped < 0f) wrapped += 360f;
            Aim = (ushort)((int)Math.Round(wrapped * AimScale) & 0xFFFF);
        }

        public bool Has(InputButtons button) => (Buttons & button) == button;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Tick);
            writer.Put(InputSeq);
            writer.Put(MoveX);
            writer.Put(MoveY);
            writer.Put(Aim);
            writer.Put((byte)Buttons);
        }

        public void Deserialize(NetDataReader reader)
        {
            Tick = reader.GetUInt();
            InputSeq = reader.GetUShort();
            MoveX = reader.GetSByte();
            MoveY = reader.GetSByte();
            Aim = reader.GetUShort();
            Buttons = (InputButtons)reader.GetByte();
        }

        private static sbyte Quantize(float value)
        {
            if (float.IsNaN(value)) return 0;
            if (value > 1f) value = 1f;
            else if (value < -1f) value = -1f;
            return (sbyte)Math.Round(value * MoveScale);
        }
    }
}
