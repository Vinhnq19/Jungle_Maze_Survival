using Api.DTOs;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

// ApiController tự trả 400 khi DTO thiếu dữ liệu, sai độ dài hoặc JSON không hợp lệ.
[ApiController]
[AllowAnonymous]
[Route("api/auth")]
[EnableRateLimiting("public-auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    /// <summary>Đăng ký tài khoản demo.</summary>
    /// <remarks>Tên dài 3–32 ký tự sau Trim; password dài 8–128 ký tự. Register không tự login.</remarks>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<UserResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var user = await auth.RegisterAsync(request, cancellationToken);
        if (user is null)
            return Conflict(new { error = "UsernameAlreadyExists", message = "Tên tài khoản đã tồn tại." });

        return Created("/api/me", user);
    }

    /// <summary>Đăng nhập và lấy accessToken để dùng nút Authorize.</summary>
    /// <remarks>Copy accessToken trong response 200. Password và token không được ghi vào log.</remarks>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var login = await auth.LoginAsync(request, cancellationToken);
        // Cùng một lỗi cho user không tồn tại và password sai; không tiết lộ tài khoản nào đã đăng ký.
        if (login is null)
            return Unauthorized(new { error = "InvalidCredentials", message = "Tài khoản hoặc mật khẩu không đúng." });
        return Ok(login);
    }
}
