using System;
using System.IO;
using JungleMaze.App.Hud;
using JungleMaze.App.Hud.State;
using JungleMaze.App.Ports;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Hud;
using JungleMaze.Infrastructure.Config;
using JungleMaze.Presentation.UI.Hud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace JungleMaze.Tests.Hud
{
    public sealed class HudPresenterTests
    {
        private const float Dt = 1f / 60;
        private IBalanceConfigProvider _balance;
        private HudStateStore _hud;
        private InventoryStateStore _inventory;
        private EffectStateStore _effects;
        private FakeVitalsView _vitals;
        private FakeQuickbarView _quickbar;
        private FakeMinimapView _minimap;
        private FakeMatchStatusView _match;
        private HudPresenter _presenter;

        [SetUp]
        public void SetUp() => Build(new BalanceConfigProvider(Application.streamingAssetsPath, null), HudTuning.Default);

        [TearDown]
        public void TearDown() => _presenter?.Dispose();

        private void Build(IBalanceConfigProvider balance, HudTuning tuning)
        {
            _presenter?.Dispose();
            Assert.IsNull(balance.LastError);
            _balance = balance;
            var bus = new EventBus();
            _hud = new HudStateStore(bus, balance);
            _inventory = new InventoryStateStore(bus, balance);
            _effects = new EffectStateStore(bus);
            _vitals = new FakeVitalsView();
            _quickbar = new FakeQuickbarView();
            _minimap = new FakeMinimapView();
            _match = new FakeMatchStatusView();
            _presenter = new HudPresenter(_hud, _inventory, _effects, tuning, _vitals, _quickbar, _minimap, _match);
        }

        /// <summary>One frame in the same order as the game: TickRunner (Update) then HudRoot (LateUpdate).</summary>
        private void Frame(float dt = Dt)
        {
            _effects.Tick(dt);
            _presenter.Tick(dt);
        }

        private void Run(float seconds, float dt = Dt)
        {
            for (var t = 0f; t < seconds - 1e-5f; t += dt) Frame(dt);
        }

        private static int Row(VitalsRow row) => (int)row;

        [Test]
        public void DefaultState_G7()
        {
            Assert.AreEqual("100", _vitals.HpNumber);
            Assert.AreEqual("/100", _vitals.HpMax);
            Assert.AreEqual(1f, _vitals.HpFill);
            Assert.AreEqual(1f, _vitals.HpTrail);
            foreach (VitalsRow row in Enum.GetValues(typeof(VitalsRow)))
            {
                Assert.AreEqual(TextTone.Faint, _vitals.LabelTones[Row(row)], row.ToString());
                Assert.IsNull(_vitals.Values[Row(row)], row.ToString());
                Assert.AreEqual(0f, _vitals.Fills[Row(row)], row.ToString());
            }
            Assert.AreEqual("Giáp", _vitals.Labels[Row(VitalsRow.Armor)]);
            Assert.AreEqual("Mũ", _vitals.Labels[Row(VitalsRow.Helmet)]);
            Assert.AreEqual("Adrenaline", _vitals.Labels[Row(VitalsRow.Adrenaline)]);
            Assert.AreEqual("Bare Hands", _quickbar.WeaponName);
            Assert.IsFalse(_quickbar.AmmoVisible);
            Assert.IsFalse(_quickbar.ReloadVisible);
            Assert.AreEqual("Bare Hands", _quickbar.SlotLabels[0]);
            Assert.AreEqual(SlotVisual.Selected, _quickbar.Visuals[0]);
            for (var i = 1; i < 8; i++) Assert.AreEqual(SlotVisual.Empty, _quickbar.Visuals[i], $"slot {i + 1}");
            Assert.AreEqual("--:--", _match.Zone);
            Assert.AreEqual("-/-", _match.Alive);
            Assert.IsFalse(_minimap.Visible);
        }

        [Test]
        public void BindsToReadPortsOnly()
        {
            var hud = new FakeHudState();
            var inventory = new FakeInventoryState();
            var effects = new FakeEffectState();
            var vitals = new FakeVitalsView();
            var quickbar = new FakeQuickbarView();
            var match = new FakeMatchStatusView();
            var presenter = new HudPresenter(hud, inventory, effects, HudTuning.Default, vitals, quickbar, new FakeMinimapView(), match);
            Assert.AreEqual(1, hud.SubscriberCount);
            Assert.AreEqual(1, inventory.SubscriberCount);
            Assert.AreEqual(1, effects.SubscriberCount);

            hud.Hp = 40;
            hud.Raise(HudStateChange.Hp);
            Assert.AreEqual("40", vitals.HpNumber);
            inventory.Slots[4] = new InventorySlot("GOD_LEAF", "God Leaf", 3);
            inventory.ActiveSlot = 4;
            inventory.Raise(InventoryChange.All);
            Assert.AreEqual("God Leaf", quickbar.SlotLabels[4]);
            Assert.AreEqual(SlotVisual.Selected, quickbar.Visuals[4]);
            effects.HasAdrenaline = true;
            effects.Adrenaline = new ActiveEffect(EffectKind.Adrenaline, 8f, 4f);
            effects.Raise(new EffectChange(EffectKind.Adrenaline, EffectChangeType.Added));
            Assert.AreEqual("4s", vitals.Values[Row(VitalsRow.Adrenaline)]);
            hud.MatchStatus = new MatchStatusInfo(true, 84f, true, 7, 8);
            hud.Raise(HudStateChange.ZoneCountdown | HudStateChange.AliveCount);
            Assert.AreEqual("01:24", match.Zone);
            Assert.AreEqual("7/8", match.Alive);

            presenter.Dispose();
            Assert.AreEqual(0, hud.SubscriberCount);
            Assert.AreEqual(0, inventory.SubscriberCount);
            Assert.AreEqual(0, effects.SubscriberCount);
        }

        [Test]
        public void HpNumberUpdatesOnSameEvent_BarReachesTargetIn025s()
        {
            _hud.SetHp(72);
            Assert.AreEqual("72", _vitals.HpNumber, "number changes immediately");
            Assert.AreEqual(1f, _vitals.HpFill, "bar has not moved before the next tick");
            var frames = 0;
            while (_vitals.HpFill != 0.72f && frames < 100) { Frame(); frames++; }
            Assert.That(frames, Is.InRange(15, 16));
        }

        [Test]
        public void Hp30HzDecrement_FillNeverIncreases()
        {
            var previous = _vitals.HpFill;
            var hp = 100;
            var timeToNextHit = 0f;
            for (var frame = 0; frame < 600; frame++)
            {
                timeToNextHit -= Dt;
                if (timeToNextHit <= 0f && hp > 50)
                {
                    _hud.SetHp(--hp);
                    timeToNextHit += 0.033f;
                }
                Frame();
                Assert.That(_vitals.HpFill, Is.LessThanOrEqualTo(previous), $"frame {frame}");
                Assert.That(_vitals.HpTrail, Is.GreaterThanOrEqualTo(_vitals.HpFill));
                previous = _vitals.HpFill;
            }
            Assert.AreEqual(0.5f, _vitals.HpFill);
            Assert.AreEqual("50", _vitals.HpNumber);
        }

        [Test]
        public void ArmorHelmet_DurabilityFromJson_CeilNumber()
        {
            _hud.SetArmor("ARMOR", 48f);
            _hud.SetHelmet("WOODEN_HELMET", 12f);
            Run(0.3f);
            Assert.AreEqual("Spiked Armor", _vitals.Labels[Row(VitalsRow.Armor)]);
            Assert.AreEqual(TextTone.Normal, _vitals.LabelTones[Row(VitalsRow.Armor)]);
            Assert.AreEqual("48", _vitals.Values[Row(VitalsRow.Armor)]);
            Assert.That(_vitals.Fills[Row(VitalsRow.Armor)], Is.EqualTo(0.6f).Within(1e-5f));
            Assert.AreEqual("Wooden Helmet", _vitals.Labels[Row(VitalsRow.Helmet)]);
            Assert.AreEqual("12", _vitals.Values[Row(VitalsRow.Helmet)]);
            Assert.That(_vitals.Fills[Row(VitalsRow.Helmet)], Is.EqualTo(0.4f).Within(1e-5f));

            _hud.SetArmor("ARMOR", 47.2f);
            Assert.AreEqual("48", _vitals.Values[Row(VitalsRow.Armor)], "durability rounds up (G-13)");

            _hud.SetArmor("WOODEN_ARMOR", 50f);
            Run(0.3f);
            Assert.AreEqual(1f, _vitals.Fills[Row(VitalsRow.Armor)]);
            _hud.SetArmor("MAGIC_ARMOR", 60f);
            Run(0.3f);
            Assert.AreEqual("Magic Armor", _vitals.Labels[Row(VitalsRow.Armor)]);
            Assert.That(_vitals.Fills[Row(VitalsRow.Armor)], Is.EqualTo(0.5f).Within(1e-5f), "denominator 120");

            _hud.SetArmor(null, 0f);
            Run(0.3f);
            Assert.AreEqual("Giáp", _vitals.Labels[Row(VitalsRow.Armor)]);
            Assert.AreEqual(TextTone.Faint, _vitals.LabelTones[Row(VitalsRow.Armor)]);
            Assert.IsNull(_vitals.Values[Row(VitalsRow.Armor)]);
            Assert.AreEqual(0f, _vitals.Fills[Row(VitalsRow.Armor)]);
        }

        [Test]
        public void OutOfRangeValues_Clamped_NoThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                _hud.SetHp(-20);
                Run(0.3f);
                Assert.AreEqual("0", _vitals.HpNumber);
                Assert.AreEqual(0f, _vitals.HpFill);
                _hud.SetHp(500);
                Run(0.3f);
                Assert.AreEqual("100", _vitals.HpNumber);
                Assert.AreEqual(1f, _vitals.HpFill);
                _hud.SetArmor("ARMOR", 500f);
                _hud.SetHelmet("WOODEN_HELMET", -4f);
                Run(0.3f);
                Assert.AreEqual("80", _vitals.Values[Row(VitalsRow.Armor)]);
                Assert.AreEqual(1f, _vitals.Fills[Row(VitalsRow.Armor)]);
                Assert.AreEqual("0", _vitals.Values[Row(VitalsRow.Helmet)]);
                Assert.AreEqual(0f, _vitals.Fills[Row(VitalsRow.Helmet)]);
                _effects.Apply(EffectKind.Adrenaline, 8f, 50f);
                Assert.AreEqual(1f, _vitals.Fills[Row(VitalsRow.Adrenaline)]);
                _effects.Apply(EffectKind.Adrenaline, -8f, -1f);
                Assert.AreEqual(1f, _vitals.Fills[Row(VitalsRow.Adrenaline)], "invalid write ignored");
                _hud.SetAmmo(-3, -9);
                _hud.SetHeldWeapon("CROSSBOW");
                Assert.AreEqual("0", _quickbar.Magazine);
                Assert.AreEqual("/ 0", _quickbar.Reserve);
                _hud.SetAmmo(99, 30);
                Assert.AreEqual("6", _quickbar.Magazine, "clamped to magazineSize");
                _hud.SetArmor("NOT_AN_ITEM", 10f);
                Assert.AreEqual("NOT_AN_ITEM", _vitals.Labels[Row(VitalsRow.Armor)]);
            });
        }

        [Test]
        public void MaxHpFromConfig_150()
        {
            var root = Path.Combine(Path.GetTempPath(), "jungle-hud-" + Guid.NewGuid().ToString("N"));
            var folder = Path.Combine(root, BalanceConfigProvider.Folder);
            Directory.CreateDirectory(folder);
            try
            {
                foreach (var file in new[] { "match.json", "weapons.json", "equipments.json" })
                {
                    var data = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, BalanceConfigProvider.Folder, file));
                    if (file == "match.json")
                    {
                        Assert.That(data, Does.Contain("\"heroMaxHP\": 100"));
                        data = data.Replace("\"heroMaxHP\": 100", "\"heroMaxHP\": 150");
                    }
                    File.WriteAllText(Path.Combine(folder, file), data);
                }
                Build(new BalanceConfigProvider(root, null), HudTuning.Default);
                Assert.AreEqual("/150", _vitals.HpMax);
                Assert.AreEqual("150", _vitals.HpNumber);
                Assert.AreEqual(1f, _vitals.HpFill);
                _hud.SetHp(75);
                Run(0.3f);
                Assert.AreEqual(0.5f, _vitals.HpFill);
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void NoEvents600Frames_ViewsUntouched()
        {
            _hud.SetHp(60);
            _hud.SetArmor("ARMOR", 48f);
            _hud.SetHeldWeapon("CROSSBOW");
            _hud.SetAmmo(4, 30);
            _hud.SetZoneCountdown(84f);
            _hud.SetAliveCount(7, 8);
            _inventory.SetWeaponSlot(1, "CROSSBOW");
            Run(1f);
            var vitalsWrites = _vitals.Writes;
            var quickbarWrites = _quickbar.Writes;
            var matchWrites = _match.Writes;
            Run(600 * Dt);
            Assert.AreEqual(vitalsWrites, _vitals.Writes);
            Assert.AreEqual(quickbarWrites, _quickbar.Writes);
            Assert.AreEqual(matchWrites, _match.Writes);
            Assert.AreEqual("01:24", _match.Zone, "no local countdown (G-15)");
            Assert.AreEqual("60", _vitals.HpNumber);
            Assert.AreEqual("4", _quickbar.Magazine);
        }

        [Test]
        public void AdrenalineRow_YellowCeilSeconds()
        {
            _effects.Apply(EffectKind.Adrenaline, 8f, 5f);
            Assert.AreEqual("5s", _vitals.Values[Row(VitalsRow.Adrenaline)]);
            Assert.AreEqual(TextTone.Highlight, _vitals.LabelTones[Row(VitalsRow.Adrenaline)]);
            Assert.AreEqual(TextTone.Highlight, _vitals.ValueTones[Row(VitalsRow.Adrenaline)]);
            Assert.That(_vitals.Fills[Row(VitalsRow.Adrenaline)], Is.EqualTo(0.625f).Within(0.01f));
            _effects.Apply(EffectKind.Adrenaline, 8f, 4.2f);
            Assert.AreEqual("5s", _vitals.Values[Row(VitalsRow.Adrenaline)]);

            _effects.Apply(EffectKind.Adrenaline, 8f, 8f);
            Assert.AreEqual(1f, _vitals.Fills[Row(VitalsRow.Adrenaline)]);
            var elapsed = 0f;
            var previousFill = 1f;
            while (_vitals.Values[Row(VitalsRow.Adrenaline)] != null && elapsed < 20f)
            {
                Frame();
                elapsed += Dt;
                Assert.That(_vitals.Fills[Row(VitalsRow.Adrenaline)], Is.LessThanOrEqualTo(previousFill));
                previousFill = _vitals.Fills[Row(VitalsRow.Adrenaline)];
            }
            Assert.That(elapsed, Is.EqualTo(8f).Within(0.1f));
            Assert.AreEqual(0f, _vitals.Fills[Row(VitalsRow.Adrenaline)]);
            Assert.AreEqual(TextTone.Faint, _vitals.LabelTones[Row(VitalsRow.Adrenaline)]);
        }

        [Test]
        public void AmmoStrip_AllFourGuns_AndMeleeHidesAmmo()
        {
            _hud.SetHeldWeapon("CROSSBOW");
            _hud.SetAmmo(4, 30);
            Assert.AreEqual("Crossbow", _quickbar.WeaponName);
            Assert.IsTrue(_quickbar.AmmoVisible);
            Assert.AreEqual("4", _quickbar.Magazine);
            Assert.AreEqual("/ 30", _quickbar.Reserve);
            _hud.SetAmmo(3, 30);
            Assert.AreEqual("3", _quickbar.Magazine, "same event");

            foreach (var id in new[] { "RUBBER_GUN", "SCEPTER", "SHOTGUN", "CROSSBOW" })
            {
                _balance.TryGetWeapon(id, out var weapon);
                _hud.SetHeldWeapon(id);
                _hud.SetAmmo(1, 5);
                Assert.AreEqual(weapon.DisplayName, _quickbar.WeaponName, id);
                Assert.IsTrue(_quickbar.AmmoVisible, id);
                Assert.AreEqual("1", _quickbar.Magazine, id);
            }
            foreach (var id in new[] { "HAND", "KNIFE", "BANANA", "ROPE" })
            {
                _balance.TryGetWeapon(id, out var weapon);
                _hud.SetHeldWeapon(id);
                Assert.AreEqual(weapon.DisplayName, _quickbar.WeaponName, id);
                Assert.IsFalse(_quickbar.AmmoVisible, id);
                Assert.IsFalse(_quickbar.ReloadVisible, id);
            }
            _hud.SetHeldWeapon(null);
            Assert.AreEqual("Bare Hands", _quickbar.WeaponName, "null = bare hands");
        }

        [Test]
        public void ReloadHint_TogglesOnSameEvent()
        {
            _hud.SetHeldWeapon("CROSSBOW");
            _hud.SetAmmo(4, 30);
            Assert.IsTrue(_quickbar.ReloadVisible);
            _hud.SetAmmo(6, 30);
            Assert.IsFalse(_quickbar.ReloadVisible, "full magazine");
            _hud.SetAmmo(4, 0);
            Assert.IsFalse(_quickbar.ReloadVisible, "no reserve");
            _hud.SetAmmo(4, 30);
            Assert.IsTrue(_quickbar.ReloadVisible);
            _hud.SetHeldWeapon("HAND");
            Assert.IsFalse(_quickbar.ReloadVisible, "bare hands");
        }

        [Test]
        public void Scenario_MockupV2()
        {
            _inventory.SetWeaponSlot(0, "HAND");
            _inventory.SetWeaponSlot(1, "CROSSBOW");
            _inventory.SetWeaponSlot(2, "RUBBER_GUN");
            _inventory.SetWeaponSlot(3, null);
            _inventory.SetConsumableSlot(0, "GOD_LEAF", 6);
            _inventory.SetConsumableSlot(1, "UNICORN_BLOOD", 1);
            _inventory.SetConsumableSlot(2, null, 0);
            _inventory.SetConsumableSlot(3, "ADRENALINE", 4);
            _inventory.SetActiveSlot(1);
            CollectionAssert.AreEqual(
                new[] { "Bare Hands", "Crossbow", "Rubber Slingshot", "", "God Leaf", "Unicorn Blood", "", "Energy Drink" },
                _quickbar.SlotLabels);
            CollectionAssert.AreEqual(
                new[] { null, null, null, null, "×6", "×1", null, "×4" }, _quickbar.SlotCounts);
            CollectionAssert.AreEqual(new[]
            {
                SlotVisual.Default, SlotVisual.Selected, SlotVisual.Default, SlotVisual.Empty,
                SlotVisual.Default, SlotVisual.Default, SlotVisual.Empty, SlotVisual.Default
            }, _quickbar.Visuals);
            _inventory.SetConsumableSlot(1, "UNICORN_BLOOD", 0);
            Assert.AreEqual(SlotVisual.Empty, _quickbar.Visuals[5]);
            Assert.IsNull(_quickbar.SlotCounts[5]);
            _inventory.SetActiveSlot(3);
            Assert.AreEqual(SlotVisual.Selected, _quickbar.Visuals[3], "an empty selected slot stays Selected");
            Assert.AreEqual("Bare Hands", _quickbar.WeaponName, "the strip follows HeldWeapon, not the slot");
        }

        [Test]
        public void LowHpNumberRed_At25_AndTrailOnLoss()
        {
            _hud.SetHp(26);
            Assert.IsFalse(_vitals.HpLow);
            _hud.SetHp(25);
            Assert.IsTrue(_vitals.HpLow);
            _hud.SetHp(80);
            Assert.IsFalse(_vitals.HpLow);
            Run(1f);
            _hud.SetHp(72);
            Assert.AreEqual(0.8f, _vitals.HpTrail, "trail shows the HP just lost");
            Assert.That((_vitals.HpTrail - 0.72f) * 264f, Is.EqualTo(21f).Within(1f));
            Run(0.62f);
            Assert.AreEqual(0.72f, _vitals.HpTrail);
            _hud.SetHp(90);
            Frame();
            Assert.AreEqual(_vitals.HpFill, _vitals.HpTrail, "healing has no trail");

            var noFx = HudTuning.Default;
            noFx.LowHpRatio = 0f;
            noFx.DamageTrailEnabled = false;
            Build(new BalanceConfigProvider(Application.streamingAssetsPath, null), noFx);
            _hud.SetHp(10);
            Assert.IsFalse(_vitals.HpLow);
            Assert.AreEqual(_vitals.HpFill, _vitals.HpTrail);
        }

        [Test]
        public void MatchStatus_ShowsSourceValues_NoCountdown()
        {
            _hud.SetZoneCountdown(84f);
            Assert.AreEqual("01:24", _match.Zone);
            _hud.SetZoneCountdown(0f);
            Assert.AreEqual("00:00", _match.Zone);
            _hud.SetZoneCountdown(-3f);
            Assert.AreEqual("00:00", _match.Zone);
            _hud.SetAliveCount(7, 8);
            Assert.AreEqual("7/8", _match.Alive);
            _hud.SetAliveCount(7, 10);
            Assert.AreEqual("7/10", _match.Alive);
            _hud.SetAliveCount(12, 10);
            Assert.AreEqual("10/10", _match.Alive);
            _hud.SetAliveCount(-1, 10);
            Assert.AreEqual("0/10", _match.Alive);
            _hud.SetZoneCountdown(84f);
            Run(600 * Dt);
            Assert.AreEqual("01:24", _match.Zone);
            _hud.Reset();
            Assert.AreEqual("--:--", _match.Zone);
            Assert.AreEqual("-/-", _match.Alive);
        }

        [Test]
        public void Minimap_FollowsPose()
        {
            _hud.SetPose(25.5f, 25.5f, 90f);
            Frame();
            Assert.IsTrue(_minimap.Visible);
            Assert.That(_minimap.X, Is.EqualTo(100f).Within(4f));
            Assert.That(_minimap.Y, Is.EqualTo(100f).Within(4f));
            Assert.That(_minimap.Heading, Is.EqualTo(0f).Within(5f));
            var writes = _minimap.Writes;
            Frame();
            Assert.AreEqual(writes, _minimap.Writes, "unchanged pose is not rewritten");
        }

        [Test]
        public void AdrenalineRow_NeverShowsZeroWhileActive()
        {
            _effects.Apply(EffectKind.Adrenaline, 8f, 0.00005f);
            Assert.AreEqual("1s", _vitals.Values[Row(VitalsRow.Adrenaline)], "G-13: 0 only when the effect is over");
            Frame();
            Assert.IsNull(_vitals.Values[Row(VitalsRow.Adrenaline)], "expired: number hidden");
        }

        [Test]
        public void TickWithoutChanges_NoGcAlloc()
        {
            _hud.SetHp(60);
            _hud.SetPose(10f, 10f, 0f);
            Run(1f);
            Assert.That(() =>
            {
                for (var i = 0; i < 120; i++) Frame();
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
