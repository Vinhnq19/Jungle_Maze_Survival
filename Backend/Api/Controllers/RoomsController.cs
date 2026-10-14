using System.IdentityModel.Tokens.Jwt;
using Api.DTOs;
using Api.Services;
using JungleBackend.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;
[ApiController, Authorize, Route("api/rooms")]
public sealed class RoomsController(RoomControlClient control) : ControllerBase
{
    private string? Owner => User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    private ActionResult Result((int Status, string? Json) response) => response.Json is null
        ? StatusCode(response.Status) : new ContentResult { StatusCode = response.Status, ContentType = "application/json", Content = response.Json };

    /// <summary>Tạo phòng tạm; lưu mã phòng trả về để chia sẻ cho người chơi.</summary>
    /// <remarks>Tên 1–48 ký tự, sức chứa 1–10 (mặc định 10). Trả roomCode gồm 6 ký tự chữ hoa/số, ví dụ K7M2Q9. 400: dữ liệu sai; 409: UserRoomLimitReached (2 phòng bổ sung/user) hoặc RoomLimitReached (5 phòng/server gồm phòng mặc định); 429: quá 5 yêu cầu/phút; 503: điều khiển phòng chưa sẵn sàng. Tổng server tối đa 50 phiên active/pending. Phòng trống 10 phút bị thu hồi; restart mất phòng tạo thêm. Chủ phòng lấy từ JWT sub.</remarks>
    [HttpPost, EnableRateLimiting("room-create")]
    [ProducesResponseType(typeof(RoomView), 201), ProducesResponseType(400), ProducesResponseType(401), ProducesResponseType(409), ProducesResponseType(429), ProducesResponseType(503)]
    public async Task<ActionResult> Create(CreateRoomRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(Owner)) return Unauthorized();
        return Result(await control.Send(HttpMethod.Post, "/internal/rooms", new CreateRoomCommand
            { OwnerId = Owner, Name = request.Name, MaxPlayers = request.MaxPlayers }, ct));
    }
    /// <summary>Liệt kê phòng mặc định và các phòng của bạn; không lộ mã phòng mặc định.</summary>
    [HttpGet, ProducesResponseType(typeof(RoomView[]), 200), ProducesResponseType(401), ProducesResponseType(503)]
    public async Task<ActionResult> List(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(Owner)) return Unauthorized();
        return Result(await control.Send(HttpMethod.Get, "/internal/rooms?ownerId=" + Uri.EscapeDataString(Owner), null, ct));
    }
    /// <summary>Xóa phòng của bạn khi trống; không xóa được phòng mặc định.</summary>
    /// <remarks>403 NotRoomOwner; 404 RoomNotFound; 409 RoomNotEmpty (kể cả pending JWT) hoặc DefaultRoomProtected. 204 khi xóa thành công.</remarks>
    [HttpDelete("{id:guid}"), ProducesResponseType(204), ProducesResponseType(401), ProducesResponseType(403), ProducesResponseType(404), ProducesResponseType(409), ProducesResponseType(503)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(Owner)) return Unauthorized();
        return Result(await control.Send(HttpMethod.Delete, $"/internal/rooms/{id}?ownerId=" + Uri.EscapeDataString(Owner), null, ct));
    }
}
