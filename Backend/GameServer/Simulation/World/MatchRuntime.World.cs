using JungleBackend.Protocol.Gameplay;
using JungleMaze.World;
using JungleMaze.World.Inventory;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace GameServer.Simulation;

public sealed partial class MatchRuntime
{
    public MatchWorldRuntime? World {get;private set;}
    private MapData? _worldMap;
    private PhysicsSimulation? _worldPhysics;
    private WorldSettings? _worldSettings;
    private ItemCatalog? _worldCatalog;
    private LootProfile? _worldLootProfile;

    private void ConfigureWorld()
    {
        var mode = Environment.GetEnvironmentVariable("WORLD_ENABLED");
        if (mode != "1" && (mode == "0" || !_profile.WorldEnabled)) return;
        _worldSettings = WorldSettings.Load(GameDataLoader.ReadGameDataFile("world_settings.json"));
        var options = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        var document = JsonSerializer.Deserialize<ItemCatalogDocument>(GameDataLoader.ReadGameDataFile("items_catalog.json"), options)
            ?? throw new InvalidDataException("Missing item catalog.");
        _worldCatalog = document.Build();
        _worldLootProfile = document.Loot;
    }

    public bool EnsureWorld(Guid matchId)
    {
        return matchId == MatchId && Phase == MatchPhase.InGame && World != null;
    }

    private void StartWorld()
    {
        if (_worldSettings == null || _worldCatalog == null) return;
        World=new MatchWorldRuntime(MatchId,_worldSettings,this,_health,_worldCatalog,_worldLootProfile);
        _worldMap=new MapData{Version=_map.Version,MinX=0,MinY=0,MaxX=World.Layout.Size,MaxY=World.Layout.Size};
        _worldPhysics=new PhysicsSimulation(_worldMap,_profile.PlayerRadius,_profile.PlayerSpeed);
        var i=0;foreach(var entity in _entities.Values){var spawn=World.Layout.Spawns[i++%World.Layout.Spawns.Count];entity.X=spawn.X;entity.Y=spawn.Y;}
        World.Tick(CurrentTick, _matchElapsedSeconds);RefreshWorldGeometry();
        _combat = CreateCombat();
    }
    public bool ProcessWorldRequest(string sessionId,in WorldActionRequestPacket request)
    {
        if(request.MatchId!=MatchId||Phase!=MatchPhase.InGame||!_bySession.TryGetValue(sessionId,out var actor)||!actor.IsAlive||actor.IsDisconnected)return false;
        if(request.Action==WorldActionKind.Resync)return EnsureWorld(request.MatchId);
        if(World==null)return false;
        return World.Request(actor.NetId,request)==ItemResult.Ok;
    }
    private void TickWorld()
    {
        if(World==null)return;
        World.Tick(CurrentTick,_matchElapsedSeconds);
        RefreshWorldGeometry();
    }
    private void RefreshWorldGeometry()
    {
        if(World==null||_worldMap==null)return;
        _worldMap.Obstacles.Clear();
        foreach(var o in World.Layout.Objects)
            if(o.IsHardCover||(o.IsDestructible&&!World.Destructibles.IsBroken(o.Id))||(o.Kind==WorldObjectKind.Door&&World.Hazards.DoorsClosed))
                _worldMap.Obstacles.Add(new AABB(o.Kind+"-"+o.Id,o.Bounds.MinX,o.Bounds.MaxX,o.Bounds.MinY,o.Bounds.MaxY));
    }
    private void ResetWorld(){World=null;_worldMap=null;_worldPhysics=null;}
}
