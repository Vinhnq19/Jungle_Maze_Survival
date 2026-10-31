using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>One stored match's leaderboard (AC37) with empty/unavailable states. References only.</summary>
    public sealed class LeaderboardView : LobbyPanelView
    {
        [SerializeField] private TMP_Text state;
        [SerializeField] private Button retry;
        [SerializeField] private Button back;
        [SerializeField] private ResultRowView[] rows;

        public TMP_Text State => state;
        public Button Retry => retry;
        public Button Back => back;
        public ResultRowView[] Rows => rows;
    }
}
