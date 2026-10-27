#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using JungleMaze.App.Audio;
using JungleMaze.App.MatchUi;
using JungleMaze.Domain.Audio;

namespace JungleMaze.Tests.PlayMode.MatchUi
{
    /// <summary>Fakes for the UI-03 PlayMode tests (the EditMode ones live in another assembly).</summary>
    internal static class MatchUiPlayFakes
    {
        internal sealed class Store : IPlayerSettingsStore
        {
            public int Saves;
            public bool TryLoad(out float mouseSensitivity, out bool screenShake)
            {
                mouseSensitivity = float.NaN;
                screenShake = true;
                return false;
            }
            public void Save(float mouseSensitivity, bool screenShake) => Saves++;
        }

        /// <summary>Remembers volumes like AUD-01 (same-frame read back).</summary>
        internal sealed class VolumeAudio : IAudioService
        {
            public readonly float[] Volumes = { 0.5f, 0.5f, 0.5f, 0.5f };
            public void PlaySfx(SfxId id) { }
            public void PlaySfx(SfxId id, float worldX, float worldY, int sourceKey = 0) { }
            public void Play(in AudioCue cue) { }
            public void Stop(SfxId id, int sourceKey) { }
            public void StopAll() { }
            public void SetVolume(AudioBus bus, float volume01) => Volumes[(int)bus] = AudioVolume.Clamp01(volume01);
            public float GetVolume(AudioBus bus) => Volumes[(int)bus];
            public bool MusicEnabled { get; set; } = true;
            public int ActiveVoices => 0;
            public int MaxVoices => 16;
        }

        internal sealed class Recorder<T> where T : struct
        {
            public readonly List<T> Items = new List<T>();
            public readonly Action<T> Handler;
            public Recorder() => Handler = Items.Add;
            public int Count => Items.Count;
        }

        internal sealed class Clock
        {
            public double Now;
            public Func<double> Read => () => Now;
        }
    }
}
#endif
