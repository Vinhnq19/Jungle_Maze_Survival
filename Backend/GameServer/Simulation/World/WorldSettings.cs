using System.Text.Json;
using JungleMaze.World;
namespace GameServer.Simulation;
public sealed class WorldSettings
{
    public string Version="world-prototype-1";
    public uint Seed=20261028;
    public string LayoutMode="Authored";
    public MapLayout? Layout;
    public MapLayoutConfig Map=new();
    public DestructibleConfig Destructibles=new();
    public HazardConfig Hazards=new();
    public static WorldSettings Load(string json)
    {
        var settings=JsonSerializer.Deserialize<WorldSettings>(json,new JsonSerializerOptions{IncludeFields=true,PropertyNameCaseInsensitive=true})??throw new ArgumentException("Missing world config.");
        settings.Map.Version=settings.Version;
        var mode = Environment.GetEnvironmentVariable("WORLD_LAYOUT_MODE");
        if (!string.IsNullOrEmpty(mode)) settings.LayoutMode = mode;
        if (settings.LayoutMode != "Authored" && settings.LayoutMode != "Prototype") throw new ArgumentException("Unknown world layout mode.");
        settings.Map.Validate();settings.Destructibles.Validate();settings.Hazards.Validate();
        return settings;
    }
}
