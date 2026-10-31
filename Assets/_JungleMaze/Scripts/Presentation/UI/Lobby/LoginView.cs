using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>
    /// First screen (Figma 01_login 121:285): login/register tabs, fields, submit and "Máy chủ (nâng cao)". No back button:
    /// there is nothing before it. The password field is cleared by the presenter. References only.
    /// </summary>
    public sealed class LoginView : LobbyPanelView
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_InputField username;
        [SerializeField] private TMP_InputField password;
        [SerializeField] private Button loginTab;
        [SerializeField] private Button registerTab;
        [SerializeField] private Button submit;
        [SerializeField] private Button advanced;
        [SerializeField] private TMP_Text message;
        [Tooltip("Inactive tab: dimmed button and grey label (AC50: changed on the prefab only).")]
        [SerializeField] private float idleTabAlpha = 0.6f;
        [SerializeField] private Color idleTabText = new Color32(0xA6, 0xBB, 0xB7, 0xFF);
        [SerializeField] private Color activeTabText = new Color32(0xED, 0xF2, 0xEE, 0xFF);

        public TMP_Text Title => title;
        public TMP_InputField Username => username;
        public TMP_InputField Password => password;
        public Button LoginTab => loginTab;
        public Button RegisterTab => registerTab;
        public Button Submit => submit;
        public Button Advanced => advanced;
        public TMP_Text Message => message;

        public void SetTab(bool register)
        {
            Tab(loginTab, !register);
            Tab(registerTab, register);
        }

        private void Tab(Button tab, bool active)
        {
            var c = tab.image.color;
            c.a = active ? 1f : idleTabAlpha;
            tab.image.color = c;
            var label = tab.GetComponentInChildren<TMP_Text>();
            if (label != null) label.color = active ? activeTabText : idleTabText;
        }
    }
}
