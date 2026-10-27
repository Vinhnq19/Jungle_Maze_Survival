using System;
using JungleMaze.App.MatchUi;
using JungleMaze.Domain.MatchUi;
using UnityEngine.Events;

namespace JungleMaze.Presentation.UI.MatchOverlay
{
    /// <summary>
    /// Phase panels: exactly one of Killcam / Eliminated / Spectator is visible, following
    /// <see cref="MatchOverlayFlow.Phase"/> in the same call that changed it (AC18, AC21, AC33). Spectator rows and
    /// labels are rewritten on list or target events only; the killcam board is redrawn every frame from the clip.
    /// </summary>
    public sealed partial class MatchOverlayPresenter
    {
        private OverlayFade _eliminatedFade, _spectatorFade, _killcamFade, _noticeFade;
        private Action _onInfo, _onSpectatorChanged;
        private Action<string> _onNotice;
        private Action<byte, bool> _onTarget;
        private UnityAction _onHome, _onSpectate, _onSpectatorHome, _onKiller, _onSkip;
        private UnityAction[] _onRows;
        private ISpectatorSource _boundSpectator;
        private bool _shownCanSpectate, _shownCanJump, _retryOnNotice;

        public OverlayFade EliminatedFade => _eliminatedFade;
        public OverlayFade SpectatorFade => _spectatorFade;
        public OverlayFade KillcamFade => _killcamFade;
        public OverlayFade NoticeFade => _noticeFade;

        private void BindPhases()
        {
            _eliminatedFade = new OverlayFade(_root.Eliminated != null ? _root.Eliminated.Group : null);
            _spectatorFade = new OverlayFade(_root.Spectator != null ? _root.Spectator.Group : null);
            _killcamFade = new OverlayFade(_root.Killcam != null ? _root.Killcam.Group : null);
            _noticeFade = new OverlayFade(_root.Notice != null ? _root.Notice.Group : null);
            if (_flow == null) return;

            _onInfo = () => { if (_flow.Phase == MatchOverlayPhase.Eliminated) _root.Eliminated.Show(_flow.Info); };
            _onNotice = OnNotice;
            _onTarget = (id, automatic) => RefreshSpectator();
            _onSpectatorChanged = RefreshSpectator;
            _onHome = () => _menu.LeaveWithoutConfirm(LeaveMatchSource.EliminatedScreen);
            _onSpectate = () => _flow.Spectate();
            _onSpectatorHome = () => _menu.LeaveWithoutConfirm(LeaveMatchSource.Spectator);
            _onKiller = () => _flow.Targets.JumpToKiller();
            _onSkip = () => _flow.SkipKillcam();
            _flow.InfoChanged += _onInfo;
            _flow.NoticeRaised += _onNotice;
            _flow.Targets.TargetChanged += _onTarget;

            _root.Eliminated.Home.onClick.AddListener(_onHome);
            _root.Eliminated.Spectate.onClick.AddListener(_onSpectate);
            _root.Spectator.Home.onClick.AddListener(_onSpectatorHome);
            _root.Spectator.Killer.onClick.AddListener(_onKiller);
            _root.Killcam.Skip.onClick.AddListener(_onSkip);
            var rows = _root.Spectator.Rows;
            _onRows = new UnityAction[rows.Length];
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                _onRows[i] = () => _flow.Targets.Select(row.NetId);
                row.Button.onClick.AddListener(_onRows[i]);
            }
        }

        private void UnbindPhases()
        {
            if (_flow == null) return;
            _flow.InfoChanged -= _onInfo;
            _flow.NoticeRaised -= _onNotice;
            _flow.Targets.TargetChanged -= _onTarget;
            BindSpectatorSource(null);
            if (_root == null) return;
            _root.Eliminated.Home.onClick.RemoveListener(_onHome);
            _root.Eliminated.Spectate.onClick.RemoveListener(_onSpectate);
            _root.Spectator.Home.onClick.RemoveListener(_onSpectatorHome);
            _root.Spectator.Killer.onClick.RemoveListener(_onKiller);
            _root.Killcam.Skip.onClick.RemoveListener(_onSkip);
            var rows = _root.Spectator.Rows;
            for (var i = 0; i < rows.Length && i < _onRows.Length; i++) rows[i].Button.onClick.RemoveListener(_onRows[i]);
        }

        private void ShowPhase(MatchOverlayPhase phase)
        {
            if (_flow == null) return;
            _killcamFade.Set(phase == MatchOverlayPhase.Killcam);
            _eliminatedFade.Set(phase == MatchOverlayPhase.Eliminated);
            _spectatorFade.Set(phase == MatchOverlayPhase.Spectating);
            switch (phase)
            {
                case MatchOverlayPhase.Killcam:
                    _root.Killcam.SetTitle(_flow.Info.KillerName);
                    _root.Killcam.SetNames(NameOf);
                    DrawKillcam();
                    break;
                case MatchOverlayPhase.Eliminated:
                    _root.Eliminated.Show(_flow.Info);
                    _shownCanSpectate = _flow.CanSpectate;
                    _root.Eliminated.SetSpectateAvailable(_shownCanSpectate, _flow.SpectateBlockedReason);
                    break;
                case MatchOverlayPhase.Spectating:
                    BindSpectatorSource(_flow.Spectator);
                    _root.Spectator.ResetCache();
                    RefreshSpectator();
                    break;
                default:
                    BindSpectatorSource(null);
                    break;
            }
        }

