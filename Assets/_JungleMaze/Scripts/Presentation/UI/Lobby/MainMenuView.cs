using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>
    /// Home (Figma 03_home 121:2): six buttons, API status line and the account card (status, name, logout). The server
    /// fields moved to <see cref="ServerSettingsView"/> (Flow_Proposal 6.2). References only.
    /// </summary>
    public sealed class MainMenuView : LobbyPanelView
    {
        [SerializeField] private Button join;
        [SerializeField] private Button rooms;
        [SerializeField] private Button history;
        [SerializeField] private Button leaderboard;
        [SerializeField] private Button settings;
        [SerializeField] private Button quit;
        [SerializeField] private TMP_Text apiStatus;
        [SerializeField] private TMP_Text accountStatus;
        [SerializeField] private TMP_Text accountName;
        [SerializeField] private Button logout;

        public Button Join => join;
        public Button Rooms => rooms;
        public Button History => history;
        public Button Leaderboard => leaderboard;
        public Button Settings => settings;
        public Button Quit => quit;
        public TMP_Text ApiStatus => apiStatus;
        public TMP_Text AccountStatus => accountStatus;
        public TMP_Text AccountName => accountName;
        public Button Logout => logout;
    }
}
