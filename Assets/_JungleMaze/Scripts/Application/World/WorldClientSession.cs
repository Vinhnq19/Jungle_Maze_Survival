using System;
using System.Collections.Generic;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Match;
using JungleMaze.App.Player;
using JungleMaze.App.Ports;
using JungleMaze.App.Hud.State;
using JungleMaze.Domain.Hud;
using JungleMaze.Core.DI;
using JungleMaze.Core.Ticking;
using JungleMaze.World;
using JungleMaze.World.Inventory;

namespace JungleMaze.App.World
{
    /// <summary>Opt-in world stream. State pages are committed atomically; no client item simulation online.</summary>
    public sealed class WorldClientSession : IInitializable, IDisposable, ITickable
    {
        private readonly MatchSession _match;
        private readonly IPacketSender _sender;
        private readonly IPacketReceiver _receiver;
        private readonly IClientRuntimeData _runtime;
        private readonly InventoryHudBinding _hud;
        private readonly MapLayoutConfig _config;
        private readonly MapLayout _authored;
        private readonly Func<bool> _enabled;
        private readonly IEffectStateWriter _effects;
        private uint _effectsTick;
        private bool _hasEffects;
        private float _movementMultiplier=1f;
        private uint _portalSequence;
        private readonly Dictionary<ushort, WorldObjectStatePacket> _pages = new Dictionary<ushort, WorldObjectStatePacket>();
        private Guid _requested;
        private uint _request, _stagingRevision, _revision, _inventoryRevision;
        private ushort _pageCount, _total;
        private bool _hasRevision, _hasInventory;
        private float _retry;
        private string _collisionKey;
        public MapLayout Layout { get; private set; }
        public WorldObjectWire[] Objects { get; private set; } = Array.Empty<WorldObjectWire>();
        public InventorySnapshot Inventory { get; private set; }
        public uint EquipmentGeneration { get; private set; }
        public string Error { get; private set; }
        public bool Enabled { get; set; } = true;
        /// <summary>Only a verified layout, complete state and own inventory unlock generated-world gameplay.</summary>
        public bool IsReady => Enabled && (_enabled == null || _enabled()) && _requested == _match.MatchId &&
            Layout != null && _hasRevision && _hasInventory && Error == null;
        public bool Required => Enabled && (_enabled == null || _enabled());
        public event Action Changed;
        /// <summary>A recent, deduplicated portal traversal confirmed by the server.</summary>
        public event Action<ushort,uint> Teleported;

