using System;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.MatchUi;
using JungleMaze.Core.Events;
using JungleMaze.Core.Ticking;
using JungleMaze.Domain.Lobby;
using JungleMaze.Domain.Network;

namespace JungleMaze.App.Lobby
{
    /// <summary>
    /// UI-02 screen machine (plan 4.1). Menu sub-screens are UI-02's own state inside <c>AppScreen.MainMenu</c> (G-2);
    /// connected screens are derived every tick from the server phase and the connection (decision 2). Each command is
    /// sent once per press (decision 4), and <see cref="LeaveMatchRequested"/> from UI-03 is handled here (N7).
    /// Round 2 (Flow_Proposal 6): login comes first; without a valid JWT every menu screen falls back to Login, and
    /// joining is blocked on the client only (the server-side JWT check belongs to NET-01).
    /// </summary>
    public sealed class LobbyFlow : ITickable, IDisposable
    {
        private readonly ILobbySource _lobby;
        private readonly IMatchResultSource _results;
        private readonly IConnectionStateSource _connection;
        private readonly ILobbyCommands _commands;
        private readonly Func<double> _clock;
        private readonly Func<string> _eliminationCause;
        private readonly IEventBus _bus;
        private readonly Action<LeaveMatchRequested> _onLeaveMatch;
        private readonly OnceGate _gate = new OnceGate();
        private LobbyScreen _leaderboardReturn = LobbyScreen.History;
        private double _loadingSince;
        private bool _sawConnect, _leaving, _roomsAfterLogin, _devSkipped;
        private Guid _lastMatchId;

        public LobbyFlow(ILobbySource lobby, IMatchResultSource results, IConnectionStateSource connection,
            ILobbyCommands commands, JoinController join, AccountSession account, RoomListModel rooms,
            HistoryModel history, LeaderboardModel leaderboard, ApiStatusModel api, Func<double> clock,
            LobbyOptions options = null, IEventBus bus = null, Func<string> eliminationCause = null)
        {
            _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            _results = results ?? throw new ArgumentNullException(nameof(results));
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            Join = join ?? throw new ArgumentNullException(nameof(join));
            Account = account;
            Rooms = rooms;
            History = history;
            Leaderboard = leaderboard;
            Api = api;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Options = options ?? LobbyOptions.Default;
            _eliminationCause = eliminationCause ?? (() => null);
            Reconnect = new ReconnectController(commands, clock, Options);
            _bus = bus;
            _onLeaveMatch = _ => GoHome();
            _bus?.Subscribe(_onLeaveMatch);
            if (Account != null) Account.LoggedIn += OnLoggedIn;
            if (Rooms != null) Rooms.AuthExpired += OnAuthExpired;
            Screen = HomeScreen;
        }

        public LobbyScreen Screen { get; private set; }
        public bool LogoutConfirmOpen { get; private set; }
        public bool ServerSettingsOpen { get; private set; }

        /// <summary>Home when a JWT is valid, otherwise Login. No account service (old rigs) means no gate.</summary>
        public LobbyScreen HomeScreen => NeedsLogin ? LobbyScreen.Login : LobbyScreen.MainMenu;

        private bool NeedsLogin => Account != null && !_devSkipped && Account.Token == null;

        /// <summary>True after "Bỏ qua đăng nhập (dev)": Home and room codes work, the room manager still needs a JWT.</summary>
        public bool DevSkipped => _devSkipped;

        /// <summary>M1 (round 2): Editor/dev builds only, so the team can play by room code without a backend.</summary>
        public void DevSkipLogin()
        {
            if (!Options.DevSkipLogin || Screen != LobbyScreen.Login) return;
            _devSkipped = true;
            Go(LobbyScreen.MainMenu);
        }
        public string Notice { get; private set; } = "";
        public string LoadErrorText { get; private set; } = "";
        public bool ReplayPending { get; private set; }
        public int ResultsShown { get; private set; }
        public int LeaveRequests { get; private set; }
        public int ReadyRequests { get; private set; }
        public int ReplayRequests { get; private set; }

        public LobbyOptions Options { get; }
        public ILobbySource Lobby => _lobby;
        public IConnectionStateSource Connection => _connection;
        public JoinController Join { get; }
        public AccountSession Account { get; }
        public RoomListModel Rooms { get; }
        public HistoryModel History { get; }
        public LeaderboardModel Leaderboard { get; }
        public ApiStatusModel Api { get; }
        public ReconnectController Reconnect { get; }
        public ResultModel Result { get; } = new ResultModel();

