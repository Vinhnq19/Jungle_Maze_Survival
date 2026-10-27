using System;
using JungleMaze.App.Hud.State;
using JungleMaze.Core.Logging;
using JungleMaze.Core.Ticking;
using JungleMaze.Domain.Audio;

namespace JungleMaze.App.Audio
{
    /// <summary>
    /// Default <see cref="IAudioService"/>: every playback decision in plain C#. Order of checks for one request:
    /// rule exists → cooldown per (id, source) → max instances → distance from the Hero → voice by priority.
    /// The Unity side only executes through <see cref="IAudioOutput"/>. Time comes from <see cref="Tick"/>,
    /// so tests drive it with a simulated clock. No allocation per request.
    /// </summary>
    public sealed class SfxPlaybackService : IAudioService, ITickable
    {
        private const float DefaultVolume = AudioVolume.DefaultVolume;
        /// <summary>Settings are written to disk this long after the last change (slider drags write once).</summary>
        public const float SaveDebounceSeconds = 0.5f;

        private readonly SfxRuleTable _rules;
        private readonly IAudioOutput _output;
        private readonly IHudState _listener;
        private readonly IAudioSettingsStore _settings;
        private readonly MusicPlan _music;
        private readonly ILog _log;
        private readonly SfxGate _gate = new SfxGate();
        private readonly VoiceAllocator _voices;
        private readonly ClipPicker _picker;
        private readonly SfxScheduler _scheduler = new SfxScheduler();
        private readonly float[] _volumes = new float[4];
        private readonly bool[] _warned = new bool[SfxIds.TableSize];
        private readonly int[] _droppedNoVoice = new int[4];
        private readonly Action<AudioCue> _playRepeat;
        private bool _musicEnabled;
        private float _now;
        private float _saveAt = float.PositiveInfinity;
        private float _appliedDuckDb;

        public SfxPlaybackService(SfxRuleTable rules, IAudioOutput output, IHudState listener,
            IAudioSettingsStore settings = null, MusicPlan music = null, ILog log = null, int seed = 12345)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _listener = listener;
            _settings = settings;
            _music = music;
            _log = log ?? NullLog.Instance;
            _voices = new VoiceAllocator(output.VoiceCount);
            _picker = new ClipPicker(seed);
            _playRepeat = cue => Start(cue, true);

            for (var bus = 0; bus < _volumes.Length; bus++)
            {
                _volumes[bus] = AudioVolume.Clamp01(settings?.Load((AudioBus)bus, DefaultVolume) ?? DefaultVolume);
                _output.SetBusDecibels((AudioBus)bus, AudioVolume.ToDecibels(_volumes[bus]));
            }
            _musicEnabled = settings?.LoadMusicEnabled(false) ?? false;
            _music?.SetEnabled(_musicEnabled);
            _output.SetMusicDuckDecibels(0f);
        }

        /// <summary>Simulated clock in seconds since construction (advanced by <see cref="Tick"/>).</summary>
        public float Now => _now;
        public int MaxVoices => _voices.VoiceCount;

        public int ActiveVoices
        {
            get { RefreshFinished(); return _voices.ActiveCount; }
        }

        /// <summary>True while an Alert-bus sound plays or still has repeats pending (drives music ducking).</summary>
        public bool AlertActive => _voices.AnyActiveOn(AudioBus.Alert) || _scheduler.AnyPendingOn(AudioBus.Alert);

        public int CountActive(SfxId id) { RefreshFinished(); return _voices.CountActive(id); }

        /// <summary>Requests of <paramref name="priority"/> dropped because no voice could be freed (diagnostics, AC-A26).</summary>
        public int DroppedNoVoice(SfxPriority priority) => _droppedNoVoice[(int)priority];

        public bool MusicEnabled
        {
            get => _musicEnabled;
            set
            {
                if (_musicEnabled == value) return;
                _musicEnabled = value;
                _music?.SetEnabled(value);
                _settings?.SaveMusicEnabled(value);
                ScheduleSave();
            }
        }

        public void PlaySfx(SfxId id) => Start(AudioCue.Global(id), false);
        public void PlaySfx(SfxId id, float worldX, float worldY, int sourceKey = 0) => Start(AudioCue.At(id, worldX, worldY, sourceKey), false);
        public void Play(in AudioCue cue) => Start(cue, false);

