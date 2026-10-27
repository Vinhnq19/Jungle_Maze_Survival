using System;
using System.Collections.Generic;
using JungleMaze.App.Audio;
using JungleMaze.App.MatchUi;
using JungleMaze.Domain.Audio;

namespace JungleMaze.Tests.MatchUi
{
    /// <summary>Settings store that counts writes; it can only hold sensitivity and shake (AC8).</summary>
    internal sealed class FakeSettingsStore : IPlayerSettingsStore
    {
        public bool Has;
        public float Sensitivity = float.NaN;
        public bool Shake = true;
        public int Saves;

        public bool TryLoad(out float mouseSensitivity, out bool screenShake)
        {
            mouseSensitivity = Sensitivity;
            screenShake = Shake;
            return Has;
        }

        public void Save(float mouseSensitivity, bool screenShake)
        {
            Saves++;
            Has = true;
            Sensitivity = mouseSensitivity;
            Shake = screenShake;
        }
    }

    /// <summary>Volume-only audio service: remembers SetVolume/MusicEnabled like AUD-01 does.</summary>
    internal sealed class FakeVolumeAudio : IAudioService
    {
        public readonly float[] Volumes = { 0.5f, 0.5f, 0.5f, 0.5f };
        public int SetVolumeCalls;
        public void PlaySfx(SfxId id) { }
        public void PlaySfx(SfxId id, float worldX, float worldY, int sourceKey = 0) { }
        public void Play(in AudioCue cue) { }
        public void Stop(SfxId id, int sourceKey) { }
        public void StopAll() { }
        public void SetVolume(AudioBus bus, float volume01) { SetVolumeCalls++; Volumes[(int)bus] = AudioVolume.Clamp01(volume01); }
        public float GetVolume(AudioBus bus) => Volumes[(int)bus];
        public bool MusicEnabled { get; set; } = true;
        public int ActiveVoices => 0;
        public int MaxVoices => 16;
    }

    /// <summary>Stand-in for MOV-02: turns a mouse delta into an aim delta using the published sensitivity (AC10).</summary>
    internal sealed class FakeSensitivityReader
    {
        private readonly IPlayerSettings _settings;
        public FakeSensitivityReader(IPlayerSettings settings) => _settings = settings;
        public float Apply(float mouseDelta) => mouseDelta * _settings.MouseSensitivity;
    }

    /// <summary>Stand-in for VFX-01: performs a shake only while the setting allows it (AC11).</summary>
    internal sealed class FakeShaker
    {
        private readonly IPlayerSettings _settings;
        public int Performed;
        public FakeShaker(IPlayerSettings settings) => _settings = settings;
        public void Request() { if (_settings.ScreenShakeEnabled) Performed++; }
    }

    /// <summary>Manually advanced clock for the UI-03 controllers (they read time through Func&lt;double&gt;).</summary>
    internal sealed class FakeClock
    {
        public double Now;
        public Func<double> Read => () => Now;
        public void Advance(double seconds) => Now += seconds;
    }

    internal sealed class Recorder<T> where T : struct
    {
        public readonly List<T> Items = new List<T>();
        public readonly Action<T> Handler;
        public Recorder() => Handler = Items.Add;
        public int Count => Items.Count;
    }
}
