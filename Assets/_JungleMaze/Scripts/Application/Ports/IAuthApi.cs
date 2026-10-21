using System;
using JungleMaze.App.Config;

namespace JungleMaze.App.Ports
{
    public readonly struct LoginResult
    {
        public readonly bool Success;
        public readonly string AccessToken;
        public readonly string Error;

        private LoginResult(bool success, string token, string error)
        {
            Success = success;
            AccessToken = token;
            Error = error;
        }

        public static LoginResult Ok(string token) => new LoginResult(true, token, null);
        public static LoginResult Fail(string error) => new LoginResult(false, null, error);
    }

    /// <summary>Đăng nhập Account API lấy JWT cho authMode=Jwt. Callback chạy ở main thread.</summary>
    public interface IAuthApi
    {
        void Login(ClientConfig config, string username, string password, Action<LoginResult> onComplete);
    }
}
