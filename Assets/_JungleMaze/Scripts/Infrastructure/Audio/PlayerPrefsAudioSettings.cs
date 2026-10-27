using JungleMaze.App.Audio;
using JungleMaze.Domain.Audio;
using UnityEngine;

namespace JungleMaze.Infrastructure.Audio
{
    /// <summary>
    /// Keeps the user's volumes and the music switch in PlayerPrefs (Q9). UI-03 only calls <see cref="IAudioService"/>.
    /// Writes only stage values; the disk write (PlayerPrefs.Save) happens in <see cref="Flush"/>, which the playback
    /// service calls after a short debounce and the AudioManager on application pause/quit.
    /// </summary>
    public sealed class PlayerPrefsAudioSettings : IAudioSettingsStore
    {
        public const string MusicEnabledKey = "jm.audio.musicEnabled";
        private static readonly string[] VolumeKeys = { "jm.audio.master", "jm.audio.sfx", "jm.audio.music", "jm.audio.alert" };

        public float Load(AudioBus bus, float fallback) =>
            (int)bus < VolumeKeys.Length ? PlayerPrefs.GetFloat(VolumeKeys[(int)bus], fallback) : fallback;

        public bool HasPendingWrites { get; private set; }

        public void Save(AudioBus bus, float volume01)
        {
            if ((int)bus >= VolumeKeys.Length) return;
            PlayerPrefs.SetFloat(VolumeKeys[(int)bus], volume01);
            HasPendingWrites = true;
        }

        public bool LoadMusicEnabled(bool fallback) => PlayerPrefs.GetInt(MusicEnabledKey, fallback ? 1 : 0) != 0;

        public void SaveMusicEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(MusicEnabledKey, enabled ? 1 : 0);
            HasPendingWrites = true;
        }

        public void Flush()
        {
            if (!HasPendingWrites) return;
            HasPendingWrites = false;
            PlayerPrefs.Save();
        }
    }
}
