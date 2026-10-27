#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using JungleMaze.App.Flow;
using JungleMaze.Presentation.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace JungleMaze.Tests.PlayMode
{
    /// <summary>
    /// ADHOC-UI-ASSETS-A A8 smoke on the real Boot scene and Bootstrap: menu art (backdrop, logo, key art) only on
    /// MainMenu/Connecting, logo/key art never over the control panel or its buttons, buttons drawn with the art, and
    /// <c>ConfigureArt(null)</c> back to the default skin. Screens are driven through the same presenter actions the
    /// buttons call. Synthetic mouse clicks cannot be delivered to player IMGUI from the editor test runner (the Game
    /// view input queue and <c>EditorWindow.SendEvent</c> were both tried), so a real click stays a manual QA step.
    /// No server is needed: a join with no server stays Connecting and is cancelled; with a local server it reaches
    /// InRoom, where the leave button must fit inside the bar.
    /// </summary>
    public sealed class ConnectionMenuSmokeTests
    {
        private const string BootScene = "Assets/_JungleMaze/Scenes/Boot.unity";
        private const string BootName = "Boot";
        private const string NoRoomCode = "Nhập mã phòng."; // ConnectToServerUseCase error for an empty room code
        private static readonly Rect Panel = new Rect(20f, 20f, 380f, 0f);

        private ConnectionMenuView _view;
        private ConnectionMenuPresenter _presenter;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_presenter != null && _presenter.Screen != AppScreen.MainMenu) _presenter.Disconnect();
            if (_view != null) Object.Destroy(_view.gameObject); // the DontDestroyOnLoad Bootstrap
            yield return null;
        }

        [UnityTest]
        public IEnumerator BootMenu_ArtOnlyOnMenuScreens_ControlsClear_NullArtFallsBack()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(BootScene, new LoadSceneParameters(LoadSceneMode.Single));
            for (var i = 0; i < 300 && (_presenter == null || _presenter.Screen != AppScreen.MainMenu); i++)
            {
                _view = Object.FindAnyObjectByType<ConnectionMenuView>();
                _presenter = _view != null ? PresenterOf(_view) : null;
                yield return null;
            }
            Assert.IsNotNull(_presenter, "Bootstrap composed the connection menu");
            Assert.AreEqual(AppScreen.MainMenu, _presenter.Screen);

            var art = _view.Art;
            Assert.IsNotNull(art, "Boot wires the direction A menu art");
            Assert.AreEqual(6, art.AssignedCount);
            yield return Frames(3);
            Assert.IsTrue(_view.ShowsBackdrop, "backdrop on MainMenu in the menu scene");
            Assert.AreSame(art.ButtonPrimary, _view.PrimaryStyle.normal.background, "art skin");
            AssertControlsClear(art, ConnectionMenuView.JoinLabel);
            foreach (var size in new[] { new Vector2(1280f, 720f), new Vector2(1920f, 1080f), new Vector2(2560f, 1440f) })
                AssertBackdropClearOfPanel(art, size);

            // The Join action with no room code reports the error and keeps the menu (what the button calls).
            _presenter.RoomCode = "";
            _presenter.Join();
            Assert.AreEqual(NoRoomCode, _presenter.Status);
            Assert.AreEqual(AppScreen.MainMenu, _presenter.Screen);

            _presenter.RoomCode = "SMOKE";
            _presenter.Join();
            for (var i = 0; i < 180 && _presenter.Screen == AppScreen.MainMenu; i++) yield return null;
            yield return Frames(3);
            if (_presenter.Screen == AppScreen.Connecting)
            {
                Assert.IsTrue(_view.ShowsBackdrop, "backdrop on Connecting");
                AssertControlsClear(art, ConnectionMenuView.CancelLabel);
                // With a local GameServer (room code SMOKE) the join completes; without one it stays Connecting.
                for (var i = 0; i < 180 && _presenter.Screen == AppScreen.Connecting; i++) yield return null;
                yield return Frames(3);
            }
            if (_presenter.Screen == AppScreen.InRoom || _presenter.Screen == AppScreen.InMatch)
            {
                Assert.IsFalse(_view.ShowsBackdrop, "no backdrop in room/match");
                Assert.IsTrue(_view.TryGetButtonRect(ConnectionMenuView.LeaveLabel, out var leave), "leave button drawn");
                Assert.GreaterOrEqual(leave.yMin, 10f);
                Assert.LessOrEqual(leave.yMax, 10f + _view.InGameBarHeight + 0.5f, "leave button fits inside the bar");
                Assert.GreaterOrEqual(leave.height, 27.5f, "leave button keeps its 28 px height");
                Debug.Log($"[Smoke] in-room bar height {_view.InGameBarHeight}, leave button {leave}");
            }
            _presenter.Disconnect();
            for (var i = 0; i < 300 && _presenter.Screen != AppScreen.MainMenu; i++) yield return null;
            Assert.AreEqual(AppScreen.MainMenu, _presenter.Screen, "cancel/leave returns to the menu");

            // No art: default skin, no backdrop, buttons still drawn.
            _view.ConfigureArt(null, BootName);
            yield return Frames(3);
            Assert.IsFalse(_view.ShowsBackdrop);
            Assert.AreNotSame(art.ButtonPrimary, _view.PrimaryStyle.normal.background, "default skin");
            Assert.IsTrue(_view.TryGetButtonRect(ConnectionMenuView.JoinLabel, out _));

            _view.ConfigureArt(art, BootName);
            yield return Frames(3);
            Assert.IsTrue(_view.ShowsBackdrop);
            Assert.AreSame(art.ButtonPrimary, _view.PrimaryStyle.normal.background);
        }

        private void AssertControlsClear(UiMenuArtCatalog art, string label)
        {
            Assert.IsTrue(_view.TryGetButtonRect(label, out var button), $"'{label}' drawn");
            Assert.Greater(button.width, 0f);
            Assert.GreaterOrEqual(button.xMin, Panel.xMin);
            Assert.LessOrEqual(button.xMax, Panel.xMax);
            ConnectionMenuView.BackdropLayout(art, Screen.width, Screen.height, out var logo, out var keyArt);
            Assert.IsFalse(logo.Overlaps(button), $"logo over '{label}'");
            Assert.IsFalse(keyArt.Overlaps(button), $"key art over '{label}'");
        }

        private static void AssertBackdropClearOfPanel(UiMenuArtCatalog art, Vector2 size)
        {
            ConnectionMenuView.BackdropLayout(art, size.x, size.y, out var logo, out var keyArt);
            var panel = new Rect(Panel.x, Panel.y, Panel.width, size.y - 40f);
            Assert.Greater(logo.width, 0f, $"logo shown at {size}");
            Assert.Greater(keyArt.width, 0f, $"key art shown at {size}");
            Assert.IsFalse(logo.Overlaps(panel), $"logo over the panel at {size}");
            Assert.IsFalse(keyArt.Overlaps(panel), $"key art over the panel at {size}");
            Assert.LessOrEqual(keyArt.yMax, size.y, $"key art on screen at {size}");
        }

        private static ConnectionMenuPresenter PresenterOf(ConnectionMenuView view) =>
            (ConnectionMenuPresenter)typeof(ConnectionMenuView).GetField("_presenter", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(view);

        private static IEnumerator Frames(int n)
        {
            for (var i = 0; i < n; i++) yield return null;
        }
    }
}
#endif
