using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Hud
{
    /// <summary>
    /// One quick-bar slot (Figma component Slot 39:139, 64×64): key number, "×n", icon square and item name, with
    /// Default / Selected / Empty visuals. Sprites and colors come from the prefab and <see cref="HudPalette"/>,
    /// so art can be swapped without code changes. The icon keeps a placeholder square until ART-04 provides sprites.
    /// </summary>
    public sealed class QuickbarSlotView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image border;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text keyLabel;
        [SerializeField] private TMP_Text countLabel;
        [SerializeField] private TMP_Text nameLabel;
        [Header("Border sprites per state")]
        [SerializeField] private Sprite defaultBorder;
        [SerializeField] private Sprite selectedBorder;
        [SerializeField] private Sprite emptyBorder;

        private HudPalette _palette = HudPalette.Default;
        private Sprite _placeholderIcon;
        private bool _hasPlaceholder;

        public SlotVisual Visual { get; private set; } = SlotVisual.Empty;
        public Image Background => background;
        public Image Border => border;
        public TMP_Text KeyLabel => keyLabel;
        public TMP_Text CountLabel => countLabel;
        public TMP_Text NameLabel => nameLabel;

        public void ApplyPalette(HudPalette palette)
        {
            _palette = palette;
            if (keyLabel != null) keyLabel.color = palette.Faint;
            if (countLabel != null) countLabel.color = palette.Text;
            SetVisual(Visual);
        }

        /// <summary>Key number shown in the top-left corner (1..8), set once.</summary>
        public void SetKey(string key)
        {
            if (keyLabel != null) keyLabel.text = key;
        }

        /// <summary>Shows an item. <paramref name="iconSprite"/> null keeps the placeholder square.</summary>
        public void Show(string label, char[] count, int countLength, SlotVisual visual, Sprite iconSprite)
        {
            if (nameLabel != null && !string.Equals(nameLabel.text, label)) nameLabel.text = label;
            if (countLabel != null)
            {
                var showCount = countLength > 0 && visual != SlotVisual.Empty;
                if (countLabel.enabled != showCount) countLabel.enabled = showCount;
                if (showCount) countLabel.SetCharArray(count, 0, countLength);
            }
            if (icon != null)
            {
                if (!_hasPlaceholder) { _placeholderIcon = icon.sprite; _hasPlaceholder = true; }
                icon.sprite = iconSprite != null ? iconSprite : _placeholderIcon;
            }
            SetVisual(visual);
        }

        public void SetVisual(SlotVisual visual)
        {
            Visual = visual;
            var empty = visual == SlotVisual.Empty;
            if (background != null)
            {
                background.enabled = !empty;
                background.color = visual == SlotVisual.Selected ? _palette.SelectedFill : _palette.Surface;
            }
            if (border != null)
            {
                border.sprite = visual == SlotVisual.Selected ? selectedBorder : empty ? emptyBorder : defaultBorder;
                border.color = visual == SlotVisual.Selected ? _palette.Amber : _palette.Line;
            }
            if (icon != null)
            {
                var placeholder = icon.sprite == _placeholderIcon;
                var color = placeholder ? _palette.IconPlaceholder : Color.white;
                if (empty) color.a *= 0.35f;
                icon.color = color;
            }
            if (nameLabel != null)
            {
                nameLabel.enabled = !empty;
                nameLabel.color = _palette.Text;
            }
            if (countLabel != null && empty) countLabel.enabled = false;
        }
    }
}
