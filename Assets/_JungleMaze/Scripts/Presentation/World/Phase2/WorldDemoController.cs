using System;
using System.Collections.Generic;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Flow;
using JungleMaze.App.Hud.State;
using JungleMaze.App.World;
using JungleMaze.Core.DI;
using JungleMaze.Domain.Hud;
using JungleMaze.World;
using JungleMaze.World.Inventory;
using UnityEngine;

namespace JungleMaze.Presentation.World.Phase2
{
    /// <summary>Explicit offline prototype scene. Damage input is a confirmed-damage fixture, not WPN-02 combat.</summary>
    public sealed partial class WorldDemoController : MonoBehaviour, IDestructibleLootSink, IEnvironmentDamageSink,
        IEnvironmentEffectSink,IEnvironmentMovementSink,IItemGameplayPort
    {
        [SerializeField] private uint seed=20261028;
        private MapLayout _map;private DestructibleService _destructibles;private HazardService _hazards;
        private ItemCatalog _catalog;private MemoryItemWorld _floor;private InventoryService _items;
        private MapLayoutConfig _mapConfig;private DestructibleConfig _destructibleConfig;private HazardConfig _hazardConfig;private LootTable _lootTable;
        private IHudStateWriter _hud;private IInventoryStateWriter _inventoryHud;private IEffectStateWriter _effectHud;private GameFlow _flow;
        private InventoryHudBinding _binding;private Transform _hero;private Camera _camera;private Sprite _square;private Texture2D _texture;
        private WorldVisualSet _visuals;private readonly Dictionary<ushort,WorldObjectView> _views=new Dictionary<ushort,WorldObjectView>();
        private readonly List<WorldActor> _actors=new List<WorldActor>();private readonly Dictionary<ushort,double> _slow=new Dictionary<ushort,double>();
        private Guid _id;private uint _request,_tick;private ushort _nextId=10000;private double _now,_fraction,_invulnerable,_poisonUntil,_poisonDue,_boostUntil;
        private int _hp=100;private WorldPoint _position;private bool _ready;
        public MapLayout Layout=>_map;public int Hp=>_hp;public InventoryService Items=>_items;public MemoryItemWorld Floor=>_floor;
        public WorldPoint Position=>_position;public bool Ready=>_ready;
        [Inject] public void Construct(IHudStateWriter hud,IInventoryStateWriter inventory,IEffectStateWriter effects,GameFlow flow)
        {_hud=hud;_inventoryHud=inventory;_effectHud=effects;_flow=flow;}
        [Inject] public void Configure(MapLayoutConfig map,DestructibleConfig destructibles,HazardConfig hazards,ItemCatalog catalog,LootTable loot)
        {_mapConfig=map;_destructibleConfig=destructibles;_hazardConfig=hazards;_catalog=catalog;_lootTable=loot;}
        private void Start()
        {
            if(_hud==null||_catalog==null||_mapConfig==null||heroVisual==null||projectilePrefab==null||swingPrefab==null){Debug.LogError("[WorldDemo] Bootstrap/config or Game visual references missing; configure the WorldDemo scene.");enabled=false;return;}
            _flow.ApplyAuthoritativePhase(new MatchPhasePacket{Phase=MatchPhase.InGame});_flow.IsOffline=true;
            _id=Guid.NewGuid();_map=MapLayoutGenerator.Generate(_mapConfig,seed);
            _floor=new MemoryItemWorld(()=>++_nextId);_items=new InventoryService(_id,_catalog,this,_floor);_items.Register(0);
            _destructibles=new DestructibleService(_map,_destructibleConfig,this);
            _hazards=new HazardService(_map,_hazardConfig,this,this,this,_destructibles.IsBroken);
            _binding=new InventoryHudBinding(_inventoryHud,_hud,false);_visuals=Resources.Load<WorldVisualSet>("WorldVisualSet");
            _position=_map.Spawns[0];_items.SetActorState(0,_position.X,_position.Y,true);
            _texture=new Texture2D(1,1);_texture.SetPixel(0,0,Color.white);_texture.Apply();_square=Sprite.Create(_texture,new Rect(0,0,1,1),new Vector2(.5f,.5f),1);
            CreateHeroVisual();
            _camera=Camera.main;_camera.orthographic=true;_camera.orthographicSize=10;
            // Explicit demo fixtures next to the initial spawn; real match starter loadout is server configured.
            ushort id;_floor.TrySpawn(new ItemStack("AXE"),_position.X+.7f,_position.Y,out id);
            _floor.TrySpawn(new ItemStack("GOD_LEAF",3),_position.X,_position.Y+.7f,out id);
            _floor.TrySpawn(new ItemStack("ELIXIR",1),_position.X-.7f,_position.Y,out id);
            _floor.TrySpawn(new ItemStack("SHOTGUN",1,0,9,3),_position.X,_position.Y-.7f,out id);
            InitializeCombat();
            _hp=70;_hud.SetHp(_hp);_hud.SetAliveCount(1,1);_ready=true;Render();
        }
        private void Update()
        {
            if(!_ready)return;
            var move=new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"));if(move.sqrMagnitude>1)move.Normalize();
            if(move.sqrMagnitude>0)_items.CancelForAction(0,ItemCancelAction.Move);
            _fraction+=Time.deltaTime;while(_fraction>=1.0/30){_fraction-=1.0/30;Step(move.x,move.y);}
            for(var i=0;i<4;i++)
            {
                if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i))){var s=_items.GetSnapshot(0);_items.Select(_id,0,++_request,s.ActiveWeapon==i?-1:i);}
                if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha5+i)))_items.BeginUse(_id,0,++_request,i,_now);
            }
            if(Input.GetKeyDown(KeyCode.F))PickupAtCursor();
            if(Input.GetKeyDown(KeyCode.Backspace)){var s=_items.GetSnapshot(0);if(s.ActiveWeapon>=0)_items.Drop(_id,0,++_request,s.ActiveWeapon);}
            if(Input.GetKeyDown(KeyCode.Space))ConfirmDemoHit();
            if(Input.GetKeyDown(KeyCode.H))RequestDamage(0,0,++_tick,30,true);
            UpdateCombat();
            _binding.Apply(_items.GetSnapshot(0),_now);_hud.SetPose(_position.X,_position.Y,0);_hud.SetHp(_hp);
            _hero.position=new Vector3(_position.X,_position.Y,0);_camera.transform.position=new Vector3(_position.X,_position.Y,-10);
            Render();
        }
        public void Step(float x,float y)
        {
            _now+=1.0/30;_tick++;var speed=_slow.Count>0?.6f:1; if(_boostUntil>_now)speed*=1.3f;
            var use=_items.GetSnapshot(0);if(use.IsUsing)speed=0;
            var dx=x*4*speed/30;var dy=y*4*speed/30;
            var next=new WorldPoint(_position.X+dx,_position.Y);if(_map.IsWalkable(next,.4f,_destructibles.IsBroken,_hazards.IsDoorClosed))_position=next;
            next=new WorldPoint(_position.X,_position.Y+dy);if(_map.IsWalkable(next,.4f,_destructibles.IsBroken,_hazards.IsDoorClosed))_position=next;
            _items.SetActorState(0,_position.X,_position.Y,_hp>0);_actors.Clear();_actors.Add(new WorldActor(0,_position.X,_position.Y,_hp>0));_hazards.Tick(_tick,_actors);
            if(_poisonUntil>_now&&_poisonDue<=_now){_poisonDue=_now+1;RequestDamage(0,0,_tick,5,true);}
            _items.Tick(_now);
        }
        private void PickupAtCursor()
        {
            var cursor=_camera.ScreenToWorldPoint(Input.mousePosition);ushort target=0;var best=float.MaxValue;
            foreach(var o in _floor.Items)if(_position.DistanceSquared(new WorldPoint(o.X,o.Y))<=1.44f){var d=((Vector2)cursor-new Vector2(o.X,o.Y)).sqrMagnitude;if(d<best){best=d;target=o.Id;}}
            if(target!=0&&_floor.TryGet(target,out var item)&&_items.Pickup(_id,0,++_request,target)==ItemResult.Ok)_hud.NotifyPickup(item.Stack.ItemId,item.Stack.Count);
        }
        public void ConfirmDemoHit()
        {
            foreach(var o in _map.Objects)if(o.IsDestructible&&!_destructibles.IsBroken(o.Id)&&o.Bounds.Center.DistanceSquared(_position)<5)
            {_items.CancelForAction(0,ItemCancelAction.Attack);_destructibles.DamageConfirmed(o.Id,30,0,++_request);return;}
        }
        public void SpawnLoot(WorldObjectKind source,ushort worldId,WorldPoint position,uint lootSeed)
        {
            var loot=_lootTable.Roll(source==WorldObjectKind.Pot?LootSource.Pot:LootSource.Chest,lootSeed);
            for(var i=0;i<loot.Length;i++){var p=new WorldPoint(position.X+(i%2)*.3f,position.Y+(i/2)*.3f);ushort id;_floor.TrySpawn(loot[i],p.X,p.Y,out id);}
        }
        public void RequestDamage(byte actor,ushort source,uint tick,int amount,bool bypassArmor)
        {
            if(_hp<=0||_now<_invulnerable)return;if(_hp-amount<=0&&_items.TryConsumeLifePassive(actor,tick))return;
            _hp=Math.Max(0,_hp-amount);_hud.NotifyDamageTaken(false,0);if(_hp==0)_items.DeathDrop(actor,tick);
        }
        public void ApplyEffect(byte actor,ushort source,string effect,uint tick,float seconds,float magnitude)
        {
            if(effect=="Slow"){_slow[source]=_now+seconds;_effectHud.Apply(EffectKind.Slow,seconds,seconds);}
            if(effect=="Poison"){_poisonUntil=_now+seconds;_effectHud.Apply(EffectKind.Poison,seconds,seconds);}
        }
        public void RemoveEffect(byte actor,ushort source,string effect,uint tick){if(effect=="Slow"){_slow.Remove(source);if(_slow.Count==0)_effectHud.Remove(EffectKind.Slow);}}
        public bool TryMove(byte actor,WorldPoint destination,ushort source,uint tick){if(!_map.IsWalkable(destination,.4f,_destructibles.IsBroken,_hazards.IsDoorClosed))return false;_position=destination;return true;}
        public void Heal(byte actor,float amount){_hp=Math.Min(100,_hp+(int)amount);}
        public void ApplyEffect(byte actor,ItemEffect effect,double seconds){if(effect==ItemEffect.Speed)_boostUntil=_now+seconds;}
        public void Rescue(byte actor,float hp,double invulnerableSeconds){_hp=(int)hp;_invulnerable=_now+invulnerableSeconds;}
        private void Render()
        {
            var states=new List<WorldObjectWire>();foreach(var o in _map.Objects)
            {DestructibleState d;_destructibles.TryGet(o.Id,out d);var p=o.Bounds.Center;states.Add(new WorldObjectWire{Id=o.Id,Kind=(byte)o.Kind,X=p.X,Y=p.Y,Width=o.Bounds.MaxX-o.Bounds.MinX,Height=o.Bounds.MaxY-o.Bounds.MinY,Flags=(byte)((d!=null&&d.Broken?1:0)|(o.Kind==WorldObjectKind.Door&&_hazards.DoorsClosed?2:0)),Hp=(ushort)(d!=null?d.Hp:0)});}
            foreach(var f in _floor.Items)states.Add(new WorldObjectWire{Id=f.Id,Kind=100,X=f.X,Y=f.Y,Width=.35f,Height=.35f,ItemId=f.Stack.ItemId,Count=(ushort)f.Stack.Count});
            WorldSceneView.ApplyViews(states.ToArray(),transform,_views,_visuals,_square);
            RenderItemIcons();
            UpdateDemoCollision();
        }
        private void OnDestroy(){DisposeCombat();if(_hero!=null)Destroy(_hero.gameObject);if(_square!=null)Destroy(_square);if(_texture!=null)Destroy(_texture);}
    }
}
