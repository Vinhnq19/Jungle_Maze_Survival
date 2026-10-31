#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.Core.Ticking;
using JungleMaze.Domain.Lobby;
using JungleMaze.Domain.Network;

namespace JungleMaze.App.Lobby.Mocks
{
    /// <summary>The AC44 run plus the six DoD cases; one choice each, no code change (4.4).</summary>
    public enum LobbyCase : byte
    {
        HappyPath, RoomFull, WrongCode, JwtExpired, LoadError, Disconnect, DisconnectTimeout, DisconnectRejected, Replay
    }

    /// <summary>
    /// Dev-only stand-in for SRV-03/NET-02/MOV-02 (F9, <c>-lobbyMock=&lt;case&gt;</c>). Owns the mocks, plays a small
    /// server script against the commands UI-02 sends, and hands UI-02 either the mocks or the live ports through
    /// <see cref="Ports"/>. The script advances the mock server tick at 30 Hz like the server would.
    /// </summary>
    public sealed class LobbyScenarios : ITickable
    {
        public readonly MockLobbySource Lobby = new MockLobbySource();
        public readonly MockMatchResultSource Results = new MockMatchResultSource();
        public readonly MockConnectionStateSource Connection = new MockConnectionStateSource();
        public readonly MockLobbyCommands Commands = new MockLobbyCommands();
        public readonly MockLobbyApi Api = new MockLobbyApi();
        public readonly LobbyPortSwitch Ports;

        public float JoinDelay = 0.3f, LobbySeconds = 5f, LoadingSeconds = 2f, InGameSeconds = 4f, EndingSeconds = 10f, DropAfter = 1f;
        public bool TicksRunning = true;

        private readonly Func<double> _clock;
        private readonly List<(double at, Action act)> _timeline = new List<(double, Action)>();
        private double _tickCarry, _lastClock;
        private bool _dropped;

        public LobbyScenarios(Func<double> clock, ILobbySource liveLobby = null, IMatchResultSource liveResults = null,
            IConnectionStateSource liveConnection = null, ILobbyCommands liveCommands = null, IAccountApi liveAccount = null,
            IRoomsApi liveRooms = null, IApiHealth liveHealth = null, ILeaderboardApi liveBoard = null)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _lastClock = clock();
            Ports = new LobbyPortSwitch(this, liveLobby, liveResults, liveConnection, liveCommands, liveAccount, liveRooms,
                liveHealth, liveBoard);
            Commands.Called += OnCommand;
        }

        public LobbyCase Case { get; private set; }
        public bool UsingMocks => Ports.UseMocks;

        public void Use(LobbyCase scenario)
        {
            Case = scenario;
            Ports.UseMocks = true;
            _timeline.Clear();
            Lobby.Clear();
            Results.Current = null;
            Connection.Set(NetworkState.Disconnected);
            Connection.CanResume = false;
            Api.SeedRooms(1);
            _dropped = false;
            Api.ListStatus = scenario == LobbyCase.JwtExpired ? 401 : 200;
        }

        public void UseLive()
        {
            _timeline.Clear();
            Ports.UseMocks = false;
        }

        public void Tick(float deltaTime)
        {
            var now = _clock();
            var dt = now - _lastClock;
            _lastClock = now;
            if (!Ports.UseMocks) return;
            if (TicksRunning && Connection.State == NetworkState.Joined)
            {
                _tickCarry += dt * LobbyCountdown.TickRate;
                var whole = (uint)_tickCarry;
                Lobby.ServerTick += whole;
                _tickCarry -= whole;
            }
            for (var i = 0; i < _timeline.Count; i++)
            {
                if (_timeline[i].at > now) continue;
                var act = _timeline[i].act;
                _timeline.RemoveAt(i--);
                act();
            }
        }

        private void After(float seconds, Action act) => _timeline.Add((_clock() + seconds, act));

