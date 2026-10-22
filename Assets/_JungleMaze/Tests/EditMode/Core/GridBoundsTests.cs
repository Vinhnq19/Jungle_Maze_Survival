using JungleMaze.Domain.World;
using NUnit.Framework;

namespace JungleMaze.Tests.Core
{
    public sealed class GridBoundsTests
    {
        [TestCase(0f, 0f, true)]
        [TestCase(50f, 50f, true)]
        [TestCase(25f, 25f, true)]
        [TestCase(-0.01f, 10f, false)]
        [TestCase(10f, 50.01f, false)]
        public void Contains_ChecksSquareBounds(float x, float y, bool expected) =>
            Assert.AreEqual(expected, GridBounds.Contains(x, y, 50));

        [Test]
        public void ClampToBounds_LeavesInsidePointUnchanged()
        {
            var (x, y) = GridBounds.ClampToBounds(25f, 30f, 50);
            Assert.AreEqual(25f, x);
            Assert.AreEqual(30f, y);
        }

        [Test]
        public void ClampToBounds_ClampsOutsidePoint()
        {
            var (x, y) = GridBounds.ClampToBounds(-5f, 120f, 50);
            Assert.AreEqual(0f, x);
            Assert.AreEqual(50f, y);
        }

        [TestCase(HardCoverType.AncientPillar, 1f, 2f)]
        [TestCase(HardCoverType.CityWall, 1f, 4f)]
        [TestCase(HardCoverType.BoulderRock, 4f, 4f)]
        [TestCase(HardCoverType.AncientStatue, 3f, 3f)]
        public void HardCoverSize_MatchesGddFootprint(HardCoverType type, float width, float height)
        {
            var (w, h) = HardCoverSize.Of(type);
            Assert.AreEqual(width, w);
            Assert.AreEqual(height, h);
        }
    }
}
