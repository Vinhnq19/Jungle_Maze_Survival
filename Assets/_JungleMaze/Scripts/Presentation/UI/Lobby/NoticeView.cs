using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>Blocking error with a reason and 'Về menu' (load error, lost connection: AC25, AC39, AC40). References only.</summary>
    public sealed class NoticeView : LobbyPanelView
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text message;
        [SerializeField] private Button home;

        public TMP_Text Title => title;
        public TMP_Text Message => message;
        public Button Home => home;
    }
}
