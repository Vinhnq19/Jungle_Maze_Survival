using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JungleMaze.App.Ports;

namespace JungleMaze.App.Lobby
{
    /// <summary>
    /// Personal history (AC35, AC36) over the existing <see cref="IPlayerHistory"/>: JWT when logged in, otherwise the
    /// verified guest credential, never a nickname. Every failure becomes "tạm ngừng" with a retry; nothing blocks
    /// joining. Rows are read-only and never cached to disk (G-12).
    /// </summary>
    public sealed class HistoryModel
    {
        private const string ClientUnavailable = "Lịch sử online không khả dụng"; // GuestHistoryClient's failure status

        private readonly IPlayerHistory _history;
        private readonly Func<string> _accountToken;
        private readonly Func<bool> _apiConfigured;
        private readonly LobbyOptions _options;
        private readonly int _timeoutMs;

        public HistoryModel(IPlayerHistory history, Func<string> accountToken, Func<bool> apiConfigured,
            LobbyOptions options, int timeoutMs = 10000)
        {
            _history = history;
            _accountToken = accountToken ?? (() => null);
            _apiConfigured = apiConfigured ?? (() => false);
            _options = options ?? LobbyOptions.Default;
            _timeoutMs = timeoutMs;
        }

        public ListState State { get; private set; }
        public string Message { get; private set; } = "";
        public IReadOnlyList<PlayerMatchHistory> Rows { get; private set; } = Array.Empty<PlayerMatchHistory>();

        /// <summary>"Khách: lịch sử chưa được lưu" until the guest profile is verified (AC36).</summary>
        public string IdentityLabel => _accountToken() != null ? "Tài khoản"
            : _history != null && _history.IsReady ? "Khách" : ApiMessages.GuestUnsaved;

        public async Task Refresh()
        {
            if (State == ListState.Loading) return;
            var token = _accountToken();
            if (!_options.HistoryEnabled || !_apiConfigured() || _history == null || token == null && !_history.IsReady)
            {
                Set(ListState.Unavailable, ApiMessages.HistoryDown);
                return;
            }
            Set(ListState.Loading, "");
            try
            {
                using (var cts = new CancellationTokenSource(_timeoutMs))
                {
                    var work = _history.RefreshHistoryAsync(token, cts.Token);
                    if (!work.IsCompleted) await Task.WhenAny(work, Task.Delay(_timeoutMs));
                    if (!work.IsCompleted || work.IsFaulted || work.IsCanceled
                        || _history.Status == ClientUnavailable)
                    {
                        Set(ListState.Unavailable, ApiMessages.HistoryDown);
                        return;
                    }
                }
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                Set(ListState.Unavailable, ApiMessages.HistoryDown);
                return;
            }
            Rows = _history.Rows ?? Array.Empty<PlayerMatchHistory>();
            if (Rows.Count == 0) Set(ListState.Empty, ApiMessages.HistoryEmpty);
            else Set(ListState.Ready, "");
        }

        /// <summary>Local time, rank, survival, reason.</summary>
        public static string Line(PlayerMatchHistory row) =>
            row.EndedAt.ToLocalTime().ToString("dd/MM HH:mm") + " · Hạng #" + row.Rank + " · Sống sót "
            + Domain.Lobby.ResultRow.Clock((float)row.SurvivalSeconds) + " · " + (row.Reason ?? "");

        private void Set(ListState state, string message)
        {
            State = state;
            Message = message;
            if (state != ListState.Ready) Rows = Array.Empty<PlayerMatchHistory>();
        }
    }

    /// <summary>Leaderboard of one stored match (AC37). 404 means "not stored yet", not an error.</summary>
    public sealed class LeaderboardModel
    {
        private readonly ILeaderboardApi _api;

        public LeaderboardModel(ILeaderboardApi api) => _api = api;

        public ListState State { get; private set; }
        public string Message { get; private set; } = "";
        public Guid MatchId { get; private set; }
        public LeaderboardEntry[] Rows { get; private set; } = Array.Empty<LeaderboardEntry>();

        public void Load(Guid matchId)
        {
            MatchId = matchId;
            Rows = Array.Empty<LeaderboardEntry>();
            if (_api == null || matchId == Guid.Empty) { State = ListState.Unavailable; Message = "Bảng xếp hạng online tạm ngừng"; return; }
            State = ListState.Loading;
            Message = "";
            _api.Get(matchId, r =>
            {
                if (r.Ok)
                {
                    Rows = r.Value ?? Array.Empty<LeaderboardEntry>();
                    State = Rows.Length == 0 ? ListState.Empty : ListState.Ready;
                    Message = Rows.Length == 0 ? ApiMessages.LeaderboardEmpty : "";
                    return;
                }
                State = r.HttpStatus == 404 ? ListState.Empty : ListState.Unavailable;
                Message = ApiMessages.For(ApiOp.Leaderboard, r);
            });
        }
    }

    /// <summary>Menu status line (AC3). Join stays enabled in every state.</summary>
    public sealed class ApiStatusModel
    {
        private readonly IApiHealth _health;

        public ApiStatusModel(IApiHealth health) => _health = health;

        public string Text { get; private set; } = "";
        public bool Ready { get; private set; }
        public bool Checking { get; private set; }

        public void Check()
        {
            if (Checking) return;
            if (_health == null || !_health.Configured) { Text = ApiMessages.NoApi; Ready = false; return; }
            Checking = true;
            Text = "Đang kiểm tra API…";
            _health.Check(r =>
            {
                Checking = false;
                Ready = r.Ok;
                Text = r.Ok ? ApiMessages.ApiReady : ApiMessages.ApiDown;
            });
        }
    }
}
