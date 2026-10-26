using System;
using JungleMaze.App.Flow;

namespace JungleMaze.App.Audio
{
    /// <summary>Background music tracks. WaitingRoom uses the Loading clip until ART-05 compresses its own file (T-5).</summary>
    public enum MusicTrack : byte
    {
        None = 0,
        WaitingRoom = 1,
        Loading = 2,
        Match = 3
    }

    /// <summary>
    /// Pure music decisions (G-4, G-9, G-10): which track per screen, a 1 s crossfade between two slots, a 0.4 s
    /// fade-out when music is turned off, and ducking −9 dB within 0.2 s while an alert plays, released within 1 s.
    /// Slot A is the current track; slot B is the previous one fading out. Off by default.
    /// </summary>
    public sealed class MusicPlan
    {
        public const float CrossfadeSeconds = 1f;
        public const float DisableFadeSeconds = 0.4f;
        public const float DuckDecibels = -9f;
        public const float DuckAttackSeconds = 0.2f;
        public const float DuckReleaseSeconds = 1f;

        private bool _enabled;
        private bool _alert;
        private AppScreen _screen = AppScreen.Boot;

        public MusicTrack TrackA { get; private set; }
        public MusicTrack TrackB { get; private set; }
        public float WeightA { get; private set; }
        public float WeightB { get; private set; }
        public float DuckDb { get; private set; }
        public bool Enabled => _enabled;
        public AppScreen Screen => _screen;

        /// <summary>Number of slots with an audible track (≤ 2 during a crossfade, ≤ 1 otherwise).</summary>
        public int AudibleSources => (TrackA != MusicTrack.None && WeightA > 0f ? 1 : 0)
                                     + (TrackB != MusicTrack.None && WeightB > 0f ? 1 : 0);

        public static MusicTrack TrackFor(AppScreen screen)
        {
            switch (screen)
            {
                case AppScreen.MainMenu:
                case AppScreen.InRoom:
                case AppScreen.Results: return MusicTrack.WaitingRoom;
                case AppScreen.Connecting: return MusicTrack.Loading;
                case AppScreen.InMatch: return MusicTrack.Match;
                default: return MusicTrack.None;
            }
        }

        public void SetScreen(AppScreen screen) { _screen = screen; Retarget(); }
        public void SetEnabled(bool enabled) { _enabled = enabled; Retarget(); }
        public void SetAlertActive(bool active) => _alert = active;

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            if (TrackA != MusicTrack.None) WeightA = Math.Min(1f, WeightA + deltaTime / CrossfadeSeconds);
            if (TrackB != MusicTrack.None)
            {
                var fade = TrackA == MusicTrack.None ? DisableFadeSeconds : CrossfadeSeconds;
                WeightB = Math.Max(0f, WeightB - deltaTime / fade);
                if (WeightB <= 0f) TrackB = MusicTrack.None;
            }

            var target = _alert ? DuckDecibels : 0f;
            var rate = -DuckDecibels / (_alert ? DuckAttackSeconds : DuckReleaseSeconds) * deltaTime;
            DuckDb = DuckDb < target ? Math.Min(target, DuckDb + rate) : Math.Max(target, DuckDb - rate);
        }

        private void Retarget()
        {
            var desired = _enabled ? TrackFor(_screen) : MusicTrack.None;
            if (desired == TrackA) return;
            if (desired != MusicTrack.None && desired == TrackB)
            {
                // Back to the track that is fading out: swap slots so it keeps playing instead of restarting.
                var weight = WeightB;
                TrackB = TrackA;
                WeightB = WeightA;
                TrackA = desired;
                WeightA = weight;
                return;
            }
            if (TrackA != MusicTrack.None)
            {
                TrackB = TrackA;
                WeightB = WeightA;
            }
            TrackA = desired;
            WeightA = 0f;
        }
    }
}
