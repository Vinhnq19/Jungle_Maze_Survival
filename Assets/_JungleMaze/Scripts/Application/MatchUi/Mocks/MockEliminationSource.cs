#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using JungleMaze.Domain.MatchUi;

namespace JungleMaze.App.MatchUi.Mocks
{
    /// <summary>
    /// Dev-only <see cref="IEliminationSource"/>: the F7 panel and tests eliminate the local player with any of the
    /// five causes (AC19) without waiting for SRV-03/WPN-02. Not compiled into Release builds.
    /// </summary>
    public sealed class MockEliminationSource : IEliminationSource
    {
        public const byte KillerNetId = 2;
        public const string KillerName = "Hào";

        public bool IsLocalEliminated { get; private set; }
        public EliminationInfo Current { get; private set; }
        public event Action<EliminationInfo> LocalEliminated;
        public event Action<EliminationInfo> Updated;

        public void Eliminate(in EliminationInfo info)
        {
            if (IsLocalEliminated) return;
            IsLocalEliminated = true;
            Current = info;
            LocalEliminated?.Invoke(info);
        }

        public void Update(in EliminationInfo info)
        {
            Current = info;
            Updated?.Invoke(info);
        }

        public void Reset()
        {
            IsLocalEliminated = false;
            Current = default;
        }

        /// <summary>A full sample per cause: by a player (name, weapon, distance), zone, left, disconnected, unknown.</summary>
        public static EliminationInfo Sample(EliminationCause cause, Guid matchId, uint tick = 900)
        {
            switch (cause)
            {
                case EliminationCause.Player:
                    return new EliminationInfo(matchId, tick, 4, 10, cause, KillerNetId, KillerName, "Nỏ", 6.4f, 312f, 2);
                case EliminationCause.Zone:
                    return new EliminationInfo(matchId, tick, 7, 10, cause, EliminationInfo.NoNetId, null, null, float.NaN, 421f, 0);
                case EliminationCause.LeftMatch:
                    return new EliminationInfo(matchId, tick, 9, 10, cause, EliminationInfo.NoNetId, null, null, float.NaN, 95f, 0);
                case EliminationCause.Disconnected:
                    return new EliminationInfo(matchId, tick, 8, 10, cause, EliminationInfo.NoNetId, null, null, float.NaN, 140f, 1);
                default:
                    return new EliminationInfo(matchId, tick, 6, 10, EliminationCause.Unknown, EliminationInfo.NoNetId, null, null,
                        float.NaN, 200f, -1);
            }
        }
    }
}
#endif
