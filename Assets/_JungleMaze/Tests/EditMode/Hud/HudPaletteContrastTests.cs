using JungleMaze.Presentation.UI.Hud;
using NUnit.Framework;
using UnityEngine;

namespace JungleMaze.Tests.Hud
{
    /// <summary>AES-02: every (text, background) pair the UI declares reaches WCAG 4.5:1.</summary>
    public sealed class HudPaletteContrastTests
    {
        private static readonly HudPalette P = HudPalette.Default;
        private static readonly Color ButtonPrimary = new Color32(0x2E, 0x72, 0x63, 255);

        private static float Lin(float c) => c <= 0.03928f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        private static float Lum(Color c) => 0.2126f * Lin(c.r) + 0.7152f * Lin(c.g) + 0.0722f * Lin(c.b);
        private static Color Over(Color f, Color b) => new Color(f.r * f.a + b.r * (1 - f.a), f.g * f.a + b.g * (1 - f.a), f.b * f.a + b.b * (1 - f.a), 1f);

        private static float Ratio(Color a, Color b)
        {
            var x = Lum(a);
            var y = Lum(b);
            return (Mathf.Max(x, y) + 0.05f) / (Mathf.Min(x, y) + 0.05f);
        }

        [Test]
        public void EveryTextOnSurfacePairIsAtLeast4_5()
        {
            var surface = Over(P.Surface, P.Raised);
            foreach (var (name, color) in new[]
            {
                ("Text", P.Text), ("Muted", P.Muted), ("Faint", P.Faint), ("Green", P.Green), ("Amber", P.Amber),
                ("Yellow", P.Yellow), ("RedText", P.RedText), ("Blue", P.Blue)
            })
                Assert.GreaterOrEqual(Ratio(color, surface), 4.5f, name + " on Surface");
        }

        [Test]
        public void ButtonLabelPairsAreAtLeast4_5()
        {
            Assert.GreaterOrEqual(Ratio(P.Text, ButtonPrimary), 4.5f, "Text on primary button");
            Assert.GreaterOrEqual(Ratio(P.Text, Over(P.Surface, P.Raised)), 4.5f, "Text on secondary button");
            Assert.GreaterOrEqual(Ratio(P.RedText, new Color32(0x2E, 0x1A, 0x1C, 255)), 4.5f, "RedText on danger button");
        }
    }
}
