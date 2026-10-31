using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>Connecting: current step (UDP/WebSocket/Auto), RTT once measured, cancel (AC9). References only.</summary>
    public sealed class ConnectingView : LobbyPanelView
    {
        [SerializeField] private TMP_Text step;
        [SerializeField] private TMP_Text rtt;
        [SerializeField] private Button cancel;

        public TMP_Text Step => step;
        public TMP_Text Rtt => rtt;
        public Button Cancel => cancel;
    }
}
