using JungleMaze.Domain.Audio;

namespace JungleMaze.App.Audio
{
    /// <summary>
    /// The only way gameplay, HUD and network code make sound. Nobody else touches AudioSource.
    /// Positions are world cells; positional sounds fade with distance from the Hero and stay silent without a Hero.
    /// </summary>
    public interface IAudioService
    {
        /// <summary>2D sound of the local player (no distance falloff).</summary>
        void PlaySfx(SfxId id);

        /// <summary>Sound at a world position (cells). Cooldown is counted per <paramref name="sourceKey"/>.</summary>
        void PlaySfx(SfxId id, float worldX, float worldY, int sourceKey = 0);

        /// <summary>General entry used by the router; <see cref="AudioCue.DedupeKey"/> is ignored here.</summary>
        void Play(in AudioCue cue);

        /// <summary>Stops every voice of <paramref name="id"/> started by <paramref name="sourceKey"/> (a fuse when its bomb explodes).</summary>
        void Stop(SfxId id, int sourceKey);

        /// <summary>Stops every SFX voice and pending repeat (leaving a match, AC-A27).</summary>
        void StopAll();

        /// <summary>Clamped to [0, 1]; persisted after a short debounce, not on every call.</summary>
        void SetVolume(AudioBus bus, float volume01);

        float GetVolume(AudioBus bus);

        /// <summary>Background music on/off; off by default (G-4). Saved.</summary>
        bool MusicEnabled { get; set; }

        int ActiveVoices { get; }
        int MaxVoices { get; }
    }
}