        public WorldClientSession(MatchSession match, IPacketSender sender, IPacketReceiver receiver,
            IClientRuntimeData runtime, InventoryHudBinding hud, MapLayoutConfig config, Func<bool> enabled = null, IEffectStateWriter effects = null, MapLayout authored = null)
        { _match=match; _sender=sender; _receiver=receiver; _runtime=runtime; _hud=hud; _config=config; _enabled=enabled; _effects=effects; _authored=authored; }
        public void Initialize()
        { _match.GameplayReady = GameplayReady; _receiver.Subscribe<WorldManifestPacket>(Manifest); _receiver.Subscribe<WorldObjectStatePacket>(Page); _receiver.Subscribe<InventoryStatePacket>(Items); }
        public void Dispose()
        { if (_match.GameplayReady == GameplayReady) _match.GameplayReady=null; _receiver.Unsubscribe<WorldManifestPacket>(Manifest); _receiver.Unsubscribe<WorldObjectStatePacket>(Page); _receiver.Unsubscribe<InventoryStatePacket>(Items); }
        private bool GameplayReady() => !Required || IsReady;
        public void Tick(float dt)
        {
            if (!Required || !_match.Connected || _match.Phase != MatchPhase.InGame || _match.OwnNetId == 255)
            { if (_requested != Guid.Empty) Reset(); return; }
            if (_requested != _match.MatchId) { Reset(); _requested = _match.MatchId; _retry=0; }
            _retry -= dt;
            if (!IsReady && _retry <= 0) { Send(WorldActionKind.Resync); _retry=2; }
        }
        public bool Send(WorldActionKind kind, ushort target=0, byte slot=255)
        {
            if (!Required || !_match.Connected || _match.Phase!=MatchPhase.InGame || _requested!=_match.MatchId ||
                (kind != WorldActionKind.Resync && !IsReady)) return false;
            if (++_request==0) _request=1;
            var packet=new WorldActionRequestPacket{MatchId=_requested,RequestId=_request,Action=kind,TargetId=target,Slot=slot};
            return _sender.Send(ref packet,PacketDelivery.ReliableOrdered);
        }
        private void Reset()
        {
            if(Layout!=null&&_runtime.PlayerRadius>0&&_runtime.PlayerSpeed>0)
                _match.Prediction.SetWorld(new MovementSimulation(_runtime.Bounds,_runtime.Covers,_runtime.PlayerRadius,_runtime.PlayerSpeed));
            _requested=Guid.Empty;Layout=null;Objects=Array.Empty<WorldObjectWire>();Inventory=null;EquipmentGeneration=0;_pages.Clear();_hasRevision=_hasInventory=_hasEffects=false;_request=_portalSequence=0;_collisionKey=null;Error=null;_movementMultiplier=1;_match.Prediction.MovementMultiplier=1;ClearEffects();_hud.Reset();Changed?.Invoke();
        }
        private void Manifest(WorldManifestPacket p)
        {
            if (p.MatchId!=_requested || p.MatchId==Guid.Empty) return;
            try
            {
                if(_config==null||(_authored!=null?_authored.Version:_config.Version)!=p.Version||(_authored!=null?_authored.Size:_config.Size)!=p.Size){Reject("World config version/size differs from server.");return;}
                if(_authored!=null&&p.Seed!=_authored.Seed){Reject("Authored map seed differs from server.");return;}
                var layout=_authored??MapLayoutGenerator.Generate(_config,p.Seed);
                if (!string.Equals(layout.Hash,p.Hash,StringComparison.OrdinalIgnoreCase))
                { Reject("World layout/config differs from server. Update the client data."); return; }
                if (Layout != null && string.Equals(Layout.Hash, layout.Hash, StringComparison.OrdinalIgnoreCase)) return;
                Layout=layout;Error=null;Changed?.Invoke();
            }
            catch(Exception e){Reject("World manifest rejected: "+e.Message);}
        }
        private void Reject(string error)
        { Error=error; Layout=null; Objects=Array.Empty<WorldObjectWire>(); _pages.Clear(); _hasRevision=false; Changed?.Invoke(); }
        private void Page(WorldObjectStatePacket p)
        {
            if (p.MatchId!=_requested || Layout==null || p.Objects==null || p.TotalObjects>WorldWire.MaxObjects ||
                p.PageCount==0 || p.PageCount>(WorldWire.MaxObjects+23)/24 || p.PageIndex>=p.PageCount || p.Objects.Length>24) return;
            if (_hasRevision && unchecked((int)(p.Revision-_revision))<=0) return;
            if (_pages.Count!=0 && unchecked((int)(p.Revision-_stagingRevision))<0) return;
            if (_pages.Count==0 || p.Revision!=_stagingRevision)
            { _pages.Clear();_stagingRevision=p.Revision;_pageCount=p.PageCount;_total=p.TotalObjects; }
            if (_pageCount!=p.PageCount || _total!=p.TotalObjects || p.PageCount!=Math.Max(1,(p.TotalObjects+23)/24) ||
                p.Objects.Length!=(p.PageIndex+1<p.PageCount?24:p.TotalObjects-p.PageIndex*24)) return;
            _pages[p.PageIndex]=p;
            if (_pages.Count!=_pageCount) return;
            var all=new List<WorldObjectWire>(_total);var ids=new HashSet<ushort>();
            for(ushort i=0;i<_pageCount;i++)foreach(var o in _pages[i].Objects)
            {
                if(o.Id==0 || !ids.Add(o.Id) || !WorldWire.Finite(o.X) || !WorldWire.Finite(o.Y) || o.X<0 || o.Y<0 ||
                    o.X>Layout.Size || o.Y>Layout.Size || o.Width<=0 || o.Height<=0) { _pages.Clear();return; }
                all.Add(o);
            }
            if(all.Count!=_total){_pages.Clear();return;}
            foreach(var definition in Layout.Objects)
            {
                var found=false;foreach(var state in all)if(state.Id==definition.Id)
                {
                    var b=definition.Bounds;var c=b.Center;
                    if(state.Kind!=(byte)definition.Kind||state.PairId!=definition.PairId||Math.Abs(state.X-c.X)>.001f||Math.Abs(state.Y-c.Y)>.001f||
                        Math.Abs(state.Width-(b.MaxX-b.MinX))>.001f||Math.Abs(state.Height-(b.MaxY-b.MinY))>.001f){_pages.Clear();return;}
                    found=true;break;
                }
                if(!found){_pages.Clear();return;}
            }
            Objects=all.ToArray();_revision=p.Revision;_hasRevision=true;_pages.Clear();
            var covers=new List<RuntimeCover>();var key=new System.Text.StringBuilder();
            foreach(var o in Objects)
            {
                var solid=o.Kind<=4 || ((o.Kind==5||o.Kind==6||o.Kind==13)&&(o.Flags&1)==0) || (o.Kind==12&&(o.Flags&2)!=0);
                if(!solid)continue;
                covers.Add(new RuntimeCover{Name="World "+o.Id,MinX=o.X-o.Width/2,MaxX=o.X+o.Width/2,MinY=o.Y-o.Height/2,MaxY=o.Y+o.Height/2});
                key.Append(o.Id).Append(':').Append(o.Flags).Append(';');
            }
            var newKey=key.ToString();
            if(_collisionKey!=newKey)
            {
                _collisionKey=newKey;
                _match.Prediction.SetWorld(new MovementSimulation(new RuntimeMapBounds{MinX=0,MinY=0,MaxX=Layout.Size,MaxY=Layout.Size},covers,_runtime.PlayerRadius,_runtime.PlayerSpeed));
                _match.Prediction.MovementMultiplier=_movementMultiplier;
            }
            Changed?.Invoke();
        }
        private void Items(InventoryStatePacket p)
        {
            if(p.MatchId!=_requested || p.Actor!=_match.OwnNetId || p.Slots==null || p.Slots.Length!=8 ||
                (_hasInventory&&unchecked((int)(p.Revision-_inventoryRevision))<0))return;
            if (_hasEffects && unchecked((int)(p.Tick-_effectsTick))<0) return;
            _hasEffects=true; _effectsTick=p.Tick; _movementMultiplier=p.MovementMultiplier;
            _match.Prediction.MovementMultiplier=_movementMultiplier;
            ApplyEffect(EffectKind.Slow,p.SlowRemaining);ApplyEffect(EffectKind.Poison,p.PoisonRemaining);ApplyEffect(EffectKind.Stun,p.StunRemaining);
            ApplyEffect(EffectKind.Adrenaline,p.SpeedRemaining);ApplyEffect(EffectKind.Invisible,p.InvisibleRemaining);ApplyEffect(EffectKind.CcImmune,p.ImmuneRemaining);
            var s=new InventorySnapshot{Actor=p.Actor,ActiveWeapon=p.ActiveWeapon==255?-1:p.ActiveWeapon,IsUsing=p.IsUsing,
                UseSlot=p.UseSlot==255?-1:p.UseSlot,UseItemId=p.UseItemId,UseDuration=p.UseDuration,UseEnd=p.Tick/30.0+p.UseRemaining,Revision=p.Revision};
            for(var i=0;i<4;i++){s.Weapons[i]=Stack(p.Slots[i]);s.Consumables[i]=Stack(p.Slots[i+4]);}
            s.Helmet=Stack(p.Helmet);s.Armor=Stack(p.Armor);Inventory=s;_inventoryRevision=p.Revision;_hasInventory=true;
            EquipmentGeneration=p.EquipmentGeneration;
            _hud.Apply(s,_match.ServerTick/30.0);Changed?.Invoke();
            if(p.PortalSequence!=0&&(_portalSequence==0||unchecked((int)(p.PortalSequence-_portalSequence))>0))
            {
                _portalSequence=p.PortalSequence;
                var age=unchecked((int)(p.Tick-p.PortalTick));
                if(age>=0&&age<=30&&p.PortalSourceId!=0&&IsReady)Teleported?.Invoke(p.PortalSourceId,p.PortalSequence);
            }
        }
        private void ApplyEffect(EffectKind kind,float remaining)
        {
            if (_effects==null)return;
            if (remaining<0) {_effects.Apply(kind,0,0);return;}
            var elapsed=Math.Max(0,unchecked((int)(_match.ServerTick-_effectsTick)))/30f;
            var left=Math.Max(0,remaining-elapsed);
            if(left>0)_effects.Apply(kind,remaining,left);else _effects.Remove(kind);
        }
        private void ClearEffects()
        {
            if(_effects==null)return;
            _effects.Remove(EffectKind.Slow);_effects.Remove(EffectKind.Poison);_effects.Remove(EffectKind.Stun);
            _effects.Remove(EffectKind.Adrenaline);_effects.Remove(EffectKind.Invisible);_effects.Remove(EffectKind.CcImmune);
        }
        private static ItemStack Stack(InventorySlotWire s)=>new ItemStack(s.ItemId,s.Count,s.Durability,s.Ammo,s.MagazineKnown?s.Magazine:-1);
    }
}
