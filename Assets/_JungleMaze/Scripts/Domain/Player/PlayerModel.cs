namespace JungleMaze.Domain.Player
{
    /// <summary>Trạng thái server gửi về cho một người chơi, đã tách khỏi wire format.</summary>
    public struct PlayerSnapshot
    {
        public uint Tick;
        public float X, Y;
        public float RotationDegrees;
        public byte Hp;
        public bool IsDead;
        public ushort LastProcessedInputSeq;
    }

    /// <summary>Model phía client của một người chơi. Server authoritative: client chỉ áp snapshot mới hơn.</summary>
    public sealed class PlayerModel
    {
        public PlayerModel(byte netId) => NetId = netId;

        public byte NetId { get; }
        public bool HasState { get; private set; }
        public PlayerSnapshot Current { get; private set; }

        /// <summary>Bỏ snapshot cũ/trùng (packet Unreliable có thể đến sai thứ tự). Trả true nếu đã áp.</summary>
        public bool Apply(in PlayerSnapshot snapshot)
        {
            if (HasState && snapshot.Tick <= Current.Tick) return false;
            Current = snapshot;
            HasState = true;
            return true;
        }
    }

    /// <summary>Intent điều khiển từ Presentation (bàn phím/tay cầm), chưa lượng tử hóa.</summary>
    public struct PlayerIntent
    {
        public float MoveX, MoveY;
        public float AimDegrees;
        public bool Attack, Interact, Reload, Dash, UseItem;
    }

    public readonly struct PlayerStateUpdated
    {
        public readonly byte NetId;
        public PlayerStateUpdated(byte netId) => NetId = netId;
    }

    public readonly struct PlayerRemoved
    {
        public readonly byte NetId;
        public PlayerRemoved(byte netId) => NetId = netId;
    }
}
