using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Telemetry;
using JungleMaze.Domain.Network;
using JungleMaze.Domain.Telemetry;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace JungleMaze.Tests.Telemetry
{
    /// <summary>TEL-01 perf_client recorder on a real MatchSession (AC35, AC38; schema type 18).</summary>
    public sealed class PerfClientRecorderTests
    {
        private MemorySink _sink;
        private TelemetryHarness _h;

        [SetUp]
        public void SetUp()
        {
            _sink = new MemorySink();
            _h = new TelemetryHarness(_sink);
        }

        [TearDown]
        public void TearDown() => _h.Dispose();

        // AC38: 60 s in InGame gives at least 12 records, one per 5 s window.
        [Test]
        public void SixtySecondsInGame_TwelveRecords()
        {
            _h.StartInGame();
            _h.Run(60f);
            Assert.GreaterOrEqual(_sink.Samples.Count, 12);
            Assert.AreEqual(12, _h.Recorder.Written);
            foreach (var s in _sink.Samples)
            {
                Assert.AreEqual(_h.MatchId, s.MatchId);
                Assert.AreEqual(2, s.NetId);
                Assert.AreEqual(5.0, s.WindowSec, 0.02);
                Assert.AreEqual(60.0, s.FpsAvg, 0.5);
                Assert.AreEqual("1.0.0", s.Cfg);
                Assert.AreEqual(TelemetryHarness.ConfigHash, s.CfgHash);
                Assert.AreEqual(4, s.Entities);
                Assert.AreEqual(TelemetryTransport.Udp, s.Transport);
                Assert.AreEqual(21.5, s.RttP50, 1e-9);
                Assert.AreEqual(38.25, s.RttP95, 1e-9);
                Assert.GreaterOrEqual(s.MatchTick, 0);
            }
        }

        [Test]
        public void OutsideInGame_NoRecord()
        {
            _h.Phase(MatchPhase.Waiting, _h.Tick);
            _h.Run(12f);
            _h.Phase(MatchPhase.Loading, _h.Tick);
            _h.Run(12f);
            Assert.AreEqual(0, _sink.Samples.Count);
        }

        // A window cut by a disconnect is dropped, not merged with the frames after the reconnect.
        [Test]
        public void Disconnect_RestartsTheWindow()
        {
            _h.StartInGame();
            _h.Run(4f);
            _h.Fake.SetState(NetworkState.Disconnected, DisconnectReason.ConnectionLost);
            _h.Run(3f);
            _h.Fake.SetState(NetworkState.Joined, DisconnectReason.None);
            _h.Run(4f);
            Assert.AreEqual(0, _sink.Samples.Count, "4 s + 4 s around a gap are two short windows");
            _h.Run(1.5f);
            Assert.AreEqual(1, _sink.Samples.Count);
            Assert.AreEqual(5.0, _sink.Samples[0].WindowSec, 0.02);
        }

        [Test]
        public void PingLoss_PerWindow_NullWithoutPings()
        {
            _h.StartInGame();
            _h.Network.Value.PingsSent = 10;
            _h.Network.Value.PongsLost = 1;
            _h.Run(0.1f);
            _h.Network.Value.PingsSent = 30;
            _h.Network.Value.PongsLost = 5;
            _h.Run(5f);
            Assert.AreEqual(20.0, _sink.Samples[0].LossPct, 1e-9, "4 lost of 20 pings in the window");
            _h.Run(5f);
            Assert.IsNaN(_sink.Samples[1].LossPct, "no ping in the second window");
        }

        [Test]
        public void NoPong_RttNull_UnknownTransportNull()
        {
            _h.Network.Value = new NetworkStats { Transport = "none", PongsReceived = 0 };
            _h.StartInGame();
            _h.Run(5.1f);
            var s = _sink.Samples[0];
            Assert.IsNaN(s.RttP50);
            Assert.IsNaN(s.RttP95);
            Assert.AreEqual(TelemetryTransport.None, s.Transport);
            Assert.AreEqual(TelemetryTransport.Wss, PerfClientRecorder.TransportOf("Wss"));
        }

        [Test]
        public void DisabledSink_NothingRecorded_RefusedCounted()
        {
            _h.StartInGame();
            _sink.Enabled = false;
            _h.Run(10f);
            Assert.AreEqual(0, _sink.Samples.Count);
            _sink.Enabled = true;
            _sink.Accept = false;
            _h.Run(5.1f);
            Assert.AreEqual(1, _h.Recorder.Skipped, "a refused sample is counted, not retried");
        }

        // AC35: the per-frame path (including closing a window) allocates nothing in steady state.
        [Test]
        public void SteadyState_NoAllocationPerFrame()
        {
            _h.StartInGame();
            _h.Run(10f); // warm-up: first windows, sort helpers
            var recorder = _h.Recorder;
            Assert.That(() =>
            {
                for (var i = 0; i < 600; i++) recorder.Tick(1f / 60f);
            }, Is.Not.AllocatingGCMemory());
            Assert.GreaterOrEqual(_sink.Samples.Count, 3);
        }
    }
}
