using JungleMaze.Domain.World;
using UnityEngine;

namespace JungleMaze.Presentation.World
{
    /// <summary>
    /// Marks a GameObject as Hard Cover (ENV-01): fully blocks movement and projectiles via its own
    /// Collider2D (on the "HardCover" physics layer) and is meant to be queried by future systems (FOW,
    /// line-of-sight raycasts) that need to tell Hard Cover apart from other collidable scenery.
    /// Carries no behaviour of its own; it is data for other systems to read.
    /// </summary>
    public sealed class HardCoverTag : MonoBehaviour
    {
        [SerializeField] private HardCoverType type;

        public HardCoverType Type => type;
    }
}
