using UnityEngine;

namespace JungleMaze.Tests.PlayMode
{
    /// <summary>
    /// Figma v2 numbers (frame 41:2, canvas 1280×720, origin top-left) used by the HUD PlayMode tests only.
    /// Source: Figma metadata taken 24/10/2026 (ticket UI-01 section 2).
    /// </summary>
    internal static class HudFigmaV2
    {
        public const float Width = 1280f;
        public const float Height = 720f;
        public const float Margin = 24f;
        /// <summary>G-11 layout tolerance.</summary>
        public const float Tolerance = 2f;

        public static readonly Rect Vitals = new Rect(24, 532, 296, 164);
        public static readonly Rect QuickBar = new Rect(358, 566, 564, 130);
        public static readonly Rect Minimap = new Rect(1056, 24, 200, 200);
        public static readonly Rect MatchStatus = new Rect(1056, 232, 200, 74);
        public static readonly Rect Killfeed = new Rect(24, 24, 171, 90);
        public static readonly Rect CenterClear = new Rect(320, 180, 640, 360);

        // Children, relative to their block's top-left corner.
        public static readonly Rect HpBar = new Rect(16, 63, 264, 12);
        public static readonly float[] RowY = { 83f, 108f, 133f };
        public static readonly Rect RowBar = new Rect(86, 5.5f, 140, 6);
        public const float WeaponStripHeight = 38f;
        public const float SlotSize = 64f;
        public const float SlotStep = 70f;
        public const float ConsumableGroupX = 290f;
        public const float SlotRowY = 46f + 20f;
    }
}
