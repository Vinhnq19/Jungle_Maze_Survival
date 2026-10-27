using System;

namespace JungleMaze.Domain.MatchUi
{
    /// <summary>Why the local player left the match (AC19). <see cref="Unknown"/> until the server sends a killer.</summary>
    public enum EliminationCause : byte
    {
        Unknown = 0,
        Player = 1,
        Zone = 2,
        LeftMatch = 3,
        Disconnected = 4
    }

    /// <summary>
    /// What the Eliminated screen shows (N7). Every field has a "missing" value and the view hides that line
    /// instead of printing 0/null (AC20): Rank/StartCount 0, KillerNetId 255, names null, floats NaN, Kills −1.
    /// Rank comes from the server as is, ties included; nothing here recomputes it (AC22).
    /// </summary>
    public readonly struct EliminationInfo
    {
        public const byte NoNetId = 255;

        public readonly Guid MatchId;
        public readonly uint EliminationTick;
        public readonly byte Rank;
        public readonly byte StartCount;
        public readonly EliminationCause Cause;
        public readonly byte KillerNetId;
        public readonly string KillerName;
        public readonly string WeaponName;
        public readonly float DistanceCells;
        public readonly float SurvivalSeconds;
        public readonly int Kills;

        public EliminationInfo(Guid matchId, uint eliminationTick, byte rank, byte startCount, EliminationCause cause,
            byte killerNetId, string killerName, string weaponName, float distanceCells, float survivalSeconds, int kills)
        {
            MatchId = matchId;
            EliminationTick = eliminationTick;
            Rank = rank;
            StartCount = startCount;
            Cause = cause;
            KillerNetId = killerNetId;
            KillerName = string.IsNullOrEmpty(killerName) ? null : killerName;
            WeaponName = string.IsNullOrEmpty(weaponName) ? null : weaponName;
            DistanceCells = distanceCells < 0f ? float.NaN : distanceCells;
            SurvivalSeconds = survivalSeconds < 0f ? float.NaN : survivalSeconds;
            Kills = kills < 0 ? -1 : kills;
        }

        /// <summary>Only the match and tick are known; every other line is hidden.</summary>
        public static EliminationInfo Unknown(Guid matchId, uint tick) =>
            new EliminationInfo(matchId, tick, 0, 0, EliminationCause.Unknown, NoNetId, null, null, float.NaN, float.NaN, -1);

        public bool HasRank => Rank > 0 && StartCount > 0;
        public bool HasKiller => KillerNetId != NoNetId;
        public bool HasKillerName => KillerName != null;
        public bool HasWeapon => WeaponName != null;
        public bool HasDistance => !float.IsNaN(DistanceCells) && !float.IsInfinity(DistanceCells);
        public bool HasSurvival => !float.IsNaN(SurvivalSeconds) && !float.IsInfinity(SurvivalSeconds);
        public bool HasKills => Kills >= 0;

        /// <summary>The roster may arrive one packet after the elimination event (TR5): fill rank and time later.</summary>
        public EliminationInfo WithRank(byte rank, byte startCount, float survivalSeconds) =>
            new EliminationInfo(MatchId, EliminationTick, rank, startCount, Cause, KillerNetId, KillerName, WeaponName,
                DistanceCells, survivalSeconds, Kills);
    }
}
