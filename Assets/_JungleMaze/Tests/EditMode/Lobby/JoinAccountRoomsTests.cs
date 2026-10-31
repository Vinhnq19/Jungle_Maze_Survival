using System;
using System.IO;
using System.Linq;
using JungleMaze.App.Lobby;
using JungleMaze.Domain.Lobby;
using JungleMaze.Domain.Network;
using NUnit.Framework;
using UnityEngine;

namespace JungleMaze.Tests.Lobby
{
    /// <summary>AC5–AC10 on the join form.</summary>
    public sealed class JoinControllerTests
    {
        [Test]
        public void FormValidation_LocksJoinAndExplains()
        {
            using var rig = new LobbyRig();
            rig.Flow.OpenJoin();
            rig.Join.SetCode("k7m2q");
            Assert.AreEqual("K7M2Q", rig.Join.RoomCode, "upper-cased as typed");
            Assert.IsFalse(rig.Join.CanSubmit);
            Assert.AreEqual(JoinController.CodeLengthText, rig.Join.CodeErrorText);
            rig.Join.SetCode("K7M2QI");
            Assert.AreEqual(JoinController.BadCodeText, rig.Join.CodeErrorText);
            rig.Join.SetCode("K7M2Q9");
            rig.Join.SetName("   ");
            Assert.IsFalse(rig.Join.CanSubmit);
            Assert.AreEqual(JoinController.NameText, rig.Join.NameErrorText);
            rig.Join.SetName(new string('x', 33));
            Assert.IsFalse(rig.Join.CanSubmit);
            rig.Join.SetName("  Huy ");
            Assert.AreEqual("Huy", rig.Join.DisplayName);
            Assert.IsTrue(rig.Join.CanSubmit);
            Assert.IsTrue(rig.Flow.SubmitJoin());
            Assert.AreEqual(1, rig.Commands.Joins);
            Assert.AreEqual("K7M2Q9", rig.Commands.LastCode);
            Assert.AreEqual("Huy", rig.Commands.LastName);
        }

        [Test]
        public void ApiDown_JoinNotBlocked()
        {
            using var rig = new LobbyRig();
            rig.Api.Configured = false;
            rig.Api.HealthStatus = 503;
            rig.Flow.Api.Check();
            Assert.AreEqual(ApiMessages.NoApi, rig.Flow.Api.Text);
            rig.JoinLobby();
            Assert.AreEqual(LobbyScreen.Lobby, rig.Flow.Screen, "no login on the guest path (AC6)");
            Assert.AreEqual(0, rig.Api.Logins + rig.Api.Lists + rig.Api.Healths, "join never waits on the API");
        }

        [TestCase(DisconnectReason.InvalidRoomCode, true)]
        [TestCase(DisconnectReason.RoomFull, false)]
        [TestCase(DisconnectReason.ServerFull, false)]
        public void RejectKeepsForm_NoRetry(DisconnectReason reason, bool focusCode)
        {
            using var rig = new LobbyRig();
            rig.Flow.OpenJoin();
            rig.Join.SetCode("K7M2Q9");
            rig.Flow.SubmitJoin();
            rig.Connection.Set(NetworkState.Connecting);
            rig.Tick();
            rig.Connection.Set(NetworkState.Failed, reason);
            rig.Tick();
            Assert.AreEqual(LobbyScreen.JoinForm, rig.Flow.Screen, "on the same tick (AC7 ≤ 500 ms)");
            Assert.AreEqual(JoinRejectText.For(reason), rig.Join.Message);
            Assert.AreEqual("K7M2Q9", rig.Join.RoomCode);
            Assert.AreEqual("Huy", rig.Join.DisplayName);
            Assert.AreEqual(focusCode, rig.Join.FocusCode);
            rig.Tick(120);
            Assert.AreEqual(1, rig.Commands.Joins, "no automatic retry");
        }

        [Test]
        public void FiveClicks_OneConnect()
        {
            using var rig = new LobbyRig();
            rig.Flow.OpenJoin();
            rig.Join.SetCode("K7M2Q9");
            for (var i = 0; i < 5; i++) { rig.Flow.SubmitJoin(); rig.Join.Submit(); rig.Clock.Advance(0.04); }
            Assert.AreEqual(1, rig.Commands.Joins);
            Assert.AreEqual(LobbyScreen.Connecting, rig.Flow.Screen);
        }

