using JungleMaze.App.Audio;
using JungleMaze.Domain.Audio;
using UnityEngine;

namespace JungleMaze.Presentation.Audio
{
    /// <summary>
    /// Owns the runtime AudioSources: a fixed pool of SFX voices plus two music sources, all created once in
    /// <see cref="Configure"/>. Implements <see cref="IAudioOutput"/> and only executes what Application decides.
    /// Sources are 2D (spatialBlend 0): distance falloff is computed in cells by the playback service.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour, IAudioOutput
    {
        private static readonly string[] BusParams =
        {
            AudioRuntimeSet.MasterVolumeParam, AudioRuntimeSet.SfxVolumeParam,
            AudioRuntimeSet.MusicVolumeParam, AudioRuntimeSet.AlertVolumeParam
        };

        private AudioRuntimeSet _set;
        private AudioSource[] _voices = new AudioSource[0];
        private float[] _endTimes = new float[0];
        private readonly float[] _busDb = new float[4];
        private float _duckDb;
        private MusicDirector _music;
        private Transform _root;

        public bool IsConfigured => _set != null;
        public AudioRuntimeSet RuntimeSet => _set;
        public int VoiceCount => _voices.Length;
        public MusicDirector Music => _music;

        /// <summary>Creates the voice pool once. Calling again with the same set does nothing.</summary>
        public AudioManager Configure(AudioRuntimeSet set)
        {
            if (set == null || _set == set) return this;
            _set = set;
            if (_root != null) Destroy(_root.gameObject);
            _root = new GameObject("[Audio]").transform;
            _root.SetParent(transform, false);

            _voices = new AudioSource[Mathf.Max(1, set.PoolSize)];
            _endTimes = new float[_voices.Length];
            for (var i = 0; i < _voices.Length; i++) _voices[i] = CreateSource($"SFX Voice {i:00}", set.SfxGroup, false);
            var a = CreateSource("Music A", set.MusicTrackGroup, true);
            var b = CreateSource("Music B", set.MusicTrackGroup, true);
            _music = new MusicDirector(a, b, set.MusicFor);
            ApplyMixer();
            return this;
        }

        /// <summary>Binds the music plan whose slots and weights are applied every frame.</summary>
        public void BindMusic(MusicPlan plan) => _music?.Bind(plan);

        public void PlayVoice(int voice, SfxId id, int clipIndex, AudioBus bus, float gain, float pitch)
        {
            if (voice < 0 || voice >= _voices.Length || _set == null) return;
            var clip = _set.Catalog != null ? _set.Catalog.GetClip(id, clipIndex) : null;
            var source = _voices[voice];
            if (clip == null) { source.Stop(); _endTimes[voice] = 0f; return; }
            source.Stop();
            source.clip = clip;
            source.outputAudioMixerGroup = bus == AudioBus.Alert ? _set.AlertGroup : _set.SfxGroup;
            source.volume = Mathf.Clamp01(gain);
            source.pitch = pitch;
            source.Play();
            // Kept as a fallback for "still playing" when the audio device is off (batch mode, muted editor).
            _endTimes[voice] = Time.unscaledTime + clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
        }

        public void StopVoice(int voice)
        {
            if (voice < 0 || voice >= _voices.Length) return;
            _voices[voice].Stop();
            _endTimes[voice] = 0f;
        }

        public bool IsVoicePlaying(int voice) =>
            voice >= 0 && voice < _voices.Length && _voices[voice].clip != null
            && (_voices[voice].isPlaying || Time.unscaledTime < _endTimes[voice]);

        /// <summary>The AudioSource behind a voice (tests and the F6 panel only).</summary>
        public AudioSource VoiceSource(int voice) => _voices[voice];

        public void SetBusDecibels(AudioBus bus, float decibels)
        {
            var index = (int)bus;
            if (index < 0 || index >= _busDb.Length) return;
            _busDb[index] = decibels;
            if (_set != null && _set.Mixer != null) _set.Mixer.SetFloat(BusParams[index], decibels);
        }

        public void SetMusicDuckDecibels(float decibels)
        {
            _duckDb = decibels;
            if (_set != null && _set.Mixer != null) _set.Mixer.SetFloat(AudioRuntimeSet.MusicDuckParam, decibels);
        }

        // Mixer parameters set before the mixer is live are sometimes lost; apply again once running.
        private void Start() => ApplyMixer();

        private void Update() => _music?.Apply();

        private void OnDestroy()
        {
            for (var i = 0; i < _voices.Length; i++) if (_voices[i] != null) _voices[i].Stop();
            _music?.StopAll();
        }

        private void ApplyMixer()
        {
            if (_set == null || _set.Mixer == null) return;
            for (var i = 0; i < _busDb.Length; i++) _set.Mixer.SetFloat(BusParams[i], _busDb[i]);
            _set.Mixer.SetFloat(AudioRuntimeSet.MusicDuckParam, _duckDb);
        }

        private AudioSource CreateSource(string sourceName, UnityEngine.Audio.AudioMixerGroup group, bool loop)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(_root, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            // AddComponent already ran Awake with playOnAwake on: without Stop, isPlaying reports true with no clip.
            source.Stop();
            source.loop = loop;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = group;
            return source;
        }
    }
}
