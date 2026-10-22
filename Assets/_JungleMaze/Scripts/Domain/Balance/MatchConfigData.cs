namespace JungleMaze.Domain.Balance
{
    /// <summary>
    /// Match-wide balance parameters, loaded from StreamingAssets/JungleMaze/balance/match.json
    /// (source: GDD Sheet 14 Match Config, "Config" group only).
    /// The "ZONE BASELINE" and "AIRDROP BASELINE" schedule tables from the same sheet are intentionally
    /// NOT modeled here: the sheet marks them "cần thiết kế lại" (pending redesign), so they are not
    /// stable enough to encode yet. Extend this type and the JSON schema together when that data firms up.
    /// </summary>
    public sealed class MatchConfigData
    {
        public MatchConfigData(
            float matchDurationSeconds, float lobbyDurationSeconds, int maxPlayers, int minPlayersToStart,
            int mapSizeCells, int cellSizePixels, int heroMaxHp, float heroBaseSpeed, float heroColliderRadius,
            float visionRadiusCells, float aimLerpTimeSeconds, float weaponSwapTimeSeconds, float pickupRadiusCells,
            float armorCapPct, string zoneShape, float zoneShrinkSpeed, float zoneDamageFloor,
            float zoneRecalcIntervalSeconds, float zoneWarningTimeSeconds, float zoneCenterMarginCells,
            int airdropCount, float airdropEdgeMarginCells, float dragonFlyDurationSeconds, int chestCount,
            int potCount, int bushCount, int portalPairs, float stoneDoorCycleSeconds, float spawnMinDistanceCells,
            bool lootRespawn, float disconnectGraceSeconds, float killcamDurationSeconds, int targetFps)
        {
            MatchDurationSeconds = matchDurationSeconds;
            LobbyDurationSeconds = lobbyDurationSeconds;
            MaxPlayers = maxPlayers;
            MinPlayersToStart = minPlayersToStart;
            MapSizeCells = mapSizeCells;
            CellSizePixels = cellSizePixels;
            HeroMaxHp = heroMaxHp;
            HeroBaseSpeed = heroBaseSpeed;
            HeroColliderRadius = heroColliderRadius;
            VisionRadiusCells = visionRadiusCells;
            AimLerpTimeSeconds = aimLerpTimeSeconds;
            WeaponSwapTimeSeconds = weaponSwapTimeSeconds;
            PickupRadiusCells = pickupRadiusCells;
            ArmorCapPct = armorCapPct;
            ZoneShape = zoneShape;
            ZoneShrinkSpeed = zoneShrinkSpeed;
            ZoneDamageFloor = zoneDamageFloor;
            ZoneRecalcIntervalSeconds = zoneRecalcIntervalSeconds;
            ZoneWarningTimeSeconds = zoneWarningTimeSeconds;
            ZoneCenterMarginCells = zoneCenterMarginCells;
            AirdropCount = airdropCount;
            AirdropEdgeMarginCells = airdropEdgeMarginCells;
            DragonFlyDurationSeconds = dragonFlyDurationSeconds;
            ChestCount = chestCount;
            PotCount = potCount;
            BushCount = bushCount;
            PortalPairs = portalPairs;
            StoneDoorCycleSeconds = stoneDoorCycleSeconds;
            SpawnMinDistanceCells = spawnMinDistanceCells;
            LootRespawn = lootRespawn;
            DisconnectGraceSeconds = disconnectGraceSeconds;
            KillcamDurationSeconds = killcamDurationSeconds;
            TargetFps = targetFps;
        }

        public float MatchDurationSeconds { get; }
        public float LobbyDurationSeconds { get; }
        public int MaxPlayers { get; }
        public int MinPlayersToStart { get; }
        /// <summary>Map is a square of MapSizeCells x MapSizeCells; 1 Unity world unit == 1 cell.</summary>
        public int MapSizeCells { get; }
        /// <summary>Sprite pixels per cell (art/PPU convention only, not a world-unit scale).</summary>
        public int CellSizePixels { get; }
        public int HeroMaxHp { get; }
        public float HeroBaseSpeed { get; }
        public float HeroColliderRadius { get; }
        public float VisionRadiusCells { get; }
        public float AimLerpTimeSeconds { get; }
        public float WeaponSwapTimeSeconds { get; }
        public float PickupRadiusCells { get; }
        public float ArmorCapPct { get; }
        public string ZoneShape { get; }
        public float ZoneShrinkSpeed { get; }
        public float ZoneDamageFloor { get; }
        public float ZoneRecalcIntervalSeconds { get; }
        public float ZoneWarningTimeSeconds { get; }
        public float ZoneCenterMarginCells { get; }
        public int AirdropCount { get; }
        public float AirdropEdgeMarginCells { get; }
        public float DragonFlyDurationSeconds { get; }
        public int ChestCount { get; }
        public int PotCount { get; }
        public int BushCount { get; }
        public int PortalPairs { get; }
        public float StoneDoorCycleSeconds { get; }
        public float SpawnMinDistanceCells { get; }
        public bool LootRespawn { get; }
        public float DisconnectGraceSeconds { get; }
        public float KillcamDurationSeconds { get; }
        public int TargetFps { get; }

        /// <summary>Returns the first validation error, or null if every value is in a usable range.</summary>
        public string Validate()
        {
            if (MatchDurationSeconds <= 0f) return "matchDuration must be > 0.";
            if (MaxPlayers <= 0) return "maxPlayers must be > 0.";
            if (MinPlayersToStart <= 0 || MinPlayersToStart > MaxPlayers)
                return "minPlayersToStart must be in (0, maxPlayers].";
            if (MapSizeCells <= 0) return "mapSize must be > 0.";
            if (CellSizePixels <= 0) return "cellSize must be > 0.";
            if (HeroMaxHp <= 0) return "heroMaxHP must be > 0.";
            if (HeroBaseSpeed <= 0f) return "heroBaseSpeed must be > 0.";
            if (HeroColliderRadius <= 0f) return "heroColliderRadius must be > 0.";
            if (AimLerpTimeSeconds < 0f) return "aimLerpTime must be >= 0.";
            if (ArmorCapPct < 0f || ArmorCapPct > 1f) return "armorCapPct must be in [0, 1].";
            if (TargetFps <= 0) return "targetFPS must be > 0.";
            return null;
        }
    }
}
