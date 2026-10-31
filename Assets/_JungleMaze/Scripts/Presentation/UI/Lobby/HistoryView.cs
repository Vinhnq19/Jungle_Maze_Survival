using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>Personal history (AC35, AC36): identity label, loading/empty/unavailable states, 20 rows, retry/back. References only.</summary>
    public sealed class HistoryView : LobbyPanelView
    {
        [SerializeField] private TMP_Text identity;
        [SerializeField] private TMP_Text state;
        [SerializeField] private Button retry;
        [SerializeField] private Button back;
        [SerializeField] private HistoryRowView[] rows;

        public TMP_Text Identity => identity;
        public TMP_Text State => state;
        public Button Retry => retry;
        public Button Back => back;
        public HistoryRowView[] Rows => rows;
    }
}
