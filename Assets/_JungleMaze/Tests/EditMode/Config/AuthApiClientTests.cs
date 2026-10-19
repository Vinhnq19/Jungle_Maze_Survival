using JungleBackend.Protocol;
using JungleMaze.App.Config;
using JungleMaze.App.Ports;
using JungleMaze.Infrastructure.Auth;
using NUnit.Framework;

namespace JungleMaze.Tests.Config
{
    /// <summary>Các nhánh bị chặn trước khi gửi HTTP: password không bao giờ rời máy qua kênh không an toàn.</summary>
    public sealed class AuthApiClientTests
    {
        private static LoginResult LoginWith(string api, ConnectionMode mode, string user = "player01", string password = "ClassDemo_123!")
        {
            var config = new ClientConfig(mode, api, "127.0.0.1", 7777, JoinAuthMode.Jwt, 1, 5000);
            LoginResult? result = null;
            new AuthApiClient(null).Login(config, user, password, r => result = r);
            Assert.IsTrue(result.HasValue, "Lỗi validate phải trả callback ngay, không gửi request");
            return result.Value;
        }

        [Test]
        public void RemoteHttp_IsRejectedBeforeSending()
        {
            var result = LoginWith("http://192.168.1.20:5000", ConnectionMode.LAN);
            Assert.IsFalse(result.Success);
            StringAssert.Contains("HTTPS", result.Error);
        }

        [Test]
        public void MissingApiUrl_IsRejected() => Assert.IsFalse(LoginWith(null, ConnectionMode.Local).Success);

        [Test]
        public void ShortUsername_IsRejected() => Assert.IsFalse(LoginWith("http://127.0.0.1:5000", ConnectionMode.Local, user: "ab").Success);

        [Test]
        public void EmptyPassword_IsRejected() => Assert.IsFalse(LoginWith("http://127.0.0.1:5000", ConnectionMode.Local, password: "").Success);
    }
}
