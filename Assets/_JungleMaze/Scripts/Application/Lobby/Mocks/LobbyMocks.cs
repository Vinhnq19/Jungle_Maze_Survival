#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.Domain.Lobby;
using JungleMaze.Domain.Network;

namespace JungleMaze.App.Lobby.Mocks
{
    /// <summary>Dev-only N1: every field settable; ticks move only when the test or scenario moves them (AC21).</summary>
    public sealed class MockLobbySource : ILobbySource
    {
        public readonly List<LobbySlot> SlotList = new List<LobbySlot>();
        public bool HasPhase { get; set; }
        public Guid MatchId { get; set; }
        public MatchPhase Phase { get; set; }
        public int PlayerCount { get; set; }
        public int MinPlayers { get; set; } = 6;
        public int MaxPlayers { get; set; } = 10;
        public uint PhaseEndTick { get; set; }
        public uint ServerTick { get; set; }
        public byte OwnNetId { get; set; } = 255;
        public string RoomCode { get; set; } = "";
        public bool ReadySupported { get; set; }
        public string LoadError { get; set; }
        public IReadOnlyList<LobbySlot> Slots => SlotList;

        public void SetPhase(MatchPhase phase, int seconds)
        {
            HasPhase = true;
            Phase = phase;
            PhaseEndTick = seconds > 0 ? ServerTick + (uint)(seconds * LobbyCountdown.TickRate) : ServerTick;
        }

        public void Clear()
        {
            HasPhase = false;
            MatchId = Guid.Empty;
            SlotList.Clear();
            PlayerCount = 0;
            OwnNetId = 255;
            LoadError = null;
        }
    }

    public sealed class MockMatchResultSource : IMatchResultSource
    {
        public bool HasResult => Current != null;
        public MatchResultView Current { get; set; }
    }

    public sealed class MockConnectionStateSource : IConnectionStateSource
    {
        public NetworkState State { get; set; }
        public DisconnectReason LastReason { get; set; }
        public string Step { get; set; } = "UDP";
        public int RttMs { get; set; } = -1;
        public bool CanResume { get; set; }

        public void Set(NetworkState state, DisconnectReason reason = DisconnectReason.None)
        {
            State = state;
            LastReason = reason;
        }
    }

    /// <summary>Counts every command (AC9, AC22, AC32, AC39, AC42) and reports it to the scenario script.</summary>
    public sealed class MockLobbyCommands : ILobbyCommands
    {
        public int Joins, Cancels, Leaves, Readies, Replays, Resumes;
        public string LastCode, LastName;
        public event Action<string> Called;

        public void Join(string roomCode, string displayName)
        {
            Joins++;
            LastCode = roomCode;
            LastName = displayName;
            Called?.Invoke(nameof(Join));
        }

        public void Cancel() { Cancels++; Called?.Invoke(nameof(Cancel)); }
        public void Leave() { Leaves++; Called?.Invoke(nameof(Leave)); }
        public void Ready() { Readies++; Called?.Invoke(nameof(Ready)); }
        public void Replay() { Replays++; Called?.Invoke(nameof(Replay)); }
        public void Resume() { Resumes++; Called?.Invoke(nameof(Resume)); }
    }

    /// <summary>
    /// Dev-only API with one switch per status code (400, 401, 403, 404, four 409s, 429, 503, timeout, not
    /// configured). <see cref="Deferred"/> holds answers until <see cref="Flush"/> so tests can press 5 times while a
    /// request is in flight.
    /// </summary>
    public sealed class MockLobbyApi : IAccountApi, IRoomsApi, IApiHealth, ILeaderboardApi
    {
        private readonly List<Action> _pending = new List<Action>();
        private readonly List<RoomInfo> _rooms = new List<RoomInfo>();
        private int _codeSeed;

        public bool Configured { get; set; } = true;
        public bool Deferred;
        public Func<DateTime> UtcNow = () => DateTime.UtcNow;
        public double TokenLifetimeSeconds = 3600;

