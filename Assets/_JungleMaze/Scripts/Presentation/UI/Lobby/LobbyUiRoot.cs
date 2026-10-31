using UnityEngine;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>
    /// Root of LobbyUi.prefab: canvas under the UI-03 overlay (15 &lt; 20) and above the HUD. Instantiated once at
    /// composition, never per press (G-1). Holds panel references; Bootstrap binds the presenter, which this drives in
    /// LateUpdate on unscaled time. Has no EventSystem of its own: UI-03's single EventSystem serves both (AC45).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyUiRoot : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private GameObject backdrop;
        [SerializeField] private MainMenuView mainMenu;
        [SerializeField] private JoinFormView joinForm;
        [SerializeField] private ConnectingView connecting;
        [SerializeField] private LoginView login;
        [SerializeField] private RoomManagerView roomManager;
        [SerializeField] private HistoryView history;
        [SerializeField] private LeaderboardView leaderboard;
        [SerializeField] private LobbyView lobby;
        [SerializeField] private LoadingView loading;
        [SerializeField] private ResultsView results;
        [SerializeField] private ReconnectView reconnect;
        [SerializeField] private NoticeView notice;

        public Canvas Canvas => canvas;
        public GameObject Backdrop => backdrop;
        public MainMenuView MainMenu => mainMenu;
        public JoinFormView JoinForm => joinForm;
        public ConnectingView Connecting => connecting;
        public LoginView Login => login;
        public RoomManagerView RoomManager => roomManager;
        public HistoryView History => history;
        public LeaderboardView Leaderboard => leaderboard;
        public LobbyView Lobby => lobby;
        public LoadingView Loading => loading;
        public ResultsView Results => results;
        public ReconnectView Reconnect => reconnect;
        public NoticeView Notice => notice;
        public LobbyUiPresenter Presenter { get; private set; }

        public LobbyPanelView[] Panels => new LobbyPanelView[]
            { mainMenu, joinForm, connecting, login, roomManager, history, leaderboard, lobby, loading, results, reconnect, notice };

        public void Bind(LobbyUiPresenter presenter)
        {
            Presenter?.Dispose();
            Presenter = presenter;
        }

        private void LateUpdate() => Presenter?.Tick();

        private void OnDestroy()
        {
            Presenter?.Dispose();
            Presenter = null;
        }
    }
}
