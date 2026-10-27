#if UNITY_EDITOR || DEVELOPMENT_BUILD
using JungleMaze.App.Audio;
using JungleMaze.App.Audio.Mocks;
using JungleMaze.App.Flow;
using JungleMaze.App.Hud.State;
using JungleMaze.Core.DI;
using JungleMaze.Domain.Audio;
using UnityEngine;

namespace JungleMaze.Presentation.Audio
{
    /// <summary>
    /// Dev-only audio test panel (AC-A8), toggled with F6, bottom-left. Buttons go through the mock cue source, so
    /// they take the same router → service path as real events. Not compiled into release builds.
    /// </summary>
    public sealed class AudioDebugPanel : MonoBehaviour
    {
        private static readonly float[] Distances = { 0f, 6f, 12f };
        private static readonly AppScreen[] MusicScreens =
            { AppScreen.Boot, AppScreen.MainMenu, AppScreen.Connecting, AppScreen.InRoom, AppScreen.InMatch, AppScreen.Results };

        private IAudioService _audio;
        private MockAudioEventSource _mock;
        private IHudState _hud;
        private IHudStateWriter _hudWriter;
        private MusicPlan _music;
        private bool _visible;
        private int _distance;
        private Vector2 _scroll;

        [Inject]
        public void Construct(IAudioService audio, MockAudioEventSource mock, IHudState hud, IHudStateWriter hudWriter, MusicPlan music)
        {
            _audio = audio;
            _mock = mock;
            _hud = hud;
            _hudWriter = hudWriter;
            _music = music;
        }

        public bool Visible { get => _visible; set => _visible = value; }

        /// <summary>Plays <paramref name="id"/> 2D, or 0/6/12 cells east of the Hero for positional ids.</summary>
        public void PlayButton(SfxId id)
        {
            if (_mock == null) return;
            if (_hud != null && _hud.HasPose && _distance > 0)
            {
                var pose = _hud.Pose;
                _mock.Raise(id, pose.X + Distances[_distance], pose.Y, 1);
            }
            else _mock.Raise(id);
        }

        public void SpamButton() => _mock?.Spam(100);
        public void RepeatButton() => _mock?.RepeatServerEvent();
        public void BurstButton() => _mock?.StartBurst();
        public void FakePoseButton() => _hudWriter?.SetPose(0f, 0f, 0f);

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F6)) _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible || _audio == null) return;
            var height = Mathf.Min(560f, Screen.height - 32f);
            GUI.Window(GetInstanceID(), new Rect(16f, Screen.height - height - 16f, 420f, height), Draw, "Audio · F6 (dev only)");
        }

        private void Draw(int windowId)
        {
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label($"Voices {_audio.ActiveVoices}/{_audio.MaxVoices}" + (_mock != null && _mock.BurstRunning ? "  · burst running" : ""));
            Slider(AudioBus.Master, "Master");
            Slider(AudioBus.Sfx, "SFX");
            Slider(AudioBus.Music, "Music");

            GUILayout.BeginHorizontal();
            var music = GUILayout.Toggle(_audio.MusicEnabled, "BGM");
            if (music != _audio.MusicEnabled) _audio.MusicEnabled = music;
            if (_music != null)
            {
                GUILayout.Label("Screen:", GUILayout.Width(50f));
                foreach (var screen in MusicScreens)
                    if (GUILayout.Toggle(_music.Screen == screen, screen.ToString(), "Button")) if (_music.Screen != screen) _music.SetScreen(screen);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Spam 100")) SpamButton();
            if (GUILayout.Button("Server event ×5")) RepeatButton();
            if (GUILayout.Button(_mock != null && _mock.BurstRunning ? "Stop burst" : "Burst 10 s"))
            {
                if (_mock != null && _mock.BurstRunning) _mock.StopBurst(); else BurstButton();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(_hud != null && _hud.HasPose ? "Positional at:" : "No Hero pose:", GUILayout.Width(90f));
            _distance = GUILayout.Toolbar(_distance, new[] { "0", "6", "12 cells" });
            if (GUILayout.Button("Fake pose (0,0)")) FakePoseButton();
            GUILayout.EndHorizontal();

            for (var i = 1; i <= SfxIds.Count; i += 2)
            {
                GUILayout.BeginHorizontal();
                for (var k = i; k < i + 2 && k <= SfxIds.Count; k++)
                    if (GUILayout.Button($"{k}. {(SfxId)k}", GUILayout.Width(190f))) PlayButton((SfxId)k);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private void Slider(AudioBus bus, string label)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(50f));
            var value = GUILayout.HorizontalSlider(_audio.GetVolume(bus), 0f, 1f);
            GUILayout.Label(value.ToString("0.00"), GUILayout.Width(36f));
            GUILayout.EndHorizontal();
            if (!Mathf.Approximately(value, _audio.GetVolume(bus))) _audio.SetVolume(bus, value);
        }
    }
}
#endif
