using System;
using System.Collections.Generic;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Audio;
using JungleMaze.App.Ports;
using JungleMaze.Domain.Audio;
using JungleMaze.Domain.Player;
using LiteNetLib.Utils;

namespace JungleMaze.Tests.Audio
{
    /// <summary>Spec table 2.1 of AUD-01 as test data: the catalog asset and the rule table must match it.</summary>
    internal static class AudioSpec
    {
        public readonly struct Row
        {
            public readonly SfxId Id;
            public readonly SfxPriority Priority;
            public readonly AudioBus Bus;
            public readonly float Range;
            public readonly int Max;
            public readonly float Cooldown;

            public Row(SfxId id, SfxPriority priority, AudioBus bus, float range, int max, float cooldown)
            {
                Id = id;
                Priority = priority;
                Bus = bus;
                Range = range;
                Max = max;
                Cooldown = cooldown;
            }
        }

        private const SfxPriority L = SfxPriority.Low, N = SfxPriority.Normal, H = SfxPriority.High, C = SfxPriority.Critical;
        private const AudioBus S = AudioBus.Sfx, A = AudioBus.Alert;

        public static readonly Row[] Table =
        {
            new Row(SfxId.FootstepGrass, L, S, 6, 6, 0.25f),
            new Row(SfxId.FootstepSwamp, L, S, 6, 6, 0.3f),
            new Row(SfxId.BushEnter, L, S, 0, 2, 0.5f),
            new Row(SfxId.MeleeSwing, N, S, 8, 4, 0.15f),
            new Row(SfxId.MeleeHit, N, S, 8, 4, 0.1f),
            new Row(SfxId.ShootRanged, H, S, 12, 6, 0.08f),
            new Row(SfxId.ShootCrossbow, H, S, 12, 3, 0.3f),
            new Row(SfxId.Throw, N, S, 8, 3, 0.3f),
            new Row(SfxId.Reload, L, S, 6, 2, 0.5f),
            new Row(SfxId.EmptyClick, L, S, 0, 1, 0.3f),
            new Row(SfxId.BombFuse, H, S, 12, 3, 0.2f),
            new Row(SfxId.Explosion, H, S, 12, 3, 0.08f),
            new Row(SfxId.TrapTriggered, N, S, 8, 3, 0.15f),
            new Row(SfxId.HitMarker, C, S, 0, 1, 0.06f),
            new Row(SfxId.HurtLocal, C, S, 0, 2, 0.1f),
            new Row(SfxId.ArmorAbsorb, H, S, 0, 2, 0.1f),
            new Row(SfxId.Eliminated, H, S, 12, 2, 0.5f),
            new Row(SfxId.PickupWeapon, N, S, 0, 2, 0.15f),
            new Row(SfxId.PickupItem, N, S, 0, 3, 0.1f),
            new Row(SfxId.WearArmor, N, S, 0, 2, 0.2f),
            new Row(SfxId.UseHeal, N, S, 0, 1, 0.3f),
            new Row(SfxId.UseDrink, N, S, 0, 1, 0.3f),
            new Row(SfxId.UseCancel, N, S, 0, 1, 0.2f),
            new Row(SfxId.ZoneBell3, C, A, 0, 1, 0f),
            new Row(SfxId.DragonRoar, C, A, 0, 1, 5f),
            new Row(SfxId.MatchEnd, C, S, 0, 1, 5f),
        };

        /// <summary>Rule table built from <see cref="Table"/> (one clip per id unless overridden, bell repeats 3 × 0.8 s).</summary>
        public static SfxRuleTable Rules(Func<SfxId, int> clipCount = null, float pitchJitter = 0f)
        {
            var rules = new SfxRule[SfxIds.TableSize];
            foreach (var row in Table)
            {
                var bell = row.Id == SfxId.ZoneBell3;
                var volume = row.Id == SfxId.HitMarker ? 1f : row.Id == SfxId.FootstepGrass ? 0.4f : 0.8f;
                rules[(int)row.Id] = new SfxRule(row.Bus, row.Priority, row.Range, row.Max, row.Cooldown,
                    clipCount?.Invoke(row.Id) ?? 1, volume, pitchJitter, bell ? 3 : 1, bell ? 0.8f : 0f);
            }
            return new SfxRuleTable(rules);
        }

        public static Row RowOf(SfxId id)
        {
            foreach (var row in Table) if (row.Id == id) return row;
            throw new ArgumentException($"{id} is not in table 2.1");
        }
    }

    /// <summary>Records what the service asks the Unity side to do. Voices keep playing until the test finishes them.</summary>
    internal sealed class FakeAudioOutput : IAudioOutput
    {
        public readonly struct PlayCall
        {
            public readonly int Voice;
            public readonly SfxId Id;
            public readonly int Clip;
            public readonly AudioBus Bus;
            public readonly float Gain, Pitch;

            public PlayCall(int voice, SfxId id, int clip, AudioBus bus, float gain, float pitch)
            {
                Voice = voice;
                Id = id;
                Clip = clip;
                Bus = bus;
                Gain = gain;
                Pitch = pitch;
            }
        }

        private readonly bool[] _playing;
        public readonly SfxId[] VoiceIds;
        public readonly List<PlayCall> Plays = new List<PlayCall>(8192);
        public readonly float[] BusDb = new float[4];
        public int Stops;
        public float DuckDb;
        public int DuckCalls;

