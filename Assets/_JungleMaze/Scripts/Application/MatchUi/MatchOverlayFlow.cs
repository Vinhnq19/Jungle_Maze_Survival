using System;
using JungleMaze.App.Flow;
using JungleMaze.Core.Events;
using JungleMaze.Core.Ticking;
using JungleMaze.Domain.MatchUi;

namespace JungleMaze.App.MatchUi
{
    /// <summary>
    /// UI-03 sub-states of a match (G-13): Playing → (Killcam) → Eliminated → Spectating → HandedOff. Leaving
    /// <see cref="AppScreen.InMatch"/> resets everything (AC5, AC42); the match ending or nobody left alive hands off
    /// at once to the results flow, which stays GameFlow/UI-02's (AC28, T-10). Every switch happens inside the call
    /// that caused it, so the "≤ 500 ms" budgets of G-6/G-8 are met on the same frame. The ESC menu is separate and
    /// works on top of any phase (AC6).
    /// </summary>
    public sealed class MatchOverlayFlow : ITickable, IDisposable
    {
        private readonly IEventBus _bus;
        private readonly Func<double> _clock;
        private readonly MatchOverlayTuning _tuning;
        private readonly Action<AppScreenChanged> _onScreen;
        private readonly Action<EliminationInfo> _onEliminated;
        private readonly Action<byte> _onPlayerOut;
        private readonly Action _onSpectatorChanged;
        private IEliminationSource _elimination;
        private ISpectatorSource _spectator;
        private readonly KillcamSession _killcam;

        public MatchOverlayFlow(IEventBus bus, Func<double> clock, IEliminationSource elimination, ISpectatorSource spectator,
            AppScreen initialScreen, MatchOverlayTuning tuning = null, KillcamSession killcam = null)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _tuning = tuning ?? MatchOverlayTuning.Default;
            _killcam = killcam;
            _onScreen = OnScreenChanged;
            _onEliminated = OnLocalEliminated;
            _onPlayerOut = OnPlayerOut;
            _onSpectatorChanged = CheckEnd;
            Targets = new SpectatorTargets(spectator);
            Camera = new SpectatorCameraFollow(_tuning.CameraSwitchSeconds);
            Targets.TargetChanged += OnTargetChanged;
            _bus.Subscribe(_onScreen);
            UseSources(elimination, spectator);
            Phase = initialScreen == AppScreen.InMatch ? MatchOverlayPhase.Playing : MatchOverlayPhase.Inactive;
        }

        public MatchOverlayPhase Phase { get; private set; }
        public SpectatorTargets Targets { get; }
        public SpectatorCameraFollow Camera { get; }
        public IEliminationSource Elimination => _elimination;
        public ISpectatorSource Spectator => _spectator;
        public EliminationInfo Info { get; private set; }

        /// <summary>Null when the killcam path is not composed (always skipped silently).</summary>
        public KillcamSession Killcam => _killcam;

        /// <summary>Short status line ("&lt;name&gt; đã bị loại", "Killcam không khả dụng: …"); null when none.</summary>
        public string Notice { get; private set; }
        public double NoticeUntil { get; private set; }

        /// <summary>Unscaled time of the last phase change (tests measure the G-4…G-8 budgets with it).</summary>
        public double PhaseChangedAt { get; private set; }

        public bool CanSpectate => _spectator != null && _spectator.AliveCount > 0 && !_spectator.MatchEnded;

        /// <summary>Why "Theo dõi người khác" is disabled (AC21); null when enabled.</summary>
        public string SpectateBlockedReason => CanSpectate ? null
            : _spectator != null && _spectator.MatchEnded ? "Trận đã kết thúc" : "Không còn người chơi nào để theo dõi";

        public event Action<MatchOverlayPhase> PhaseChanged;
        public event Action<string> NoticeRaised;
        /// <summary>The info shown on the Eliminated screen changed (late rank).</summary>
        public event Action InfoChanged;

        /// <summary>Swaps the data sources (dev scenarios use mocks); resets the overlay to the current screen.</summary>
        public void UseSources(IEliminationSource elimination, ISpectatorSource spectator)
        {
            Unbind();
            _elimination = elimination;
            _spectator = spectator;
            Bind();
            Targets.SetSource(spectator);
            Camera.Clear();
            if (Phase != MatchOverlayPhase.Inactive) SetPhase(MatchOverlayPhase.Playing);
        }

        /// <summary>"Theo dõi người khác": Eliminated → Spectating when someone is alive (AC21).</summary>
        public bool Spectate()
        {
            if (Phase != MatchOverlayPhase.Eliminated || !CanSpectate) return false;
            Targets.KillerNetId = Info.HasKiller ? Info.KillerNetId : SpectatorTargets.None;
            if (!Targets.Begin()) return false;
            if (_spectator.TryGetPosition(Targets.Current, out var x, out var y)) Camera.Snap(x, y);
            SetPhase(MatchOverlayPhase.Spectating);
            return true;
        }

        /// <summary>Space/click "Bỏ qua" during the killcam (Q3); the eliminated screen follows in the same call.</summary>
        public bool SkipKillcam()
        {
            if (Phase != MatchOverlayPhase.Killcam) return false;
            _killcam.Skip();
            SetPhase(MatchOverlayPhase.Eliminated);
            return true;
        }

