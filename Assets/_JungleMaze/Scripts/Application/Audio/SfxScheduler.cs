using System;
using JungleMaze.Domain.Audio;

namespace JungleMaze.App.Audio
{
    /// <summary>
    /// Pending repeats of multi-play rules (zone bell: 3 plays 0.8 s apart, G-8). Fixed <see cref="Capacity"/> entries.
    /// The first play happens immediately in the caller; this only schedules the later ones on the tick clock.
    /// </summary>
    public sealed class SfxScheduler
    {
        public const int Capacity = 8;

        private readonly AudioCue[] _cues = new AudioCue[Capacity];
        private readonly AudioBus[] _buses = new AudioBus[Capacity];
        private readonly int[] _remaining = new int[Capacity];
        private readonly float[] _next = new float[Capacity];
        private readonly float[] _interval = new float[Capacity];

        public int PendingCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < Capacity; i++) if (_remaining[i] > 0) n++;
                return n;
            }
        }

        /// <summary>Schedules <paramref name="repeats"/> more plays of <paramref name="cue"/>. False when full.</summary>
        public bool Schedule(in AudioCue cue, AudioBus bus, int repeats, float interval, float now)
        {
            if (repeats <= 0) return true;
            for (var i = 0; i < Capacity; i++)
            {
                if (_remaining[i] > 0) continue;
                _cues[i] = cue;
                _buses[i] = bus;
                _remaining[i] = repeats;
                _interval[i] = interval;
                _next[i] = now + interval;
                return true;
            }
            return false;
        }

        /// <summary>True while a repeat on <paramref name="bus"/> is still waiting (keeps music ducked between bells).</summary>
        public bool AnyPendingOn(AudioBus bus)
        {
            for (var i = 0; i < Capacity; i++) if (_remaining[i] > 0 && _buses[i] == bus) return true;
            return false;
        }

        /// <summary>Calls <paramref name="play"/> for every repeat due at <paramref name="now"/>. Pass a cached delegate.</summary>
        public void Update(float now, Action<AudioCue> play)
        {
            for (var i = 0; i < Capacity; i++)
            {
                if (_remaining[i] <= 0 || now + 1e-4f < _next[i]) continue;
                _remaining[i]--;
                _next[i] += _interval[i];
                play(_cues[i]);
            }
        }

        public void Clear() => Array.Clear(_remaining, 0, Capacity);
    }
}