        public FakeAudioOutput(int voices = 16)
        {
            _playing = new bool[voices];
            VoiceIds = new SfxId[voices];
        }

        public int VoiceCount => _playing.Length;

        public int PlayingCount
        {
            get
            {
                var n = 0;
                foreach (var p in _playing) if (p) n++;
                return n;
            }
        }

        public int PlayingOf(SfxId id)
        {
            var n = 0;
            for (var i = 0; i < _playing.Length; i++) if (_playing[i] && VoiceIds[i] == id) n++;
            return n;
        }

        public int CountPlays(SfxId id)
        {
            var n = 0;
            foreach (var p in Plays) if (p.Id == id) n++;
            return n;
        }

        public void PlayVoice(int voice, SfxId id, int clipIndex, AudioBus bus, float gain, float pitch)
        {
            if (Plays.Count < Plays.Capacity) Plays.Add(new PlayCall(voice, id, clipIndex, bus, gain, pitch));
            _playing[voice] = true;
            VoiceIds[voice] = id;
        }

        public void StopVoice(int voice) { _playing[voice] = false; Stops++; }
        public bool IsVoicePlaying(int voice) => _playing[voice];
        public void SetBusDecibels(AudioBus bus, float decibels) => BusDb[(int)bus] = decibels;
        public void SetMusicDuckDecibels(float decibels) { DuckDb = decibels; DuckCalls++; }
        public void FinishAll() { for (var i = 0; i < _playing.Length; i++) _playing[i] = false; }
    }

    internal sealed class FakeAudioSettings : IAudioSettingsStore
    {
        public readonly Dictionary<AudioBus, float> Values = new Dictionary<AudioBus, float>();
        public bool? Music;
        public int Flushes;
        public void Flush() => Flushes++;
        public float Load(AudioBus bus, float fallback) => Values.TryGetValue(bus, out var v) ? v : fallback;
        public void Save(AudioBus bus, float volume01) => Values[bus] = volume01;
        public bool LoadMusicEnabled(bool fallback) => Music ?? fallback;
        public void SaveMusicEnabled(bool enabled) => Music = enabled;
    }

    /// <summary>Records cues the router sends to the audio service.</summary>
    internal sealed class FakeAudioService : IAudioService
    {
        public readonly List<AudioCue> Cues = new List<AudioCue>();
        public void PlaySfx(SfxId id) => Cues.Add(AudioCue.Global(id));
        public void PlaySfx(SfxId id, float worldX, float worldY, int sourceKey = 0) => Cues.Add(AudioCue.At(id, worldX, worldY, sourceKey));
        public void Play(in AudioCue cue) => Cues.Add(cue);
        /// <summary>Stop calls in order, as (id, source); StopAll is recorded as (None, 0).</summary>
        public readonly List<(SfxId Id, int Source)> Stops = new List<(SfxId, int)>();
        /// <summary>Index in <see cref="Cues"/> at the time of each stop, to check ordering against plays.</summary>
        public readonly List<int> StopAt = new List<int>();
        public void Stop(SfxId id, int sourceKey) { Stops.Add((id, sourceKey)); StopAt.Add(Cues.Count); }
        public void StopAll() { Stops.Add((SfxId.None, 0)); StopAt.Add(Cues.Count); }
        public void SetVolume(AudioBus bus, float volume01) { }
        public float GetVolume(AudioBus bus) => 1f;
        public bool MusicEnabled { get; set; }
        public int ActiveVoices => 0;
        public int MaxVoices => 16;

        public int Count(SfxId id)
        {
            var n = 0;
            foreach (var c in Cues) if (c.Id == id) n++;
            return n;
        }

        public List<SfxId> Ids()
        {
            var list = new List<SfxId>();
            foreach (var c in Cues) list.Add(c.Id);
            return list;
        }
    }

    internal sealed class FakeAudioEventSource : IAudioEventSource
    {
        public event Action<AudioCue> CueRaised;
        public int HandlerCount => CueRaised?.GetInvocationList().Length ?? 0;
        public void Raise(in AudioCue cue) => CueRaised?.Invoke(cue);
    }

    internal sealed class FakeMatchFeed : IMatchAudioFeed
    {
        public byte OwnNetId { get; set; } = 255;
        public bool HasZone;
        public bool InWarning;
        public byte Phase;

        public bool TryGetZone(out bool inWarning, out byte phaseIndex)
        {
            inWarning = HasZone && InWarning;
            phaseIndex = Phase;
            return HasZone;
        }
    }

    internal sealed class FakePacketReceiver : IPacketReceiver
    {
        private readonly List<Delegate> _handlers = new List<Delegate>();
        public int HandlerCount => _handlers.Count;
        public void Subscribe<T>(Action<T> handler) where T : struct, INetSerializable => _handlers.Add(handler);
        public void Unsubscribe<T>(Action<T> handler) where T : struct, INetSerializable => _handlers.Remove(handler);

        public void Deliver(EventPacket packet)
        {
            foreach (var h in _handlers.ToArray()) (h as Action<EventPacket>)?.Invoke(packet);
        }
    }

    internal sealed class FakePoseSource : IPlayerPoseSource
    {
        public readonly Dictionary<byte, PlayerPose> Poses = new Dictionary<byte, PlayerPose>();
        public bool TryGetPose(byte netId, out PlayerPose pose) => Poses.TryGetValue(netId, out pose);
    }
}
