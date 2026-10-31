using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>One room row: name, occupancy, default tag or code + copy, join, delete (AC14, AC16). References only.</summary>
    public sealed class RoomRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text roomName;
        [SerializeField] private TMP_Text occupancy;
        [SerializeField] private TMP_Text code;
        [SerializeField] private Button copy;
        [SerializeField] private Button join;
        [SerializeField] private Button delete;

        public TMP_Text RoomName => roomName;
        public TMP_Text Occupancy => occupancy;
        public TMP_Text Code => code;
        public Button Copy => copy;
        public Button Join => join;
        public Button Delete => delete;
    }
}
