using System.Text;

namespace GameServer.Configuration;

/// <summary>Đọc cùng biến JWT với API; chỉ yêu cầu khi AUTH_MODE=Jwt.</summary>
public sealed class JwtSettings
{
    public string Issuer { get; init; } = "JungleMaze.Local";
    public string Audience { get; init; } = "JungleMaze.Game";
    public string SigningKey { get; init; } = string.Empty;

    public static JwtSettings FromEnvironment() => new()
    {
        Issuer = Environment.GetEnvironmentVariable("Jwt__Issuer") ?? "JungleMaze.Local",
        Audience = Environment.GetEnvironmentVariable("Jwt__Audience") ?? "JungleMaze.Game",
        SigningKey = Environment.GetEnvironmentVariable("Jwt__SigningKey") ?? string.Empty
    };

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience)
            || string.IsNullOrWhiteSpace(SigningKey) || Encoding.UTF8.GetByteCount(SigningKey) < 32)
            throw new ArgumentException("Jwt cần issuer/audience và Jwt__SigningKey ít nhất 32 UTF-8 byte, khớp API.");
    }
}
