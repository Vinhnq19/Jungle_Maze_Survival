#if UNITY_EDITOR || DEVELOPMENT_BUILD
using JungleMaze.App.MatchUi.Mocks;
using JungleMaze.Core.DI;
using JungleMaze.Domain.MatchUi;
using UnityEngine;

namespace JungleMaze.Presentation.UI.MatchOverlay
{
    /// <summary>
    /// Dev-only UI-03 panel, toggled with F7 (F3–F6 are taken). Thin IMGUI over <see cref="MatchOverlayScenarios"/>:
    /// mock or live sources, match preview, the five elimination causes, killcam on/off and the eight replay cases,
    /// spectator count, "watched player out" and "match ended". Not compiled into Release builds.
    /// </summary>
    public sealed class MatchOverlayDebugPanel : MonoBehaviour
    {
        private static readonly string[] Cases = { "Full", "None", "Short", "Slow", "WrongMatch", "WrongPlayer", "WrongWindow", "Drift" };

        private MatchOverlayScenarios _scenarios;
        private bool _visible;

        [Inject]
        public void Construct(MatchOverlayScenarios scenarios) => _scenarios = scenarios;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F7)) _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible || _scenarios == null) return;
            GUILayout.BeginArea(new Rect(Screen.width - 330, 10, 320, 520), GUI.skin.box);
            GUILayout.Label("UI-03 overlay (F7)");
            var mocks = GUILayout.Toggle(_scenarios.UsingMocks, "Dùng dữ liệu giả");
            if (mocks != _scenarios.UsingMocks) _scenarios.UseMocks(mocks);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Xem thử trận")) _scenarios.PreviewMatch();
            if (GUILayout.Button("Thoát xem thử")) _scenarios.EndPreview();
            GUILayout.EndHorizontal();

            // Both read the live state, so `-killcam` or a change made elsewhere shows as it is.
            var killcamOn = _scenarios.KillcamEnabled;
            if (GUILayout.Toggle(killcamOn, "Killcam bật") != killcamOn) _scenarios.SetKillcam(!killcamOn);
            var current = (int)_scenarios.Replay.Mode;
            var picked = GUILayout.SelectionGrid(current, Cases, 3);
            if (picked != current) _scenarios.SetReplayCase((MockKillcamReplaySource.Case)picked);

            GUILayout.Label("Bị loại");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Người chơi")) _scenarios.Eliminate(EliminationCause.Player);
            if (GUILayout.Button("Bo")) _scenarios.Eliminate(EliminationCause.Zone);
            if (GUILayout.Button("Rời")) _scenarios.Eliminate(EliminationCause.LeftMatch);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Mất kết nối")) _scenarios.Eliminate(EliminationCause.Disconnected);
            if (GUILayout.Button("Không rõ")) _scenarios.Eliminate(EliminationCause.Unknown);
            if (GUILayout.Button("Thiếu dữ liệu")) _scenarios.EliminateMinimal();
            GUILayout.EndHorizontal();

            GUILayout.Label("Spectator");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("9 người")) _scenarios.Populate(9);
            if (GUILayout.Button("1 người")) _scenarios.Populate(1);
            if (GUILayout.Button("0 người")) _scenarios.Populate(0);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Người đang xem bị loại")) _scenarios.EliminateWatched();
            if (GUILayout.Button("Hết trận")) _scenarios.EndMatch();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
#endif
