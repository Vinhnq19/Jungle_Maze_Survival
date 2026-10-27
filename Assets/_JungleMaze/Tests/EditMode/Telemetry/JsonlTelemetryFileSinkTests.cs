using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using JungleMaze.App.Telemetry;
using JungleMaze.Core.Logging;
using JungleMaze.Domain.Telemetry;
using JungleMaze.Infrastructure.Telemetry;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using UnityEngine;

namespace JungleMaze.Tests.Telemetry
{
    /// <summary>TEL-01 client JSONL sink: format and key order (schema v1), files, cap, I/O failure, non-blocking (AC24–AC27, AC4).</summary>
    public sealed class JsonlTelemetryFileSinkTests
    {
        /// <summary>perf_client key order: envelope (telemetry.schema.v1.json "envelope") then the type's "fields".</summary>
        public static readonly string[] PerfClientKeys =
        {
            "v", "type", "matchId", "roomId", "src", "wid", "seq", "tick", "mt", "utc", "build", "cfg", "cfgHash", "balHash",
            "netId", "windowSec", "frames", "fpsAvg", "fps1Low", "frameMsP95", "frameMsMax", "transport", "rttP50", "rttP95",
            "jitterMs", "lossPct", "entities"
        };
        private static readonly string[] HeaderKeys = { "v", "type", "schema", "src", "wid", "matchId", "build", "opened" };
        private static readonly string[] TrailerKeys = { "v", "type", "src", "wid", "matchId", "written", "droppedQueue", "droppedIo", "truncated", "closed" };
        private static readonly Regex Key = new Regex("\"([A-Za-z0-9_]+)\":");
        private const string BalHash = "9b04e17a00000000000000000000000000000000000000000000000000000000";

        private string _dir;
        private readonly TelemetryContext _context = new TelemetryContext("dev", "1a2b3c4d", BalHash);

        private sealed class CountingLog : ILog
        {
            public int Warnings;
            public void Info(string message) { }
            public void Warn(string message) => Warnings++;
            public void Error(string message) { }
        }

        [SetUp]
        public void SetUp() => _dir = Path.Combine(Path.GetTempPath(), "tel01_" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            else if (File.Exists(_dir)) File.Delete(_dir);
        }

        private static PerfClientSample Sample(Guid match, uint tick = 4200) => new PerfClientSample
        {
            MatchId = match, Tick = tick, MatchTick = 3000, UtcMs = 1_793_000_000_123, Cfg = "1.0.0",
            CfgHash = TelemetryHarness.ConfigHash, NetId = 3, WindowSec = 5.0004, Frames = 301, FpsAvg = 60.19234, Fps1Low = 41.5,
            FrameMsP95 = 18.0001, FrameMsMax = 33.25, Transport = TelemetryTransport.Udp, RttP50 = 21.5, RttP95 = double.NaN,
            JitterMs = 2.125, LossPct = double.NaN, Entities = 4
        };

        private JsonlTelemetryFileSink Sink(ILog log = null, long maxBytes = JsonlTelemetryFileSink.DefaultMaxFileBytes, Func<string, Stream> open = null,
            int capacity = JsonlTelemetryFileSink.DefaultCapacity) =>
            new JsonlTelemetryFileSink(_dir, _context, log, capacity, maxBytes, open, () => 1_793_000_000_000, 5);

        private static List<string> Keys(string line) => Key.Matches(line).Cast<Match>().Select(m => m.Groups[1].Value).ToList();

        [Test]
        public void HeaderRecordsTrailer_KeyOrder_SeqFromOne()
        {
            var match = Guid.NewGuid();
            var sink = Sink();
            for (uint i = 0; i < 5; i++) Assert.IsTrue(sink.TryWrite(Sample(match, 4200 + i * 150)));
            sink.Dispose();
            var path = sink.LastPath;
            StringAssert.EndsWith(match.ToString("D") + ".client.3.1a2b3c4d.jsonl", path);
            var text = File.ReadAllText(path);
            StringAssert.DoesNotContain("\r", text);
            Assert.AreEqual((byte)'{', File.ReadAllBytes(path)[0], "no BOM");
            var lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual(7, lines.Length);
            CollectionAssert.AreEqual(HeaderKeys, Keys(lines[0]));
            StringAssert.Contains("\"schema\":\"telemetry.v1\"", lines[0]);
            for (var i = 1; i <= 5; i++)
            {
                CollectionAssert.AreEqual(PerfClientKeys, Keys(lines[i]), "line " + i);
                StringAssert.Contains("\"seq\":" + i + ",", lines[i]);
            }
            CollectionAssert.AreEqual(TrailerKeys, Keys(lines[6]));
            StringAssert.Contains("\"written\":5,\"droppedQueue\":0,\"droppedIo\":0,\"truncated\":false", lines[6]);
            Assert.AreEqual(
                "{\"v\":1,\"type\":\"perf_client\",\"matchId\":\"" + match.ToString("D") + "\",\"roomId\":null,\"src\":\"client\",\"wid\":\"1a2b3c4d\"," +
                "\"seq\":1,\"tick\":4200,\"mt\":3000,\"utc\":1793000000123,\"build\":\"dev\",\"cfg\":\"1.0.0\",\"cfgHash\":\"" + TelemetryHarness.ConfigHash + "\"," +
                "\"balHash\":\"" + BalHash + "\",\"netId\":3,\"windowSec\":5,\"frames\":301,\"fpsAvg\":60.192,\"fps1Low\":41.5,\"frameMsP95\":18," +
                "\"frameMsMax\":33.25,\"transport\":\"UDP\",\"rttP50\":21.5,\"rttP95\":null,\"jitterMs\":2.125,\"lossPct\":null,\"entities\":4}",
                lines[1], "3 decimals at most, NaN as null, no fake key");
        }

