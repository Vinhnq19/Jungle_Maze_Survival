using TMPro;
using UnityEngine;

namespace JungleMaze.Presentation.UI.MatchOverlay
{
    /// <summary>One short status line ("Lan đã bị loại", "Killcam không khả dụng: …"); lifetime is the flow's.</summary>
    public sealed class OverlayNoticeView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text text;

        public CanvasGroup Group => group;
        public TMP_Text Text => text;
    }
}
