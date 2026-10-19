using JungleMaze.App.Config;
using JungleMaze.App.Flow;
using JungleMaze.Core.DI;
using UnityEngine;

namespace JungleMaze.Presentation.UI
{
    /// <summary>
    /// View (MVP) bằng IMGUI: chỉ vẽ và chuyển thao tác cho presenter. Thay bằng uGUI chỉ cần viết view mới
    /// gọi cùng các method của ConnectionMenuPresenter.
    /// </summary>
    public sealed class ConnectionMenuView : MonoBehaviour
    {
        private static readonly string[] ModeNames = { "Local", "LAN", "Online" };
        private ConnectionMenuPresenter _presenter;
        private GUIStyle _status;

        [Inject]
        public void Construct(ConnectionMenuPresenter presenter) => _presenter = presenter;

        private void OnGUI()
        {
            if (_presenter == null) return;
            _status ??= new GUIStyle(GUI.skin.label) { wordWrap = true, fontStyle = FontStyle.Bold };
            switch (_presenter.Screen)
            {
                case AppScreen.MainMenu: DrawMenu(); break;
                case AppScreen.Connecting: DrawConnecting(); break;
                case AppScreen.InRoom:
                case AppScreen.InMatch:
                case AppScreen.Results: DrawInGameBar(); break;
            }
        }

        private void DrawMenu()
        {
            GUILayout.BeginArea(new Rect(20, 20, 380, Screen.height - 40), GUI.skin.box);
            GUILayout.Label("Jungle Maze — Kết nối", _status);

            GUILayout.Label("Profile (StreamingAssets/JungleMaze)");
            GUILayout.BeginHorizontal();
            foreach (var profile in _presenter.Profiles)
                if (GUILayout.Button(profile)) _presenter.SelectProfile(profile);
            GUILayout.EndHorizontal();

            _presenter.Mode = (ConnectionMode)GUILayout.Toolbar((int)_presenter.Mode, ModeNames);
            _presenter.Host = Field("Game server host", _presenter.Host);
            _presenter.Port = Field("Port", _presenter.Port);
            if (_presenter.RequiresLogin)
            {
                _presenter.ApiBaseUrl = Field("API base URL", _presenter.ApiBaseUrl);
                _presenter.Username = Field("Tài khoản", _presenter.Username);
                GUILayout.Label("Mật khẩu");
                _presenter.Password = GUILayout.PasswordField(_presenter.Password ?? "", '*');
            }
            _presenter.RoomCode = Field("Mã phòng", _presenter.RoomCode);
            _presenter.DisplayName = Field("Tên hiển thị", _presenter.DisplayName);

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUI.enabled = !_presenter.IsBusy;
            if (GUILayout.Button("Vào phòng", GUILayout.Height(32))) _presenter.Join();
            if (GUILayout.Button("Lưu endpoint", GUILayout.Height(32))) _presenter.SaveEndpoint();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label($"Auth: {_presenter.AuthModeText} · Nguồn config: {_presenter.ConfigSource}");
            if (!string.IsNullOrEmpty(_presenter.Status)) GUILayout.Label(_presenter.Status, _status);
            GUILayout.Label("F3: bật/tắt thông số mạng");
            GUILayout.EndArea();
        }

        private void DrawConnecting()
        {
            GUILayout.BeginArea(new Rect(20, 20, 380, 120), GUI.skin.box);
            GUILayout.Label(_presenter.Status, _status);
            if (GUILayout.Button("Hủy", GUILayout.Height(28))) _presenter.Disconnect();
            GUILayout.EndArea();
        }

        private void DrawInGameBar()
        {
            GUILayout.BeginArea(new Rect(10, 10, 520, 34), GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{_presenter.Screen} · {_presenter.Endpoint} · {_presenter.PlayerId}");
            if (GUILayout.Button("Rời phòng", GUILayout.Width(90))) _presenter.Disconnect();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private static string Field(string label, string value)
        {
            GUILayout.Label(label);
            return GUILayout.TextField(value ?? "");
        }
    }
}
