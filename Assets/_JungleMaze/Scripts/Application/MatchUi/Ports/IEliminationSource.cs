using System;
using JungleMaze.Domain.MatchUi;

namespace JungleMaze.App.MatchUi
{
    /// <summary>
    /// N7: "the local player is out" and the Eliminated screen data. Live: <c>MatchSessionEliminationSource</c>
    /// (rank and time from the roster, killer once the server sends one). Dev: <c>MockEliminationSource</c>.
    /// Fields the source does not know stay "missing" in <see cref="EliminationInfo"/> and the view hides them.
    /// </summary>
    public interface IEliminationSource
    {
        bool IsLocalEliminated { get; }
        EliminationInfo Current { get; }

        /// <summary>Once per match, when the local player is eliminated. Never for other players (AC18).</summary>
        event Action<EliminationInfo> LocalEliminated;

        /// <summary>Data arrived after the elimination (rank one packet later, TR5).</summary>
        event Action<EliminationInfo> Updated;
    }
}
