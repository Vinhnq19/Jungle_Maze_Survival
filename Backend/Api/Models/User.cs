using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace Api.Models;

[BsonIgnoreExtraElements]
public sealed class User
{
    // MongoDB dùng Id làm _id; giữ dạng Guid string để API và GameServer dùng chung.
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Username { get; set; } = string.Empty;

    // Tìm tài khoản bằng tên đã Trim + ToUpperInvariant, không phân biệt hoa/thường.
    [JsonIgnore]
    public string NormalizedUsername { get; set; } = string.Empty;

    // Chỉ lưu hash. JsonIgnore là lớp bảo vệ thêm nếu vô tình serialize model.
    [JsonIgnore]
    public string PasswordHash { get; set; } = string.Empty;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
