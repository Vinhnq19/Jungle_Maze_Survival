using JungleMaze.App.Audio;
using UnityEngine;
using UnityEngine.Audio;

namespace JungleMaze.Presentation.Audio
{
    /// <summary>
    /// The one audio entry point Bootstrap wires: mixer and groups, SFX catalog, music clips and pool size.
    /// Lives at <c>Art/Audio/Resources/AudioRuntimeSet.asset</c> so a Bootstrap auto-created in Game/HudPreview
    /// loads it once at composition.
    /// </summary>
    [CreateAssetMenu(menuName = "Jungle Maze/Audio/Audio Runtime Set")]
    public sealed class AudioRuntimeSet : ScriptableObject
    {
        /// <summary>Resources path (no extension) used by Bootstrap.</summary>
        public const string ResourcePath = "AudioRuntimeSet";

        public const string MasterVolumeParam = "MasterVolume";
        public const string SfxVolumeParam = "SfxVolume";
        public const string AlertVolumeParam = "AlertVolume";
        public const string MusicVolumeParam = "MusicVolume";
        public const string MusicDuckParam = "MusicDuck";

        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioMixerGroup alertGroup;
        [Tooltip("Music plays into this child of Music so ducking and the user's music volume stack.")]
        [SerializeField] private AudioMixerGroup musicTrackGroup;
        [SerializeField] private SfxCatalog catalog;
        [Tooltip("Until ART-05 compresses Waiting Room Music.wav, this uses the Loading clip (T-5).")]
        [SerializeField] private AudioClip waitingRoomMusic;
        [SerializeField] private AudioClip loadingMusic;
        [SerializeField] private AudioClip matchMusic;
        [SerializeField, Min(1)] private int poolSize = 16;
        [Tooltip("After entering a match or a HUD reset, gear and pickup changes are silent this long (T-3).")]
        [SerializeField, Min(0f)] private float settleSeconds = SfxRouter.DefaultSettleSeconds;

        public AudioMixer Mixer => mixer;
        public AudioMixerGroup SfxGroup => sfxGroup;
        public AudioMixerGroup AlertGroup => alertGroup;
        public AudioMixerGroup MusicTrackGroup => musicTrackGroup;
        public SfxCatalog Catalog => catalog;
        public int PoolSize => poolSize;
        public float SettleSeconds => settleSeconds;

        public AudioClip MusicFor(MusicTrack track)
        {
            switch (track)
            {
                case MusicTrack.WaitingRoom: return waitingRoomMusic;
                case MusicTrack.Loading: return loadingMusic;
                case MusicTrack.Match: return matchMusic;
                default: return null;
            }
        }
    }
}
