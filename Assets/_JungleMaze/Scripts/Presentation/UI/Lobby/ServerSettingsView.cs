using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>
    /// "Cài đặt · Máy chủ" modal (Figma 05_settings-server 133:195): the old Home endpoint picker plus Close, opened from
    /// Home "Cài đặt" and Login "Máy chủ (nâng cao)". Audio opens the UI-03 settings panel (Home only). References only.
    /// </summary>
    public sealed class ServerSettingsView : LobbyPanelView
    {
        [SerializeField] private TMP_Dropdown profile;
        [SerializeField] private TMP_InputField host;
        [SerializeField] private TMP_InputField port;
        [SerializeField] private TMP_InputField api;
        [SerializeField] private Button save;
        [SerializeField] private TMP_Text status;
        [SerializeField] private Button audioSettings;
        [SerializeField] private Button close;

        public TMP_Dropdown Profile => profile;
        public TMP_InputField Host => host;
        public TMP_InputField Port => port;
        public TMP_InputField Api => api;
        public Button Save => save;
        public TMP_Text Status => status;
        public Button Audio => audioSettings;
        public Button Close => close;
    }
}
