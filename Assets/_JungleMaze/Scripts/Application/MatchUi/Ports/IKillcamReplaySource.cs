using System;
using JungleMaze.Domain.MatchUi;

namespace JungleMaze.App.MatchUi
{
    /// <summary>
    /// N9 request: only the current match, our own NetId, the killer named by the server and the elimination tick
    /// (AC40). The source answers with that 5 s window or a reason.
    /// </summary>
    public readonly struct KillcamRequest
    {
        public readonly Guid MatchId;
        public readonly byte VictimNetId;
        public readonly byte KillerNetId;
        public readonly uint EliminationTick;
        public readonly int FrameCount;

        public KillcamRequest(Guid matchId, byte victimNetId, byte killerNetId, uint eliminationTick, int frameCount)
        {
            MatchId = matchId;
            VictimNetId = victimNetId;
            KillerNetId = killerNetId;
            EliminationTick = eliminationTick;
            FrameCount = frameCount;
        }
    }

    /// <summary>
    /// N10. <see cref="Enabled"/> is off by default (Q1) and comes from the client asset; the duration is
    /// <c>MatchConfigData.KillcamDurationSeconds</c> (5.0); the wait is G-3 (1.0 s). <see cref="EndTickTolerance"/> is
    /// how far the clip's end tick may sit from our elimination tick: the request carries the client's own tick count at
    /// the moment the event arrived, the server stamps the clip with its own, so a few ticks apart is the same moment.
    /// </summary>
    public readonly struct KillcamOptions
    {
        public readonly bool Enabled;
        public readonly float DurationSeconds;
        public readonly float WaitSeconds;
        public readonly int EndTickTolerance;

        public const int DefaultEndTickTolerance = 30;

        public KillcamOptions(bool enabled, float durationSeconds, float waitSeconds, int endTickTolerance = DefaultEndTickTolerance)
        {
            Enabled = enabled;
            DurationSeconds = durationSeconds > 0f ? durationSeconds : 5f;
            WaitSeconds = waitSeconds > 0f ? waitSeconds : 1f;
            EndTickTolerance = endTickTolerance >= 0 ? endTickTolerance : DefaultEndTickTolerance;
        }

        public int FrameCount => (int)Math.Round(DurationSeconds * KillcamClip.FramesPerSecond);
    }

    /// <summary>
    /// N9: where replay frames come from. Live today: <c>UnavailableKillcamReplaySource</c> (server has not published
    /// replays yet, SRV-03). Dev: <c>MockKillcamReplaySource</c> with six cases. The source owns one pre-allocated clip.
    /// </summary>
    public interface IKillcamReplaySource
    {
        void Request(in KillcamRequest request);

        /// <summary>
        /// False while the answer is pending. True once answered: <paramref name="clip"/> holds the data, or is null
        /// and <paramref name="reason"/> says why.
        /// </summary>
        bool TryTake(out KillcamClip clip, out KillcamUnavailableReason reason);

        /// <summary>Stop waiting; a late answer must be dropped (AC36).</summary>
        void Cancel();

        /// <summary>Forget the clip data (end of killcam, end of match: G-14, AC42).</summary>
        void Release();
    }
}