        public void Tick(float deltaTime)
        {
            var now = _clock();
            if (Notice != null && now >= NoticeUntil) { Notice = null; NoticeRaised?.Invoke(null); }
            if (Phase == MatchOverlayPhase.Killcam) TickKillcam();
            CheckEnd();
        }

        public void Dispose()
        {
            Unbind();
            Targets.TargetChanged -= OnTargetChanged;
            _bus.Unsubscribe(_onScreen);
        }

        /// <summary>Shows a short notice for <paramref name="seconds"/>.</summary>
        public void ShowNotice(string text, float seconds)
        {
            Notice = text;
            NoticeUntil = _clock() + seconds;
            NoticeRaised?.Invoke(text);
        }

        private void OnScreenChanged(AppScreenChanged change)
        {
            Release();
            SetPhase(change.Current == AppScreen.InMatch ? MatchOverlayPhase.Playing : MatchOverlayPhase.Inactive);
        }

        private void OnLocalEliminated(EliminationInfo info)
        {
            if (Phase != MatchOverlayPhase.Playing) return;
            Info = info;
            if (_killcam != null && _killcam.Begin(info, _spectator != null ? _spectator.LocalNetId : EliminationInfo.NoNetId))
            {
                SetPhase(MatchOverlayPhase.Killcam);
                TickKillcam();
            }
            else
            {
                // Disabled is the normal case and stays silent (AC31); every other reason gets a notice (AC35).
                if (_killcam != null) NoticeKillcam(_killcam.Reason);
                SetPhase(MatchOverlayPhase.Eliminated);
            }
            CheckEnd();
        }

        private void OnInfoUpdated(EliminationInfo info)
        {
            if (Phase == MatchOverlayPhase.Inactive || Phase == MatchOverlayPhase.Playing) return;
            Info = info;
            InfoChanged?.Invoke();
        }

        private void OnPlayerOut(byte netId)
        {
            if (Phase != MatchOverlayPhase.Spectating) return;
            var name = Targets.CurrentName;
            if (!Targets.OnPlayerEliminated(netId)) return;
            ShowNotice((name ?? "Người chơi") + " đã bị loại", _tuning.EliminatedNoticeSeconds);
            CheckEnd();
        }

        private void OnTargetChanged(byte netId, bool automatic)
        {
            if (Phase == MatchOverlayPhase.Spectating) Camera.Retarget(_clock());
        }

        private void TickKillcam()
        {
            _killcam.Tick();
            if (_killcam.State == KillcamSession.SessionState.Failed)
            {
                NoticeKillcam(_killcam.Reason);
                SetPhase(MatchOverlayPhase.Eliminated);
            }
            else if (_killcam.State == KillcamSession.SessionState.Finished) SetPhase(MatchOverlayPhase.Eliminated);
        }

        private void NoticeKillcam(KillcamUnavailableReason reason)
        {
            if (reason == KillcamUnavailableReason.None || reason == KillcamUnavailableReason.Disabled) return;
            ShowNotice("Killcam không khả dụng: " + KillcamSession.ReasonText(reason, _killcam.Options.DurationSeconds),
                _tuning.KillcamNoticeSeconds);
        }

        private void CheckEnd()
        {
            if (Phase != MatchOverlayPhase.Eliminated && Phase != MatchOverlayPhase.Spectating && Phase != MatchOverlayPhase.Killcam) return;
            if (_spectator == null) return;
            var ended = _spectator.MatchEnded;
            var nobody = Phase == MatchOverlayPhase.Spectating && (_spectator.AliveCount == 0 || !Targets.HasTarget);
            if (ended || nobody) HandOff();
        }

        private void HandOff()
        {
            Release();
            SetPhase(MatchOverlayPhase.HandedOff);
        }

        /// <summary>Frees everything that belongs to one match (G-14, AC42).</summary>
        private void Release()
        {
            _killcam?.Stop();
            Targets.Clear();
            Camera.Clear();
            if (Phase == MatchOverlayPhase.Inactive || Phase == MatchOverlayPhase.Playing) Info = default;
        }

        private void SetPhase(MatchOverlayPhase phase)
        {
            if (phase == MatchOverlayPhase.Playing || phase == MatchOverlayPhase.Inactive) Info = default;
            if (Phase == phase) return;
            Phase = phase;
            PhaseChangedAt = _clock();
            PhaseChanged?.Invoke(phase);
        }

        private void Bind()
        {
            if (_elimination != null)
            {
                _elimination.LocalEliminated += _onEliminated;
                _elimination.Updated += OnInfoUpdated;
            }
            if (_spectator != null)
            {
                _spectator.PlayerEliminated += _onPlayerOut;
                _spectator.Changed += _onSpectatorChanged;
            }
        }

        private void Unbind()
        {
            if (_elimination != null)
            {
                _elimination.LocalEliminated -= _onEliminated;
                _elimination.Updated -= OnInfoUpdated;
            }
            if (_spectator != null)
            {
                _spectator.PlayerEliminated -= _onPlayerOut;
                _spectator.Changed -= _onSpectatorChanged;
            }
        }
    }
}
