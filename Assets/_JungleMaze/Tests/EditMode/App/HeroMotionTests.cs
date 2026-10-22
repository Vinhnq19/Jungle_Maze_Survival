using JungleMaze.App.Player;
using NUnit.Framework;

namespace JungleMaze.Tests.App
{
    public sealed class HeroMotionTests
    {
        [Test]
        public void ComputeMoveDelta_StraightAxis_MovesAtFullSpeed()
        {
            var (x, y) = HeroMotion.ComputeMoveDelta(1f, 0f, 4f, 0.5f);
            Assert.AreEqual(2f, x, 1e-5f);
            Assert.AreEqual(0f, y, 1e-5f);
        }

        [Test]
        public void ComputeMoveDelta_Diagonal_IsNoFasterThanStraight()
        {
            var (x, y) = HeroMotion.ComputeMoveDelta(1f, 1f, 4f, 1f);
            var distance = System.Math.Sqrt(x * x + y * y);
            Assert.AreEqual(4.0, distance, 1e-4);
            Assert.AreEqual(x, y, 1e-5f);
        }

        [Test]
        public void ComputeMoveDelta_SubUnitInput_IsNotRenormalized()
        {
            var (x, y) = HeroMotion.ComputeMoveDelta(0.5f, 0f, 4f, 1f);
            Assert.AreEqual(2f, x, 1e-5f);
            Assert.AreEqual(0f, y, 1e-5f);
        }

        [Test]
        public void ComputeMoveDelta_NoInput_NoMovement()
        {
            var (x, y) = HeroMotion.ComputeMoveDelta(0f, 0f, 4f, 1f);
            Assert.AreEqual(0f, x);
            Assert.AreEqual(0f, y);
        }

        [Test]
        public void SmoothAimDegrees_ZeroLerpTime_SnapsInstantly()
        {
            Assert.AreEqual(90f, HeroMotion.SmoothAimDegrees(0f, 90f, 0f, 0.016f), 1e-4f);
        }

        [Test]
        public void SmoothAimDegrees_ApproachesTargetOverTime()
        {
            var angle = 0f;
            for (var i = 0; i < 300; i++) angle = HeroMotion.SmoothAimDegrees(angle, 90f, 0.08f, 1f / 60f);
            Assert.AreEqual(90f, angle, 0.5f);
        }

        [Test]
        public void SmoothAimDegrees_TurnsTheShortWayAcrossWrap()
        {
            // From 170° to -170° (== 190°): the short way is +20°, not -340°.
            var result = HeroMotion.SmoothAimDegrees(170f, -170f, 1f, 0.1f);
            Assert.Greater(result, 170f);
            Assert.Less(result, 190f);
        }

        [TestCase(0f, 0f)]
        [TestCase(180f, 180f)]
        [TestCase(181f, -179f)]
        [TestCase(-180f, 180f)]
        [TestCase(360f, 0f)]
        [TestCase(-540f, 180f)]
        public void NormalizeDegrees_WrapsToHalfOpenRange(float input, float expected) =>
            Assert.AreEqual(expected, HeroMotion.NormalizeDegrees(input), 1e-3f);
    }
}
