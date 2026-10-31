using System;
using JungleMaze.App.Audio;
using JungleMaze.App.MatchUi;
using JungleMaze.Domain.Audio;
using JungleMaze.Domain.MatchUi;
using UnityEngine;
using UnityEngine.Events;

namespace JungleMaze.Presentation.UI.MatchOverlay
{
    /// <summary>
    /// Connects the UI-03 logic to the prefab views. Setting rows write straight to <see cref="IAudioService"/> and
    /// <see cref="IPlayerSettingsWriter"/> and read back in the same frame (AC7, AC10); the menu and confirm box are
    /// shown and hidden by CanvasGroup fades only (AC1). The canvas is off outside a match. The phase panels
    /// (killcam, eliminated, spectator, notice) are in <c>MatchOverlayPresenter.Phases.cs</c>.
    /// </summary>
    public sealed partial class MatchOverlayPresenter : IDisposable
    {
        public const string MusicOffText = "Nhạc nền đang tắt";
        public const string RetryText = "Chưa thoát được, thử lại";

        private readonly MatchOverlayRoot _root;
        private readonly MatchMenuController _menu;
        private readonly MatchOverlayFlow _flow;
        private readonly IPlayerSettingsWriter _settings;
        private readonly IAudioService _audio;
        private readonly MatchOverlayTuning _tuning;
        private readonly int _maxHp;
        private readonly OverlayFade _menuFade, _confirmFade;
        private readonly Action<bool> _onMenuOpen, _onConfirm;
        private readonly Action _onLeaveState;
        private readonly Action<PlayerSettingsChanged> _onSettings;
        private readonly Action<MatchOverlayPhase> _onPhase;
        private readonly UnityAction<float> _onMaster, _onSfx, _onMusic, _onSensitivity;
        private readonly UnityAction<bool> _onMusicEnabled, _onShake;
        private readonly UnityAction _onResume, _onDefaults, _onLeave, _onConfirmLeave, _onStay;

        public MatchOverlayPresenter(MatchOverlayRoot root, MatchMenuController menu, MatchOverlayFlow flow,
            IPlayerSettingsWriter settings, IAudioService audio, MatchOverlayTuning tuning = null, int heroMaxHp = 100)
        {
            _root = root ? root : throw new ArgumentNullException(nameof(root));
            _menu = menu ?? throw new ArgumentNullException(nameof(menu));
            _flow = flow;
            _settings = settings;
            _audio = audio;
            _tuning = tuning ?? MatchOverlayTuning.Default;
            _maxHp = heroMaxHp > 0 ? heroMaxHp : 100;
            _menuFade = new OverlayFade(root.Menu.Group);
            _confirmFade = new OverlayFade(root.Confirm.Group);

            _onMenuOpen = OnMenuOpen;
            _onConfirm = open => _confirmFade.Set(open);
            _onLeaveState = RefreshLeaveState;
            _onSettings = _ => RefreshSettingRows();
            _onPhase = OnPhase;
            _onMaster = v => SetVolume(AudioBus.Master, v);
            _onSfx = v => SetVolume(AudioBus.Sfx, v);
            _onMusic = v => SetVolume(AudioBus.Music, v);
            _onSensitivity = v => _settings?.SetMouseSensitivity(v / 10f);
            _onMusicEnabled = SetMusicEnabled;
            _onShake = on => _settings?.SetScreenShake(on);
            _onResume = _menu.Close;
            _onDefaults = ResetDefaults;
            _onLeave = _menu.RequestLeave;
            _onConfirmLeave = _menu.ConfirmLeave;
            _onStay = _menu.CancelLeave;

            _menu.OpenChanged += _onMenuOpen;
            _menu.ConfirmChanged += _onConfirm;
            _menu.LeaveStateChanged += _onLeaveState;
            if (_settings != null) _settings.Changed += _onSettings;
            if (_flow != null) _flow.PhaseChanged += _onPhase;

            var v = root.Menu;
            v.Master.onValueChanged.AddListener(_onMaster);
            v.Sfx.onValueChanged.AddListener(_onSfx);
            v.Music.onValueChanged.AddListener(_onMusic);
            v.Sensitivity.onValueChanged.AddListener(_onSensitivity);
            v.MusicEnabled.onValueChanged.AddListener(_onMusicEnabled);
            v.ScreenShake.onValueChanged.AddListener(_onShake);
            v.Resume.onClick.AddListener(_onResume);
            v.Defaults.onClick.AddListener(_onDefaults);
            v.Leave.onClick.AddListener(_onLeave);
            root.Confirm.Confirm.onClick.AddListener(_onConfirmLeave);
            root.Confirm.Stay.onClick.AddListener(_onStay);

            BindPhases();
            RefreshSettingRows();
            RefreshLeaveState();
            OnPhase(_flow != null ? _flow.Phase : MatchOverlayPhase.Playing);
        }

        public OverlayFade MenuFade => _menuFade;
        public OverlayFade ConfirmFade => _confirmFade;

        public void Tick(float unscaledDelta)
        {
            _menuFade.Step(unscaledDelta, _tuning.MenuFadeSeconds);
            _confirmFade.Step(unscaledDelta, _tuning.MenuFadeSeconds);
            TickPhases(unscaledDelta);
        }

