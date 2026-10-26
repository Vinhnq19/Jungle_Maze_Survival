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
        /// <summary>Dark outer edge of direction A frames (overlay tint, ADHOC-UI-ASSETS-A).</summary>
        public Color Edge;

        /// <summary>
        /// Direction A (charcoal / jade / soft gold, Figma 51:32) on top of the UI-01 semantic colors: HP red,
        /// armor/helmet blue, adrenaline yellow and zone purple keep their meaning and values.
        /// </summary>
        public static HudPalette Default => new HudPalette
        {
            Red = Hex(0xE5, 0x48, 0x4D),
            Blue = Hex(0x5B, 0xB8, 0xE8),
            Yellow = Hex(0xF5, 0xC5, 0x42),
            Amber = Hex(0xC6, 0xAE, 0x78),
            Purple = Hex(0xA2, 0x74, 0xFF),
            Text = Hex(0xED, 0xF2, 0xEE),
            Muted = Hex(0xA6, 0xBB, 0xB7),
            Faint = Hex(0x77, 0x90, 0x8A),
            Surface = Hex(0x20, 0x2A, 0x2D, 0.92f),
            Raised = Hex(0x0C, 0x14, 0x17),
            Line = Hex(0x52, 0x6B, 0x69),
            Track = Hex(0x0C, 0x14, 0x17, 0.80f),
            Trail = Hex(0xE5, 0x48, 0x4D, 0.35f),
            SelectedFill = Hex(0x28, 0x53, 0x4F, 0.25f),
            Grid = Hex(0x52, 0x6B, 0x69, 0.60f),
            IconPlaceholder = Hex(0x77, 0x90, 0x8A),
            Green = Hex(0x63, 0xBA, 0xA5),
            HitMarker = Color.white,
            DangerFill = Hex(0xE5, 0x48, 0x4D, 0.15f),
            Edge = Hex(0x0C, 0x14, 0x17)
        };

        private static Color Hex(byte r, byte g, byte b, float alpha = 1f) => new Color(r / 255f, g / 255f, b / 255f, alpha);
    }
}
