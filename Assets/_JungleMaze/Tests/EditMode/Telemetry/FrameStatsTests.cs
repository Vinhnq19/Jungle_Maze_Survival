using JungleMaze.Domain.Telemetry;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace JungleMaze.Tests.Telemetry
{
    /// <summary>TEL-01 frame-time numbers (Schema.vi.md 8, G-7): fpsAvg, fps1Low, p95, max; AC35 (no allocation), AC38.</summary>
    public sealed class FrameStatsTests
    {
        private static FrameSummary Summarize(FrameStats stats)
        {
            Assert.IsTrue(stats.TrySummarize(out var s));
            return s;
        }

        [Test]
        public void SteadySixtyFps()
        {
            var stats = new FrameStats();
            for (var i = 0; i < 300; i++) stats.Add(1f / 60f);
            var s = Summarize(stats);
            Assert.AreEqual(300, s.Frames);
            Assert.AreEqual(5.0, s.WindowSeconds, 1e-4);
            Assert.AreEqual(60.0, s.FpsAvg, 0.01);
            Assert.AreEqual(60.0, s.Fps1Low, 0.01);
            Assert.AreEqual(1000.0 / 60.0, s.FrameMsP95, 0.01);
            Assert.AreEqual(1000.0 / 60.0, s.FrameMsMax, 0.01);
        }

        // 99 frames of 10 ms and one of 100 ms: k = max(1, ceil(1.0)) = 1, fps1Low = 1 / 0.1 s.
        [Test]
        public void OneSlowFrame_DefinesOnePercentLow_NotP95()
        {
            var stats = new FrameStats();
            for (var i = 0; i < 99; i++) stats.Add(0.010f);
            stats.Add(0.100f);
            var s = Summarize(stats);
            Assert.AreEqual(10.0, s.Fps1Low, 0.01);
            Assert.AreEqual(10.0, s.FrameMsP95, 0.01, "p95 = 95th of 100 sorted = a 10 ms frame");
            Assert.AreEqual(100.0, s.FrameMsMax, 0.01);
            Assert.AreEqual(100 / (0.99 + 0.1), s.FpsAvg, 0.01);
        }

        // n = 250: k = ceil(2.5) = 3 slowest frames (3 × 50 ms) → 3 / 0.15 s = 20 fps.
        [Test]
        public void OnePercentLow_RoundsKUp()
        {
            var stats = new FrameStats();
            for (var i = 0; i < 247; i++) stats.Add(0.016f);
            for (var i = 0; i < 3; i++) stats.Add(0.050f);
            Assert.AreEqual(20.0, Summarize(stats).Fps1Low, 0.01);
        }

        // AC38: fpsAvg equals frames / measured time within 2 % for uneven frames.
        [Test]
        public void FpsAvg_MatchesIndependentCount()
        {
            var stats = new FrameStats();
            var random = new System.Random(7);
            double total = 0;
            for (var i = 0; i < 600; i++)
            {
                var dt = (float)(0.008 + random.NextDouble() * 0.025);
                total += dt;
                stats.Add(dt);
            }
            var expected = 600 / total;
            Assert.AreEqual(expected, Summarize(stats).FpsAvg, expected * 0.02);
        }

        // The in-place sort gives the same p95, max and 1% low as a reference sort, on random and repeated values.
        [TestCase(1000, 3)]
        [TestCase(2048, 11)]
        [TestCase(17, 5)]
        public void Percentiles_MatchReferenceSort(int n, int seed)
        {
            var stats = new FrameStats();
            var random = new System.Random(seed);
            var times = new float[n];
            for (var i = 0; i < n; i++)
            {
                times[i] = i % 5 == 0 ? 0.016f : (float)(0.005 + random.NextDouble() * 0.04);
                stats.Add(times[i]);
            }
            var sorted = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.OrderBy(times, t => t));
            var k = System.Math.Max(1, (int)System.Math.Ceiling(0.01 * n));
            double slowest = 0;
            for (var i = n - k; i < n; i++) slowest += sorted[i];
            var s = Summarize(stats);
            Assert.AreEqual(sorted[System.Math.Max(1, (int)System.Math.Ceiling(0.95 * n)) - 1] * 1000.0, s.FrameMsP95, 1e-6);
            Assert.AreEqual(sorted[n - 1] * 1000.0, s.FrameMsMax, 1e-6);
            Assert.AreEqual(k / slowest, s.Fps1Low, 1e-6);
        }

        [Test]
        public void IgnoresZeroNegativeAndNonFinite_EmptyHasNoSummary()
        {
            var stats = new FrameStats();
            stats.Add(0f);
            stats.Add(-1f);
            stats.Add(float.NaN);
            stats.Add(float.PositiveInfinity);
            Assert.AreEqual(0, stats.Frames);
            Assert.IsFalse(stats.TrySummarize(out _));
        }

        // A still frame (breakpoint, alt-tab) is one long frame: it is the max and drives 1% low.
        [Test]
        public void StillFrame_IsMaxAndOnePercentLow()
        {
            var stats = new FrameStats();
            for (var i = 0; i < 120; i++) stats.Add(1f / 60f);
            stats.Add(2.0f);
            var s = Summarize(stats);
            Assert.AreEqual(2000.0, s.FrameMsMax, 0.01);
            Assert.AreEqual(2, (int)System.Math.Ceiling(0.01 * 121));
            Assert.AreEqual(2 / (2.0 + 1.0 / 60.0), s.Fps1Low, 0.01);
        }

        // Beyond capacity the average still counts every frame; percentiles use the newest frames.
        [Test]
        public void PastCapacity_AverageCountsAllFrames()
        {
            var stats = new FrameStats(64);
            for (var i = 0; i < 100; i++) stats.Add(0.010f);
            for (var i = 0; i < 64; i++) stats.Add(0.020f);
            var s = Summarize(stats);
            Assert.AreEqual(164, s.Frames);
            Assert.AreEqual(164 / (1.0 + 1.28), s.FpsAvg, 0.05);
            Assert.AreEqual(20.0, s.FrameMsP95, 0.01, "only the newest 64 frames (all 20 ms) are stored");
        }

        // AC35: adding frames and summarizing allocate nothing once warmed up.
        [Test]
        public void AddAndSummarize_NoAllocation()
        {
            var stats = new FrameStats();
            for (var i = 0; i < 300; i++) stats.Add(1f / 60f);
            stats.TrySummarize(out _);
            stats.Reset();
            Assert.That(() =>
            {
                for (var i = 0; i < 300; i++) stats.Add(0.016f + (i % 7) * 0.001f);
                stats.TrySummarize(out _);
                stats.Reset();
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
