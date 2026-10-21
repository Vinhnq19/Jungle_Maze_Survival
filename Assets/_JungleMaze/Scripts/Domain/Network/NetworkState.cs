namespace JungleMaze.Domain.Network
{
    /// <summary>Trạng thái kết nối client. Joined là lúc server đã trả JoinAccepted.</summary>
    public enum NetworkState : byte
    {
        Disconnected = 0,
        Connecting = 1,
        Authenticating = 2,
        Joined = 3,
        Failed = 4
    }

    /// <summary>Publish lên EventBus mỗi lần đổi trạng thái; UI/State chỉ nghe event này.</summary>
    public readonly struct NetworkStateChanged
    {
        public readonly NetworkState Previous;
        public readonly NetworkState Current;
        public readonly DisconnectReason Reason;

        public NetworkStateChanged(NetworkState previous, NetworkState current, DisconnectReason reason)
        {
            Previous = previous;
            Current = current;
            Reason = reason;
        }
    }

    /// <summary>Cấp khi JoinAccepted; PlayerId là chuỗi server cấp (chưa có NetId dạng byte).</summary>
    public readonly struct JoinedRoom
    {
        public readonly string PlayerId;
        public JoinedRoom(string playerId) => PlayerId = playerId;
    }
}
