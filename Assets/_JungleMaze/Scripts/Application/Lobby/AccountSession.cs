using System;
using JungleMaze.Domain.Lobby;

namespace JungleMaze.App.Lobby
{
    /// <summary>
    /// Login/register for room management only (AC12, AC13, AC17). The JWT and its expiry live in this object and
    /// nowhere else (Q6); the password field is cleared the moment it is sent, success or not. Joining never uses it
    /// on the RoomCode profile (AC18).
    /// </summary>
    public sealed class AccountSession
    {
        private readonly IAccountApi _api;
        private readonly Func<DateTime> _utcNow;
        private AuthToken? _token;

        public AccountSession(IAccountApi api, Func<DateTime> utcNow)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        }

        public string Username = "", Password = "";
        public bool RegisterMode;
        public bool Busy { get; private set; }
        public string Message { get; private set; } = "";

        /// <summary>Raised once per successful login.</summary>
        public event Action LoggedIn;

        public bool IsValid => _token.HasValue && _utcNow() < _token.Value.ExpiresAtUtc;

        /// <summary>Null when there is no valid token; an expired one is dropped with the expiry notice (AC17).</summary>
        public string Token
        {
            get
            {
                if (IsValid) return _token.Value.AccessToken;
                if (_token.HasValue) Expire();
                return null;
            }
        }

        public string Validate()
        {
            var user = (Username ?? "").Trim();
            if (user.Length < LobbyLimits.UserMin || user.Length > LobbyLimits.UserMax) return "Tên đăng nhập cần 3–32 ký tự";
            var pass = Password ?? "";
            var min = RegisterMode ? LobbyLimits.PassMin : 1;
            if (pass.Length < min || pass.Length > LobbyLimits.PassMax)
                return RegisterMode ? "Mật khẩu cần 8–128 ký tự" : "Nhập mật khẩu";
            return null;
        }

        public bool Submit()
        {
            if (Busy) return false;
            var invalid = Validate();
            if (invalid != null) { Message = invalid; return false; }
            var user = Username.Trim();
            var pass = Password;
            Password = ""; // AC13: never kept after sending, even when the call fails
            Busy = true;
            Message = "";
            if (RegisterMode)
                _api.Register(user, pass, r =>
                {
                    if (!r.Ok) { Busy = false; Message = ApiMessages.For(ApiOp.Register, r); return; }
                    _api.Login(user, pass, OnLogin);
                });
            else _api.Login(user, pass, OnLogin);
            return true;
        }

        /// <summary>401 or passed expiry: drop the token, ask again, never resend (AC17).</summary>
        public void Expire()
        {
            _token = null;
            Message = ApiMessages.SessionExpired;
        }

        public void Logout()
        {
            _token = null;
            Message = "";
        }

        private void OnLogin(ApiResult<AuthToken> r)
        {
            Busy = false;
            if (!r.Ok || string.IsNullOrEmpty(r.Value.AccessToken))
            {
                Message = r.Ok ? ApiMessages.AccountUnreachable : ApiMessages.For(ApiOp.Login, r);
                return;
            }
            _token = r.Value;
            Message = "";
            RegisterMode = false;
            LoggedIn?.Invoke();
        }
    }
}
