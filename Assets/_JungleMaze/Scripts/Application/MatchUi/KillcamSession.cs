using System;
using JungleMaze.Domain.MatchUi;

namespace JungleMaze.App.MatchUi
{
    /// <summary>
    /// One killcam from request to end. Checks in the order of the plan (AC31, AC35): disabled (no request at all) →
    /// no killer → wait over <see cref="KillcamOptions.WaitSeconds"/> → no data → other match → other victim → window not
    /// ending near our elimination tick → fewer frames than the duration. The killer drawn is the one of the request
    /// (the server's elimination data), never one named by the clip. Playback runs on the unscaled clock, not on frame count, so a hitch does not stretch
    /// it (AC32). Data that arrives after the wait or after the killcam ended is dropped and the source released
    /// (AC36). Only the last <c>duration × 30</c> frames of a clip are ever shown (AC41).
    /// Draws nothing and computes no damage or death: it only says which frame to show (AC38).
    /// </summary>
    public sealed class KillcamSession
    {
        public enum SessionState : byte { Idle, Waiting, Playing, Finished, Failed }

        private readonly Func<double> _clock;
        private KillcamOptions _options;
        private IKillcamReplaySource _source;
        private KillcamRequest _request;
        private double _waitStart, _playStart;
        private int _frameOffset;

        public KillcamSession(Func<double> clock, KillcamOptions options, IKillcamReplaySource source)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _options = options;
            _source = source;
        }

        public KillcamOptions Options => _options;
        public SessionState State { get; private set; }
        public KillcamUnavailableReason Reason { get; private set; }
        public KillcamClip Clip { get; private set; }
        public bool Skipped { get; private set; }
        public int Requests { get; private set; }
        public KillcamRequest LastRequest => _request;
        public double PlayStartedAt => _playStart;
        public double EndedAt { get; private set; }

        public float Elapsed => State == SessionState.Playing ? (float)Math.Min(_options.DurationSeconds, _clock() - _playStart) : 0f;
        public float Remaining => State == SessionState.Playing ? Math.Max(0f, _options.DurationSeconds - Elapsed) : 0f;

        /// <summary>Fractional frame inside the clip for the current time (interpolated by the view).</summary>
        public float Frame => Clip == null ? 0f : _frameOffset + Math.Min(Elapsed * KillcamClip.FramesPerSecond, _options.FrameCount - 1);

        /// <summary>N10 switch at runtime (F7 panel, <c>-killcam</c>); takes effect from the next elimination.</summary>
        public void SetEnabled(bool enabled) =>
            _options = new KillcamOptions(enabled, _options.DurationSeconds, _options.WaitSeconds, _options.EndTickTolerance);

        public void SetSource(IKillcamReplaySource source)
        {
            Stop();
            _source = source;
        }

        /// <summary>
        /// Starts after the local elimination. False = no killcam: <see cref="Reason"/> is <see cref="KillcamUnavailableReason.Disabled"/>
        /// (silent, AC31) or a reason worth a notice.
        /// </summary>
        public bool Begin(in EliminationInfo info, byte localNetId)
        {
            Reset();
            if (!_options.Enabled) return Fail(KillcamUnavailableReason.Disabled, false);
            if (!info.HasKiller || info.KillerNetId == localNetId) return Fail(KillcamUnavailableReason.UnknownKiller, false);
            if (_source == null) return Fail(KillcamUnavailableReason.NoData, false);
            _request = new KillcamRequest(info.MatchId, localNetId, info.KillerNetId, info.EliminationTick, _options.FrameCount);
            Requests++;
            State = SessionState.Waiting;
            _waitStart = _clock();
            _source.Request(_request);
            return true;
        }

        public void Tick()
        {
            if (State == SessionState.Waiting) Wait();
            if (State == SessionState.Playing && _clock() - _playStart >= _options.DurationSeconds) End(false);
        }

        /// <summary>Space/click "Bỏ qua" (Q3): ends now and does not replay.</summary>
        public void Skip()
        {
            if (State == SessionState.Playing || State == SessionState.Waiting) End(true);
        }

