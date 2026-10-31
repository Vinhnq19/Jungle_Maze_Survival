using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>One history line; the button opens that match's online leaderboard (AC37). References only.</summary>
    public sealed class HistoryRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text line;
        [SerializeField] private Button open;

        public TMP_Text Line => line;
        public Button Open => open;
    }
}
