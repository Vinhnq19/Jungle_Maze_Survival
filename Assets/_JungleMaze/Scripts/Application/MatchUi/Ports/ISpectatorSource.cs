using System;
using JungleMaze.Domain.MatchUi;

namespace JungleMaze.App.MatchUi
{
    /// <summary>
    /// N8: who is still alive and where. The list never contains the local player and never anyone who was not in
    /// the match when it started (AC23, AC27); it is ordered by NetId so next/previous are stable.
    /// Live positions come from the MOV-03 interpolation (<c>IPlayerPoseSource</c>) behind <see cref="TryGetPosition"/>.
    /// </summary>
    public interface ISpectatorSource
    {
        byte LocalNetId { get; }

        /// <summary>Players when the match started (the N in "Còn sống n/N"); 0 = not known yet.</summary>
        int StartCount { get; }

        /// <summary>Alive players other than the local one.</summary>
        int AliveCount { get; }

        SpectatorEntry GetAlive(int index);
        bool TryGetEntry(byte netId, out SpectatorEntry entry);
        bool TryGetPosition(byte netId, out float x, out float y);

        /// <summary>The server said the match is over (AC28, AC37).</summary>
        bool MatchEnded { get; }

        /// <summary>A watched-able player left the alive list (eliminated or gone).</summary>
        event Action<byte> PlayerEliminated;

        /// <summary>List, HP or <see cref="MatchEnded"/> changed.</summary>
        event Action Changed;
    }
}
