namespace JungleMaze.Domain.Player
{
    /// <summary>
    /// Vị trí/hướng dùng để VẼ một người chơi trong frame hiện tại (đã dự đoán hoặc nội suy).
    /// Khác PlayerSnapshot: snapshot là dữ liệu server tại một tick, pose là kết quả hiển thị tại thời điểm render.
    /// </summary>
    public struct PlayerPose
    {
        public float X, Y;
        public float RotationDegrees;
        public bool IsDead;
    }
}
