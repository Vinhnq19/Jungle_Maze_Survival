using JungleMaze.App.MatchUi;
using JungleMaze.Domain.MatchUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.MatchOverlay
{
    /// <summary>
    /// Eliminated screen (Figma v2 43:283): frame (320,141) 640×417, "Về menu" and "Theo dõi người khác". A line whose
    /// data is missing is switched off rather than printed as 0/null (AC20); text comes from <see cref="EliminationText"/>.
    /// </summary>
    public sealed class EliminatedView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform frame;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text rank;
        [SerializeField] private TMP_Text cause;
        [SerializeField] private TMP_Text kills;
        [SerializeField] private TMP_Text survival;
        [Tooltip("Why spectating is not possible (AC21).")]
        [SerializeField] private TMP_Text blockedReason;
        [SerializeField] private Button home;
        [SerializeField] private Button spectate;
        [Tooltip("Icon left of the cause text; both sit in a centred Horizontal Layout Group (UX review).")]
        [SerializeField] private Image causeIcon;
        [Tooltip("Sprite per EliminationCause: Unknown, Player, Zone, LeftMatch, Disconnected.")]
        [SerializeField] private Sprite[] causeIcons = new Sprite[5];

        public CanvasGroup Group => group;
        public RectTransform Frame => frame;
        public TMP_Text Rank => rank;
        public TMP_Text Cause => cause;
        public TMP_Text Kills => kills;
        public TMP_Text Survival => survival;
        public TMP_Text BlockedReason => blockedReason;
        public Button Home => home;
        public Button Spectate => spectate;
        public Image CauseIcon => causeIcon;

        public void Show(in EliminationInfo info)
        {
            if (title != null) title.text = EliminationText.Title;
            Line(rank, EliminationText.Rank(info));
            Line(cause, EliminationText.Cause(info));
            PlaceCauseIcon(info.Cause);
            Line(kills, EliminationText.Kills(info));
            Line(survival, EliminationText.Survival(info));
        }

        public void SetSpectateAvailable(bool available, string reason)
        {
            spectate.interactable = available;
            Line(blockedReason, available ? null : reason);
        }

        // Once per elimination: picks the cause sprite; the layout group places icon + text, rebuilt now so it is
        // right on the first frame.
        private void PlaceCauseIcon(EliminationCause value)
        {
            if (causeIcon == null || cause == null || causeIcons == null || causeIcons.Length == 0) return;
            var index = (int)value;
            causeIcon.sprite = index < causeIcons.Length && causeIcons[index] != null ? causeIcons[index] : causeIcons[0];
            causeIcon.gameObject.SetActive(cause.gameObject.activeSelf);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)cause.transform.parent);
        }

        private static void Line(TMP_Text text, string value)
        {
            if (text == null) return;
            var show = value != null;
            if (show) text.text = value;
            if (text.gameObject.activeSelf != show) text.gameObject.SetActive(show);
        }
    }
}