        public int LoginStatus = 200, RegisterStatus = 201, ListStatus = 200, CreateStatus = 201, DeleteStatus = 204;
        public int HealthStatus = 200, LeaderboardStatus = 200;
        public string CreateError, DeleteError;
        public LeaderboardEntry[] Board = Array.Empty<LeaderboardEntry>();
        public int Logins, Registers, Lists, Creates, Deletes, Healths, Boards;
        /// <summary>Every token handed out; tests assert none of them reaches a log or file (AC13).</summary>
        public readonly List<string> IssuedTokens = new List<string>();

        public MockLobbyApi() => SeedRooms(0);

        public IReadOnlyList<RoomInfo> Rooms => _rooms;

        /// <summary>Default room plus <paramref name="own"/> rooms of the logged-in account.</summary>
        public void SeedRooms(int own)
        {
            _rooms.Clear();
            _rooms.Add(new RoomInfo(Guid.NewGuid(), "Phòng mặc định", null, true, 10, 3, 0));
            for (var i = 0; i < own; i++) _rooms.Add(NewRoom("Phòng " + (i + 1), 10));
        }

        public void Flush()
        {
            var calls = _pending.ToArray();
            _pending.Clear();
            foreach (var call in calls) call();
        }

        public void Register(string username, string password, Action<ApiResult<bool>> done)
        {
            Registers++;
            Answer(() => done(RegisterStatus / 100 == 2 ? ApiResult<bool>.Success(true, RegisterStatus) : Fail<bool>(RegisterStatus, null)));
        }

        public void Login(string username, string password, Action<ApiResult<AuthToken>> done)
        {
            Logins++;
            Answer(() =>
            {
                if (LoginStatus != 200) { done(Fail<AuthToken>(LoginStatus, null)); return; }
                var token = "mock-jwt-" + Guid.NewGuid().ToString("N");
                IssuedTokens.Add(token);
                done(ApiResult<AuthToken>.Success(new AuthToken(token, UtcNow().AddSeconds(TokenLifetimeSeconds))));
            });
        }

        public void List(string token, Action<ApiResult<RoomInfo[]>> done)
        {
            Lists++;
            Answer(() => done(ListStatus == 200 ? ApiResult<RoomInfo[]>.Success(_rooms.ToArray()) : Fail<RoomInfo[]>(ListStatus, null)));
        }

        public void Create(string token, string name, int capacity, Action<ApiResult<RoomInfo>> done)
        {
            Creates++;
            Answer(() =>
            {
                if (CreateStatus != 201) { done(Fail<RoomInfo>(CreateStatus, CreateError)); return; }
                var room = NewRoom(name, capacity);
                _rooms.Add(room);
                done(ApiResult<RoomInfo>.Success(room, 201));
            });
        }

        public void Delete(string token, Guid id, Action<ApiResult<bool>> done)
        {
            Deletes++;
            Answer(() =>
            {
                if (DeleteStatus != 204) { done(Fail<bool>(DeleteStatus, DeleteError)); return; }
                _rooms.RemoveAll(r => r.Id == id);
                done(ApiResult<bool>.Success(true, 204));
            });
        }

        public void Check(Action<ApiResult<bool>> done)
        {
            Healths++;
            Answer(() => done(HealthStatus == 200 ? ApiResult<bool>.Success(true) : Fail<bool>(HealthStatus, null)));
        }

        public void Get(Guid matchId, Action<ApiResult<LeaderboardEntry[]>> done)
        {
            Boards++;
            Answer(() => done(LeaderboardStatus == 200 ? ApiResult<LeaderboardEntry[]>.Success(Board) : Fail<LeaderboardEntry[]>(LeaderboardStatus, null)));
        }

        /// <summary>Status 0 means timeout (no answer).</summary>
        private static ApiResult<T> Fail<T>(int status, string code) =>
            ApiResult<T>.Fail(status, status == 0 ? ApiResult<T>.Timeout : code);

        private void Answer(Action call)
        {
            if (Deferred) _pending.Add(call);
            else call();
        }

        private RoomInfo NewRoom(string name, int capacity)
        {
            var code = new char[RoomCodeRules.Length];
            for (var i = 0; i < code.Length; i++)
                code[i] = RoomCodeRules.Alphabet[(++_codeSeed * 7 + i * 13) % RoomCodeRules.Alphabet.Length];
            return new RoomInfo(Guid.NewGuid(), name, new string(code), false, capacity, 0, 0);
        }
    }
}
#endif
