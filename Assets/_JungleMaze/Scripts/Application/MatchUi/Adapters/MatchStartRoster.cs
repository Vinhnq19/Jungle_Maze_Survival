using System;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Match;

namespace JungleMaze.App.MatchUi
{
    /// <summary>
    /// Who was in the match when it started: the first non-empty roster seen while the phase is InGame, per MatchId.
    /// "N" of the rank and the spectator list come from here, never from <c>maxPlayers</c> (AC19, AC27).
    /// Names are cached once per match so the per-tick list rebuild does not allocate.
    /// </summary>
    internal sealed class MatchStartRoster
    {
        public const int MaxPlayers = 10;

        private readonly string[] _names = new string[MaxPlayers];
        private Guid _matchId;
        private ushort _members;

        public int Count { get; private set; }
        public Guid MatchId => _matchId;

        /// <summary>True when the match changed since the last call (callers reset their own state).</summary>
        public bool Update(MatchSession match)
        {
            var changed = false;
            if (match.MatchId != _matchId)
            {
                _matchId = match.MatchId;
                _members = 0;
                Count = 0;
                Array.Clear(_names, 0, _names.Length);
                changed = true;
            }
            var roster = match.Roster;
            if (Count == 0 && match.Phase == MatchPhase.InGame && roster != null && roster.Length > 0)
            {
                for (var i = 0; i < roster.Length; i++)
                {
                    var id = roster[i].NetId;
                    if (id >= MaxPlayers) continue;
                    _members |= (ushort)(1 << id);
                    _names[id] = string.IsNullOrEmpty(roster[i].DisplayName) ? "Người chơi " + (id + 1) : roster[i].DisplayName;
                }
                Count = roster.Length;
            }
            return changed;
        }

        public bool Contains(byte netId) => netId < MaxPlayers && (_members & (1 << netId)) != 0;

        public string NameOf(byte netId) => netId < MaxPlayers ? _names[netId] : null;

        /// <summary>The roster entry of <paramref name="netId"/> in the latest roster (rank and time are filled late).</summary>
        public static bool TryFind(MatchSession match, byte netId, out MatchParticipantState state)
        {
            var roster = match.Roster;
            if (roster != null)
                for (var i = 0; i < roster.Length; i++)
                    if (roster[i].NetId == netId) { state = roster[i]; return true; }
            state = default;
            return false;
        }
    }
}
