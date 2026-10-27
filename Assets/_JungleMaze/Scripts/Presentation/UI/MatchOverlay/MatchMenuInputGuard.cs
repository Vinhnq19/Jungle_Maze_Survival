using System;
using JungleMaze.App.MatchUi;
using JungleMaze.App.Player;
using JungleMaze.App.Ports;
using JungleMaze.Domain.Player;

namespace JungleMaze.Presentation.UI.MatchOverlay
{
    /// <summary>
    /// Temporary N5 hook (T-1 = A) until MOV-02 reads <see cref="IMatchMenuState"/> itself. Uses public API only, edits
    /// no file of Vinh/Vũ. While the menu is open: <see cref="MatchDemoView.UseExternalIntent"/> = true so the demo stops
    /// writing intent, a neutral intent (no move, no buttons, aim kept on our current facing) is set once, and the
    /// offline <see cref="PlayerInputService"/> is paused. Closing restores exactly what was there; the offline sender
    /// comes back only while still in a match, since <c>InMatchState</c> owns that flag. Remove with one line in
    /// <c>Bootstrap.MatchOverlay.cs</c> (T-12).
    /// </summary>
    public sealed class MatchMenuInputGuard : IDisposable
    {
        private readonly IMatchMenuState _menu;
        private readonly Action<PlayerIntent> _setIntent;
        private readonly Func<byte> _ownNetId;
        private readonly IPlayerPoseSource _poses;
        private readonly PlayerInputService _offlineInput;
        private readonly Func<MatchDemoView> _findDemo;
        private readonly Func<bool> _inMatch;
        private readonly Action<bool> _onOpen;
        private MatchDemoView _demo;
        private bool _engaged, _savedExternal, _pausedOffline;

        public MatchMenuInputGuard(IMatchMenuState menu, Action<PlayerIntent> setIntent, Func<byte> ownNetId,
            IPlayerPoseSource poses, PlayerInputService offlineInput, Func<MatchDemoView> findDemo, Func<bool> inMatch)
        {
            _menu = menu ?? throw new ArgumentNullException(nameof(menu));
            _setIntent = setIntent;
            _ownNetId = ownNetId ?? (() => 255);
            _poses = poses;
            _offlineInput = offlineInput;
            _findDemo = findDemo;
            _inMatch = inMatch ?? (() => true);
            _onOpen = OnOpenChanged;
            _menu.OpenChanged += _onOpen;
        }

        public bool Engaged => _engaged;

        public void Dispose()
        {
            _menu.OpenChanged -= _onOpen;
            Release();
        }

        private void OnOpenChanged(bool open)
        {
            if (open && _menu.BlocksGameplayInput) Engage();
            else Release();
        }

        private void Engage()
        {
            if (_engaged) return;
            _engaged = true;
            _demo = _findDemo?.Invoke();
            if (_demo != null)
            {
                _savedExternal = _demo.UseExternalIntent;
                _demo.UseExternalIntent = true;
            }
            var neutral = new PlayerIntent();
            var own = _ownNetId();
            if (_poses != null && own != 255 && _poses.TryGetPose(own, out var pose)) neutral.AimDegrees = pose.RotationDegrees;
            _setIntent?.Invoke(neutral);
            _pausedOffline = _offlineInput != null && _offlineInput.Enabled;
            if (_pausedOffline) _offlineInput.Enabled = false;
        }

        private void Release()
        {
            if (!_engaged) return;
            _engaged = false;
            if (_demo != null) _demo.UseExternalIntent = _savedExternal;
            _demo = null;
            if (_pausedOffline && _inMatch()) _offlineInput.Enabled = true;
            _pausedOffline = false;
        }
    }
}
