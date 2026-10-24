using System;
using UnityEngine;

namespace JungleMaze.Presentation.UI.Hud
{
    /// <summary>
    /// HUD colors (Figma style guide 39:3 v0.1), serialized on the HUD prefab. One color, one meaning: red = HP,
    /// blue = armor/helmet, yellow = adrenaline, amber = selected, purple = zone. Art can be replaced later by editing
    /// the prefab only; code never hard-codes these values outside <see cref="Default"/>.
    /// </summary>
    [Serializable]
    public struct HudPalette
    {
        public Color Red;
        public Color Blue;
        public Color Yellow;
        public Color Amber;
        public Color Purple;
        public Color Text;
        public Color Muted;
        public Color Faint;
        /// <summary>Block background (surface at 72%).</summary>
        public Color Surface;
        /// <summary>Raised surface (key caps).</summary>
        public Color Raised;
        public Color Line;
        /// <summary>Empty part of a bar (proposal T5: line at 70%).</summary>
        public Color Track;
        /// <summary>Damage trail (proposal T5: red at 35%).</summary>
        public Color Trail;
        /// <summary>Selected slot background (proposal T5: amber at 25%).</summary>
        public Color SelectedFill;
        /// <summary>Minimap grid lines (proposal T5: line at 60%).</summary>
        public Color Grid;
        /// <summary>Icon placeholder square until ART-04 provides sprites.</summary>
        public Color IconPlaceholder;
        public Color Green;
        public Color HitMarker;
        public Color DangerFill;

        /// <summary>Style guide v2 hex values plus the T5 proposals.</summary>
        public static HudPalette Default => new HudPalette
        {
            Red = Hex(0xE5, 0x48, 0x4D),
            Blue = Hex(0x5B, 0xB8, 0xE8),
            Yellow = Hex(0xF5, 0xC5, 0x42),
            Amber = Hex(0xF2, 0xA3, 0x3A),
            Purple = Hex(0xA2, 0x74, 0xFF),
            Text = Hex(0xF3, 0xEE, 0xDB),
            Muted = Hex(0xA7, 0xB5, 0xA3),
            Faint = Hex(0x6E, 0x80, 0x70),
            Surface = Hex(0x15, 0x23, 0x1B, 0.72f),
            Raised = Hex(0x1F, 0x33, 0x27),
            Line = Hex(0x34, 0x50, 0x3F),
            Track = Hex(0x34, 0x50, 0x3F, 0.70f),
            Trail = Hex(0xE5, 0x48, 0x4D, 0.35f),
            SelectedFill = Hex(0xF2, 0xA3, 0x3A, 0.25f),
            Grid = Hex(0x34, 0x50, 0x3F, 0.60f),
            IconPlaceholder = Hex(0x6E, 0x80, 0x70),
            Green = Hex(0x5D, 0xD3, 0x9E),
            HitMarker = Color.white,
            DangerFill = Hex(0xE5, 0x48, 0x4D, 0.15f)
        };

        private static Color Hex(byte r, byte g, byte b, float alpha = 1f) => new Color(r / 255f, g / 255f, b / 255f, alpha);
    }
}
