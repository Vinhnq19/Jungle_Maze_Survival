using System.Collections.Generic;
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
        public const string JoinLabel = "Vào phòng";
        public const string CancelLabel = "Hủy";
        public const string LeaveLabel = "Rời phòng";
        private const float BarButtonHeight = 28f;
        private static readonly Color HoverOverlay = new Color(1f, 1f, 1f, 0.14f);
        private static readonly Color PressedOverlay = new Color(0f, 0f, 0f, 0.22f);
        private ConnectionMenuPresenter _presenter;
        private GUIStyle _status;
        private UiMenuArtCatalog _art;
        private UiMenuArtCatalog _stylesFor;
        private bool _stylesBuilt;
        private string _backdropScene;
        private bool _backdropActive;
        private GUIStyle _panel, _primary, _secondary, _ghost, _label;
        private Vector2 _areaOrigin;
        private readonly Dictionary<string, Rect> _buttonRects = new Dictionary<string, Rect>();

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
        /// <summary>True when the backdrop is drawn this frame: art set, menu scene active and screen MainMenu/Connecting.</summary>
        public bool ShowsBackdrop => BackdropActive && _presenter != null
                                     && (_presenter.Screen == AppScreen.MainMenu || _presenter.Screen == AppScreen.Connecting);
        /// <summary>The cached style of the primary button (default skin copy when no art is configured).</summary>
        public GUIStyle PrimaryStyle => _primary;
        /// <summary>Height of the in-room/in-match bar: button plus the panel's vertical padding, so nothing is clipped.</summary>
        public float InGameBarHeight => BarButtonHeight + (_panel != null ? _panel.padding.vertical : 8f);

        /// <summary>
        /// Screen rect (GUI pixels, top-left origin) of a labelled button as drawn on the last repaint
        /// (<see cref="JoinLabel"/>, <see cref="CancelLabel"/>, <see cref="LeaveLabel"/>). For smoke tests and QA.
        /// </summary>
        public bool TryGetButtonRect(string label, out Rect rect) => _buttonRects.TryGetValue(label, out rect);

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
            BeginArea(new Rect(20, 20, 380, Screen.height - 40));
            GUILayout.Label("Jungle Maze — Kết nối", _status);

            var oldMode = _presenter.Mode;
            var newMode = (ConnectionMode)GUILayout.Toolbar((int)_presenter.Mode, ModeNames, GUILayout.Height(28));
            if (newMode != oldMode)
            {
                if (newMode == ConnectionMode.Local) _presenter.SelectTarget("Local");
                else if (newMode == ConnectionMode.LAN) _presenter.SelectTarget("LAN");
                else if (newMode == ConnectionMode.Online) _presenter.SelectTarget(_presenter.SelectedOnlineTarget ?? "AWS");
            }

            if (_presenter.Mode == ConnectionMode.Local)
            {
                GUILayout.Label("Chế độ chơi đơn Solo Offline (không cần server)", _label);
                GUILayout.Space(6);
                if (Button("Chơi Solo (Offline)", _primary, GUILayout.Height(36))) _presenter.StartOffline();
                GUILayout.Space(4);

            }
            else if (_presenter.Mode == ConnectionMode.LAN)
            {
                GUILayout.Label("Mạng nội bộ (LAN UDP)", _label);
                _presenter.Host = Field("IP máy chủ (LAN)", _presenter.Host);
                _presenter.RoomCode = Field("Mã phòng", _presenter.RoomCode);
                _presenter.DisplayName = Field("Tên hiển thị", _presenter.DisplayName);
                GUILayout.Space(6);
                GUI.enabled = !_presenter.IsBusy;
                if (Button(JoinLabel, _primary, GUILayout.Height(36))) _presenter.Join();
                GUI.enabled = true;
            }
            else // Online
            {
                GUILayout.Label("Mục tiêu Online", _label);
                GUILayout.BeginHorizontal();
                var isAws = _presenter.SelectedOnlineTarget == "AWS";
                if (Button("AWS Cloud", isAws ? _primary : _ghost, GUILayout.Height(26))) _presenter.SelectTarget("AWS");
                if (Button("Radmin / VPN", !isAws ? _primary : _ghost, GUILayout.Height(26))) _presenter.SelectTarget("Radmin");
                GUILayout.EndHorizontal();

                if (!isAws)
                {
                    _presenter.Host = Field("IP host Radmin/VPN", _presenter.Host);
                }
                else
                {
                    GUILayout.Label($"Máy chủ: {_presenter.Host} (Auto UDP->WSS)", _label);
                }
                _presenter.RoomCode = Field("Mã phòng", _presenter.RoomCode);
                _presenter.DisplayName = Field("Tên hiển thị", _presenter.DisplayName);
                if (_presenter.RequiresLogin)
                {
                    _presenter.ApiBaseUrl = Field("API base URL", _presenter.ApiBaseUrl);
                    _presenter.Username = Field("Tài khoản", _presenter.Username);
                    GUILayout.Label("Mật khẩu", _label);
                    _presenter.Password = GUILayout.PasswordField(_presenter.Password ?? "", '*');
                }
                GUILayout.Space(6);
                GUI.enabled = !_presenter.IsBusy;
                if (Button(JoinLabel, _primary, GUILayout.Height(36))) _presenter.Join();
                GUI.enabled = true;
            }

            GUILayout.Space(6);
            var advText = _presenter.AdvancedExpanded ? "▼ Cấu hình nâng cao" : "▶ Cấu hình nâng cao";
            if (Button(advText, _ghost, GUILayout.Height(24))) _presenter.AdvancedExpanded = !_presenter.AdvancedExpanded;
            if (_presenter.AdvancedExpanded)
            {
                GUILayout.Label("Profile (StreamingAssets/JungleMaze)", _label);
                GUILayout.BeginHorizontal();
                foreach (var profile in _presenter.Profiles)
                    if (Button(profile, _ghost)) _presenter.SelectProfile(profile);
                GUILayout.EndHorizontal();

                _presenter.Host = Field("Host", _presenter.Host);
                _presenter.RoomCode = Field("RoomCode", _presenter.RoomCode);
                if (_presenter.Mode == ConnectionMode.Local && Button(JoinLabel, _secondary, GUILayout.Height(30))) _presenter.Join();
                _presenter.Port = Field("Port", _presenter.Port);
                if (!_presenter.RequiresLogin) _presenter.ApiBaseUrl = Field("API base URL", _presenter.ApiBaseUrl);
                if (Button("Lưu endpoint", _secondary, GUILayout.Height(28))) _presenter.SaveEndpoint();
                GUILayout.Label($"Auth: {_presenter.AuthModeText} · Transport: {_presenter.TransportText}", _label);
                GUILayout.Label($"Nguồn: {_presenter.ConfigSource}", _label);
            }

            if (!string.IsNullOrEmpty(_presenter.Status)) GUILayout.Label(_presenter.Status, _status);
            GUILayout.Label("F3: thông số mạng · ESC: menu trong trận", _label);
            GUILayout.EndArea();
        }

        private void DrawConnecting()
        {
            BeginArea(new Rect(20, 20, 380, 120));
            GUILayout.Label(_presenter.Status, _status);
            if (Button(CancelLabel, _secondary, GUILayout.Height(28))) _presenter.Disconnect();
            GUILayout.EndArea();
        }

        private void DrawInGameBar()
        {
            // Height follows the panel padding (10 + 10 with art), so the 28 px button is never clipped.
            BeginArea(new Rect(10, 10, 520, InGameBarHeight));
            GUILayout.BeginHorizontal();
            var text = _presenter.IsOffline
                ? $"{_presenter.Screen} · Solo Offline · {_presenter.DisplayName}"
                : $"{_presenter.Screen}";
            GUILayout.Label(text, _label, GUILayout.Height(BarButtonHeight));
            if (Button(LeaveLabel, _secondary, GUILayout.Width(90), GUILayout.Height(BarButtonHeight))) _presenter.Disconnect();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void BeginArea(Rect rect)
        {
            _areaOrigin = rect.position;
            GUILayout.BeginArea(rect, _panel);
        }

        /// <summary>
        /// GUILayout button with visible hover/pressed feedback for the art faces (a light or dark overlay, no new
        /// texture) and its screen rect recorded on repaint. Returns the click exactly like GUILayout.Button.
        /// </summary>
        private bool Button(string text, GUIStyle style, params GUILayoutOption[] options)
        {
            var clicked = GUILayout.Button(text, style, options);
            if (Event.current.type != EventType.Repaint) return clicked;
            var rect = GUILayoutUtility.GetLastRect();
            _buttonRects[text] = new Rect(rect.position + _areaOrigin, rect.size);
            if (_art == null || !GUI.enabled || !rect.Contains(Event.current.mousePosition)) return clicked;
            var color = GUI.color;
            GUI.color = GUIUtility.hotControl != 0 ? PressedOverlay : HoverOverlay;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            return clicked;
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
            BackdropLayout(_art, Screen.width, Screen.height, out var logo, out var keyArt);
            if (logo.width > 0f) GUI.DrawTexture(logo, _art.Logo, ScaleMode.ScaleToFit);
            if (keyArt.width > 0f) GUI.DrawTexture(keyArt, _art.KeyArt, ScaleMode.ScaleToFit);
        }

        /// <summary>
        /// Logo and key art rects in the column right of the 20 + 380 px panel (40 px gap), logo above key art, both
        /// centred. Empty rects when the art is missing or the screen is too narrow. Public for layout tests.
        /// </summary>
        public static void BackdropLayout(UiMenuArtCatalog art, float screenWidth, float screenHeight, out Rect logo, out Rect keyArt)
        {
            logo = keyArt = default;
            const float left = 440f;
            var width = screenWidth - left - 40f;
            if (art == null || width < 160f) return;
            var top = 40f;
            if (art.Logo != null)
            {
                var logoWidth = Mathf.Min(width, 640f);
                var logoHeight = logoWidth * art.Logo.height / art.Logo.width;
                logo = new Rect(left + (width - logoWidth) * 0.5f, top, logoWidth, logoHeight);
                top += logoHeight + 24f;
            }
            if (art.KeyArt != null)
            {
                var size = Mathf.Min(width, screenHeight - top - 40f);
                if (size > 64f) keyArt = new Rect(left + (width - size) * 0.5f, top, size, size);
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
            state.textColor = text;
        }
    }
}