        [Test]
        public void Cancel_BackToForm_Within500ms()
        {
            using var rig = new LobbyRig();
            rig.Flow.OpenJoin();
            rig.Join.SetCode("K7M2Q9");
            rig.Flow.SubmitJoin();
            rig.Flow.CancelJoin();
            Assert.AreEqual(LobbyScreen.JoinForm, rig.Flow.Screen, "same call, no wait");
            Assert.AreEqual(1, rig.Commands.Cancels);
            Assert.IsFalse(rig.Join.Busy);
        }

        [Test]
        public void Hang20s_RetryAndHome()
        {
            using var rig = new LobbyRig();
            rig.Flow.OpenJoin();
            rig.Join.SetCode("K7M2Q9");
            rig.Flow.SubmitJoin();
            rig.Connection.Set(NetworkState.Connecting);
            rig.Clock.Advance(19.9);
            rig.Tick();
            Assert.AreEqual(LobbyScreen.Connecting, rig.Flow.Screen);
            rig.Clock.Advance(0.2);
            rig.Tick();
            Assert.AreEqual(LobbyScreen.JoinForm, rig.Flow.Screen);
            Assert.IsTrue(rig.Join.TimedOut, "the form shows Thử lại");
            Assert.AreEqual(JoinRejectText.Unreachable, rig.Join.Message);
            Assert.AreEqual(1, rig.Commands.Cancels, "the hanging attempt is closed");
            Assert.IsTrue(LobbyScreenRules.HasHome(rig.Flow.Screen));
        }

        [Test]
        public void EditingFormHidesOldNotice()
        {
            using var rig = new LobbyRig();
            rig.Join.Rejected(DisconnectReason.RoomFull);
            Assert.IsNotEmpty(rig.Join.Message);
            rig.Join.SetCode("K7M2Q");
            Assert.IsEmpty(rig.Join.Message, "G-6");
        }
    }

    /// <summary>AC12, AC13, AC17 on the account session.</summary>
    public sealed class AccountSessionTests
    {
        [TestCase(401, "Sai tên hoặc mật khẩu")]
        [TestCase(0, ApiMessages.AccountUnreachable)]
        [TestCase(503, ApiMessages.AccountUnreachable)]
        public void Login_ErrorMapping(int status, string message)
        {
            using var rig = new LobbyRig();
            rig.Api.LoginStatus = status;
            rig.Account.Username = "huycv";
            rig.Account.Password = "secret123";
            Assert.IsTrue(rig.Account.Submit());
            Assert.AreEqual(message, rig.Account.Message);
            Assert.IsFalse(rig.Account.IsValid);
        }

        [Test]
        public void Register_ValidatesAndMapsConflict()
        {
            using var rig = new LobbyRig();
            rig.Account.RegisterMode = true;
            rig.Account.Username = "hu";
            rig.Account.Password = "secret123";
            Assert.IsFalse(rig.Account.Submit(), "username 3–32");
            rig.Account.Username = "huycv";
            rig.Account.Password = "short";
            Assert.IsFalse(rig.Account.Submit(), "register password 8–128");
            rig.Account.Password = "longenough";
            rig.Api.RegisterStatus = 409;
            Assert.IsTrue(rig.Account.Submit());
            Assert.AreEqual("Tên đã được dùng hoặc không hợp lệ", rig.Account.Message);
            rig.Api.RegisterStatus = 201;
            rig.Account.Password = "longenough";
            rig.Account.Submit();
            Assert.IsTrue(rig.Account.IsValid, "register then login");
        }

        [Test]
        public void PasswordClearedEvenOnError()
        {
            using var rig = new LobbyRig();
            rig.Api.LoginStatus = 401;
            rig.Account.Username = "huycv";
            rig.Account.Password = "secret123";
            rig.Account.Submit();
            Assert.AreEqual("", rig.Account.Password);
            rig.Api.LoginStatus = 200;
            rig.Account.Password = "secret123";
            rig.Account.Submit();
            Assert.AreEqual("", rig.Account.Password);
            Assert.IsTrue(rig.Account.IsValid);
        }

