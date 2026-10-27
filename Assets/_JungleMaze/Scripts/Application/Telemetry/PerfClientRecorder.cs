using System;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Match;
using JungleMaze.App.Player;
using JungleMaze.App.Ports;
using JungleMaze.Core.Ticking;
using JungleMaze.Domain.Telemetry;

namespace JungleMaze.App.Telemetry
{
    /// <summary>
    /// TEL-01 perf_client: every 5 s of InGame, one record of frame times, RTT, jitter, ping loss and player count.
    /// Runs on the main thread each frame with no allocation; the sink serializes and writes on its own thread.
    /// The window restarts whenever the client is not connected in InGame (menu, loading, reconnecting), so no record
    /// mixes match and non-match frames. Reads only ids, ticks and stats: no names, room code or tokens.
    /// </summary>
    public sealed class PerfClientRecorder : ITickable
    {
        public const float DefaultWindowSeconds = 5f;
        public const int MaxEntities = 10;
        private const float WindowEpsilon = 1e-3f;
        private readonly MatchSession _match;
        private readonly INetworkClient _network;
        private readonly RemotePlayerInterpolator _remote;
        private readonly ITelemetrySink _sink;
        private readonly Func<long> _utcMs;
        private readonly FrameStats _frames = new FrameStats();
        private float _elapsed;
        private bool _open;
        private int _pingsAtStart, _lostAtStart;

        public PerfClientRecorder(MatchSession match, INetworkClient network, RemotePlayerInterpolator remote, ITelemetrySink sink,
            Func<long> utcMs = null)
        {
            _match = match ?? throw new ArgumentNullException(nameof(match));
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _remote = remote ?? throw new ArgumentNullException(nameof(remote));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _utcMs = utcMs ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public float WindowSeconds { get; set; } = DefaultWindowSeconds;
        public int Written { get; private set; }
        /// <summary>Windows closed without a record: unknown own NetId or config hash, or a sink that refused it.</summary>
        public int Skipped { get; private set; }

        public void Tick(float deltaTime)
        {
            if (!_sink.Enabled || !_match.Connected || _match.Phase != MatchPhase.InGame)
            {
                _open = false;
                return;
            }
            if (!_open) Open();
            _frames.Add(deltaTime);
            _elapsed += deltaTime;
            if (_elapsed + WindowEpsilon < WindowSeconds) return; // float sums of frame times land a hair under 5 s
            Close();
            Open();
        }

        private void Open()
        {
            _open = true;
            _elapsed = 0f;
            _frames.Reset();
            var stats = _network.Stats;
            _pingsAtStart = stats.PingsSent;
            _lostAtStart = stats.PongsLost;
        }

        private void Close()
        {
            var phase = _match.PhaseState;
            if (_match.OwnNetId > 9 || string.IsNullOrEmpty(phase.ConfigHash) || !_frames.TrySummarize(out var frames))
            {
                Skipped++;
                return;
            }
            var stats = _network.Stats;
            var remote = _remote.Stats;
            var pings = stats.PingsSent - _pingsAtStart;
            var tick = _match.ServerTick;
            var sample = new PerfClientSample
            {
                MatchId = _match.MatchId,
                Tick = tick,
                MatchTick = phase.Phase == MatchPhase.InGame && tick >= phase.PhaseStartTick ? tick - phase.PhaseStartTick : -1,
                UtcMs = _utcMs(),
                Cfg = phase.ConfigVersion,
                CfgHash = phase.ConfigHash,
                NetId = _match.OwnNetId,
                WindowSec = frames.WindowSeconds,
                Frames = frames.Frames,
                FpsAvg = frames.FpsAvg,
                Fps1Low = frames.Fps1Low,
                FrameMsP95 = frames.FrameMsP95,
                FrameMsMax = frames.FrameMsMax,
                Transport = TransportOf(stats.Transport),
                RttP50 = stats.PongsReceived > 0 ? stats.P50RttMs : double.NaN,
                RttP95 = stats.PongsReceived > 0 ? stats.P95RttMs : double.NaN,
                JitterMs = _remote.ClockStarted ? remote.JitterMs : double.NaN,
                LossPct = pings > 0 ? Math.Min(100.0, Math.Max(0.0, 100.0 * (stats.PongsLost - _lostAtStart) / pings)) : double.NaN,
                Entities = Math.Min(MaxEntities, _match.Players.Count)
            };
            if (_sink.TryWrite(in sample)) Written++;
            else Skipped++;
        }

        /// <summary>NetworkClient names transports "Udp"/"Wss"; anything else is unknown (null).</summary>
        public static TelemetryTransport TransportOf(string transport)
        {
            if (string.Equals(transport, "Udp", StringComparison.OrdinalIgnoreCase)) return TelemetryTransport.Udp;
            if (string.Equals(transport, "Wss", StringComparison.OrdinalIgnoreCase)) return TelemetryTransport.Wss;
            return TelemetryTransport.None;
        }
    }
}