        private void OnCommand(string command)
        {
            switch (command)
            {
                case nameof(ILobbyCommands.Join):
                    Connection.Set(NetworkState.Connecting);
                    After(JoinDelay, () =>
                    {
                        if (Case == LobbyCase.RoomFull) Connection.Set(NetworkState.Failed, DisconnectReason.RoomFull);
                        else if (Case == LobbyCase.WrongCode) Connection.Set(NetworkState.Failed, DisconnectReason.InvalidRoomCode);
                        else { Connection.Set(NetworkState.Joined); Lobby.RoomCode = Commands.LastCode; EnterLobby(); }
                    });
                    break;
                case nameof(ILobbyCommands.Cancel):
                case nameof(ILobbyCommands.Leave):
                    _timeline.Clear();
                    Connection.Set(NetworkState.Disconnected, DisconnectReason.UserRequested);
                    Connection.CanResume = false;
                    Lobby.Clear();
                    Results.Current = null;
                    break;
                case nameof(ILobbyCommands.Replay):
                    After(JoinDelay, EnterLobby);
                    break;
                case nameof(ILobbyCommands.Resume):
                    Connection.Set(NetworkState.Connecting);
                    if (Case == LobbyCase.Disconnect)
                        After(DropAfter, () => { Connection.Set(NetworkState.Joined); Connection.CanResume = false; EnterGame(); });
                    else if (Case == LobbyCase.DisconnectRejected)
                        After(DropAfter, () => Connection.Set(NetworkState.Failed, DisconnectReason.ServerClosed));
                    break; // DisconnectTimeout: no answer, the grace runs out
            }
        }

        private void EnterLobby()
        {
            _timeline.Clear();
            Results.Current = null;
            Lobby.MatchId = Guid.NewGuid();
            Lobby.OwnNetId = 0;
            Lobby.MinPlayers = 2;
            Lobby.MaxPlayers = 10;
            Lobby.SlotList.Clear();
            Lobby.SlotList.Add(new LobbySlot(0, "s0", Commands.LastName ?? "Bạn", true));
            Lobby.SlotList.Add(new LobbySlot(1, "s1", "Huy", false));
            Lobby.SlotList.Add(new LobbySlot(2, "s2", "Huy", false)); // same name, separate row (AC11)
            Lobby.SlotList.Add(new LobbySlot(3, "s3", "Mai", false));
            Lobby.PlayerCount = Lobby.SlotList.Count;
            Lobby.LoadError = null;
            Lobby.SetPhase(MatchPhase.Lobby, (int)LobbySeconds);
            After(LobbySeconds, () =>
            {
                Lobby.SetPhase(MatchPhase.Loading, (int)LoadingSeconds);
                if (Case == LobbyCase.LoadError) { Lobby.LoadError = "hash khác server (mock)"; return; }
                After(LoadingSeconds, EnterGame);
            });
        }

        private void EnterGame()
        {
            Lobby.SetPhase(MatchPhase.InGame, 0);
            var drops = Case == LobbyCase.Disconnect || Case == LobbyCase.DisconnectTimeout || Case == LobbyCase.DisconnectRejected;
            if (drops && !_dropped)
            {
                _dropped = true; // drop once per run
                After(DropAfter, () => { Connection.CanResume = true; Connection.Set(NetworkState.Disconnected, DisconnectReason.ConnectionLost); });
                return;
            }
            After(InGameSeconds, EnterEnding);
        }

        private void EnterEnding()
        {
            Lobby.SetPhase(MatchPhase.Ending, (int)EndingSeconds);
            var won = Case != LobbyCase.Replay;
            Results.Current = new MatchResultView
            {
                MatchId = Lobby.MatchId,
                Reason = MatchEndReason.LastAlive,
                WinnerNetId = won ? (byte)0 : (byte)3,
                ParticipantCount = 4,
                OwnNetId = 0,
                Participants = new[]
                {
                    Participant(0, won ? (byte)1 : (byte)2, Commands.LastName ?? "Bạn", 900),
                    Participant(1, 3, "Huy", 600),
                    Participant(2, 3, "Huy", 600), // tie: same rank as sent
                    Participant(3, won ? (byte)2 : (byte)1, "Mai", 1200)
                }
            };
            After(EndingSeconds, () => Lobby.SetPhase(MatchPhase.Waiting, 0));
        }

