using UnityEngine;

namespace JungleMaze.Presentation.World
{
    /// <summary>Shared physics query for projectile sweeps and future LOS/FOW. No separate cover geometry.</summary>
    public static class CoverQuery
    {
        private static readonly int HardCoverMask = LayerMask.GetMask("HardCover");

        public static RaycastHit2D Sweep(Vector2 start, Vector2 direction, float distance, float radius = 0f)
        {
            // Cache the params-array lookup: queries in the projectile hot path do not allocate it per step.
            return radius > 0f ? Physics2D.CircleCast(start, radius, direction, distance, HardCoverMask)
                : Physics2D.Raycast(start, direction, distance, HardCoverMask);
        }
    }
}
