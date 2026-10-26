using System;
using JungleMaze.App.Match;
using JungleMaze.App.Ports;
using JungleMaze.Domain.Player;

namespace JungleMaze.App.Player
{
    /// <summary>
    /// Chọn nguồn pose cho từng NetId: nhân vật của mình → prediction (MOV-02); người khác, hoặc chính mình khi
    /// prediction chưa chạy (chết, chưa có map) → interpolation (MOV-03). View chỉ biết interface này.
    /// </summary>
    public sealed class PlayerPoseSource : IPlayerPoseSource
    {
        private readonly MatchSession _match;
        private readonly RemotePlayerInterpolator _remote;

        public PlayerPoseSource(MatchSession match, RemotePlayerInterpolator remote)
        {
            _match = match ?? throw new ArgumentNullException(nameof(match));
            _remote = remote ?? throw new ArgumentNullException(nameof(remote));
        }

        public bool TryGetPose(byte netId, out PlayerPose pose)
        {
            if (netId == _match.OwnNetId && _match.Prediction.TryGetPose(out pose)) return true;
            return _remote.TryGetPose(netId, out pose);
        }
    }
}
