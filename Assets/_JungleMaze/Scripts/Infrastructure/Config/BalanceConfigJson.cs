using System;
using System.Collections.Generic;
using System.Globalization;
using JungleMaze.Domain.Balance;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JungleMaze.Infrastructure.Config
{
    /// <summary>
    /// Parses the gameplay balance JSON files (match/weapons/equipments config, source: the GDD spreadsheet).
    /// Unknown keys and missing required keys are rejected, mirroring <c>ClientConfigJson</c>'s strictness so a
    /// typo'd field name fails loudly at load time instead of silently falling back to a default.
    /// </summary>
    public static class BalanceConfigJson
    {
        private static readonly HashSet<string> MatchKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "matchDuration", "lobbyDuration", "maxPlayers", "minPlayersToStart", "mapSize", "cellSize",
            "heroMaxHP", "heroBaseSpeed", "heroColliderRadius", "visionRadius", "aimLerpTime", "weaponSwapTime",
            "pickupRadius", "armorCapPct", "zoneShape", "zoneShrinkSpeed", "zoneDamageFloor", "zoneRecalcInterval",
            "zoneWarningTime", "zoneCenterMargin", "airdropCount", "airdropEdgeMargin", "dragonFlyDuration",
            "chestCount", "potCount", "bushCount", "portalPairs", "stoneDoorCycle", "spawnMinDistance",
            "lootRespawn", "disconnectGrace", "killcamDuration", "targetFPS"
        };

        private static readonly HashSet<string> WeaponKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "id", "displayName", "category", "damage", "cooldownSeconds", "range", "arcDegrees", "stunSeconds",
            "fireRatePerSecond", "projectileSpeed", "pellets", "magazineSize", "reserveAmmo", "fuseSeconds",
            "areaWidth", "areaHeight", "carry", "charges", "reloadSeconds", "spreadDegrees"
        };

        private static readonly HashSet<string> EquipmentKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "id", "displayName", "type", "damageReductionPct", "durabilityPoints", "healAmount", "useSeconds",
            "maxCarry", "effectDescription"
        };

        public static bool TryParseMatch(string json, string source, out MatchConfigData config, out string error)
        {
            config = null;
            error = null;
            if (!TryParseObject(json, source, out var root)) { error = $"{source}: invalid JSON."; return false; }
            var values = IndexProperties(root, source, MatchKeys, out error);
            if (values == null) return false;

            string err = null;
            bool F(string key, out float v)
            {
                v = 0f;
                if (!values.TryGetValue(key, out var t)) { err = $"{source}: missing required key '{key}'."; return false; }
                if (!TryFloat(t, out v)) { err = $"{source}: '{key}' must be a number."; return false; }
                return true;
            }
            bool I(string key, out int v)
            {
                v = 0;
                if (!values.TryGetValue(key, out var t)) { err = $"{source}: missing required key '{key}'."; return false; }
                if (!TryInt(t, out v)) { err = $"{source}: '{key}' must be an integer."; return false; }
                return true;
            }
            bool B(string key, out bool v)
            {
                v = false;
                if (!values.TryGetValue(key, out var t)) { err = $"{source}: missing required key '{key}'."; return false; }
                if (!TryBool(t, out v)) { err = $"{source}: '{key}' must be a boolean."; return false; }
                return true;
            }
            bool S(string key, out string v)
            {
                v = null;
                if (!values.TryGetValue(key, out var t)) { err = $"{source}: missing required key '{key}'."; return false; }
                v = t.ToString();
                return true;
            }

            if (!F("matchDuration", out var matchDuration) || !F("lobbyDuration", out var lobbyDuration)
                || !I("maxPlayers", out var maxPlayers) || !I("minPlayersToStart", out var minPlayersToStart)
                || !I("mapSize", out var mapSize) || !I("cellSize", out var cellSize)
                || !I("heroMaxHP", out var heroMaxHp) || !F("heroBaseSpeed", out var heroBaseSpeed)
                || !F("heroColliderRadius", out var heroColliderRadius) || !F("visionRadius", out var visionRadius)
                || !F("aimLerpTime", out var aimLerpTime) || !F("weaponSwapTime", out var weaponSwapTime)
                || !F("pickupRadius", out var pickupRadius) || !F("armorCapPct", out var armorCapPct)
                || !S("zoneShape", out var zoneShape) || !F("zoneShrinkSpeed", out var zoneShrinkSpeed)
                || !F("zoneDamageFloor", out var zoneDamageFloor) || !F("zoneRecalcInterval", out var zoneRecalcInterval)
                || !F("zoneWarningTime", out var zoneWarningTime) || !F("zoneCenterMargin", out var zoneCenterMargin)
                || !I("airdropCount", out var airdropCount) || !F("airdropEdgeMargin", out var airdropEdgeMargin)
                || !F("dragonFlyDuration", out var dragonFlyDuration) || !I("chestCount", out var chestCount)
                || !I("potCount", out var potCount) || !I("bushCount", out var bushCount)
                || !I("portalPairs", out var portalPairs) || !F("stoneDoorCycle", out var stoneDoorCycle)
                || !F("spawnMinDistance", out var spawnMinDistance) || !B("lootRespawn", out var lootRespawn)
                || !F("disconnectGrace", out var disconnectGrace) || !F("killcamDuration", out var killcamDuration)
                || !I("targetFPS", out var targetFps))
            {
                error = err;
                return false;
            }

            config = new MatchConfigData(matchDuration, lobbyDuration, maxPlayers, minPlayersToStart, mapSize,
                cellSize, heroMaxHp, heroBaseSpeed, heroColliderRadius, visionRadius, aimLerpTime, weaponSwapTime,
                pickupRadius, armorCapPct, zoneShape, zoneShrinkSpeed, zoneDamageFloor, zoneRecalcInterval,
                zoneWarningTime, zoneCenterMargin, airdropCount, airdropEdgeMargin, dragonFlyDuration, chestCount,
                potCount, bushCount, portalPairs, stoneDoorCycle, spawnMinDistance, lootRespawn, disconnectGrace,
                killcamDuration, targetFps);
            error = config.Validate();
            return error == null;
        }

        public const float DefaultReloadSeconds = 2f;
        public const float DefaultSpreadDegrees = 25f;

        public static bool TryParseWeapons(string json, string source, out List<WeaponDefinition> weapons, out string error)
        {
            weapons = new List<WeaponDefinition>();
            error = null;
            if (!TryParseArray(json, source, out var array)) { error = $"{source}: expected a JSON array."; return false; }

            foreach (var item in array)
            {
                if (!(item is JObject obj)) { error = $"{source}: every entry must be an object."; return false; }
                var values = IndexProperties(obj, source, WeaponKeys, out error);
                if (values == null) return false;

                if (!Require(values, "id", source, out var id, out error)) return false;
                if (!Require(values, "displayName", source, out var displayName, out error)) return false;
                if (!Require(values, "category", source, out var categoryText, out error)) return false;
                if (!TryCategory(categoryText, out var category))
                {
                    error = $"{source}: {id} has unknown category '{categoryText}'.";
                    return false;
                }
                if (!RequireFloat(values, "damage", source, id, out var damage, out error)) return false;
                if (!RequireFloat(values, "range", source, id, out var range, out error)) return false;

                var stats = new WeaponStats
                {
                    Damage = damage,
                    CooldownSeconds = OptionalFloat(values, "cooldownSeconds"),
                    Range = range,
                    ArcDegrees = OptionalFloat(values, "arcDegrees"),
                    StunSeconds = OptionalFloat(values, "stunSeconds"),
                    FireRatePerSecond = OptionalFloat(values, "fireRatePerSecond"),
                    ProjectileSpeed = OptionalFloat(values, "projectileSpeed"),
                    Pellets = OptionalInt(values, "pellets"),
                    MagazineSize = OptionalInt(values, "magazineSize"),
                    ReserveAmmo = OptionalInt(values, "reserveAmmo"),
                    FuseSeconds = OptionalFloat(values, "fuseSeconds"),
                    AreaWidth = OptionalFloat(values, "areaWidth"),
                    AreaHeight = OptionalFloat(values, "areaHeight"),
                    Carry = OptionalInt(values, "carry"),
                    Charges = OptionalInt(values, "charges"),
                    ReloadSeconds = OptionalFloat(values, "reloadSeconds"),
                    SpreadDegrees = OptionalFloat(values, "spreadDegrees")
                };
                // WPN-01 defaults (reload 1.2-2.8 s band, midpoint 2.0; shotgun cone 25 degrees). Override in JSON.
                if (stats.ReloadSeconds == null && category == WeaponCategory.Ranged && stats.MagazineSize.HasValue)
                    stats.ReloadSeconds = DefaultReloadSeconds;
                if (stats.SpreadDegrees == null && stats.Pellets.HasValue)
                    stats.SpreadDegrees = DefaultSpreadDegrees;

                var weapon = new WeaponDefinition(id, displayName, category, stats);
                error = weapon.Validate();
                if (error != null) return false;
                weapons.Add(weapon);
            }
            return true;
        }

        public static bool TryParseEquipments(string json, string source, out List<EquipmentDefinition> equipments, out string error)
        {
            equipments = new List<EquipmentDefinition>();
            error = null;
            if (!TryParseArray(json, source, out var array)) { error = $"{source}: expected a JSON array."; return false; }

            foreach (var item in array)
            {
                if (!(item is JObject obj)) { error = $"{source}: every entry must be an object."; return false; }
                var values = IndexProperties(obj, source, EquipmentKeys, out error);
                if (values == null) return false;

                if (!Require(values, "id", source, out var id, out error)) return false;
                if (!Require(values, "displayName", source, out var displayName, out error)) return false;
                if (!Require(values, "type", source, out var typeText, out error)) return false;
                if (!TryEquipmentType(typeText, out var type))
                {
                    error = $"{source}: {id} has unknown type '{typeText}'.";
                    return false;
                }
                if (!RequireFloat(values, "healAmount", source, id, out var healAmount, out error)) return false;
                if (!RequireFloat(values, "useSeconds", source, id, out var useSeconds, out error)) return false;
                if (!RequireInt(values, "maxCarry", source, id, out var maxCarry, out error)) return false;

                var stats = new EquipmentStats
                {
                    DamageReductionPct = OptionalFloat(values, "damageReductionPct"),
                    DurabilityPoints = OptionalFloat(values, "durabilityPoints"),
                    HealAmount = healAmount,
                    UseSeconds = useSeconds,
                    MaxCarry = maxCarry
                };
                var effect = values.TryGetValue("effectDescription", out var effectToken) ? effectToken.ToString() : "";

                var equipment = new EquipmentDefinition(id, displayName, type, stats, effect);
                error = equipment.Validate();
                if (error != null) return false;
                equipments.Add(equipment);
            }
            return true;
        }

        private static bool TryParseObject(string json, string source, out JObject root)
        {
            try { root = JObject.Parse(json ?? ""); return true; }
            catch (JsonException) { root = null; return false; }
        }

        private static bool TryParseArray(string json, string source, out JArray array)
        {
            try { array = JArray.Parse(json ?? ""); return true; }
            catch (JsonException) { array = null; return false; }
        }

        private static Dictionary<string, JToken> IndexProperties(JObject root, string source, HashSet<string> known, out string error)
        {
            var values = new Dictionary<string, JToken>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in root.Properties())
            {
                if (!known.Contains(property.Name)) { error = $"{source}: unsupported key '{property.Name}'."; return null; }
                values[property.Name] = property.Value;
            }
            error = null;
            return values;
        }

        private static bool Require(Dictionary<string, JToken> values, string key, string source, out string value, out string error)
        {
            value = null;
            if (!values.TryGetValue(key, out var token)) { error = $"{source}: missing required key '{key}'."; return false; }
            value = token.ToString();
            error = null;
            return true;
        }

        private static bool RequireFloat(Dictionary<string, JToken> values, string key, string source, string id, out float value, out string error)
        {
            value = 0f;
            if (!values.TryGetValue(key, out var token)) { error = $"{source}: {id} missing required key '{key}'."; return false; }
            if (!TryFloat(token, out value)) { error = $"{source}: {id}.{key} must be a number."; return false; }
            error = null;
            return true;
        }

        private static bool RequireInt(Dictionary<string, JToken> values, string key, string source, string id, out int value, out string error)
        {
            value = 0;
            if (!values.TryGetValue(key, out var token)) { error = $"{source}: {id} missing required key '{key}'."; return false; }
            if (!TryInt(token, out value)) { error = $"{source}: {id}.{key} must be an integer."; return false; }
            error = null;
            return true;
        }

        private static float? OptionalFloat(Dictionary<string, JToken> values, string key) =>
            values.TryGetValue(key, out var token) && TryFloat(token, out var value) ? value : (float?)null;

        private static int? OptionalInt(Dictionary<string, JToken> values, string key) =>
            values.TryGetValue(key, out var token) && TryInt(token, out var value) ? value : (int?)null;

        private static bool TryFloat(JToken token, out float value)
        {
            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer) { value = token.Value<float>(); return true; }
            if (token.Type == JTokenType.String)
                return float.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            value = 0f;
            return false;
        }

        private static bool TryInt(JToken token, out int value)
        {
            if (token.Type == JTokenType.Integer) { value = token.Value<int>(); return true; }
            if (token.Type == JTokenType.String)
                return int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            value = 0;
            return false;
        }

        private static bool TryBool(JToken token, out bool value)
        {
            if (token.Type == JTokenType.Boolean) { value = token.Value<bool>(); return true; }
            if (token.Type == JTokenType.String) return bool.TryParse(token.ToString(), out value);
            value = false;
            return false;
        }

        private static bool TryCategory(string text, out WeaponCategory category)
        {
            switch (text)
            {
                case "Melee": category = WeaponCategory.Melee; return true;
                case "Ranged": category = WeaponCategory.Ranged; return true;
                case "Thrown": category = WeaponCategory.Thrown; return true;
                case "Special": category = WeaponCategory.Special; return true;
                default: category = default; return false;
            }
        }

        private static bool TryEquipmentType(string text, out EquipmentType type)
        {
            switch (text)
            {
                case "Helmet": type = EquipmentType.Helmet; return true;
                case "Armor": type = EquipmentType.Armor; return true;
                case "Healing": type = EquipmentType.Healing; return true;
                case "Support": type = EquipmentType.Support; return true;
                case "Airdrop": type = EquipmentType.Airdrop; return true;
                default: type = default; return false;
            }
        }
    }
}
