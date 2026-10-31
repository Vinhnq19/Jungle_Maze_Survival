using JungleMaze.App.Lobby;
using UnityEngine;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>
    /// UI-02 entry asset at <c>Art/UI/Lobby/Resources/LobbyUiSet.asset</c>, loaded once by Bootstrap like
    /// <c>MatchOverlaySet</c> (T-10). Holds the prefab and the N9 switches (client asset, not <c>match.json</c>).
    /// </summary>
    [CreateAssetMenu(menuName = "Jungle Maze/Lobby UI Set")]
    public sealed class LobbyUiSet : ScriptableObject
    {
        public const string ResourcePath = "LobbyUiSet";

        [SerializeField] private LobbyUiRoot uiPrefab;
        [Tooltip("Q1: per-player Ready button. Off until the rule is approved and SRV-03 supports it.")]
        [SerializeField] private bool lobbyReadyEnabled;
        [Tooltip("Q1/T-6: Start button, shown locked because the server has no start command.")]
        [SerializeField] private bool lobbyStartEnabled;
        [Tooltip("Q2: reconnect overlay and one resume request when the connection drops in a match.")]
        [SerializeField] private bool reconnectEnabled = true;
        [SerializeField] private bool historyEnabled = true;
        [Tooltip("T-4: the server sends Kills = 0 until PlayerDied ships.")]
        [SerializeField] private bool showKills;
        [SerializeField] private float connectBudgetSeconds = 20f;
        [SerializeField] private float loadingStuckSeconds = 35f;
        [SerializeField] private string controlsHint = "WASD di chuyển · Chuột chọn hướng";
        [SerializeField] private string[] tips = { "Bo thu hẹp theo pha: để ý vòng tiếp theo trên bản đồ." };

        public LobbyUiRoot UiPrefab => uiPrefab;

        public LobbyOptions ToOptions(float graceSeconds) => new LobbyOptions
        {
            ReadyEnabled = lobbyReadyEnabled,
            StartEnabled = lobbyStartEnabled,
            ReconnectEnabled = reconnectEnabled,
            HistoryEnabled = historyEnabled,
            ShowKills = showKills,
            GraceSeconds = graceSeconds > 0 ? graceSeconds : 15f,
            ConnectBudgetSeconds = connectBudgetSeconds,
            LoadingStuckSeconds = loadingStuckSeconds,
            ControlsHint = controlsHint,
            Tips = tips != null && tips.Length > 0 ? tips : LobbyOptions.Default.Tips
        };
    }
}
