using System;
using System.Collections.Generic;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.Domain.Lobby;
using JungleMaze.Domain.Network;

namespace JungleMaze.App.Lobby
{
    /// <summary>
    /// N1: lobby state as the server sent it. Live: <c>MatchSessionLobbySource</c>; dev: <c>MockLobbySource</c>.
    /// UI-02 polls it every frame and never advances the countdown itself (AC21).
    /// </summary>
    public interface ILobbySource
    {
        bool HasPhase { get; }
        Guid MatchId { get; }
        MatchPhase Phase { get; }
        int PlayerCount { get; }
        int MinPlayers { get; }
        int MaxPlayers { get; }
        uint PhaseEndTick { get; }
        uint ServerTick { get; }
        IReadOnlyList<LobbySlot> Slots { get; }
        byte OwnNetId { get; }
        string RoomCode { get; }

        /// <summary>The server reports per-player ready flags (off until SRV-03 ships it, M2).</summary>
        bool ReadySupported { get; }

        /// <summary>Map version/hash error from the scene adapter (H1); null when none or not exposed.</summary>
        string LoadError { get; }
    }

    /// <summary>N2 in a UI-friendly shape. Keyed by NetId; names are display only (AC30).</summary>
    public sealed class MatchResultView
    {
        public Guid MatchId;
        public MatchEndReason Reason;
        public byte WinnerNetId, ParticipantCount, OwnNetId = 255;
        public MatchParticipantState[] Participants = Array.Empty<MatchParticipantState>();
    }

    /// <summary>N2: the final result of the current match.</summary>
    public interface IMatchResultSource
    {
        bool HasResult { get; }
        MatchResultView Current { get; }
    }

    /// <summary>N3: connection state, reject reason, resume availability.</summary>
    public interface IConnectionStateSource
    {
        NetworkState State { get; }
        DisconnectReason LastReason { get; }

        /// <summary>"UDP", "WebSocket" or "Auto" when the transport is not known yet (T-5).</summary>
        string Step { get; }

        /// <summary>Last measured RTT in ms; negative when nothing was measured (M6: no fake numbers).</summary>
        int RttMs { get; }

        bool CanResume { get; }
    }

    /// <summary>N4: everything UI-02 asks of the connection/server. Callers send each at most once per press.</summary>
    public interface ILobbyCommands
    {
        void Join(string roomCode, string displayName);
        void Cancel();
        void Leave();
        void Ready();
        void Replay();
        void Resume();
    }

    /// <summary>Countdown = ceil((PhaseEndTick − ServerTick) / 30); 0 when the end is not ahead (AC21).</summary>
    public static class LobbyCountdown
    {
        public const int TickRate = 30;

        public static int Seconds(uint phaseEndTick, uint serverTick)
        {
            var left = unchecked((int)(phaseEndTick - serverTick));
            return left <= 0 ? 0 : (left + TickRate - 1) / TickRate;
        }
    }
}
