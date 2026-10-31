using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>Logout confirm over Home (Figma 04_logout-confirm 133:31): question, Đăng xuất / Hủy. References only.</summary>
    public sealed class LogoutConfirmView : LobbyPanelView
    {
        [SerializeField] private TMP_Text text;
        [SerializeField] private Button yes;
        [SerializeField] private Button no;

        public TMP_Text Text => text;
        public Button Yes => yes;
        public Button No => no;
    }
}
