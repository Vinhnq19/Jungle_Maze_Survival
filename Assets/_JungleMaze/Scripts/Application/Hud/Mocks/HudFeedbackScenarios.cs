#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using JungleMaze.App.Hud.State;
using JungleMaze.Core.Ticking;
using JungleMaze.Domain.Hud;

namespace JungleMaze.App.Hud.Mocks
{
    /// <summary>Deterministic development-only feedback stress and skirmish scenarios.</summary>
    public sealed class HudFeedbackScenarios:ITickable
    {
        private readonly MockHudState _hud;private readonly MockInventoryState _inventory;private readonly MockEffectState _effects;
        private readonly IInventoryState _inventoryRead;private float _time,_next;private int _mode,_step;
        private static readonly string[] PickupIds={"GOD_LEAF","UNICORN_BLOOD","ENERGY_DRINK","CROSSBOW","SHOTGUN","ARMOR","WOODEN_HELMET","HAND","RUBBER_SLING","PHOENIX_FEATHERS"};
        public HudFeedbackScenarios(MockHudState hud,MockInventoryState inventory,MockEffectState effects,IInventoryState inventoryRead)
        {_hud=hud;_inventory=inventory;_effects=effects;_inventoryRead=inventoryRead;}
        public bool Running=>_mode!=0;
        public void Burst(){Stop();_hud.Pose(10,10,0);_mode=1;}
        public void Skirmish(){Stop();_hud.Pose(10,10,0);_mode=2;_inventory.SetSlot(4,"GOD_LEAF",6);_inventory.BeginUse(4);}
        public void Stop(){_mode=0;_time=0;_next=0;_step=0;}
        public void Tick(float dt)
        {
            if(_inventoryRead.Use.Phase==ItemUsePhase.Using&&_inventoryRead.Use.RemainingSeconds<=0)_inventory.CompleteUse();
            if(_mode==0||!(dt>0))return;_time+=dt;
            if(_mode==1){while(_step<30&&_time+.00001f>=_step/30f){BurstStep(_step++);}if(_step==30)_mode=0;}
            else if(_time>=_next){_next+=.5f;_hud.Hit(HitTargetKind.Hero);_hud.Damage((float)((_step++*45)%360));_hud.Bomb(1,1.5f,7,7);_hud.Trap(2,13,9);_effects.Apply(EffectKind.Poison,3,3);_effects.Apply(EffectKind.Invisible,8,8);_hud.Pickup("GOD_LEAF",1);if(_time>=10)Stop();}
        }
        private void BurstStep(int step)
        {
            // 10 distinct pickups, 12 directional hits and 3 bombs over exactly 30 source ticks.
            if(step<10)_hud.Pickup(PickupIds[step],1);
            if(step<12){_hud.Hit(HitTargetKind.Hero);_hud.Damage((float)(step*30));}
            if(step<3)_hud.Bomb(step+1,1.5f,10+(step==0?-20:20),10+(step-1)*20);
            if(step==0)for(var k=2;k<=8;k++)_effects.Apply((EffectKind)k,.5f,.5f);
        }
    }
}
#endif
