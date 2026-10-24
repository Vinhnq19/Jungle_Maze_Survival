#if UNITY_EDITOR || DEVELOPMENT_BUILD
using JungleMaze.App.Hud.Mocks;
using JungleMaze.App.Hud.State;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Hud;
using NUnit.Framework;

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
            var pickups=0;var damage=0;var hits=0;hud.ItemPicked+=_=>pickups++;hud.DamageTaken+=_=>damage++;hud.HitDealt+=_=>hits++;
            scenario.Burst();for(var i=0;i<30;i++)scenario.Tick(1f/30);
            Assert.AreEqual(10,pickups);Assert.AreEqual(12,damage);Assert.AreEqual(12,hits);Assert.AreEqual(3,hud.DangerCount);Assert.False(scenario.Running);
        }
    }
}
#endif