        /// <summary>Raised when the screen changes; the view swaps panels in the same frame.</summary>
        public event Action<LobbyScreen> ScreenChanged;

        /// <summary>Set by the composition: opens the UI-03 settings panel with its leave row hidden (AC4).</summary>
        public Action OpenSettingsPanel;

        public int CountdownSeconds => LobbyCountdown.Seconds(_lobby.PhaseEndTick, _lobby.ServerTick);

        public bool HasCountdown => _lobby.HasPhase && (_lobby.Phase == MatchPhase.Lobby || _lobby.Phase == MatchPhase.Loading)
            && CountdownSeconds > 0;

        /// <summary>Lobby footer: server countdown, or how many players are still needed (AC21, AC22).</summary>
        public string LobbyStatusText => HasCountdown ? "Bắt đầu sau " + CountdownSeconds + " s"
            : "Chờ đủ " + Math.Max(1, _lobby.MinPlayers) + " người";

        public string AutoStartText => "Tự bắt đầu sau 15 s khi đủ " + Math.Max(1, _lobby.MinPlayers) + " người";

        // ---- menu navigation -------------------------------------------------------------------------------------

        /// <summary>"Vào phòng": a modal over Home; without a JWT it goes to Login instead.</summary>
        public void OpenJoin()
        {
            if (NeedsLogin) { Go(LobbyScreen.Login); return; }
            Go(LobbyScreen.JoinForm);
        }

        /// <summary>"Tạo phòng": login first unless a valid JWT is in memory (AC12, AC17).</summary>
        public void OpenRooms()
        {
            if (Account == null || Rooms == null) return;
            if (Account.Token == null) { _roomsAfterLogin = true; Go(LobbyScreen.Login); return; }
            Go(LobbyScreen.RoomManager);
            Rooms.Refresh();
        }

        public void OpenHistory()
        {
            Go(LobbyScreen.History);
            _ = History?.Refresh();
        }

        public void OpenLeaderboard(Guid matchId)
        {
            if (Leaderboard == null) return;
            _leaderboardReturn = Screen;
            Go(LobbyScreen.Leaderboard);
            Leaderboard.Load(matchId);
        }

        /// <summary>Home "Bảng xếp hạng": the newest match in history, else the last result seen this session.</summary>
        public void OpenLatestLeaderboard()
        {
            var latest = _lastMatchId;
            var newest = DateTimeOffset.MinValue;
            if (History != null)
                foreach (var row in History.Rows)
                    if (row.EndedAt > newest) { newest = row.EndedAt; latest = row.MatchId; }
            OpenLeaderboard(latest);
        }

        /// <summary>Logout on the account card asks first; confirming drops the JWT and lands on Login.</summary>
        public void RequestLogout()
        {
            if (Account == null || Screen != LobbyScreen.MainMenu) return;
            if (_devSkipped) { Go(LobbyScreen.Login); return; } // nothing to lose: no confirm
            LogoutConfirmOpen = true;
        }

        public void CancelLogout() => LogoutConfirmOpen = false;

        public void ConfirmLogout()
        {
            if (!LogoutConfirmOpen) return;
            LogoutConfirmOpen = false;
            _roomsAfterLogin = false;
            Account.Logout();
            Rooms?.Clear();
            Go(LobbyScreen.Login);
        }

        /// <summary>"Cài đặt" on Home and "Máy chủ (nâng cao)" on Login: host, port and API in a modal.</summary>
        public void OpenServerSettings()
        {
            if (Screen == LobbyScreen.MainMenu || Screen == LobbyScreen.Login) ServerSettingsOpen = true;
        }

        public void CloseServerSettings() => ServerSettingsOpen = false;

        /// <summary>Audio/controls: the UI-03 settings panel, reached from the server-settings modal on Home.</summary>
        public void OpenSettings()
        {
            ServerSettingsOpen = false;
            Go(LobbyScreen.Settings);
            OpenSettingsPanel?.Invoke();
        }

        public void SettingsClosed()
        {
            if (Screen == LobbyScreen.Settings) Go(HomeScreen);
        }

