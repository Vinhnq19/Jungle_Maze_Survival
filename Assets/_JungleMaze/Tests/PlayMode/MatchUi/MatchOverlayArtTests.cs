#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using JungleMaze.App.MatchUi.Mocks;
using JungleMaze.Domain.MatchUi;
using JungleMaze.Presentation.UI.MatchOverlay;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JungleMaze.Tests.PlayMode.MatchUi
{
    /// <summary>
    /// UI-03 art binding (ticket section 8): the 19 overlay sprites are imported per Docs/Art/Production_A/MatchOverlay/
    /// asset_mapping.json and sit on the prefab elements it names, with the reused HUD and menu sprites. Icons never
    /// cover text or push it into an ellipsis; the cause icon follows the cause, the killer mark the killer.
    /// </summary>
    public sealed class MatchOverlayArtTests
    {
        private const string Art = "Assets/_JungleMaze/Art/UI/MatchOverlay/";
        private static readonly Guid Match = Guid.NewGuid();

        // Name → 9-slice border (px, all sides), from asset_mapping.json.
        private static readonly Dictionary<string, int> Sprites = new Dictionary<string, int>
        {
            { "SliderHandle", 0 }, { "ToggleBox", 12 }, { "ToggleCheck", 0 }, { "ZoneRing", 0 }, { "KillcamGrid", 0 },
            { "PlayerDot", 0 }, { "PlayerDotOutline", 0 }, { "KillerMark", 0 }, { "CausePlayer", 0 }, { "CauseZone", 0 },
            { "CauseLeft", 0 }, { "CauseDisconnect", 0 }, { "CauseUnknown", 0 }, { "IconVolume", 0 }, { "IconMusic", 0 },
            { "IconMouse", 0 }, { "IconShake", 0 }, { "IconExit", 0 }, { "IconSpectate", 0 }
        };

        private MatchOverlayRoot _root;
        private OverlayRig _rig;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root.gameObject);
            _rig?.Dispose();
            _rig = null;
        }

        private MatchOverlayRoot Spawn()
        {
            _root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(OverlayRig.PrefabPath)).GetComponent<MatchOverlayRoot>();
            var canvas = _root.Canvas;
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)_root.transform;
            rt.sizeDelta = new Vector2(1280, 720);
            rt.position = Vector3.zero;
            rt.localScale = Vector3.one;
            Canvas.ForceUpdateCanvases();
            return _root;
        }

        private static string SpriteOf(Component c) => c.GetComponent<Image>().sprite?.name;
        private static Image Child(Component c, string path) => c.transform.Find(path).GetComponent<Image>();

        [Test]
        public void OverlaySprites_ImportSettings()
        {
            Assert.AreEqual(19, Sprites.Count);
            foreach (var pair in Sprites)
            {
                var path = Art + pair.Key + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.NotNull(importer, path);
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, pair.Key);
                Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode, pair.Key);
                Assert.AreEqual(100f, importer.spritePixelsPerUnit, pair.Key);
                Assert.AreEqual(FilterMode.Point, importer.filterMode, pair.Key);
                Assert.IsFalse(importer.mipmapEnabled, pair.Key);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, pair.Key);
                Assert.AreEqual(pair.Key == "KillcamGrid" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp, importer.wrapMode, pair.Key);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.NotNull(sprite, path);
                Assert.AreEqual(Vector4.one * pair.Value, sprite.border, pair.Key + " border");
            }
        }

        [Test]
        public void Prefab_BindsMappedSprites()
        {
            var root = Spawn();
            var menu = root.Menu;
            foreach (var slider in new[] { menu.Master, menu.Sfx, menu.Music, menu.Sensitivity })
            {
                Assert.AreEqual("SliderHandle", SpriteOf(slider.handleRect), slider.name);
                Assert.AreEqual("BarFill", SpriteOf(slider.fillRect), slider.name);
                Assert.AreEqual("BarTrack", SpriteOf(slider.transform.Find("Background")), slider.name);
            }
            foreach (var toggle in new[] { menu.MusicEnabled, menu.ScreenShake })
            {
                var box = (Image)toggle.targetGraphic;
                Assert.AreEqual("ToggleBox", box.sprite.name, toggle.name);
                Assert.AreEqual(Image.Type.Sliced, box.type, toggle.name);
                Assert.AreEqual("ToggleCheck", ((Image)toggle.graphic).sprite.name, toggle.name);
            }
            var icons = new Dictionary<string, string>
            {
                { "MasterRow", "IconVolume" }, { "SfxRow", "IconVolume" }, { "MusicRow", "IconMusic" },
                { "SensitivityRow", "IconMouse" }, { "ShakeRow", "IconShake" }
            };
            foreach (var pair in icons) Assert.AreEqual(pair.Value, Child(menu, pair.Key + "/Icon").sprite.name, pair.Key);
            Assert.AreEqual("IconExit", Child(menu.Leave, "Icon").sprite.name);
            Assert.AreEqual("IconExit", Child(root.Confirm.Confirm, "Icon").sprite.name);
            Assert.AreEqual("IconSpectate", Child(root.Spectator.WatchingBlock, "Icon").sprite.name);
            Assert.NotNull(root.Eliminated.CauseIcon);

            // Panels: HudRound10 base, then edge and bevel overlays behind the content.
            foreach (var panel in new Component[] { menu, root.Eliminated.Frame, root.Spectator.Card, root.Confirm.transform.Find("Box") })
            {
                Assert.AreEqual("HudRound10", SpriteOf(panel), panel.name);
                Assert.AreEqual("AFrameEdge96", Child(panel, "ArtEdge").sprite.name, panel.name);
                Assert.AreEqual("ABevel96", Child(panel, "ArtBevel").sprite.name, panel.name);
                Assert.AreEqual(0, panel.transform.Find("ArtEdge").GetSiblingIndex(), panel.name + " edge behind content");
                Assert.AreEqual(1, panel.transform.Find("ArtBevel").GetSiblingIndex(), panel.name + " bevel behind content");
            }
            Assert.AreEqual("HudRound10", SpriteOf(root.Spectator.WatchingBlock));
            Assert.AreEqual("HudRound6", SpriteOf(root.Spectator.KeyBar));
            Assert.AreEqual("ToastBg", SpriteOf(root.Notice));
            var row = root.Spectator.Rows[0];
            Assert.AreEqual("HudRound6", SpriteOf(row));
            Assert.AreEqual("AFrameEdgeSlot128", Child(row, "ArtEdge").sprite.name);
            Assert.AreEqual("ABevelSlot128", Child(row, "ArtBevel").sprite.name);
            Assert.AreEqual("HudRound6Stroke2", Child(row, "Selected").sprite.name);
            Assert.AreEqual("BarTrack", SpriteOf(root.Spectator.CardHp.transform.Find("Track")));

            foreach (var b in new[] { menu.Resume, root.Eliminated.Spectate, root.Spectator.Home, root.Confirm.Stay })
                Assert.AreEqual("ButtonPrimary", SpriteOf(b), b.name);
            foreach (var b in new[] { menu.Defaults, menu.Leave, root.Eliminated.Home, root.Spectator.Killer, root.Killcam.Skip, root.Confirm.Confirm })
                Assert.AreEqual("ButtonSecondary", SpriteOf(b), b.name);

            var killcam = root.Killcam;
            var grid = killcam.Board.GetComponent<Image>();
            Assert.AreEqual("KillcamGrid", grid.sprite.name);
            Assert.AreEqual(Image.Type.Tiled, grid.type, "tiled, not one stretched tile");
            Assert.IsFalse(grid.raycastTarget);
            Assert.AreEqual("ZoneRing", SpriteOf(killcam.Board.Find("Zone")));
            Assert.AreEqual(KillcamClip.MaxPlayers, killcam.KillerMarks.Length);
            for (var i = 0; i < killcam.Dots.Length; i++)
            {
                Assert.AreEqual("PlayerDot", SpriteOf(killcam.Dots[i]), "dot " + i);
                Assert.AreEqual("PlayerDotOutline", Child(killcam.Dots[i], "Outline").sprite.name, "dot " + i);
                Assert.AreEqual("KillerMark", killcam.KillerMarks[i].GetComponent<Image>().sprite.name, "dot " + i);
                Assert.IsFalse(killcam.KillerMarks[i].activeSelf, "mark hidden until a killer is drawn");
                Assert.AreEqual("BarFill", SpriteOf(killcam.HpFills[i]), "dot " + i);
            }
        }

        private static Rect WorldRect(RectTransform r)
        {
            var c = new Vector3[4];
            r.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        private static Rect TextRect(TMP_Text text)
        {
            text.ForceMeshUpdate(true);
            var b = text.textBounds;
            var min = text.transform.TransformPoint(b.min);
            var max = text.transform.TransformPoint(b.max);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        // The icons sit in space the text already left free: no label is truncated and no icon covers a glyph.
        [Test]
        public void Icons_CoverNoText_NoEllipsis()
        {
            var root = Spawn();
            var checks = new List<(Image icon, TMP_Text text)>();
            foreach (var row in new[] { "MasterRow", "SfxRow", "MusicRow", "SensitivityRow", "ShakeRow" })
            {
                var r = root.Menu.transform.Find(row);
                checks.Add((r.Find("Icon").GetComponent<Image>(), r.Find("Label").GetComponent<TMP_Text>()));
            }
            foreach (var b in new[] { root.Menu.Leave, root.Confirm.Confirm })
                checks.Add((Child(b, "Icon"), b.GetComponentInChildren<TMP_Text>()));
            var watching = root.Spectator.WatchingBlock;
            checks.Add((Child(watching, "Icon"), watching.Find("Caption").GetComponent<TMP_Text>()));
            checks.Add((Child(watching, "Icon"), root.Spectator.WatchingName));

            foreach (var (icon, text) in checks)
            {
                var label = text.transform.parent.name + "/" + text.name;
                var glyphs = TextRect(text);
                Assert.IsFalse(text.isTextTruncated, label + " truncated");
                Assert.IsFalse(WorldRect(icon.rectTransform).Overlaps(glyphs), label + " under its icon");
                var box = WorldRect(text.rectTransform);
                Assert.IsTrue(glyphs.xMin >= box.xMin - 0.5f && glyphs.xMax <= box.xMax + 0.5f, label + " runs past its rect");
                Assert.AreEqual(28f, icon.rectTransform.rect.width, 0.01f, label + " icon size");
            }
        }

        [TestCase(EliminationCause.Player, "CausePlayer")]
        [TestCase(EliminationCause.Zone, "CauseZone")]
        [TestCase(EliminationCause.LeftMatch, "CauseLeft")]
        [TestCase(EliminationCause.Disconnected, "CauseDisconnect")]
        [TestCase(EliminationCause.Unknown, "CauseUnknown")]
        public void CauseIcon_FollowsCause_LeftOfTheCentredText(EliminationCause cause, string sprite)
        {
            var root = Spawn();
            var view = root.Eliminated;
            view.Show(MockEliminationSource.Sample(cause, Match));
            Assert.AreEqual(sprite, view.CauseIcon.sprite.name);
            Assert.IsTrue(view.CauseIcon.gameObject.activeInHierarchy);
            var icon = WorldRect(view.CauseIcon.rectTransform);
            var text = TextRect(view.Cause);
            Assert.LessOrEqual(icon.xMax, text.xMin, "icon left of the text");
            Assert.AreEqual(8f, text.xMin - icon.xMax, 1f, "8 px gap");
            Assert.AreEqual(icon.center.y, WorldRect(view.Cause.rectTransform).center.y, 1f, "icon on the text line");
            var frame = WorldRect(view.Frame);
            Assert.AreEqual(frame.center.x, (icon.xMin + text.xMax) / 2f, 4f, "icon + text centred in the frame");
        }

        // The killer mark is on the killer's dot only; the selected spectator row shows its outline, the others none.
        [UnityTest]
        public IEnumerator KillerMark_OnKillerOnly_SelectedRowOutlined()
        {
            _rig = new OverlayRig(killcam: true);
            _rig.Eliminate();
            Assert.AreEqual(MatchOverlayPhase.Killcam, _rig.Flow.Phase);
            for (var i = 0; i < 10; i++) yield return null;
            var killcam = _rig.Root.Killcam;
            var killer = MockEliminationSource.KillerNetId;
            Assert.IsTrue(killcam.Dots[killer].gameObject.activeSelf, "killer drawn");
            for (var i = 0; i < killcam.KillerMarks.Length; i++)
                Assert.AreEqual(i == killer, killcam.KillerMarks[i].activeSelf, "mark on dot " + i);

            _rig.Flow.SkipKillcam();
            Assert.IsTrue(_rig.Flow.Spectate());
            yield return null;
            var selected = 0;
            foreach (var row in _rig.Root.Spectator.Rows)
            {
                if (!row.gameObject.activeSelf) continue;
                var outline = row.transform.Find("Selected").GetComponent<Image>();
                Assert.AreEqual(row.Selected, outline.enabled, "row " + row.NetId);
                if (row.Selected) selected++;
            }
            Assert.AreEqual(1, selected);
        }
    }
}
#endif
