#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using JungleMaze.App.Hud.Mocks;
using JungleMaze.App.Hud.State;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Hud;
using JungleMaze.Infrastructure.Config;
using JungleMaze.Presentation.Player;
using JungleMaze.Presentation.UI.Hud;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JungleMaze.Tests.PlayMode
{
    /// <summary>UI-01 HUD: layout against Figma v2, style, fonts and runtime behaviour on the real prefabs.</summary>
    public sealed class HudAcceptanceTests
    {
        private const string HudPrefabPath = "Assets/_JungleMaze/Prefabs/UI/Hud.prefab";
        private const string SlotPrefabPath = "Assets/_JungleMaze/Prefabs/UI/HudSlot.prefab";
        private const string KeyCapPrefabPath = "Assets/_JungleMaze/Prefabs/UI/HudKeyCap.prefab";
        private const string HeroPrefabPath = "Assets/_JungleMaze/Prefabs/Hero.prefab";

        private readonly List<GameObject> _objects = new List<GameObject>();
        private BalanceConfigProvider _balance;
        private HudStateStore _hud;
        private InventoryStateStore _inventory;
        private EffectStateStore _effects;
        private HudRoot _root;
        private float _timeScale, _captureDeltaTime;
        private SimulationMode2D _simulation;

        [SetUp]
        public void SetUp()
        {
            _timeScale = Time.timeScale;
            _captureDeltaTime = Time.captureDeltaTime;
            _simulation = Physics2D.simulationMode;
            _balance = new BalanceConfigProvider(Application.streamingAssetsPath, null);
            Assert.IsNull(_balance.LastError);
            var bus = new EventBus();
            _hud = new HudStateStore(bus, _balance);
            _inventory = new InventoryStateStore(bus, _balance);
            _effects = new EffectStateStore(bus);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = _timeScale;
            Time.captureDeltaTime = _captureDeltaTime;
            Physics2D.simulationMode = _simulation;
            foreach (var go in _objects) if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
        }

        // ---------- Layout (AC1, AC2, AC3, AC20, AC22, AC13) ----------

        [UnityTest]
        public IEnumerator Layout_FourBlocksMatchFigmaV2_At1280x720()
        {
            yield return SpawnHud();
            var canvas = MeasureAt(1280f, 720f);
            AssertRect(HudFigmaV2.Vitals, Measure(canvas, _root.Vitals.transform), "Vitals");
            AssertRect(HudFigmaV2.QuickBar, Measure(canvas, _root.Quickbar.transform), "QuickBar");
            AssertRect(HudFigmaV2.Minimap, Measure(canvas, _root.Minimap.transform), "Minimap");
            AssertRect(HudFigmaV2.MatchStatus, Measure(canvas, _root.MatchStatus.transform), "MatchStatus");
            var quickbar = Measure(canvas, _root.Quickbar.transform);
            Assert.That(quickbar.center.x, Is.EqualTo(640f).Within(HudFigmaV2.Tolerance));
            Assert.That(1280f - Measure(canvas, _root.Minimap.transform).xMax, Is.EqualTo(24f).Within(HudFigmaV2.Tolerance));
            Assert.That(1280f - Measure(canvas, _root.MatchStatus.transform).xMax, Is.EqualTo(24f).Within(HudFigmaV2.Tolerance));
            Assert.That(720f - Measure(canvas, _root.Vitals.transform).yMax, Is.EqualTo(24f).Within(HudFigmaV2.Tolerance));
            Assert.That(720f - quickbar.yMax, Is.EqualTo(24f).Within(HudFigmaV2.Tolerance));
        }

        [UnityTest]
        public IEnumerator Layout_ScalerIsExpand1280x720_AndBlocksInsideWithMargins()
        {
            yield return SpawnHud();
            var scaler = _root.GetComponent<CanvasScaler>();
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(new Vector2(1280, 720), scaler.referenceResolution);
            Assert.AreEqual(CanvasScaler.ScreenMatchMode.Expand, scaler.screenMatchMode);
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, _root.GetComponent<Canvas>().renderMode);
            Assert.IsNull(_root.GetComponent<GraphicRaycaster>(), "HUD must not catch clicks");
            foreach (var graphic in _root.GetComponentsInChildren<Graphic>(true))
                Assert.IsFalse(graphic.raycastTarget, graphic.name);

            var canvas = MeasureAt(1280f, 720f);
            var blocks = Blocks(canvas);
            for (var i = 0; i < blocks.Length; i++)
            {
                var r = blocks[i];
                Assert.That(r.xMin, Is.GreaterThanOrEqualTo(24f - HudFigmaV2.Tolerance), $"block {i} left");
                Assert.That(r.yMin, Is.GreaterThanOrEqualTo(24f - HudFigmaV2.Tolerance), $"block {i} top");
                Assert.That(1280f - r.xMax, Is.GreaterThanOrEqualTo(24f - HudFigmaV2.Tolerance), $"block {i} right");
                Assert.That(720f - r.yMax, Is.GreaterThanOrEqualTo(24f - HudFigmaV2.Tolerance), $"block {i} bottom");
                for (var j = i + 1; j < blocks.Length; j++) Assert.AreEqual(0f, OverlapArea(r, blocks[j]), $"blocks {i}/{j} overlap");
            }
            var minimap = Measure(canvas, _root.Minimap.transform);
            Assert.That(minimap.width / minimap.height, Is.EqualTo(1f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Layout_KeepsKillfeedAndCenterClear()
        {
            yield return SpawnHud();
            var canvas = MeasureAt(1280f, 720f);
            foreach (var block in Blocks(canvas))
            {
                Assert.AreEqual(0f, OverlapArea(block, HudFigmaV2.Killfeed), block.ToString());
                Assert.AreEqual(0f, OverlapArea(block, HudFigmaV2.CenterClear), block.ToString());
            }
        }

        [UnityTest]
        public IEnumerator Layout_ChildElementsMatchFigmaV2()
        {
            yield return SpawnHud();
            var canvas = MeasureAt(1280f, 720f);
            var vitals = Measure(canvas, _root.Vitals.transform);
            AssertRect(Offset(HudFigmaV2.HpBar, vitals), Measure(canvas, _root.Vitals.HpBar.transform), "HpBar");
            var rows = new[] { _root.Vitals.ArmorRow, _root.Vitals.HelmetRow, _root.Vitals.AdrenalineRow };
            var names = new[] { "ArmorRow", "HelmetRow", "AdrenalineRow" };
            for (var i = 0; i < 3; i++)
            {
                Assert.AreEqual(names[i], rows[i].name, "row order Giáp → Mũ → Adrenaline");
                var row = Measure(canvas, rows[i].transform);
                Assert.That(row.y - vitals.y, Is.EqualTo(HudFigmaV2.RowY[i]).Within(HudFigmaV2.Tolerance), names[i]);
                AssertRect(Offset(HudFigmaV2.RowBar, row), Measure(canvas, rows[i].Bar.transform), names[i] + " bar");
            }
            var quickbar = Measure(canvas, _root.Quickbar.transform);
            var strip = Measure(canvas, _root.Quickbar.WeaponStrip);
            Assert.That(strip.height, Is.EqualTo(HudFigmaV2.WeaponStripHeight).Within(HudFigmaV2.Tolerance));
            Assert.That(strip.y - quickbar.y, Is.EqualTo(0f).Within(HudFigmaV2.Tolerance));
            Assert.That(strip.center.x, Is.EqualTo(640f).Within(HudFigmaV2.Tolerance));
            for (var i = 0; i < 8; i++)
            {
                var slot = Measure(canvas, _root.Quickbar.GetSlotView(i).transform);
                var group = i / 4;
                var expectedX = (group == 0 ? 0f : HudFigmaV2.ConsumableGroupX) + (i % 4) * HudFigmaV2.SlotStep;
                Assert.That(slot.width, Is.EqualTo(HudFigmaV2.SlotSize).Within(HudFigmaV2.Tolerance), $"slot {i + 1} w");
                Assert.That(slot.height, Is.EqualTo(HudFigmaV2.SlotSize).Within(HudFigmaV2.Tolerance), $"slot {i + 1} h");
                Assert.That(slot.x - quickbar.x, Is.EqualTo(expectedX).Within(HudFigmaV2.Tolerance), $"slot {i + 1} x");
                Assert.That(slot.y - quickbar.y, Is.EqualTo(HudFigmaV2.SlotRowY).Within(HudFigmaV2.Tolerance), $"slot {i + 1} y");
            }
        }

        [UnityTest]
        public IEnumerator Layout_NoClipping_16x10_And_21x9()
        {
            yield return SpawnHud();
            foreach (var size in new[] { new Vector2(1280f, 800f), new Vector2(1706.67f, 720f) })
            {
                var canvas = MeasureAt(size.x, size.y);
                var blocks = Blocks(canvas);
                for (var i = 0; i < blocks.Length; i++)
                {
                    var r = blocks[i];
                    Assert.That(r.xMin, Is.GreaterThanOrEqualTo(0f), $"{size} block {i}");
                    Assert.That(r.yMin, Is.GreaterThanOrEqualTo(0f), $"{size} block {i}");
                    Assert.That(r.xMax, Is.LessThanOrEqualTo(size.x), $"{size} block {i}");
                    Assert.That(r.yMax, Is.LessThanOrEqualTo(size.y), $"{size} block {i}");
                    for (var j = i + 1; j < blocks.Length; j++) Assert.AreEqual(0f, OverlapArea(r, blocks[j]), $"{size} {i}/{j}");
                }
            }
        }

        [UnityTest]
        public IEnumerator WeaponStrip_StaysCentered_WithAndWithoutAmmo()
        {
            yield return SpawnHud();
            _hud.SetHeldWeapon("CROSSBOW");
            _hud.SetAmmo(4, 30);
            Assert.IsTrue(_root.Quickbar.Ammo.activeSelf);
            Assert.IsTrue(_root.Quickbar.ReloadHint.activeSelf);
            var canvas = MeasureAt(1280f, 720f);
            var withAmmo = Measure(canvas, _root.Quickbar.WeaponStrip);
            Assert.That(withAmmo.center.x, Is.EqualTo(640f).Within(HudFigmaV2.Tolerance), "Crossbow 4/30 + R Nạp");
            _hud.SetHeldWeapon("HAND");
            Assert.IsFalse(_root.Quickbar.Ammo.activeSelf);
            Assert.IsFalse(_root.Quickbar.ReloadHint.activeSelf);
            canvas = MeasureAt(1280f, 720f);
            var bare = Measure(canvas, _root.Quickbar.WeaponStrip);
            Assert.That(bare.center.x, Is.EqualTo(640f).Within(HudFigmaV2.Tolerance), "Bare Hands");
            Assert.That(bare.width, Is.LessThan(withAmmo.width));
        }

        // ---------- Behaviour (AC10, AC11, AC14, AC15, AC17, AC18) ----------

        [UnityTest]
        public IEnumerator Hud_DefaultState_BeforeAnyData()
        {
            yield return SpawnHud();
            var vitals = _root.Vitals;
            Assert.AreEqual("100", Parsed(vitals.HpNumber));
            Assert.AreEqual("/100", Parsed(vitals.HpMax));
            Assert.AreEqual(1f, vitals.HpBar.FillValue);
            var palette = _root.Palette;
            foreach (var row in new[] { vitals.ArmorRow, vitals.HelmetRow, vitals.AdrenalineRow })
            {
                AssertColor(palette.Faint, row.Label.color, row.name + " label faint");
                Assert.IsFalse(row.Value.enabled, row.name + " number hidden");
                Assert.AreEqual(0f, row.Bar.FillValue, row.name + " empty");
            }
            Assert.AreEqual("Giáp", vitals.ArmorRow.Label.text);
            Assert.AreEqual("Mũ", vitals.HelmetRow.Label.text);
            Assert.AreEqual("Bare Hands", _root.Quickbar.WeaponName.text);
            Assert.IsFalse(_root.Quickbar.Ammo.activeSelf);
            Assert.IsFalse(_root.Quickbar.ReloadHint.activeSelf);
            Assert.AreEqual("Bare Hands", _root.Quickbar.GetSlotView(0).NameLabel.text);
            Assert.AreEqual(SlotVisual.Selected, _root.Quickbar.GetSlotView(0).Visual);
            for (var i = 1; i < 8; i++) Assert.AreEqual(SlotVisual.Empty, _root.Quickbar.GetSlotView(i).Visual, $"slot {i + 1}");
            for (var i = 0; i < 8; i++) Assert.AreEqual((i + 1).ToString(), _root.Quickbar.GetSlotView(i).KeyLabel.text);
            Assert.AreEqual("VŨ KHÍ", _root.transform.Find("QuickBar/Slots/WeaponGroup/Label").GetComponent<TMP_Text>().text);
            Assert.AreEqual("DƯỢC PHẨM", _root.transform.Find("QuickBar/Slots/ConsumableGroup/Label").GetComponent<TMP_Text>().text);
            Assert.AreEqual("--:--", Parsed(_root.MatchStatus.ZoneValue));
            Assert.AreEqual("-/-", Parsed(_root.MatchStatus.AliveValue));
            Assert.IsFalse(_root.Minimap.Marker.gameObject.activeSelf, "no Hero yet");
        }

        [UnityTest]
        public IEnumerator Hud_HoldsValues10sWithoutUpdates()
        {
            yield return SpawnHud();
            var scenarios = Scenarios();
            scenarios.MockupV2();
            _effects.Remove(EffectKind.Adrenaline);
            yield return null;
            _root.Presenter.Tick(1f); // settle tweens
            var hp = Parsed(_root.Vitals.HpNumber);
            var fill = _root.Vitals.HpBar.FillValue;
            var armor = Parsed(_root.Vitals.ArmorRow.Value);
            var ammo = Parsed(_root.Quickbar.WeaponStrip.Find("Ammo/Magazine").GetComponent<TMP_Text>());
            var zone = Parsed(_root.MatchStatus.ZoneValue);
            var alive = Parsed(_root.MatchStatus.AliveValue);
            var visuals = new SlotVisual[8];
            for (var i = 0; i < 8; i++) visuals[i] = _root.Quickbar.GetSlotView(i).Visual;

            _effects.Apply(EffectKind.Adrenaline, 8f, 8f);
            Time.timeScale = 4f;
            var elapsed = 0f;
            while (elapsed < 10f)
            {
                _effects.Tick(Time.deltaTime); // TickRunner's job in the game
                elapsed += Time.deltaTime;
                yield return null;
            }
            Time.timeScale = _timeScale;
            Assert.AreEqual(hp, Parsed(_root.Vitals.HpNumber));
            Assert.AreEqual(fill, _root.Vitals.HpBar.FillValue);
            Assert.AreEqual(armor, Parsed(_root.Vitals.ArmorRow.Value));
            Assert.AreEqual(ammo, Parsed(_root.Quickbar.WeaponStrip.Find("Ammo/Magazine").GetComponent<TMP_Text>()));
            Assert.AreEqual(zone, Parsed(_root.MatchStatus.ZoneValue), "zone clock does not count down on its own (G-15)");
            Assert.AreEqual(alive, Parsed(_root.MatchStatus.AliveValue));
            for (var i = 0; i < 8; i++) Assert.AreEqual(visuals[i], _root.Quickbar.GetSlotView(i).Visual);
            Assert.AreEqual(0f, _root.Vitals.AdrenalineRow.Bar.FillValue, "adrenaline still counts down (AC12)");
            Assert.IsFalse(_root.Vitals.AdrenalineRow.Value.enabled);
        }

        [UnityTest]
        public IEnumerator Scenario_MockupV2_ShowsDisplayNamesAndCounts()
        {
            yield return SpawnHud();
            Scenarios().MockupV2();
            var expectedNames = new[] { "Bare Hands", "Crossbow", "Rubber Slingshot", "", "God Leaf", "Unicorn Blood", "", "Energy Drink" };
            var expectedVisuals = new[]
            {
                SlotVisual.Default, SlotVisual.Selected, SlotVisual.Default, SlotVisual.Empty,
                SlotVisual.Default, SlotVisual.Default, SlotVisual.Empty, SlotVisual.Default
            };
            for (var i = 0; i < 8; i++)
            {
                var slot = _root.Quickbar.GetSlotView(i);
                Assert.AreEqual(expectedNames[i], slot.NameLabel.text, $"slot {i + 1}");
                Assert.AreEqual(expectedVisuals[i], slot.Visual, $"slot {i + 1}");
            }
            Assert.AreEqual("×6", Parsed(_root.Quickbar.GetSlotView(4).CountLabel));
            Assert.AreEqual("×4", Parsed(_root.Quickbar.GetSlotView(7).CountLabel));
            Assert.AreEqual("Crossbow", _root.Quickbar.WeaponName.text);
            Assert.AreEqual("72", Parsed(_root.Vitals.HpNumber));
            Assert.AreEqual("01:24", Parsed(_root.MatchStatus.ZoneValue));
            Assert.AreEqual("7/10", Parsed(_root.MatchStatus.AliveValue));
            Assert.AreEqual("5s", Parsed(_root.Vitals.AdrenalineRow.Value));
        }

        [UnityTest]
        public IEnumerator HpTrail_WidthMatchesLoss()
        {
            yield return SpawnHud();
            _hud.SetHp(80);
            _root.Presenter.Tick(1f);
            _hud.SetHp(72);
            _root.Presenter.Tick(0.27f); // fill tween done (0.25 s), trail still holding (0.3 s)
            var canvas = MeasureAt(1280f, 720f);
            var fill = Measure(canvas, _root.Vitals.HpBar.FillImage.transform);
            var trail = Measure(canvas, _root.Vitals.HpBar.TrailImage.transform);
            Assert.That(trail.width - fill.width, Is.EqualTo(21f).Within(1f));
            _root.Presenter.Tick(0.4f);
            canvas = MeasureAt(1280f, 720f);
            fill = Measure(canvas, _root.Vitals.HpBar.FillImage.transform);
            trail = Measure(canvas, _root.Vitals.HpBar.TrailImage.transform);
            Assert.That(trail.width - fill.width, Is.EqualTo(0f).Within(0.5f), "trail retracted");
        }

        [UnityTest]
        public IEnumerator Minimap_FollowsHeroPose()
        {
            Physics2D.simulationMode = SimulationMode2D.Script;
            yield return SpawnHud();
            var hero = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HeroPrefabPath)));
            hero.GetComponent<HeroController>().enabled = false;
            var reporter = hero.GetComponent<LocalHeroPoseReporter>();
            Assert.IsNotNull(reporter, "Hero.prefab carries the pose reporter");
            reporter.Construct(_hud);
            var body = hero.GetComponent<Rigidbody2D>();
            var cases = new[]
            {
                (cell: 0, expected: 0f, rotation: 0f), (cell: 25, expected: 100f, rotation: 90f), (cell: 49, expected: 200f, rotation: 225f)
            };
            foreach (var c in cases)
            {
                var position = new Vector2(c.cell + 0.5f, c.cell + 0.5f); // T9: Hero at the cell centre, error per axis
                body.position = position;
                body.rotation = c.rotation;
                hero.transform.SetPositionAndRotation(position, Quaternion.Euler(0, 0, c.rotation));
                Physics2D.SyncTransforms();
                yield return null; // reporter Update
                yield return null; // HUD LateUpdate of the frame the pose was written
                var marker = _root.Minimap.Marker;
                Assert.IsTrue(marker.gameObject.activeSelf);
                Assert.That(marker.anchoredPosition.x, Is.EqualTo(c.expected).Within(4f), $"cell {c.cell} x");
                Assert.That(marker.anchoredPosition.y, Is.EqualTo(c.expected).Within(4f), $"cell {c.cell} y");
                var needle = Mathf.DeltaAngle(_root.Minimap.Heading.localEulerAngles.z, c.rotation - 90f);
                Assert.That(Mathf.Abs(needle), Is.LessThanOrEqualTo(5f), $"cell {c.cell} heading");
            }
        }

        [UnityTest]
        public IEnumerator Hud_Stress30HzFor60s_NoErrors()
        {
            yield return SpawnHud();
            var scenarios = Scenarios();
            Time.captureDeltaTime = 1f / 30;
            for (var frame = 0; frame < 1800; frame++)
            {
                scenarios.StressStep(frame);
                _effects.Tick(1f / 30);
                yield return null;
            }
            Time.captureDeltaTime = _captureDeltaTime;
            LogAssert.NoUnexpectedReceived();
            Assert.IsTrue(_root.enabled);
        }

        // ---------- Style and fonts (AC24, AC26) ----------

        [UnityTest]
        public IEnumerator Text_MinSize13_AndVietnameseGlyphs()
        {
            yield return SpawnHud();
            var required = new List<string> { "VŨ KHÍ", "DƯỢC PHẨM", "Bo thu sau", "Còn sống", "Nạp", "Tay không", "Giáp", "Mũ", "Adrenaline", "…" };
            foreach (var weapon in _balance.Weapons) required.Add(weapon.DisplayName);
            foreach (var equipment in _balance.Equipments) required.Add(equipment.DisplayName);
            var numberTexts = new HashSet<TMP_Text>
            {
                _root.Vitals.HpNumber, _root.Vitals.ArmorRow.Value, _root.Vitals.HelmetRow.Value, _root.Vitals.AdrenalineRow.Value,
                _root.MatchStatus.ZoneValue, _root.MatchStatus.AliveValue,
                _root.Quickbar.WeaponStrip.Find("Ammo/Magazine").GetComponent<TMP_Text>(),
                _root.Quickbar.ReloadHint.transform.Find("KeyCap/Label").GetComponent<TMP_Text>()
            };
            for (var i = 0; i < 8; i++)
            {
                numberTexts.Add(_root.Quickbar.GetSlotView(i).KeyLabel);
                numberTexts.Add(_root.Quickbar.GetSlotView(i).CountLabel);
            }
            var all = new List<TMP_Text>(_root.GetComponentsInChildren<TMP_Text>(true));
            foreach (var path in new[] { SlotPrefabPath, KeyCapPrefabPath })
                all.AddRange(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<TMP_Text>(true));
            Assert.That(all.Count, Is.GreaterThan(30));
            foreach (var text in all)
            {
                Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(13f), text.name);
                if (text.enableAutoSizing) Assert.That(text.fontSizeMin, Is.GreaterThanOrEqualTo(13f), text.name);
                Assert.IsNotNull(text.font, text.name);
                foreach (var s in required)
                    Assert.IsTrue(text.font.HasCharacters(s, out uint[] missing, true), $"{text.name} ({text.font.name}) misses '{s}': {Codepoints(missing)}");
            }
            foreach (var text in _root.GetComponentsInChildren<TMP_Text>(true))
            {
                var expected = numberTexts.Contains(text) ? "Pixelify Sans" : "Be Vietnam Pro";
                Assert.AreEqual(expected, text.font.faceInfo.familyName, text.name);
            }
        }

        [UnityTest]
        public IEnumerator Palette_MatchesStyleGuide()
        {
            yield return SpawnHud();
            _effects.Apply(EffectKind.Adrenaline, 8f, 8f);
            _inventory.SetActiveSlot(1);
            AssertColor(Hex(0xE5, 0x48, 0x4D), _root.Vitals.HpBar.FillImage.color, "HP fill");
            AssertColor(Hex(0x5B, 0xB8, 0xE8), _root.Vitals.ArmorRow.Bar.FillImage.color, "armor fill");
            AssertColor(Hex(0x5B, 0xB8, 0xE8), _root.Vitals.HelmetRow.Bar.FillImage.color, "helmet fill");
            AssertColor(Hex(0xF5, 0xC5, 0x42), _root.Vitals.AdrenalineRow.Bar.FillImage.color, "adrenaline fill");
            AssertColor(Hex(0xF5, 0xC5, 0x42), _root.Vitals.AdrenalineRow.Label.color, "adrenaline label");
            AssertColor(Hex(0xF5, 0xC5, 0x42), _root.Vitals.AdrenalineRow.Value.color, "adrenaline number");
            AssertColor(Hex(0xF2, 0xA3, 0x3A), _root.Quickbar.GetSlotView(1).Border.color, "selected slot border");
            AssertColor(Hex(0xA2, 0x74, 0xFF), _root.MatchStatus.ZoneValue.color, "zone clock");
            var surface = Hex(0x15, 0x23, 0x1B);
            surface.a = 0.72f;
            foreach (var block in new Component[] { _root.Vitals, _root.Minimap, _root.MatchStatus })
                AssertColor(surface, block.GetComponent<Image>().color, block.name + " background");
            AssertColor(surface, _root.Quickbar.WeaponStrip.GetComponent<Image>().color, "weapon strip background");
            var round10 = _root.Vitals.GetComponent<Image>().sprite;
            Assert.AreEqual("HudRound10", round10.name);
            Assert.AreEqual(new Vector4(10, 10, 10, 10), round10.border);
            Assert.AreEqual(Image.Type.Sliced, _root.Vitals.GetComponent<Image>().type);
            Assert.AreEqual(1f, _root.Vitals.GetComponent<Image>().pixelsPerUnitMultiplier);
            _hud.SetHp(25);
            AssertColor(Hex(0xE5, 0x48, 0x4D), _root.Vitals.HpNumber.color, "HP number at 25");
            _hud.SetHp(26);
            AssertColor(Hex(0xF3, 0xEE, 0xDB), _root.Vitals.HpNumber.color, "HP number at 26");
        }

        // ---------- helpers ----------

        private IEnumerator SpawnHud()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            Assert.IsNotNull(prefab);
            var go = Track(Object.Instantiate(prefab));
            _root = go.GetComponent<HudRoot>();
            _root.Construct(_hud, _inventory, _effects, new NullItemIconProvider());
            yield return null; // Start creates the presenter
            Assert.IsNotNull(_root.Presenter, "HudRoot started");
        }

        private HudMockScenarios Scenarios() => new HudMockScenarios(new MockHudState(_hud, _balance),
            new MockInventoryState(_inventory, _balance), new MockEffectState(_effects));

        /// <summary>Puts the canvas in world space at exactly <paramref name="width"/>×<paramref name="height"/> units.</summary>
        private RectTransform MeasureAt(float width, float height)
        {
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            _root.GetComponent<CanvasScaler>().enabled = false;
            var rect = (RectTransform)_root.transform;
            rect.localScale = Vector3.one;
            rect.position = Vector3.zero;
            rect.rotation = Quaternion.identity;
            rect.sizeDelta = new Vector2(width, height);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_root.Quickbar.WeaponStrip);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            Canvas.ForceUpdateCanvases();
            return rect;
        }

        /// <summary>Rect of <paramref name="target"/> in canvas units, origin at the canvas top-left (Figma convention).</summary>
        private static Rect Measure(RectTransform canvas, Transform target)
        {
            var root = new Vector3[4];
            canvas.GetWorldCorners(root);
            var corners = new Vector3[4];
            ((RectTransform)target).GetWorldCorners(corners);
            var left = corners[0].x - root[0].x;
            var top = root[1].y - corners[1].y;
            return new Rect(left, top, corners[2].x - corners[0].x, corners[1].y - corners[0].y);
        }

        private Rect[] Blocks(RectTransform canvas) => new[]
        {
            Measure(canvas, _root.Vitals.transform), Measure(canvas, _root.Quickbar.transform),
            Measure(canvas, _root.Minimap.transform), Measure(canvas, _root.MatchStatus.transform)
        };

        private static Rect Offset(Rect child, Rect parent) => new Rect(parent.x + child.x, parent.y + child.y, child.width, child.height);

        private static float OverlapArea(Rect a, Rect b)
        {
            var w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            var h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            return w > 0.01f && h > 0.01f ? w * h : 0f;
        }

        private static void AssertRect(Rect expected, Rect actual, string label)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(HudFigmaV2.Tolerance), label + " x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(HudFigmaV2.Tolerance), label + " y");
            Assert.That(actual.width, Is.EqualTo(expected.width).Within(HudFigmaV2.Tolerance), label + " w");
            Assert.That(actual.height, Is.EqualTo(expected.height).Within(HudFigmaV2.Tolerance), label + " h");
        }

        private static void AssertColor(Color expected, Color actual, string label)
        {
            const float channel = 1f / 255f + 1e-4f;
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(channel), label + " r");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(channel), label + " g");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(channel), label + " b");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.01f), label + " a");
        }

        private static Color Hex(byte r, byte g, byte b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

        private static string Parsed(TMP_Text text)
        {
            text.ForceMeshUpdate();
            return text.GetParsedText();
        }

        private static string Codepoints(uint[] missing)
        {
            if (missing == null) return "";
            var chars = new char[missing.Length];
            for (var i = 0; i < missing.Length; i++) chars[i] = (char)missing[i];
            return new string(chars);
        }

        private GameObject Track(GameObject go) { _objects.Add(go); return go; }
    }
}
#endif