        // AC26: a new MatchId opens a new file; seq restarts per match.
        [Test]
        public void NewMatch_NewFile_SeqPerMatch()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var sink = Sink();
            sink.TryWrite(Sample(a));
            sink.TryWrite(Sample(a));
            sink.TryWrite(Sample(b));
            sink.Dispose();
            var files = Directory.GetFiles(_dir);
            Assert.AreEqual(2, files.Length);
            var fileB = File.ReadAllLines(files.Single(f => f.Contains(b.ToString("D"))));
            Assert.AreEqual(3, fileB.Length);
            StringAssert.Contains("\"seq\":1,", fileB[1]);
            StringAssert.Contains("\"written\":1,", fileB[2]);
            StringAssert.Contains("\"written\":2,", File.ReadAllLines(files.Single(f => f.Contains(a.ToString("D"))))[3]);
        }

        // AC26: past the cap the file stops taking records and says truncated.
        [Test]
        public void PastCap_Truncated()
        {
            var match = Guid.NewGuid();
            var sink = Sink(maxBytes: 1500);
            for (var i = 0; i < 20; i++) sink.TryWrite(Sample(match));
            sink.Dispose();
            var lines = File.ReadAllLines(sink.LastPath);
            var records = lines.Length - 2;
            Assert.Less(records, 20);
            Assert.Greater(records, 0);
            StringAssert.Contains("\"written\":" + records + ",", lines.Last());
            StringAssert.Contains("\"truncated\":true", lines.Last());
            Assert.LessOrEqual(new FileInfo(sink.LastPath).Length - lines.Last().Length - 1, 1500);
        }

        // AC25: a directory path that is a file: the sink turns itself off, warns once, never throws.
        [Test]
        public void DirectoryIsAFile_DisablesWithOneWarning()
        {
            File.WriteAllText(_dir, "not a directory");
            var log = new CountingLog();
            var sink = Sink(log);
            Assert.DoesNotThrow(() =>
            {
                sink.TryWrite(Sample(Guid.NewGuid()));
                Thread.Sleep(100);
                sink.TryWrite(Sample(Guid.NewGuid()));
                sink.Dispose();
            });
            Assert.IsFalse(sink.Enabled);
            Assert.AreEqual(1, log.Warnings);
            Assert.IsFalse(sink.TryWrite(Sample(Guid.NewGuid())), "disabled sink refuses");
        }

        // AC25: disk full while writing (stream throws) → disabled, counted, no exception.
        [Test]
        public void StreamThrows_DisablesAndCounts()
        {
            var log = new CountingLog();
            var sink = Sink(log, open: path => new ThrowingStream());
            sink.TryWrite(Sample(Guid.NewGuid()));
            sink.TryWrite(Sample(Guid.NewGuid()));
            sink.Dispose();
            Assert.IsFalse(sink.Enabled);
            Assert.AreEqual(1, log.Warnings);
            Assert.GreaterOrEqual(sink.DroppedIo, 1);
        }

        // AC24: the writer is stuck on I/O; 10 000 TryWrite calls return at once, full queue counted as dropped.
        [Test]
        public void BlockedWriter_CallerNeverWaits()
        {
            var gate = new ManualResetEventSlim(false);
            var sink = Sink(open: path => new BlockingStream(gate), capacity: 64);
            var match = Guid.NewGuid();
            var watch = Stopwatch.StartNew();
            var accepted = 0;
            for (var i = 0; i < 10000; i++) if (sink.TryWrite(Sample(match))) accepted++;
            watch.Stop();
            Assert.Less(watch.ElapsedMilliseconds, 200, "10 000 calls while the writer is blocked");
            Assert.LessOrEqual(accepted, 64 + 1);
            Assert.AreEqual(10000 - accepted, sink.Dropped);
            gate.Set();
            sink.Dispose();
        }

