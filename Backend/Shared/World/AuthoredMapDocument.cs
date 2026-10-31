#nullable disable
using System;
using System.Collections.Generic;
namespace JungleMaze.World
{
    /// <summary>Reads the team's map data without generating or moving any authored geometry.</summary>
    public sealed class AuthoredMapDocument
    {
        public sealed class Bounds { public float MinX, MinY, MaxX, MaxY; }
        public sealed class Cover { public string Name; public float MinX, MinY, MaxX, MaxY; }
        public string Version;
        public Bounds MapBounds;
        public Cover[] Obstacles = Array.Empty<Cover>();
        public WorldPoint[] SpawnPoints = Array.Empty<WorldPoint>();
        // Optional placements exported by the map owner. Empty means no interactive objects yet.
        public WorldObjectDefinition[] WorldObjects = Array.Empty<WorldObjectDefinition>();

        public MapLayout Build(string worldVersion)
        {
            if (MapBounds == null || Obstacles == null || SpawnPoints == null || WorldObjects == null ||
                MapBounds.MinX != 0 || MapBounds.MinY != 0 || MapBounds.MaxX != MapBounds.MaxY ||
                MapBounds.MaxX != Math.Floor(MapBounds.MaxX))
                throw new ArgumentException("Authored world requires a square map with origin (0,0).");
            var map = new MapLayout { Version = worldVersion + "/" + Version, Size = (int)MapBounds.MaxX, Seed = 0 };
            foreach (var spawn in SpawnPoints) map.Spawns.Add(spawn);
            ushort id = 0;
            foreach (var cover in Obstacles)
            {
                if (cover == null) throw new ArgumentException("Missing authored cover.");
                var kind = cover.Name == "AncientPillar" ? WorldObjectKind.Pillar :
                    cover.Name == "AncientStatue" ? WorldObjectKind.Statue :
                    cover.Name == "BoulderRock" ? WorldObjectKind.Rock : WorldObjectKind.Wall;
                map.Objects.Add(new WorldObjectDefinition { Id = ++id, Kind = kind,
                    Bounds = new WorldBounds { MinX = cover.MinX, MinY = cover.MinY, MaxX = cover.MaxX, MaxY = cover.MaxY } });
            }
            var validation = new MapLayoutConfig { Version = map.Version, Size = map.Size, SpawnCount = map.Spawns.Count,
                ChestCount = 0, PotCount = 0, BushCount = 0, PortalPairs = 0, DoorCount = 0, SwampCount = 0, LavaCount = 0, PoisonCount = 0 };
            var portals = 0;
            foreach (var item in WorldObjects)
            {
                if (item == null || item.Kind < WorldObjectKind.Chest || item.Kind > WorldObjectKind.Supply)
                    throw new ArgumentException("Only interactive objects belong in authored world placements.");
                map.Objects.Add(new WorldObjectDefinition { Id = ++id, Kind = item.Kind, Bounds = item.Bounds, PairId = item.PairId });
                switch (item.Kind)
                {
                    case WorldObjectKind.Chest: validation.ChestCount++; break;
                    case WorldObjectKind.Pot: validation.PotCount++; break;
                    case WorldObjectKind.Bush: validation.BushCount++; break;
                    case WorldObjectKind.Portal: portals++; break;
                    case WorldObjectKind.Door: validation.DoorCount++; break;
                    case WorldObjectKind.Swamp: validation.SwampCount++; break;
                    case WorldObjectKind.Lava: validation.LavaCount++; break;
                    case WorldObjectKind.Poison: validation.PoisonCount++; break;
                }
            }
            if (portals % 2 != 0) throw new ArgumentException("Authored portals require pairs.");
            validation.PortalPairs = portals / 2;
            MapLayoutGenerator.Validate(map, validation);
            map.Hash = MapLayoutGenerator.ComputeHash(map);
            return map;
        }
    }
}
