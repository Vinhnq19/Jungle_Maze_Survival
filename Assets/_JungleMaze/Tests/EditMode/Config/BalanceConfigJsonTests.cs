using System.IO;
using JungleMaze.Domain.Balance;
using JungleMaze.Infrastructure.Config;
using NUnit.Framework;

namespace JungleMaze.Tests.Config
{
    public sealed class BalanceConfigJsonTests
    {
        private const string ValidMatchJson = @"{
  ""matchDuration"": 420, ""lobbyDuration"": 15, ""maxPlayers"": 10, ""minPlayersToStart"": 6,
  ""mapSize"": 50, ""cellSize"": 48, ""heroMaxHP"": 100, ""heroBaseSpeed"": 4.0,
  ""heroColliderRadius"": 0.4, ""visionRadius"": 12, ""aimLerpTime"": 0.08, ""weaponSwapTime"": 0.3,
  ""pickupRadius"": 1.2, ""armorCapPct"": 0.7, ""zoneShape"": ""circle"", ""zoneShrinkSpeed"": 1,
  ""zoneDamageFloor"": 5, ""zoneRecalcInterval"": 10, ""zoneWarningTime"": 10, ""zoneCenterMargin"": 2,
  ""airdropCount"": 3, ""airdropEdgeMargin"": 3, ""dragonFlyDuration"": 10, ""chestCount"": 60,
  ""potCount"": 90, ""bushCount"": 45, ""portalPairs"": 3, ""stoneDoorCycle"": 20,
  ""spawnMinDistance"": 6, ""lootRespawn"": false, ""disconnectGrace"": 15, ""killcamDuration"": 5,
  ""targetFPS"": 60
}";

        [Test]
        public void Match_ParsesValidJson()
        {
            Assert.IsTrue(BalanceConfigJson.TryParseMatch(ValidMatchJson, "test", out var config, out var error), error);
            Assert.AreEqual(50, config.MapSizeCells);
            Assert.AreEqual(4.0f, config.HeroBaseSpeed);
            Assert.AreEqual(0.08f, config.AimLerpTimeSeconds);
            Assert.IsFalse(config.LootRespawn);
        }

        [Test]
        public void Match_RejectsUnknownKey()
        {
            var json = ValidMatchJson.Replace("\"mapSize\"", "\"mapSizee\"");
            Assert.IsFalse(BalanceConfigJson.TryParseMatch(json, "t", out _, out var error));
            StringAssert.Contains("mapSizee", error);
        }

        [Test]
        public void Match_RejectsMissingKey()
        {
            Assert.IsFalse(BalanceConfigJson.TryParseMatch(@"{ ""matchDuration"": 420 }", "t", out _, out var error));
            StringAssert.Contains("missing required key", error);
        }

        [Test]
        public void Match_RejectsOutOfRangeValue()
        {
            var json = ValidMatchJson.Replace("\"maxPlayers\": 10", "\"maxPlayers\": 0");
            Assert.IsFalse(BalanceConfigJson.TryParseMatch(json, "t", out _, out var error));
            StringAssert.Contains("maxPlayers", error);
        }

