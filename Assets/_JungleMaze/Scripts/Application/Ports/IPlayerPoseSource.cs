using JungleMaze.Domain.Player;

namespace JungleMaze.App.Ports
{
    /// <summary>
    /// Nguồn pose cho view. View không cần biết pose đến từ prediction (MOV-02, nhân vật của mình)
    /// hay interpolation (MOV-03, người chơi khác). Trả false khi chưa có dữ liệu: view tự fallback.
    /// </summary>
    public interface IPlayerPoseSource
    {
        bool TryGetPose(byte netId, out PlayerPose pose);
    }
}
