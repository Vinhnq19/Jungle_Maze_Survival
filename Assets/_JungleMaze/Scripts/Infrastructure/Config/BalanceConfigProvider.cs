using System;
using System.Collections.Generic;
using System.IO;
using JungleMaze.App.Ports;
using JungleMaze.Core.Logging;
using JungleMaze.Domain.Balance;

namespace JungleMaze.Infrastructure.Config
{
    /// <summary>
    /// Loads gameplay balance from StreamingAssets/JungleMaze/balance/{match,weapons,equipments}.json once at
    /// construction. A load/validation failure is logged and falls back to a safe default so the app keeps
    /// running (gameplay systems reading stale/placeholder numbers beats a crash at startup).
    /// </summary>
    public sealed class BalanceConfigProvider : IBalanceConfigProvider
    {
        public const string Folder = "JungleMaze/balance";

        private readonly Dictionary<string, WeaponDefinition> _weaponsById = new Dictionary<string, WeaponDefinition>();
        private readonly Dictionary<string, EquipmentDefinition> _equipmentsById = new Dictionary<string, EquipmentDefinition>();

        public BalanceConfigProvider(string streamingAssetsPath, ILog log)
        {
            log = log ?? NullLog.Instance;
            var directory = Path.Combine(streamingAssetsPath, Folder);
            var errors = new List<string>();

            Match = LoadFile(Path.Combine(directory, "match.json"), "match.json",
                (json, source) => BalanceConfigJson.TryParseMatch(json, source, out var value, out var error)
                    ? (value, error) : (null, error), errors) ?? Fallback();

            Weapons = LoadFile(Path.Combine(directory, "weapons.json"), "weapons.json",
                (json, source) => BalanceConfigJson.TryParseWeapons(json, source, out var value, out var error)
                    ? (value, error) : (null, error), errors) ?? new List<WeaponDefinition>();

            Equipments = LoadFile(Path.Combine(directory, "equipments.json"), "equipments.json",
                (json, source) => BalanceConfigJson.TryParseEquipments(json, source, out var value, out var error)
                    ? (value, error) : (null, error), errors) ?? new List<EquipmentDefinition>();

            foreach (var weapon in Weapons) _weaponsById[weapon.Id] = weapon;
            foreach (var equipment in Equipments) _equipmentsById[equipment.Id] = equipment;

            LastError = errors.Count > 0 ? string.Join(" | ", errors) : null;
            if (LastError != null) log.Error("[Balance] " + LastError);
            else log.Info($"[Balance] Loaded {Weapons.Count} weapons, {Equipments.Count} equipments, match={Match.MapSizeCells}x{Match.MapSizeCells}.");
        }

        public MatchConfigData Match { get; }
        public IReadOnlyList<WeaponDefinition> Weapons { get; }
        public IReadOnlyList<EquipmentDefinition> Equipments { get; }
        public string LastError { get; }

        public bool TryGetWeapon(string id, out WeaponDefinition weapon) => _weaponsById.TryGetValue(id ?? "", out weapon);
        public bool TryGetEquipment(string id, out EquipmentDefinition equipment) => _equipmentsById.TryGetValue(id ?? "", out equipment);

        private static T LoadFile<T>(string path, string source, Func<string, string, (T value, string error)> parse,
            List<string> errors) where T : class
        {
            string json;
            try { json = File.ReadAllText(path); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException
                                      || e is NotSupportedException)
            {
                errors.Add($"{source}: could not read file ({e.Message}).");
                return null;
            }
            var (value, error) = parse(json, source);
            if (error != null) errors.Add(error);
            return value;
        }

        /// <summary>Matches the GDD baseline so a missing/broken match.json still yields a playable config.</summary>
        private static MatchConfigData Fallback() => new MatchConfigData(
            matchDurationSeconds: 420f, lobbyDurationSeconds: 15f, maxPlayers: 10, minPlayersToStart: 6,
            mapSizeCells: 50, cellSizePixels: 48, heroMaxHp: 100, heroBaseSpeed: 4f, heroColliderRadius: 0.4f,
            visionRadiusCells: 12f, aimLerpTimeSeconds: 0.08f, weaponSwapTimeSeconds: 0.3f, pickupRadiusCells: 1.2f,
            armorCapPct: 0.7f, zoneShape: "circle", zoneShrinkSpeed: 1f, zoneDamageFloor: 5f,
            zoneRecalcIntervalSeconds: 10f, zoneWarningTimeSeconds: 10f, zoneCenterMarginCells: 2f, airdropCount: 3,
            airdropEdgeMarginCells: 3f, dragonFlyDurationSeconds: 10f, chestCount: 60, potCount: 90, bushCount: 45,
            portalPairs: 3, stoneDoorCycleSeconds: 20f, spawnMinDistanceCells: 6f, lootRespawn: false,
            disconnectGraceSeconds: 15f, killcamDurationSeconds: 5f, targetFps: 60);
    }
}
