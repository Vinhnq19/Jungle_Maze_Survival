using System.Text.Json;
using System.Text.Json.Serialization;
using JungleBackend.Protocol;

namespace NetSmokeClient.Configuration;

/// <summary>Endpoint ngoài chương trình: đổi IP LAN/VPN không cần build lại client.</summary>
public sealed class ClientOptions
{
    [JsonRequired] public string Mode { get; init; } = string.Empty;
    public string? ApiBaseUrl { get; init; }
    [JsonRequired] public string GameServerHost { get; init; } = string.Empty;
    [JsonRequired] public int GameServerPort { get; init; }
    [JsonRequired] public string AuthMode { get; init; } = string.Empty;
    [JsonRequired] public int ProtocolVersion { get; init; }
    [JsonRequired] public int ConnectTimeoutMs { get; init; }

    public static ClientOptions Load(string path)
    {
        var options = JsonSerializer.Deserialize<ClientOptions>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        }) ?? throw new ArgumentException("Config JSON không được null.");
        if (options.Mode is not ("Local" or "LAN" or "Online"))
            throw new ArgumentException("mode phải là Local, LAN hoặc Online.");
        if (string.IsNullOrWhiteSpace(options.GameServerHost)
            || options.GameServerHost != options.GameServerHost.Trim()
            || Uri.CheckHostName(options.GameServerHost) == UriHostNameType.Unknown)
            throw new ArgumentException("gameServerHost phải là IP/hostname, không có http:// hoặc port.");
        if (options.GameServerPort is < 1 or > 65535)
            throw new ArgumentException("gameServerPort phải nằm trong [1, 65535].");
        if (options.AuthMode is not ("RoomCode" or "Jwt"))
            throw new ArgumentException("authMode phải là RoomCode hoặc Jwt, khớp server.");
        if (options.ProtocolVersion != NetworkProtocol.Version)
            throw new ArgumentException($"protocolVersion hiện chỉ hỗ trợ {NetworkProtocol.Version}.");
        if (options.ConnectTimeoutMs is < 500 or > 60000)
            throw new ArgumentException("connectTimeoutMs phải nằm trong [500, 60000].");
        // Chỉ kiểm tra API khi bật account; RoomCode hoạt động độc lập API/DB.
        if (options.AuthMode == "Jwt") options.ValidateApiUrl();
        return options;
    }

    public JoinAuthMode JoinMode => AuthMode == "Jwt" ? JoinAuthMode.Jwt : JoinAuthMode.RoomCode;

    public Uri ValidateApiUrl()
    {
        if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("Jwt cần apiBaseUrl HTTP/HTTPS hợp lệ, không chứa user/password/query.");
        // Password/token phải qua HTTPS, localhost, hoặc địa chỉ Tailscale được người dùng cấu hình.
        var vpnAddress = System.Net.IPAddress.TryParse(uri.Host, out var ip)
            && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            && ip.GetAddressBytes()[0] == 100 && ip.GetAddressBytes()[1] is >= 64 and <= 127;
        if (uri.Scheme == "http" && !uri.IsLoopback && !(Mode == "Online" && vpnAddress))
            throw new ArgumentException("API từ xa cần HTTPS hoặc mode=Online với IP Tailscale 100.64.0.0/10 đã kết nối VPN.");
        return uri;
    }
}
