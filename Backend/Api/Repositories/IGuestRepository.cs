using System.Threading;
using System.Threading.Tasks;
using Api.Models;

namespace Api.Repositories;

public interface IGuestRepository
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);
    Task CreateAsync(GuestProfile guest, CancellationToken cancellationToken = default);
    Task<GuestProfile?> FindByCredentialHashAsync(string hash, CancellationToken cancellationToken = default);
}
