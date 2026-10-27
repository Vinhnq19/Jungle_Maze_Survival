using System;
using JungleMaze.App.Flow;
using JungleMaze.Core.Events;
using JungleMaze.Core.Ticking;

namespace JungleMaze.App.MatchUi
{
    /// <summary>
    /// The ESC menu as plain C#: layers (leave confirm above menu, one ESC closes one layer, AC6), only while the
    /// screen is <see cref="AppScreen.InMatch"/> (AC5), and a leave request published exactly once (AC16). After a
    /// request the buttons stay locked until the screen changes or <see cref="MatchOverlayTuning.LeaveRetrySeconds"/>
    /// pass, then unlock with a retry message (G-7). It never disconnects or changes the screen itself (AC17).
    /// </summary>
    public sealed class MatchMenuController : IMatchMenuState, ITickable, IDisposable
    {
        private readonly IEventBus _bus;
        private readonly Func<double> _clock;
        private readonly Func<Guid> _matchId;
        private readonly IPlayerSettingsWriter _settings;
        private readonly MatchOverlayTuning _tuning;
        private readonly Action<AppScreenChanged> _onScreen;
        private AppScreen _screen;
        private double _lockedAt;

        public MatchMenuController(IEventBus bus, Func<double> clock, Func<Guid> matchId, AppScreen initialScreen,
            IPlayerSettingsWriter settings = null, MatchOverlayTuning tuning = null)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _matchId = matchId ?? (() => Guid.Empty);
            _settings = settings;
            _tuning = tuning ?? MatchOverlayTuning.Default;
            _screen = initialScreen;
            _onScreen = OnScreenChanged;
            _bus.Subscribe(_onScreen);
        }

        public bool IsOpen { get; private set; }
        public bool IsConfirmOpen { get; private set; }
        public bool IsLeaveLocked { get; private set; }

        /// <summary>True after a request timed out: the view shows "Chưa thoát được, thử lại" (G-7).</summary>
        public bool ShowRetryNotice { get; private set; }

        public int LeaveRequests { get; private set; }
        public bool InMatch => _screen == AppScreen.InMatch;
        public bool BlocksGameplayInput => IsOpen && InMatch;

        public event Action<bool> OpenChanged;
        public event Action<bool> ConfirmChanged;
        /// <summary>Lock state or retry notice changed; the view re-reads both.</summary>
        public event Action LeaveStateChanged;

        /// <summary>ESC: closes the top layer, or opens the menu. False when ignored (not in a match).</summary>
        public bool PressEscape()
        {
            if (!InMatch) return false;
            if (IsConfirmOpen) CancelLeave();
            else if (IsOpen) Close();
            else Open();
            return true;
        }

        public void Open()
        {
            if (!InMatch || IsOpen) return;
            IsOpen = true;
            OpenChanged?.Invoke(true);
        }

        /// <summary>"Tiếp tục", ESC, or leaving the match. Forces the debounced settings write (AC12).</summary>
        public void Close()
        {
            if (IsConfirmOpen) SetConfirm(false);
            if (!IsOpen) return;
            IsOpen = false;
            _settings?.Flush();
            OpenChanged?.Invoke(false);
        }

        /// <summary>"Thoát trận": asks first (Q7).</summary>
        public void RequestLeave()
        {
            if (!IsOpen || IsLeaveLocked) return;
            SetConfirm(true);
        }

        /// <summary>"Ở lại": closes the confirm box without publishing (AC15).</summary>
        public void CancelLeave()
        {
            if (IsConfirmOpen) SetConfirm(false);
        }

        /// <summary>"Thoát": publishes once, then locks.</summary>
        public void ConfirmLeave()
        {
            if (!IsConfirmOpen) return;
            if (!PublishLeave(LeaveMatchSource.MatchMenu)) return;
            SetConfirm(false);
        }

        /// <summary>"Về menu"/"Về sảnh" after elimination: no confirm (Q7), same once-only lock.</summary>
        public bool LeaveWithoutConfirm(LeaveMatchSource source) => PublishLeave(source);

        public void Tick(float deltaTime)
        {
            if (!IsLeaveLocked) return;
            if (_clock() - _lockedAt < _tuning.LeaveRetrySeconds) return;
            IsLeaveLocked = false;
            ShowRetryNotice = true;
            LeaveStateChanged?.Invoke();
        }

        public void Dispose() => _bus.Unsubscribe(_onScreen);

        private bool PublishLeave(LeaveMatchSource source)
        {
            if (!InMatch || IsLeaveLocked) return false;
            IsLeaveLocked = true;
            ShowRetryNotice = false;
            _lockedAt = _clock();
            LeaveRequests++;
            LeaveStateChanged?.Invoke();
            _bus.Publish(new LeaveMatchRequested(_matchId(), source));
            return true;
        }

        private void SetConfirm(bool open)
        {
            IsConfirmOpen = open;
            ConfirmChanged?.Invoke(open);
        }

        private void OnScreenChanged(AppScreenChanged change) => ApplyScreen(change.Current);

        /// <summary>What an <see cref="AppScreenChanged"/> does; the F7 preview calls it without a server.</summary>
        public void ApplyScreen(AppScreen screen)
        {
            _screen = screen;
            var hadLock = IsLeaveLocked || ShowRetryNotice;
            IsLeaveLocked = false;
            ShowRetryNotice = false;
            if (screen != AppScreen.InMatch) Close();
            if (hadLock) LeaveStateChanged?.Invoke();
        }
    }
}
