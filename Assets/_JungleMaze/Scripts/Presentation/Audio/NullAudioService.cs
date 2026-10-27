using JungleMaze.App.Audio;
using JungleMaze.Domain.Audio;

namespace JungleMaze.Presentation.Audio
{
    /// <summary>Silent <see cref="IAudioService"/> registered when the audio runtime set is missing, so callers never null-check.</summary>
    public sealed class NullAudioService : IAudioService
    {
        private readonly float[] _volumes = { 1f, 1f, 1f, 1f };

        public void PlaySfx(SfxId id) { }
        public void PlaySfx(SfxId id, float worldX, float worldY, int sourceKey = 0) { }
        public void Play(in AudioCue cue) { }
        public void Stop(SfxId id, int sourceKey) { }
        public void StopAll() { }

        public void SetVolume(AudioBus bus, float volume01)
        {
            if ((int)bus < _volumes.Length) _volumes[(int)bus] = AudioVolume.Clamp01(volume01);
        }

        public float GetVolume(AudioBus bus) => (int)bus < _volumes.Length ? _volumes[(int)bus] : 0f;
        public bool MusicEnabled { get; set; }
        public int ActiveVoices => 0;
        public int MaxVoices => 0;
    }
}
