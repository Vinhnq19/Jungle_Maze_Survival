using System.Text;

namespace Api.Services;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "JungleMaze.Local";
    public string Audience { get; set; } = "JungleMaze.Game";
    public string SigningKey { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
            throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience are required.");

        // HS256 cần ít nhất 256 bit. Không ghi key trong appsettings/Git hay thông báo lỗi.
        if (string.IsNullOrWhiteSpace(SigningKey) || Encoding.UTF8.GetByteCount(SigningKey) < 32)
            throw new InvalidOperationException("Set Jwt__SigningKey to a random secret of at least 32 UTF-8 bytes.");

        if (ExpiryMinutes is < 1 or > 1440)
            throw new InvalidOperationException("Jwt:ExpiryMinutes must be between 1 and 1440.");
    }
}
