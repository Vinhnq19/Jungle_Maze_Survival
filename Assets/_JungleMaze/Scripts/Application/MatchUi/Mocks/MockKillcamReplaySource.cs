#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using JungleMaze.Domain.MatchUi;

namespace JungleMaze.App.MatchUi.Mocks
{
    /// <summary>
    /// Dev-only replay source with the six cases of 4.4 (AC35): full 150 frames with known positions, none, short
    /// (90 frames), slow (answer after 1.5 s), other match, other victim. One clip allocated up front and reused.
    /// The killer walks +3 cells/s along X from (10, 20); the victim stands at (16, 20); nobody else is in the clip.
    /// </summary>
    public sealed class MockKillcamReplaySource : IKillcamReplaySource
    {
        public enum Case : byte { Full, None, Short, Slow, WrongMatch, WrongPlayer }

        public const float KillerStartX = 10f;
        public const float KillerY = 20f;
        public const float KillerSpeed = 3f;
        public const float VictimX = 16f;
        public const int ShortFrames = 90;
        public const float SlowSeconds = 1.5f;

        private readonly Func<double> _clock;
        private readonly KillcamClip _clip;
        private bool _pending;
        private double _requestedAt;

        public MockKillcamReplaySource(Func<double> clock, int frameCapacity = 150)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _clip = new KillcamClip(frameCapacity);
        }

        public Case Mode { get; set; }
        public int RequestCount { get; private set; }
        public int CancelCount { get; private set; }
        public int ReleaseCount { get; private set; }
        public KillcamRequest LastRequest { get; private set; }

        /// <summary>Frames currently held (0 after release: AC42).</summary>
        public int HeldFrames => _clip.FrameCount;

        public static float KillerXAt(int frame) => KillerStartX + KillerSpeed * frame / KillcamClip.FramesPerSecond;

        public void Request(in KillcamRequest request)
        {
            RequestCount++;
            LastRequest = request;
            _pending = true;
            _requestedAt = _clock();
            _clip.Clear();
            if (Mode == Case.None) return;
            var frames = Math.Min(Mode == Case.Short ? ShortFrames : request.FrameCount, _clip.FrameCapacity);
            _clip.MatchId = Mode == Case.WrongMatch ? Guid.NewGuid() : request.MatchId;
            _clip.VictimNetId = Mode == Case.WrongPlayer ? (byte)((request.VictimNetId + 1) % KillcamClip.MaxPlayers) : request.VictimNetId;
            _clip.KillerNetId = request.KillerNetId;
            _clip.EndTick = request.EliminationTick;
            _clip.FrameCount = frames;
            for (var f = 0; f < frames; f++)
            {
                _clip.Set(f, request.KillerNetId, KillerXAt(f), KillerY, 80);
                _clip.Set(f, request.VictimNetId, VictimX, KillerY, (byte)Math.Max(0, 100 - f));
                _clip.ZoneX[f] = 25f;
                _clip.ZoneY[f] = 25f;
                _clip.ZoneRadius[f] = 20f;
            }
        }

        public bool TryTake(out KillcamClip clip, out KillcamUnavailableReason reason)
        {
            clip = null;
            reason = KillcamUnavailableReason.None;
            if (!_pending) return false;
            if (Mode == Case.Slow && _clock() - _requestedAt < SlowSeconds) return false;
            _pending = false;
            if (Mode == Case.None) { reason = KillcamUnavailableReason.NoData; return true; }
            clip = _clip;
            return true;
        }

        public void Cancel()
        {
            CancelCount++;
            _pending = false;
        }

        public void Release()
        {
            ReleaseCount++;
            _pending = false;
            _clip.Clear();
        }
    }
}
#endif