        private void TickPhases(float unscaledDelta)
        {
            var fade = _tuning.MenuFadeSeconds;
            _killcamFade.Step(unscaledDelta, fade);
            _eliminatedFade.Step(unscaledDelta, fade);
            _spectatorFade.Step(unscaledDelta, fade);
            _noticeFade.Step(unscaledDelta, fade);
            if (_flow == null) return;
            switch (_flow.Phase)
            {
                case MatchOverlayPhase.Killcam:
                    DrawKillcam();
                    break;
                case MatchOverlayPhase.Eliminated:
                    if (_flow.CanSpectate != _shownCanSpectate)
                    {
                        _shownCanSpectate = _flow.CanSpectate;
                        _root.Eliminated.SetSpectateAvailable(_shownCanSpectate, _flow.SpectateBlockedReason);
                    }
                    break;
                case MatchOverlayPhase.Spectating:
                    if (!ReferenceEquals(_boundSpectator, _flow.Spectator)) { BindSpectatorSource(_flow.Spectator); RefreshSpectator(); }
                    if (_flow.Targets.CanJumpToKiller != _shownCanJump) RefreshKillerButton();
                    break;
            }
        }

        /// <summary>Rows, "Đang theo dõi", "Còn sống n/N" and the target card from the current source.</summary>
        public void RefreshSpectator()
        {
            if (_flow == null || _flow.Phase != MatchOverlayPhase.Spectating || _flow.Spectator == null) return;
            var source = _flow.Spectator;
            var view = _root.Spectator;
            var current = _flow.Targets.Current;
            var rows = view.Rows;
            for (var i = 0; i < rows.Length; i++)
            {
                if (i < source.AliveCount)
                {
                    var entry = source.GetAlive(i);
                    rows[i].Set(entry.NetId, entry.Name, entry.NetId == current);
                }
                else rows[i].Hide();
            }
            view.SetAlive(source.AliveCount, source.StartCount);
            var has = source.TryGetEntry(current, out var target);
            view.SetTarget(has, target);
            RefreshKillerButton();
        }

        private void RefreshKillerButton()
        {
            _shownCanJump = _flow.Targets.CanJumpToKiller;
            _root.Spectator.Killer.interactable = _shownCanJump;
        }

        private void DrawKillcam()
        {
            var session = _flow.Killcam;
            if (session == null || session.State != KillcamSession.SessionState.Playing || session.Clip == null) return;
            _root.Killcam.SetCountdown(session.Remaining);
            if (!session.TryGetKillerPosition(out var cx, out var cy)) return;
            var request = session.LastRequest;
            _root.Killcam.Draw(session.Clip, session.Frame, cx, cy, request.KillerNetId, request.VictimNetId);
        }

        private string NameOf(byte netId)
        {
            if (netId == _flow.Info.KillerNetId && _flow.Info.HasKillerName) return _flow.Info.KillerName;
            if (_flow.Spectator != null && netId == _flow.Spectator.LocalNetId) return "Bạn";
            return _flow.Spectator != null && _flow.Spectator.TryGetEntry(netId, out var e) ? e.Name : null;
        }

        private void OnNotice(string text)
        {
            if (_root.Notice == null) return;
            if (text != null)
            {
                _root.Notice.Text.text = text;
                _retryOnNotice = false;
            }
            _noticeFade.Set(text != null || _retryOnNotice);
        }

        /// <summary>
        /// "Về menu" (Eliminated) and "Về sảnh" (Spectator) follow the same 5 s lock as the ESC menu; when the lock runs
        /// out without a screen change, the retry line shows on the notice bar, since the ESC panel is not open there.
        /// </summary>
        private void RefreshPhaseLeaveState(bool locked)
        {
            if (_root.Eliminated != null) _root.Eliminated.Home.interactable = !locked;
            if (_root.Spectator != null) _root.Spectator.Home.interactable = !locked;
            if (_root.Notice == null || _noticeFade == null) return;
            var phase = _flow != null ? _flow.Phase : MatchOverlayPhase.Playing;
            var retry = _menu.ShowRetryNotice && (phase == MatchOverlayPhase.Eliminated || phase == MatchOverlayPhase.Spectating);
            if (retry)
            {
                _root.Notice.Text.text = RetryText;
                _retryOnNotice = true;
                _noticeFade.Set(true);
            }
            else if (_retryOnNotice)
            {
                _retryOnNotice = false;
                var flowNotice = _flow != null ? _flow.Notice : null;
                if (flowNotice != null) _root.Notice.Text.text = flowNotice;
                _noticeFade.Set(flowNotice != null);
            }
        }

        private void BindSpectatorSource(ISpectatorSource source)
        {
            if (ReferenceEquals(_boundSpectator, source)) return;
            if (_boundSpectator != null) _boundSpectator.Changed -= _onSpectatorChanged;
            _boundSpectator = source;
            if (_boundSpectator != null) _boundSpectator.Changed += _onSpectatorChanged;
        }
    }
}
