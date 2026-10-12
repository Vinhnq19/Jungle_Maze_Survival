using System.Net.Http.Json;
using NetSmokeClient.Configuration;

namespace NetSmokeClient.Services;

public static class AccountLogin
{
    private sealed class LoginResponse
    {
        public string AccessToken { get; init; } = string.Empty;
    }

    public static async Task<string> LoginAsync(ClientOptions options, string username, string password,
        CancellationToken cancellation)
    {
        var baseUrl = options.ValidateApiUrl();
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        var endpoint = new Uri(baseUrl.AbsoluteUri.TrimEnd('/') + "/api/auth/login");
        // HTTPS/VPN được kiểm tra trước POST; không đưa password/token vào log hoặc URL.
        using var response = await http.PostAsJsonAsync(endpoint, new { username, password }, cancellation);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"API login trả HTTP {(int)response.StatusCode}; không kết nối UDP và không fallback RoomCode.");
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: cancellation);
        if (result is null || string.IsNullOrWhiteSpace(result.AccessToken) || result.AccessToken.Length > 2048)
            throw new InvalidOperationException("API không trả JWT hợp lệ theo hợp đồng client.");
        return result.AccessToken; // Token chỉ nằm trong memory của lần chạy.
    }
}
