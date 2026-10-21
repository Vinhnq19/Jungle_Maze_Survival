using System;
using System.Text;
using JungleBackend.Protocol;
using JungleMaze.App.Config;
using JungleMaze.App.Ports;
using JungleMaze.Core.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace JungleMaze.Infrastructure.Auth
{
    /// <summary>
    /// POST {apiBaseUrl}/api/auth/login như AccountLogin của NetSmokeClient. URL được kiểm (HTTPS/loopback/Tailscale)
    /// trước khi gửi password; không redirect, không log password/token. Token chỉ trả qua callback, giữ trong RAM.
    /// </summary>
    public sealed class AuthApiClient : IAuthApi
    {
        private const int TimeoutSeconds = 10;
        private readonly ILog _log;

        public AuthApiClient(ILog log) => _log = log ?? NullLog.Instance;

        public void Login(ClientConfig config, string username, string password, Action<LoginResult> onComplete)
        {
            if (onComplete == null) throw new ArgumentNullException(nameof(onComplete));
            if (config == null) { onComplete(LoginResult.Fail("Chưa có config.")); return; }
            var urlError = config.ValidateApiUrl(out var baseUri);
            if (urlError != null) { onComplete(LoginResult.Fail(urlError)); return; }
            username = username?.Trim();
            if (string.IsNullOrEmpty(username) || username.Length < 3 || username.Length > 32)
                { onComplete(LoginResult.Fail("Tên đăng nhập cần 3–32 ký tự.")); return; }
            if (string.IsNullOrEmpty(password) || password.Length > 128)
                { onComplete(LoginResult.Fail("Mật khẩu không hợp lệ.")); return; }

            var endpoint = baseUri.AbsoluteUri.TrimEnd('/') + "/api/auth/login";
            var body = new JObject { ["username"] = username, ["password"] = password }.ToString(Formatting.None);
            var request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = TimeoutSeconds,
                redirectLimit = 0
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SendWebRequest().completed += _ =>
            {
                try { onComplete(ReadResult(request)); }
                finally { request.Dispose(); }
            };
        }

        private LoginResult ReadResult(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                _log.Warn("[Auth] Không kết nối được Account API.");
                return LoginResult.Fail("Không kết nối được Account API.");
            }
            if (request.responseCode == 401) return LoginResult.Fail("Tài khoản hoặc mật khẩu không đúng.");
            if (request.responseCode == 429) return LoginResult.Fail("Đăng nhập quá nhiều lần, thử lại sau.");
            if (request.responseCode != 200) return LoginResult.Fail($"Account API trả HTTP {request.responseCode}.");
            try
            {
                var token = JObject.Parse(request.downloadHandler.text).Value<string>("accessToken");
                if (string.IsNullOrWhiteSpace(token) || token.Length > NetworkProtocol.MaxTokenChars)
                    return LoginResult.Fail("API không trả JWT hợp lệ.");
                return LoginResult.Ok(token);
            }
            catch (JsonException) { return LoginResult.Fail("Response login không phải JSON hợp lệ."); }
        }
    }
}
