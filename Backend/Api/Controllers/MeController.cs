using System.IdentityModel.Tokens.Jwt;
using Api.DTOs;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me")]
public sealed class MeController(AuthService auth) : ControllerBase
{
    /// <summary>Xem tài khoản của JWT hiện tại.</summary>
    /// <remarks>Bấm Authorize và dán accessToken trước khi Execute. Thiếu/sai/hết hạn token trả 401.</remarks>
    [HttpGet]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> Get(CancellationToken cancellationToken)
    {
        // Chỉ đọc sub sau khi middleware đã xác minh JWT; không lấy ID từ query/client.
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var user = await auth.GetUserAsync(userId, cancellationToken);
        if (user is null) return Unauthorized();
        return Ok(user);
    }
}
