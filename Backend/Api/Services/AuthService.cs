using Api.DTOs;
using Api.Models;
using Api.Repositories;
using Microsoft.AspNetCore.Identity;

namespace Api.Services;

public sealed class AuthService(IUserRepository users, IPasswordHasher<User> passwordHasher, TokenService tokens)
{
    public async Task<UserResponse?> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var user = new User
        {
            Username = request.Username,
            NormalizedUsername = request.Username.ToUpperInvariant(),
            // BSON lưu thời gian tới millisecond: response đăng ký và /me phải cùng giá trị.
            CreatedAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).UtcDateTime
        };

        // PasswordHasher sinh salt và hash; không lưu password thô, không tự dùng SHA256 đơn thuần.
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        if (!await users.TryCreateAsync(user, cancellationToken)) return null;
        return ToResponse(user);
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByNormalizedUsernameAsync(request.Username.ToUpperInvariant(), cancellationToken);
        if (user is null) return null;

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed) return null;

        // Nếu hasher nâng cấu hình bảo mật, nâng hash sau lần login thành công.
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            var newHash = passwordHasher.HashPassword(user, request.Password);
            await users.UpdatePasswordHashAsync(user.Id, user.PasswordHash, newHash, cancellationToken);
        }

        return tokens.CreateToken(user);
    }

    public async Task<UserResponse?> GetUserAsync(string id, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(id, cancellationToken);
        return user is null ? null : ToResponse(user);
    }

    private static UserResponse ToResponse(User user) => new(user.Id, user.Username, user.CreatedAtUtc);
}
