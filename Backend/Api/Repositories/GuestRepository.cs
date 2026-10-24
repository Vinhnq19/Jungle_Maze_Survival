using System.Threading;
using System.Threading.Tasks;
using Api.Data;
using Api.Models;
using MongoDB.Driver;

namespace Api.Repositories;

public sealed class GuestRepository(MongoDbContext context) : IGuestRepository
{
    public Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var index = new CreateIndexModel<GuestProfile>(
            Builders<GuestProfile>.IndexKeys.Ascending(g => g.CredentialHash),
            new CreateIndexOptions { Unique = true, Name = "ux_guests_credential_hash" });
        return context.Guests.Indexes.CreateOneAsync(index, cancellationToken: cancellationToken);
    }

    public Task CreateAsync(GuestProfile guest, CancellationToken cancellationToken = default)
        => context.Guests.InsertOneAsync(guest, cancellationToken: cancellationToken);

    public Task<GuestProfile?> FindByCredentialHashAsync(string hash, CancellationToken cancellationToken = default)
        => context.Guests.Find(g => g.CredentialHash == hash).FirstOrDefaultAsync(cancellationToken)!;
}
