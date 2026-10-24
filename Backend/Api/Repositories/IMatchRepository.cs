using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Api.Models;

namespace Api.Repositories;

public enum MatchInsertResult
{
    Inserted,
    DuplicateIdentical,
    ConflictDifferent
}

public interface IMatchRepository
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);
    Task<MatchInsertResult> TryInsertMatchAsync(MatchRecord record, CancellationToken cancellationToken = default);
    Task<MatchRecord?> GetByIdAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<MatchRecord> Items, long Total)> GetMatchesForProfileAsync(string profileKey, int page, int pageSize, CancellationToken cancellationToken = default);
}
