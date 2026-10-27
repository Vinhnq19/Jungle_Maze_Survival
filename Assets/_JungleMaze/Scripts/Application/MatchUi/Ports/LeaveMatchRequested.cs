using System;

namespace JungleMaze.App.MatchUi
{
    /// <summary>Which UI-03 button asked to leave.</summary>
    public enum LeaveMatchSource : byte
    {
        MatchMenu = 0,
        EliminatedScreen = 1,
        Spectator = 2
    }

    /// <summary>
    /// N6 on the EventBus: the player asked to leave the match. UI-03 publishes it exactly once and does nothing else
    /// (no disconnect, no screen change, no rank, AC17). GameFlow/UI-02 owns what happens next; until UI-02 listens,
    /// a temporary bridge in the composition root handles it (T-2).
    /// </summary>
    public readonly struct LeaveMatchRequested
    {
        public readonly Guid MatchId;
        public readonly LeaveMatchSource Source;

        public LeaveMatchRequested(Guid matchId, LeaveMatchSource source)
        {
            MatchId = matchId;
            Source = source;
        }
    }
}
