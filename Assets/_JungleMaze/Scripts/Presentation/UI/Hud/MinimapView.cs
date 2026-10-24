using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Hud
{
    /// <summary>
    /// Minimap (Figma 41:72): frame, faint grid and the "you are here" marker with a heading needle. Lives under its
    /// own Canvas so the marker moving every frame does not rebuild the rest of the HUD.
    /// </summary>
    public sealed class MinimapView : MonoBehaviour, IMinimapView
    {
        [SerializeField] private float sizePixels = 200f;
        [SerializeField] private Graphic background;
        [SerializeField] private Graphic[] gridLines;
        [SerializeField] private RectTransform marker;
        [SerializeField] private RectTransform heading;
        [SerializeField] private Graphic markerGraphic;
        [SerializeField] private Graphic headingGraphic;

        public float SizePixels => sizePixels;
        public RectTransform Marker => marker;
        public RectTransform Heading => heading;

        public void ApplyPalette(HudPalette palette)
        {
            if (background != null) background.color = palette.Surface;
            if (gridLines != null)
                foreach (var line in gridLines)
                    if (line != null) line.color = palette.Grid;
            if (markerGraphic != null) markerGraphic.color = palette.Amber;
            if (headingGraphic != null) headingGraphic.color = palette.Amber;
        }

        public void SetMarker(bool visible, float x, float y, float headingDegrees)
        {
            if (marker == null) return;
            if (marker.gameObject.activeSelf != visible) marker.gameObject.SetActive(visible);
            if (!visible) return;
            marker.anchoredPosition = new Vector2(x, y);
            if (heading != null) heading.localEulerAngles = new Vector3(0f, 0f, headingDegrees);
        }
    }
}
