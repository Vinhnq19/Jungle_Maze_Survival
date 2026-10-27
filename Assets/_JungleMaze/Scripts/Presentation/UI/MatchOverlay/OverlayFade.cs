using UnityEngine;

namespace JungleMaze.Presentation.UI.MatchOverlay
{
    /// <summary>
    /// Shows/hides one pre-built panel by its CanvasGroup only (no SetActive, no Instantiate: AC1, AC3). Showing makes
    /// it clickable at once and fades alpha up; hiding stops clicks at once and fades out. Driven by unscaled time.
    /// </summary>
    public sealed class OverlayFade
    {
        private readonly CanvasGroup _group;
        private bool _visible;

        public OverlayFade(CanvasGroup group)
        {
            _group = group;
            SetInstant(false);
        }

        public bool Visible => _visible;
        public float Alpha => _group != null ? _group.alpha : 0f;
        public CanvasGroup Group => _group;

        public void Set(bool visible)
        {
            _visible = visible;
            if (_group == null) return;
            _group.interactable = visible;
            if (visible) _group.blocksRaycasts = true;
        }

        public void SetInstant(bool visible)
        {
            Set(visible);
            if (_group == null) return;
            _group.alpha = visible ? 1f : 0f;
            _group.blocksRaycasts = visible;
        }

        public void Step(float unscaledDelta, float fadeSeconds)
        {
            if (_group == null) return;
            var target = _visible ? 1f : 0f;
            if (Mathf.Approximately(_group.alpha, target)) { _group.alpha = target; FinishHide(); return; }
            var step = fadeSeconds <= 0f ? 1f : unscaledDelta / fadeSeconds;
            _group.alpha = Mathf.MoveTowards(_group.alpha, target, step);
            FinishHide();
        }

        private void FinishHide()
        {
            if (!_visible && _group.alpha <= 0f) _group.blocksRaycasts = false;
        }
    }
}
