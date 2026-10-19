using JungleMaze.Infrastructure.Network;
using NUnit.Framework;

namespace JungleMaze.Tests.Network
{
    public sealed class RttTrackerTests
    {
        // Frequency 1000 → 1 tick = 1 ms cho dễ đọc.
        private static RttTracker Tracker(int maxPending = 4) => new RttTracker(maxPending, 8, 5000, 1000);

        [Test]
        public void MatchedPong_RecordsRtt()
        {
            var rtt = Tracker();
            rtt.OnPingSent(1, 100);
            Assert.IsTrue(rtt.OnPongReceived(1, 142));
            Assert.AreEqual(42, rtt.LastMs, 1e-9);
            Assert.AreEqual(1, rtt.Received);
        }

        [Test]
        public void DuplicateOrUnknownPong_IsIgnored()
        {
            var rtt = Tracker();
            rtt.OnPingSent(1, 0);
            Assert.IsTrue(rtt.OnPongReceived(1, 10));
            Assert.IsFalse(rtt.OnPongReceived(1, 20));
            Assert.IsFalse(rtt.OnPongReceived(99, 20));
            Assert.AreEqual(1, rtt.Received);
        }

        [Test]
        public void ExpiredPing_CountsLost_AndLatePongIgnored()
        {
            var rtt = Tracker();
            rtt.OnPingSent(1, 0);
            rtt.ExpirePending(5000);
            Assert.AreEqual(1, rtt.Lost);
            Assert.IsFalse(rtt.OnPongReceived(1, 5001));
        }

        [Test]
        public void FullPending_OldestCountsLost()
        {
            var rtt = Tracker(maxPending: 2);
            rtt.OnPingSent(1, 0);
            rtt.OnPingSent(2, 10);
            rtt.OnPingSent(3, 20);
            Assert.AreEqual(1, rtt.Lost);
            Assert.IsFalse(rtt.OnPongReceived(1, 30));
            Assert.IsTrue(rtt.OnPongReceived(3, 30));
        }

        [Test]
        public void Percentile_NearestRank()
        {
            var rtt = Tracker();
            for (var i = 1; i <= 4; i++) { rtt.OnPingSent(i, 0); rtt.OnPongReceived(i, i * 10); }
            Assert.AreEqual(40, rtt.Percentile(0.95), 1e-9);
            Assert.AreEqual(20, rtt.Percentile(0.5), 1e-9);
            Assert.AreEqual(25, rtt.AverageMs, 1e-9);
        }
    }
}
