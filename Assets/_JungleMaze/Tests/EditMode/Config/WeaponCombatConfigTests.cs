using System.IO;
using JungleMaze.Domain.Balance;
using JungleMaze.Infrastructure.Config;
using NUnit.Framework;

namespace JungleMaze.Tests.Config
{
    public sealed class WeaponCombatConfigTests
    {
        private const string CombatJson = @"[
  { ""id"": ""HAND"", ""displayName"": ""Hand"", ""category"": ""Melee"", ""damage"": 8, ""cooldownSeconds"": 0.5, ""range"": 1, ""arcDegrees"": 90 },
  { ""id"": ""RUBBER_GUN"", ""displayName"": ""Rubber"", ""category"": ""Ranged"", ""damage"": 15, ""range"": 6, ""fireRatePerSecond"": 3, ""projectileSpeed"": 18, ""magazineSize"": 12, ""reserveAmmo"": 60 },
  { ""id"": ""SHOTGUN"", ""displayName"": ""Shotgun"", ""category"": ""Ranged"", ""damage"": 9, ""pellets"": 5, ""range"": 4, ""fireRatePerSecond"": 0.8, ""projectileSpeed"": 24, ""magazineSize"": 4, ""reserveAmmo"": 20, ""reloadSeconds"": 1.5 }
]";

        [Test]
        public void Reload_DefaultsToTwoSecondsForRangedWithMagazine()
        {
            Assert.IsTrue(BalanceConfigJson.TryParseWeapons(CombatJson, "t", out var weapons, out var error), error);
            Assert.AreEqual(2f, Find(weapons, "RUBBER_GUN").Stats.ReloadSeconds);
        }

        [Test]
        public void Reload_ReadsExplicitValueFromJson()
        {
            Assert.IsTrue(BalanceConfigJson.TryParseWeapons(CombatJson, "t", out var weapons, out var error), error);
            Assert.AreEqual(1.5f, Find(weapons, "SHOTGUN").Stats.ReloadSeconds);
        }

        [Test]
        public void Reload_NotSetForMeleeWeapons()
        {
            Assert.IsTrue(BalanceConfigJson.TryParseWeapons(CombatJson, "t", out var weapons, out var error), error);
            Assert.IsFalse(Find(weapons, "HAND").Stats.ReloadSeconds.HasValue);
        }

        [Test]
        public void Spread_DefaultsTo25DegreesForPelletWeapons()
        {
            Assert.IsTrue(BalanceConfigJson.TryParseWeapons(CombatJson, "t", out var weapons, out var error), error);
            Assert.AreEqual(25f, Find(weapons, "SHOTGUN").Stats.SpreadDegrees);
            Assert.IsFalse(Find(weapons, "RUBBER_GUN").Stats.SpreadDegrees.HasValue);
        }

        [Test]
        public void Reload_RejectsNonPositiveValue()
        {
            var json = CombatJson.Replace("\"reloadSeconds\": 1.5", "\"reloadSeconds\": 0");
            Assert.IsFalse(BalanceConfigJson.TryParseWeapons(json, "t", out _, out var error));
            StringAssert.Contains("reloadSeconds", error);
        }

        [Test]
        public void Spread_RejectsOutOfRangeValue()
        {
            var json = CombatJson.Replace("\"reloadSeconds\": 1.5", "\"spreadDegrees\": 400");
            Assert.IsFalse(BalanceConfigJson.TryParseWeapons(json, "t", out _, out var error));
            StringAssert.Contains("spreadDegrees", error);
        }

        [Test]
        public void ShippedWeaponsJson_ContainsAllTenClientWeapons()
        {
            var path = Path.Combine(UnityEngine.Application.streamingAssetsPath, "JungleMaze/balance/weapons.json");
            Assert.IsTrue(BalanceConfigJson.TryParseWeapons(File.ReadAllText(path), "weapons.json", out var weapons, out var error), error);

            string[] expected = { "HAND", "TREE_BRANCH", "KNIFE", "BONE", "AXE", "MACE", "RUBBER_GUN", "SCEPTER", "CROSSBOW", "SHOTGUN" };
            foreach (var id in expected) Assert.IsNotNull(Find(weapons, id), id);
            Assert.AreEqual(5, Find(weapons, "SHOTGUN").Stats.Pellets);
            Assert.AreEqual(2.4f, Find(weapons, "CROSSBOW").Stats.ReloadSeconds);
            Assert.AreEqual(1.2f, Find(weapons, "RUBBER_GUN").Stats.ReloadSeconds);
            Assert.AreEqual(1.6f, Find(weapons, "SCEPTER").Stats.ReloadSeconds);
            Assert.AreEqual(2.8f, Find(weapons, "SHOTGUN").Stats.ReloadSeconds);
            Assert.AreEqual(25f, Find(weapons, "SHOTGUN").Stats.SpreadDegrees);
        }

        private static WeaponDefinition Find(System.Collections.Generic.List<WeaponDefinition> weapons, string id)
        {
            foreach (var weapon in weapons) if (weapon.Id == id) return weapon;
            return null;
        }
    }
}
