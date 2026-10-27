using System;
using JungleMaze.App.MatchUi;
using JungleMaze.Core.Events;

namespace JungleMaze.Composition
{
    /// <summary>
    /// Temporary listener for N6 (T-2) until UI-02 handles <see cref="LeaveMatchRequested"/> itself: does what the
    /// "Rời phòng" button does today (PlayerLeave, disconnect, GameFlow back to the main menu). Lives in the composition
    /// root, not in UI-03 code, and is removed by deleting its one registration line (T-12).
    /// </summary>
    public sealed class LeaveMatchBridge : IDisposable
    {
        private readonly IEventBus _bus;
        private readonly Action _leave;
        private readonly Action<LeaveMatchRequested> _onLeave;

        public LeaveMatchBridge(IEventBus bus, Action leave)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _leave = leave ?? throw new ArgumentNullException(nameof(leave));
            _onLeave = _ => _leave();
            _bus.Subscribe(_onLeave);
        }

        public void Dispose() => _bus.Unsubscribe(_onLeave);
    }
}
