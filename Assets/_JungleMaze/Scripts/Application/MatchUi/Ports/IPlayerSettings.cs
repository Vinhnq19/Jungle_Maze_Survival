using System;

namespace JungleMaze.App.MatchUi
{
    /// <summary>Which UI-03 setting changed (N4).</summary>
    public enum PlayerSettingKind : byte
    {
        MouseSensitivity = 0,
        ScreenShake = 1
    }

    /// <summary>
    /// N4 on the EventBus: published in the same call that changed the value, with both current values so a reader
    /// never has to query back. Volumes are not here: they belong to <c>IAudioService</c> (N1).
    /// </summary>
    public readonly struct PlayerSettingsChanged
    {
        public readonly PlayerSettingKind Kind;
        public readonly float MouseSensitivity;
        public readonly bool ScreenShake;

        public PlayerSettingsChanged(PlayerSettingKind kind, float mouseSensitivity, bool screenShake)
        {
            Kind = kind;
            MouseSensitivity = mouseSensitivity;
            ScreenShake = screenShake;
        }
    }

    /// <summary>
    /// Read side of the in-match settings (N2, N3). MOV-02 reads <see cref="MouseSensitivity"/>, VFX-01 reads
    /// <see cref="ScreenShakeEnabled"/>; values are always in range (AC14).
    /// </summary>
    public interface IPlayerSettings
    {
        /// <summary>0.5–2.0 in 0.1 steps, default 1.0 (G-9).</summary>
        float MouseSensitivity { get; }

        /// <summary>Default true (Q5).</summary>
        bool ScreenShakeEnabled { get; }

        event Action<PlayerSettingsChanged> Changed;
    }

    /// <summary>Write side, used only by the UI-03 menu. Changes apply at once (Q8) and are saved after a debounce.</summary>
    public interface IPlayerSettingsWriter : IPlayerSettings
    {
        void SetMouseSensitivity(float value);
        void SetScreenShake(bool enabled);

        /// <summary>Sensitivity 1.0, shake on, AUD-01 default volumes, music off (AC13).</summary>
        void ResetDefaults();

        /// <summary>Writes a pending change now (menu closed, AC12).</summary>
        void Flush();
    }

    /// <summary>Where sensitivity and shake live between runs. Holds no volume by design (AC8).</summary>
    public interface IPlayerSettingsStore
    {
        /// <summary>False when nothing was saved; values may be out of range or not finite and are clamped by the caller.</summary>
        bool TryLoad(out float mouseSensitivity, out bool screenShake);

        void Save(float mouseSensitivity, bool screenShake);
    }
}
