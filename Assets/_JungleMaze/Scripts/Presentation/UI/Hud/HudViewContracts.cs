using UnityEngine;

namespace JungleMaze.Presentation.UI.Hud
{
    /// <summary>Visual state of a quick-bar slot (Figma component Slot 39:139).</summary>
    public enum SlotVisual
    {
        Default = 0,
        Selected = 1,
        Empty = 2
    }

    /// <summary>Text color role; the view maps it to the style-guide palette.</summary>
    public enum TextTone
    {
        /// <summary>Primary text color.</summary>
        Normal = 0,
        /// <summary>Faint text: nothing worn / effect not running.</summary>
        Faint = 1,
        /// <summary>Adrenaline yellow while the effect runs.</summary>
        Highlight = 2
    }

    /// <summary>Rows of the Vitals block, top to bottom under the HP bar.</summary>
    public enum VitalsRow
    {
        Armor = 0,
        Helmet = 1,
        Adrenaline = 2
    }

    /// <summary>Vitals block (Figma 41:93): HP number + bar with damage trail, then armor, helmet, adrenaline rows.</summary>
    public interface IVitalsView
    {
        void SetHpNumber(char[] text, int length, bool low);
        void SetHpMax(char[] text, int length);
        /// <summary>Fill and trail are 0..1 of the bar width; trail &gt;= fill.</summary>
        void SetHpBar(float fill, float trail);
        void SetRowLabel(VitalsRow row, string label, TextTone tone);
        /// <summary>A length of 0 hides the number.</summary>
        void SetRowValue(VitalsRow row, char[] text, int length, TextTone tone);
        void SetRowFill(VitalsRow row, float fill);
    }

    /// <summary>Quick bar (Figma 41:115): held-weapon strip and 4 + 4 slots.</summary>
    public interface IQuickbarView
    {
        void SetWeaponName(string name);
        void SetAmmo(bool visible, char[] magazine, int magazineLength, char[] reserve, int reserveLength);
        void SetReloadHint(bool visible);
        /// <summary>Slot 0..7. <paramref name="countLength"/> 0 hides the "×n" label.</summary>
        void SetSlot(int index, string itemId, string label, char[] count, int countLength, SlotVisual visual);
    }

    /// <summary>Minimap (Figma 41:72). Positions are pixels from the bottom-left corner of the map area.</summary>
    public interface IMinimapView
    {
        float SizePixels { get; }
        void SetMarker(bool visible, float x, float y, float headingDegrees);
    }

    /// <summary>Match status (Figma 41:86): zone countdown and players alive.</summary>
    public interface IMatchStatusView
    {
        void SetZoneCountdown(char[] text, int length);
        void SetAlive(char[] text, int length);
    }

    /// <summary>Item icons for quick-bar slots. ART-04 registers a real provider; null keeps the placeholder.</summary>
    public interface IItemIconProvider
    {
        Sprite GetIcon(string itemId);
    }

    /// <summary>No icons yet (ART-04 pending): slots keep their placeholder square.</summary>
    public sealed class NullItemIconProvider : IItemIconProvider
    {
        public Sprite GetIcon(string itemId) => null;
    }
}
