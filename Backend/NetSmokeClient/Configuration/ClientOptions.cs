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
    public string Transport { get; init; } = "Udp";
    public string? WebSocketUrl { get; init; }
    // Optional per-process DNS pin for diagnostics; TLS still validates WebSocketUrl.
    public string? WebSocketConnectIp { get; init; }

    public static ClientOptions Load(string path)
    {
        var options = JsonSerializer.Deserialize<ClientOptions>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        }) ?? throw new ArgumentException("Config JSON không được null.");
        // Chỉ override endpoint, không tự chuyển tuyến khi kết nối lỗi. Cùng validation
        // được áp dụng sau khi merge để env sai không lặng lẽ dùng lại JSON.
        var portText = Environment.GetEnvironmentVariable("NET_GAME_SERVER_PORT");
        if (portText is not null && !int.TryParse(portText, out _))
            throw new ArgumentException("NET_GAME_SERVER_PORT phải là số nguyên trong [1, 65535].");
        options = new ClientOptions
        {
            Mode = Environment.GetEnvironmentVariable("NET_MODE") ?? options.Mode,
            ApiBaseUrl = Environment.GetEnvironmentVariable("NET_API_BASE_URL") ?? options.ApiBaseUrl,
            GameServerHost = Environment.GetEnvironmentVariable("NET_GAME_SERVER_HOST") ?? options.GameServerHost,
            GameServerPort = portText is null ? options.GameServerPort : int.Parse(portText),
            AuthMode = options.AuthMode,
            ProtocolVersion = options.ProtocolVersion,
            ConnectTimeoutMs = options.ConnectTimeoutMs
            ,Transport = Environment.GetEnvironmentVariable("NET_TRANSPORT") ?? options.Transport
            ,WebSocketUrl = Environment.GetEnvironmentVariable("NET_WS_URL") ?? options.WebSocketUrl
            ,WebSocketConnectIp = Environment.GetEnvironmentVariable("NET_WS_CONNECT_IP") ?? options.WebSocketConnectIp
        };
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
        if (options.ProtocolVersion is not (NetworkProtocol.Version or NetworkProtocol.LatestVersion))
            throw new ArgumentException("protocolVersion chỉ hỗ trợ 1 hoặc 2.");
        if (options.Transport is not ("Udp" or "Wss" or "Auto")) throw new ArgumentException("transport phải Udp, Wss hoặc Auto.");
        if (options.Transport != "Udp")
        {
            if (options.ProtocolVersion != NetworkProtocol.LatestVersion) throw new ArgumentException("Wss/Auto cần protocolVersion=2.");
            if (!Uri.TryCreate(options.WebSocketUrl, UriKind.Absolute, out var ws) || ws.Scheme is not ("ws" or "wss")
                || !string.IsNullOrEmpty(ws.UserInfo) || !string.IsNullOrEmpty(ws.Query) || !string.IsNullOrEmpty(ws.Fragment)
                || ws.AbsolutePath != "/game" || ws.Scheme == "ws" && !ws.IsLoopback)
                throw new ArgumentException("webSocketUrl cần wss://hostname/game; ws chỉ dùng loopback Local.");
            if (options.WebSocketConnectIp is not null &&
                (!System.Net.IPAddress.TryParse(options.WebSocketConnectIp, out var connectIp)
                 || connectIp.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork
                 || ws.Scheme == "ws" && !System.Net.IPAddress.IsLoopback(connectIp)))
                throw new ArgumentException("webSocketConnectIp cần IPv4; ws chỉ được pin tới loopback.");
        }
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
