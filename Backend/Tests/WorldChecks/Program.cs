using JungleMaze.World;
using JungleBackend.Protocol.Gameplay;
using GameServer.Simulation;
using LiteNetLib.Utils;
using JungleMaze.World.Inventory;

var checks=0;
void Check(bool okay,string name){checks++;if(!okay)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);}
void Throws(Action action,string name){try{action();}catch(ArgumentException){Check(true,name);return;}throw new Exception("Expected rejection: "+name);}
var config=new MapLayoutConfig();
var map=MapLayoutGenerator.Generate(config,20261028);
Check(map.Objects.Count(o=>o.Kind==WorldObjectKind.Chest)==60,"60 chests");
Check(map.Objects.Count(o=>o.Kind==WorldObjectKind.Pot)==90,"90 pots");
Check(map.Objects.Count(o=>o.Kind==WorldObjectKind.Bush)==45,"45 bushes");
Check(map.Objects.Count(o=>o.Kind==WorldObjectKind.Portal)==6,"3 portal pairs");
Check(map.Spawns.Count==10,"10 spawns");
Check(map.Hash==MapLayoutGenerator.Generate(config,20261028).Hash,"stable seed and hash");
Check(map.Hash!=MapLayoutGenerator.Generate(config,20261029).Hash,"different seed changes layout");
for(uint seed=1;seed<=12;seed++){var seeded=MapLayoutGenerator.Generate(config,seed);MapLayoutGenerator.Validate(seeded,config);Check(seeded.Hash.Length==64,"valid deterministic layout seed "+seed);}
Throws(()=>MapLayoutGenerator.Generate(new MapLayoutConfig{ChestCount=4000},1),"bounded impossible placement rejects");
Throws(()=>MapLayoutGenerator.Generate(new MapLayoutConfig{HeroRadius=float.NaN},1),"nonfinite radius rejects");
var loot=new FakePorts();var destructibles=new DestructibleService(map,new DestructibleConfig(),loot);var chest=map.Objects.First(o=>o.Kind==WorldObjectKind.Chest);
Check(destructibles.DamageConfirmed(chest.Id,20,0,1),"confirmed damage applied");
Check(!destructibles.DamageConfirmed(chest.Id,20,0,1),"duplicate damage ignored");
Check(destructibles.DamageConfirmed(chest.Id,20,1,1),"second attacker breaks chest");
Check(loot.LootCount==1&&destructibles.IsBroken(chest.Id),"single loot batch and broken state");
Check(!destructibles.DamageConfirmed(chest.Id,100,1,2)&&loot.LootCount==1,"broken cannot respawn loot");
var hazardMap=new MapLayout{Version="test",Size=50};
hazardMap.Objects.Add(new WorldObjectDefinition{Id=1,Kind=WorldObjectKind.Lava,Bounds=new WorldBounds(5,5,3,3)});
hazardMap.Objects.Add(new WorldObjectDefinition{Id=2,Kind=WorldObjectKind.Swamp,Bounds=new WorldBounds(10,10,4,4)});
hazardMap.Objects.Add(new WorldObjectDefinition{Id=3,Kind=WorldObjectKind.Poison,Bounds=new WorldBounds(15,15,1,1)});
hazardMap.Objects.Add(new WorldObjectDefinition{Id=4,Kind=WorldObjectKind.Portal,Bounds=new WorldBounds(20,20,1,1),PairId=1});
hazardMap.Objects.Add(new WorldObjectDefinition{Id=5,Kind=WorldObjectKind.Portal,Bounds=new WorldBounds(25,25,1,1),PairId=1});
hazardMap.Objects.Add(new WorldObjectDefinition{Id=6,Kind=WorldObjectKind.Door,Bounds=new WorldBounds(30,30,1,2)});
var hazardPorts=new FakePorts();var hazards=new HazardService(hazardMap,new HazardConfig(),hazardPorts,hazardPorts,hazardPorts);
var actors=new[]{new WorldActor(0,5,5),new WorldActor(1,10,10),new WorldActor(2,15,15),new WorldActor(3,20,20)};
hazards.Tick(0,actors);Check(hazardPorts.DamageCount==0,"lava waits half-second");Check(hazardPorts.SlowCount==1&&hazardPorts.PoisonCount==1,"slow and poison requests");Check(hazardPorts.MoveCount==1,"portal moves once");
hazards.Tick(15,actors);Check(hazardPorts.DamageCount==1,"lava damage 10 every 15 ticks");
hazards.Tick(15,actors);Check(hazardPorts.DamageCount==1,"same tick no duplicate lava");
hazards.Tick(30,actors);Check(hazardPorts.DamageCount==2,"second lava pulse");
hazards.Tick(60,Array.Empty<WorldActor>());Check(hazardPorts.Removed==1,"swamp leaves remove source");
hazards.Tick(90,new[]{new WorldActor(3,25,25)});Check(hazardPorts.MoveCount==2,"portal cooldown expires");
hazards.Tick(600,new[]{new WorldActor(0,30,30)});Check(hazards.DoorsClosed&&hazardPorts.MoveCount==3,"door closes and displaces occupant");
hazards.Tick(1200,Array.Empty<WorldActor>());Check(!hazards.DoorsClosed&&hazards.CollisionRevision==2,"door all phases use server clock");
var registry=new PacketRegistry();WorldActionRequestPacket received=default;registry.Subscribe<WorldActionRequestPacket>(p=>received=p);
var request=new WorldActionRequestPacket{MatchId=Guid.NewGuid(),RequestId=1,Action=WorldActionKind.Pickup,TargetId=400,Slot=0};var writer=new NetDataWriter();registry.WriteFrame(writer,ref request);Check(registry.TryReadFrame(new NetDataReader(writer.CopyData()))&&received.TargetId==400,"world request roundtrip ushort ID");
var invalid=writer.CopyData();invalid[9]=99;Check(!registry.TryReadFrame(new NetDataReader(invalid)),"wrong world version rejects");
var priorWorld=Environment.GetEnvironmentVariable("WORLD_ENABLED");
try
{
    Environment.SetEnvironmentVariable("WORLD_ENABLED","1");
    var canonical=MapData.Load(GameDataLoader.ReadGameDataFile("map_demo.json"));
    var schedule=ZoneSchedule.Load(GameDataLoader.ReadGameDataFile("zone_schedule.json"));
    var profile=new MatchProfile{MinPlayers=2,LobbyDurationSeconds=.01,LoadingTimeoutSeconds=1,ZoneTimeScale=1000,TargetMatchDurationSeconds=10000};
    MatchRuntime Start(string room,byte hp=100)
    {
        var p=hp==100?profile:new MatchProfile{MinPlayers=2,LobbyDurationSeconds=.01,LoadingTimeoutSeconds=1,PlayerHp=hp,ZoneTimeScale=1000,TargetMatchDurationSeconds=10000};
        var m=new MatchRuntime(room,canonical,schedule,p);
        m.TryRegisterPlayer("p0","Hero0",out _);m.TryRegisterPlayer("p1","Hero1",out _);m.SetPlayerReady("p0");m.SetPlayerReady("p1");
        for(var i=0;i<4;i++)m.Tick(1.0/30);
        m.SetPlayerLoaded("p0",m.MatchId);m.SetPlayerLoaded("p1",m.MatchId);m.Tick(1.0/30);
        return m;
    }
    var match=Start("WORLD_A");var other=Start("WORLD_B");
    Check(match.World!=null&&other.World!=null,"world initializes from server profile at match start");
    var active=match.World!;var second=other.World!;
    Check(canonical.Obstacles.Count==4,"world collision never mutates shared canonical map");
    Check(match.Entities.All.All(e=>!match.Zone!.IsOutside(e.X,e.Y)),"world spawns start inside actual zone schedule");
    match.Entities.TryGet(0,out var actor);var px=actor!.X;var py=actor.Y;
    Check(match.EnsureWorld(match.MatchId)&&actor.X==px&&actor.Y==py,"resync does not teleport or mutate world");
    Check(!match.EnsureWorld(Guid.NewGuid()),"resync rejects old match identity");
    active.Floor.TrySpawn(new ItemStack{ItemId="TREE_BRANCH",Count=1},px,py,out var floor);
    var pickup=new WorldActionRequestPacket{MatchId=match.MatchId,RequestId=1,Action=WorldActionKind.Pickup,TargetId=floor};
    Check(match.ProcessWorldRequest("p0",pickup),"authoritative scoped pickup");
    Check(!match.ProcessWorldRequest("p0",pickup),"pickup retry does not duplicate item");
    Check(active.Inventory.GetSnapshot(0).Weapons[0].ItemId=="TREE_BRANCH","inventory stores received item");
    var victimChest=active.Layout.Objects.First(o=>o.Kind==WorldObjectKind.Chest);
    Check(active.Destructibles.DamageConfirmed(victimChest.Id,30,0,1)&&active.Floor.Items.Count>=3,"confirmed chest break creates floor items");
    Check(!second.Destructibles.IsBroken(victimChest.Id)&&second.Floor.Items.Count==0,"room isolation of broken state and loot");
    active.Floor.TrySpawn(new ItemStack{ItemId="ELIXIR_OF_LIFE",Count=1},px,py,out var life);
    pickup.RequestId=2;pickup.TargetId=life;Check(match.ProcessWorldRequest("p0",pickup),"passive item picked into dược slot");
    match.Health.ApplyDamage(0,255,1);Check(actor.Hp==50&&actor.IsAlive,"lethal hit consumes passive and restores 50 HP");
    match.Health.ApplyDamage(0,10,1);Check(actor.Hp==50,"rescue invulnerability blocks repeat same-tick hit");
    Check(match.ProcessLeave("p0",match.MatchId)&&!actor.IsAlive,"explicit leave bypasses invulnerability and passive");
    Check(active.Floor.Items.Any(f=>f.Stack.ItemId=="TREE_BRANCH"),"leave elimination death-drops inventory immediately");
    var low=Start("HP60",60);low.Health.ApplyDamage(0,20,1);((HealthService)low.Health).Heal(0,255);Check(low.Health.GetHp(0)==60,"healing respects profile max60");
    var high=Start("HP150",150);high.Health.ApplyDamage(0,20,1);((HealthService)high.Health).Heal(0,255);Check(high.Health.GetHp(0)==150,"healing respects profile max150");
    var secondActor=other.Entities.All.First();var newItem=new ItemStack{ItemId="GOD_LEAF",Count=1};second.Floor.TrySpawn(newItem,secondActor.X,secondActor.Y,out var healId);
    var serial=second.SnapshotObjects();Check(serial.Any(o=>o.Id==healId&&o.Kind==100&&o.ItemId=="GOD_LEAF"),"world snapshot includes floor catalog identity");
    var pageObjects=serial.Take(WorldWire.PageSize).ToArray();var page=new WorldObjectStatePacket{MatchId=other.MatchId,Revision=second.Revision,PageIndex=0,PageCount=(ushort)((serial.Count+23)/24),TotalObjects=(ushort)serial.Count,Objects=pageObjects};
    var pageReader=new PacketRegistry();WorldObjectStatePacket pageReceived=default;pageReader.Subscribe<WorldObjectStatePacket>(p=>pageReceived=p);writer.Reset();pageReader.WriteFrame(writer,ref page);Check(writer.Length<=4096&&pageReader.TryReadFrame(new NetDataReader(writer.CopyData()))&&pageReceived.Objects.Length==24,"world page is bounded and roundtrips");
    var s=second.SnapshotInventory(0);var inventoryReader=new PacketRegistry();inventoryReader.Subscribe<InventoryStatePacket>(_=>{});writer.Reset();inventoryReader.WriteFrame(writer,ref s);Check(inventoryReader.TryReadFrame(new NetDataReader(writer.CopyData())),"inventory snapshot roundtrip");
}
finally{Environment.SetEnvironmentVariable("WORLD_ENABLED",priorWorld);}
Console.WriteLine($"WorldChecks: {checks} passed, 0 failed");

sealed class FakePorts:IDestructibleLootSink,IEnvironmentDamageSink,IEnvironmentEffectSink,IEnvironmentMovementSink
{
    public int LootCount,DamageCount,SlowCount,PoisonCount,MoveCount,Removed;
    public void SpawnLoot(WorldObjectKind source,ushort id,WorldPoint p,uint seed){LootCount++;}
    public void RequestDamage(byte actor,ushort source,uint tick,int amount,bool bypass){if(amount!=10||!bypass)throw new Exception("Invalid lava damage");DamageCount++;}
    public void ApplyEffect(byte actor,ushort source,string effect,uint tick,float seconds,float magnitude){if(effect=="Slow")SlowCount++;if(effect=="Poison")PoisonCount++;}
    public void RemoveEffect(byte actor,ushort source,string effect,uint tick){Removed++;}
    public bool TryMove(byte actor,WorldPoint destination,ushort source,uint tick){MoveCount++;return true;}
}
