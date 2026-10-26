using JungleMaze.App.Audio;
using JungleMaze.Domain.Audio;
using UnityEngine;

namespace JungleMaze.Infrastructure.Audio
{
    /// <summary>Saves the user's volumes and the music switch in PlayerPrefs (Q9). UI-03 only calls <see cref="IAudioService"/>.</summary>
    public sealed class PlayerPrefsAudioSettings : IAudioSettingsStore
    {
        public const string MusicEnabledKey = "jm.audio.musicEnabled";
        private static readonly string[] VolumeKeys = { "jm.audio.master", "jm.audio.sfx", "jm.audio.music", "jm.audio.alert" };

        public float Load(AudioBus bus, float fallback) =>
            (int)bus < VolumeKeys.Length ? PlayerPrefs.GetFloat(VolumeKeys[(int)bus], fallback) : fallback;

        public void Save(AudioBus bus, float volume01)
        {
            if ((int)bus >= VolumeKeys.Length) return;
            PlayerPrefs.SetFloat(VolumeKeys[(int)bus], volume01);
            PlayerPrefs.Save();
        }

        public bool LoadMusicEnabled(bool fallback) => PlayerPrefs.GetInt(MusicEnabledKey, fallback ? 1 : 0) != 0;

        public void SaveMusicEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(MusicEnabledKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
