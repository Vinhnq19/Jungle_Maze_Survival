#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace JungleBackend.Shared;

/// <summary>
/// Immutable DTO for registering one-use game tickets via private room-control API.
/// </summary>
public sealed record RegisterGameTicketCommand(
    string Ticket,
    Guid AttemptId,
    string RoomCode,
    string ProfileKey,
    DateTimeOffset ExpiresUtc
);

/// <summary>
/// Client request to obtain a one-use game ticket.
/// </summary>
public sealed record GameTicketRequest(
    Guid AttemptId,
    string RoomCode
);

/// <summary>
/// Client response containing issued one-use game ticket and TTL.
/// </summary>
public sealed record GameTicketResponse(
    string Ticket,
    DateTimeOffset ExpiresUtc
);

/// <summary>
/// Response for guest profile creation. Credential is 256-bit opaque crypto secret.
/// </summary>
public sealed record GuestResponse(
    string GuestProfileId,
    string Credential
);

/// <summary>
/// Immutable participant submission payload from server to API.
/// </summary>
public sealed record MatchParticipantSubmission(
    string PlayerSessionId,
    string DisplayName,
    int Rank,
    int Kills,
    double SurvivalSeconds,
    string? ProfileKey = null
);

/// <summary>
/// Immutable match result submission from game server outbox to persistence API.
/// </summary>
public sealed record MatchResultSubmission(
    Guid MatchId,
    string RoomId,
    string ConfigVersion,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string Reason,
    IReadOnlyList<MatchParticipantSubmission> Participants
);

/// <summary>
/// Sanitized participant in public leaderboard (no credentials, no profile keys).
/// </summary>
public sealed record SanitizedMatchParticipant(
    string PlayerSessionId,
    string DisplayName,
    int Rank,
    int Kills,
    double SurvivalSeconds
);

/// <summary>
/// Public match result and leaderboard view.
/// </summary>
public sealed record MatchLeaderboardResponse(
    Guid MatchId,
    string RoomId,
    string ConfigVersion,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string Reason,
    IReadOnlyList<SanitizedMatchParticipant> Players
);

/// <summary>
/// Verified owner's personal match history record.
/// </summary>
public sealed record MyMatchHistoryItem(
    Guid MatchId,
    string DisplayName,
    int Rank,
    int Kills,
    double SurvivalSeconds,
    DateTimeOffset EndedAt,
    string Reason
);

/// <summary>
/// Generic paged response envelope.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long Total
);

/// <summary>
/// Deterministic canonical hasher for match results. Normalizes participant ordering and formatting
/// before hashing so that duplicate identical results yield identical hashes, independent of JSON property ordering.
/// </summary>
public static class MatchCanonicalHasher
{
    public static string ComputeCanonicalHash(MatchResultSubmission submission) =>
        ComputeCanonicalHash(
            submission.MatchId,
            submission.RoomId,
            submission.ConfigVersion,
            submission.StartUtc,
            submission.EndUtc,
            submission.Reason,
            submission.Participants);

    public static string ComputeCanonicalHash(
        Guid matchId,
        string roomId,
        string configVersion,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        string reason,
        IEnumerable<MatchParticipantSubmission> participants)
    {
        // Length-prefixed binary canonical form: names may contain pipes/newlines without hash ambiguity.
        // Preserve full time/double precision so a subtly changed payload is still a conflict.
        using var stream = new System.IO.MemoryStream();
        using (var writer = new System.IO.BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write(matchId.ToByteArray()); writer.Write(roomId ?? ""); writer.Write(configVersion ?? "");
            writer.Write(startUtc.UtcTicks); writer.Write(endUtc.UtcTicks); writer.Write(reason ?? "");
            var ordered = participants.OrderBy(p => p.PlayerSessionId, StringComparer.Ordinal).ToArray();
            writer.Write(ordered.Length);
            foreach (var p in ordered)
            {
                writer.Write(p.PlayerSessionId); writer.Write(p.DisplayName); writer.Write(p.Rank); writer.Write(p.Kills);
                writer.Write(p.SurvivalSeconds == 0 ? 0 : p.SurvivalSeconds);
                writer.Write(p.ProfileKey != null); if (p.ProfileKey != null) writer.Write(p.ProfileKey);
            }
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }
}
