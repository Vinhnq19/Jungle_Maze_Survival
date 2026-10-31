using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Lobby;
using JungleMaze.Domain.Lobby;
using JungleMaze.Domain.Network;
using NUnit.Framework;

namespace JungleMaze.Tests.Lobby
{
    /// <summary>AC5 (room code and name rules), T-3 drift, AC7/AC8 reject text, AC21 countdown, AC40 home matrix.</summary>
    public sealed class LobbyRulesTests
    {
        [TestCase("", JoinFieldError.Empty)]
        [TestCase("   ", JoinFieldError.Empty)]
        [TestCase("K7M2Q", JoinFieldError.TooShort)]
        [TestCase("K7M2Q9A", JoinFieldError.TooLong)]
        [TestCase("k7m2q9", JoinFieldError.None)]
        [TestCase(" K7M2Q9 ", JoinFieldError.None)]
        [TestCase("K7M2QI", JoinFieldError.BadChar)]
        [TestCase("K7M2QO", JoinFieldError.BadChar)]
        [TestCase("K7M2Q0", JoinFieldError.BadChar)]
        [TestCase("K7M2Q1", JoinFieldError.BadChar)]
        [TestCase("K7M-Q9", JoinFieldError.BadChar)]
        [TestCase("K7M2😀", JoinFieldError.BadChar)]
        [TestCase("ABCDEF", JoinFieldError.None)]
        public void RoomCode_Validate(string code, JoinFieldError expected) =>
            Assert.AreEqual(expected, RoomCodeRules.Validate(code));

        [Test]
        public void RoomCode_NormalizeUpperCases() => Assert.AreEqual("K7M2Q9", RoomCodeRules.Normalize(" k7m2q9 "));

        [TestCase("Huy", JoinFieldError.None)]
        [TestCase("   ", JoinFieldError.Empty)]
        [TestCase("  Huy  ", JoinFieldError.None)]
        public void Name_Validate(string name, JoinFieldError expected) => Assert.AreEqual(expected, RoomCodeRules.ValidateName(name));

        [Test]
        public void Name_32Ok_33TooLong()
        {
            Assert.AreEqual(JoinFieldError.None, RoomCodeRules.ValidateName(new string('a', 32)));
            Assert.AreEqual(JoinFieldError.TooLong, RoomCodeRules.ValidateName(new string('a', 33)));
        }

