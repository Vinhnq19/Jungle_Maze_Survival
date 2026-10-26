#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using JungleMaze.App.Hud.Mocks;
using JungleMaze.App.Hud.State;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Hud;
using JungleMaze.Infrastructure.Config;
using JungleMaze.Presentation.UI;
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
    /// ADHOC-UI-ASSETS-A on the real HUD prefab with the real item icon catalog: quick-bar slots, pickup rows and effect
    /// chips swap sprites by ID, fall back to placeholders/labels, keep UI-01 geometry and do not allocate on warm paths.
    /// </summary>
    public sealed class UiArtIntegrationTests
    {
        private const string Ui = "Assets/_JungleMaze/Prefabs/UI/";
        private readonly List<Object> _objects = new List<Object>();
        private HudStateStore _hud; private InventoryStateStore _inventory; private EffectStateStore _effects;
        private BalanceConfigProvider _balance; private HudRoot _core; private HudFeedbackRoot _feedback; private HudItemIconCatalog _icons;
        private float _timeScale;

        [SetUp]
        public void SetUp()
        {
            _timeScale = Time.timeScale; Time.timeScale = 0;
            _balance = new BalanceConfigProvider(Application.streamingAssetsPath, null);
            var bus = new EventBus();
            _hud = new HudStateStore(bus, _balance); _inventory = new InventoryStateStore(bus, _balance); _effects = new EffectStateStore(bus);
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
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace; go.GetComponent<CanvasScaler>().enabled = false;
            var canvas = (RectTransform)go.transform; canvas.sizeDelta = new Vector2(1280, 720); canvas.position = Vector3.zero; canvas.localScale = Vector3.one;
            Tick(0);
        }

        private void Tick(float dt)
        {
            _hud.Tick(dt); _inventory.Tick(dt); _effects.Tick(dt);
            _core.Presenter.Tick(dt); _feedback.Presenter.Tick(dt, 0, 0);
            Canvas.ForceUpdateCanvases();
        }

        private static Rect WorldRect(Component c)
        {
            var corners = new Vector3[4]; ((RectTransform)c.transform).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        [UnityTest]
        public IEnumerator RealQuickbar_KnownUnknownKnownAndEmpty()
        {
            yield return Spawn();
            Scenarios().MockupV2();
            Tick(0);
            var quickbar = _core.Quickbar;
            var placeholder = AssetDatabase.LoadAssetAtPath<GameObject>(Ui + "HudSlot.prefab").GetComponent<QuickbarSlotView>().Icon.sprite;
            var expectedNames = new[] { "Bare Hands", "Crossbow", "Rubber Slingshot", "", "God Leaf", "Unicorn Blood", "", "Energy Drink" };
            for (var i = 0; i < 8; i++)
            {
                var slot = quickbar.GetSlotView(i); var item = _inventory.GetSlot(i);
                Assert.AreEqual(expectedNames[i], slot.NameLabel.text, "slot " + i);
                Assert.AreEqual(new Vector2(28, 28), ((RectTransform)slot.Icon.transform).sizeDelta, "slot " + i);
                if (item.IsEmpty)
                {
                    Assert.AreEqual(SlotVisual.Empty, slot.Visual);
                    Assert.AreSame(placeholder, slot.Icon.sprite, "empty slot keeps placeholder " + i);
                    Assert.False(slot.Frame.Edge.enabled, "empty slot hides A frame overlay");
                    continue;
                }
                Assert.AreSame(_icons.GetIcon(item.ItemId), slot.Icon.sprite, "slot " + i + " " + item.ItemId);
                Assert.AreEqual(Image.Type.Simple, slot.Icon.type);
                Assert.True(slot.Icon.preserveAspect);
                Assert.AreEqual(Color.white, slot.Icon.color, "real art is not tinted");
                Assert.True(slot.Frame.Edge.enabled);
            }
            Assert.AreEqual(SlotVisual.Selected, quickbar.GetSlotView(1).Visual);
            Assert.AreEqual("×6", Parsed(quickbar.GetSlotView(4).CountLabel));

            // Unknown ID → placeholder (dim placeholder colour), then known again → real sprite restored.
            var view = quickbar.GetSlotView(0); var count = new char[4];
            quickbar.SetSlot(0, "NOT_AN_ITEM", "???", count, 0, SlotVisual.Default);
            Assert.AreSame(placeholder, view.Icon.sprite);
            Assert.AreEqual(Image.Type.Sliced, view.Icon.type);
            Assert.AreEqual(_core.Palette.IconPlaceholder, view.Icon.color);
            quickbar.SetSlot(0, "AXE", "Axe", count, 0, SlotVisual.Default);
            Assert.AreSame(_icons.GetIcon("AXE"), view.Icon.sprite);
            Assert.AreEqual(Color.white, view.Icon.color);
        }

        [UnityTest]
        public IEnumerator PickupRow_ReusedIdsAndUnknownReset()
        {
            yield return Spawn();
            var feed = _feedback.PickupFeed; var row = feed.Rows[0];
            var placeholder = AssetDatabase.LoadAssetAtPath<GameObject>(Ui + "HudPickupToast.prefab").GetComponent<PickupToastView>().Icon.sprite;
            var size = ((RectTransform)row.Icon.transform).sizeDelta;
            feed.SetRow(0, true, "GOD_LEAF", "God Leaf", 1, 1);
            Assert.AreSame(_icons.GetIcon("GOD_LEAF"), row.Icon.sprite);
            Assert.AreEqual(Color.white, row.Icon.color, "real icon is not tinted with the placeholder colour");
            Assert.True(row.Icon.preserveAspect);
            feed.SetRow(0, true, "SHOTGUN", "Shotgun", 1, 1);
            Assert.AreSame(_icons.GetIcon("SHOTGUN"), row.Icon.sprite, "reused row swaps icon when the ID changes");
            feed.SetRow(0, true, "NOT_AN_ITEM", "?", 1, 1);
            Assert.AreSame(placeholder, row.Icon.sprite, "unknown ID never keeps the previous sprite");
            Assert.AreEqual(_core.Palette.IconPlaceholder, row.Icon.color);
            feed.SetRow(0, true, "GOD_LEAF", "God Leaf", 2, 1);
            Assert.AreSame(_icons.GetIcon("GOD_LEAF"), row.Icon.sprite);
            Assert.AreEqual(new Vector2(18, 18), size);
            Assert.AreEqual(size, ((RectTransform)row.Icon.transform).sizeDelta);

            // Through the presenter: the newest pickup row shows its own icon.
            feed.SetRow(0, false, null, null, 0, 0);
            _hud.NotifyPickup("CROSSBOW", 1); Tick(0);
            var shown = 0;
            foreach (var r in feed.Rows)
                if (r.Group.alpha > 0) { shown++; Assert.AreSame(_icons.GetIcon("CROSSBOW"), r.Icon.sprite); }
            Assert.AreEqual(1, shown);
        }

        [UnityTest]
        public IEnumerator EffectIcons_AndFallbackOverflow()
        {
            yield return Spawn();
            var chips = _feedback.EffectRow.Chips; var catalog = _feedback.Catalog;
            for (var kind = EffectKind.Adrenaline; kind <= EffectKind.CcImmune; kind++)
            {
                var polarity = EffectKinds.PolarityOf(kind);
                chips[0].Show(true, kind, polarity, true, 0.5f, 3, 0, 1);
                Assert.AreSame(catalog.Icon(kind), chips[0].Icon.sprite, kind.ToString());
                Assert.True(chips[0].Icon.enabled, kind.ToString());
                Assert.False(chips[0].Label.enabled, kind + ": icon and label never overlap");
                Assert.AreEqual(catalog.Label(kind), chips[0].Label.text, "label text kept as metadata");
                Assert.AreEqual(Color.white, chips[0].Icon.color);
                Assert.True(chips[0].Seconds.enabled, "timer kept");
                Assert.AreEqual(polarity == EffectPolarity.Harmful ? _core.Palette.Red : _core.Palette.Green, chips[0].Border.color);
            }
            chips[0].Show(true, EffectKind.Poison, EffectPolarity.Harmful, true, 0.5f, 3, 4, 1);
            Assert.False(chips[0].Icon.enabled, "overflow shows +n only");
            Assert.True(chips[0].Label.enabled);
            Assert.AreEqual("+4", Parsed(chips[0].Label));

            var noArt = Track(Object.Instantiate(catalog));
            for (var i = 0; i < noArt.Entries.Length; i++) { var e = noArt.Entries[i]; e.Icon = null; noArt.Entries[i] = e; }
            chips[1].Configure(_core.Palette, noArt);
            chips[1].Show(true, EffectKind.Slow, EffectPolarity.Harmful, false, 1, 0, 0, 1);
            Assert.False(chips[1].Icon.enabled); Assert.True(chips[1].Label.enabled);
            Assert.AreEqual("Chậm", chips[1].Label.text, "missing art falls back to the label");
            Assert.False(chips[1].Seconds.enabled, "untimed effect keeps no timer");

            // Through the presenter with the real catalog.
            _effects.Apply(EffectKind.Poison, 3, 3); Tick(0);
            Assert.True(chips[0].Icon.enabled); Assert.AreSame(catalog.Icon(EffectKind.Poison), chips[0].Icon.sprite);
            Assert.AreEqual("3s", Parsed(chips[0].Seconds));
        }

        [UnityTest]
        public IEnumerator SwapCatalogSprite_PreservesSlotAndToastRects()
        {
            yield return Spawn();
            Scenarios().MockupV2(); Tick(0);
            var slot = _core.Quickbar.GetSlotView(1); var row = _feedback.PickupFeed.Rows[0];
            _feedback.PickupFeed.SetRow(0, true, "CROSSBOW", "Crossbow", 1, 1);
            var slotRects = new[] { WorldRect(slot), WorldRect(slot.Icon), WorldRect(slot.NameLabel) };
            var rowRects = new[] { WorldRect(row), WorldRect(row.Icon), WorldRect(row.Label) };

            var texture = Track(new Texture2D(32, 16)); var clone = Track(Sprite.Create(texture, new Rect(0, 0, 32, 16), Vector2.one * 0.5f));
            var swapped = Track(Object.Instantiate(_icons));
            var entries = (HudItemIconCatalog.Entry[])swapped.Entries.Clone();
            for (var i = 0; i < entries.Length; i++) if (entries[i].Id == "CROSSBOW") entries[i].Icon = clone;
            swapped.SetEntries(entries);
            _core.Quickbar.SetIconProvider(swapped); _feedback.PickupFeed.ConfigureIcons(swapped);
            // The presenter only pushes changed slots, so replay the same slot/row state through the views.
            _core.Quickbar.SetSlot(1, "CROSSBOW", slot.NameLabel.text, new char[4], 0, slot.Visual);
            _feedback.PickupFeed.SetRow(0, true, "CROSSBOW", "Crossbow", 1, 1); Canvas.ForceUpdateCanvases();

            Assert.AreSame(clone, slot.Icon.sprite); Assert.AreSame(clone, row.Icon.sprite);
            CollectionAssert.AreEqual(slotRects, new[] { WorldRect(slot), WorldRect(slot.Icon), WorldRect(slot.NameLabel) });
            CollectionAssert.AreEqual(rowRects, new[] { WorldRect(row), WorldRect(row.Icon), WorldRect(row.Label) });
        }

        [UnityTest]
        public IEnumerator HudPrefabs_PaletteA_OneOverlayPairPerBlock_NoRaycast()
        {
            yield return Spawn();
            Assert.AreEqual(HudPalette.Default.Surface, _core.Palette.Surface);
            Assert.AreEqual(4, _core.ArtFrames.Length);
            foreach (var frame in _core.ArtFrames)
            {
                Assert.AreEqual(1, Count(frame.transform, "ArtEdge"), frame.name);
                Assert.AreEqual(1, Count(frame.transform, "ArtBevel"), frame.name);
                Assert.True(frame.Edge.GetComponent<LayoutElement>().ignoreLayout, frame.name);
                Assert.AreEqual(_core.Palette.Edge, frame.Edge.color, frame.name + " edge tint");
                Assert.AreEqual(_core.Palette.Line, frame.Bevel.color, frame.name + " bevel tint");
                Assert.AreEqual(WorldRect(frame), WorldRect(frame.Edge), frame.name + " overlay matches block");
            }
            foreach (var block in new Component[] { _core.Vitals, _core.Minimap, _core.MatchStatus })
                Assert.AreEqual(_core.Palette.Surface, block.GetComponent<Image>().color, block.name + " base keeps Surface, overlays tint separately");
            foreach (var graphic in _core.GetComponentsInChildren<Graphic>(true)) Assert.False(graphic.raycastTarget, graphic.name);
        }

        [UnityTest]
        public IEnumerator ScopedLookupAndViewWarmGC()
        {
            yield return Spawn();
            var quickbar = _core.Quickbar; var row = _feedback.PickupFeed.Rows[0]; var count = new char[] { '×', '1' };
            string[] ids = { "AXE", "NOT_AN_ITEM", "SHOTGUN" };
            for (var w = 0; w < 3; w++)
                foreach (var id in ids) { quickbar.SetSlot(2, id, "Item", count, 2, SlotVisual.Default); row.Show(true, id, "Item", 1, 1); }
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 300; i++)
            {
                var id = ids[i % ids.Length];
                _icons.GetIcon(id);
                quickbar.SetSlot(2, id, "Item", count, 2, SlotVisual.Default);
                row.Show(true, id, "Item", 1, 1);
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated, "icon swap paths (lookup, slot, toast) allocate nothing once warm");
        }

        private HudMockScenarios Scenarios() => new HudMockScenarios(new MockHudState(_hud, _balance), new MockInventoryState(_inventory, _balance), new MockEffectState(_effects));

        private static string Parsed(TMPro.TMP_Text text) { text.ForceMeshUpdate(); return text.GetParsedText(); }

        private static int Count(Transform parent, string name)
        {
            var n = 0;
            for (var i = 0; i < parent.childCount; i++) if (parent.GetChild(i).name == name) n++;
            return n;
        }
    }
}
#endif
