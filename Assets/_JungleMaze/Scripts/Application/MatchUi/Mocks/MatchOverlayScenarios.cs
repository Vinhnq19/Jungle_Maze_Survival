#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using JungleMaze.App.Flow;
using JungleMaze.Domain.MatchUi;

namespace JungleMaze.App.MatchUi.Mocks
{
    /// <summary>
    /// Dev-only switchboard behind the F7 panel: swaps the overlay between the live sources and the three mocks,
    /// previews a match without a server, eliminates the local player with any cause, toggles the killcam and picks
    /// one of the eight replay cases. Everything goes through the same ports and flow the game uses.
    /// </summary>
    public sealed class MatchOverlayScenarios
    {
        private readonly MatchOverlayFlow _flow;
        private readonly MatchMenuController _menu;
        private readonly KillcamSession _killcam;
        private readonly IEliminationSource _liveElimination;
        private readonly ISpectatorSource _liveSpectator;
        private readonly IKillcamReplaySource _liveReplay;
        private uint _tick = 900;

        public MatchOverlayScenarios(MatchOverlayFlow flow, MatchMenuController menu, KillcamSession killcam,
            IEliminationSource liveElimination, ISpectatorSource liveSpectator, IKillcamReplaySource liveReplay, Func<double> clock)
        {
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _menu = menu;
            _killcam = killcam;
            _liveElimination = liveElimination;
            _liveSpectator = liveSpectator;
            _liveReplay = liveReplay;
            Elimination = new MockEliminationSource();
            Spectator = new MockSpectatorSource();
            Replay = new MockKillcamReplaySource(clock);
        }

        public MockEliminationSource Elimination { get; }
        public MockSpectatorSource Spectator { get; }
        public MockKillcamReplaySource Replay { get; }
        public bool UsingMocks { get; private set; }
        public Guid MatchId { get; } = Guid.NewGuid();

        public void UseMocks(bool on)
        {
            UsingMocks = on;
            Elimination.Reset();
            if (on) Spectator.Populate(9);
            _flow.UseSources(on ? Elimination : _liveElimination, on ? (ISpectatorSource)Spectator : _liveSpectator);
            _killcam?.SetSource(on ? Replay : _liveReplay);
        }

        /// <summary>Overlay as if a match is running (no GameFlow change, no network).</summary>
        public void PreviewMatch()
        {
            if (!UsingMocks) UseMocks(true);
            Elimination.Reset();
            _menu?.ApplyScreen(AppScreen.InMatch);
            _flow.ApplyScreen(AppScreen.InMatch);
        }

        public void EndPreview()
        {
            _menu?.ApplyScreen(AppScreen.MainMenu);
            _flow.ApplyScreen(AppScreen.MainMenu);
        }

        /// <summary>The session's real switch (asset, <c>-killcam</c> or <see cref="SetKillcam"/>).</summary>
        public bool KillcamEnabled => _killcam != null && _killcam.Options.Enabled;

        public void SetKillcam(bool enabled) => _killcam?.SetEnabled(enabled);
        public void SetReplayCase(MockKillcamReplaySource.Case mode) => Replay.Mode = mode;

        public void Eliminate(EliminationCause cause)
        {
            if (!UsingMocks) UseMocks(true);
            Elimination.Reset();
            Elimination.Eliminate(MockEliminationSource.Sample(cause, MatchId, _tick += 30));
        }

        /// <summary>Only rank and survival time, to check hidden lines (AC20).</summary>
        public void EliminateMinimal()
        {
            if (!UsingMocks) UseMocks(true);
            Elimination.Reset();
            Elimination.Eliminate(new EliminationInfo(MatchId, _tick += 30, 6, 10, EliminationCause.Unknown,
                EliminationInfo.NoNetId, null, null, float.NaN, 200f, -1));
        }

        public void Populate(int alive) => Spectator.Populate(alive);

        public void EliminateWatched()
        {
            var id = _flow.Targets.Current;
            if (id != SpectatorTargets.None) Spectator.Eliminate(id);
        }

        public void EndMatch() => Spectator.EndMatch();
    }
}
#endif