        /// <summary>T-3: the client copies must equal the Backend source (read as text; Backend is not compiled here).</summary>
        [Test]
        public void LobbyLimits_MatchBackendSource()
        {
            var rooms = File.ReadAllText("Backend/Shared/RoomContracts.cs");
            Assert.AreEqual(LobbyLimits.PlayersPerRoom, Const(rooms, "PlayersPerRoom"));
            Assert.AreEqual(LobbyLimits.Rooms, Const(rooms, "Rooms"));
            Assert.AreEqual(LobbyLimits.RoomsPerUser, Const(rooms, "RoomsPerUser"));
            Assert.AreEqual(RoomCodeRules.Length, Const(rooms, "JoinCodeLength"));
            StringAssert.Contains("\"" + RoomCodeRules.Alphabet + "\"", rooms);
            var auth = File.ReadAllText("Backend/Api/DTOs/AuthDtos.cs");
            StringAssert.Contains("StringLength(" + LobbyLimits.UserMax + ", MinimumLength = " + LobbyLimits.UserMin + ")", auth);
            StringAssert.Contains("StringLength(" + LobbyLimits.PassMax + ", MinimumLength = " + LobbyLimits.PassMin + ")", auth);
            var protocol = Directory.GetFiles("Backend/Shared/Protocol", "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText).First(t => t.Contains("MaxNameChars"));
            Assert.AreEqual(LobbyLimits.MaxNameChars, Const(protocol, "MaxNameChars"));
        }

        private static int Const(string text, string name)
        {
            var m = Regex.Match(text, @"\b" + name + @"\s*=\s*(\d+)");
            Assert.IsTrue(m.Success, name);
            return int.Parse(m.Groups[1].Value);
        }

        [TestCase(DisconnectReason.InvalidRoomCode, JoinRejectText.WrongCode)]
        [TestCase(DisconnectReason.RoomFull, "Phòng đã đủ 10 người")]
        [TestCase(DisconnectReason.ServerFull, "Máy chủ đã đầy, thử lại sau")]
        [TestCase(DisconnectReason.AuthenticationRequired, "Máy chủ yêu cầu đăng nhập")]
        [TestCase(DisconnectReason.AuthModeMismatch, "Máy chủ yêu cầu đăng nhập")]
        [TestCase(DisconnectReason.ConnectTimeout, JoinRejectText.Unreachable)]
        [TestCase(DisconnectReason.Unknown, "Không vào được phòng (mã lỗi: Unknown)")]
        [TestCase(DisconnectReason.DuplicateSession, "Không vào được phòng (mã lỗi: DuplicateSession)")]
        [TestCase(DisconnectReason.UserRequested, "")]
        public void RejectText_OneLinePerReason(DisconnectReason reason, string expected) =>
            Assert.AreEqual(expected, JoinRejectText.For(reason));

        [TestCase(1450u, 1000u, 15)]
        [TestCase(1450u, 1001u, 15)]
        [TestCase(1450u, 1420u, 1)]
        [TestCase(1450u, 1421u, 1)]
        [TestCase(1450u, 1450u, 0)]
        [TestCase(1450u, 1500u, 0)]
        public void Countdown_CeilOfTicks(uint end, uint now, int expected) => Assert.AreEqual(expected, LobbyCountdown.Seconds(end, now));

        private static IEnumerable<TestCaseData> HomeMatrix()
        {
            var screens = new[] { LobbyScreen.Connecting, LobbyScreen.Lobby, LobbyScreen.Loading, LobbyScreen.InGame,
                LobbyScreen.Results, LobbyScreen.RoomManager, LobbyScreen.History };
            var errors = new[] { DisconnectReason.ConnectionLost, DisconnectReason.ConnectTimeout, DisconnectReason.ServerClosed };
            foreach (var s in screens)
            foreach (var e in errors)
                yield return new TestCaseData(s, e).SetName("HomeAlways_" + s + "_" + e);
        }

        /// <summary>AC40: 7 screens × 3 network errors; the screen after the error always offers "Về menu".</summary>
        [TestCaseSource(nameof(HomeMatrix))]
        public void HomeAlways(LobbyScreen screen, DisconnectReason error)
        {
            foreach (var reconnect in new[] { true, false })
            {
                var next = LobbyScreenRules.OnLost(screen, reconnect, canResume: true);
                Assert.IsTrue(LobbyScreenRules.HasHome(next), screen + " → " + next);
                Assert.AreNotEqual(LobbyScreen.InGame, next, "a lost match never stays InGame");
            }
            Assert.IsFalse(string.IsNullOrEmpty(LobbyScreenRules.LoadLostText(error)));
        }

        [TestCase(MatchPhase.Waiting, LobbyScreen.Lobby)]
        [TestCase(MatchPhase.Lobby, LobbyScreen.Lobby)]
        [TestCase(MatchPhase.Loading, LobbyScreen.Loading)]
        [TestCase(MatchPhase.InGame, LobbyScreen.InGame)]
        [TestCase(MatchPhase.Ending, LobbyScreen.Results)]
        [TestCase(MatchPhase.Cleanup, LobbyScreen.Lobby)]
        public void ScreenFollowsPhase(MatchPhase phase, LobbyScreen expected) =>
            Assert.AreEqual(expected, LobbyScreenRules.ForPhase(true, phase, false));

        [Test]
        public void ApiMessages_EveryCode()
        {
            Assert.AreEqual("Sai tên hoặc mật khẩu", ApiMessages.For(ApiOp.Login, ApiResult<bool>.Fail(401)));
            Assert.AreEqual(ApiMessages.AccountUnreachable, ApiMessages.For(ApiOp.Login, ApiResult<bool>.Fail(0, ApiResult<bool>.Timeout)));
            Assert.AreEqual("Tên đã được dùng hoặc không hợp lệ", ApiMessages.For(ApiOp.Register, ApiResult<bool>.Fail(409)));
            Assert.AreEqual("Tên đã được dùng hoặc không hợp lệ", ApiMessages.For(ApiOp.Register, ApiResult<bool>.Fail(400)));
            Assert.AreEqual("Mỗi tài khoản tối đa 2 phòng", ApiMessages.For(ApiOp.CreateRoom, ApiResult<bool>.Fail(409, "UserRoomLimitReached")));
            Assert.AreEqual("Máy chủ đã đủ 5 phòng", ApiMessages.For(ApiOp.CreateRoom, ApiResult<bool>.Fail(409, "RoomLimitReached")));
            Assert.AreEqual("Tạo quá nhanh, thử lại sau 1 phút", ApiMessages.For(ApiOp.CreateRoom, ApiResult<bool>.Fail(429)));
            Assert.AreEqual("Tên hoặc sức chứa không hợp lệ", ApiMessages.For(ApiOp.CreateRoom, ApiResult<bool>.Fail(400)));
            Assert.AreEqual(ApiMessages.RoomsUnavailable, ApiMessages.For(ApiOp.CreateRoom, ApiResult<bool>.Fail(503)));
            Assert.AreEqual("Bạn không phải chủ phòng", ApiMessages.For(ApiOp.DeleteRoom, ApiResult<bool>.Fail(403, "NotRoomOwner")));
            Assert.AreEqual("Phòng không còn", ApiMessages.For(ApiOp.DeleteRoom, ApiResult<bool>.Fail(404)));
            Assert.AreEqual("Phòng đang có người", ApiMessages.For(ApiOp.DeleteRoom, ApiResult<bool>.Fail(409, "RoomNotEmpty")));
            Assert.AreEqual("Không xóa được phòng mặc định", ApiMessages.For(ApiOp.DeleteRoom, ApiResult<bool>.Fail(409, "DefaultRoomProtected")));
            Assert.AreEqual(ApiMessages.LeaderboardEmpty, ApiMessages.For(ApiOp.Leaderboard, ApiResult<bool>.Fail(404)));
            Assert.AreEqual("", ApiMessages.For(ApiOp.Login, ApiResult<bool>.Success(true)));
        }
    }
}
