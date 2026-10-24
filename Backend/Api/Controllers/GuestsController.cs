using System.Threading;
using System.Threading.Tasks;
using Api.Services;
using JungleBackend.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/guests")]
[EnableRateLimiting("guest-create")]
public sealed class GuestsController(GuestService guestService, IdentityResolver identityResolver) : ControllerBase
{
    /// <summary>Tạo tài khoản khách (Guest Profile).</summary>
    /// <remarks>
    /// Trả về guestProfileId và credential 256-bit ngẫu nhiên dùng làm proof trong header X-Guest-Credential.
    /// DB chỉ lưu hash SHA-256 của credential, không lưu plaintext. Bắt buộc HTTPS ngoại trừ HTTP loopback cục bộ.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(GuestResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<GuestResponse>> CreateGuest(CancellationToken cancellationToken)
    {
        if (!identityResolver.IsSecureOrLoopback(HttpContext))
        {
            return BadRequest(new { error = "HttpsRequired", message = "HTTPS is required except on loopback." });
        }

        var response = await guestService.CreateGuestAsync(cancellationToken);
        return Created($"/api/me/matches", response);
    }
}
