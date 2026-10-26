using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Hud
{
    /// <summary>
    /// Direction A frame overlays on top of a block's own background Image: a dark outer edge and a lighter top-left
    /// bevel, each a separate white mask tinted from <see cref="HudPalette"/>. The base keeps its Surface tint (owned
    /// by the block's view), so no mask is tinted twice and the bevel can be lighter than the surface. Overlays
    /// stretch over the block, never raycast and never change its RectTransform.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudArtFrameView : MonoBehaviour
    {
        [SerializeField] private Image edge;
        [SerializeField] private Image bevel;

        public Image Edge => edge;
        public Image Bevel => bevel;

        /// <summary>Tints the edge with <see cref="HudPalette.Edge"/> and the bevel with <see cref="HudPalette.Line"/>.</summary>
        public void ApplyPalette(HudPalette palette)
        {
            if (edge != null) edge.color = palette.Edge;
            if (bevel != null) bevel.color = palette.Line;
        }

        /// <summary>Shows or hides both overlays (an empty quick-bar slot shows only its dashed outline).</summary>
        public void SetVisible(bool visible)
        {
            if (edge != null && edge.enabled != visible) edge.enabled = visible;
            if (bevel != null && bevel.enabled != visible) bevel.enabled = visible;
        }
    }
}
