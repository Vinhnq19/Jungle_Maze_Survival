using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Hud
{
    /// <summary>Match status (Figma 41:86): "Bo thu sau" + mm:ss and "Còn sống" + alive/total. Display only.</summary>
    public sealed class MatchStatusView : MonoBehaviour, IMatchStatusView
    {
        [SerializeField] private Graphic background;
        [SerializeField] private TMP_Text zoneLabel;
        [SerializeField] private TMP_Text zoneValue;
        [SerializeField] private TMP_Text aliveLabel;
        [SerializeField] private TMP_Text aliveValue;

        public TMP_Text ZoneValue => zoneValue;
        public TMP_Text AliveValue => aliveValue;

        public void ApplyPalette(HudPalette palette)
        {
            if (background != null) background.color = palette.Surface;
            if (zoneLabel != null) zoneLabel.color = palette.Muted;
            if (aliveLabel != null) aliveLabel.color = palette.Muted;
            if (zoneValue != null) zoneValue.color = palette.Purple;
            if (aliveValue != null) aliveValue.color = palette.Text;
        }

        public void SetZoneCountdown(char[] text, int length)
        {
            if (zoneValue != null) zoneValue.SetCharArray(text, 0, length);
        }

        public void SetAlive(char[] text, int length)
        {
            if (aliveValue != null) aliveValue.SetCharArray(text, 0, length);
        }
    }
}
