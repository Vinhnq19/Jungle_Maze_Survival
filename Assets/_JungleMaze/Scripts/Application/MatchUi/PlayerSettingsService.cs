using System;
using JungleMaze.App.Audio;
using JungleMaze.Core.Events;
using JungleMaze.Core.Ticking;
using JungleMaze.Domain.Audio;
using JungleMaze.Domain.MatchUi;

namespace JungleMaze.App.MatchUi
{
    /// <summary>
    /// Owns mouse sensitivity and screen shake (N2–N4). Every change is clamped (AC14), then raises
    /// <see cref="Changed"/> and publishes <see cref="PlayerSettingsChanged"/> in the same call (AC10, AC11).
    /// Saving waits <see cref="SaveDebounceSeconds"/> after the last change and <see cref="Flush"/> forces it, so a
    /// slider drag writes once, never per frame (AC12). Volumes go straight to <see cref="IAudioService"/> (AC8).
    /// </summary>
    public sealed class PlayerSettingsService : IPlayerSettingsWriter, ITickable
    {
        public const float SaveDebounceSeconds = 1f;

        private readonly IPlayerSettingsStore _store;
        private readonly IEventBus _bus;
        private readonly IAudioService _audio;
        private float _now;
        private float _saveAt = float.PositiveInfinity;
        private bool _dirty;

        public PlayerSettingsService(IPlayerSettingsStore store, IEventBus bus, IAudioService audio = null)
        {
            _store = store;
            _bus = bus;
            _audio = audio;
            MouseSensitivity = PlayerSettingsRules.DefaultSensitivity;
            ScreenShakeEnabled = PlayerSettingsRules.DefaultScreenShake;
            if (_store != null && _store.TryLoad(out var sensitivity, out var shake))
            {
                MouseSensitivity = PlayerSettingsRules.ClampSensitivity(sensitivity);
                ScreenShakeEnabled = shake;
            }
        }

        public float MouseSensitivity { get; private set; }
        public bool ScreenShakeEnabled { get; private set; }
        public bool HasPendingSave => _dirty;
        public event Action<PlayerSettingsChanged> Changed;

        public void SetMouseSensitivity(float value)
        {
            var clamped = PlayerSettingsRules.ClampSensitivity(value);
            if (PlayerSettingsRules.SameStep(clamped, MouseSensitivity)) return;
            MouseSensitivity = clamped;
            Raise(PlayerSettingKind.MouseSensitivity);
        }

        public void SetScreenShake(bool enabled)
        {
            if (enabled == ScreenShakeEnabled) return;
            ScreenShakeEnabled = enabled;
            Raise(PlayerSettingKind.ScreenShake);
        }

        public void ResetDefaults()
        {
            SetMouseSensitivity(PlayerSettingsRules.DefaultSensitivity);
            SetScreenShake(PlayerSettingsRules.DefaultScreenShake);
            if (_audio == null) return;
            _audio.SetVolume(AudioBus.Master, AudioVolume.DefaultVolume);
            _audio.SetVolume(AudioBus.Sfx, AudioVolume.DefaultVolume);
            _audio.SetVolume(AudioBus.Music, AudioVolume.DefaultVolume);
            _audio.MusicEnabled = false;
        }

        public void Flush()
        {
            if (!_dirty) return;
            _dirty = false;
            _saveAt = float.PositiveInfinity;
            _store?.Save(MouseSensitivity, ScreenShakeEnabled);
        }

        public void Tick(float deltaTime)
        {
            _now += deltaTime;
            if (_dirty && _now >= _saveAt) Flush();
        }

        private void Raise(PlayerSettingKind kind)
        {
            _dirty = true;
            _saveAt = _now + SaveDebounceSeconds;
            var evt = new PlayerSettingsChanged(kind, MouseSensitivity, ScreenShakeEnabled);
            Changed?.Invoke(evt);
            _bus?.Publish(evt);
        }
    }
}
