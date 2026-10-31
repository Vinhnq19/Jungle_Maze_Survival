#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using JungleMaze.App.Flow;
using JungleMaze.App.Lobby;
using JungleMaze.App.Lobby.Mocks;
using JungleMaze.App.MatchUi;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Lobby;
using JungleMaze.Presentation.UI.Lobby;
using JungleMaze.Presentation.UI.MatchOverlay;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JungleMaze.Tests.PlayMode.Lobby
{
    /// <summary>
    /// UI-02 on real frames: LobbyUi.prefab, presenter and flow ticked every Update on the unscaled clock, data from
    /// the scripted mocks (<see cref="LobbyScenarios"/>). What Bootstrap.Lobby composes, minus network and Bootstrap.
    /// </summary>
    internal sealed class LobbyUiRig : IDisposable
    {
        public const string PrefabPath = "Assets/_JungleMaze/Prefabs/UI/Lobby/LobbyUi.prefab";
        public const string OverlayPath = "Assets/_JungleMaze/Prefabs/UI/MatchOverlay/MatchOverlay.prefab";
        public readonly Func<double> Clock = () => Time.unscaledTimeAsDouble;
        public readonly EventBus Bus = new EventBus();
        public readonly LobbyScenarios Scenarios;
        public readonly LobbyFlow Flow;
        public readonly LobbyUiRoot Root;
        public readonly LobbyUiPresenter Presenter;
        public readonly Ticker Driver;
        public string Copied;
        public int Quits;

        public LobbyUiRig(LobbyCase scenario = LobbyCase.HappyPath, Func<bool> settingsOpen = null, Action<LobbyFlow> wire = null,
            LobbyOptions options = null)
        {
            Scenarios = new LobbyScenarios(Clock) { JoinDelay = 0.1f, LobbySeconds = 1, LoadingSeconds = 1, InGameSeconds = 1, EndingSeconds = 5, DropAfter = 0.2f };
            Scenarios.Use(scenario);
            var p = Scenarios.Ports;
            var account = new AccountSession(p, () => DateTime.UtcNow);
            Flow = new LobbyFlow(p, p, p, p, new JoinController(p, Clock, null, "Huy"), account, new RoomListModel(p, account),
                null, new LeaderboardModel(p), new ApiStatusModel(p), Clock, options, Bus);
            account.Username = "huycv"; // UI-02 round 2: login first, the flow starts on Home
            account.Password = "secret123";
            account.Submit();
            wire?.Invoke(Flow);
            Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)).GetComponent<LobbyUiRoot>();
            Presenter = new LobbyUiPresenter(Root, Flow, null, () => Quits++, code => Copied = code, settingsOpen);
            Root.Bind(Presenter);
            Driver = new GameObject("UI-02 ticker").AddComponent<Ticker>();
            Driver.Rig = this;
        }

        public bool Visible(LobbyPanelView panel) => panel.Group.alpha > 0.5f && panel.Group.blocksRaycasts;

        public void Click(Button button)
        {
            Assert.IsTrue(button.gameObject.activeInHierarchy && button.interactable, button.name + " clickable");
            button.onClick.Invoke();
        }

        public IEnumerator Until(Func<bool> condition, float seconds, string what)
        {
            var end = Time.unscaledTime + seconds;
            while (!condition() && Time.unscaledTime < end) yield return null;
            Assert.IsTrue(condition(), what + " within " + seconds + " s");
        }

        public void JoinForm(string code = "K7M2Q9")
        {
            Click(Root.MainMenu.Join);
            Root.JoinForm.RoomCode.text = code; // fires onValueChanged like typing
        }

        public void Dispose()
        {
            if (Driver != null) Object.Destroy(Driver.gameObject);
            if (Root != null) Object.Destroy(Root.gameObject);
            Flow.Dispose();
        }

        internal sealed class Ticker : MonoBehaviour
        {
            public LobbyUiRig Rig;

            private void Update()
            {
                Rig.Scenarios.Tick(Time.unscaledDeltaTime);
                Rig.Flow.Tick(Time.unscaledDeltaTime);
            }
        }
    }

    public sealed class LobbyUiTimingTests
    {
        /// <summary>Round 2: Home after login within 1 s, six buttons, the account card shows who is logged in.</summary>
        [UnityTest]
        public IEnumerator HomeWithin1s_SixButtons_AccountCard()
        {
            var start = Time.realtimeSinceStartupAsDouble;
            using var rig = new LobbyUiRig();
            yield return null;
            Assert.Less(Time.realtimeSinceStartupAsDouble - start, 1.0, "AC1 ≤ 1 s");
            Assert.IsTrue(rig.Visible(rig.Root.MainMenu));
            Assert.IsFalse(rig.Visible(rig.Root.Login), "logged in: no login screen");
            var m = rig.Root.MainMenu;
            var labels = new[] { m.Join, m.Rooms, m.History, m.Leaderboard, m.Settings, m.Quit }
                .Select(b => b.GetComponentInChildren<TMP_Text>().text).ToArray();
            CollectionAssert.AreEqual(new[] { "Vào phòng", "Tạo phòng", "Lịch sử", "Bảng xếp hạng", "Cài đặt", "Thoát" }, labels);
            Assert.AreEqual("Đã đăng nhập", m.AccountStatus.text);
            Assert.AreEqual("huycv", m.AccountName.text);
            rig.Click(m.Quit);
            Assert.AreEqual(1, rig.Quits);
        }

        /// <summary>Review M1: "Bỏ qua đăng nhập (dev)" is hidden unless the dev option is on; with it, Login shows the
        /// account-server error with Thử lại and the skip lands on Home in dev mode.</summary>
        [UnityTest]
        public IEnumerator DevSkip_HiddenByDefault_ShownInDev_ApiDownHasRetry()
        {
            using (var release = new LobbyUiRig())
            {
                yield return null;
                Assert.IsFalse(release.Root.Login.DevSkip.gameObject.activeSelf, "release: no dev skip");
            }
            using var rig = new LobbyUiRig(options: new LobbyOptions { DevSkipLogin = true });
            rig.Scenarios.Api.Configured = false;
            yield return null;
            rig.Click(rig.Root.MainMenu.Logout);
            rig.Click(rig.Root.LogoutConfirm.Yes);
            yield return null;
            yield return null;
            Assert.IsTrue(rig.Visible(rig.Root.Login));
            Assert.AreEqual(ApiMessages.NoApi, rig.Root.Login.ApiStatus.text);
            Assert.IsTrue(rig.Root.Login.Retry.gameObject.activeInHierarchy, "Thử lại when the account server is down");
            rig.Click(rig.Root.Login.DevSkip);
            yield return null;
            Assert.IsTrue(rig.Visible(rig.Root.MainMenu));
            Assert.AreEqual("Chế độ dev", rig.Root.MainMenu.AccountStatus.text);
            rig.JoinForm();
            rig.Click(rig.Root.JoinForm.Join);
            yield return rig.Until(() => rig.Visible(rig.Root.Lobby), 2f, "lobby by room code without login");
        }

        /// <summary>Round 2: the join form is a modal over Home (Home drawn but inert), "Hủy" closes it; the server
        /// settings modal opens from "Cài đặt" with the endpoint fields and closes with "Đóng".</summary>
        [UnityTest]
        public IEnumerator JoinModal_OverHome_ServerSettingsModal()
        {
            using var rig = new LobbyUiRig();
            yield return null;
            rig.JoinForm();
            yield return null;
            Assert.IsTrue(rig.Visible(rig.Root.JoinForm));
            Assert.AreEqual(1f, rig.Root.MainMenu.Group.alpha, "Home stays drawn under the modal");
            Assert.IsFalse(rig.Root.MainMenu.Group.blocksRaycasts, "but takes no clicks");
            StringAssert.Contains("huycv", rig.Root.JoinForm.Hint.text);
            Assert.AreEqual("Hủy", rig.Root.JoinForm.Back.GetComponentInChildren<TMP_Text>().text);
            rig.Click(rig.Root.JoinForm.Back);
            yield return null;
            Assert.IsTrue(rig.Visible(rig.Root.MainMenu));
            rig.Click(rig.Root.MainMenu.Settings);
            yield return null;
            Assert.IsTrue(rig.Visible(rig.Root.ServerSettings));
            Assert.IsTrue(rig.Root.ServerSettings.Host.gameObject.activeInHierarchy);
            rig.Click(rig.Root.ServerSettings.Close);
            yield return null;
            Assert.IsFalse(rig.Visible(rig.Root.ServerSettings));
        }

        [UnityTest]
        public IEnumerator NoInstantiateOnPress()
        {
            using var rig = new LobbyUiRig();
            yield return null;
            var before = rig.Root.GetComponentsInChildren<Transform>(true).Length;
            rig.JoinForm();
            rig.Click(rig.Root.JoinForm.Back);
            rig.Click(rig.Root.MainMenu.History);
            yield return null;
            Assert.AreEqual(before, rig.Root.GetComponentsInChildren<Transform>(true).Length, "panels pre-built (G-1)");
        }
    }

    public sealed class LobbyFlowPlayTests
    {
        [UnityTest]
        public IEnumerator GuestJoin_ApiDown_LobbyWithin500ms_TenSlots_Copy6Chars()
        {
            using var rig = new LobbyUiRig();
            rig.Scenarios.Api.Configured = false;
            yield return null;
            rig.JoinForm("k7m2q9");
            Assert.AreEqual("K7M2Q9", rig.Root.JoinForm.RoomCode.text, "upper-cased in the field");
            rig.Click(rig.Root.JoinForm.Join);
            var sawLogin = false;
            var end = Time.unscaledTime + 2f;
            while (!rig.Visible(rig.Root.Lobby) && Time.unscaledTime < end) { sawLogin |= rig.Visible(rig.Root.Login); yield return null; }
            Assert.IsTrue(rig.Visible(rig.Root.Lobby), "joined without the API (AC6)");
            Assert.IsFalse(sawLogin);
            var joinedAt = Time.unscaledTime;
            yield return null;
            Assert.Less(Time.unscaledTime - joinedAt, 0.5f);
            var slots = rig.Root.Lobby.Slots.Where(s => s.gameObject.activeSelf).ToArray();
            Assert.AreEqual(10, slots.Length);
            Assert.AreEqual(6, slots.Count(s => s.PlayerName.text == LobbyUiPresenter.EmptySlot));
            Assert.AreEqual(2, slots.Count(s => s.PlayerName.text == "Huy" && s.Badge.text != LobbyUiPresenter.SelfTag), "same name, two rows");
            Assert.AreEqual(1, slots.Count(s => s.Badge.text == LobbyUiPresenter.SelfTag));
            StringAssert.Contains("tối thiểu 2", rig.Root.Lobby.Count.text);
            rig.Click(rig.Root.Lobby.Copy);
            Assert.AreEqual("K7M2Q9", rig.Copied);
            Assert.IsFalse(rig.Root.Lobby.Ready.gameObject.activeSelf, "Ready off by default (Q1)");
        }

        [UnityTest]
        public IEnumerator LeaveWithin1s_KeepsForm()
        {
            using var rig = new LobbyUiRig();
            yield return null;
            rig.JoinForm("ABCDEF");
            rig.Click(rig.Root.JoinForm.Join);
            yield return rig.Until(() => rig.Visible(rig.Root.Lobby), 2f, "lobby");
            rig.Click(rig.Root.Lobby.Leave);
            yield return rig.Until(() => rig.Visible(rig.Root.MainMenu), 1f, "menu");
            Assert.AreEqual(1, rig.Scenarios.Commands.Leaves);
            rig.Click(rig.Root.MainMenu.Join);
            yield return null;
            Assert.AreEqual("ABCDEF", rig.Root.JoinForm.RoomCode.text);
            Assert.AreEqual("Huy", rig.Root.JoinForm.DisplayName.text);
        }

        [UnityTest]
        public IEnumerator LoadingWithin500ms_Steps_Hint()
        {
            using var rig = new LobbyUiRig();
            yield return null;
            rig.JoinForm();
            rig.Click(rig.Root.JoinForm.Join);
            yield return rig.Until(() => rig.Visible(rig.Root.Loading), 2f, "loading");
            yield return null;
            Assert.AreEqual("1. Tải dữ liệu bản đồ", rig.Root.Loading.StepMap.text);
            Assert.AreEqual(LobbyOptions.Default.ControlsHint, rig.Root.Loading.Hint.text);
            StringAssert.StartsWith("Còn ", rig.Root.Loading.TimeLeft.text);
            StringAssert.StartsWith("Mẹo: ", rig.Root.Loading.Tip.text);
        }

        [UnityTest]
        public IEnumerator FullRun_ResultsApiDown_RankShown_ReplayThenHome()
        {
            using var rig = new LobbyUiRig();
            rig.Scenarios.Api.Configured = false;
            rig.Flow.Api.Check();
            yield return null;
            rig.JoinForm();
            rig.Click(rig.Root.JoinForm.Join);
            yield return rig.Until(() => rig.Visible(rig.Root.Results), 5f, "results");
            yield return null;
            var v = rig.Root.Results;
            Assert.AreEqual(ResultModel.VictoryText, v.Title.text);
            Assert.AreEqual("#1 / 4", v.Rank.text, "rank from the packet, no API (AC31)");
            Assert.IsFalse(v.Kills.gameObject.activeSelf, "T-4");
            Assert.AreEqual(4, v.Rows.Count(r => r.gameObject.activeSelf));
            Assert.AreEqual(ApiMessages.HistoryUnavailableLine, v.OnlineLine.text, "only the online part says unavailable");
            Assert.IsFalse(v.Online.gameObject.activeSelf);
            Assert.AreEqual(1, rig.Flow.ResultsShown, "once per match (AC34)");
            for (var i = 0; i < 5; i++) v.Replay.onClick.Invoke();
            yield return null;
            Assert.AreEqual(1, rig.Scenarios.Commands.Replays);
            Assert.AreEqual(LobbyUiPresenter.ReplayWaiting, v.ReplayLabel.text);
            yield return rig.Until(() => rig.Visible(rig.Root.Lobby), 1f, "new lobby");
            rig.Click(rig.Root.Lobby.Leave);
            yield return rig.Until(() => rig.Visible(rig.Root.MainMenu), 1f, "menu");
        }

        [UnityTest]
        public IEnumerator Disconnect_ReconnectOverlay_ThenGame()
        {
            using var rig = new LobbyUiRig(LobbyCase.Disconnect);
            yield return null;
            rig.JoinForm();
            rig.Click(rig.Root.JoinForm.Join);
            yield return rig.Until(() => rig.Visible(rig.Root.Reconnect), 4f, "reconnect overlay");
            StringAssert.Contains("khoảng", rig.Root.Reconnect.Seconds.text);
            Assert.IsTrue(rig.Root.Reconnect.Home.gameObject.activeInHierarchy);
            yield return rig.Until(() => rig.Flow.Screen == LobbyScreen.InGame, 1f, "resumed");
            yield return null;
            Assert.IsFalse(rig.Root.Canvas.enabled, "UI-02 hidden in the match");
        }

        /// <summary>Round 2: logout asks first and lands on Login (no back button there); login lands on Home; "Tạo
        /// phòng" opens the room manager, which has no logout of its own; "Vào" uses the RoomCode path (AC18).</summary>
        [UnityTest]
        public IEnumerator Logout_Confirm_Login_Rooms_JoinThisRoom()
        {
            using var rig = new LobbyUiRig();
            yield return null;
            rig.Click(rig.Root.MainMenu.Logout);
            yield return null;
            Assert.IsTrue(rig.Visible(rig.Root.LogoutConfirm), "asks first");
            rig.Click(rig.Root.LogoutConfirm.No);
            yield return null;
            Assert.IsFalse(rig.Visible(rig.Root.LogoutConfirm));
            Assert.IsTrue(rig.Flow.Account.IsValid, "Hủy keeps the session");
            rig.Click(rig.Root.MainMenu.Logout);
            rig.Click(rig.Root.LogoutConfirm.Yes);
            yield return null;
            Assert.IsTrue(rig.Visible(rig.Root.Login), "logout lands on Login");
            Assert.IsFalse(rig.Visible(rig.Root.LogoutConfirm));
            Assert.IsTrue(rig.Root.Login.Advanced.gameObject.activeInHierarchy, "server settings reachable before login");
            rig.Root.Login.Username.text = "huycv";
            rig.Root.Login.Password.text = "secret123";
            rig.Click(rig.Root.Login.Submit);
            Assert.AreEqual("", rig.Root.Login.Password.text, "password field cleared");
            yield return null;
            Assert.IsTrue(rig.Visible(rig.Root.MainMenu), "login lands on Home");
            rig.Click(rig.Root.MainMenu.Rooms);
            yield return null;
            Assert.IsTrue(rig.Visible(rig.Root.RoomManager));
            var own = rig.Root.RoomManager.Rows.First(r => r.gameObject.activeSelf && r.Delete.gameObject.activeSelf);
            var def = rig.Root.RoomManager.Rows.First(r => r.gameObject.activeSelf && r.Code.text == "Mặc định");
            Assert.IsFalse(def.Delete.gameObject.activeSelf, "default room has no delete");
            var code = own.Code.text;
            rig.Click(own.Join);
            yield return null;
            Assert.IsTrue(rig.Visible(rig.Root.JoinForm));
            Assert.AreEqual(code, rig.Root.JoinForm.RoomCode.text, "same RoomCode path (AC18)");
        }
    }

    public sealed class LobbyOverlayIntegrationTests
    {
        /// <summary>AC4 + AC45: "Cài đặt" opens UI-03's panel without the leave row; one EventSystem everywhere.</summary>
        [UnityTest]
        public IEnumerator Settings_OpensUi03Panel_NoLeaveRow_OneEventSystem()
        {
            var bus = new EventBus();
            var menu = new MatchMenuController(bus, () => Time.unscaledTimeAsDouble, () => Guid.Empty, AppScreen.MainMenu);
            var overlay = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(LobbyUiRig.OverlayPath)).GetComponent<MatchOverlayRoot>();
            overlay.EnsureEventSystem();
            overlay.Bind(new MatchOverlayPresenter(overlay, menu, null, null, null), new MatchOverlayInput(menu));
            using var rig = new LobbyUiRig(settingsOpen: () => menu.IsOpen, wire: f => f.OpenSettingsPanel = menu.OpenFromMainMenu);
            try
            {
                yield return null;
                Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, "menu");
                rig.Click(rig.Root.MainMenu.Settings);
                yield return null;
                Assert.IsTrue(rig.Visible(rig.Root.ServerSettings), "round 2: Cài đặt opens the server modal first");
                rig.Click(rig.Root.ServerSettings.Audio);
                yield return null;
                Assert.IsFalse(rig.Visible(rig.Root.ServerSettings));
                Assert.IsTrue(menu.IsOpen && menu.FromMainMenu);
                Assert.IsTrue(overlay.Canvas.enabled, "overlay canvas on outside a match");
                Assert.IsFalse(overlay.Menu.Leave.gameObject.activeSelf, "no leave row from the main menu");
                Assert.IsTrue(menu.PressEscape(), "ESC closes it");
                yield return null;
                yield return null;
                Assert.AreEqual(LobbyScreen.MainMenu, rig.Flow.Screen);
                rig.JoinForm();
                rig.Click(rig.Root.JoinForm.Join);
                yield return rig.Until(() => rig.Visible(rig.Root.Lobby), 2f, "lobby");
                Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, "lobby");
                yield return rig.Until(() => rig.Visible(rig.Root.Results), 5f, "results");
                Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, "results");
            }
            finally
            {
                Object.Destroy(overlay.gameObject);
                menu.Dispose();
            }
        }

        /// <summary>N7 on real frames: UI-03's leave request takes UI-02 home with one leave.</summary>
        [UnityTest]
        public IEnumerator LeaveMatchRequested_HomeWithin1s()
        {
            using var rig = new LobbyUiRig();
            yield return null;
            rig.JoinForm();
            rig.Click(rig.Root.JoinForm.Join);
            yield return rig.Until(() => rig.Flow.Screen == LobbyScreen.InGame, 4f, "in game");
            rig.Bus.Publish(new LeaveMatchRequested(rig.Flow.Lobby.MatchId, LeaveMatchSource.MatchMenu));
            yield return rig.Until(() => rig.Visible(rig.Root.MainMenu), 1f, "menu");
            Assert.AreEqual(1, rig.Scenarios.Commands.Leaves);
        }
    }

    public sealed class LobbyLayoutTests
    {
        private static RectTransform Block(Component panel, string name) => (RectTransform)panel.transform.Find(name);

        private static void AssertRect(RectTransform r, float x, float y, float w, float h)
        {
            Assert.AreEqual(x, r.anchoredPosition.x, 20f, r.name + " x");
            Assert.AreEqual(y, -r.anchoredPosition.y, 20f, r.name + " y");
            Assert.AreEqual(w, r.sizeDelta.x, 20f, r.name + " w");
            Assert.AreEqual(h, r.sizeDelta.y, 20f, r.name + " h");
        }

        /// <summary>AC49: Figma v2 blocks within 20 px; buttons ≥ 44 px; text ≥ 13 px; nothing outside 1280×720.</summary>
        [UnityTest]
        public IEnumerator FigmaBlocks_ButtonsAndText_NoOverflow()
        {
            using var rig = new LobbyUiRig();
            yield return null;
            AssertRect(Block(rig.Root.JoinForm, "Panel"), 360, 67, 560, 586);
            AssertRect(Block(rig.Root.Results, "Table"), 760, 123, 440, 475);
            AssertRect(Block(rig.Root.Lobby, "Slots"), 760, 123, 440, 475);
            Assert.AreEqual(399f, -((RectTransform)rig.Root.Results.Replay.transform).anchoredPosition.y, 20f);
            Assert.AreEqual(399f, -((RectTransform)rig.Root.Results.Home.transform).anchoredPosition.y, 20f);
            foreach (var b in rig.Root.GetComponentsInChildren<Button>(true))
                if (b.GetComponent<HistoryRowView>() == null)
                    Assert.GreaterOrEqual(((RectTransform)b.transform).rect.height, 44f, b.name);
            foreach (var t in rig.Root.GetComponentsInChildren<TMP_Text>(true))
                Assert.GreaterOrEqual(t.fontSize, 13f, t.name);
            foreach (var panel in rig.Root.Panels)
            foreach (RectTransform child in panel.transform)
            {
                if (child.anchorMin != child.anchorMax) continue; // stretched backgrounds
                Assert.GreaterOrEqual(child.anchoredPosition.x, 0f, child.name);
                Assert.LessOrEqual(child.anchoredPosition.x + child.sizeDelta.x, 1280f, child.name);
                Assert.LessOrEqual(-child.anchoredPosition.y + child.sizeDelta.y, 720f, child.name);
            }
            var scaler = rig.Root.GetComponent<CanvasScaler>();
            Assert.AreEqual(new Vector2(1280, 720), scaler.referenceResolution);
            Assert.AreEqual(CanvasScaler.ScreenMatchMode.Expand, scaler.screenMatchMode, "1920×1080 scales the whole canvas: no overflow");
        }

        /// <summary>AC50: art lives on the prefab only. Swapping a sprite/colour/font on an instance changes no behaviour.</summary>
        [UnityTest]
        public IEnumerator ArtSwapNeedsPrefabOnly()
        {
            using var rig = new LobbyUiRig();
            yield return null;
            foreach (var image in rig.Root.GetComponentsInChildren<Image>(true)) { image.sprite = null; image.color = Color.gray; }
            foreach (var text in rig.Root.GetComponentsInChildren<TMP_Text>(true)) text.color = Color.white;
            rig.JoinForm();
            rig.Click(rig.Root.JoinForm.Join);
            yield return rig.Until(() => rig.Visible(rig.Root.Lobby), 2f, "lobby still works without sprites");
            foreach (var file in Directory.GetFiles("Assets/_JungleMaze/Scripts/Presentation/UI/Lobby", "*.cs"))
            {
                var code = File.ReadAllText(file);
                StringAssert.DoesNotContain(".sprite", code, file);
                StringAssert.DoesNotContain("Resources.Load", code, file);
                StringAssert.DoesNotContain("TMP_FontAsset", code, file);
            }
        }
    }
}
#endif
