using JungleMaze.App.Hud;
using JungleMaze.Domain.Balance;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace JungleMaze.Tests.Hud
{
    public sealed class SmoothedBarTests
    {
        /// <summary>Ticks until the bar reaches its target; returns the number of frames, asserting monotonic descent.</summary>
        private static int FramesToTarget(SmoothedBar bar, float dt, bool decreasing)
        {
            var previous = bar.Value;
            for (var frame = 1; frame <= 10000; frame++)
            {
                bar.Tick(dt);
                if (decreasing) Assert.That(bar.Value, Is.LessThanOrEqualTo(previous), $"frame {frame}");
                else Assert.That(bar.Value, Is.GreaterThanOrEqualTo(previous), $"frame {frame}");
                Assert.That(bar.Value, Is.InRange(0f, 1f));
                previous = bar.Value;
                if (bar.Value == bar.Target) return frame;
            }
            Assert.Fail("Bar never reached its target.");
            return -1;
        }

        [Test]
        public void ReachesTargetIn025s_Monotonic()
        {
            var bar = new SmoothedBar(0.25f);
            bar.Snap(1f);
            bar.SetTarget(0.72f);
            var frames = FramesToTarget(bar, 1f / 60, true);
            Assert.That(frames, Is.InRange(15, 16)); // 0.25 s ± 1 frame
            Assert.AreEqual(0.72f, bar.Value);
            bar.SetTarget(0.9f);
            Assert.That(FramesToTarget(bar, 1f / 60, false), Is.InRange(15, 16));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void FrameRateIndependent(int fps)
        {
            var bar = new SmoothedBar(0.25f);
            bar.Snap(1f);
            bar.SetTarget(0.2f);
            var dt = 1f / fps;
            var frames = FramesToTarget(bar, dt, true);
            Assert.That(frames * dt, Is.EqualTo(0.25f).Within(dt + 1e-4f));
        }

        [Test]
        public void RetargetMidTween_ContinuesFromShown()
        {
            var bar = new SmoothedBar(0.25f);
            bar.Snap(1f);
            bar.SetTarget(0.5f);
            for (var i = 0; i < 6; i++) bar.Tick(1f / 60);
            var shown = bar.Value;
            Assert.That(shown, Is.LessThan(1f).And.GreaterThan(0.5f));
            bar.SetTarget(0.4f);
            Assert.AreEqual(shown, bar.Value, "a new target must not jump");
            bar.Tick(1f / 60);
            Assert.That(bar.Value, Is.LessThan(shown));
        }

        [Test]
        public void ClampsAndNaN()
        {
            var bar = new SmoothedBar(0.25f);
            bar.Snap(2f);
            Assert.AreEqual(1f, bar.Value);
            bar.Snap(-1f);
            Assert.AreEqual(0f, bar.Value);
            bar.Snap(float.NaN);
            Assert.AreEqual(0f, bar.Value);
            bar.SetTarget(float.PositiveInfinity);
            Assert.AreEqual(1f, bar.Target);
            Assert.DoesNotThrow(() => bar.Tick(float.NaN));
            var zero = new SmoothedBar(0f);
            zero.SetTarget(0.5f);
            Assert.AreEqual(0.5f, zero.Value);
        }
    }

    public sealed class DamageTrailTests
    {
        [Test]
        public void HoldThenRetract_WithinOneFrame()
        {
            const float dt = 1f / 60;
            var trail = new DamageTrail(0.3f, 0.3f);
            trail.Snap(0.8f);
            trail.OnTargetChanged(0.8f, 0.72f);
            Assert.IsTrue(trail.IsActive);
            Assert.AreEqual(0.8f, trail.Value);
            var frames = 0;
            var holdFrames = 0;
            while (trail.IsActive && frames < 1000)
            {
                var before = trail.Value;
                trail.Tick(dt);
                frames++;
                if (trail.Value == 0.8f) holdFrames = frames;
                Assert.That(trail.Value, Is.LessThanOrEqualTo(before));
            }
            Assert.That(holdFrames * dt, Is.EqualTo(0.3f).Within(dt + 1e-4f), "hold");
            Assert.That(frames * dt, Is.EqualTo(0.6f).Within(dt + 1e-4f), "hold + retract");
            Assert.AreEqual(0.72f, trail.Value);
        }

        [Test]
        public void Heal_HasNoTrail()
        {
            var trail = new DamageTrail(0.3f, 0.3f);
            trail.Snap(0.5f);
            trail.OnTargetChanged(0.5f, 0.9f);
            Assert.IsFalse(trail.IsActive);
            Assert.AreEqual(0.9f, trail.Value);
        }

        [Test]
        public void HitWhileHolding_KeepsHighestAndRestartsHold()
        {
            var trail = new DamageTrail(0.3f, 0.3f);
            trail.Snap(1f);
            trail.OnTargetChanged(1f, 0.8f);
            trail.Tick(0.2f);
            trail.OnTargetChanged(0.85f, 0.6f);
            Assert.AreEqual(1f, trail.Value);
            trail.Tick(0.25f);
            Assert.AreEqual(1f, trail.Value, "hold restarted by the second hit");
            trail.Tick(0.4f);
            Assert.IsFalse(trail.IsActive);
            Assert.AreEqual(0.6f, trail.Value);
        }
    }

    public sealed class AdrenalineGaugeTests
    {
        [Test]
        public void EmptiesAfter8s_Linear()
        {
            var gauge = new AdrenalineGauge();
            gauge.Set(8f, 8f);
            Assert.AreEqual(1f, gauge.Fill);
            var elapsed = 0f;
            while (gauge.IsActive && elapsed < 20f)
            {
                gauge.Tick(1f / 60);
                elapsed += 1f / 60;
                if (gauge.IsActive) Assert.That(gauge.Fill, Is.EqualTo(1f - elapsed / 8f).Within(0.001f));
            }
            Assert.That(elapsed, Is.EqualTo(8f).Within(0.1f));
            Assert.AreEqual(0f, gauge.Fill);
        }

        [Test]
        public void ReactivateRefills_AndNoEffectIsEmpty()
        {
            var gauge = new AdrenalineGauge();
            Assert.IsFalse(gauge.IsActive);
            Assert.AreEqual(0f, gauge.Fill);
            gauge.Set(8f, 8f);
            gauge.Tick(5f);
            gauge.Set(8f, 8f);
            Assert.AreEqual(1f, gauge.Fill);
            gauge.Set(8f, 20f);
            Assert.AreEqual(8f, gauge.RemainingSeconds, "clamped to total");
            gauge.Set(0f, 5f);
            Assert.IsFalse(gauge.IsActive);
            gauge.Set(float.NaN, 1f);
            Assert.AreEqual(0f, gauge.Fill);
        }
    }

    public sealed class MinimapProjectionTests
    {
        [Test]
        public void CellCentres_LandWithinOneCellPerAxis()
        {
            var projection = new MinimapProjection(50, 200f);
            Assert.AreEqual(4f, projection.PixelsPerCell);
            // T9: Hero stands at the centre of a cell; error is measured per axis against the expected corner/centre.
            projection.Project(0.5f, 0.5f, out var x, out var y);
            Assert.That(x, Is.EqualTo(0f).Within(4f));
            Assert.That(y, Is.EqualTo(0f).Within(4f));
            projection.Project(25.5f, 25.5f, out x, out y);
            Assert.That(x, Is.EqualTo(100f).Within(4f));
            Assert.That(y, Is.EqualTo(100f).Within(4f));
            projection.Project(49.5f, 49.5f, out x, out y);
            Assert.That(x, Is.EqualTo(200f).Within(4f));
            Assert.That(y, Is.EqualTo(200f).Within(4f));
        }

        [Test]
        public void ClampsOutsideMap_AndHeadingFollowsRotation()
        {
            var projection = new MinimapProjection(50, 200f);
            projection.Project(-10f, 80f, out var x, out var y);
            Assert.AreEqual(0f, x);
            Assert.AreEqual(200f, y);
            Assert.That(MinimapProjection.HeadingDegrees(90f), Is.EqualTo(0f).Within(5f), "facing north = needle up");
            Assert.That(MinimapProjection.HeadingDegrees(0f), Is.EqualTo(-90f).Within(5f));
            Assert.That(MinimapProjection.HeadingDegrees(180f), Is.EqualTo(90f).Within(5f));
            Assert.That(MinimapProjection.HeadingDegrees(-90f), Is.EqualTo(180f).Within(5f));
        }
    }

    public sealed class HudFormatTests
    {
        private readonly char[] _buffer = new char[HudFormat.BufferSize];

        private string Text(int length) => new string(_buffer, 0, length);

        [Test]
        public void CeilInt()
        {
            Assert.AreEqual(48, HudFormat.CeilToInt(47.2f));
            Assert.AreEqual(48, HudFormat.CeilToInt(48f));
            Assert.AreEqual(1, HudFormat.CeilToInt(0.01f));
            Assert.AreEqual(0, HudFormat.CeilToInt(0f));
            Assert.AreEqual(0, HudFormat.CeilToInt(float.NaN));
        }

        [Test]
        public void MmSs()
        {
            Assert.AreEqual("01:24", Text(HudFormat.MinutesSeconds(_buffer, 84f)));
            Assert.AreEqual("01:24", Text(HudFormat.MinutesSeconds(_buffer, 83.2f)));
            Assert.AreEqual("00:00", Text(HudFormat.MinutesSeconds(_buffer, 0f)));
            Assert.AreEqual("00:00", Text(HudFormat.MinutesSeconds(_buffer, -5f)));
            Assert.AreEqual("00:00", Text(HudFormat.MinutesSeconds(_buffer, float.NaN)));
            Assert.AreEqual("99:59", Text(HudFormat.MinutesSeconds(_buffer, 1e9f)));
            Assert.AreEqual("--:--", Text(HudFormat.ClockPlaceholder(_buffer)));
            Assert.AreEqual("-/-", Text(HudFormat.FractionPlaceholder(_buffer)));
        }

        [Test]
        public void NumbersFractionsCountsSeconds()
        {
            Assert.AreEqual("72", Text(HudFormat.Integer(_buffer, 72)));
            Assert.AreEqual("0", Text(HudFormat.Integer(_buffer, 0)));
            Assert.AreEqual("-15", Text(HudFormat.Integer(_buffer, -15)));
            Assert.AreEqual(int.MinValue.ToString(), Text(HudFormat.Integer(_buffer, int.MinValue)));
            Assert.AreEqual("/100", Text(HudFormat.SlashInteger(_buffer, 100)));
            Assert.AreEqual("/ 30", Text(HudFormat.SlashSpaceInteger(_buffer, 30)));
            Assert.AreEqual("7/8", Text(HudFormat.Fraction(_buffer, 7, 8)));
            Assert.AreEqual("×6", Text(HudFormat.Count(_buffer, 6)));
            Assert.AreEqual("5s", Text(HudFormat.Seconds(_buffer, 5f)));
            Assert.AreEqual("5s", Text(HudFormat.Seconds(_buffer, 4.2f)));
            Assert.AreEqual("0s", Text(HudFormat.Seconds(_buffer, -1f)));
        }

        [Test]
        public void Formatting_DoesNotAllocate()
        {
            TestDelegate formatAll = () =>
            {
                HudFormat.Integer(_buffer, 12345);
                HudFormat.Fraction(_buffer, 7, 10);
                HudFormat.MinutesSeconds(_buffer, 84f);
                HudFormat.Seconds(_buffer, 5f);
                HudFormat.Count(_buffer, 4);
                HudFormat.ClockPlaceholder(_buffer);
            };
            formatAll(); // warm up: first-call JIT/static init is not per-frame allocation
            Assert.That(formatAll, Is.Not.AllocatingGCMemory());
        }
    }

    public sealed class WeaponStripRulesTests
    {
        private static WeaponDefinition Crossbow() => new WeaponDefinition("CROSSBOW", "Crossbow", WeaponCategory.Ranged,
            new WeaponStats { MagazineSize = 6, ReserveAmmo = 30 });

        [Test]
        public void ReloadHint_OnlyWhenReloadIsPossible()
        {
            var crossbow = Crossbow();
            Assert.IsTrue(WeaponStripRules.ShowAmmo(crossbow));
            Assert.IsTrue(WeaponStripRules.ShowReloadHint(crossbow, 4, 30));
            Assert.IsFalse(WeaponStripRules.ShowReloadHint(crossbow, 6, 30), "full magazine");
            Assert.IsFalse(WeaponStripRules.ShowReloadHint(crossbow, 4, 0), "no reserve");
        }

        [Test]
        public void MeleeThrownSpecialAndNull_HideAmmoAndHint()
        {
            foreach (var category in new[] { WeaponCategory.Melee, WeaponCategory.Thrown, WeaponCategory.Special })
            {
                var weapon = new WeaponDefinition("X", "X", category, new WeaponStats());
                Assert.IsFalse(WeaponStripRules.ShowAmmo(weapon), category.ToString());
                Assert.IsFalse(WeaponStripRules.ShowReloadHint(weapon, 0, 10), category.ToString());
            }
            Assert.IsFalse(WeaponStripRules.ShowAmmo(null));
            Assert.IsFalse(WeaponStripRules.ShowReloadHint(null, 0, 10));
        }
    }
}
