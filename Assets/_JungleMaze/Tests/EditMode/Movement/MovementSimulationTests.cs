using System;
using JungleMaze.App.Player;
using JungleMaze.App.Ports;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace JungleMaze.Tests.Movement
{
    /// <summary>MovementSimulation phải cho cùng kết quả với PhysicsSimulation của server (cùng công thức sweep/slide).</summary>
    public sealed class MovementSimulationTests
    {
        private const float Dt = MovementSimulation.TickSeconds;

        [Test]
        public void OpenSpace_MovesFourCellsPerSecond_DiagonalNormalized()
        {
            var sim = MovementTestMap.Create();
            float x = 10, y = 10;
            for (var i = 0; i < 30; i++) (x, y) = sim.Move(x, y, 1f, 1f, Dt);
            var distance = Math.Sqrt((x - 10) * (x - 10) + (y - 10) * (y - 10));
            Assert.AreEqual(4.0, distance, 1e-3);
            Assert.AreEqual(x, y, 1e-4f);
        }

        [Test]
        public void PushingIntoCover_StopsAtSurface_NeverOverlaps()
        {
            var sim = MovementTestMap.Create();
            float x = 26f, y = 25f; // Bên trái BoulderRock (minX 28)
            for (var i = 0; i < 90; i++)
            {
                (x, y) = sim.Move(x, y, 1f, 0f, Dt);
                Assert.IsFalse(sim.Overlaps(x, y), $"Tick {i}: ({x}, {y}) lấn Hard Cover");
            }
            Assert.AreEqual(28f - MovementTestMap.Radius, x, 2e-3f);
            Assert.AreEqual(25f, y, 1e-5f);
        }

        [Test]
        public void DiagonalIntoCover_SlidesAlongFace()
        {
            var sim = MovementTestMap.Create();
            float x = 27.5f, y = 24f;
            for (var i = 0; i < 15; i++) (x, y) = sim.Move(x, y, 1f, 1f, Dt);
            Assert.AreEqual(28f - MovementTestMap.Radius, x, 2e-3f, "Bị chặn theo X");
            Assert.Greater(y, 25f, "Vẫn trượt lên theo Y");
            Assert.IsFalse(sim.Overlaps(x, y));
        }

        [Test]
        public void LargeStep_DoesNotTunnelThroughThinPillar()
        {
            var sim = MovementTestMap.Create();
            // Một bước 4 cell (dt = 1s) lớn hơn bề dày cột 1 cell: sweep phải chặn, không nhảy qua.
            var (x, y) = sim.Move(21f, 25f, 1f, 0f, 1f);
            Assert.AreEqual(22.5f - MovementTestMap.Radius, x, 2e-3f);
            Assert.AreEqual(25f, y, 1e-5f);
        }

        [Test]
        public void MapEdge_ClampsInsideBounds()
        {
            var sim = MovementTestMap.Create();
            float x = 1f, y = 49f;
            for (var i = 0; i < 30; i++) (x, y) = sim.Move(x, y, -1f, 1f, Dt);
            Assert.AreEqual(MovementTestMap.Radius, x, 2e-3f);
            Assert.AreEqual(50f - MovementTestMap.Radius, y, 2e-3f);
        }

        [Test]
        public void StandingInsideCover_IsPushedOut()
        {
            var sim = MovementTestMap.Create();
            var (x, y) = sim.Move(28.1f, 25f, 0f, 0f, Dt);
            Assert.IsFalse(sim.Overlaps(x, y));
            Assert.AreEqual(28f - MovementTestMap.Radius, x, 2e-3f);
        }

        [Test]
        public void QuantizedInput_MatchesServerDequantize()
        {
            var input = MovementTestMap.Input(1, -0.25f, 0.2f);
            Assert.AreEqual(input.MoveXNormalized, MovementSimulation.Dequantize(input.MoveX));
            Assert.AreEqual(input.MoveYNormalized, MovementSimulation.Dequantize(input.MoveY));
        }

        [Test]
        public void Move_DoesNotAllocate()
        {
            var sim = MovementTestMap.Create();
            float x = 26f, y = 24.5f;
            TestDelegate step = () => { for (var i = 0; i < 64; i++) (x, y) = sim.Move(x, y, 1f, 0.3f, Dt); };
            step();
            Assert.That(step, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void InvalidConfig_Throws()
        {
            var bounds = new RuntimeMapBounds { MinX = 0, MinY = 0, MaxX = 50, MaxY = 50 };
            Assert.Throws<ArgumentException>(() => new MovementSimulation(bounds, null, 0f, 4f));
            Assert.Throws<ArgumentException>(() => new MovementSimulation(bounds, null, .4f, float.NaN));
            Assert.Throws<ArgumentException>(() => new MovementSimulation(new RuntimeMapBounds(), null, .4f, 4f));
        }
    }
}
