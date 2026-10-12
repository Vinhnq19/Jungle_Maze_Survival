using Api.Models;

namespace Api.Repositories;

public interface IUserRepository
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);
    Task<bool> TryCreateAsync(User user, CancellationToken cancellationToken = default);
    Task<User?> FindByNormalizedUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<User?> FindByIdAsync(string id, CancellationToken cancellationToken = default);
    Task UpdatePasswordHashAsync(string id, string previousHash, string newHash,
        CancellationToken cancellationToken = default);
}
