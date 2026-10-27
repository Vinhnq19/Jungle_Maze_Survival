using JungleMaze.Domain.Audio;

namespace JungleMaze.App.Audio
{
    /// <summary>Where the user's volume choices live between runs (Q9: AUD-01 owns saving, UI-03 only calls the API).</summary>
    public interface IAudioSettingsStore
    {
        float Load(AudioBus bus, float fallback);
        void Save(AudioBus bus, float volume01);
        bool LoadMusicEnabled(bool fallback);
        void SaveMusicEnabled(bool enabled);
        /// <summary>Writes pending values to disk. <see cref="Save"/>/<see cref="SaveMusicEnabled"/> only stage them.</summary>
        void Flush();
    }
}
