using System;
using JungleMaze.App.Audio;
using JungleMaze.Domain.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JungleMaze.Presentation.Audio
{
    /// <summary>
    /// Owns the runtime AudioSources: a fixed pool of SFX voices plus two music sources, all created once in
    /// <see cref="Configure"/>. Implements <see cref="IAudioOutput"/> and only executes what Application decides.
    /// Sources are 2D (spatialBlend 0): distance falloff is computed in cells by the playback service.
    /// The project scenes have no AudioListener, so this keeps a fallback one active while no other listener is
    /// enabled, re-checked on scene load and every <see cref="ListenerCheckSeconds"/> (a camera listener may appear late).
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
        public const float ListenerCheckSeconds = 2f;

        private GameObject _listener;
        private AudioListener _sceneListener;
        private Camera[] _cameras = new Camera[8];
        private float _nextListenerCheck;
        private Action _flushSettings;

        public bool IsConfigured => _set != null;
        public AudioRuntimeSet RuntimeSet => _set;
        public int VoiceCount => _voices.Length;
        public MusicDirector Music => _music;
        public bool OwnsListener => _listener != null && _listener.activeSelf;

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
            EnsureListener();
            return this;
        }

        /// <summary>Called on application pause/quit so staged volume settings reach the disk.</summary>
        public void BindSettingsFlush(Action flush) => _flushSettings = flush;

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

        private void Update()
        {
            _music?.Apply();
            if (Time.unscaledTime < _nextListenerCheck) return;
            CheckListener();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) _flushSettings?.Invoke();
        }

        private void OnApplicationQuit() => _flushSettings?.Invoke();

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureListener();

        /// <summary>
        /// Full check (configure, scene load): uses a scene's own enabled listener when there is one, otherwise
        /// activates the fallback child. Ours is switched off for the lookup so it never finds itself.
        /// </summary>
        public void EnsureListener()
        {
            _nextListenerCheck = Time.unscaledTime + ListenerCheckSeconds;
            if (_root == null) return;
            if (_listener == null)
            {
                _listener = new GameObject("[AudioListener]");
                _listener.transform.SetParent(_root, false);
                _listener.SetActive(false);
                _listener.AddComponent<AudioListener>();
            }
            else if (_listener.activeSelf) _listener.SetActive(false);
            var other = FindAnyObjectByType<AudioListener>();
            _sceneListener = other != null && other.enabled ? other : null;
            _listener.SetActive(_sceneListener == null);
        }

        /// <summary>
        /// Periodic check without allocation: the remembered scene listener, then the listeners on enabled cameras
        /// (a pre-sized array, TryGetComponent). The allocating full lookup runs only when the fallback may need to
        /// turn back on, so a late camera listener is picked up within <see cref="ListenerCheckSeconds"/>.
        /// </summary>
        public void CheckListener()
        {
            _nextListenerCheck = Time.unscaledTime + ListenerCheckSeconds;
            if (_listener == null) return;
            if (_sceneListener != null && _sceneListener.isActiveAndEnabled) return;
            var camera = CameraListener();
            if (camera != null)
            {
                _sceneListener = camera;
                if (_listener.activeSelf) _listener.SetActive(false);
                return;
            }
            _sceneListener = null;
            if (!_listener.activeSelf) EnsureListener();
        }

        private AudioListener CameraListener()
        {
            var count = Camera.allCamerasCount;
            if (count > _cameras.Length) _cameras = new Camera[count * 2];
            count = Camera.GetAllCameras(_cameras);
            for (var i = 0; i < count; i++)
                if (_cameras[i] != null && _cameras[i].TryGetComponent<AudioListener>(out var l) && l.isActiveAndEnabled) return l;
            return null;
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
