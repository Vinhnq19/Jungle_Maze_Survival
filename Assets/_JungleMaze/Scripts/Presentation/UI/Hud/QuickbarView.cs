using JungleMaze.Domain.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Hud
{
    /// <summary>
    /// Quick bar (Figma 41:115): held-weapon strip (name, ammo, "R Nạp" hint; auto-sized and centred by its layout
    /// group) and the 4 + 4 slots.
    /// </summary>
    public sealed class QuickbarView : MonoBehaviour, IQuickbarView
    {
        private static readonly string[] KeyNames = { "1", "2", "3", "4", "5", "6", "7", "8" };

        [SerializeField] private RectTransform weaponStrip;
        [SerializeField] private Graphic weaponStripBackground;
        [SerializeField] private TMP_Text weaponName;
        [SerializeField] private GameObject ammo;
        [SerializeField] private TMP_Text magazine;
        [SerializeField] private TMP_Text reserve;
        [SerializeField] private GameObject reloadHint;
        [SerializeField] private TMP_Text reloadLabel;
        [SerializeField] private TMP_Text weaponGroupLabel;
        [SerializeField] private TMP_Text consumableGroupLabel;
        [SerializeField] private QuickbarSlotView[] slots = new QuickbarSlotView[InventorySlots.TotalSlotCount];

        private IItemIconProvider _icons;

        public RectTransform WeaponStrip => weaponStrip;
        public TMP_Text WeaponName => weaponName;
        public GameObject Ammo => ammo;
        public GameObject ReloadHint => reloadHint;
        public QuickbarSlotView GetSlotView(int index) => slots != null && index >= 0 && index < slots.Length ? slots[index] : null;
        public int SlotViewCount => slots != null ? slots.Length : 0;

        private void Awake()
        {
            if (slots == null || slots.Length != InventorySlots.TotalSlotCount)
                Debug.LogWarning($"[HUD] Quick bar has {SlotViewCount} slot views, expected {InventorySlots.TotalSlotCount}.");
            for (var i = 0; i < SlotViewCount && i < KeyNames.Length; i++)
                if (slots[i] != null) slots[i].SetKey(KeyNames[i]);
        }

        public void SetIconProvider(IItemIconProvider icons) => _icons = icons;

        public void ApplyPalette(HudPalette palette)
        {
            if (weaponStripBackground != null) weaponStripBackground.color = palette.Surface;
            if (weaponName != null) weaponName.color = palette.Text;
            if (magazine != null) magazine.color = palette.Text;
            if (reserve != null) reserve.color = palette.Muted;
            if (reloadLabel != null) reloadLabel.color = palette.Text;
            if (weaponGroupLabel != null) weaponGroupLabel.color = palette.Muted;
            if (consumableGroupLabel != null) consumableGroupLabel.color = palette.Muted;
            for (var i = 0; i < SlotViewCount; i++)
                if (slots[i] != null) slots[i].ApplyPalette(palette);
        }

        public void SetWeaponName(string name)
        {
            if (weaponName == null || string.Equals(weaponName.text, name)) return;
            weaponName.text = name;
            MarkStripDirty();
        }

        public void SetAmmo(bool visible, char[] magazineText, int magazineLength, char[] reserveText, int reserveLength)
        {
            if (ammo != null && ammo.activeSelf != visible) ammo.SetActive(visible);
            if (visible)
            {
                if (magazine != null) magazine.SetCharArray(magazineText, 0, magazineLength);
                if (reserve != null) reserve.SetCharArray(reserveText, 0, reserveLength);
            }
            MarkStripDirty();
        }

        public void SetReloadHint(bool visible)
        {
            if (reloadHint == null || reloadHint.activeSelf == visible) return;
            reloadHint.SetActive(visible);
            MarkStripDirty();
        }

        public void SetSlot(int index, string itemId, string label, char[] count, int countLength, SlotVisual visual)
        {
            var view = GetSlotView(index);
            if (view == null) return;
            var sprite = _icons != null && itemId != null ? _icons.GetIcon(itemId) : null;
            view.Show(label, count, countLength, visual, sprite);
        }

        private void MarkStripDirty()
        {
            if (weaponStrip != null) LayoutRebuilder.MarkLayoutForRebuild(weaponStrip);
        }
    }
}
