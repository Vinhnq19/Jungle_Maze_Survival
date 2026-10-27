#if UNITY_EDITOR
using System.Collections.Generic;
using JungleMaze.Presentation.UI.MatchOverlay;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JungleMaze.Tests.PlayMode.MatchUi
{
    /// <summary>
    /// AC44–AC47 on the 1280×720 reference canvas. The prefab's canvas is switched to world space at 1 unit = 1 canvas
    /// pixel, so rects are measured independent of the Game view size; values are top-left based like Figma.
    /// Tolerance 20 px (spec H: hand-drawn Figma coordinates).
    /// </summary>
    public sealed class MatchOverlayLayoutTests
    {
        private const float Tolerance = 20f;
        private MatchOverlayRoot _root;

        [SetUp]
        public void SetUp() => _root = Spawn();

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root.gameObject);
        }

        private static MatchOverlayRoot Spawn()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(OverlayRig.PrefabPath)).GetComponent<MatchOverlayRoot>();
            var canvas = root.Canvas;
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(1280, 720);
            rt.position = Vector3.zero;
            rt.localScale = Vector3.one;
            Canvas.ForceUpdateCanvases();
            return root;
        }

        /// <summary>Rect in canvas pixels, origin top-left, y down.</summary>
        private static Rect Measure(RectTransform r)
        {
            var c = new Vector3[4];
            r.GetWorldCorners(c);
            return new Rect(c[0].x + 640f, 360f - c[1].y, c[2].x - c[0].x, c[1].y - c[0].y);
        }

        private static void Near(RectTransform r, float x, float y, float w, float h, string what)
        {
            var m = Measure(r);
            Assert.AreEqual(x, m.x, Tolerance, what + " x");
            Assert.AreEqual(y, m.y, Tolerance, what + " y");
            Assert.AreEqual(w, m.width, Tolerance, what + " w");
            Assert.AreEqual(h, m.height, Tolerance, what + " h");
        }

        private static string Codes(uint[] missing) =>
            missing == null ? "" : string.Join(" ", System.Array.ConvertAll(missing, c => "U+" + c.ToString("X4")));

        private static IEnumerable<T> All<T>(Component root) where T : Component => root.GetComponentsInChildren<T>(true);

        // AC44
        [Test]
        public void EscPanel()
        {
            var panel = (RectTransform)_root.Menu.transform;
            var m = Measure(panel);
            Assert.AreEqual(560f, m.width, Tolerance);
            Assert.AreEqual(640f, m.center.x, 1f, "centred horizontally");
            Assert.AreEqual(360f, m.center.y, 1f, "centred vertically");
            Assert.LessOrEqual(m.height, 720f * 0.6f, "≤ 60 % of the screen height");
            Assert.GreaterOrEqual(m.xMin, 0f);
            Assert.LessOrEqual(m.yMax, 720f);
            foreach (var slider in new[] { _root.Menu.Master, _root.Menu.Sfx, _root.Menu.Music, _root.Menu.Sensitivity })
                Assert.GreaterOrEqual(Measure((RectTransform)slider.transform.parent).height, 44f, slider.name + " row");
            Assert.GreaterOrEqual(Measure((RectTransform)_root.Menu.ScreenShake.transform.parent).height, 44f);
            foreach (var button in All<Button>(_root.Menu))
                Assert.GreaterOrEqual(Measure((RectTransform)button.transform).height, 44f, button.name);
            foreach (var text in All<TMP_Text>(_root.Menu))
                Assert.GreaterOrEqual(text.fontSize, 13f, text.name);
            foreach (var text in All<TMP_Text>(_root))
                StringAssert.Contains("Hud", text.font.name, "HUD fonts (Be Vietnam Pro / Pixelify Sans) on " + text.name);
        }

        // TR9: every character the overlay prints exists in its own HUD font, so TMP never falls back to (and writes
        // glyphs into) the JSurviv LiberationSans fallback asset.
        [Test]
        public void AllText_IsInTheHudFonts()
        {
            var textFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_JungleMaze/Art/Fonts/TMP/HudText SDF.asset");
            var runtime = new[]
            {
                JungleMaze.App.MatchUi.EliminationText.Title, "Bị Hào hạ bằng Nỏ · cách 6 ô", "Bị bo thu hẹp loại", "Bạn đã rời trận",
                "Mất kết nối quá lâu", "Không rõ nguyên nhân", "Hạ gục: 2", "Sống sót: 05:12", "#4 / 10",
                "Không còn người chơi nào để theo dõi", "Trận đã kết thúc", "Lan đã bị loại", "Còn sống 9/10",
                "Killcam không khả dụng: dữ liệu chưa đủ 5 giây, chờ dữ liệu quá lâu, máy chủ chưa có dữ liệu",
                "dữ liệu không thuộc trận này, dữ liệu không phải của bạn, không rõ kẻ hạ, trận đã kết thúc",
                "Killcam · góc nhìn của Thảo Ngọc Tùng Khoa Minh Vũ", "Killcam 3,2 s", MatchOverlayPresenter.MusicOffText,
                MatchOverlayPresenter.RetryText, "Người chơi 10", "100%", "1,5", "80/100"
            };
            foreach (var text in All<TMP_Text>(_root))
            {
                Assert.IsTrue(text.font.HasCharacters(text.text, out var missing, false, false),
                    text.name + " misses " + Codes(missing));
            }
            foreach (var line in runtime)
                Assert.IsTrue(textFont.HasCharacters(line, out var missing, false, false),
                    line + " misses " + Codes(missing));
        }

        // AC45
        [Test]
        public void EliminatedScreen()
        {
            Near(_root.Eliminated.Frame, 320, 141, 640, 417, "frame");
            Near((RectTransform)_root.Eliminated.Home.transform, 461, 508, 126, 50, "Về menu");
            Near((RectTransform)_root.Eliminated.Spectate.transform, 599, 508, 220, 48, "Theo dõi người khác");
        }

        // AC46
        [Test]
        public void Spectator()
        {
            var v = _root.Spectator;
            Near(v.WatchingBlock, 430, 24, 420, 71, "Đang theo dõi");
            var keys = Measure(v.KeyBar);
            Assert.AreEqual(440f, keys.x, Tolerance);
            Assert.AreEqual(640f, keys.y, Tolerance);
            Assert.AreEqual(44f, keys.height, Tolerance);
            Near((RectTransform)v.Home.transform, 1114, 640, 100, 47, "Về sảnh");
            Near(v.Card, 24, 560, 300, 90, "thẻ mục tiêu");
            Assert.AreEqual(9, v.Rows.Length);
            for (var i = 0; i < v.Rows.Length; i++)
                Near((RectTransform)v.Rows[i].transform, 24, 130 + i * 38, 160, 32, "row " + (i + 1));
            // The killfeed area (24,24) 171×90 stays empty.
            var killfeed = new Rect(24, 24, 171, 90);
            foreach (var g in All<Graphic>(v))
                Assert.IsFalse(killfeed.Overlaps(Measure(g.rectTransform)), g.name + " in the killfeed area");
        }

        // AC47: swapping colors, sprites and fonts on the prefab copy changes no measured rect.
        [Test]
        public void ArtSwapNeedsPrefabOnly()
        {
            var watched = new List<RectTransform>
            {
                (RectTransform)_root.Menu.transform, _root.Eliminated.Frame, (RectTransform)_root.Eliminated.Home.transform,
                (RectTransform)_root.Eliminated.Spectate.transform, _root.Spectator.WatchingBlock, _root.Spectator.KeyBar,
                (RectTransform)_root.Spectator.Home.transform, _root.Spectator.Card
            };
            var before = new List<Rect>();
            foreach (var r in watched) before.Add(Measure(r));

            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            // Swap to the other HUD font that covers every glyph the overlay prints (AllText_IsInTheHudFonts), so the
            // test never makes TMP write glyphs into the JSurviv fallback asset.
            var otherFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_JungleMaze/Art/Fonts/TMP/HudText SDF.asset");
            foreach (var image in All<Image>(_root)) { image.sprite = knob; image.color = Color.magenta; }
            foreach (var text in All<TMP_Text>(_root)) { if (text.font != otherFont) text.font = otherFont; text.fontSize += 2; text.color = Color.cyan; }
            Canvas.ForceUpdateCanvases();

            for (var i = 0; i < watched.Count; i++)
            {
                var after = Measure(watched[i]);
                Assert.AreEqual(before[i].position, after.position, watched[i].name);
                Assert.AreEqual(before[i].size, after.size, watched[i].name);
            }
        }
    }
}
#endif
