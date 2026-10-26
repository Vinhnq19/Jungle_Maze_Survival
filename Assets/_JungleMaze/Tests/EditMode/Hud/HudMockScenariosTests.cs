using System.Collections.Generic;
using JungleMaze.App.Hud.Mocks;
using JungleMaze.App.Hud.State;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Hud;
using NUnit.Framework;

namespace JungleMaze.Tests.Hud
{
    public sealed class HudMockScenariosTests
    {
        private HudStateStore _hud;
        private InventoryStateStore _inventory;
        private EffectStateStore _effects;
        private HudMockScenarios _scenarios;

        [SetUp]
        public void SetUp()
        {
            var balance = StoreTestUtil.Balance();
            var bus = new EventBus();
            _hud = new HudStateStore(bus, balance);
            _inventory = new InventoryStateStore(bus, balance);
            _effects = new EffectStateStore(bus);
            _scenarios = new HudMockScenarios(new MockHudState(_hud, balance), new MockInventoryState(_inventory, balance),
                new MockEffectState(_effects));
        }

        [Test]
        public void MockupV2_ProducesAc14State()
        {
            _scenarios.MockupV2();
            var ids = new string[8];
            var counts = new int[8];
            for (var i = 0; i < 8; i++)
            {
                ids[i] = _inventory.GetSlot(i).ItemId;
                counts[i] = _inventory.GetSlot(i).Count;
            }
            CollectionAssert.AreEqual(new[] { "HAND", "CROSSBOW", "RUBBER_GUN", null, "GOD_LEAF", "UNICORN_BLOOD", null, "ADRENALINE" }, ids);
            CollectionAssert.AreEqual(new[] { 1, 1, 1, 0, 6, 1, 0, 4 }, counts);
            Assert.AreEqual(1, _inventory.ActiveSlot);
            Assert.AreEqual("CROSSBOW", _hud.HeldWeapon.ItemId);
            Assert.AreEqual(4, _hud.Ammo.InMagazine);
            Assert.AreEqual(30, _hud.Ammo.Reserve);
            Assert.AreEqual("ARMOR", _hud.Armor.ItemId);
            Assert.AreEqual(48f, _hud.Armor.Durability);
            Assert.AreEqual("WOODEN_HELMET", _hud.Helmet.ItemId);
            Assert.AreEqual(12f, _hud.Helmet.Durability);
            Assert.AreEqual(84f, _hud.MatchStatus.ZoneSecondsRemaining);
            Assert.AreEqual(7, _hud.MatchStatus.Alive);
            Assert.AreEqual(10, _hud.MatchStatus.Total);
            Assert.IsTrue(_effects.TryGet(EffectKind.Adrenaline, out var adrenaline));
            Assert.AreEqual(8f, adrenaline.DurationSeconds);
            Assert.AreEqual(5f, adrenaline.RemainingSeconds);
        }

        [Test]
        public void HpStepDown_100To0_Step10()
        {
            var seen = new List<int>();
            while (_scenarios.HpStepDown(10)) seen.Add(_hud.Hp);
            seen.Add(_hud.Hp);
            CollectionAssert.AreEqual(new[] { 90, 80, 70, 60, 50, 40, 30, 20, 10, 0 }, seen);
            Assert.IsFalse(_scenarios.HpStepDown(10));
            Assert.AreEqual(0, _hud.Hp);
        }

        [Test]
        public void HealStepUp_0To100()
        {
            _scenarios.Hud.SetHp(0);
            var steps = 0;
            while (_scenarios.HealStepUp(10)) steps++;
            Assert.AreEqual(9, steps);
            Assert.AreEqual(100, _hud.Hp);
        }

        [Test]
        public void StressStep_WritesEveryKind()
        {
            var hudChanges = HudStateChange.None;
            var inventoryChanges = InventoryChange.None;
            var effectChanges = 0;
            _hud.Changed += c => hudChanges |= c;
            _inventory.Changed += c => inventoryChanges |= c;
            _effects.Changed += _ => effectChanges++;
            for (var frame = 0; frame < 300; frame++)
            {
                _scenarios.StressStep(frame);
                _effects.Tick(1f / 30);
            }
            Assert.AreEqual(HudStateChange.All & ~HudStateChange.MaxHp, hudChanges);
            Assert.AreEqual(InventoryChange.All, inventoryChanges);
            Assert.That(effectChanges, Is.GreaterThan(0));
            Assert.IsTrue(_hud.HasPose);
        }
    }
}
