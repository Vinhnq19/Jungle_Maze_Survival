using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace JungleMaze.App.Ports
{
    /// <summary>Connection-attempt identity and explicit resume; durable account credentials stay outside gameplay packets.</summary>
    public interface IMatchConnection
    {
        Guid AttemptId { get; }
        ConnectRequest LastRequest { get; }
        void Resume(Guid proof);
    }

    public sealed class PlayerMatchHistory
    {
        public Guid MatchId { get; set; }
        public string DisplayName { get; set; }
        public int Rank { get; set; }
        public int Kills { get; set; }
        public double SurvivalSeconds { get; set; }
        public DateTimeOffset EndedAt { get; set; }
        public string Reason { get; set; }
    }

    public interface IPlayerHistory : IDisposable
    {
        string Status { get; }
        bool IsReady { get; }
        IReadOnlyList<PlayerMatchHistory> Rows { get; }
        Task<bool> EnsureGuestAsync(string baseUrl, CancellationToken cancellation = default);
        Task<string> TicketAsync(Guid attemptId, string roomCode, string accountToken = null, CancellationToken cancellation = default);
        Task RefreshHistoryAsync(string accountToken = null, CancellationToken cancellation = default);
    }
}
