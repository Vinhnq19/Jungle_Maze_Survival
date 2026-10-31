#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using JungleMaze.App.Lobby;
using JungleMaze.App.Lobby.Mocks;
using UnityEngine;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>
    /// Dev-only UI-02 panel, toggled with F9 (F3–F8 are taken). Picks a <see cref="LobbyCase"/> (AC44: the full run
    /// and the six DoD cases) or the live ports, and flips the N9 switches. Not compiled into Release builds.
    /// </summary>
    public sealed class LobbyDebugPanel : MonoBehaviour
    {
        private static readonly LobbyCase[] Cases = (LobbyCase[])Enum.GetValues(typeof(LobbyCase));
        private LobbyScenarios _scenarios;
        private LobbyFlow _flow;
        private bool _visible;

        public void Construct(LobbyScenarios scenarios, LobbyFlow flow)
        {
            _scenarios = scenarios;
            _flow = flow;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9)) _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible || _scenarios == null) return;
            GUILayout.BeginArea(new Rect(10, 10, 300, 470), GUI.skin.box);
            GUILayout.Label("UI-02 lobby (F9) · màn: " + _flow.Screen);
            GUILayout.Label(_scenarios.UsingMocks ? "Đang dùng dữ liệu giả: " + _scenarios.Case : "Đang dùng nguồn thật");
            if (GUILayout.Button("Dùng nguồn thật")) { _flow.GoHome(); _scenarios.UseLive(); }
            foreach (var c in Cases)
                if (GUILayout.Button("Giả: " + c)) { _flow.GoHome(); _scenarios.Use(c); }
            var o = _flow.Options;
            o.ReadyEnabled = GUILayout.Toggle(o.ReadyEnabled, "lobbyReadyEnabled");
            o.StartEnabled = GUILayout.Toggle(o.StartEnabled, "lobbyStartEnabled");
            o.ReconnectEnabled = GUILayout.Toggle(o.ReconnectEnabled, "reconnectEnabled");
            o.HistoryEnabled = GUILayout.Toggle(o.HistoryEnabled, "historyEnabled");
            GUILayout.EndArea();
        }
    }
}
#endif