        /// <summary>"Quay lại" on menu sub-screens. Connected screens use <see cref="GoHome"/>.</summary>
        public void Back()
        {
            switch (Screen)
            {
                case LobbyScreen.Leaderboard: Go(_leaderboardReturn); break;
                case LobbyScreen.Connecting: CancelJoin(); break;
                default:
                    if (Screen == LobbyScreen.Login) break; // first screen: nowhere to go back to
                    if (LobbyScreenRules.IsMenu(Screen)) Go(HomeScreen);
                    else GoHome();
                    break;
            }
        }

        /// <summary>"Vào phòng này": the same RoomCode path as the join modal (AC18).</summary>
        public void JoinRoom(in RoomInfo room)
        {
            if (room.RoomCode != null) Join.SetCode(room.RoomCode);
            OpenJoin();
        }

        // ---- join ------------------------------------------------------------------------------------------------

        public bool SubmitJoin()
        {
            if (Screen != LobbyScreen.JoinForm) return false;
            if (NeedsLogin) { Go(LobbyScreen.Login); return false; } // client-side gate only
            if (!Join.Submit()) return false;
            _sawConnect = false;
            Notice = "";
            Go(LobbyScreen.Connecting);
            return true;
        }

        public void CancelJoin()
        {
            Join.Cancel();
            if (Screen == LobbyScreen.Connecting) Go(LobbyScreen.JoinForm);
        }

        // ---- lobby / results -------------------------------------------------------------------------------------

        public bool Ready()
        {
            if (!Options.ReadyEnabled || Screen != LobbyScreen.Lobby || !_gate.TryPass(_lobby.MatchId, "ready")) return false;
            ReadyRequests++;
            _commands.Ready();
            return true;
        }

        public bool Replay()
        {
            if (Screen != LobbyScreen.Results || _lobby.Phase != MatchPhase.Ending) return false;
            if (!_gate.TryPass(_lobby.MatchId, "replay")) return false;
            ReplayRequests++;
            ReplayPending = true;
            _commands.Replay();
            return true;
        }

        /// <summary>"Về trang chủ"/"Rời phòng"/N7: one leave, then Home (Login if the JWT ran out); the form keeps name and code (AC23, AC42).</summary>
        public void GoHome()
        {
            if (Join.Busy) Join.Cancel();
            else if (!_leaving && (!LobbyScreenRules.IsMenu(Screen) || IsActive(_connection.State)))
            {
                _leaving = true;
                LeaveRequests++;
                _commands.Leave();
            }
            Reconnect.Reset();
            Result.Clear();
            ReplayPending = false;
            Notice = "";
            LoadErrorText = "";
            Go(HomeScreen);
        }

        // ---- tick ------------------------------------------------------------------------------------------------

        public void Tick(float deltaTime)
        {
            var net = _connection.State;
            if (_leaving)
            {
                if (IsActive(net)) return;
                _leaving = false;
            }
            switch (Screen)
            {
                case LobbyScreen.Connecting: TickConnecting(net); break;
                case LobbyScreen.Reconnecting: TickReconnecting(net); break;
                case LobbyScreen.RoomManager:
                    if (NeedsLogin) { _roomsAfterLogin = true; Go(LobbyScreen.Login); } // AC17: expiry by clock
                    break;
                case LobbyScreen.MainMenu:
                case LobbyScreen.JoinForm:
                case LobbyScreen.History:
                case LobbyScreen.Leaderboard:
                    if (NeedsLogin) Go(LobbyScreen.Login);
                    break;
                case LobbyScreen.Lobby:
                case LobbyScreen.Loading:
                case LobbyScreen.InGame:
                case LobbyScreen.Results:
                    if (net != NetworkState.Joined) OnLost();
                    else Follow();
                    break;
            }
        }

        public void Dispose()
        {
            _bus?.Unsubscribe(_onLeaveMatch);
            if (Account != null) Account.LoggedIn -= OnLoggedIn;
            if (Rooms != null) Rooms.AuthExpired -= OnAuthExpired;
        }

        private void TickConnecting(NetworkState net)
        {
            if (Join.Tick()) { Go(LobbyScreen.JoinForm); return; } // AC10: retry + home on the form
            if (net == NetworkState.Joined) { Join.Joined(); Notice = ""; Follow(); return; }
            if (net == NetworkState.Connecting || net == NetworkState.Authenticating) { _sawConnect = true; return; }
            if (net == NetworkState.Failed || _sawConnect)
            {
                Join.Rejected(_connection.LastReason);
                Go(LobbyScreen.JoinForm);
            }
        }

