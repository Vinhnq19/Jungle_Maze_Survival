using System;
using JungleMaze.App.Audio;
using UnityEngine;

namespace JungleMaze.Presentation.Audio
{
    /// <summary>
    /// Applies a <see cref="MusicPlan"/> to two looping AudioSources: each audible slot is mapped to the source that
    /// already plays its track (so a track that comes back is not restarted), volumes follow the slot weights, and
    /// a source with no slot is stopped. Ducking is applied by the playback service through the mixer.
    /// </summary>
    public sealed class MusicDirector
    {
        private readonly AudioSource[] _sources;
        private readonly MusicTrack[] _playing = new MusicTrack[2];
        private readonly Func<MusicTrack, AudioClip> _clipFor;
        private MusicPlan _plan;

        public MusicDirector(AudioSource a, AudioSource b, Func<MusicTrack, AudioClip> clipFor)
        {
            _sources = new[] { a, b };
            _clipFor = clipFor;
        }

        public void Bind(MusicPlan plan) => _plan = plan;

        /// <summary>Sources currently playing (0..2).</summary>
        public int PlayingSources
        {
            get
            {
                var n = 0;
                foreach (var s in _sources) if (s != null && s.isPlaying) n++;
                return n;
            }
        }

        public MusicTrack TrackOn(int source) => _playing[source];
        public AudioSource Source(int index) => _sources[index];

        public void Apply()
        {
            if (_plan == null) return;
            var a = _plan.WeightA > 0f ? _plan.TrackA : MusicTrack.None;
            var b = _plan.WeightB > 0f ? _plan.TrackB : MusicTrack.None;
            var sourceA = Claim(a, -1);
            var sourceB = Claim(b, sourceA);
            for (var i = 0; i < _sources.Length; i++)
            {
                var source = _sources[i];
                if (source == null) continue;
                if (i == sourceA) source.volume = _plan.WeightA;
                else if (i == sourceB) source.volume = _plan.WeightB;
                else if (_playing[i] != MusicTrack.None)
                {
                    source.Stop();
                    _playing[i] = MusicTrack.None;
                }
            }
        }

        public void StopAll()
        {
            for (var i = 0; i < _sources.Length; i++)
            {
                if (_sources[i] != null) _sources[i].Stop();
                _playing[i] = MusicTrack.None;
            }
        }

        private int Claim(MusicTrack track, int taken)
        {
            if (track == MusicTrack.None) return -1;
            for (var i = 0; i < _sources.Length; i++) if (i != taken && _playing[i] == track) return i;
            for (var i = 0; i < _sources.Length; i++)
            {
                if (i == taken || _playing[i] != MusicTrack.None && IsWanted(_playing[i])) continue;
                var clip = _clipFor?.Invoke(track);
                var source = _sources[i];
                if (clip == null || source == null) return -1;
                source.Stop();
                source.clip = clip;
                source.volume = 0f;
                source.Play();
                _playing[i] = track;
                return i;
            }
            return -1;
        }

        private bool IsWanted(MusicTrack track) =>
            (_plan.TrackA == track && _plan.WeightA > 0f) || (_plan.TrackB == track && _plan.WeightB > 0f);
    }
}
