using System;

namespace JungleMaze.App.Lobby
{
    /// <summary>
    /// One HTTP answer. <see cref="HttpStatus"/> 0 means no answer: <see cref="ErrorCode"/> is then
    /// <see cref="Network"/>, <see cref="Timeout"/> or <see cref="NotConfigured"/>. Messages are built by
    /// <see cref="ApiMessages"/>, never from the body text.
    /// </summary>
    public readonly struct ApiResult<T>
    {
        public const string Network = "Network", Timeout = "Timeout", NotConfigured = "NotConfigured";

        public readonly int HttpStatus;
        public readonly string ErrorCode;
        public readonly T Value;

        public ApiResult(int httpStatus, string errorCode = null, T value = default)
        {
            HttpStatus = httpStatus;
            ErrorCode = errorCode ?? "";
            Value = value;
        }

        public bool Ok => HttpStatus >= 200 && HttpStatus < 300;

        public static ApiResult<T> Success(T value, int status = 200) => new ApiResult<T>(status, null, value);
        public static ApiResult<T> Fail(int status, string code = null) => new ApiResult<T>(status, code);
    }

    /// <summary>JWT kept in memory only (Q6).</summary>
    public readonly struct AuthToken
    {
        public readonly string AccessToken;
        public readonly DateTime ExpiresAtUtc;

        public AuthToken(string accessToken, DateTime expiresAtUtc)
        {
            AccessToken = accessToken;
            ExpiresAtUtc = expiresAtUtc;
        }

        /// <summary>Never prints the token.</summary>
        public override string ToString() => "AuthToken(expires " + ExpiresAtUtc.ToString("u") + ")";
    }

    /// <summary><c>RoomView</c> of NET-01. <see cref="RoomCode"/> is null for the default room.</summary>
    public readonly struct RoomInfo
    {
        public readonly Guid Id;
        public readonly string Name, RoomCode;
        public readonly bool IsDefault;
        public readonly int MaxPlayers, Active, Pending;

        public RoomInfo(Guid id, string name, string roomCode, bool isDefault, int maxPlayers, int active, int pending)
        {
            Id = id;
            Name = name ?? "";
            RoomCode = string.IsNullOrEmpty(roomCode) ? null : roomCode;
            IsDefault = isDefault;
            MaxPlayers = maxPlayers;
            Active = active;
            Pending = pending;
        }
    }

    /// <summary>One leaderboard row of a stored match; keyed by session, not by name (AC37).</summary>
    public readonly struct LeaderboardEntry
    {
        public readonly string SessionId, Name;
        public readonly int Rank, Kills;
        public readonly double SurvivalSeconds;

        public LeaderboardEntry(string sessionId, string name, int rank, int kills, double survivalSeconds)
        {
            SessionId = sessionId ?? "";
            Name = name ?? "";
            Rank = rank;
            Kills = kills;
            SurvivalSeconds = survivalSeconds;
        }
    }

    /// <summary>N5: <c>POST api/auth/register</c>, <c>POST api/auth/login</c>.</summary>
    public interface IAccountApi
    {
        void Register(string username, string password, Action<ApiResult<bool>> done);
        void Login(string username, string password, Action<ApiResult<AuthToken>> done);
    }

    /// <summary>N5: <c>GET/POST api/rooms</c>, <c>DELETE api/rooms/{id}</c>. Never retried by the client (G-4).</summary>
    public interface IRoomsApi
    {
        void List(string token, Action<ApiResult<RoomInfo[]>> done);
        void Create(string token, string name, int capacity, Action<ApiResult<RoomInfo>> done);
        void Delete(string token, Guid id, Action<ApiResult<bool>> done);
    }

    /// <summary><c>GET /health</c> with a 5 s timeout (AC3).</summary>
    public interface IApiHealth
    {
        bool Configured { get; }
        void Check(Action<ApiResult<bool>> done);
    }

    /// <summary>N6: <c>GET api/matches/{matchId}/leaderboard</c> (anonymous).</summary>
    public interface ILeaderboardApi
    {
        void Get(Guid matchId, Action<ApiResult<LeaderboardEntry[]>> done);
    }
}
