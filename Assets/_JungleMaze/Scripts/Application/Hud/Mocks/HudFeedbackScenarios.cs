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
        private static readonly string[] PickupIds={"GOD_LEAF","SPIRIT_TEAR","MERMAID_TAIL","UNICORN_BLOOD","ADRENALINE","CROSSBOW","SHOTGUN","RUBBER_GUN"};
        private static readonly EffectKind[] BurstEffects={EffectKind.Poison,EffectKind.Stun,EffectKind.Slow,EffectKind.Invisible};
        public HudFeedbackScenarios(MockHudState hud,MockInventoryState inventory,MockEffectState effects,IInventoryState inventoryRead)
        {_hud=hud;_inventory=inventory;_effects=effects;_inventoryRead=inventoryRead;}
        public bool Running=>_mode!=0;
        public void Burst(){Stop();_hud.Pose(10,10,0);_inventory.SetSlot(4,"GOD_LEAF",6);_inventory.SetSlot(5,"SPIRIT_TEAR",5);_mode=1;}
        public void Skirmish(){Stop();_hud.Pose(10,10,0);_mode=2;_inventory.SetSlot(4,"COMPASS",1);_inventory.BeginUse(4);_effects.Apply(EffectKind.Stun,5,5);_hud.Bomb(50,1.5f,-10,10);for(var i=0;i<4;i++)_hud.Pickup(PickupIds[i],1);}
        public void Stop(){_mode=0;_time=0;_next=0;_step=0;}
        public void Tick(float dt)
        {
            if(_inventoryRead.Use.Phase==ItemUsePhase.Using&&_inventoryRead.Use.RemainingSeconds<=0)_inventory.CompleteUse();
            if(_mode==0||!(dt>0))return;_time+=dt;
            if(_mode==1){while(_step<30&&_time+.00001f>=_step/30f){BurstStep(_step++);}if(_step==30)_mode=0;}
            else if(_time>=_next){_next+=.15f;if(_step<6){_hud.Hit(HitTargetKind.Hero);_hud.Damage((float)(_step++*60));}if(_time>=5.5f)Stop();}
        }
        private void BurstStep(int step)
        {
            // AC-FB42: exact event counts over 30 source ticks, with two offscreen sources.
            if(step<20)_hud.Pickup(PickupIds[step%PickupIds.Length],1);
            if(step<10)_effects.Apply(BurstEffects[step%BurstEffects.Length],1,1);
            if(step<15)_hud.Hit(HitTargetKind.Hero);
            if(step<8)_hud.Damage((float)((step%6)*60));
            if(step<4)_hud.Bomb(step+1,1.5f,step==0?-10:step==1?30:step==2?9:11,10);
            if(step==4)_hud.Trap(5,10,11);
            if(step==0)_inventory.BeginUse(4);
            if(step==15)_inventory.BeginUse(5);
        }
    }
}
#endif