        [Test]
        public void Expired_Or401_ClearsAndReLogin_NoResend()
        {
            using var rig = new LobbyRig();
            rig.Api.TokenLifetimeSeconds = 60;
            rig.Flow.OpenRooms();
            Assert.AreEqual(LobbyScreen.Login, rig.Flow.Screen, "no JWT → login first");
            Assert.AreEqual(0, rig.Api.Lists, "no rooms request without a token");
            rig.Account.Username = "huycv";
            rig.Account.Password = "secret123";
            rig.Account.Submit();
            Assert.AreEqual(LobbyScreen.RoomManager, rig.Flow.Screen);
            Assert.AreEqual(1, rig.Api.Lists);

            rig.Utc = rig.Utc.AddSeconds(61); // fake clock passes ExpiresAtUtc
            rig.Tick();
            Assert.AreEqual(LobbyScreen.Login, rig.Flow.Screen);
            Assert.AreEqual(ApiMessages.SessionExpired, rig.Account.Message);

            rig.Account.Password = "secret123";
            rig.Account.Submit();
            Assert.AreEqual(LobbyScreen.RoomManager, rig.Flow.Screen, "back to rooms after login");
            Assert.AreEqual(2, rig.Api.Lists, "list reloaded");

            rig.Api.ListStatus = 401;
            rig.Rooms.Refresh();
            Assert.AreEqual(LobbyScreen.Login, rig.Flow.Screen);
            Assert.IsFalse(rig.Account.IsValid);
            Assert.AreEqual(3, rig.Api.Lists, "the 401 request is not resent");
        }

        /// <summary>AC13: tokens and passwords never reach Debug.Log, PlayerPrefs or client.json.</summary>
        [Test]
        public void NoSecretsInLogs()
        {
            var seen = new System.Collections.Generic.List<string>();
            void Capture(string condition, string stack, LogType type) => seen.Add(condition);
            Application.logMessageReceived += Capture;
            try
            {
                using var rig = new LobbyRig();
                rig.Account.Username = "huycv";
                rig.Account.Password = "Pa55word-UI02";
                rig.Account.Submit();
                rig.Flow.OpenRooms();
                rig.Rooms.Refresh();
                Debug.Log(rig.Account.ToString() + " " + new AuthToken(rig.Api.IssuedTokens[0], rig.Utc));
                var token = rig.Api.IssuedTokens.Single();
                Assert.IsFalse(seen.Any(l => l.Contains(token) || l.Contains("Pa55word-UI02")));
                Assert.IsFalse(PlayerPrefs.GetString("jm.menu.displayName", "").Contains(token));
                var json = Path.Combine(Application.persistentDataPath, "client.json");
                if (File.Exists(json)) Assert.IsFalse(File.ReadAllText(json).Contains(token));
            }
            finally { Application.logMessageReceived -= Capture; }
        }
    }

    /// <summary>AC14–AC16, AC41 on the room manager.</summary>
    public sealed class RoomListModelTests
    {
        private static LobbyRig LoggedIn()
        {
            var rig = new LobbyRig();
            rig.Account.Username = "huycv";
            rig.Account.Password = "secret123";
            rig.Account.Submit();
            return rig;
        }

        [TestCase(0, ListState.Empty, ApiMessages.NoRooms)]
        [TestCase(3, ListState.Ready, "")]
        public void List_States(int own, ListState state, string message)
        {
            using var rig = LoggedIn();
            rig.Api.SeedRooms(own);
            rig.Rooms.Refresh();
            Assert.AreEqual(state, rig.Rooms.State);
            Assert.AreEqual(message, rig.Rooms.Message);
            Assert.AreEqual(own + 1, rig.Rooms.Rooms.Count, "default room listed too");
            var def = rig.Rooms.Rooms.First(r => r.IsDefault);
            Assert.IsNull(def.RoomCode, "the default code is not exposed");
            Assert.IsFalse(RoomListModel.CanDelete(def));
            Assert.AreEqual("3/10", RoomListModel.Occupancy(def));
        }

        [Test]
        public void List_503_RetryAfterNetworkBack()
        {
            using var rig = LoggedIn();
            rig.Api.SeedRooms(1);
            rig.Api.ListStatus = 503;
            rig.Rooms.Refresh();
            Assert.AreEqual(ListState.Unavailable, rig.Rooms.State);
            rig.Api.ListStatus = 200;
            rig.Rooms.Refresh();
            Assert.AreEqual(ListState.Ready, rig.Rooms.State);
        }

        [Test]
        public void List_LoadingWhileInFlight()
        {
            using var rig = LoggedIn();
            rig.Api.SeedRooms(1);
            rig.Api.Deferred = true;
            rig.Rooms.Refresh();
            Assert.AreEqual(ListState.Loading, rig.Rooms.State);
            rig.Api.Flush();
            Assert.AreEqual(ListState.Ready, rig.Rooms.State);
        }

