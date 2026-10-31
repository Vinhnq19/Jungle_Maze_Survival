using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>One ranked row: rank as sent, name, kills, survival; own row highlighted by NetId (AC30). References only.</summary>
    public sealed class ResultRowView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text rank;
        [SerializeField] private TMP_Text playerName;
        [SerializeField] private TMP_Text kills;
        [SerializeField] private TMP_Text survival;

        public Image Background => background;
        public TMP_Text Rank => rank;
        public TMP_Text PlayerName => playerName;
        public TMP_Text Kills => kills;
        public TMP_Text Survival => survival;
    }
}
