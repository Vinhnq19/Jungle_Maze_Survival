namespace JungleMaze.Domain.World
{
    /// <summary>
    /// Pure coordinate math for the match map (ENV-01): a square grid of <c>mapSizeCells</c> x
    /// <c>mapSizeCells</c>, cell (0,0) to (mapSizeCells,mapSizeCells), where 1 Unity world unit == 1 cell
    /// (see MatchConfigData.MapSizeCells). cellSizePixels is a sprite/PPU concern only and plays no part here.
    /// </summary>
    public static class GridBounds
    {
        public static bool Contains(float worldX, float worldY, int mapSizeCells) =>
            worldX >= 0f && worldX <= mapSizeCells && worldY >= 0f && worldY <= mapSizeCells;

        public static (float X, float Y) ClampToBounds(float worldX, float worldY, int mapSizeCells)
        {
            var x = worldX < 0f ? 0f : worldX > mapSizeCells ? mapSizeCells : worldX;
            var y = worldY < 0f ? 0f : worldY > mapSizeCells ? mapSizeCells : worldY;
            return (x, y);
        }
    }
}
