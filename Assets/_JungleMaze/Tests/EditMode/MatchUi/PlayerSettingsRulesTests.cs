using System;
using JungleMaze.Domain.MatchUi;
using NUnit.Framework;

namespace JungleMaze.Tests.MatchUi
{
    public sealed class PlayerSettingsRulesTests
    {
        [TestCase(99f, 2f)]
        [TestCase(2.04f, 2f)]
        [TestCase(-1f, 0.5f)]
        [TestCase(0f, 0.5f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(1f, 1f)]
        [TestCase(1.04f, 1f)]
        [TestCase(1.06f, 1.1f)]
        [TestCase(1.449f, 1.4f)]
        [TestCase(1.951f, 2f)]
        public void ClampSensitivity_ClampsAndRoundsToStep(float raw, float expected)
        {
            Assert.AreEqual(expected, PlayerSettingsRules.ClampSensitivity(raw), 1e-5f);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ClampSensitivity_NotFinite_FallsBackToDefault(float raw)
        {
            Assert.AreEqual(PlayerSettingsRules.DefaultSensitivity, PlayerSettingsRules.ClampSensitivity(raw));
        }

        [Test]
        public void Defaults_MatchG9AndQ5()
        {
            Assert.AreEqual(0.5f, PlayerSettingsRules.MinSensitivity);
            Assert.AreEqual(2f, PlayerSettingsRules.MaxSensitivity);
            Assert.AreEqual(0.1f, PlayerSettingsRules.SensitivityStep);
            Assert.AreEqual(1f, PlayerSettingsRules.DefaultSensitivity);
            Assert.IsTrue(PlayerSettingsRules.DefaultScreenShake);
        }

        [Test]
        public void EveryStepFrom05To20_IsAFixedPoint()
        {
            for (var i = 5; i <= 20; i++)
            {
                var v = i / 10f;
                Assert.IsTrue(PlayerSettingsRules.SameStep(v, PlayerSettingsRules.ClampSensitivity(v)), v.ToString());
            }
        }

        [Test]
        public void EliminationInfo_MissingValues_AreReportedAsMissing()
        {
            var info = EliminationInfo.Unknown(Guid.NewGuid(), 42);
            Assert.IsFalse(info.HasRank);
            Assert.IsFalse(info.HasKiller);
            Assert.IsFalse(info.HasKillerName);
            Assert.IsFalse(info.HasWeapon);
            Assert.IsFalse(info.HasDistance);
            Assert.IsFalse(info.HasSurvival);
            Assert.IsFalse(info.HasKills);
            var ranked = info.WithRank(4, 10, 75f);
            Assert.IsTrue(ranked.HasRank);
            Assert.IsTrue(ranked.HasSurvival);
            Assert.AreEqual(42u, ranked.EliminationTick);
        }

        [Test]
        public void SpectatorEntry_WithoutMaxHp_HasNoHp()
        {
            var entry = new SpectatorEntry(3, "", true, 50, 0, "");
            Assert.IsFalse(entry.HasHp);
            Assert.IsNull(entry.Name);
            Assert.IsNull(entry.WeaponName);
            Assert.AreEqual(100, new SpectatorEntry(3, "A", true, 200, 100, null).Hp);
        }

        [Test]
        public void KillcamClip_InterpolatesBetweenFrames_AndClearDropsData()
        {
            var clip = new KillcamClip(150) { FrameCount = 2 };
            clip.Set(0, 4, 10f, 20f, 100);
            clip.Set(1, 4, 12f, 24f, 90);
            Assert.IsTrue(clip.TryGetPosition(4, 0.5f, out var x, out var y));
            Assert.AreEqual(11f, x, 1e-5f);
            Assert.AreEqual(22f, y, 1e-5f);
            Assert.IsFalse(clip.TryGetPosition(5, 0.5f, out _, out _));
            clip.Clear();
            Assert.AreEqual(0, clip.FrameCount);
            Assert.IsFalse(clip.At(0, 4).Present);
            Assert.AreEqual(150 * KillcamClip.MaxPlayers, clip.Samples.Length);
        }
    }
}
