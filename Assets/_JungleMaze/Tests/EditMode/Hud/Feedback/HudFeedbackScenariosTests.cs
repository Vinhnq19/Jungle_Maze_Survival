#if UNITY_EDITOR || DEVELOPMENT_BUILD
using JungleMaze.App.Hud.Mocks;
using JungleMaze.App.Hud.State;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Hud;
using NUnit.Framework;
using System.Collections.Generic;

namespace JungleMaze.Tests.Hud.Feedback
{
    public sealed class HudFeedbackScenariosTests
    {
        [Test] public void EveryUsableEquipment_ReadsUseSecondsFromJson_AndCompletesAtSource()
        {
            var balance=StoreTestUtil.Balance();var inv=new InventoryStateStore(new EventBus(),balance);var mock=new MockInventoryState(inv,balance);var count=0;
            foreach(var e in balance.Equipments){if(e.Stats.UseSeconds<=0)continue;count++;mock.SetSlot(4,e.Id,2);mock.BeginUse(4);Assert.AreEqual(e.Stats.UseSeconds,inv.Use.TotalSeconds,e.Id);inv.Tick(e.Stats.UseSeconds);Assert.AreEqual(ItemUsePhase.Using,inv.Use.Phase);mock.CompleteUse();Assert.AreEqual(ItemUsePhase.Completed,inv.Use.Phase);Assert.AreEqual(2,inv.GetSlot(4).Count);}
            Assert.GreaterOrEqual(count,9);
        }
        [Test] public void Burst_HasSpecifiedEventCounts_AndStops()
        {
            var balance=StoreTestUtil.Balance();var bus=new EventBus();var hud=new HudStateStore(bus,balance);var inv=new InventoryStateStore(bus,balance);var effects=new EffectStateStore(bus);
            var mock=new MockHudState(hud,balance);var mi=new MockInventoryState(inv,balance);var me=new MockEffectState(effects);var scenario=new HudFeedbackScenarios(mock,mi,me,inv);
            var pickups=0;var damage=0;var hits=0;var effectCount=0;var uses=0;var pickupIds=new HashSet<string>();var effectIds=new HashSet<EffectKind>();var directions=new HashSet<float>();
            hud.ItemPicked+=p=>{pickups++;pickupIds.Add(p.ItemId);Assert.True(balance.TryGetEquipment(p.ItemId,out _)||balance.TryGetWeapon(p.ItemId,out _));};hud.DamageTaken+=d=>{damage++;directions.Add(d.DirectionDegrees);};hud.HitDealt+=_=>hits++;effects.Changed+=c=>{effectCount++;effectIds.Add(c.Kind);};inv.UseChanged+=u=>{if(u.Phase==ItemUsePhase.Using)uses++;};
            scenario.Burst();for(var i=0;i<30;i++)scenario.Tick(1f/30);
            Assert.AreEqual(20,pickups);Assert.AreEqual(8,pickupIds.Count);Assert.AreEqual(10,effectCount);Assert.AreEqual(4,effectIds.Count);Assert.AreEqual(8,damage);Assert.AreEqual(6,directions.Count);Assert.AreEqual(15,hits);Assert.AreEqual(5,hud.DangerCount);Assert.AreEqual(2,uses);Assert.False(scenario.Running);
            var outside=0;var traps=0;for(var j=0;j<hud.DangerCount;j++){var w=hud.GetDangerAt(j);if(System.Math.Abs(w.SourceX-10)>15)outside++;if(w.Kind==DangerKind.Trap)traps++;}Assert.AreEqual(2,outside);Assert.AreEqual(1,traps);Assert.AreEqual(100,hud.Hp);
        }
        [Test] public void Skirmish_IsCompassStunSixHitsOffscreenBombAndFourPickups()
        {
            var balance=StoreTestUtil.Balance();var bus=new EventBus();var hud=new HudStateStore(bus,balance);var inv=new InventoryStateStore(bus,balance);var effects=new EffectStateStore(bus);
            var scenario=new HudFeedbackScenarios(new MockHudState(hud,balance),new MockInventoryState(inv,balance),new MockEffectState(effects),inv);var pickups=0;var hits=0;hud.ItemPicked+=_=>pickups++;hud.HitDealt+=_=>hits++;
            scenario.Skirmish();Assert.AreEqual("COMPASS",inv.Use.ItemId);Assert.AreEqual(2,inv.Use.TotalSeconds);Assert.True(effects.TryGet(EffectKind.Stun,out var stun));Assert.AreEqual(5,stun.DurationSeconds);Assert.AreEqual(4,pickups);Assert.AreEqual(1,hud.DangerCount);Assert.AreEqual(1.5f,hud.GetDangerAt(0).RemainingSeconds);Assert.AreEqual(-10,hud.GetDangerAt(0).SourceX);
            for(var frame=0;frame<60;frame++){hud.Tick(1f/30);inv.Tick(1f/30);effects.Tick(1f/30);scenario.Tick(1f/30);}Assert.AreEqual(6,hits);Assert.AreEqual(4,pickups);Assert.AreEqual(100,hud.Hp);
        }
    }
}
#endif
