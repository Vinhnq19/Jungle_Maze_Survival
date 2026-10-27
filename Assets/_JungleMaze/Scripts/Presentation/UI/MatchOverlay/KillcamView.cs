using JungleMaze.App.MatchUi;
using JungleMaze.Domain.MatchUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.MatchOverlay
{
    /// <summary>
    /// Killcam v1 (T-5): its own flat board, not the real world, so nothing outside the replay is shown (AC34, AC41).
    /// The board centre is the killer's replay position; each player in the frame is a pre-built dot with name and
    /// HP bar, the zone a tinted circle. Only positions/HP/flags are drawn: no shots, damage or VFX (AC38).
    /// </summary>
    public sealed class KillcamView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform board;
        [SerializeField] private RectTransform zone;
        [SerializeField] private RectTransform[] dots = new RectTransform[KillcamClip.MaxPlayers];
        [SerializeField] private Image[] dotImages = new Image[KillcamClip.MaxPlayers];
        [SerializeField] private TMP_Text[] names = new TMP_Text[KillcamClip.MaxPlayers];
        [SerializeField] private RectTransform[] hpFills = new RectTransform[KillcamClip.MaxPlayers];
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text countdown;
        [SerializeField] private Button skip;
        [Tooltip("Board pixels per map cell.")]
        [SerializeField] private float pixelsPerCell = 24f;
        [SerializeField] private Color killerColor = new Color(0.898f, 0.282f, 0.302f);
        [SerializeField] private Color victimColor = new Color(0.776f, 0.682f, 0.471f);
        [SerializeField] private Color otherColor = new Color(0.929f, 0.949f, 0.933f);

        private int _shownTenths = -1;

        public CanvasGroup Group => group;
        public RectTransform Board => board;
        public RectTransform[] Dots => dots;
        public TMP_Text Title => title;
        public TMP_Text Countdown => countdown;
        public Button Skip => skip;
        public float PixelsPerCell => pixelsPerCell;

        public void SetTitle(string killerName)
        {
            title.text = "Killcam · góc nhìn của " + (killerName ?? "kẻ hạ bạn");
            _shownTenths = -1;
        }

        public void SetCountdown(float remaining)
        {
            var tenths = Mathf.CeilToInt(Mathf.Max(0f, remaining) * 10f - 1e-4f);
            if (tenths == _shownTenths) return;
            _shownTenths = tenths;
            countdown.text = KillcamSession.Countdown(remaining);
        }

        /// <summary>Draws frame <paramref name="frame"/> of <paramref name="clip"/> centred on (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
        public void Draw(KillcamClip clip, float frame, float cx, float cy, byte killer, byte victim)
        {
            var f = Mathf.Clamp(Mathf.FloorToInt(frame), 0, clip.FrameCount - 1);
            for (var i = 0; i < dots.Length; i++)
            {
                var id = (byte)i;
                var present = clip.TryGetPosition(id, frame, out var x, out var y);
                if (dots[i].gameObject.activeSelf != present) dots[i].gameObject.SetActive(present);
                if (!present) continue;
                dots[i].anchoredPosition = new Vector2((x - cx) * pixelsPerCell, (y - cy) * pixelsPerCell);
                dotImages[i].color = id == killer ? killerColor : id == victim ? victimColor : otherColor;
                var hp = clip.At(f, i).Hp;
                hpFills[i].anchorMax = new Vector2(hp / 100f, 1f);
            }
            var radius = clip.ZoneRadius[f];
            var showZone = radius > 0f;
            if (zone.gameObject.activeSelf != showZone) zone.gameObject.SetActive(showZone);
            if (showZone)
            {
                zone.anchoredPosition = new Vector2((clip.ZoneX[f] - cx) * pixelsPerCell, (clip.ZoneY[f] - cy) * pixelsPerCell);
                zone.sizeDelta = Vector2.one * (radius * 2f * pixelsPerCell);
            }
        }

        public void SetNames(System.Func<byte, string> nameOf)
        {
            for (var i = 0; i < names.Length; i++)
                if (names[i] != null) names[i].text = nameOf((byte)i) ?? "#" + (i + 1);
        }
    }
}
