using JungleMaze.World;
using JungleMaze.World.Inventory;
using JungleBackend.Protocol.Gameplay;
namespace GameServer.Simulation;

/// <summary>Per-match world adapter. Inventory owns items; this runtime owns all world instance IDs.
/// Existing player NetIds are never reused for world objects. No Unity types, database or HTTP calls.</summary>
public sealed class MatchWorldRuntime : ITransactionalDestructibleLootSink,IEnvironmentDamageSink,IEnvironmentEffectSink,IEnvironmentMovementSink,IItemGameplayPort
{
    private readonly IEntityRegistry _entities;
    private readonly HealthService _health;
    private readonly Dictionary<(byte Actor,ushort Source,string Effect),(double End,float Magnitude)> _effects=new();
    private readonly Dictionary<byte,double> _invulnerable=new();
    private readonly Dictionary<byte,double> _poisonDue=new();
    private readonly HashSet<byte> _registered=new();
    private readonly LootTable _loot;
    private ushort _nextId;
    private uint _tick;
    private readonly float _heroRadius;
    public ItemResult LastResult { get; private set; }
    private double _now;
    public Guid MatchId {get;}
    public MapLayout Layout {get;}
    public InventoryService Inventory {get;}
    public MemoryItemWorld Floor {get;}
    public DestructibleService Destructibles {get;}
    public HazardService Hazards {get;}
    public uint Revision {get;private set;}=1;
    public MatchWorldRuntime(Guid matchId,WorldSettings settings,IEntityRegistry entities,HealthService health,ItemCatalog? catalog=null,LootProfile? lootProfile=null)
    {
        MatchId=matchId;_entities=entities;_health=health;_heroRadius=settings.Map.HeroRadius;Layout=MapLayoutGenerator.Generate(settings.Map,settings.Seed);
        _nextId=(ushort)(Layout.Objects.Count+1);
        Floor=new MemoryItemWorld(AllocateId);
        catalog??=ItemCatalog.CreateBaseline();_loot=lootProfile==null?new LootTable(catalog):new LootTable(catalog,lootProfile);
        Inventory=new InventoryService(matchId,catalog,this,Floor);
        Inventory.Changed+=_=>Revision++;
        Destructibles=new DestructibleService(Layout,settings.Destructibles,this);
        Destructibles.Changed+=_=>Revision++;
        Hazards=new HazardService(Layout,settings.Hazards,this,this,this,Destructibles.IsBroken);
        Hazards.DoorChanged+=(_,_)=>Revision++;
        _health.DamageInterceptor=TryPreventDamage;
    }
    private ushort AllocateId(){if(_nextId==0||Floor.Items.Count>=2048)return 0;return _nextId++;}
    public void SyncActors()
    {
        foreach(var e in _entities.All)
        {
            if(_registered.Add(e.NetId))Inventory.Register(e.NetId);
            var controls=ItemControlFlags.None;
            foreach(var effect in _effects)if(effect.Key.Actor==e.NetId&&effect.Value.End>_now&&effect.Key.Effect=="Stun")controls|=ItemControlFlags.Stun;
            Inventory.SetActorState(e.NetId,e.X,e.Y,e.IsAlive,controls);
        }
    }
    public float SpeedMultiplier(byte actor)
    {
        var value=1f;
        // Overlapping swamp sources do not multiply the same Slow effect; leaving one keeps the other.
        var slow=1f;foreach(var e in _effects)if(e.Key.Actor==actor&&e.Value.End>_now){if(e.Key.Effect=="Slow")slow=Math.Min(slow,e.Value.Magnitude);else if(e.Key.Effect=="Speed")value*=e.Value.Magnitude;else if(e.Key.Effect=="Stun")return 0;}
        return value*slow;
    }
    public bool Using(byte actor){return _registered.Contains(actor)&&Inventory.IsUsing(actor);}
    public void Tick(uint tick,double now)
    {
        _tick=tick;_now=now;SyncActors();
        var actors=_entities.All.Select(e=>new WorldActor(e.NetId,e.X,e.Y,e.IsAlive)).ToArray();
        Hazards.Tick(tick,actors);
        var expired=_effects.Where(e=>e.Value.End<=now).Select(e=>e.Key).ToArray();foreach(var k in expired)_effects.Remove(k);
        foreach(var e in _entities.All)
        {
            if(!e.IsAlive){Inventory.DeathDrop(e.NetId,tick);continue;}
            var poison=_effects.Where(p=>p.Key.Actor==e.NetId&&p.Key.Effect=="Poison"&&p.Value.End>now).Select(p=>p.Value.Magnitude).DefaultIfEmpty(0).Max();
            if(poison>0&&(!_poisonDue.TryGetValue(e.NetId,out var due)||now>=due)){_poisonDue[e.NetId]=now+1;_health.ApplyDamage(e.NetId,(byte)Math.Clamp((int)poison,0,255),255);}
            else if(poison==0)_poisonDue.Remove(e.NetId);
        }
        Inventory.Tick(now);
    }
    public ItemResult Request(byte actor,in WorldActionRequestPacket request)
    {
        SyncActors();ItemResult result;
        switch(request.Action)
        {
            case WorldActionKind.Pickup:result=Inventory.Pickup(MatchId,actor,request.RequestId,request.TargetId);break;
            case WorldActionKind.Drop:result=Inventory.Drop(MatchId,actor,request.RequestId,request.Slot);break;
            case WorldActionKind.Select:result=Inventory.Select(MatchId,actor,request.RequestId,request.Slot==255?-1:request.Slot);break;
            case WorldActionKind.Use:result=Inventory.BeginUse(MatchId,actor,request.RequestId,request.Slot,_now);break;
            case WorldActionKind.CancelUse:Inventory.CancelForAction(actor,ItemCancelAction.Move);result=ItemResult.Ok;break;
            default:return ItemResult.Invalid;
        }
        LastResult=result;
        if(result==ItemResult.Ok)Revision++;
        return result;
    }
    public void SpawnLoot(WorldObjectKind source,ushort worldId,WorldPoint position,uint seed)
    {
        var kind=source==WorldObjectKind.Chest?LootSource.Chest:source==WorldObjectKind.Pot?LootSource.Pot:LootSource.Supply;
        var items=_loot.Roll(kind,seed);
        // Preflight/rollback makes a full floor registry an explicit rejection, never partial loot.
        if(Floor.Items.Count+items.Length>2048||_nextId+items.Length>ushort.MaxValue)
            return;
        var spawned=new List<ushort>();
        for(var i=0;i<items.Length;i++)
        {
            // Broken object no longer blocks its center; bounded deterministic spread remains close enough to pick up.
            var p=new WorldPoint(position.X+(i%3-1)*.25f,position.Y+(i/3)*.25f);
            if(!Layout.IsWalkable(p,.1f,Destructibles.IsBroken,Hazards.IsDoorClosed))p=position;
            if(!Floor.TrySpawn(items[i],p.X,p.Y,out var id)){foreach(var old in spawned)Floor.Remove(old);return;}
            spawned.Add(id);
        }
        Revision++;
    }
    public bool CanSpawnLoot(WorldObjectKind source,ushort worldId,uint seed)
    {
        var kind=source==WorldObjectKind.Chest?LootSource.Chest:source==WorldObjectKind.Pot?LootSource.Pot:LootSource.Supply;
        var count=_loot.Roll(kind,seed).Length;
        return Floor.Items.Count+count<=2048&&_nextId+count<=ushort.MaxValue&&Layout.Objects.Count+Floor.Items.Count+count<=WorldWire.MaxObjects;
    }
    public void RequestDamage(byte actor,ushort source,uint tick,int amount,bool bypassArmor){_health.ApplyDamage(actor,(byte)Math.Clamp(amount,0,255),255);}
    public void ApplyEffect(byte actor,ushort source,string effect,uint tick,float seconds,float magnitude){_effects[(actor,source,effect)]=(float.IsPositiveInfinity(seconds)?double.PositiveInfinity:_now+seconds,magnitude);}
    public void RemoveEffect(byte actor,ushort source,string effect,uint tick){_effects.Remove((actor,source,effect));}
    public bool TryMove(byte actor,WorldPoint destination,ushort source,uint tick)
    {
        if(!_entities.TryGet(actor,out var e)||e==null||!e.IsAlive||!Layout.IsWalkable(destination,_heroRadius,Destructibles.IsBroken,Hazards.IsDoorClosed))return false;
        e.X=destination.X;e.Y=destination.Y;Revision++;return true;
    }
    public void Heal(byte actor,float amount){_health.Heal(actor,(byte)Math.Clamp((int)amount,0,255));}
    public void Rescue(byte actor,float hp,double invulnerableSeconds){_health.Rescue(actor,(byte)Math.Clamp((int)hp,1,100));_invulnerable[actor]=_now+invulnerableSeconds;}
    public void ApplyEffect(byte actor,ItemEffect effect,double seconds)
    {
        if(effect==ItemEffect.Cleanse){foreach(var k in _effects.Keys.Where(k=>k.Actor==actor&&(k.Effect=="Stun"||k.Effect=="Root"||k.Effect=="Reverse")).ToArray())_effects.Remove(k);_effects[(actor,0,"Immune")]=(_now+seconds,1);}
        else if(effect==ItemEffect.Speed)_effects[(actor,0,"Speed")]=(_now+seconds,1.3f);
        else if(effect==ItemEffect.Invisible)_effects[(actor,0,"Invisible")]=(_now+seconds,1);
        else if(effect==ItemEffect.Compass&&_entities.TryGet(actor,out var origin)&&origin!=null)foreach(var other in _entities.All)if(other.NetId!=actor&&other.IsAlive&&Math.Abs(other.X-origin.X)<=4.5f&&Math.Abs(other.Y-origin.Y)<=4.5f&&!_effects.Any(e=>e.Key.Actor==other.NetId&&e.Key.Effect=="Immune"&&e.Value.End>_now))_effects[(other.NetId,0,"Stun")]=(_now+Math.Min(seconds,1.5),0);
    }
    private bool TryPreventDamage(byte actor,byte amount)
    {
        if(_invulnerable.TryGetValue(actor,out var end)&&end>_now)return true;
        return _entities.TryGet(actor,out var e)&&e!=null&&amount>=e.Hp&&_registered.Contains(actor)&&Inventory.TryConsumeLifePassive(actor,_tick==0?1:_tick);
    }
    public List<WorldObjectWire> SnapshotObjects()
    {
        var result=new List<WorldObjectWire>();
        foreach(var o in Layout.Objects){Destructibles.TryGet(o.Id,out var d);var center=o.Bounds.Center;result.Add(new WorldObjectWire{Id=o.Id,Kind=(byte)o.Kind,Flags=(byte)((d?.Broken==true?1:0)|(o.Kind==WorldObjectKind.Door&&Hazards.DoorsClosed?2:0)),PairId=o.PairId,Hp=(ushort)(d?.Hp??0),X=center.X,Y=center.Y,Width=o.Bounds.MaxX-o.Bounds.MinX,Height=o.Bounds.MaxY-o.Bounds.MinY});}
        foreach(var floor in Floor.Items)result.Add(new WorldObjectWire{Id=floor.Id,Kind=100,X=floor.X,Y=floor.Y,Width=.4f,Height=.4f,ItemId=floor.Stack.ItemId,Count=(ushort)floor.Stack.Count});
        return result;
    }
    public InventoryStatePacket SnapshotInventory(byte actor)
    {
        var s=Inventory.GetSnapshot(actor);var slots=new InventorySlotWire[8];for(var i=0;i<4;i++){slots[i]=Slot(s.Weapons[i]);slots[i+4]=Slot(s.Consumables[i]);}
        return new InventoryStatePacket{MatchId=MatchId,Actor=actor,Revision=s.Revision,ActiveWeapon=(byte)(s.ActiveWeapon<0?255:s.ActiveWeapon),IsUsing=s.IsUsing,UseSlot=(byte)(s.UseSlot<0?255:s.UseSlot),UseItemId=s.UseItemId,UseDuration=(float)s.UseDuration,UseRemaining=(float)Math.Max(0,s.UseEnd-_now),Slots=slots,Helmet=Slot(s.Helmet),Armor=Slot(s.Armor)};
    }
    private static InventorySlotWire Slot(ItemStack s){return new InventorySlotWire{ItemId=s.ItemId,Count=(ushort)Math.Max(0,s.Count),Ammo=(ushort)Math.Max(0,s.Ammo),Durability=s.Durability};}
}