        /// <summary>Re-reads every row from its owner (opening the menu, after "Mặc định", after a setting event).</summary>
        public void RefreshSettingRows()
        {
            var v = _root.Menu;
            if (_audio != null)
            {
                SetSlider(v.Master, v.MasterValue, _audio.GetVolume(AudioBus.Master));
                SetSlider(v.Sfx, v.SfxValue, _audio.GetVolume(AudioBus.Sfx));
                SetSlider(v.Music, v.MusicValue, _audio.GetVolume(AudioBus.Music));
                v.MusicEnabled.SetIsOnWithoutNotify(_audio.MusicEnabled);
                RefreshMusicLabel();
            }
            if (_settings != null)
            {
                v.Sensitivity.SetValueWithoutNotify(Mathf.Round(_settings.MouseSensitivity * 10f));
                v.SensitivityValue.text = FormatSensitivity(_settings.MouseSensitivity);
                v.ScreenShake.SetIsOnWithoutNotify(_settings.ScreenShakeEnabled);
            }
        }

        public static string Percent(float volume01) => Mathf.RoundToInt(AudioVolume.Clamp01(volume01) * 100f) + "%";

        /// <summary>"1,5" with the Vietnamese decimal comma.</summary>
        public static string FormatSensitivity(float value)
        {
            var tenths = Mathf.RoundToInt(value * 10f);
            return tenths / 10 + "," + tenths % 10;
        }

        public void Dispose()
        {
            _menu.OpenChanged -= _onMenuOpen;
            _menu.ConfirmChanged -= _onConfirm;
            _menu.LeaveStateChanged -= _onLeaveState;
            if (_settings != null) _settings.Changed -= _onSettings;
            if (_flow != null) _flow.PhaseChanged -= _onPhase;
            UnbindPhases();
            if (_root == null) return;
            var v = _root.Menu;
            v.Master.onValueChanged.RemoveListener(_onMaster);
            v.Sfx.onValueChanged.RemoveListener(_onSfx);
            v.Music.onValueChanged.RemoveListener(_onMusic);
            v.Sensitivity.onValueChanged.RemoveListener(_onSensitivity);
            v.MusicEnabled.onValueChanged.RemoveListener(_onMusicEnabled);
            v.ScreenShake.onValueChanged.RemoveListener(_onShake);
            v.Resume.onClick.RemoveListener(_onResume);
            v.Defaults.onClick.RemoveListener(_onDefaults);
            v.Leave.onClick.RemoveListener(_onLeave);
            _root.Confirm.Confirm.onClick.RemoveListener(_onConfirmLeave);
            _root.Confirm.Stay.onClick.RemoveListener(_onStay);
        }

        private void OnMenuOpen(bool open)
        {
            if (open) RefreshSettingRows();
            _root.Menu.Leave.gameObject.SetActive(!_menu.FromMainMenu); // UI-02 settings: no leave row (T-8)
            _menuFade.Set(open);
            if (_root.Canvas != null) _root.Canvas.enabled = open || (_flow != null ? _flow.Phase : MatchOverlayPhase.Playing) != MatchOverlayPhase.Inactive;
        }

        private void OnPhase(MatchOverlayPhase phase)
        {
            if (_root.Canvas != null) _root.Canvas.enabled = phase != MatchOverlayPhase.Inactive || _menu.IsOpen;
            ShowPhase(phase);
        }

        private void SetVolume(AudioBus bus, float value)
        {
            if (_audio == null) return;
            _audio.SetVolume(bus, value);
            var v = _root.Menu;
            var label = bus == AudioBus.Master ? v.MasterValue : bus == AudioBus.Sfx ? v.SfxValue : v.MusicValue;
            if (label != null) label.text = Percent(_audio.GetVolume(bus));
        }

        private void SetMusicEnabled(bool on)
        {
            if (_audio == null) return;
            _audio.MusicEnabled = on;
            RefreshMusicLabel();
        }

        private void ResetDefaults()
        {
            _settings?.ResetDefaults();
            RefreshSettingRows();
        }

        private void RefreshMusicLabel()
        {
            var label = _root.Menu.MusicOffLabel;
            if (label == null) return;
            label.text = MusicOffText;
            label.gameObject.SetActive(_audio != null && !_audio.MusicEnabled);
        }

        private void RefreshLeaveState()
        {
            var v = _root.Menu;
            var locked = _menu.IsLeaveLocked;
            v.Leave.interactable = !locked;
            v.Resume.interactable = !locked;
            v.Defaults.interactable = !locked;
            _root.Confirm.Confirm.interactable = !locked;
            if (v.RetryNotice != null)
            {
                v.RetryNotice.text = RetryText;
                v.RetryNotice.gameObject.SetActive(_menu.ShowRetryNotice);
            }
            RefreshPhaseLeaveState(locked);
        }

        private static void SetSlider(UnityEngine.UI.Slider slider, TMPro.TMP_Text label, float value)
        {
            slider.SetValueWithoutNotify(value);
            if (label != null) label.text = Percent(value);
        }
    }
}
