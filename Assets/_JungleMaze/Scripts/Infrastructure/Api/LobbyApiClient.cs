using System;
using System.Text;
using JungleMaze.App.Config;
using JungleMaze.App.Lobby;
using JungleMaze.Core.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace JungleMaze.Infrastructure.Api
{
    /// <summary>
    /// UI-02 HTTP for NET-01/NET-02 endpoints that had no client yet: register, login with <c>expiresAtUtc</c> (T-7),
    /// rooms, <c>/health</c> and one match's leaderboard. Same URL rule as <c>AuthApiClient</c>
    /// (<see cref="ClientConfig.ValidateApiUrl"/>: HTTPS, loopback or Tailscale), no redirects, 5 s for health and
    /// 10 s for the rest (G-4), never retried. Logs only the operation and HTTP status, never a token or password.
    /// </summary>
    public sealed class LobbyApiClient : IAccountApi, IRoomsApi, IApiHealth, ILeaderboardApi
    {
        private const int HealthTimeout = 5, Timeout = 10;
        private readonly Func<ClientConfig> _config;
        private readonly ILog _log;

        public LobbyApiClient(Func<ClientConfig> config, ILog log)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _log = log ?? NullLog.Instance;
        }

        public bool Configured => !string.IsNullOrWhiteSpace(_config()?.ApiBaseUrl);

        public void Register(string username, string password, Action<ApiResult<bool>> done) =>
            Send("POST", "api/auth/register", null, Credentials(username, password), Timeout, "register",
                r => done(new ApiResult<bool>(r.Status, r.Code, r.Status / 100 == 2)));

        public void Login(string username, string password, Action<ApiResult<AuthToken>> done) =>
            Send("POST", "api/auth/login", null, Credentials(username, password), Timeout, "login", r =>
            {
                if (r.Status != 200) { done(ApiResult<AuthToken>.Fail(r.Status, r.Code)); return; }
                var token = r.Json?.Value<string>("accessToken");
                // DateTimeOffset keeps any offset the API sends ("Z" today; "+07:00" stays the same instant, review G2).
                var expires = r.Json?.Value<DateTimeOffset?>("expiresAtUtc")?.UtcDateTime ?? DateTime.UtcNow.AddMinutes(30);
                done(string.IsNullOrWhiteSpace(token)
                    ? ApiResult<AuthToken>.Fail(502, "BadResponse")
                    : ApiResult<AuthToken>.Success(new AuthToken(token, expires)));
            });

        public void List(string token, Action<ApiResult<RoomInfo[]>> done) =>
            Send("GET", "api/rooms", token, null, Timeout, "rooms.list", r =>
            {
                if (r.Status != 200) { done(ApiResult<RoomInfo[]>.Fail(r.Status, r.Code)); return; }
                var array = r.Body is JArray a ? a : new JArray();
                var rooms = new RoomInfo[array.Count];
                for (var i = 0; i < rooms.Length; i++) rooms[i] = ReadRoom((JObject)array[i]);
                done(ApiResult<RoomInfo[]>.Success(rooms));
            });

        public void Create(string token, string name, int capacity, Action<ApiResult<RoomInfo>> done) =>
            Send("POST", "api/rooms", token, new JObject { ["name"] = name, ["maxPlayers"] = capacity }, Timeout, "rooms.create",
                r => done(r.Status == 201 && r.Json != null ? ApiResult<RoomInfo>.Success(ReadRoom(r.Json), 201) : ApiResult<RoomInfo>.Fail(r.Status, r.Code)));

        public void Delete(string token, Guid id, Action<ApiResult<bool>> done) =>
            Send("DELETE", "api/rooms/" + id, token, null, Timeout, "rooms.delete",
                r => done(new ApiResult<bool>(r.Status, r.Code, r.Status == 204)));

        public void Check(Action<ApiResult<bool>> done) =>
            Send("GET", "health", null, null, HealthTimeout, "health", r => done(new ApiResult<bool>(r.Status, r.Code, r.Status == 200)));

        public void Get(Guid matchId, Action<ApiResult<LeaderboardEntry[]>> done) =>
            Send("GET", "api/matches/" + matchId + "/leaderboard", null, null, Timeout, "leaderboard", r =>
            {
                if (r.Status != 200 || r.Json == null) { done(ApiResult<LeaderboardEntry[]>.Fail(r.Status, r.Code)); return; }
                var players = r.Json["players"] as JArray ?? new JArray();
                var rows = new LeaderboardEntry[players.Count];
                for (var i = 0; i < rows.Length; i++)
                {
                    var p = (JObject)players[i];
                    rows[i] = new LeaderboardEntry(p.Value<string>("playerSessionId"), p.Value<string>("displayName"),
                        p.Value<int>("rank"), p.Value<int>("kills"), p.Value<double>("survivalSeconds"));
                }
                done(ApiResult<LeaderboardEntry[]>.Success(rows));
            });

        private static JObject Credentials(string username, string password) =>
            new JObject { ["username"] = username, ["password"] = password };

        private static RoomInfo ReadRoom(JObject o) => new RoomInfo(
            Guid.TryParse(o.Value<string>("id"), out var id) ? id : Guid.Empty, o.Value<string>("name"), o.Value<string>("roomCode"),
            o.Value<bool>("isDefault"), o.Value<int>("maxPlayers"), o.Value<int>("activePlayers"), o.Value<int>("pendingPlayers"));

        private readonly struct Reply
        {
            public readonly int Status;
            public readonly string Code;
            public readonly JToken Body;
            public Reply(int status, string code, JToken body) { Status = status; Code = code; Body = body; }
            public JObject Json => Body as JObject;
        }

        private void Send(string method, string path, string token, JObject body, int timeout, string op, Action<Reply> done)
        {
            var config = _config();
            if (config == null || string.IsNullOrWhiteSpace(config.ApiBaseUrl)) { done(new Reply(0, ApiResult<bool>.NotConfigured, null)); return; }
            if (config.ValidateApiUrl(out var baseUri) != null) { done(new Reply(0, ApiResult<bool>.NotConfigured, null)); return; }
            var request = new UnityWebRequest(baseUri.AbsoluteUri.TrimEnd('/') + "/" + path, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = timeout,
                redirectLimit = 0
            };
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Formatting.None)));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", "Bearer " + token);
            request.SendWebRequest().completed += _ =>
            {
                Reply reply;
                try { reply = Read(request); }
                finally { request.Dispose(); }
                // A down API is a normal state for guests (AC6): health is info, the account/room calls warn.
                var line = "[LobbyApi] " + op + " → " + (reply.Status == 0 ? reply.Code : "HTTP " + reply.Status);
                if (reply.Status / 100 != 2) { if (op == "health") _log.Info(line); else _log.Warn(line); }
                done(reply);
            };
        }

        private static Reply Read(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.ConnectionError)
                return new Reply(0, request.error != null && request.error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
                    ? ApiResult<bool>.Timeout : ApiResult<bool>.Network, null);
            var status = (int)request.responseCode;
            JToken parsed = null;
            var text = request.downloadHandler?.text;
            if (!string.IsNullOrWhiteSpace(text))
                try { parsed = JToken.Parse(text); }
                catch (JsonException) { }
            var code = (parsed as JObject)?.Value<string>("error") ?? "";
            return new Reply(status, code, parsed);
        }
    }
}
