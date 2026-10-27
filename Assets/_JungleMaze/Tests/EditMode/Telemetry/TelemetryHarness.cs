using System;
using System.Collections.Generic;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Match;
using JungleMaze.App.Player;
using JungleMaze.App.Ports;
using JungleMaze.App.Telemetry;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Network;
using JungleMaze.Domain.Player;
using JungleMaze.Domain.Telemetry;
using JungleMaze.Tests.App;

namespace JungleMaze.Tests.Telemetry
{
    /// <summary>Network port whose stats the test sets; state and packets come from <see cref="FakeNetwork"/>.</summary>
    internal sealed class StatsNetwork : INetworkClient
    {
        private readonly FakeNetwork _inner;
        public NetworkStats Value;

        public StatsNetwork(FakeNetwork inner) => _inner = inner;

        public NetworkState State => _inner.State;
        public DisconnectReason LastReason => _inner.LastReason;
        public string PlayerId => _inner.PlayerId;
        public string Endpoint => _inner.Endpoint;
        public NetworkStats Stats => Value;
        public void Connect(in ConnectRequest request) => _inner.Connect(request);
        public void Disconnect() => _inner.Disconnect();
    }

    /// <summary>Sink that keeps samples in memory (preallocated, so steady-state ticks stay allocation-free).</summary>
    internal sealed class MemorySink : ITelemetrySink
    {
        public readonly List<PerfClientSample> Samples = new List<PerfClientSample>(256);
        public bool Accept = true;
        public bool Enabled { get; set; } = true;
        public long Dropped { get; private set; }

        public bool TryWrite(in PerfClientSample sample)
        {
            if (!Accept || Samples.Count == Samples.Capacity) { Dropped++; return false; }
            Samples.Add(sample);
            return true;
        }
    }

    /// <summary>A real MatchSession + RemotePlayerInterpolator over fake packets, with a recorder on top.</summary>
    internal sealed class TelemetryHarness : IDisposable
    {
        public const string ConfigHash = "3fa1c2d0aa55aa55aa55aa55aa55aa55aa55aa55aa55aa55aa55aa55aa55aa55";
        public readonly EventBus Bus = new EventBus();
        public readonly FakeNetwork Fake;
        public readonly StatsNetwork Network;
        public readonly PlayerRegistry Players = new PlayerRegistry();
        public readonly MatchSession Match;
        public readonly RemotePlayerInterpolator Remote;
        public readonly PerfClientRecorder Recorder;
        public readonly Guid MatchId = Guid.NewGuid();
        public uint Tick = 1000;
        public long Utc = 1_793_000_000_000;

        public TelemetryHarness(ITelemetrySink sink)
        {
            Fake = new FakeNetwork(Bus);
            Network = new StatsNetwork(Fake);
            Match = new MatchSession(Network, Fake, Fake, Bus, Players);
            Match.Initialize();
            Remote = new RemotePlayerInterpolator(Bus, Players);
            Remote.Initialize();
            Recorder = new PerfClientRecorder(Match, Network, Remote, sink, () => Utc);
            Fake.SetState(NetworkState.Joined, DisconnectReason.None);
            Network.Value = new NetworkStats { Transport = "Udp", P50RttMs = 21.5, P95RttMs = 38.25, PongsReceived = 4 };
        }

        public void Phase(MatchPhase phase, uint startTick) =>
            Fake.Push(new MatchPhasePacket
            {
                MatchId = MatchId, Phase = phase, ServerTick = Tick, PhaseStartTick = startTick, ConfigVersion = "1.0.0",
                ConfigHash = ConfigHash, MinPlayers = 1, MaxPlayers = 10, PlayerCount = 4
            });

        /// <summary>InGame from the current tick, own NetId 2, a roster with sample names, four players in a snapshot.</summary>
        public void StartInGame(string displayName = "Zebra-Player")
        {
            Phase(MatchPhase.InGame, Tick);
            Fake.Push(new ResumeTokenPacket { MatchId = MatchId, NetId = 2, ResumeToken = Guid.NewGuid() });
            var roster = new MatchParticipantState[4];
            for (byte i = 0; i < roster.Length; i++) roster[i] = new MatchParticipantState { NetId = i, DisplayName = displayName + i };
            Fake.Push(new MatchRosterPacket { MatchId = MatchId, Players = roster });
            var snapshot = new WorldSnapshotPacket { MatchId = MatchId, ServerTick = Tick += 3, PlayerCount = 4 };
            for (byte i = 0; i < 4; i++) snapshot.SetPlayer(i, new PlayerStatePacket { Tick = Tick, NetId = i, X = 5 + i, Y = 10, Hp = 100 });
            Fake.Push(snapshot);
        }

        /// <summary>Runs <paramref name="seconds"/> of frames at <paramref name="fps"/>, advancing the server tick at 30 Hz.</summary>
        public void Run(float seconds, float fps = 60f)
        {
            var frames = (int)Math.Round(seconds * fps);
            for (var i = 0; i < frames; i++)
            {
                Match.Tick(1f / fps);
                Recorder.Tick(1f / fps);
                Utc += (long)(1000 / fps);
            }
        }

        public void Dispose()
        {
            Remote.Dispose();
            Match.Dispose();
        }
    }
}
