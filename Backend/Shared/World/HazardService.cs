#nullable disable
using System;
using System.Collections.Generic;
namespace JungleMaze.World
{
    public sealed class HazardConfig
    {
        public int TickRate=30,LavaDamage=10,PoisonDamage=5;public float LavaIntervalSeconds=.5f,PoisonSeconds=3,PortalCooldownSeconds=3,DoorStateSeconds=20,SwampSpeedMultiplier=.6f,HeroRadius=.4f;
        public void Validate(){if(TickRate<=0||TickRate>1000||LavaDamage<=0||LavaDamage>255||PoisonDamage<=0||PoisonDamage>255||!WorldMath.Finite(LavaIntervalSeconds)||LavaIntervalSeconds<=0||LavaIntervalSeconds>3600||!WorldMath.Finite(PoisonSeconds)||PoisonSeconds<=0||PoisonSeconds>3600||!WorldMath.Finite(PortalCooldownSeconds)||PortalCooldownSeconds<=0||PortalCooldownSeconds>3600||!WorldMath.Finite(DoorStateSeconds)||DoorStateSeconds<=0||DoorStateSeconds>3600||!WorldMath.Finite(SwampSpeedMultiplier)||SwampSpeedMultiplier<=0||SwampSpeedMultiplier>1||!WorldMath.Finite(HeroRadius)||HeroRadius<=0||HeroRadius>2)throw new ArgumentException("Invalid hazard configuration.");}
    }
    /// <summary>Server-clock triggers only: damage/effect ownership stays with their respective ports.</summary>
    public sealed class HazardService
    {
        private readonly MapLayout _map;private readonly HazardConfig _config;private readonly IEnvironmentDamageSink _damage;private readonly IEnvironmentEffectSink _effects;private readonly IEnvironmentMovementSink _movement;private readonly Func<ushort,bool> _broken;
        private readonly Dictionary<ulong,uint> _lavaDue=new Dictionary<ulong,uint>();private readonly HashSet<ulong> _swamps=new HashSet<ulong>();private readonly Dictionary<byte,uint> _portalDue=new Dictionary<byte,uint>();private uint _lastTick;private bool _hasTick;private bool _doorsClosed;
        public uint CollisionRevision {get;private set;} public bool DoorsClosed {get{return _doorsClosed;}}
        public event Action<uint,bool> DoorChanged;
        public uint BlockedDoorClosures {get;private set;}
        public HazardService(MapLayout map,HazardConfig config,IEnvironmentDamageSink damage,IEnvironmentEffectSink effects,IEnvironmentMovementSink movement,Func<ushort,bool> broken=null){if(map==null||damage==null||effects==null||movement==null)throw new ArgumentNullException();config.Validate();_map=map;_config=config;_damage=damage;_effects=effects;_movement=movement;_broken=broken;}
        public bool IsDoorClosed(ushort id){return _doorsClosed;}
        public void Tick(uint tick,IReadOnlyList<WorldActor> actors)
        {
            if(actors==null)throw new ArgumentNullException(nameof(actors));
            if(_hasTick&&unchecked((int)(tick-_lastTick))<=0)return;_hasTick=true;_lastTick=tick;
            var closed=(tick/ToTicks(_config.DoorStateSeconds)%2)==1;var justClosed=closed&&!_doorsClosed;if(closed!=_doorsClosed){_doorsClosed=closed;CollisionRevision++;DoorChanged?.Invoke(CollisionRevision,closed);}
            var currentSwamps=new HashSet<ulong>();var alive=new HashSet<byte>();
            foreach(var actor in actors)
            {
                if(!actor.Alive)continue;alive.Add(actor.Id);var teleported=false;
                foreach(var o in _map.Objects)
                {
                    if(o.Kind==WorldObjectKind.Door?!o.Bounds.IntersectsCircle(actor.Position,_config.HeroRadius):!o.Bounds.Contains(actor.Position))continue;var key=((ulong)o.Id<<8)|actor.Id;
                    switch(o.Kind)
                    {
                        case WorldObjectKind.Swamp:currentSwamps.Add(key);if(!_swamps.Contains(key))_effects.ApplyEffect(actor.Id,o.Id,"Slow",tick,float.PositiveInfinity,_config.SwampSpeedMultiplier);break;
                        case WorldObjectKind.Lava:if(!_lavaDue.TryGetValue(key,out var due)){_lavaDue[key]=tick+ToTicks(_config.LavaIntervalSeconds);}else if(unchecked((int)(tick-due))>=0){_damage.RequestDamage(actor.Id,o.Id,tick,_config.LavaDamage,true);_lavaDue[key]=tick+ToTicks(_config.LavaIntervalSeconds);}break;
                        case WorldObjectKind.Poison:_effects.ApplyEffect(actor.Id,o.Id,"Poison",tick,_config.PoisonSeconds,_config.PoisonDamage);break;
                        case WorldObjectKind.Portal:
                            if(teleported||(_portalDue.TryGetValue(actor.Id,out var ready)&&unchecked((int)(tick-ready))<0))break;
                            foreach(var other in _map.Objects)if(other.Kind==WorldObjectKind.Portal&&other.PairId==o.PairId&&other.Id!=o.Id){var destination=other.Bounds.Center;if(_map.IsWalkable(destination,_config.HeroRadius,_broken,IsDoorClosed)&&_movement.TryMove(actor.Id,destination,o.Id,tick)){_portalDue[actor.Id]=tick+ToTicks(_config.PortalCooldownSeconds);teleported=true;}break;}break;
                        case WorldObjectKind.Door:if(justClosed){WorldPoint destination;if(TryFindFree(actor.Position,out destination))_movement.TryMove(actor.Id,destination,o.Id,tick);else{_doorsClosed=false;CollisionRevision++;BlockedDoorClosures++;DoorChanged?.Invoke(CollisionRevision,false);}}break;
                    }
                }
            }
            foreach(var key in _swamps)if(!currentSwamps.Contains(key))_effects.RemoveEffect((byte)key,(ushort)(key>>8),"Slow",tick);_swamps.Clear();foreach(var k in currentSwamps)_swamps.Add(k);
            var expired=new List<ulong>();foreach(var k in _lavaDue.Keys){var id=(byte)k;var inside=false;foreach(var actor in actors)if(actor.Id==id&&actor.Alive)foreach(var o in _map.Objects)if(o.Id==(ushort)(k>>8)&&o.Bounds.Contains(actor.Position))inside=true;if(!inside)expired.Add(k);}foreach(var k in expired)_lavaDue.Remove(k);
            var retired=new List<byte>();foreach(var id in _portalDue.Keys)if(!alive.Contains(id))retired.Add(id);foreach(var id in retired)_portalDue.Remove(id);
        }
        private uint ToTicks(float seconds){return Math.Max(1u,(uint)Math.Ceiling(seconds*_config.TickRate));}
        private bool TryFindFree(WorldPoint from,out WorldPoint destination)
        {
            // Search the bounded map in deterministic distance order. If there is no free cell, do not move.
            var found=false;var best=float.MaxValue;destination=from;for(var y=0;y<_map.Size;y++)for(var x=0;x<_map.Size;x++){var p=new WorldPoint(x+.5f,y+.5f);var d=p.DistanceSquared(from);if(d<best&&_map.IsWalkable(p,_config.HeroRadius,_broken,IsDoorClosed)){best=d;destination=p;found=true;}}return found;
        }
    }
}
