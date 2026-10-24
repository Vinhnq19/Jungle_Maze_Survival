namespace JungleMaze.App.Hud
{
    /// <summary>
    /// Maps world positions (1 unit = 1 cell, map spans [0, mapSize] on both axes) onto a square minimap of
    /// <see cref="SizePixels"/> with the origin at its bottom-left corner, north up, no rotation (AC15).
    /// </summary>
    public readonly struct MinimapProjection
    {
        public readonly int MapSizeCells;
        public readonly float SizePixels;

        public MinimapProjection(int mapSizeCells, float sizePixels)
        {
            MapSizeCells = mapSizeCells > 0 ? mapSizeCells : 1;
            SizePixels = sizePixels;
        }

        public float PixelsPerCell => SizePixels / MapSizeCells;

        /// <summary>World position to minimap pixels from the bottom-left corner, clamped inside the map.</summary>
        public void Project(float worldX, float worldY, out float pixelX, out float pixelY)
        {
            pixelX = SmoothedBar.Clamp01(worldX / MapSizeCells) * SizePixels;
            pixelY = SmoothedBar.Clamp01(worldY / MapSizeCells) * SizePixels;
        }

        /// <summary>
        /// UI rotation (degrees, counter-clockwise) for a heading needle that points up at 0. Hero rotation 0 faces
        /// +X (east), so the needle is rotated by -90.
        /// </summary>
        public static float HeadingDegrees(float heroRotationDegrees)
        {
            var degrees = (heroRotationDegrees - 90f) % 360f;
            if (degrees > 180f) degrees -= 360f;
            else if (degrees <= -180f) degrees += 360f;
            return degrees;
        }
    }
}
