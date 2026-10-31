using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Lobby;
using JungleMaze.App.Lobby.Mocks;
using JungleMaze.App.Ports;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Network;
using JungleMaze.Tests.MatchUi;

namespace JungleMaze.Tests.Lobby
{
    /// <summary>A <see cref="LobbyFlow"/> over the UI-02 mocks, a fake clock and a fake UTC clock (no server, no API).</summary>
    internal sealed class LobbyRig : IDisposable
    {
        public readonly FakeClock Clock = new FakeClock { Now = 10 };
        public DateTime Utc = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);
        public readonly MockLobbySource Lobby = new MockLobbySource();
        public readonly MockMatchResultSource Results = new MockMatchResultSource();
        public readonly MockConnectionStateSource Connection = new MockConnectionStateSource();
        public readonly MockLobbyCommands Commands = new MockLobbyCommands();
        public readonly MockLobbyApi Api = new MockLobbyApi();
        public readonly EventBus Bus = new EventBus();
        public readonly FakeHistory History = new FakeHistory();
        public readonly LobbyOptions Options = new LobbyOptions();
        public readonly AccountSession Account;
        public readonly JoinController Join;
        public readonly RoomListModel Rooms;
        public readonly LobbyFlow Flow;
        public string Cause;

        public LobbyRig(Action<LobbyOptions> configure = null)
        {
            configure?.Invoke(Options);
            Api.UtcNow = () => Utc;
            Account = new AccountSession(Api, () => Utc);
            Join = new JoinController(Commands, Clock.Read, Options, "Huy", "");
            Rooms = new RoomListModel(Api, Account);
            Flow = new LobbyFlow(Lobby, Results, Connection, Commands, Join, Account, Rooms,
                new HistoryModel(History, () => Account.IsValid ? Account.Token : null, () => Api.Configured, Options, 200),
                new LeaderboardModel(Api), new ApiStatusModel(Api), Clock.Read, Options, Bus, () => Cause);
        }

        public void Tick(int frames = 1)
        {
            for (var i = 0; i < frames; i++) Flow.Tick(1f / 60f);
        }

        /// <summary>Menu → form → connecting → joined with a Lobby phase and a 15 s countdown.</summary>
        public void JoinLobby(string code = "K7M2Q9", int countdown = 15)
        {
            Flow.OpenJoin();
            Join.SetCode(code);
            Flow.SubmitJoin();
            Connection.Set(NetworkState.Connecting);
            Tick();
            Connection.Set(NetworkState.Joined);
            Lobby.MatchId = Guid.NewGuid();
            Lobby.OwnNetId = 0;
            Lobby.ServerTick = 1000;
            Lobby.SetPhase(MatchPhase.Lobby, countdown);
            Tick();
        }

        public void Phase(MatchPhase phase, int seconds = 0)
        {
            Lobby.SetPhase(phase, seconds);
            Tick();
        }

        public void InGame()
        {
            JoinLobby();
            Phase(MatchPhase.Loading, 30);
            Phase(MatchPhase.InGame);
        }

        public MatchResultView Result(MatchEndReason reason, byte winner, params (byte netId, byte rank, string name)[] players)
        {
            var list = new MatchParticipantState[players.Length];
            for (var i = 0; i < players.Length; i++)
                list[i] = new MatchParticipantState
                {
                    NetId = players[i].netId, Rank = players[i].rank, DisplayName = players[i].name,
                    PlayerSessionId = "s" + players[i].netId, Kills = 2, SurvivalTicks = 30u * 75
                };
            return new MatchResultView
            {
                MatchId = Lobby.MatchId, Reason = reason, WinnerNetId = winner, OwnNetId = Lobby.OwnNetId,
                ParticipantCount = (byte)players.Length, Participants = list
            };
        }

        public void Dispose() => Flow.Dispose();
    }

    /// <summary><see cref="IPlayerHistory"/> with switchable outcomes: ok, failure status, hang.</summary>
    internal sealed class FakeHistory : IPlayerHistory
    {
        public bool Ready = true, Fail, Hang;
        public string LastToken;
        public int Refreshes;
        public List<PlayerMatchHistory> Data = new List<PlayerMatchHistory>();

        public string Status { get; private set; } = "";
        public bool IsReady => Ready;
        public IReadOnlyList<PlayerMatchHistory> Rows { get; private set; } = Array.Empty<PlayerMatchHistory>();

        public Task<bool> EnsureGuestAsync(string baseUrl, CancellationToken cancellation = default) => Task.FromResult(Ready);

        public Task<string> TicketAsync(Guid attemptId, string roomCode, string accountToken = null, CancellationToken cancellation = default) =>
            Task.FromResult<string>(null);

        public Task RefreshHistoryAsync(string accountToken = null, CancellationToken cancellation = default)
        {
            Refreshes++;
            LastToken = accountToken;
            if (Hang) return new TaskCompletionSource<bool>().Task;
            Status = Fail ? "Lịch sử online không khả dụng" : "Lịch sử đã cập nhật";
            Rows = Fail ? (IReadOnlyList<PlayerMatchHistory>)Array.Empty<PlayerMatchHistory>() : Data;
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }
}
