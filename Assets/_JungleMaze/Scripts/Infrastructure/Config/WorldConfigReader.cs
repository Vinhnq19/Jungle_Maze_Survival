using System.IO;
using JungleMaze.World;
using JungleMaze.World.Inventory;
using Newtonsoft.Json;
namespace JungleMaze.Infrastructure.Config
{
    public sealed class WorldConfigData
    {
        public string Version="world-prototype-1";
        public uint Seed=20261028;
        public MapLayoutConfig Map=new MapLayoutConfig();
        public DestructibleConfig Destructibles=new DestructibleConfig();
        public HazardConfig Hazards=new HazardConfig();
        public static WorldConfigData Load(string streamingAssets)
        {
            var path=Path.Combine(streamingAssets,"JungleMaze/runtime/world_settings.json");
            var data=JsonConvert.DeserializeObject<WorldConfigData>(File.ReadAllText(path));
            if(data==null||data.Map==null||data.Destructibles==null||data.Hazards==null)throw new InvalidDataException("Missing world configuration sections.");
            data.Map.Version=data.Version;data.Map.Validate();data.Destructibles.Validate();data.Hazards.Validate();return data;
        }
        public static ItemCatalogDocument LoadItems(string streamingAssets)
        {
            var path=Path.Combine(streamingAssets,"JungleMaze/runtime/items_catalog.json");
            var document=JsonConvert.DeserializeObject<ItemCatalogDocument>(File.ReadAllText(path));
            if(document==null)throw new InvalidDataException("Missing item catalog.");
            document.Build();return document;
        }
    }
}
