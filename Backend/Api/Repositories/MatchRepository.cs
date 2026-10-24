using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Api.Data;
using Api.Models;
using MongoDB.Driver;

namespace Api.Repositories;

public sealed class MatchRepository(MongoDbContext context) : IMatchRepository
{
    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        // 1. Unique index on MatchId to enforce single insertion per match
        var matchIdIndex = new CreateIndexModel<MatchRecord>(
            Builders<MatchRecord>.IndexKeys.Ascending(m => m.MatchId),
            new CreateIndexOptions { Unique = true, Name = "ux_matches_match_id" });

        // 2. Multikey compound index on participant ProfileKey + EndUtc for history lookups
        var profileHistoryIndex = new CreateIndexModel<MatchRecord>(
            Builders<MatchRecord>.IndexKeys.Ascending("Participants.ProfileKey").Descending(m => m.EndUtc),
            new CreateIndexOptions { Name = "ix_matches_participants_profile_end_utc" });

        await context.Matches.Indexes.CreateManyAsync(new[] { matchIdIndex, profileHistoryIndex }, cancellationToken);
    }

    public async Task<MatchInsertResult> TryInsertMatchAsync(MatchRecord record, CancellationToken cancellationToken = default)
    {
        try
        {
            await context.Matches.InsertOneAsync(record, cancellationToken: cancellationToken);
            return MatchInsertResult.Inserted;
        }
        catch (MongoWriteException error) when (error.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Standalone Mongo atomic idempotence: fetch existing document and compare canonical hash
            var existing = await context.Matches.Find(m => m.MatchId == record.MatchId).FirstOrDefaultAsync(cancellationToken);
            if (existing != null && string.Equals(existing.CanonicalHash, record.CanonicalHash, StringComparison.Ordinal))
            {
                return MatchInsertResult.DuplicateIdentical;
            }

            return MatchInsertResult.ConflictDifferent;
        }
    }

    public Task<MatchRecord?> GetByIdAsync(Guid matchId, CancellationToken cancellationToken = default)
        => context.Matches.Find(m => m.MatchId == matchId).FirstOrDefaultAsync(cancellationToken)!;

    public async Task<(IReadOnlyList<MatchRecord> Items, long Total)> GetMatchesForProfileAsync(
        string profileKey, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var filter = Builders<MatchRecord>.Filter.ElemMatch(m => m.Participants, p => p.ProfileKey == profileKey);
        var total = await context.Matches.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var skip = Math.Max(0, (page - 1) * pageSize);
        var items = await context.Matches.Find(filter)
            .SortByDescending(m => m.EndUtc)
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}
