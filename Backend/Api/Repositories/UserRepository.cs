using Api.Data;
using Api.Models;
using MongoDB.Driver;

namespace Api.Repositories;

public sealed class UserRepository(MongoDbContext context) : IUserRepository
{
    public Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        // Unique index xử lý cả hai request đăng ký đồng thời, không chỉ kiểm tra trước insert.
        var index = new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(user => user.NormalizedUsername),
            new CreateIndexOptions { Unique = true, Name = "ux_users_normalized_username" });
        return context.Users.Indexes.CreateOneAsync(index, cancellationToken: cancellationToken);
    }

    public async Task<bool> TryCreateAsync(User user, CancellationToken cancellationToken = default)
    {
        try
        {
            await context.Users.InsertOneAsync(user, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException error) when (error.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Service/controller chuyển kết quả này thành 409; lỗi DB khác vẫn được báo đúng.
            return false;
        }
    }

    public Task<User?> FindByNormalizedUsernameAsync(string username, CancellationToken cancellationToken = default)
        => context.Users.Find(user => user.NormalizedUsername == username)
            .FirstOrDefaultAsync(cancellationToken)!;

    public Task<User?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => context.Users.Find(user => user.Id == id).FirstOrDefaultAsync(cancellationToken)!;

    public Task UpdatePasswordHashAsync(string id, string previousHash, string newHash,
        CancellationToken cancellationToken = default)
    {
        // Chỉ thay hash nếu nó chưa đổi bởi một request khác trong lúc đăng nhập.
        var filter = Builders<User>.Filter.Eq(user => user.Id, id)
            & Builders<User>.Filter.Eq(user => user.PasswordHash, previousHash);
        return context.Users.UpdateOneAsync(filter,
            Builders<User>.Update.Set(user => user.PasswordHash, newHash), cancellationToken: cancellationToken);
    }
}
