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
        [SerializeField] private Image inUseFrame;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text keyLabel;
        [SerializeField] private TMP_Text countLabel;
        [SerializeField] private TMP_Text nameLabel;
        [Tooltip("Optional direction A edge/bevel overlays; hidden on an empty slot.")]
        [SerializeField] private HudArtFrameView frame;
        [Header("Border sprites per state")]
        [SerializeField] private Sprite defaultBorder;
        [SerializeField] private Sprite selectedBorder;
        [SerializeField] private Sprite emptyBorder;

        private HudPalette _palette = HudPalette.Default;
        private Sprite _placeholderIcon;
        private bool _hasPlaceholder;
        private Image.Type _placeholderType;
        private bool _hasItem;

        public SlotVisual Visual { get; private set; } = SlotVisual.Empty;
        /// <summary>False when the slot holds nothing (an empty slot can still be Selected).</summary>
        public bool HasItem => _hasItem;
        public Image Background => background;
        public Image Border => border;
        public TMP_Text KeyLabel => keyLabel;
        public TMP_Text CountLabel => countLabel;
        public TMP_Text NameLabel => nameLabel;
        public Image InUseFrame=>inUseFrame;
        public Image Icon => icon;
        public HudArtFrameView Frame => frame;
        public void SetInUse(bool inUse){if(inUseFrame!=null)inUseFrame.enabled=inUse;}

        public void ApplyPalette(HudPalette palette)
        {
            _palette = palette;
            if(inUseFrame!=null)inUseFrame.color=palette.Amber;
            if (frame != null) frame.ApplyPalette(palette);
            if (keyLabel != null) keyLabel.color = palette.Faint;
            if (countLabel != null) countLabel.color = palette.Text;
            SetVisual(Visual);
        }

        /// <summary>Key number shown in the top-left corner (1..8), set once.</summary>
        public void SetKey(string key)
        {
            if (keyLabel != null) keyLabel.text = key;
        }

        /// <summary>
        /// Shows a slot. <paramref name="hasItem"/> false means the slot holds nothing (it may still be Selected);
        /// <paramref name="iconSprite"/> null keeps the placeholder square.
        /// </summary>
        public void Show(string label, char[] count, int countLength, SlotVisual visual, Sprite iconSprite, bool hasItem)
        {
            _hasItem = hasItem;
            if (nameLabel != null && !string.Equals(nameLabel.text, label)) nameLabel.text = label;
            if (countLabel != null)
            {
                var showCount = countLength > 0 && hasItem && visual != SlotVisual.Empty;
                if (countLabel.enabled != showCount) countLabel.enabled = showCount;
                if (showCount) countLabel.SetCharArray(count, 0, countLength);
            }
            if (icon != null)
            {
                CapturePlaceholder();
                var sprite = iconSprite != null ? iconSprite : _placeholderIcon;
                if (icon.sprite != sprite)
                {
                    icon.sprite = sprite;
                    // Item art keeps its aspect (Simple); the placeholder square stays 9-sliced.
                    var placeholder = sprite == _placeholderIcon;
                    icon.type = placeholder ? _placeholderType : Image.Type.Simple;
                    icon.preserveAspect = !placeholder;
                }
            }
            SetVisual(visual);
        }

        public void SetVisual(SlotVisual visual)
        {
            Visual = visual;
            var empty = visual == SlotVisual.Empty;
            // A selected slot with nothing in it keeps the amber frame but shows no item: dim icon, no name.
            var noItem = empty || !_hasItem;
            if (frame != null) frame.SetVisible(!empty);
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
                CapturePlaceholder();
                var placeholder = icon.sprite == _placeholderIcon;
                var color = placeholder ? _palette.IconPlaceholder : Color.white;
                if (noItem) color.a *= 0.35f;
                icon.color = color;
            }
            if (nameLabel != null)
            {
                nameLabel.enabled = !noItem;
                nameLabel.color = _palette.Text;
            }
            if (countLabel != null && noItem) countLabel.enabled = false;
        }

        /// <summary>The sprite set on the prefab is the placeholder square; remember it before any icon replaces it.</summary>
        private void CapturePlaceholder()
        {
            if (_hasPlaceholder || icon == null) return;
            _placeholderIcon = icon.sprite;
            _placeholderType = icon.type;
            _hasPlaceholder = true;
        }
    }
}
