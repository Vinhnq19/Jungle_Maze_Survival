using JungleMaze.App.Hud.State;
using JungleMaze.App.World;
using JungleMaze.Core.Events;
using JungleMaze.Infrastructure.Config;
using JungleMaze.World.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace JungleMaze.Tests.World
{
    public sealed class WorldHudBindingTests
    {
        [Test] public void UnarmedKeepsFourSlotsFreeAndCanSelectAgain()
        {
            var balance=new BalanceConfigProvider(Application.streamingAssetsPath,null);var bus=new EventBus();
            var inventory=new InventoryStateStore(bus,balance);var hud=new HudStateStore(bus,balance);
            var binding=new InventoryHudBinding(inventory,hud);var state=new InventorySnapshot{ActiveWeapon=-1};
            state.Weapons[0]=new ItemStack("AXE");binding.Apply(state,0);
            Assert.AreEqual(-1,inventory.ActiveSlot);Assert.AreEqual("AXE",inventory.GetSlot(0).ItemId);Assert.AreEqual("HAND",hud.HeldWeapon.ItemId);
            state.ActiveWeapon=0;binding.Apply(state,0);Assert.AreEqual(0,inventory.ActiveSlot);Assert.AreEqual("AXE",hud.HeldWeapon.ItemId);
            binding.Reset();Assert.AreEqual(-1,inventory.ActiveSlot);Assert.IsNull(inventory.GetSlot(0).ItemId);
        }
        [Test] public void ImmediateConsumedElixirStillShowsProgress()
        {
            var balance=new BalanceConfigProvider(Application.streamingAssetsPath,null);var bus=new EventBus();
            var inventory=new InventoryStateStore(bus,balance);var hud=new HudStateStore(bus,balance);
            var binding=new InventoryHudBinding(inventory,hud);
            var state=new InventorySnapshot{ActiveWeapon=-1,IsUsing=true,UseSlot=0,UseItemId="ELIXIR",UseDuration=1,UseEnd=2};
            binding.Apply(state,1.2);Assert.AreEqual("ELIXIR",inventory.Use.ItemId);Assert.AreEqual(.8f,inventory.Use.RemainingSeconds,.001f);
            state.IsUsing=false;binding.Apply(state,2);Assert.AreNotEqual(JungleMaze.Domain.Hud.ItemUsePhase.Using,inventory.Use.Phase);
        }
    }
}
