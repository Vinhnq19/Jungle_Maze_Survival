using System.Net.Http.Json;
using System.Text;
using JungleBackend.Shared;

namespace Api.Services;

/// <summary>Chỉ API gọi HTTP private. User JWT không gửi tới UDP hoặc control service.</summary>
public sealed class RoomControlClient(HttpClient client, IConfiguration configuration)
{
    public async Task<(int Status, string? Json)> Send(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var secret = configuration["ROOM_CONTROL_TOKEN"] ?? "";
        if (Encoding.UTF8.GetByteCount(secret) < 32) return (503, "{\"error\":\"RoomServiceNotConfigured\"}");
        var baseUrl = configuration["RoomControl:BaseUrl"] ?? "http://127.0.0.1:7780";
        using var message = new HttpRequestMessage(method, new Uri(new Uri(baseUrl), path));
        message.Headers.Add("X-Room-Control-Key", secret);
        if (body is not null) message.Content = JsonContent.Create(body);
        try
        {
            using var response = await client.SendAsync(message, ct);
            // Không log JSON trả về: create/list có mã phòng của chủ sở hữu.
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) return (503, "{\"error\":\"RoomServiceMisconfigured\"}");
            return ((int)response.StatusCode, response.StatusCode == System.Net.HttpStatusCode.NoContent
                ? null : await response.Content.ReadAsStringAsync(ct));
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        { return (503, "{\"error\":\"RoomServiceUnavailable\"}"); }
    }

    public async Task<(int Status, string? Json)> RegisterGameTicketAsync(RegisterGameTicketCommand command, CancellationToken ct)
    {
        return await Send(HttpMethod.Post, "/internal/game-tickets", command, ct);
    }
}
