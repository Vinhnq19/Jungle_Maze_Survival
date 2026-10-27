using JungleMaze.Domain.MatchUi;

namespace JungleMaze.App.MatchUi
{
    /// <summary>
    /// Live <see cref="IKillcamReplaySource"/> until SRV-03 publishes replays: every request answers "no data" at once,
    /// so an enabled killcam falls back with a notice instead of waiting. Replaced by a network client later without
    /// touching <see cref="KillcamSession"/> or the view (4.4).
    /// </summary>
    public sealed class UnavailableKillcamReplaySource : IKillcamReplaySource
    {
        private bool _pending;

        public void Request(in KillcamRequest request) => _pending = true;

        public bool TryTake(out KillcamClip clip, out KillcamUnavailableReason reason)
        {
            clip = null;
            reason = KillcamUnavailableReason.NoData;
            if (!_pending) return false;
            _pending = false;
            return true;
        }

        public void Cancel() => _pending = false;
        public void Release() => _pending = false;
    }
}
