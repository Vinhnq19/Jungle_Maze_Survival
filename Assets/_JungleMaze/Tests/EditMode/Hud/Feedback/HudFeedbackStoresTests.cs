using JungleMaze.App.Hud.State;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Hud;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace JungleMaze.Tests.Hud.Feedback
{
    public sealed class HudFeedbackStoresTests
    {
        [Test] public void NotifyPickup_ResolvesName_IgnoresBadInput()
        {
            var store = new HudStateStore(new EventBus(), StoreTestUtil.Balance()); var count = 0; ItemPickup last = default;
            store.ItemPicked += item => { count++; last = item; };
            store.NotifyPickup(null, 2); store.NotifyPickup("GOD_LEAF", 0); Assert.AreEqual(0, count);
            store.NotifyPickup("GOD_LEAF", 2); Assert.AreEqual(1, count); Assert.AreEqual("God Leaf", last.DisplayName); Assert.AreEqual(2, last.Count);
        }
        [Test] public void DangerTick_ExpiredAndResetRemovedEvents_AreDistinct()
        {
            var store = new HudStateStore(new EventBus(), StoreTestUtil.Balance()); DangerChange last = default;
            store.DangerChanged += change => last = change;
            store.SetDanger(1, DangerKind.FusedBomb, 1.5f, true, 7, 7); store.Tick(1.5f);
            Assert.AreEqual(DangerChangeType.Expired, last.Type); Assert.AreEqual(0, store.DangerCount);
            store.SetDanger(2, DangerKind.Trap, 0, false, 0, 0); store.Reset(); Assert.AreEqual(DangerChangeType.Removed, last.Type);
        }
        [Test] public void Disconnect_ResetsDangersAndUse_WithoutChangingCountsOnCompletion()
        {
            var bus = new EventBus(); var balance = StoreTestUtil.Balance();
            var hud = new HudStateStore(bus, balance); var inv = new InventoryStateStore(bus, balance); hud.Initialize(); inv.Initialize();
            try {
                inv.SetConsumableSlot(0, "GOD_LEAF", 6); inv.BeginUse(4,"GOD_LEAF",.5f,.5f,true); inv.Tick(1);
                Assert.AreEqual(ItemUsePhase.Using,inv.Use.Phase); inv.CompleteUse("GOD_LEAF"); Assert.AreEqual(6,inv.GetSlot(4).Count);
                hud.SetDanger(2,DangerKind.Trap,0,false,0,0); StoreTestUtil.Disconnect(bus);
                Assert.AreEqual(0,hud.DangerCount); Assert.AreEqual(ItemUsePhase.Idle,inv.Use.Phase);
            } finally { hud.Dispose(); inv.Dispose(); }
        }
        [Test] public void Tick_DoesNotAllocate()
        {
            var bus = new EventBus(); var balance = StoreTestUtil.Balance(); var hud = new HudStateStore(bus,balance);var inv = new InventoryStateStore(bus,balance);
            hud.SetDanger(1,DangerKind.FusedBomb,30,true,1,1);inv.BeginUse(4,"GOD_LEAF",30,30,true);
            hud.Tick(.01f);inv.Tick(.01f);
            Assert.That(()=>{hud.Tick(.01f);inv.Tick(.01f);}, Is.Not.AllocatingGCMemory());
        }
    }
}
