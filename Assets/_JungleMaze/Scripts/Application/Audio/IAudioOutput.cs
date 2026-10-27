using JungleMaze.Domain.Audio;

namespace JungleMaze.App.Audio
{
    /// <summary>
    /// Narrow port to the Unity audio objects (implemented by the AudioManager MonoBehaviour).
    /// It only executes; every decision (which voice, gain, pitch, cooldown) is made in Application.
    /// </summary>
    public interface IAudioOutput
    {
        /// <summary>Size of the fixed SFX voice pool.</summary>
        int VoiceCount { get; }

        void PlayVoice(int voice, SfxId id, int clipIndex, AudioBus bus, float gain, float pitch);
        void StopVoice(int voice);
        bool IsVoicePlaying(int voice);
        void SetBusDecibels(AudioBus bus, float decibels);
        void SetMusicDuckDecibels(float decibels);
    }
}
