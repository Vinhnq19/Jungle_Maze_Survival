using System;
using System.Globalization;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Match;
using JungleMaze.App.Ports;
using JungleMaze.Core.Logging;
using JungleMaze.Core.Ticking;

namespace JungleMaze.App.Player
{
    /// <summary>
    /// Ghi một dòng log [Mov] định kỳ khi đang trong trận: RTT, correction của prediction (MOV-02) và
    /// jitter/ngoại suy của interpolation (MOV-03). Dùng để so UDP và WSS bằng cùng một định dạng.
    /// Không ghi mã phòng, token hay tên người chơi.
    /// </summary>
    public sealed class MovementTelemetry : ITickable
    {
        public const float DefaultIntervalSeconds = 5f;
        private readonly MatchSession _match;
        private readonly INetworkClient _network;
        private readonly RemotePlayerInterpolator _remote;
        private readonly ILog _log;
        private float _elapsed;

        public MovementTelemetry(MatchSession match, INetworkClient network, RemotePlayerInterpolator remote, ILog log)
        {
            _match = match ?? throw new ArgumentNullException(nameof(match));
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _remote = remote ?? throw new ArgumentNullException(nameof(remote));
            _log = log ?? NullLog.Instance;
        }

        public float IntervalSeconds { get; set; } = DefaultIntervalSeconds;
        public string LastLine { get; private set; } = "";

        public void Tick(float deltaTime)
        {
            if (!_match.Connected || _match.Phase != MatchPhase.InGame) { _elapsed = 0f; return; }
            _elapsed += deltaTime;
            if (_elapsed < IntervalSeconds) return;
            _elapsed = 0f;
            LastLine = Format(_network.Stats.Transport, _network.Stats.P50RttMs, _network.Stats.P95RttMs, _match.Prediction.Stats, _remote.Stats);
            _log.Info(LastLine);
        }

        /// <summary>Định dạng chung cho log runtime và log test (fake server) để đối chiếu trực tiếp.</summary>
        public static string Format(string transport, double rttP50Ms, double rttP95Ms, in PredictionStats p, in InterpolationStats r) =>
            string.Format(CultureInfo.InvariantCulture,
                "[Mov] transport={0} rttP50Ms={1:F1} rttP95Ms={2:F1} ackRttMs={3:F1} predicted={4} pending={5} reconciles={6} corrections={7} snaps={8} " +
                "errLast={9:F3} errAvg={10:F3} errMax={11:F3} overflow={12} | remote jitterMs={13:F1} delayMs={14:F0} bufferedMs={15:F0} " +
                "extrapolated={16}/{17} clamped={18} dup={19} old={20} reordered={21} clockResets={22} spawns={23} despawns={24}",
                transport ?? "none", rttP50Ms, rttP95Ms, p.AverageAckRttMs, p.Predicted, p.Pending, p.Reconciles, p.Corrections, p.Snaps,
                p.LastErrorMeters, p.AverageErrorMeters, p.MaxErrorMeters, p.Overflows,
                r.JitterMs, r.RenderDelayMs, r.BufferedMs, r.ExtrapolatedSamples, r.Samples, r.ClampedSamples,
                r.Duplicates, r.TooOld, r.OutOfOrder, r.ClockResets, r.Spawns, r.Despawns);
    }
}
