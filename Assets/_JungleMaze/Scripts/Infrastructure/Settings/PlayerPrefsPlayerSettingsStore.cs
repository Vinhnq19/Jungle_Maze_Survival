using JungleMaze.App.MatchUi;
using UnityEngine;

namespace JungleMaze.Infrastructure.Settings
{
    /// <summary>
    /// UI-03 settings in PlayerPrefs under <c>jm.settings.*</c> (G-10): sensitivity and screen shake only. Volumes stay
    /// in AUD-01's <c>jm.audio.*</c> keys (AC8). A key of the wrong type reads as missing, and the service clamps
    /// whatever comes back (AC14). <see cref="Save"/> writes to disk; the service already debounces it.
    /// </summary>
    public sealed class PlayerPrefsPlayerSettingsStore : IPlayerSettingsStore
    {
        public const string DefaultPrefix = "jm.settings.";
        public const string SensitivityName = "mouseSensitivity";
        public const string ScreenShakeName = "screenShake";

        private readonly string _sensitivityKey;
        private readonly string _shakeKey;

        /// <param name="prefix">Tests pass their own prefix so they never touch the player's real keys.</param>
        public PlayerPrefsPlayerSettingsStore(string prefix = DefaultPrefix)
        {
            _sensitivityKey = prefix + SensitivityName;
            _shakeKey = prefix + ScreenShakeName;
        }

        /// <summary>Every key this store writes (AC8 checks none of them is a volume).</summary>
        public string[] Keys => new[] { _sensitivityKey, _shakeKey };

        public bool TryLoad(out float mouseSensitivity, out bool screenShake)
        {
            // GetFloat/GetInt return the fallback when the stored value has another type (a string, say).
            mouseSensitivity = PlayerPrefs.GetFloat(_sensitivityKey, float.NaN);
            // Only an explicit 0 turns shake off; missing or unreadable keeps the default (on).
            screenShake = PlayerPrefs.GetInt(_shakeKey, 1) != 0;
            return PlayerPrefs.HasKey(_sensitivityKey) || PlayerPrefs.HasKey(_shakeKey);
        }

        public void Save(float mouseSensitivity, bool screenShake)
        {
            PlayerPrefs.SetFloat(_sensitivityKey, mouseSensitivity);
            PlayerPrefs.SetInt(_shakeKey, screenShake ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
