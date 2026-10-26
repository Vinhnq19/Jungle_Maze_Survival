using System;
using System.Collections.Generic;
using JungleMaze.App.Hud.Feedback;
using JungleMaze.App.Hud.Mocks;
using JungleMaze.App.Hud.State;
using JungleMaze.App.Ports;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Hud;
using JungleMaze.Presentation.UI.Hud;
using NUnit.Framework;

namespace JungleMaze.Tests.Hud.Feedback
{
    /// <summary>Runs the entire approved source burst through independent read/write adapters.</summary>
    public sealed class FeedbackPortEquivalenceTests
    {
        [Test] public void ThirtyTickBurstAndExpiry_ProduceIdenticalCountsPositionsAndText()
        {
            var balance=StoreTestUtil.Balance();var bus=new EventBus();var h=new HudStateStore(bus,balance);var i=new InventoryStateStore(bus,balance);var e=new EffectStateStore(bus);var second=new SecondSource(balance);
            var firstScenario=new HudFeedbackScenarios(new MockHudState(h,balance),new MockInventoryState(i,balance),new MockEffectState(e),i);
            var secondScenario=new HudFeedbackScenarios(new MockHudState(second,balance),new MockInventoryState(second,balance),new MockEffectState(second),second);
            var a=new TraceView();var b=new TraceView();
            using(var p=new HudFeedbackPresenter(h,i,e,HudFeedbackTuning.Default,a,a,a,a,a,a,a,a,a))
            using(var q=new HudFeedbackPresenter(second,second,second,HudFeedbackTuning.Default,b,b,b,b,b,b,b,b,b)){
                firstScenario.Burst();secondScenario.Burst();
                for(var frame=0;frame<180;frame++){
                    const float dt=1f/30;h.Tick(dt);i.Tick(dt);e.Tick(dt);second.Tick(dt);firstScenario.Tick(dt);secondScenario.Tick(dt);a.Commands.Clear();b.Commands.Clear();p.Tick(dt,17,-21);q.Tick(dt,17,-21);
                    CollectionAssert.AreEqual(a.Commands,b.Commands,"view command trace frame "+frame);
                    Assert.AreEqual(p.Effects.Count,q.Effects.Count);Assert.AreEqual(p.Pickups.Count,q.Pickups.Count);Assert.AreEqual(p.Arcs.Count,q.Arcs.Count);Assert.AreEqual(p.Dangers.ArrowCount,q.Dangers.ArrowCount);
                }
                Assert.AreEqual(0,p.Effects.Count);Assert.AreEqual(0,p.Pickups.Count);Assert.False(p.Dangers.Banner.Visible);
            }
        }
        /// <summary>Independent state adapter built directly on Domain collections, not the production stores.</summary>
        private sealed class SecondSource:IHudState,IHudStateWriter,IInventoryState,IInventoryStateWriter,IEffectState,IEffectStateWriter
        {
            private readonly IBalanceConfigProvider _balance;private readonly DangerList _danger=new DangerList();private readonly EffectList _effects=new EffectList();private readonly ItemUseTracker _use=new ItemUseTracker();private readonly InventorySlot[] _slots=new InventorySlot[8];
            private readonly Action<int> _expiredDanger;private readonly Action<EffectKind> _expiredEffect;
            private event Action<HudStateChange> HudChanged;private event Action<InventoryChange> InventoryChanged;private event Action<EffectChange> EffectChanged;
            event Action<HudStateChange> IHudState.Changed {add{HudChanged+=value;}remove{HudChanged-=value;}}
            event Action<InventoryChange> IInventoryState.Changed {add{InventoryChanged+=value;}remove{InventoryChanged-=value;}}
            event Action<EffectChange> IEffectState.Changed {add{EffectChanged+=value;}remove{EffectChanged-=value;}}
            public event Action<ItemPickup> ItemPicked;public event Action<HitDealt> HitDealt;public event Action<DamageTaken> DamageTaken;public event Action<DangerChange> DangerChanged;public event Action<ItemUseStatus> UseChanged;
            public SecondSource(IBalanceConfigProvider balance){_balance=balance;_expiredDanger=id=>DangerChanged?.Invoke(new DangerChange(id,DangerChangeType.Expired));_expiredEffect=kind=>EffectChanged?.Invoke(new EffectChange(kind,EffectChangeType.Removed));}
            public int Hp{get;private set;}=100;public int MaxHp=>100;public GearStatus Armor=>default;public GearStatus Helmet=>default;public HeldWeaponStatus HeldWeapon=>default;public AmmoStatus Ammo=>default;public MatchStatusInfo MatchStatus=>default;public int MapSizeCells=>50;public bool HasPose{get;private set;}public HeroPose Pose{get;private set;}
            public int DangerCount=>_danger.Count;public DangerWarning GetDangerAt(int index)=>_danger.GetAt(index);public int WeaponSlotCount=>4;public int ConsumableSlotCount=>4;public int ActiveSlot{get;private set;}public InventorySlot GetSlot(int index)=>_slots[index];public ItemUseStatus Use=>_use.Status;
            public int Count=>_effects.Count;public ActiveEffect GetAt(int index)=>_effects.GetAt(index);public bool TryGet(EffectKind kind,out ActiveEffect effect)=>_effects.TryGet(kind,out effect);
            public void Tick(float dt){_danger.Tick(dt,_expiredDanger);_use.Tick(dt);_effects.Tick(dt,_expiredEffect);}
            private string Label(string id)=>_balance.TryGetEquipment(id,out var equipment)?equipment.DisplayName:_balance.TryGetWeapon(id,out var weapon)?weapon.DisplayName:id;
            public void NotifyPickup(string id,int count)=>ItemPicked?.Invoke(new ItemPickup(id,Label(id),count));public void NotifyHitDealt(HitTargetKind target)=>HitDealt?.Invoke(new HitDealt(target));public void NotifyDamageTaken(bool direction,float degrees)=>DamageTaken?.Invoke(new DamageTaken(direction,degrees));
            public void SetDanger(int id,DangerKind kind,float remaining,bool hasSource,float x,float y){var seconds=kind==DangerKind.Trap?0:remaining;_danger.Apply(new DangerWarning(id,kind,seconds,seconds,hasSource,x,y),out var change);DangerChanged?.Invoke(new DangerChange(id,change));}
            public void ClearDanger(int id){if(_danger.Remove(id))DangerChanged?.Invoke(new DangerChange(id,DangerChangeType.Removed));}
            public void BeginUse(int slot,string id,float total,float remaining,bool cancellable){_use.Begin(new ItemUseStatus(ItemUsePhase.Using,id,Label(id),slot,total,remaining,cancellable));UseChanged?.Invoke(Use);}
            public void CancelUse(string id){if(_use.Cancel(id))UseChanged?.Invoke(Use);}public void CompleteUse(string id){if(_use.Complete(id))UseChanged?.Invoke(Use);}
            public void Apply(EffectKind kind,float total,float remaining){if(_effects.Apply(kind,total,remaining,out var type))EffectChanged?.Invoke(new EffectChange(kind,type));}public void Remove(EffectKind kind){if(_effects.Remove(kind))EffectChanged?.Invoke(new EffectChange(kind,EffectChangeType.Removed));}
            public void SetWeaponSlot(int index,string id){_slots[index]=new InventorySlot(id,Label(id),1);InventoryChanged?.Invoke(InventoryChange.Slots);}public void SetConsumableSlot(int index,string id,int count){_slots[index+4]=new InventorySlot(id,Label(id),count);InventoryChanged?.Invoke(InventoryChange.Slots);}public void SetActiveSlot(int index){ActiveSlot=index;InventoryChanged?.Invoke(InventoryChange.ActiveSlot);}
            public void SetPose(float x,float y,float degrees){HasPose=true;Pose=new HeroPose(x,y,degrees);}public void SetHp(int value){Hp=value;HudChanged?.Invoke(HudStateChange.Hp);}public void SetArmor(string id,float durability){}public void SetHelmet(string id,float durability){}public void SetHeldWeapon(string id){}public void SetAmmo(int magazine,int reserve){}public void SetZoneCountdown(float seconds){}public void SetAliveCount(int alive,int total){}
            public void Reset(){while(_danger.Count>0)ClearDanger(_danger.GetAt(0).Id);if(_use.Reset())UseChanged?.Invoke(Use);while(_effects.Count>0)Remove(_effects.GetAt(0).Kind);}
        }
        /// <summary>Complete view command trace, including every visible count, position, text and opacity.</summary>
        private sealed class TraceView:IWorldViewport,IQuickbarFeedbackHooks,IUseActionView,IEffectRowView,IPickupFeedView,IHitMarkerView,IDamageArcView,IDangerBannerView,IDangerArrowView
        {
            public readonly List<string> Commands=new List<string>();
            private void Add(string value)=>Commands.Add(value);
            public bool Contains(float x,float y)=>Math.Abs(x-10)<=15&&Math.Abs(y-10)<=8;
            public void SetWeaponStripVisible(bool visible)=>Add("strip:"+visible);public void SetSlotInUse(int index,bool value)=>Add($"slot:{index},{value}");
            public void Show(bool visible,char[] label,int length,char[] seconds,int timeLength,float fill,bool hint,bool cancelled)=>Add($"use:{visible},{new string(label,0,length)},{new string(seconds,0,timeLength)},{fill:R},{hint},{cancelled}");
            public void SetChip(int index,bool visible,EffectKind kind,EffectPolarity polarity,bool timed,float fill,int seconds,int overflow,float alpha)=>Add($"chip:{index},{visible},{kind},{polarity},{timed},{fill:R},{seconds},{overflow},{alpha:R}");
            public void SetRow(int index,bool visible,string id,string name,int count,float alpha)=>Add($"pickup:{index},{visible},{id},{name},{count},{alpha:R}");public void SetOverflow(int count,float alpha)=>Add($"overflow:{count},{alpha:R}");
            public void Show(float alpha,float x,float y)=>Add($"hit:{alpha:R},{x:R},{y:R}");public void SetArc(int index,bool visible,float x,float y,float rotation,float alpha)=>Add($"arc:{index},{visible},{x:R},{y:R},{rotation:R},{alpha:R}");
            public void Show(bool visible,char[] text,int length,float alpha)=>Add($"banner:{visible},{new string(text,0,length)},{alpha:R}");public void SetArrow(int index,bool visible,float x,float y,float degrees,bool timed,int seconds,float alpha)=>Add($"arrow:{index},{visible},{x:R},{y:R},{degrees:R},{timed},{seconds},{alpha:R}");
        }
    }
}