        [Test]
        public void Weapons_ParsesValidArray()
        {
            const string json = @"[
  { ""id"": ""HAND"", ""displayName"": ""Bare Hands"", ""category"": ""Melee"",
    ""damage"": 8, ""cooldownSeconds"": 0.5, ""range"": 1, ""arcDegrees"": 90 },
  { ""id"": ""SHOTGUN"", ""displayName"": ""Shotgun"", ""category"": ""Ranged"",
    ""damage"": 9, ""pellets"": 5, ""range"": 4, ""fireRatePerSecond"": 0.8, ""magazineSize"": 4, ""reserveAmmo"": 20 },
  { ""id"": ""BELL"", ""displayName"": ""Bell"", ""category"": ""Special"",
    ""damage"": 15, ""range"": 0, ""charges"": 1 }
]";
            Assert.IsTrue(BalanceConfigJson.TryParseWeapons(json, "t", out var weapons, out var error), error);
            Assert.AreEqual(3, weapons.Count);
            Assert.AreEqual(WeaponCategory.Melee, weapons[0].Category);
            Assert.AreEqual(5, weapons[1].Stats.Pellets);
            Assert.IsFalse(weapons[2].Stats.CooldownSeconds.HasValue);
        }

        [Test]
        public void Weapons_RejectsUnknownCategory()
        {
            const string json = @"[{ ""id"": ""X"", ""displayName"": ""X"", ""category"": ""Magic"", ""damage"": 1, ""range"": 1 }]";
            Assert.IsFalse(BalanceConfigJson.TryParseWeapons(json, "t", out _, out var error));
            StringAssert.Contains("category", error);
        }

        [Test]
        public void Weapons_RejectsUnknownKey()
        {
            const string json = @"[{ ""id"": ""X"", ""displayName"": ""X"", ""category"": ""Melee"", ""damage"": 1, ""range"": 1, ""dmg"": 1 }]";
            Assert.IsFalse(BalanceConfigJson.TryParseWeapons(json, "t", out _, out var error));
            StringAssert.Contains("dmg", error);
        }

        [Test]
        public void Equipments_ParsesValidArray()
        {
            const string json = @"[
  { ""id"": ""WOODEN_HELMET"", ""displayName"": ""Wooden Helmet"", ""type"": ""Helmet"",
    ""damageReductionPct"": 0.1, ""durabilityPoints"": 30, ""healAmount"": 0, ""useSeconds"": 0, ""maxCarry"": 1 },
  { ""id"": ""ADRENALINE"", ""displayName"": ""Energy Drink"", ""type"": ""Support"",
    ""healAmount"": 0, ""useSeconds"": 1.5, ""maxCarry"": 4, ""effectDescription"": ""+30% speed 8s"" }
]";
            Assert.IsTrue(BalanceConfigJson.TryParseEquipments(json, "t", out var equipments, out var error), error);
            Assert.AreEqual(2, equipments.Count);
            Assert.AreEqual(EquipmentType.Helmet, equipments[0].Type);
            Assert.AreEqual(0.1f, equipments[0].Stats.DamageReductionPct);
            Assert.AreEqual("+30% speed 8s", equipments[1].EffectDescription);
        }

        [Test]
        public void Equipments_RejectsOutOfRangeDamageReduction()
        {
            const string json = @"[{ ""id"": ""X"", ""displayName"": ""X"", ""type"": ""Armor"",
    ""damageReductionPct"": 1.5, ""healAmount"": 0, ""useSeconds"": 0, ""maxCarry"": 1 }]";
            Assert.IsFalse(BalanceConfigJson.TryParseEquipments(json, "t", out _, out var error));
            StringAssert.Contains("damageReductionPct", error);
        }

        /// <summary>Catches a typo in the shipped data files before it reaches StreamingAssets at build time.</summary>
        [Test]
        public void ShippedBalanceFiles_AllParseAndValidate()
        {
            var directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets/JungleMaze/balance"));
            Assert.IsTrue(BalanceConfigJson.TryParseMatch(File.ReadAllText(Path.Combine(directory, "match.json")), "match.json", out _, out var matchError), matchError);
            Assert.IsTrue(BalanceConfigJson.TryParseWeapons(File.ReadAllText(Path.Combine(directory, "weapons.json")), "weapons.json", out var weapons, out var weaponsError), weaponsError);
            Assert.IsTrue(BalanceConfigJson.TryParseEquipments(File.ReadAllText(Path.Combine(directory, "equipments.json")), "equipments.json", out var equipments, out var equipmentsError), equipmentsError);
            Assert.AreEqual(18, weapons.Count);
            Assert.AreEqual(16, equipments.Count);
        }
    }
}
