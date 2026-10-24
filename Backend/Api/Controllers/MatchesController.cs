using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Api.Models;
using Api.Repositories;
using Api.Services;
using JungleBackend.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace Api.Controllers;

[ApiController]
public sealed class MatchesController(
    IMatchRepository matchRepository,
    IdentityResolver identityResolver,
    IConfiguration configuration) : ControllerBase
{
    /// <summary>Lịch sử trận đấu của tài khoản/khách hiện tại.</summary>
    /// <remarks>
    /// Xác thực qua JWT Bearer (account:sub) HOẶC X-Guest-Credential (guest:uuid).
    /// Chỉ trả về bản ghi của chính chủ sở hữu đã xác minh. Mặc định page=1, pageSize=20 (tối đa 100).
    /// </remarks>
    [HttpGet("api/me/matches")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<MyMatchHistoryItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<MyMatchHistoryItem>>> GetMyMatches(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
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

        if (page < 1 || page > 1000000) return BadRequest(new { error="InvalidPage" });
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var (matches, total) = await matchRepository.GetMatchesForProfileAsync(
            identity.ProfileKey, page, pageSize, cancellationToken);

        var items = new List<MyMatchHistoryItem>(matches.Count);
        foreach (var match in matches)
        {
            // Trích xuất DUY NHẤT bản ghi của chủ sở hữu đã xác minh
            var participant = match.Participants.FirstOrDefault(p =>
                string.Equals(p.ProfileKey, identity.ProfileKey, StringComparison.Ordinal));

            if (participant != null)
            {
                items.Add(new MyMatchHistoryItem(
                    MatchId: match.MatchId,
                    DisplayName: participant.DisplayName,
                    Rank: participant.Rank,
                    Kills: participant.Kills,
                    SurvivalSeconds: participant.SurvivalSeconds,
                    EndedAt: new DateTimeOffset(match.EndUtc, TimeSpan.Zero),
                    Reason: match.Reason
                ));
            }
        }

        return Ok(new PagedResult<MyMatchHistoryItem>(items, page, pageSize, total));
    }

    /// <summary>Bảng xếp hạng công khai cho một trận đấu đã hoàn thành.</summary>
    /// <remarks>
    /// Không cần xác thực. Trả về kết quả trận đấu đã được làm sạch (không lộ profile key hay credential).
    /// </remarks>
    [HttpGet("api/matches/{matchId:guid}/leaderboard")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MatchLeaderboardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<MatchLeaderboardResponse>> GetLeaderboard(
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var match = await matchRepository.GetByIdAsync(matchId, cancellationToken);
            if (match == null)
            {
                return NotFound(new { error = "MatchNotFound", message = "Trận đấu không tồn tại." });
            }

            var players = match.Participants
                .OrderBy(p => p.Rank)
                .Select(p => new SanitizedMatchParticipant(
                    PlayerSessionId: p.PlayerSessionId,
                    DisplayName: p.DisplayName,
                    Rank: p.Rank,
                    Kills: p.Kills,
                    SurvivalSeconds: p.SurvivalSeconds
                ))
                .ToList();

            var response = new MatchLeaderboardResponse(
                MatchId: match.MatchId,
                RoomId: match.RoomId,
                ConfigVersion: match.ConfigVersion,
                StartUtc: new DateTimeOffset(match.StartUtc, TimeSpan.Zero),
                EndUtc: new DateTimeOffset(match.EndUtc, TimeSpan.Zero),
                Reason: match.Reason,
                Players: players
            );

            return Ok(response);
        }
        catch (MongoException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "DatabaseUnavailable", message = "Cơ sở dữ liệu tạm thời không khả dụng." });
        }
    }

    /// <summary>Endpoint nội bộ nhận kết quả trận đấu từ game server outbox.</summary>
    /// <remarks>
    /// Xác thực bằng MATCH_RESULT_TOKEN (>= 32 bytes UTF-8) qua header X-Match-Result-Token.
    /// So sánh thời gian hằng số (constant-time). Không chấp nhận JWT hay guest proof.
    /// </remarks>
    [HttpPost("internal/match-results")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SubmitMatchResult(
        [FromBody] MatchResultSubmission submission,
        CancellationToken cancellationToken = default)
    {
        // 1. Authenticate using dedicated MATCH_RESULT_TOKEN
        var configuredToken = configuration["MATCH_RESULT_TOKEN"] ?? "";
        if (Encoding.UTF8.GetByteCount(configuredToken) < 32)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "MatchResultServiceNotConfigured" });
        }

        var suppliedToken = Request.Headers["X-Match-Result-Token"].ToString();
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredToken));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(suppliedToken ?? ""));

        if (string.IsNullOrEmpty(suppliedToken) || suppliedToken.Length > 256 ||
            !CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash))
        {
            return Unauthorized(new { error = "InvalidMatchResultToken" });
        }

        // 2. Validate server-issued submission bounds
        if (!Guid.TryParse(submission.RoomId, out _) || string.IsNullOrEmpty(submission.ConfigVersion) || submission.ConfigVersion.Length > 32 || submission.Reason is not ("LastAlive" or "Tie" or "Timeout" or "Cancelled"))
            return BadRequest(new { error = "InvalidMatchMetadata" });
        if (submission.MatchId == Guid.Empty)
            return BadRequest(new { error = "InvalidMatchId" });
        if (string.IsNullOrWhiteSpace(submission.ConfigVersion))
            return BadRequest(new { error = "InvalidConfigVersion" });
        if (submission.EndUtc < submission.StartUtc)
            return BadRequest(new { error = "InvalidTimeRange" });
        if (submission.Participants == null || submission.Participants.Count == 0 || submission.Participants.Count > 10)
            return BadRequest(new { error = "InvalidParticipantCount", message = "Max 10 players allowed." });

        var distinctSessions = submission.Participants.Select(p => p?.PlayerSessionId).Distinct().Count();
        if (distinctSessions != submission.Participants.Count)
            return BadRequest(new { error = "DuplicatePlayerSessionId" });

        foreach (var p in submission.Participants)
        {
            if (p is null || string.IsNullOrWhiteSpace(p.PlayerSessionId) || p.PlayerSessionId.Length > 64 || string.IsNullOrWhiteSpace(p.DisplayName) || p.DisplayName.Length > 32 || p.Rank > submission.Participants.Count || p.Kills < 0 || p.Kills > 9 || p.ProfileKey is { Length: > 80 }) return BadRequest(new { error="InvalidParticipant" });
            if (p.Rank < 1) return BadRequest(new { error = "InvalidRank" });
            if (p.SurvivalSeconds < 0 || double.IsNaN(p.SurvivalSeconds) || double.IsInfinity(p.SurvivalSeconds))
                return BadRequest(new { error = "InvalidSurvivalSeconds" });
        }

        // 3. Normalized canonical representation & canonical hash computation
        var canonicalHash = MatchCanonicalHasher.ComputeCanonicalHash(submission);

        var record = new MatchRecord
        {
            Id = submission.MatchId.ToString("D"),
            MatchId = submission.MatchId,
            CanonicalHash = canonicalHash,
            RoomId = submission.RoomId ?? "",
            ConfigVersion = submission.ConfigVersion ?? "",
            StartUtc = submission.StartUtc.UtcDateTime,
            EndUtc = submission.EndUtc.UtcDateTime,
            Reason = submission.Reason ?? "",
            Participants = submission.Participants.Select(p => new MatchParticipantRecord
            {
                PlayerSessionId = p.PlayerSessionId,
                DisplayName = p.DisplayName,
                Rank = p.Rank,
                Kills = p.Kills,
                SurvivalSeconds = p.SurvivalSeconds,
                ProfileKey = p.ProfileKey
            }).ToList()
        };

        // 4. Atomic Mongo insert
        var insertResult = await matchRepository.TryInsertMatchAsync(record, cancellationToken);
        return insertResult switch
        {
            MatchInsertResult.Inserted => StatusCode(StatusCodes.Status201Created, new { status = "created", matchId = submission.MatchId }),
            MatchInsertResult.DuplicateIdentical => Ok(new { status = "idempotent", matchId = submission.MatchId }),
            MatchInsertResult.ConflictDifferent => Conflict(new { error = "MatchResultConflict", message = "Same MatchId with conflicting payload." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
