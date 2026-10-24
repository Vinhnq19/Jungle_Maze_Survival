using System;
using System.Collections.Generic;
using JungleMaze.App.Hud.State;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Hud;
using JungleMaze.Domain.Network;
using JungleMaze.Infrastructure.Config;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace JungleMaze.Tests.Hud
{
    internal static class StoreTestUtil
    {
        public static BalanceConfigProvider Balance()
        {
            var balance = new BalanceConfigProvider(Application.streamingAssetsPath, null);
            Assert.IsNull(balance.LastError);
            return balance;
        }

        public static void Disconnect(EventBus bus) =>
            bus.Publish(new NetworkStateChanged(NetworkState.Joined, NetworkState.Disconnected, DisconnectReason.None));
    }

    public sealed class HudStateStoreTests
    {
        private EventBus _bus;
        private HudStateStore _store;
        private readonly List<HudStateChange> _changes = new List<HudStateChange>();

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _store = new HudStateStore(_bus, StoreTestUtil.Balance());
            _store.Initialize();
            _changes.Clear();
            _store.Changed += c => _changes.Add(c);
        }

        [TearDown]
        public void TearDown() => _store.Dispose();

        [Test]
        public void DefaultsMatchG7()
        {
            Assert.AreEqual(100, _store.Hp);
            Assert.AreEqual(100, _store.MaxHp);
            Assert.AreEqual(50, _store.MapSizeCells);
            Assert.IsFalse(_store.Armor.IsWorn);
            Assert.IsFalse(_store.Helmet.IsWorn);
            Assert.AreEqual("HAND", _store.HeldWeapon.ItemId);
            Assert.AreEqual("Bare Hands", _store.HeldWeapon.DisplayName);
            Assert.IsNotNull(_store.HeldWeapon.Definition);
            Assert.IsFalse(_store.HasPose);
            Assert.IsFalse(_store.MatchStatus.HasZoneCountdown);
            Assert.IsFalse(_store.MatchStatus.HasAlive);
        }

        [Test]
        public void ResolvesNamesAndMaximaFromJson()
        {
            _store.SetArmor("ARMOR", 48f);
            Assert.AreEqual("Spiked Armor", _store.Armor.DisplayName);
            Assert.AreEqual(80f, _store.Armor.MaxDurability);
            _store.SetHelmet("WOODEN_HELMET", 12f);
            Assert.AreEqual(30f, _store.Helmet.MaxDurability);
            _store.SetHeldWeapon("CROSSBOW");
            Assert.AreEqual("Crossbow", _store.HeldWeapon.DisplayName);
            Assert.AreEqual(6, _store.HeldWeapon.Definition.Stats.MagazineSize);
            _store.SetHeldWeapon("");
            Assert.AreEqual("HAND", _store.HeldWeapon.ItemId, "empty = bare hands");
        }

        [Test]
        public void ChangedWritesRaiseOnce_IdenticalWritesRaiseNothing()
        {
            _store.SetHp(72);
            _store.SetHp(72);
            _store.SetArmor("ARMOR", 48f);
            _store.SetArmor("ARMOR", 48f);
            _store.SetHeldWeapon("HAND");
            _store.SetAmmo(4, 30);
            _store.SetAmmo(4, 30);
            _store.SetZoneCountdown(84f);
            _store.SetZoneCountdown(84f);
            _store.SetAliveCount(7, 8);
            _store.SetAliveCount(7, 8);
            CollectionAssert.AreEqual(new[]
            {
                HudStateChange.Hp, HudStateChange.Armor, HudStateChange.Ammo,
                HudStateChange.ZoneCountdown, HudStateChange.AliveCount
            }, _changes);
            _store.SetPose(1f, 2f, 3f);
            Assert.AreEqual(5, _changes.Count, "pose is polled, not raised");
            Assert.IsTrue(_store.HasPose);
        }

        [Test]
        public void NaNIgnored_NoThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                _store.SetArmor("ARMOR", float.NaN);
                _store.SetHelmet("WOODEN_HELMET", float.NaN);
                _store.SetZoneCountdown(float.NaN);
                _store.SetPose(float.NaN, 0f, 0f);
                _store.SetHeldWeapon("NOT_A_WEAPON");
            });
            CollectionAssert.AreEqual(new[] { HudStateChange.HeldWeapon }, _changes);
            Assert.IsNull(_store.HeldWeapon.Definition, "unknown id keeps a null definition");
            Assert.IsFalse(_store.HasPose);
        }

        [Test]
        public void Disconnect_ResetsToDefaults()
        {
            _store.SetHp(10);
            _store.SetArmor("ARMOR", 20f);
            _store.SetPose(4f, 4f, 0f);
            StoreTestUtil.Disconnect(_bus);
            Assert.AreEqual(100, _store.Hp);
            Assert.IsFalse(_store.Armor.IsWorn);
            Assert.IsFalse(_store.HasPose);
            Assert.AreEqual(HudStateChange.All, _changes[_changes.Count - 1]);
        }
    }

    public sealed class InventoryStateStoreTests
    {
        private EventBus _bus;
        private InventoryStateStore _store;
        private readonly List<InventoryChange> _changes = new List<InventoryChange>();

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _store = new InventoryStateStore(_bus, StoreTestUtil.Balance());
            _store.Initialize();
            _changes.Clear();
            _store.Changed += c => _changes.Add(c);
        }

        [TearDown]
        public void TearDown() => _store.Dispose();

        [Test]
        public void DefaultsMatchG7()
        {
            Assert.AreEqual(4, _store.WeaponSlotCount);
            Assert.AreEqual(4, _store.ConsumableSlotCount);
            Assert.AreEqual("HAND", _store.GetSlot(0).ItemId);
            Assert.AreEqual("Bare Hands", _store.GetSlot(0).DisplayName);
            for (var i = 1; i < 8; i++) Assert.IsTrue(_store.GetSlot(i).IsEmpty, $"slot {i + 1}");
            Assert.AreEqual(0, _store.ActiveSlot);
        }

        [Test]
        public void BadIndexIgnored_NoThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                _store.SetWeaponSlot(-1, "CROSSBOW");
                _store.SetWeaponSlot(4, "CROSSBOW");
                _store.SetConsumableSlot(9, "GOD_LEAF", 2);
                _store.SetActiveSlot(8);
                _store.SetActiveSlot(-3);
                Assert.IsTrue(_store.GetSlot(-1).IsEmpty);
                Assert.IsTrue(_store.GetSlot(99).IsEmpty);
            });
            Assert.AreEqual(0, _changes.Count);
        }

        [Test]
        public void ConsumableCountZero_EmptiesSlot_AndIdenticalWritesRaiseNothing()
        {
            _store.SetConsumableSlot(0, "GOD_LEAF", 6);
            _store.SetConsumableSlot(0, "GOD_LEAF", 6);
            Assert.AreEqual("God Leaf", _store.GetSlot(4).DisplayName);
            Assert.AreEqual(6, _store.GetSlot(4).Count);
            _store.SetConsumableSlot(0, "GOD_LEAF", 0);
            Assert.IsTrue(_store.GetSlot(4).IsEmpty);
            _store.SetActiveSlot(5);
            _store.SetActiveSlot(5);
            CollectionAssert.AreEqual(new[] { InventoryChange.Slots, InventoryChange.Slots, InventoryChange.ActiveSlot }, _changes);
        }

        [Test]
        public void Disconnect_ResetsToDefaults()
        {
            _store.SetWeaponSlot(1, "CROSSBOW");
            _store.SetActiveSlot(1);
            StoreTestUtil.Disconnect(_bus);
            Assert.IsTrue(_store.GetSlot(1).IsEmpty);
            Assert.AreEqual(0, _store.ActiveSlot);
            Assert.AreEqual(InventoryChange.All, _changes[_changes.Count - 1]);
        }
    }

    public sealed class EffectStateStoreTests
    {
        private const float Dt = 1f / 60;
        private EventBus _bus;
        private EffectStateStore _store;
        private readonly List<EffectChange> _changes = new List<EffectChange>();

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _store = new EffectStateStore(_bus);
            _store.Initialize();
            _changes.Clear();
            _store.Changed += c => _changes.Add(c);
        }

        [TearDown]
        public void TearDown() => _store.Dispose();

        [Test]
        public void StartsEmpty()
        {
            Assert.AreEqual(0, _store.Count);
            Assert.IsFalse(_store.TryGet(EffectKind.Adrenaline, out _));
        }

        [Test]
        public void Adrenaline8s_LinearThenRemoved()
        {
            _store.Apply(EffectKind.Adrenaline, 8f, 8f);
            Assert.AreEqual(EffectChangeType.Added, _changes[0].Type);
            var elapsed = 0f;
            while (_store.TryGet(EffectKind.Adrenaline, out var effect) && elapsed < 20f)
            {
                Assert.That(effect.RemainingSeconds, Is.EqualTo(8f - elapsed).Within(0.01f));
                _store.Tick(Dt);
                elapsed += Dt;
            }
            Assert.That(elapsed, Is.EqualTo(8f).Within(0.1f));
            Assert.AreEqual(0, _store.Count);
            Assert.AreEqual(new EffectChange(EffectKind.Adrenaline, EffectChangeType.Removed), _changes[_changes.Count - 1]);
        }

        [Test]
        public void ApplyAgain_Refreshes()
        {
            _store.Apply(EffectKind.Adrenaline, 8f, 8f);
            _store.Tick(5f);
            _store.Apply(EffectKind.Adrenaline, 8f, 8f);
            Assert.IsTrue(_store.TryGet(EffectKind.Adrenaline, out var effect));
            Assert.AreEqual(8f, effect.RemainingSeconds);
            Assert.AreEqual(1, _store.Count);
            Assert.AreEqual(EffectChangeType.Refreshed, _changes[1].Type);
        }

        [Test]
        public void OtherKinds_CoexistInList()
        {
            var future = (EffectKind)99; // UI-01-P2 adds kinds without changing the port.
            _store.Apply(EffectKind.Adrenaline, 8f, 8f);
            _store.Apply(future, 0f, 0f);
            Assert.AreEqual(2, _store.Count);
            Assert.AreEqual(EffectKind.Adrenaline, _store.GetAt(0).Kind);
            Assert.AreEqual(future, _store.GetAt(1).Kind);
            Assert.IsFalse(_store.GetAt(1).IsTimed);
            _store.Tick(9f);
            Assert.AreEqual(1, _store.Count, "untimed effect stays");
            Assert.AreEqual(future, _store.GetAt(0).Kind);
            _store.Remove(future);
            Assert.AreEqual(0, _store.Count);
        }

        [Test]
        public void NegativeOrNaNDuration_Ignored()
        {
            Assert.DoesNotThrow(() =>
            {
                _store.Apply(EffectKind.Adrenaline, -8f, 1f);
                _store.Apply(EffectKind.Adrenaline, float.NaN, 1f);
                _store.Apply(EffectKind.Adrenaline, 8f, float.NaN);
                _store.Apply(EffectKind.Adrenaline, 8f, -1f);
                _store.Apply(EffectKind.None, 8f, 8f);
                _store.Remove(EffectKind.Adrenaline);
                _store.Tick(float.NaN);
            });
            Assert.AreEqual(0, _store.Count);
            Assert.AreEqual(0, _changes.Count);
        }

        [Test]
        public void Disconnect_ResetsToDefaults()
        {
            _store.Apply(EffectKind.Adrenaline, 8f, 8f);
            StoreTestUtil.Disconnect(_bus);
            Assert.AreEqual(0, _store.Count);
            Assert.AreEqual(EffectChangeType.Removed, _changes[_changes.Count - 1].Type);
        }

        [Test]
        public void TickWithEffects_NoGcAlloc()
        {
            var store = new EffectStateStore(new EventBus());
            Action<EffectChange> handler = _ => { };
            store.Changed += handler;
            store.Apply(EffectKind.Adrenaline, 1f, 1f);
            store.Apply((EffectKind)50, 100f, 100f);
            store.Tick(Dt);
            Assert.That(() =>
            {
                for (var i = 0; i < 120; i++) store.Tick(Dt); // Adrenaline expires inside the loop.
            }, Is.Not.AllocatingGCMemory());
            Assert.AreEqual(1, store.Count);
        }
    }
}
