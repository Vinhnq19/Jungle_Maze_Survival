#nullable disable
using System;
using System.Collections.Generic;
namespace JungleMaze.World
{
    public interface IDestructibleLootSink {void SpawnLoot(WorldObjectKind source,ushort worldId,WorldPoint position,uint seed);}
    public interface ITransactionalDestructibleLootSink : IDestructibleLootSink {bool CanSpawnLoot(WorldObjectKind source,ushort worldId,uint seed);}
    public sealed class DestructibleConfig {public int ChestHp=30,PotHp=20,SupplyHp=150;public void Validate(){if(ChestHp<=0||PotHp<=0||SupplyHp<=0||ChestHp>ushort.MaxValue||PotHp>ushort.MaxValue||SupplyHp>ushort.MaxValue)throw new ArgumentException("Invalid destructible health.");}}
    public sealed class DestructibleState {public ushort Id;public int Hp;public bool Broken;public uint Revision;}
    /// <summary>Only accepts already confirmed damage. A monotonic source action sequence per target
    /// prevents retries without unbounded request caches. World spawning/IDs remain owned by runtime.</summary>
    public sealed class DestructibleService
    {
        private readonly Dictionary<ushort,WorldObjectDefinition> _objects=new Dictionary<ushort,WorldObjectDefinition>();
        private readonly Dictionary<ushort,DestructibleState> _states=new Dictionary<ushort,DestructibleState>();
        private readonly Dictionary<ulong,uint> _lastAction=new Dictionary<ulong,uint>();
        private readonly IDestructibleLootSink _loot;private readonly uint _seed;
        public event Action<DestructibleState> Changed;
        public DestructibleService(MapLayout layout,DestructibleConfig config,IDestructibleLootSink loot){if(layout==null||loot==null)throw new ArgumentNullException();config.Validate();_loot=loot;_seed=layout.Seed;foreach(var o in layout.Objects)if(o.IsDestructible){_objects.Add(o.Id,o);_states.Add(o.Id,new DestructibleState{Id=o.Id,Hp=o.Kind==WorldObjectKind.Chest?config.ChestHp:o.Kind==WorldObjectKind.Pot?config.PotHp:config.SupplyHp});}}
        public IEnumerable<DestructibleState> States {get{foreach(var s in _states.Values)yield return Copy(s);}}
        public bool IsBroken(ushort id){return _states.TryGetValue(id,out var state)&&state.Broken;}
        public bool TryGet(ushort id,out DestructibleState state){if(_states.TryGetValue(id,out var found)){state=Copy(found);return true;}state=null;return false;}
        private static DestructibleState Copy(DestructibleState state){return new DestructibleState{Id=state.Id,Hp=state.Hp,Broken=state.Broken,Revision=state.Revision};}
        public bool DamageConfirmed(ushort id,int damage,byte source,uint actionSequence)
        {
            if(damage<=0||actionSequence==0||!_states.TryGetValue(id,out var state)||state.Broken)return false;
            var key=((ulong)id<<8)|source;if(_lastAction.TryGetValue(key,out var last)&&unchecked((int)(actionSequence-last))<=0)return false;
            if(damage>=state.Hp&&_loot is ITransactionalDestructibleLootSink transactional){var o=_objects[id];if(!transactional.CanSpawnLoot(o.Kind,id,_seed^((uint)id*2654435761u)))return false;}
            _lastAction[key]=actionSequence;
            state.Hp=Math.Max(0,state.Hp-damage);state.Broken=state.Hp==0;state.Revision++;
            if(state.Broken){var o=_objects[id];_loot.SpawnLoot(o.Kind,id,o.Bounds.Center,_seed^((uint)id*2654435761u));}
            Changed?.Invoke(Copy(state));return true;
        }
    }
}
