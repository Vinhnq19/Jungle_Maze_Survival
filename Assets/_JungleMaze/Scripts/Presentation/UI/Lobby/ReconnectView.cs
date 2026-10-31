using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>Reconnect overlay (Figma v2 43:131): message, grace seconds left (about), home (AC38). References only.</summary>
    public sealed class ReconnectView : LobbyPanelView
    {
        [SerializeField] private TMP_Text message;
        [SerializeField] private TMP_Text seconds;
        [SerializeField] private Button home;

        public TMP_Text Message => message;
        public TMP_Text Seconds => seconds;
        public Button Home => home;
    }
}
