using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Hud
{
    /// <summary>
    /// One HUD bar: Track (background), optional Trail, Fill. Values move the right anchor of Fill/Trail
    /// (0..1 of the bar width) and are written only when they change by more than 0.0001.
    /// </summary>
    public sealed class HudBarView : MonoBehaviour
    {
        private const float Epsilon = 0.0001f;

        [SerializeField] private Image track;
        [SerializeField] private Image trail;
        [SerializeField] private Image fill;

        private float _fill = -1f, _trail = -1f;

        public Image FillImage => fill;
        public Image TrailImage => trail;
        public Image TrackImage => track;
        public float FillValue => _fill;
        public float TrailValue => _trail;

        public void SetFill(float value)
        {
            if (fill == null || Mathf.Abs(value - _fill) <= Epsilon) return;
            _fill = value;
            SetRight(fill.rectTransform, value);
        }

        public void SetTrail(float value)
        {
            if (trail == null || Mathf.Abs(value - _trail) <= Epsilon) return;
            _trail = value;
            SetRight(trail.rectTransform, value);
        }

        public void SetColors(Color fillColor, Color trackColor, Color trailColor)
        {
            if (fill != null) fill.color = fillColor;
            if (track != null) track.color = trackColor;
            if (trail != null) trail.color = trailColor;
        }

        private static void SetRight(RectTransform rect, float value)
        {
            var max = rect.anchorMax;
            max.x = Mathf.Clamp01(value);
            rect.anchorMax = max;
        }
    }
}
