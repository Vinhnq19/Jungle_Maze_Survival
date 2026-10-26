using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Hud
{
    /// <summary>Vitals block (Figma 41:93): HP number + "/max", HP bar with damage trail, armor/helmet/adrenaline rows.</summary>
    public sealed class VitalsView : MonoBehaviour, IVitalsView
    {
        [SerializeField] private Graphic background;
        [SerializeField] private TMP_Text hpNumber;
        [SerializeField] private TMP_Text hpMax;
        [SerializeField] private HudBarView hpBar;
        [SerializeField] private HudGaugeRowView armorRow;
        [SerializeField] private HudGaugeRowView helmetRow;
        [SerializeField] private HudGaugeRowView adrenalineRow;

        private HudPalette _palette = HudPalette.Default;

        public TMP_Text HpNumber => hpNumber;
        public TMP_Text HpMax => hpMax;
        public HudBarView HpBar => hpBar;
        public HudGaugeRowView ArmorRow => armorRow;
        public HudGaugeRowView HelmetRow => helmetRow;
        public HudGaugeRowView AdrenalineRow => adrenalineRow;

        /// <summary>Applies static colors (background, bars, "/max").</summary>
        public void ApplyPalette(HudPalette palette)
        {
            _palette = palette;
            if (background != null) background.color = palette.Surface;
            if (hpMax != null) hpMax.color = palette.Muted;
            if (hpNumber != null) hpNumber.color = palette.Text;
            if (hpBar != null) hpBar.SetColors(palette.Red, palette.Track, palette.Trail);
            if (armorRow != null) armorRow.SetBarColors(palette.Blue, palette.Track, palette.Trail);
            if (helmetRow != null) helmetRow.SetBarColors(palette.Blue, palette.Track, palette.Trail);
            if (adrenalineRow != null) adrenalineRow.SetBarColors(palette.Yellow, palette.Track, palette.Trail);
        }

        public void SetHpNumber(char[] text, int length, bool low)
        {
            if (hpNumber == null) return;
            hpNumber.SetCharArray(text, 0, length);
            hpNumber.color = low ? _palette.Red : _palette.Text;
        }

        public void SetHpMax(char[] text, int length)
        {
            if (hpMax != null) hpMax.SetCharArray(text, 0, length);
        }

        public void SetHpBar(float fill, float trail)
        {
            if (hpBar == null) return;
            hpBar.SetFill(fill);
            hpBar.SetTrail(trail);
        }

        public void SetRowLabel(VitalsRow row, string label, TextTone tone)
        {
            var view = Row(row);
            if (view != null) view.SetLabel(label, Tone(tone));
        }

        public void SetRowValue(VitalsRow row, char[] text, int length, TextTone tone)
        {
            var view = Row(row);
            if (view != null) view.SetValue(text, length, Tone(tone));
        }

        public void SetRowFill(VitalsRow row, float fill)
        {
            var view = Row(row);
            if (view != null) view.SetFill(fill);
        }

        private HudGaugeRowView Row(VitalsRow row) =>
            row == VitalsRow.Armor ? armorRow : row == VitalsRow.Helmet ? helmetRow : adrenalineRow;

        private Color Tone(TextTone tone) =>
            tone == TextTone.Faint ? _palette.Faint : tone == TextTone.Highlight ? _palette.Yellow : _palette.Text;
    }
}
