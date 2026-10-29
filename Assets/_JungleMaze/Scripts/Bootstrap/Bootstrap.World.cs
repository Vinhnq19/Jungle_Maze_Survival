using JungleMaze.App.World;
using JungleMaze.App.Combat;
using JungleMaze.App.Match;
using JungleMaze.App.Hud.State;
using JungleMaze.App.Ports;
using JungleMaze.Infrastructure.Ticking;
using JungleMaze.Infrastructure.Config;
using UnityEngine;
using JungleMaze.World;
using JungleMaze.World.Inventory;
using JungleMaze.Presentation.World.Phase2;
namespace JungleMaze.Composition
{
    public sealed partial class Bootstrap
    {
        private void ComposeWorld(TickRunner ticks,MatchSession match)
        {
            var binding=new InventoryHudBinding(_registry.Resolve<IInventoryStateWriter>(),_registry.Resolve<IHudStateWriter>(),false);
            _registry.Register(binding);
            WorldConfigData data=null;
            try
            {
                data=WorldConfigData.Load(Application.streamingAssetsPath);_registry.Register(data);
                _registry.Register(data.Map);_registry.Register(data.Destructibles);_registry.Register(data.Hazards);
                var itemData=WorldConfigData.LoadItems(Application.streamingAssetsPath);var catalog=itemData.Build();
                _registry.Register(catalog);_registry.Register(new LootTable(catalog,itemData.Loot));
            }
            catch(System.Exception e){_log.Warn("[World] "+e.Message);}
            var config=_registry.Resolve<IConfigProvider>();
            var world=new WorldClientSession(match,_registry.Resolve<IPacketSender>(),_registry.Resolve<IPacketReceiver>(),_registry.Resolve<IClientRuntimeData>(),binding,data!=null?data.Map:null,()=>config.Current.WorldEnabled,_registry.Resolve<IEffectStateWriter>());
            _registry.Register(world);ticks.Add(world);
            var combat=new OnlineCombatSession(match,world,_registry.Resolve<IBalanceConfigProvider>(),_registry.Resolve<IPacketSender>(),_registry.Resolve<IPacketReceiver>(),_registry.Resolve<IHudStateWriter>());
            _registry.Register(combat);ticks.Add(combat);
            Attach<WorldSceneView>();
        }
    }
}
