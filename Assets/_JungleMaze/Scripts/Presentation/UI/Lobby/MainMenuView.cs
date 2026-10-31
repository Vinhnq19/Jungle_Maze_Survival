using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>Main menu (Figma v2 42:88): five buttons, API status line and the endpoint picker (AC1–AC4). References only.</summary>
    public sealed class MainMenuView : LobbyPanelView
    {
        [SerializeField] private Button join;
        [SerializeField] private Button rooms;
        [SerializeField] private Button history;
        [SerializeField] private Button settings;
        [SerializeField] private Button quit;
        [SerializeField] private TMP_Text apiStatus;
        [SerializeField] private TMP_Dropdown profile;
        [SerializeField] private TMP_InputField host;
        [SerializeField] private TMP_InputField port;
        [SerializeField] private TMP_InputField api;
        [SerializeField] private Button saveEndpoint;
        [SerializeField] private TMP_Text endpointStatus;

        public Button Join => join;
        public Button Rooms => rooms;
        public Button History => history;
        public Button Settings => settings;
        public Button Quit => quit;
        public TMP_Text ApiStatus => apiStatus;
        public TMP_Dropdown Profile => profile;
        public TMP_InputField Host => host;
        public TMP_InputField Port => port;
        public TMP_InputField Api => api;
        public Button SaveEndpoint => saveEndpoint;
        public TMP_Text EndpointStatus => endpointStatus;
    }
}
