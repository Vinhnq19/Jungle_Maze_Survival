#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using JungleMaze.App.Hud;
using JungleMaze.App.Hud.Mocks;
using JungleMaze.App.Hud.State;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Hud;
using JungleMaze.Infrastructure.Config;
using JungleMaze.Presentation.UI.Hud;
using JungleMaze.Presentation.UI.Hud.Feedback;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JungleMaze.Tests.PlayMode
{
    /// <summary>
    /// ART-04 R2-b: every roster ID through the real HUD prefab with the real item icon catalog. Covers what
    /// UiArtIntegrationTests (ADHOC) leaves out: the default HAND slot, the full 28-ID slot sweep, icon/label rect overlap at
    /// two resolutions, all 34 IDs through the pickup toast, a second writer, and a 34-ID rotation for GC.
    /// </summary>
    public sealed class Art04IconSweepTests
    {
        private const string Ui = "Assets/_JungleMaze/Prefabs/UI/";
        private readonly List<Object> _objects = new List<Object>();
        private BalanceConfigProvider _balance; private HudItemLabels _labels;
        private HudStateStore _hud; private InventoryStateStore _inventory; private EffectStateStore _effects;
        private HudRoot _core; private HudFeedbackRoot _feedback; private HudItemIconCatalog _icons;
        private MockInventoryState _mock;
        private float _timeScale;

        [SetUp]
        public void SetUp()
        {
            _timeScale = Time.timeScale; Time.timeScale = 0;
            _balance = new BalanceConfigProvider(Application.streamingAssetsPath, null);
            Assert.IsNull(_balance.LastError);
            _labels = new HudItemLabels(_balance);
            var bus = new EventBus();
            _hud = new HudStateStore(bus, _balance); _inventory = new InventoryStateStore(bus, _balance); _effects = new EffectStateStore(bus);
            _mock = new MockInventoryState(_inventory, _balance);
            _icons = AssetDatabase.LoadAssetAtPath<HudItemIconCatalog>(Ui + "HudItemIconCatalog.asset");
            Assert.NotNull(_icons);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = _timeScale;
            for (var i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        // ---------- AC8, AC10 ----------

        [UnityTest]
        public IEnumerator DefaultSlot1_IsHand_Selected_FullAlpha_AndSelectionMovesWithoutSwappingIcons()
        {
            yield return Spawn();
            var hand = _core.Quickbar.GetSlotView(0);
            Assert.AreSame(_icons.GetIcon(HudStateDefaults.BareHandsId), hand.Icon.sprite, "G-7 default: slot 1 shows the HAND icon");
            Assert.AreEqual(SlotVisual.Selected, hand.Visual);
            Assert.AreEqual(1f, hand.Icon.color.a);

            Fill(new[] { "HAND", "SHOTGUN", "BELL", "ROPE" }, new[] { "GOD_LEAF", "UNICORN_BLOOD", "ADRENALINE", "COMPASS" });
            var sprites = SlotSprites();
            for (var active = 0; active < 8; active++)
            {
                _inventory.SetActiveSlot(active); Tick(0);
                for (var i = 0; i < 8; i++)
                {
                    var slot = _core.Quickbar.GetSlotView(i);
                    Assert.AreEqual(i == active ? SlotVisual.Selected : SlotVisual.Default, slot.Visual, $"active {active}, slot {i}");
                    Assert.AreSame(sprites[i], slot.Icon.sprite, $"active {active}: slot {i} keeps its sprite");
                    Assert.AreEqual(1f, slot.Icon.color.a, $"active {active}: slot {i} icon alpha");
                }
            }
        }

        // ---------- AC7 ----------

        [UnityTest]
        public IEnumerator Sweep_18WeaponsThroughSlots1To4_10ConsumablesThroughSlots5To8()
        {
            yield return Spawn();
            Assert.AreEqual(18, _mock.WeaponIds.Count);
            Assert.AreEqual(10, _mock.ConsumableIds.Count);
            for (var slot = 0; slot < 8; slot++)
            {
                var weapon = slot < 4;
                var ids = weapon ? _mock.WeaponIds : _mock.ConsumableIds;
                var view = _core.Quickbar.GetSlotView(slot);
                foreach (var id in ids)
                {
                    var previous = view.Icon.sprite;
                    var held = _inventory.GetSlot(slot).ItemId;
                    if (weapon) _inventory.SetWeaponSlot(slot, id); else _inventory.SetConsumableSlot(slot - 4, id, 2);
                    Tick(0);
                    Assert.AreEqual(id, _inventory.GetSlot(slot).ItemId, $"slot {slot + 1} accepted {id}");
                    var expected = _icons.GetIcon(id);
                    Assert.NotNull(expected, id);
                    Assert.AreSame(expected, view.Icon.sprite, $"slot {slot + 1}: {id}");
                    if (id != held) Assert.AreNotSame(previous, view.Icon.sprite, $"slot {slot + 1}: {id} replaced {held ?? "nothing"}");
                }
            }
        }

        // ---------- AC11 ----------

        [UnityTest]
        public IEnumerator IconRect_ClearOfKeyCountAndName_At1280x720And1920x1080()
        {
            yield return Spawn();
            Fill(new[] { "HAND", "CROSSBOW", "RUBBER_GUN", "SAHUR_BAT" }, new[] { "GOD_LEAF", "UNICORN_BLOOD", "SPIRIT_TEAR", "ELIXIR_OF_LIFE" });
            var problems = new List<string>();
            foreach (var screen in new[] { new Vector2(1280, 720), new Vector2(1920, 1080) })
            {
                ScreenAt(screen);
                for (var i = 0; i < 8; i++)
                {
                    var slot = _core.Quickbar.GetSlotView(i);
                    Assert.True(slot.NameLabel.enabled, $"slot {i + 1} name shown");
                    if (i >= 4) Assert.True(slot.CountLabel.enabled, $"slot {i + 1} count shown");
                    var icon = WorldRect(slot.Icon);
                    Check(problems, screen, i, "key", icon, WorldRect(slot.KeyLabel));
                    Check(problems, screen, i, "name", icon, WorldRect(slot.NameLabel));
                    if (slot.CountLabel.enabled) Check(problems, screen, i, "×n", icon, WorldRect(slot.CountLabel));
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        // ---------- AC13 ----------

        [UnityTest]
        public IEnumerator PickupToast_ShowsTheRightIconForAll34Ids()
        {
            yield return Spawn();
            var ids = RosterIds();
            Assert.AreEqual(34, ids.Count);
            foreach (var id in ids)
            {
                Tick(10f); // expire every row so only this pickup is on screen
                _hud.NotifyPickup(id, 1); Tick(0);
                var shown = 0;
                foreach (var row in _feedback.PickupFeed.Rows)
                {
                    if (row.Group.alpha <= 0) continue;
                    shown++;
                    Assert.AreSame(_icons.GetIcon(id), row.Icon.sprite, id);
                    Assert.AreEqual(Color.white, row.Icon.color, id + " is not tinted");
                }
                Assert.AreEqual(1, shown, id);
            }
        }

        // ---------- AC15 ----------

        [UnityTest]
        public IEnumerator SecondWriter_StandInForItm01_GivesTheSameSprites()
        {
            yield return Spawn();
            // Writer 1: the dev mock.
            string[] weapons = { "HAND", "SHOTGUN", "BELL", "ROPE" }; string[] consumables = { "GOD_LEAF", "UNICORN_BLOOD", "ADRENALINE", "COMPASS" };
            int[] counts = { 6, 1, 2, 1 };
            for (var i = 0; i < 4; i++) { _mock.SetSlot(i, weapons[i]); _mock.SetSlot(4 + i, consumables[i], counts[i]); }
            _mock.Select(5); _hud.NotifyPickup("ARMOR", 1); Tick(0);
            var mockSlots = SlotSprites(); var mockVisuals = SlotVisuals(); var mockToast = VisibleToastSprite();

            _inventory.Reset(); Tick(10f);
            for (var i = 1; i < 8; i++) Assert.AreNotSame(mockSlots[i], _core.Quickbar.GetSlotView(i).Icon.sprite, "reset cleared slot " + (i + 1));

            // Writer 2: a different owner of the same ports, writing a server-style snapshot in its own order.
            new Itm01StandIn(_inventory, _hud).ApplySnapshot(weapons, consumables, counts, 5, "ARMOR"); Tick(0);
            CollectionAssert.AreEqual(mockSlots, SlotSprites());
            CollectionAssert.AreEqual(mockVisuals, SlotVisuals());
            Assert.AreSame(mockToast, VisibleToastSprite());
            Assert.AreSame(_icons.GetIcon("ARMOR"), mockToast);
        }

        // ---------- AC19 ----------

        [UnityTest]
        public IEnumerator Rotate34IdsThrough8Slots600Times_AfterWarmup_NoGc()
        {
            yield return Spawn();
            var ids = RosterIds().ToArray();
            var names = new string[ids.Length];
            for (var i = 0; i < ids.Length; i++) names[i] = _labels.DisplayName(ids[i]);
            var count = new[] { '×', '2' };
            var quickbar = _core.Quickbar; var row = _feedback.PickupFeed.Rows[0];
            for (var frame = 0; frame < 120; frame++) { Rotate(frame, ids, names, count, quickbar, row); yield return null; }
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 600; i++) Rotate(i, ids, names, count, quickbar, row);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated, "34 IDs rotated through 8 slots and a toast row allocate nothing once warm");
        }

        private void Rotate(int step, string[] ids, string[] names, char[] count, QuickbarView quickbar, PickupToastView row)
        {
            for (var slot = 0; slot < 8; slot++)
            {
                var k = (step * 8 + slot) % ids.Length;
                _icons.GetIcon(ids[k]);
                quickbar.SetSlot(slot, ids[k], names[k], count, 2, slot == step % 8 ? SlotVisual.Selected : SlotVisual.Default);
            }
            var t = step % ids.Length;
            row.Show(true, ids[t], names[t], 1, 1);
        }

        // ---------- helpers ----------

        /// <summary>Stand-in for ITM-01: owns the inventory and HUD writers and applies a whole snapshot at once.</summary>
        private sealed class Itm01StandIn
        {
            private readonly IInventoryStateWriter _inventory; private readonly IHudStateWriter _hud;
            public Itm01StandIn(IInventoryStateWriter inventory, IHudStateWriter hud) { _inventory = inventory; _hud = hud; }

            public void ApplySnapshot(string[] weapons, string[] consumables, int[] counts, int active, string pickedUp)
            {
                _inventory.SetActiveSlot(active);
                for (var i = consumables.Length - 1; i >= 0; i--) _inventory.SetConsumableSlot(i, consumables[i], counts[i]);
                for (var i = weapons.Length - 1; i >= 0; i--) _inventory.SetWeaponSlot(i, weapons[i]);
                _hud.NotifyPickup(pickedUp, 1);
            }
        }

        private T Track<T>(T item) where T : Object { _objects.Add(item); return item; }

        private IEnumerator Spawn()
        {
            var go = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Ui + "Hud.prefab")));
            _core = go.GetComponent<HudRoot>(); _feedback = go.GetComponent<HudFeedbackRoot>();
            _core.Construct(_hud, _inventory, _effects, _icons);
            _feedback.Construct(_hud, _inventory, _effects);
            _feedback.ConstructIcons(_icons);
            yield return null;
            Assert.NotNull(_core.Presenter); Assert.NotNull(_feedback.Presenter);
            _core.enabled = false; _feedback.enabled = false;
            ScreenAt(new Vector2(1280, 720));
            Tick(0);
        }

        /// <summary>
        /// World-space stand-in for the Expand scaler at <paramref name="screen"/>: the canvas keeps its reference units and is
        /// scaled by the scaler's factor, so world units are screen pixels.
        /// </summary>
        private void ScreenAt(Vector2 screen)
        {
            var go = _core.gameObject;
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var scaler = go.GetComponent<CanvasScaler>();
            var factor = Mathf.Min(screen.x / scaler.referenceResolution.x, screen.y / scaler.referenceResolution.y);
            scaler.enabled = false;
            var canvas = (RectTransform)go.transform;
            canvas.position = Vector3.zero; canvas.rotation = Quaternion.identity;
            canvas.localScale = Vector3.one * factor;
            canvas.sizeDelta = screen / factor;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(canvas);
            Canvas.ForceUpdateCanvases();
        }

        private void Tick(float dt)
        {
            _hud.Tick(dt); _inventory.Tick(dt); _effects.Tick(dt);
            _core.Presenter.Tick(dt); _feedback.Presenter.Tick(dt, 0, 0);
            Canvas.ForceUpdateCanvases();
        }

        private void Fill(string[] weapons, string[] consumables)
        {
            for (var i = 0; i < 4; i++) { _inventory.SetWeaponSlot(i, weapons[i]); _inventory.SetConsumableSlot(i, consumables[i], 2); }
            Tick(0);
            for (var i = 0; i < 8; i++)
                Assert.AreSame(_icons.GetIcon(i < 4 ? weapons[i] : consumables[i - 4]), _core.Quickbar.GetSlotView(i).Icon.sprite, "slot " + (i + 1));
        }

        private Sprite[] SlotSprites()
        {
            var sprites = new Sprite[8];
            for (var i = 0; i < 8; i++) sprites[i] = _core.Quickbar.GetSlotView(i).Icon.sprite;
            return sprites;
        }

        private SlotVisual[] SlotVisuals()
        {
            var visuals = new SlotVisual[8];
            for (var i = 0; i < 8; i++) visuals[i] = _core.Quickbar.GetSlotView(i).Visual;
            return visuals;
        }

        private Sprite VisibleToastSprite()
        {
            Sprite sprite = null; var shown = 0;
            foreach (var row in _feedback.PickupFeed.Rows) if (row.Group.alpha > 0) { shown++; sprite = row.Icon.sprite; }
            Assert.AreEqual(1, shown, "one pickup row on screen");
            return sprite;
        }

        private List<string> RosterIds()
        {
            var ids = new List<string>();
            foreach (var w in _balance.Weapons) ids.Add(w.Id);
            foreach (var e in _balance.Equipments) ids.Add(e.Id);
            return ids;
        }

        private static Rect WorldRect(Component c)
        {
            var corners = new Vector3[4]; ((RectTransform)c.transform).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        /// <summary>Records the overlap in screen px² (float noise below 0.001 px per side counts as touching).</summary>
        private static void Check(List<string> problems, Vector2 screen, int slot, string what, Rect icon, Rect other)
        {
            var w = Mathf.Min(icon.xMax, other.xMax) - Mathf.Max(icon.xMin, other.xMin);
            var h = Mathf.Min(icon.yMax, other.yMax) - Mathf.Max(icon.yMin, other.yMin);
            if (w > 0.001f && h > 0.001f)
                problems.Add($"{screen.x}×{screen.y} slot {slot + 1}: icon {icon} ∩ {what} {other} = {w:0.##}×{h:0.##} = {w * h:0.##} px²");
        }
    }
}
#endif