        private void TickReconnecting(NetworkState net)
        {
            var state = Reconnect.Tick(net);
            if (state == ReconnectState.Succeeded) { Reconnect.Reset(); Notice = ""; Follow(); }
            else if (state == ReconnectState.Failed) { Notice = ReconnectController.FailedText; Go(LobbyScreen.Disconnected); }
        }

        private void Follow()
        {
            var resultForMatch = _results.HasResult && _results.Current != null && _results.Current.MatchId == _lobby.MatchId;
            var next = LobbyScreenRules.ForPhase(_lobby.HasPhase, _lobby.Phase, resultForMatch);
            if (next == LobbyScreen.Lobby && Screen == LobbyScreen.Loading && _lobby.Phase == MatchPhase.Waiting)
                Notice = LobbyScreenRules.NotEnoughPlayers; // AC26
            if (next == LobbyScreen.Loading)
            {
                if (Screen != LobbyScreen.Loading) _loadingSince = _clock();
                var mapError = _lobby.LoadError;
                if (mapError != null) { FailLoading(LobbyScreenRules.LoadMapError + ": " + mapError); return; }
                if (_clock() - _loadingSince >= Options.LoadingStuckSeconds) { FailLoading(LobbyScreenRules.LoadStuck); return; }
            }
            if (next == LobbyScreen.Results) ShowResultIfNew();
            if (next != LobbyScreen.Results) ReplayPending = false;
            if (next == LobbyScreen.Lobby && Screen == LobbyScreen.Results) Result.Clear(); // AC32: no stale result
            if (next != LobbyScreen.Lobby && Notice == LobbyScreenRules.NotEnoughPlayers) Notice = "";
            Go(next);
        }

        private void ShowResultIfNew()
        {
            var current = _results.HasResult ? _results.Current : null;
            if (current == null || current.MatchId != _lobby.MatchId || Result.HasData && Result.MatchId == current.MatchId) return;
            Result.Show(current, _eliminationCause(), Options.ShowKills);
            _lastMatchId = current.MatchId;
            ResultsShown++;
        }

        private void FailLoading(string text)
        {
            LoadErrorText = text;
            Go(LobbyScreen.LoadError);
        }

        private void OnLost()
        {
            var next = LobbyScreenRules.OnLost(Screen, Options.ReconnectEnabled, _connection.CanResume);
            if (next == LobbyScreen.LoadError) { FailLoading(LobbyScreenRules.LoadLostText(_connection.LastReason)); return; }
            if (next == LobbyScreen.Reconnecting && !Reconnect.Begin(_connection.CanResume)) next = LobbyScreen.Disconnected;
            if (next == LobbyScreen.Disconnected) Notice = ReconnectController.LostText;
            Go(next);
        }

        private void OnLoggedIn()
        {
            if (string.IsNullOrEmpty(Join.DisplayName)) Join.SetName(Account.Username); // prefill from the account
            if (Screen != LobbyScreen.Login) return;
            if (!_roomsAfterLogin) { Go(LobbyScreen.MainMenu); return; }
            _roomsAfterLogin = false;
            Go(LobbyScreen.RoomManager);
            Rooms?.Refresh();
        }

        private void OnAuthExpired()
        {
            if (Screen != LobbyScreen.RoomManager) return;
            _roomsAfterLogin = true;
            Go(LobbyScreen.Login);
        }

        private static bool IsActive(NetworkState s) =>
            s == NetworkState.Joined || s == NetworkState.Connecting || s == NetworkState.Authenticating;

        private void Go(LobbyScreen next)
        {
            if (next == Screen) return;
            if (next != LobbyScreen.MainMenu) LogoutConfirmOpen = false;
            if (next == LobbyScreen.Login)
            {
                // Logout, expiry or a dev skip ended: nothing of the last account stays visible (review m1).
                _devSkipped = false;
                History?.Clear();
                _lastMatchId = Guid.Empty;
            }
            if (next != LobbyScreen.MainMenu && next != LobbyScreen.Login) ServerSettingsOpen = false;
            Screen = next;
            ScreenChanged?.Invoke(next);
        }
    }
}
