namespace JungleMaze.Domain.World
{
    /// <summary>
    /// The four Hard Cover obstacle types from GDD Sheet 10 (Obstacles). Hard Cover fully blocks
    /// movement, projectiles and line of sight (collision/cover only here; FOW/shader reuse the same
    /// colliders later, see ENV-01's scope boundary).
    /// </summary>
    public enum HardCoverType : byte
    {
        /// <summary>Cột đá cổ đại, 1x2 cells.</summary>
        AncientPillar = 0,
        /// <summary>Tường thành, 1x4 cells.</summary>
        CityWall = 1,
        /// <summary>Tảng đá lớn, 4x4 cells.</summary>
        BoulderRock = 2,
        /// <summary>Tượng cổ, 3x3 cells.</summary>
        AncientStatue = 3
    }

    /// <summary>Footprint size (in cells) for each <see cref="HardCoverType"/>, per GDD Sheet 10.</summary>
    public static class HardCoverSize
    {
        public static (float Width, float Height) Of(HardCoverType type)
        {
            switch (type)
            {
                case HardCoverType.AncientPillar: return (1f, 2f);
                case HardCoverType.CityWall: return (1f, 4f);
                case HardCoverType.BoulderRock: return (4f, 4f);
                case HardCoverType.AncientStatue: return (3f, 3f);
                default: return (1f, 1f);
            }
        }
    }
}