        /// <summary>Match ended or the screen changed: stop at once, release the data (AC37, AC42).</summary>
        public void Stop()
        {
            if (State == SessionState.Waiting) _source?.Cancel();
            if (State == SessionState.Waiting || State == SessionState.Playing) Fail(KillcamUnavailableReason.MatchEnded, true);
            else _source?.Release();
            Clip = null;
        }

        public bool TryGetKillerPosition(out float x, out float y)
        {
            x = y = 0f;
            return Clip != null && State == SessionState.Playing && Clip.TryGetPosition(_request.KillerNetId, Frame, out x, out y);
        }

        public static string ReasonText(KillcamUnavailableReason reason, float durationSeconds = 5f)
        {
            switch (reason)
            {
                case KillcamUnavailableReason.UnknownKiller: return "không rõ kẻ hạ";
                case KillcamUnavailableReason.Timeout: return "chờ dữ liệu quá lâu";
                case KillcamUnavailableReason.NoData: return "máy chủ chưa có dữ liệu";
                case KillcamUnavailableReason.WrongMatch: return "dữ liệu không thuộc trận này";
                case KillcamUnavailableReason.WrongPlayer: return "dữ liệu không phải của bạn";
                case KillcamUnavailableReason.WrongWindow: return "dữ liệu không đúng lúc bạn bị loại";
                case KillcamUnavailableReason.TooShort: return "dữ liệu chưa đủ " + Math.Round(durationSeconds, 1) + " giây";
                case KillcamUnavailableReason.MatchEnded: return "trận đã kết thúc";
                default: return null;
            }
        }

        /// <summary>"Killcam 3,2 s" (Vietnamese decimal comma).</summary>
        public static string Countdown(float remaining)
        {
            var tenths = (int)Math.Ceiling(Math.Max(0f, remaining) * 10f - 1e-4f);
            return "Killcam " + tenths / 10 + "," + tenths % 10 + " s";
        }

        private void Wait()
        {
            if (_clock() - _waitStart > _options.WaitSeconds)
            {
                _source.Cancel();
                Fail(KillcamUnavailableReason.Timeout, true);
                return;
            }
            if (!_source.TryTake(out var clip, out var reason)) return;
            if (clip == null) { Fail(reason == KillcamUnavailableReason.None ? KillcamUnavailableReason.NoData : reason, true); return; }
            if (clip.MatchId != _request.MatchId) { Fail(KillcamUnavailableReason.WrongMatch, true); return; }
            if (clip.VictimNetId != _request.VictimNetId) { Fail(KillcamUnavailableReason.WrongPlayer, true); return; }
            // AC41: the window ends at this player's elimination tick. The request holds the client's tick estimate, so a
            // few ticks either way is the same moment; further off is someone else's moment.
            if (Math.Abs((long)clip.EndTick - _request.EliminationTick) > _options.EndTickTolerance)
            {
                Fail(KillcamUnavailableReason.WrongWindow, true);
                return;
            }
            if (clip.FrameCount < _request.FrameCount) { Fail(KillcamUnavailableReason.TooShort, true); return; }
            Clip = clip;
            _frameOffset = clip.FrameCount - _request.FrameCount;
            State = SessionState.Playing;
            _playStart = _clock();
        }

        private void End(bool skipped)
        {
            if (State == SessionState.Waiting) _source?.Cancel();
            Skipped = skipped;
            State = SessionState.Finished;
            EndedAt = _clock();
            Clip = null;
            _source?.Release();
        }

        private bool Fail(KillcamUnavailableReason reason, bool release)
        {
            Reason = reason;
            State = SessionState.Failed;
            EndedAt = _clock();
            Clip = null;
            if (release) _source?.Release();
            return false;
        }

        private void Reset()
        {
            State = SessionState.Idle;
            Reason = KillcamUnavailableReason.None;
            Skipped = false;
            Clip = null;
            _frameOffset = 0;
        }
    }
}
