using System;
using JungleMaze.App.Lobby;
using JungleMaze.App.Lobby.Adapters;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using JungleMaze.App.Lobby.Mocks;
#endif
using JungleMaze.App.Match;
using JungleMaze.App.MatchUi;
using JungleMaze.App.Network;
using JungleMaze.App.Ports;
using JungleMaze.Core.Events;
using JungleMaze.Core.Ticking;
using JungleMaze.Domain.Network;
using JungleMaze.Infrastructure.Api;
using JungleMaze.Infrastructure.Network;
using JungleMaze.Presentation.UI;
using JungleMaze.Presentation.UI.Lobby;
using UnityEngine;

namespace JungleMaze.Composition
{
    /// <summary>
    /// UI-02 composition: the live ports (N1–N6) over MatchSession/NetworkClient/the form presenter, the API client, the
    /// screen flow (which now handles LeaveMatchRequested, N7), the prefab instantiated once (T-10), the old IMGUI menu
    /// switched off (F8 brings it back) and, in dev builds, the mocks behind the F9 panel or <c>-lobbyMock=&lt;case&gt;</c>.
    /// </summary>
    public sealed partial class Bootstrap
    {
        public const string DisplayNameKey = "jm.menu.displayName"; // G-7: not a secret

        private void ComposeLobby(IEventBus bus, ITickScheduler ticks, MatchSession match, NetworkClient network,
            IBalanceConfigProvider balance)
        {
            var set = Resources.Load<LobbyUiSet>(LobbyUiSet.ResourcePath);
            if (set == null || set.UiPrefab == null)
            {
                _log.Warn("[Bootstrap] LobbyUiSet missing or incomplete; the IMGUI connection menu stays on.");
                return;
            }

            Func<double> clock = () => Time.unscaledTimeAsDouble;
            var options = set.ToOptions(balance.Match.DisconnectGraceSeconds);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            options.DevSkipLogin = true; // review M1: play by room code without a backend; never in release builds
#endif
            var config = _registry.Resolve<IConfigProvider>();
            var menu = _registry.Resolve<ConnectionMenuPresenter>();
            var connect = _registry.Resolve<ConnectToServerUseCase>();
            var api = new LobbyApiClient(() => config.Current, _log);
            var connection = new NetworkConnectionStateSource(network, () => match.CanResume, bus);
            _registry.Register(connection);

            ILobbySource lobby = new MatchSessionLobbySource(match, () => network.LastRequest.RoomCode);
            IMatchResultSource results = new MatchSessionResultSource(match);
            IConnectionStateSource state = connection;
            ILobbyCommands commands = new SessionLobbyCommands((code, name) =>
            {
                connection.ClearLoginFailure();
                menu.RoomCode = code;
                menu.DisplayName = name;
                menu.Join();
                // The form presenter refused before connecting (bad port/endpoint, AC2): its Status says why.
                if (!connect.IsLoggingIn && (network.State == NetworkState.Disconnected || network.State == NetworkState.Failed))
                    connection.ReportLocalFailure();
            }, menu.Disconnect, match, network);
            IAccountApi account = api;
            IRoomsApi rooms = api;
            IApiHealth health = api;
            ILeaderboardApi board = api;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var scenarios = new LobbyScenarios(clock, lobby, results, state, commands, account, rooms, health, board);
            var ports = scenarios.Ports;
            lobby = ports; results = ports; state = ports; commands = ports;
            account = ports; rooms = ports; health = ports; board = ports;
            foreach (var arg in Environment.GetCommandLineArgs())
                if (arg.StartsWith("-lobbyMock=", StringComparison.Ordinal)
                    && Enum.TryParse(arg.Substring("-lobbyMock=".Length), true, out LobbyCase mockCase))
                    scenarios.Use(mockCase);
            _registry.Register(scenarios);
            ticks.Add(scenarios);
#endif

            var accountSession = new AccountSession(account, () => DateTime.UtcNow);
            var join = new JoinController(commands, clock, options, PlayerPrefs.GetString(DisplayNameKey, menu.DisplayName),
                menu.RoomCode, name => PlayerPrefs.SetString(DisplayNameKey, name));
            _registry.TryResolve<IEliminationSource>(out var elimination);
            var flow = new LobbyFlow(lobby, results, state, commands, join, accountSession,
                new RoomListModel(rooms, accountSession),
                new HistoryModel(_registry.Resolve<IPlayerHistory>(), () => accountSession.IsValid ? accountSession.Token : null,
                    () => health.Configured, options),
                new LeaderboardModel(board), new ApiStatusModel(health), clock, options, bus,
                () => elimination != null && elimination.IsLocalEliminated ? EliminationText.Cause(elimination.Current) : null);
            _registry.Register(flow);
            ticks.Add(flow);

            Func<bool> settingsOpen = null;
            if (_registry.TryResolve<MatchMenuController>(out var matchMenu))
            {
                flow.OpenSettingsPanel = matchMenu.OpenFromMainMenu;
                settingsOpen = () => matchMenu.IsOpen;
            }
            var root = Instantiate(set.UiPrefab, transform);
            root.name = "LobbyUi";
            root.Bind(new LobbyUiPresenter(root, flow, menu, null, null, settingsOpen));
            gameObject.AddComponent<LegacyImguiToggle>().Construct(GetComponent<ConnectionMenuView>);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            gameObject.AddComponent<LobbyDebugPanel>().Construct(scenarios, flow);
#endif
        }
    }
}
