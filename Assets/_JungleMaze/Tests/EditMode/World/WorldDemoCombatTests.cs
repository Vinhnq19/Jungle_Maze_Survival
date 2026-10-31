using System;
using JungleMaze.App.Hud.State;
using JungleMaze.App.World;
using JungleMaze.Core.Events;
using JungleMaze.Infrastructure.Config;
using JungleMaze.World.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace JungleMaze.Tests.World
{
    public sealed class WorldDemoCombatTests
    {
        private sealed class Port : IItemGameplayPort
        {
            public void Heal(byte actor,float amount) { }
            public void ApplyEffect(byte actor,ItemEffect effect,double seconds) { }
            public void Rescue(byte actor,float hp,double seconds) { }
        }
        private sealed class Rig : IDisposable
        {
            public readonly InventoryService Items;
            public readonly WorldDemoCombatSession Combat;
            public readonly MemoryItemWorld Floor;
            private ushort _id;
            private uint _request;
            public Rig()
            {
                var balance=new BalanceConfigProvider(Application.streamingAssetsPath,null);
                Floor=new MemoryItemWorld(()=>++_id);
                Items=new InventoryService(Guid.NewGuid(),ItemCatalog.CreateBaseline(),new Port(),Floor);Items.Register(0);
                Combat=new WorldDemoCombatSession(balance,new HudStateStore(new EventBus(),balance),Items);
            }
            public void Pickup(ItemStack item)
            {Assert.IsTrue(Floor.TrySpawn(item,0,0,out var id));Assert.AreEqual(ItemResult.Ok,Items.Pickup(Items.MatchId,0,++_request,id));}
            public void Select(int slot)=>Assert.AreEqual(ItemResult.Ok,Items.Select(Items.MatchId,0,++_request,slot));
            public void Dispose()=>Combat.Dispose();
        }
        [Test] public void SelectedAxeAttacksWithConfiguredCadence()
        {
            using(var r=new Rig())
            {
                r.Pickup(new ItemStack("AXE"));r.Select(0);int hits=0;r.Combat.Confirmed+=(weapon,aim)=>hits++;
                Assert.IsTrue(r.Combat.TryAttack(30));Assert.IsFalse(r.Combat.TryAttack(30));
                Assert.AreEqual("AXE",r.Combat.Controller.SelectedSlot.Definition.Id);Assert.AreEqual(1,hits);
                r.Combat.Tick(1.01f);Assert.IsTrue(r.Combat.TryAttack(30));Assert.AreEqual(2,hits);
            }
        }
        [Test] public void ShotgunUsesInventoryAmmoAndReloadsWithoutGrantingFakeDefaults()
        {
            using(var r=new Rig())
            {
                r.Pickup(new ItemStack("SHOTGUN",1,0,9,3));r.Select(0);
                Assert.IsTrue(r.Combat.TryAttack(0));Assert.AreEqual(2,r.Items.HeldWeapon(0).Magazine);
                Assert.AreEqual(9,r.Items.HeldWeapon(0).Ammo);Assert.IsTrue(r.Combat.TryReload());
                Assert.IsFalse(r.Combat.TryAttack(0));r.Combat.Tick(3);
                Assert.AreEqual(4,r.Items.HeldWeapon(0).Magazine);Assert.AreEqual(7,r.Items.HeldWeapon(0).Ammo);
            }
        }
        [Test] public void SwitchingSameKindGunCancelsReloadWithoutTransferringAmmo()
        {
            using(var r=new Rig())
            {
                r.Pickup(new ItemStack("SHOTGUN",1,0,9,2));r.Pickup(new ItemStack("SHOTGUN",1,0,6,1));r.Select(0);
                Assert.IsTrue(r.Combat.TryReload());r.Select(1);r.Combat.Tick(3);
                Assert.IsFalse(r.Combat.Controller.IsReloading);
                Assert.AreEqual(2,r.Items.GetSnapshot(0).Weapons[0].Magazine);
                Assert.AreEqual(1,r.Items.HeldWeapon(0).Magazine);Assert.AreEqual(6,r.Items.HeldWeapon(0).Ammo);
            }
        }
    }
}
