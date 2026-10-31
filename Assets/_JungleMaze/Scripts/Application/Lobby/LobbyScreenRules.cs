using JungleBackend.Protocol.Gameplay;
using JungleMaze.Domain.Lobby;
using JungleMaze.Domain.Network;

namespace JungleMaze.App.Lobby
{
    /// <summary>
    /// Pure screen rules (decision 2 of the plan). The screen follows the server phase and the connection; nothing here
    /// changes <see cref="MatchPhase"/>, ranks or the countdown (AC43). Every network failure lands on a screen that
    /// has a "Về menu" button (AC40).
    /// </summary>
    public static class LobbyScreenRules
    {
        public const string NotEnoughPlayers = "Không đủ người, quay về phòng chờ";
        public const string LoadMapError = "Dữ liệu bản đồ không khớp";
        public const string LoadLost = "Mất kết nối khi đang tải";
        public const string LoadKicked = "Bạn bị loại vì chưa tải xong trong 30 giây";
        public const string LoadStuck = "Tải trận quá lâu, hãy về menu và thử lại";

        /// <summary>Main menu and its own sub-screens: no connection involved.</summary>
        public static bool IsMenu(LobbyScreen s) => s <= LobbyScreen.Settings;

        /// <summary>Screens that live on a joined connection and react to its loss.</summary>
        public static bool IsConnected(LobbyScreen s) =>
            s == LobbyScreen.Lobby || s == LobbyScreen.Loading || s == LobbyScreen.InGame || s == LobbyScreen.Results;

        /// <summary>Everything except the main menu itself offers a way home (InGame through the UI-03 ESC menu).</summary>
        public static bool HasHome(LobbyScreen s) => s != LobbyScreen.MainMenu;

        /// <summary>Where a joined client belongs for a server phase.</summary>
        public static LobbyScreen ForPhase(bool hasPhase, MatchPhase phase, bool hasResultForMatch)
        {
            if (!hasPhase) return LobbyScreen.Lobby;
            switch (phase)
            {
                case MatchPhase.Loading: return LobbyScreen.Loading;
                case MatchPhase.InGame: return hasResultForMatch ? LobbyScreen.Results : LobbyScreen.InGame;
                case MatchPhase.Ending: return LobbyScreen.Results;
                default: return LobbyScreen.Lobby; // Waiting, Lobby, Cleanup (AC26, AC33)
            }
        }

        /// <summary>Where a screen goes when the connection drops without the player asking.</summary>
        public static LobbyScreen OnLost(LobbyScreen current, bool reconnectEnabled, bool canResume)
        {
            switch (current)
            {
                case LobbyScreen.Connecting: return LobbyScreen.JoinForm;
                case LobbyScreen.Loading:
                case LobbyScreen.LoadError: return LobbyScreen.LoadError;
                case LobbyScreen.InGame: return reconnectEnabled && canResume ? LobbyScreen.Reconnecting : LobbyScreen.Disconnected;
                case LobbyScreen.Lobby:
                case LobbyScreen.Results:
                case LobbyScreen.Reconnecting: return LobbyScreen.Disconnected;
                default: return current; // menu screens: API errors stay on the screen with a retry
            }
        }

        public static string LoadLostText(DisconnectReason reason) =>
            reason == DisconnectReason.ServerClosed ? LoadKicked : LoadLost;
    }
}
