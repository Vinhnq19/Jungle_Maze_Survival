using Api.Models;
using MongoDB.Driver;

namespace Api.Data;

public sealed class MongoDbContext
{
    public IMongoDatabase Database { get; }
    public IMongoCollection<User> Users { get; }

    public MongoDbContext(IMongoClient client, IConfiguration configuration)
    {
        var databaseName = configuration["Mongo:Database"] ?? "jungle_maze";
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("Mongo:Database must not be empty.");

        // Tái sử dụng MongoClient singleton và connection pool; không tạo client cho mỗi request.
        Database = client.GetDatabase(databaseName);
        Users = Database.GetCollection<User>("users");
    }
}