        private static MatchParticipantState Participant(byte netId, byte rank, string name, uint ticks) =>
            new MatchParticipantState { NetId = netId, Rank = rank, DisplayName = name, PlayerSessionId = "s" + netId, SurvivalTicks = ticks };
    }

    /// <summary>Lets the F9 panel swap UI-02 between the live ports and the mocks without rebuilding anything.</summary>
    public sealed class LobbyPortSwitch : ILobbySource, IMatchResultSource, IConnectionStateSource, ILobbyCommands,
        IAccountApi, IRoomsApi, IApiHealth, ILeaderboardApi
    {
        private readonly LobbyScenarios _mocks;
        private readonly ILobbySource _lobby;
        private readonly IMatchResultSource _results;
        private readonly IConnectionStateSource _connection;
        private readonly ILobbyCommands _commands;
        private readonly IAccountApi _account;
        private readonly IRoomsApi _rooms;
        private readonly IApiHealth _health;
        private readonly ILeaderboardApi _board;

        public LobbyPortSwitch(LobbyScenarios mocks, ILobbySource lobby, IMatchResultSource results,
            IConnectionStateSource connection, ILobbyCommands commands, IAccountApi account, IRoomsApi rooms,
            IApiHealth health, ILeaderboardApi board)
        {
            _mocks = mocks;
            _lobby = lobby; _results = results; _connection = connection; _commands = commands;
            _account = account; _rooms = rooms; _health = health; _board = board;
            UseMocks = lobby == null;
        }

        public bool UseMocks;

        private ILobbySource L => UseMocks || _lobby == null ? _mocks.Lobby : _lobby;
        private IMatchResultSource R => UseMocks || _results == null ? _mocks.Results : _results;
        private IConnectionStateSource C => UseMocks || _connection == null ? _mocks.Connection : _connection;
        private ILobbyCommands K => UseMocks || _commands == null ? _mocks.Commands : _commands;
        private IAccountApi A => UseMocks || _account == null ? _mocks.Api : _account;
        private IRoomsApi Ro => UseMocks || _rooms == null ? _mocks.Api : _rooms;
        private IApiHealth H => UseMocks || _health == null ? _mocks.Api : _health;
        private ILeaderboardApi B => UseMocks || _board == null ? _mocks.Api : _board;

        public bool HasPhase => L.HasPhase;
        public Guid MatchId => L.MatchId;
        public MatchPhase Phase => L.Phase;
        public int PlayerCount => L.PlayerCount;
        public int MinPlayers => L.MinPlayers;
        public int MaxPlayers => L.MaxPlayers;
        public uint PhaseEndTick => L.PhaseEndTick;
        public uint ServerTick => L.ServerTick;
        public IReadOnlyList<LobbySlot> Slots => L.Slots;
        public byte OwnNetId => L.OwnNetId;
        public string RoomCode => L.RoomCode;
        public bool ReadySupported => L.ReadySupported;
        public string LoadError => L.LoadError;
        public bool HasResult => R.HasResult;
        public MatchResultView Current => R.Current;
        public NetworkState State => C.State;
        public DisconnectReason LastReason => C.LastReason;
        public string Step => C.Step;
        public int RttMs => C.RttMs;
        public bool CanResume => C.CanResume;
        public void Join(string roomCode, string displayName) => K.Join(roomCode, displayName);
        public void Cancel() => K.Cancel();
        public void Leave() => K.Leave();
        public void Ready() => K.Ready();
        public void Replay() => K.Replay();
        public void Resume() => K.Resume();
        public void Register(string username, string password, Action<ApiResult<bool>> done) => A.Register(username, password, done);
        public void Login(string username, string password, Action<ApiResult<AuthToken>> done) => A.Login(username, password, done);
        public void List(string token, Action<ApiResult<RoomInfo[]>> done) => Ro.List(token, done);
        public void Create(string token, string name, int capacity, Action<ApiResult<RoomInfo>> done) => Ro.Create(token, name, capacity, done);
        public void Delete(string token, Guid id, Action<ApiResult<bool>> done) => Ro.Delete(token, id, done);
        public bool Configured => H.Configured;
        public void Check(Action<ApiResult<bool>> done) => H.Check(done);
        public void Get(Guid matchId, Action<ApiResult<LeaderboardEntry[]>> done) => B.Get(matchId, done);
    }
}
#endif
