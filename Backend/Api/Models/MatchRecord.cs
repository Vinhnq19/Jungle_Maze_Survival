using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace Api.Models;

[BsonIgnoreExtraElements]
public sealed class MatchParticipantRecord
{
    public string PlayerSessionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int Rank { get; set; }
    public int Kills { get; set; }
    public double SurvivalSeconds { get; set; }
    public string? ProfileKey { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class MatchRecord
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonGuidRepresentation(MongoDB.Bson.GuidRepresentation.Standard)]
    public Guid MatchId { get; set; }

    /// <summary>
    /// Normalized canonical SHA-256 hash. Used to detect identical replay (200 OK) vs payload conflict (409 Conflict).
    /// </summary>
    public string CanonicalHash { get; set; } = string.Empty;

    public string RoomId { get; set; } = string.Empty;
    public string ConfigVersion { get; set; } = string.Empty;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime StartUtc { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime EndUtc { get; set; }

    public string Reason { get; set; } = string.Empty;

    public List<MatchParticipantRecord> Participants { get; set; } = new();
}