        [Test]
        public void Create_FiveClicks_OneRequest_ShowsCode()
        {
            using var rig = LoggedIn();
            rig.Rooms.Refresh();
            rig.Api.Deferred = true;
            rig.Rooms.NewName = "Phòng Huy";
            rig.Rooms.NewCapacity = 6;
            for (var i = 0; i < 5; i++) rig.Rooms.Create();
            rig.Api.Flush();
            Assert.AreEqual(1, rig.Api.Creates);
            var created = rig.Rooms.Rooms.Last();
            Assert.AreEqual("Phòng Huy", created.Name);
            Assert.AreEqual(RoomCodeRules.Length, created.RoomCode.Length, "code shown in the same frame as the answer");
            Assert.AreEqual(6, created.MaxPlayers);
        }

        [TestCase(409, "UserRoomLimitReached", "Mỗi tài khoản tối đa 2 phòng")]
        [TestCase(409, "RoomLimitReached", "Máy chủ đã đủ 5 phòng")]
        [TestCase(429, null, "Tạo quá nhanh, thử lại sau 1 phút")]
        [TestCase(400, null, "Tên hoặc sức chứa không hợp lệ")]
        [TestCase(503, null, ApiMessages.RoomsUnavailable)]
        public void Create_ErrorCodes(int status, string code, string message)
        {
            using var rig = LoggedIn();
            rig.Rooms.Refresh();
            rig.Api.CreateStatus = status;
            rig.Api.CreateError = code;
            rig.Rooms.NewName = "X";
            rig.Rooms.Create();
            Assert.AreEqual(message, rig.Rooms.Message);
        }

        [TestCase("", 10)]
        [TestCase("ok", 0)]
        [TestCase("ok", 11)]
        public void Create_ValidatesLocally(string name, int capacity)
        {
            using var rig = LoggedIn();
            rig.Rooms.NewName = name;
            rig.Rooms.NewCapacity = capacity;
            Assert.IsFalse(rig.Rooms.Create());
            Assert.AreEqual(0, rig.Api.Creates);
        }

        [TestCase(204, null, null, true)]
        [TestCase(403, "NotRoomOwner", "Bạn không phải chủ phòng", false)]
        [TestCase(404, "RoomNotFound", "Phòng không còn", true)]
        [TestCase(409, "RoomNotEmpty", "Phòng đang có người", false)]
        [TestCase(409, "DefaultRoomProtected", "Không xóa được phòng mặc định", false)]
        public void Delete_ConfirmOnce_Codes(int status, string code, string message, bool removed)
        {
            using var rig = LoggedIn();
            rig.Api.SeedRooms(2);
            rig.Rooms.Refresh();
            var room = rig.Rooms.Rooms.First(r => !r.IsDefault);
            rig.Api.DeleteStatus = status;
            rig.Api.DeleteError = code;
            rig.Rooms.RequestDelete(room.Id);
            Assert.AreEqual(0, rig.Api.Deletes, "asks first");
            Assert.IsTrue(rig.Rooms.ConfirmDelete());
            Assert.IsFalse(rig.Rooms.ConfirmDelete(), "one request");
            Assert.AreEqual(1, rig.Api.Deletes);
            Assert.AreEqual(removed, rig.Rooms.Rooms.All(r => r.Id != room.Id));
            if (message != null) Assert.AreEqual(message, rig.Rooms.Message);
        }

        [Test]
        public void Delete_DefaultRoomHasNoDelete()
        {
            using var rig = LoggedIn();
            rig.Rooms.Refresh();
            rig.Rooms.RequestDelete(rig.Rooms.Rooms.First(r => r.IsDefault).Id);
            Assert.IsNull(rig.Rooms.PendingDelete);
        }

        [Test]
        public void JoinThisRoom_UsesRoomCodePath()
        {
            using var rig = LoggedIn();
            rig.Api.SeedRooms(1);
            rig.Flow.OpenRooms();
            var room = rig.Rooms.Rooms.First(r => !r.IsDefault);
            rig.Flow.JoinRoom(room);
            Assert.AreEqual(LobbyScreen.JoinForm, rig.Flow.Screen);
            Assert.AreEqual(room.RoomCode, rig.Join.RoomCode);
            rig.Flow.SubmitJoin();
            Assert.AreEqual(room.RoomCode, rig.Commands.LastCode, "same Join(code, name) as a guest; no token");
        }
    }
}
