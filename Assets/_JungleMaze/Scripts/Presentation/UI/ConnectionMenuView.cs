using JungleMaze.App.Config;
using JungleMaze.App.Flow;
using JungleMaze.Core.DI;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        private UiMenuArtCatalog _art;
        private UiMenuArtCatalog _stylesFor;
        private bool _stylesBuilt;
        private string _backdropScene;
        private bool _backdropActive;
        private GUIStyle _panel, _primary, _secondary, _ghost, _label;

        [Inject]
        public void Construct(ConnectionMenuPresenter presenter) => _presenter = presenter;

        /// <summary>
        /// Direction A menu art (ADHOC-UI-ASSETS-A). Only keeps the reference; styles are built once on the next
        /// OnGUI. Null keeps the default IMGUI skin. Never changes which screen or action is shown. The full-screen
        /// background, logo and key art are drawn only while <paramref name="backdropScene"/> (the menu scene) is
        /// active, so Play in Game/HudPreview still shows the world and HUD behind the panel.
        /// </summary>
        public void ConfigureArt(UiMenuArtCatalog art, string backdropScene)
        {
            _art = art;
            _stylesBuilt = false;
            _backdropScene = backdropScene;
            _backdropActive = SceneManager.GetActiveScene().name == backdropScene;
        }

        public UiMenuArtCatalog Art => _art;
        /// <summary>True when the backdrop (background, logo, key art) is drawn on MainMenu/Connecting.</summary>
        public bool BackdropActive => _art != null && _backdropActive;

        private void OnEnable() => SceneManager.activeSceneChanged += OnActiveSceneChanged;

        private void OnDisable() => SceneManager.activeSceneChanged -= OnActiveSceneChanged;

        private void OnActiveSceneChanged(Scene previous, Scene next) => _backdropActive = next.name == _backdropScene;

        private void OnGUI()
        {
            if (_presenter == null) return;
            _status ??= new GUIStyle(GUI.skin.label) { wordWrap = true, fontStyle = FontStyle.Bold };
            if (!_stylesBuilt || _stylesFor != _art) BuildStyles();
            switch (_presenter.Screen)
            {
                case AppScreen.MainMenu: DrawBackdrop(); DrawMenu(); break;
                case AppScreen.Connecting: DrawBackdrop(); DrawConnecting(); break;
                case AppScreen.InRoom:
                case AppScreen.InMatch:
                case AppScreen.Results: DrawInGameBar(); break;
            }
        }

        private void DrawMenu()
        {
            GUILayout.BeginArea(new Rect(20, 20, 380, Screen.height - 40), _panel);
            GUILayout.Label("Jungle Maze — Kết nối", _status);

            GUILayout.Label("Profile (StreamingAssets/JungleMaze)", _label);
            GUILayout.BeginHorizontal();
            foreach (var profile in _presenter.Profiles)
                if (GUILayout.Button(profile, _ghost)) _presenter.SelectProfile(profile);
            GUILayout.EndHorizontal();

            _presenter.Mode = (ConnectionMode)GUILayout.Toolbar((int)_presenter.Mode, ModeNames);
            _presenter.Host = Field("Game server host", _presenter.Host);
            _presenter.Port = Field("Port", _presenter.Port);
            if (_presenter.RequiresLogin)
            {
                _presenter.ApiBaseUrl = Field("API base URL", _presenter.ApiBaseUrl);
                _presenter.Username = Field("Tài khoản", _presenter.Username);
                GUILayout.Label("Mật khẩu", _label);
                _presenter.Password = GUILayout.PasswordField(_presenter.Password ?? "", '*');
            }
            _presenter.RoomCode = Field("Mã phòng", _presenter.RoomCode);
            _presenter.DisplayName = Field("Tên hiển thị", _presenter.DisplayName);

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUI.enabled = !_presenter.IsBusy;
            if (GUILayout.Button("Vào phòng", _primary, GUILayout.Height(32))) _presenter.Join();
            if (GUILayout.Button("Lưu endpoint", _secondary, GUILayout.Height(32))) _presenter.SaveEndpoint();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label($"Auth: {_presenter.AuthModeText} · Transport: {_presenter.TransportText} · Nguồn: {_presenter.ConfigSource}", _label);
            if (!string.IsNullOrEmpty(_presenter.Status)) GUILayout.Label(_presenter.Status, _status);
            GUILayout.Label("F3: bật/tắt thông số mạng", _label);
            GUILayout.EndArea();
        }

        private void DrawConnecting()
        {
            GUILayout.BeginArea(new Rect(20, 20, 380, 120), _panel);
            GUILayout.Label(_presenter.Status, _status);
            if (GUILayout.Button("Hủy", _secondary, GUILayout.Height(28))) _presenter.Disconnect();
            GUILayout.EndArea();
        }

        private void DrawInGameBar()
        {
            GUILayout.BeginArea(new Rect(10, 10, 520, 34), _panel);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{_presenter.Screen} · {_presenter.Endpoint} · {_presenter.PlayerId}", _label);
            if (GUILayout.Button("Rời phòng", _secondary, GUILayout.Width(90))) _presenter.Disconnect();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private string Field(string label, string value)
        {
            GUILayout.Label(label, _label);
            return GUILayout.TextField(value ?? "");
        }

        /// <summary>
        /// Full-screen background plus logo and key art in the free space right of the control panel. Pure drawing:
        /// GUI.DrawTexture takes no input, so every control stays clickable. Only on MainMenu/Connecting.
        /// </summary>
        private void DrawBackdrop()
        {
            if (!BackdropActive || Event.current.type != EventType.Repaint) return;
            if (_art.Background != null) GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _art.Background, ScaleMode.ScaleAndCrop);
            // Column right of the 20 + 380 px panel with a 40 px gap; logo above key art, both centred in it.
            const float left = 440f;
            var width = Screen.width - left - 40f;
            if (width < 160f) return;
            var top = 40f;
            if (_art.Logo != null)
            {
                var logoWidth = Mathf.Min(width, 640f);
                var logoHeight = logoWidth * _art.Logo.height / _art.Logo.width;
                GUI.DrawTexture(new Rect(left + (width - logoWidth) * 0.5f, top, logoWidth, logoHeight), _art.Logo, ScaleMode.ScaleToFit);
                top += logoHeight + 24f;
            }
            if (_art.KeyArt != null)
            {
                var size = Mathf.Min(width, Screen.height - top - 40f);
                if (size > 64f) GUI.DrawTexture(new Rect(left + (width - size) * 0.5f, top, size, size), _art.KeyArt, ScaleMode.ScaleToFit);
            }
        }

        /// <summary>
        /// Builds the cached styles from the current skin and art. Runs on the first OnGUI and after
        /// <see cref="ConfigureArt"/>; button faces are 9-sliced with GUIStyle.border. GUI.skin itself is not changed.
        /// </summary>
        private void BuildStyles()
        {
            _stylesFor = _art;
            _stylesBuilt = true;
            _panel = new GUIStyle(GUI.skin.box);
            _primary = new GUIStyle(GUI.skin.button);
            _secondary = new GUIStyle(GUI.skin.button);
            _ghost = new GUIStyle(GUI.skin.button);
            _label = new GUIStyle(GUI.skin.label);
            if (_art == null) return;
            var border = _art.ImguiBorder;
            Face(_panel, _art.ButtonGhost, border, _art.Text);
            _panel.padding = new RectOffset(12, 12, 10, 10);
            Face(_primary, _art.ButtonPrimary, border, _art.Text);
            Face(_secondary, _art.ButtonSecondary, border, _art.Text);
            Face(_ghost, _art.ButtonGhost, border, _art.Text);
            _label.normal.textColor = _art.Muted;
            _status.normal.textColor = _art.Text;
        }

        private static void Face(GUIStyle style, Texture2D texture, int border, Color text)
        {
            if (texture == null) return;
            style.border = new RectOffset(border, border, border, border);
            SetState(style.normal, texture, text);
            SetState(style.hover, texture, Color.white);
            SetState(style.active, texture, Color.white);
            SetState(style.focused, texture, text);
            SetState(style.onNormal, texture, text);
            SetState(style.onHover, texture, Color.white);
            SetState(style.onActive, texture, Color.white);
            SetState(style.onFocused, texture, text);
        }

        private static void SetState(GUIStyleState state, Texture2D texture, Color text)
        {
            state.background = texture;
            state.scaledBackgrounds = null;
            state.textColor = text;
        }
    }
}
