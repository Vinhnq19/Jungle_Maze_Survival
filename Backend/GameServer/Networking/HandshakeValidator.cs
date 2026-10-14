using System.IdentityModel.Tokens.Jwt;
using System.Text;
using GameServer.Configuration;
using JungleBackend.Protocol;
using Microsoft.IdentityModel.Tokens;

namespace GameServer.Networking;

/// <summary>Kiểm tra quyền vào phòng; không gọi API/MongoDB trong callback hoặc Tick.</summary>
public sealed class HandshakeValidator
{
    private readonly ServerOptions _options;
    private readonly JwtSecurityTokenHandler _handler = new() { MapInboundClaims = false, MaximumTokenSizeInBytes = NetworkProtocol.MaxAuthBytes };
    private readonly TokenValidationParameters? _jwt;

    public HandshakeValidator(ServerOptions options)
    {
        options.Validate();
        _options = options;
        if (options.AuthMode == "Jwt")
        {
            var settings = options.Jwt!;
            // Chốt cùng HS256/issuer/audience/clock skew với API; ReadJwtToken không đủ để xác thực.
            _jwt = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                ValidateIssuer = true, ValidIssuer = settings.Issuer,
                ValidateAudience = true, ValidAudience = settings.Audience,
                ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }, ClockSkew = TimeSpan.FromSeconds(30)
            };
        }
    }

    public string? ValidateRequest(JoinRequest request)
    {
        if (request.Version != NetworkProtocol.Version) return "UnsupportedVersion";
        if (request.Mode != _options.Mode) return "AuthModeMismatch";
        if (string.IsNullOrWhiteSpace(request.RoomCode) || request.RoomCode.Length > NetworkProtocol.MaxRoomChars) return "InvalidRoomCode";
        if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Length > NetworkProtocol.MaxNameChars)
            return "InvalidDisplayName";
        return null;
    }

    public bool TryValidateToken(string token, out string playerId)
    {
        playerId = string.Empty;
        if (_jwt is null || string.IsNullOrWhiteSpace(token) || token.Length > NetworkProtocol.MaxTokenChars) return false;
        try
        {
            var principal = _handler.ValidateToken(token, _jwt, out _);
            var subjects = principal.FindAll(JwtRegisteredClaimNames.Sub).ToArray();
            if (subjects.Length != 1 || string.IsNullOrWhiteSpace(subjects[0].Value) || subjects[0].Value.Length > 64) return false;
            // UserId lấy từ sub đã kiểm tra chữ ký; bỏ qua mọi ID mà client tự khai.
            playerId = subjects[0].Value;
            return true;
        }
        catch (Exception error) when (error is SecurityTokenException or ArgumentException)
        {
            // Lỗi cụ thể có thể chứa token/claim. Chỉ trả InvalidToken, không in exception ra log.
            return false;
        }
    }
}
