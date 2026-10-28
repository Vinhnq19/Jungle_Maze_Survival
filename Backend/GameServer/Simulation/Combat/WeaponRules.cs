using System.Text.Json;
using JungleBackend.Protocol.Gameplay;

namespace GameServer.Simulation.Combat;

/// <summary>Server-side combat rules of one weapon, read from balance/weapons.json. Holds no client logic.</summary>
public sealed class WeaponRule
{
    public required CombatWeaponId Id { get; init; }
    public required bool IsRanged { get; init; }
    public required int Damage { get; init; }
    public required double CooldownSeconds { get; init; }
    public required float Range { get; init; }
    public float ArcDegrees { get; init; }
    public int Pellets { get; init; } = 1;
    public float SpreadDegrees { get; init; }
    public int MagazineSize { get; init; }
    public int ReserveAmmo { get; init; }
    public double ReloadSeconds { get; init; }
    public double StunSeconds { get; init; }
    public bool IsBreakableAxe { get; init; }
}

/// <summary>Validated weapon rule table. Built only from valid JSON; unknown weapons are rejected, never skipped.</summary>
public sealed class WeaponRules
{
    /// <summary>Reload time used when the JSON has no reloadSeconds. GDD range is 1.2–2.8 s; per-weapon values are still pending.</summary>
    public const double DefaultReloadSeconds = 2.0;

    /// <summary>Total spread of the Shotgun cone when the JSON has no spreadDegrees (GDD: 25°).</summary>
    public const float DefaultSpreadDegrees = 25f;

    /// <summary>Axe damage multiplier against breakable obstacles (GDD: +50%).</summary>
    public const float AxeBreakableMultiplier = 1.5f;

    private static readonly IReadOnlyDictionary<string, CombatWeaponId> IdMap = new Dictionary<string, CombatWeaponId>
    {
        ["HAND"] = CombatWeaponId.Hand,
        ["TREE_BRANCH"] = CombatWeaponId.TreeBranch,
        ["KNIFE"] = CombatWeaponId.Knife,
        ["BONE"] = CombatWeaponId.Bone,
        ["AXE"] = CombatWeaponId.Axe,
        ["MACE"] = CombatWeaponId.Mace,
        ["RUBBER_GUN"] = CombatWeaponId.RubberGun,
        ["SCEPTER"] = CombatWeaponId.Scepter,
        ["CROSSBOW"] = CombatWeaponId.Crossbow,
        ["SHOTGUN"] = CombatWeaponId.Shotgun
    };

    private readonly Dictionary<CombatWeaponId, WeaponRule> _rules;

    private WeaponRules(Dictionary<CombatWeaponId, WeaponRule> rules)
    {
        _rules = rules;
    }

    public int Count => _rules.Count;

    public bool TryGet(CombatWeaponId id, out WeaponRule rule) => _rules.TryGetValue(id, out rule!);

    /// <summary>Parses and validates the whole table. Throws on a missing or duplicate weapon, an unknown id, or invalid numbers.</summary>
    public static WeaponRules Load(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var rules = new Dictionary<CombatWeaponId, WeaponRule>();

        foreach (var elem in doc.RootElement.EnumerateArray())
        {
            var id = elem.GetProperty("id").GetString() ?? throw new ArgumentException("Weapon id is missing.");
            var category = elem.GetProperty("category").GetString();

            // Thrown and special weapons belong to WPN-03 and are not part of this table.
            if (category is "Thrown" or "Special") continue;

            if (!IdMap.TryGetValue(id, out var weapon))
                throw new ArgumentException($"Unknown weapon id '{id}' in weapons.json.");
            if (rules.ContainsKey(weapon))
                throw new ArgumentException($"Duplicate weapon id '{id}' in weapons.json.");

            var isRanged = category switch
            {
                "Ranged" => true,
                "Melee" => false,
                _ => throw new ArgumentException($"Weapon '{id}' has unknown category '{category}'.")
            };

            var rule = new WeaponRule
            {
                Id = weapon,
                IsRanged = isRanged,
                Damage = Read<int>(elem, "damage"),
                Range = Read<float>(elem, "range"),
                CooldownSeconds = isRanged ? 1.0 / Read<double>(elem, "fireRatePerSecond") : ReadOr(elem, "cooldownSeconds", 0.0),
                ArcDegrees = ReadOr(elem, "arcDegrees", 0f),
                Pellets = ReadOr(elem, "pellets", 1),
                SpreadDegrees = ReadOr(elem, "spreadDegrees", elem.TryGetProperty("pellets", out _) ? DefaultSpreadDegrees : 0f),
                MagazineSize = isRanged ? Read<int>(elem, "magazineSize") : 0,
                ReserveAmmo = isRanged ? Read<int>(elem, "reserveAmmo") : 0,
                ReloadSeconds = ReadOr(elem, "reloadSeconds", DefaultReloadSeconds),
                StunSeconds = ReadOr(elem, "stunSeconds", 0.0),
                IsBreakableAxe = weapon == CombatWeaponId.Axe
            };

            ValidateRule(rule);
            rules[weapon] = rule;
        }

        foreach (CombatWeaponId id in Enum.GetValues<CombatWeaponId>())
        {
            if (!rules.ContainsKey(id))
                throw new ArgumentException($"weapons.json is missing weapon {id}.");
        }

        return new WeaponRules(rules);
    }

    private static void ValidateRule(WeaponRule rule)
    {
        if (rule.Damage < 0 || rule.Range <= 0f || rule.CooldownSeconds <= 0.0 || rule.Pellets < 1)
            throw new ArgumentException($"Weapon {rule.Id} has invalid combat numbers.");
        if (rule.IsRanged && (rule.MagazineSize < 1 || rule.ReserveAmmo < 0))
            throw new ArgumentException($"Ranged weapon {rule.Id} has invalid ammo numbers.");
        if (rule.ReloadSeconds <= 0.0)
            throw new ArgumentException($"Weapon {rule.Id} has invalid reload time.");
    }

    private static T Read<T>(JsonElement elem, string name) where T : struct
    {
        if (!elem.TryGetProperty(name, out var value))
            throw new ArgumentException($"Weapon field '{name}' is missing.");
        return typeof(T) == typeof(int) ? (T)(object)value.GetInt32()
            : typeof(T) == typeof(float) ? (T)(object)(float)value.GetDouble()
            : (T)(object)value.GetDouble();
    }

    private static T ReadOr<T>(JsonElement elem, string name, T fallback) where T : struct
    {
        if (!elem.TryGetProperty(name, out var value)) return fallback;
        return typeof(T) == typeof(int) ? (T)(object)value.GetInt32()
            : typeof(T) == typeof(float) ? (T)(object)(float)value.GetDouble()
            : (T)(object)value.GetDouble();
    }
}
