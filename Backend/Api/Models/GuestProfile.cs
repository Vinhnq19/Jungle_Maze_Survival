using MongoDB.Bson.Serialization.Attributes;

namespace Api.Models;

[BsonIgnoreExtraElements]
public sealed class GuestProfile
{
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString("D");

    /// <summary>
    /// Hex-encoded SHA-256 hash of the 256-bit opaque credential.
    /// Plaintext credential is never stored in DB or logged.
    /// </summary>
    public string CredentialHash { get; set; } = string.Empty;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