        public void SetVolume(AudioBus bus, float volume01)
        {
            var index = (int)bus;
            if (index < 0 || index >= _volumes.Length) return;
            var v = AudioVolume.Clamp01(volume01);
            _volumes[index] = v;
            _output.SetBusDecibels(bus, AudioVolume.ToDecibels(v));
            _settings?.Save(bus, v);
            ScheduleSave();
        }

        /// <summary>Writes staged settings now (application pause/quit). Does nothing when nothing changed.</summary>
        public void FlushSettings()
        {
            if (float.IsPositiveInfinity(_saveAt)) return;
            _saveAt = float.PositiveInfinity;
            _settings?.Flush();
        }

        private void ScheduleSave()
        {
            if (_settings != null) _saveAt = _now + SaveDebounceSeconds;
        }

        public float GetVolume(AudioBus bus)
        {
            var index = (int)bus;
            return index >= 0 && index < _volumes.Length ? _volumes[index] : 0f;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime > 0f) _now += deltaTime;
            RefreshFinished();
            _scheduler.Update(_now, _playRepeat);
            if (_now >= _saveAt) FlushSettings();
            if (_music == null) return;
            _music.SetAlertActive(AlertActive);
            _music.Tick(deltaTime);
            if (_music.DuckDb == _appliedDuckDb) return;
            _appliedDuckDb = _music.DuckDb;
            _output.SetMusicDuckDecibels(_appliedDuckDb);
        }

        public void Stop(SfxId id, int sourceKey)
        {
            for (var i = 0; i < _voices.VoiceCount; i++)
            {
                if (!_voices.IsActive(i) || _voices.IdAt(i) != id || _voices.SourceAt(i) != sourceKey) continue;
                _output.StopVoice(i);
                _voices.Release(i);
            }
        }

        /// <summary>Stops every SFX voice and pending repeat (leaving a match, AC-A27).</summary>
        public void StopAll()
        {
            _scheduler.Clear();
            for (var i = 0; i < _voices.VoiceCount; i++)
            {
                if (!_voices.IsActive(i)) continue;
                _output.StopVoice(i);
                _voices.Release(i);
            }
        }

        private bool Start(in AudioCue cue, bool isRepeat)
        {
            var id = cue.Id;
            if (!_rules.TryGet(id, out var rule))
            {
                WarnOnce(id);
                return false;
            }
            if (!isRepeat)
            {
                if (_gate.IsCoolingDown(id, cue.SourceKey, _now, rule.CooldownSeconds)) return false;
                RefreshFinished();
                if (_voices.CountActive(id) >= rule.MaxInstances) return false;
            }

            var gain = 1f;
            if (!rule.Is2D && cue.HasPosition)
            {
                if (_listener == null || !_listener.HasPose) return false;
                var pose = _listener.Pose;
                gain = SfxDistance.Gain(cue.X, cue.Y, pose.X, pose.Y, rule.RangeCells);
                if (gain <= 0f) return false;
            }

            var voice = _voices.Acquire(rule.Priority, out var evicted);
            if (voice < 0)
            {
                RefreshFinished();
                voice = _voices.Acquire(rule.Priority, out evicted);
                if (voice < 0) { _droppedNoVoice[(int)rule.Priority]++; return false; }
            }
            if (evicted) _output.StopVoice(voice);

            var clip = _picker.PickClip(id, rule.ClipCount);
            _output.PlayVoice(voice, id, clip, rule.Bus, rule.Volume * gain, _picker.PickPitch(rule.PitchJitter));
            _voices.Assign(voice, id, rule.Priority, rule.Bus, cue.SourceKey);

            if (!isRepeat)
            {
                _gate.Mark(id, cue.SourceKey, _now);
                if (rule.RepeatCount > 1) _scheduler.Schedule(cue, rule.Bus, rule.RepeatCount - 1, rule.RepeatInterval, _now);
            }
            return true;
        }

        private void RefreshFinished()
        {
            for (var i = 0; i < _voices.VoiceCount; i++)
                if (_voices.IsActive(i) && !_output.IsVoicePlaying(i)) _voices.Release(i);
        }

        private void WarnOnce(SfxId id)
        {
            var slot = SfxIds.IsValid(id) ? (int)id : 0;
            if (_warned[slot]) return;
            _warned[slot] = true;
            _log.Warn($"[Audio] No clip for SFX '{id}'; it stays silent until the catalog has one.");
        }
    }
}