        // AC27: constructing the sink creates nothing until a record arrives; no flag means no sink at all.
        [Test]
        public void NoRecord_NoDirectory_FlagOffByDefault()
        {
            var sink = Sink();
            sink.Dispose();
            Assert.IsFalse(Directory.Exists(_dir));
            Assert.IsFalse(ClientTelemetryOptions.FromArgs(new[] { "Game.exe", "-batchmode" }, "C:/data").Enabled);
            var on = ClientTelemetryOptions.FromArgs(new[] { "Game.exe", "-telemetry" }, "C:/data");
            Assert.IsTrue(on.Enabled);
            Assert.AreEqual(Path.Combine("C:/data", "Telemetry"), on.Directory);
            Assert.AreEqual("D:/qa", ClientTelemetryOptions.FromArgs(new[] { "-telemetryDir", "D:/qa", "-telemetry" }, "C:/data").Directory);
        }

        // Hook contract: TryWrite copies a struct into the ring, nothing else.
        [Test]
        public void TryWrite_NoAllocation()
        {
            var sink = Sink(capacity: 4096);
            var sample = Sample(Guid.NewGuid());
            sink.TryWrite(in sample);
            Assert.That(() =>
            {
                for (var i = 0; i < 1000; i++) sink.TryWrite(in sample);
            }, Is.Not.AllocatingGCMemory());
            sink.Dispose();
        }

        // balHash: same recipe as the server's DataHash; CRLF and LF give the same hash; a missing file gives null.
        [Test]
        public void BalanceHash_LineEndingsAgnostic_NullWhenMissing()
        {
            Directory.CreateDirectory(_dir);
            foreach (var f in ClientTelemetryIdentity.BalanceFiles) File.WriteAllText(Path.Combine(_dir, f), "[\r\n  {\"id\": \"" + f + "\"}\r\n]");
            var crlf = ClientTelemetryIdentity.BalanceHash(_dir);
            foreach (var f in ClientTelemetryIdentity.BalanceFiles) File.WriteAllText(Path.Combine(_dir, f), "[\n  {\"id\": \"" + f + "\"}\n]");
            Assert.AreEqual(crlf, ClientTelemetryIdentity.BalanceHash(_dir));
            Assert.IsTrue(Regex.IsMatch(crlf, "^[0-9a-f]{64}$"));
            File.Delete(Path.Combine(_dir, "match.json"));
            Assert.IsNull(ClientTelemetryIdentity.BalanceHash(_dir));
            var real = ClientTelemetryIdentity.Create(Application.streamingAssetsPath, "dev");
            Assert.IsTrue(Regex.IsMatch(real.BalHash, "^[0-9a-f]{64}$"), "shipped balance files hash");
            Assert.IsTrue(Regex.IsMatch(real.Wid, "^[0-9a-f]{8}$"));
        }

        // AC4 + AC38 end to end: recorder → sink → file, 60 s of InGame; no names, room code, keys or addresses.
        [Test]
        public void RecorderToFile_SixtySeconds_NoPii()
        {
            var sink = Sink();
            using (var h = new TelemetryHarness(sink))
            {
                h.StartInGame("Zebra-Player");
                h.Run(60f);
                Thread.Sleep(50);
            }
            sink.Dispose();
            var text = File.ReadAllText(sink.LastPath);
            var lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual(12, lines.Count(l => l.Contains("\"type\":\"perf_client\"")));
            StringAssert.DoesNotContain("Zebra", text);
            StringAssert.DoesNotContain("127.0.0.1", text);
            foreach (var key in new[] { "displayName", "name", "roomCode", "code", "jwt", "token", "ticket", "resumeToken", "profileKey", "ip", "address", "endpoint", "playerSessionId", "playerId" })
                foreach (var line in lines)
                    Assert.IsFalse(Keys(line).Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)), key);
            Assert.IsFalse(Regex.IsMatch(text, "(?<![0-9.])(?:[0-9]{1,3}\\.){3}[0-9]{1,3}(?![0-9.])"), "no IPv4");
            for (var i = 1; i <= 12; i++) StringAssert.Contains("\"seq\":" + i + ",", lines[i]);
        }

        private sealed class ThrowingStream : MemoryStream
        {
            public override void Write(byte[] buffer, int offset, int count) => throw new IOException("There is not enough space on the disk.");
        }

        private sealed class BlockingStream : MemoryStream
        {
            private readonly ManualResetEventSlim _gate;
            public BlockingStream(ManualResetEventSlim gate) => _gate = gate;
            public override void Write(byte[] buffer, int offset, int count)
            {
                _gate.Wait(5000);
                base.Write(buffer, offset, count);
            }
        }
    }
}
