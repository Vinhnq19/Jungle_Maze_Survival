using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>Login/register for room management only (AC12, AC13). The password field is cleared by the presenter. References only.</summary>
    public sealed class LoginView : LobbyPanelView
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_InputField username;
        [SerializeField] private TMP_InputField password;
        [SerializeField] private Button loginTab;
        [SerializeField] private Button registerTab;
        [SerializeField] private Button submit;
        [SerializeField] private Button back;
        [SerializeField] private TMP_Text message;

        public TMP_Text Title => title;
        public TMP_InputField Username => username;
        public TMP_InputField Password => password;
        public Button LoginTab => loginTab;
        public Button RegisterTab => registerTab;
        public Button Submit => submit;
        public Button Back => back;
        public TMP_Text Message => message;
    }
}
