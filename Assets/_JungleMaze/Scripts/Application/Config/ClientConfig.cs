using System;
using System.Net;
using System.Net.Sockets;
using JungleBackend.Protocol;

namespace JungleMaze.App.Config
{
    public enum ConnectionMode : byte { Local = 0, LAN = 1, Online = 2 }

    /// <summary>
    /// Endpoint đọc từ JSON ngoài build, cùng schema với Backend/NetSmokeClient/client.*.json.
    /// Đổi IP Local/LAN/VPN chỉ cần sửa file, không build lại.
    /// </summary>
    public sealed class ClientConfig
    {
        public const int MinTimeoutMs = 500, MaxTimeoutMs = 60000;

        public ConnectionMode Mode { get; }
        public string ApiBaseUrl { get; }
        public string GameServerHost { get; }
        public int GameServerPort { get; }
        public JoinAuthMode AuthMode { get; }
        public ushort ProtocolVersion { get; }
        public int ConnectTimeoutMs { get; }
        /// <summary>Unity client hiện chỉ có UDP; Wss/Auto bị từ chối rõ ràng thay vì lặng lẽ dùng UDP.</summary>
        public string Transport { get; }
        /// <summary>Nơi config được nạp (file/override), để hiện trên UI khi debug.</summary>
        public string Source { get; }

        public ClientConfig(ConnectionMode mode, string apiBaseUrl, string gameServerHost, int gameServerPort,
            JoinAuthMode authMode, ushort protocolVersion, int connectTimeoutMs, string transport = "Udp",
            string source = "")
        {
            Mode = mode;
            ApiBaseUrl = string.IsNullOrWhiteSpace(apiBaseUrl) ? null : apiBaseUrl;
            GameServerHost = gameServerHost;
            GameServerPort = gameServerPort;
            AuthMode = authMode;
            ProtocolVersion = protocolVersion;
            ConnectTimeoutMs = connectTimeoutMs;
            Transport = string.IsNullOrEmpty(transport) ? "Udp" : transport;
            Source = source ?? "";
        }

        public ClientConfig WithEndpoint(string host, int port, ConnectionMode mode, string source) =>
            new ClientConfig(mode, ApiBaseUrl, host, port, AuthMode, ProtocolVersion, ConnectTimeoutMs, Transport, source);

        public ClientConfig WithApiBaseUrl(string apiBaseUrl, string source) =>
            new ClientConfig(Mode, apiBaseUrl, GameServerHost, GameServerPort, AuthMode, ProtocolVersion,
                ConnectTimeoutMs, Transport, source);

        public string Endpoint => $"{GameServerHost}:{GameServerPort}";

        /// <summary>Kiểm tra như ClientOptions.Load phía backend; trả thông báo lỗi đầu tiên hoặc null nếu hợp lệ.</summary>
        public string Validate()
        {
            if (string.IsNullOrWhiteSpace(GameServerHost) || GameServerHost != GameServerHost.Trim()
                || Uri.CheckHostName(GameServerHost) == UriHostNameType.Unknown)
                return "gameServerHost phải là IP/hostname, không có http:// hoặc port.";
            if (GameServerPort < 1 || GameServerPort > 65535)
                return "gameServerPort phải nằm trong [1, 65535].";
            if (ProtocolVersion != NetworkProtocol.Version && ProtocolVersion != NetworkProtocol.LatestVersion)
                return $"protocolVersion chỉ hỗ trợ {NetworkProtocol.Version} hoặc {NetworkProtocol.LatestVersion}.";
            if (Transport != "Udp")
                return "Unity client hiện chỉ hỗ trợ transport=Udp (Wss/Auto chưa triển khai).";
            if (ConnectTimeoutMs < MinTimeoutMs || ConnectTimeoutMs > MaxTimeoutMs)
                return $"connectTimeoutMs phải nằm trong [{MinTimeoutMs}, {MaxTimeoutMs}].";
            // Chỉ kiểm tra API khi bật account; RoomCode hoạt động độc lập API/DB.
            if (AuthMode == JoinAuthMode.Jwt) return ValidateApiUrl(out _);
            return null;
        }

        /// <summary>Password/token chỉ đi qua HTTPS, loopback hoặc IP Tailscale ở mode Online.</summary>
        public string ValidateApiUrl(out Uri uri)
        {
            if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out uri)
                || (uri.Scheme != "http" && uri.Scheme != "https") || !string.IsNullOrEmpty(uri.UserInfo)
                || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                return "Jwt cần apiBaseUrl HTTP/HTTPS hợp lệ, không chứa user/password/query.";
            var vpnAddress = IPAddress.TryParse(uri.Host, out var ip)
                && ip.AddressFamily == AddressFamily.InterNetwork
                && ip.GetAddressBytes()[0] == 100 && ip.GetAddressBytes()[1] >= 64 && ip.GetAddressBytes()[1] <= 127;
            if (uri.Scheme == "http" && !uri.IsLoopback && !(Mode == ConnectionMode.Online && vpnAddress))
                return "API từ xa cần HTTPS hoặc mode=Online với IP Tailscale 100.64.0.0/10.";
            return null;
        }

        public override string ToString() => $"ClientConfig(mode={Mode}, endpoint={Endpoint}, auth={AuthMode}, v{ProtocolVersion})";
    }
}
