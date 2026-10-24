using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Api.Services;
using JungleBackend.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/game-tickets")]
public sealed class GameTicketsController(
    IdentityResolver identityResolver,
    RoomControlClient control) : ControllerBase
{
    /// <summary>Cấp vé vào game 1 lần (One-use Game Ticket) có thời hạn 30 giây.</summary>
    /// <remarks>
    /// Xác thực qua JWT Bearer (account:sub) HOẶC header X-Guest-Credential (guest:uuid).
    /// Vé được đăng ký đồng bộ với GameServer trước khi trả về client.
    /// Không gửi JWT hoặc credential dài hạn qua kết nối gameplay UDP.
    /// </remarks>
    [HttpPost]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("game-ticket")]
    [ProducesResponseType(typeof(GameTicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GameTicketResponse>> IssueTicket(
        [FromBody] GameTicketRequest request,
        CancellationToken cancellationToken)
    {
        var identity = await identityResolver.ResolveAsync(HttpContext, cancellationToken);
        if (identity.Error == "HttpsRequired")
        {
            return BadRequest(new { error = "HttpsRequired", message = "HTTPS is required except on loopback." });
        }
        if (!string.IsNullOrEmpty(identity.Error))
        {
            return Unauthorized(new { error = identity.Error });
        }

        if (request.AttemptId == Guid.Empty)
        {
            return BadRequest(new { error = "InvalidAttemptId", message = "AttemptId must not be empty." });
        }

        var roomCode = (request.RoomCode ?? "").Trim();
        if (roomCode.Length == 0 || roomCode.Length > 64)
        {
            return BadRequest(new { error = "InvalidRoomCode", message = "RoomCode must not be empty." });
        }

        // Random 32 bytes cryptographically secure ticket (64 hex characters)
        var ticketBytes = RandomNumberGenerator.GetBytes(32);
        var ticket = Convert.ToHexString(ticketBytes).ToLowerInvariant();
        var expiresUtc = DateTimeOffset.UtcNow.AddSeconds(30);

        var command = new RegisterGameTicketCommand(
            Ticket: ticket,
            AttemptId: request.AttemptId,
            RoomCode: roomCode,
            ProfileKey: identity.ProfileKey,
            ExpiresUtc: expiresUtc
        );

        // Register BEFORE response via private authenticated room-control API
        var (status, json) = await control.RegisterGameTicketAsync(command, cancellationToken);
        if (status == 404)
        {
            return NotFound(new { error = "RoomNotFound", message = "Phòng không tồn tại." });
        }
        if (status != 200)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "TicketRegistrationFailed", message = "GameServer unavailable or ticket queue busy." });
        }

        return Ok(new GameTicketResponse(ticket, expiresUtc));
    }
}
