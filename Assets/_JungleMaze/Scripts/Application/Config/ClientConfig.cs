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
        /// <summary>Hỗ trợ Udp, Wss hoặc Auto (thử UDP trước rồi fallback sang Wss).</summary>
        public string Transport { get; }
        /// <summary>Nơi config được nạp (file/override), để hiện trên UI khi debug.</summary>
        public string Source { get; }
        /// <summary>URL kết nối WebSocket endpoint (ví dụ: wss://hostname/game hoặc ws://127.0.0.1:7781/game).</summary>
        public string WebSocketUrl { get; }

        public ClientConfig(ConnectionMode mode, string apiBaseUrl, string gameServerHost, int gameServerPort,
            JoinAuthMode authMode, ushort protocolVersion, int connectTimeoutMs, string transport = "Udp",
            string source = "", string webSocketUrl = null)
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
            WebSocketUrl = string.IsNullOrWhiteSpace(webSocketUrl) ? null : webSocketUrl.Trim();
        }

        public ClientConfig WithEndpoint(string host, int port, ConnectionMode mode, string source) =>
            new ClientConfig(mode, ApiBaseUrl, host, port, AuthMode, ProtocolVersion, ConnectTimeoutMs, Transport, source, WebSocketUrl);

        public ClientConfig WithApiBaseUrl(string apiBaseUrl, string source) =>
            new ClientConfig(Mode, apiBaseUrl, GameServerHost, GameServerPort, AuthMode, ProtocolVersion,
                ConnectTimeoutMs, Transport, source, WebSocketUrl);

        public ClientConfig WithWebSocketUrl(string webSocketUrl, string source = null) =>
            new ClientConfig(Mode, ApiBaseUrl, GameServerHost, GameServerPort, AuthMode, ProtocolVersion,
                ConnectTimeoutMs, Transport, source ?? Source, webSocketUrl);

        public ClientConfig WithTransport(string transport, string webSocketUrl, string source = null) =>
            new ClientConfig(Mode, ApiBaseUrl, GameServerHost, GameServerPort, AuthMode, ProtocolVersion,
                ConnectTimeoutMs, transport, source ?? Source, webSocketUrl);

        public string Endpoint => $"{GameServerHost}:{GameServerPort}";

        /// <summary>Kiểm tra cấu hình; trả chuỗi lỗi đầu tiên hoặc null nếu hợp lệ.</summary>
        public string Validate()
        {
            if (string.IsNullOrWhiteSpace(GameServerHost) || GameServerHost != GameServerHost.Trim()
                || Uri.CheckHostName(GameServerHost) == UriHostNameType.Unknown)
                return "gameServerHost phải là IP/hostname, không có http:// hoặc port.";
            if (GameServerPort < 1 || GameServerPort > 65535)
                return "gameServerPort phải nằm trong [1, 65535].";
            if (Mode == ConnectionMode.Local && !IsLoopbackHost(GameServerHost))
                return "Local cần host loopback; chọn đúng profile LAN/VPN/AWS trước khi đổi môi trường.";
            if (Mode != ConnectionMode.Local && !IsLoopbackHost(GameServerHost) && Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var apiRoute) && apiRoute.IsLoopback)
                return "API loopback không đi cùng server từ xa; chọn profile cùng backend hoặc bỏ API để chơi ẩn danh.";
            if (ProtocolVersion != NetworkProtocol.Version && ProtocolVersion != NetworkProtocol.LatestVersion && ProtocolVersion != NetworkProtocol.Version3)
                return $"protocolVersion chỉ hỗ trợ {NetworkProtocol.Version}, {NetworkProtocol.LatestVersion} hoặc {NetworkProtocol.Version3}.";
            if (Transport != "Udp" && Transport != "Wss" && Transport != "Auto")
                return "transport phải là Udp, Wss hoặc Auto.";
            if (Transport != "Udp")
            {
                if (ProtocolVersion != NetworkProtocol.LatestVersion && ProtocolVersion != NetworkProtocol.Version3)
                    return "Wss/Auto cần protocolVersion=2 hoặc 3.";
                var wsError = ValidateWebSocketUrl(out _);
                if (wsError != null) return wsError;
                var ws = new Uri(WebSocketUrl);
                if (Mode == ConnectionMode.Local && !ws.IsLoopback) return "Local không được fallback sang server từ xa.";
                if (Mode != ConnectionMode.Local && Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var api) &&
                    (!string.Equals(api.Host, ws.Host, StringComparison.OrdinalIgnoreCase) || api.Port != ws.Port))
                    return "API và WSS phải dùng cùng HTTPS gateway của một backend.";
            }
            if (ConnectTimeoutMs < MinTimeoutMs || ConnectTimeoutMs > MaxTimeoutMs)
                return $"connectTimeoutMs phải nằm trong [{MinTimeoutMs}, {MaxTimeoutMs}].";
            // Chỉ kiểm tra API khi bật account; RoomCode hoạt động độc lập API/DB.
            if (AuthMode == JoinAuthMode.Jwt) return ValidateApiUrl(out _);
            return null;
        }

        /// <summary>Strict TLS cho wss; ws chỉ cho phép loopback ở mode Local.</summary>
        public string ValidateWebSocketUrl(out Uri uri)
        {
            if (string.IsNullOrWhiteSpace(WebSocketUrl) || !Uri.TryCreate(WebSocketUrl, UriKind.Absolute, out uri)
                || (uri.Scheme != "ws" && uri.Scheme != "wss")
                || !string.IsNullOrEmpty(uri.UserInfo)
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment)
                || uri.AbsolutePath != "/game")
            {
                uri = null;
                return "webSocketUrl cần wss://hostname/game; ws chỉ dùng loopback Local.";
            }
            if (uri.Scheme == "ws" && (!uri.IsLoopback || Mode != ConnectionMode.Local))
            {
                return "webSocketUrl không bảo mật (ws) chỉ được dùng trên loopback (127.0.0.1/localhost) với mode Local.";
            }
            return null;
        }

        /// <summary>Password/token chỉ đi qua HTTPS, ngoại trừ loopback.</summary>
        public string ValidateApiUrl(out Uri uri)
        {
            if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out uri)
                || (uri.Scheme != "http" && uri.Scheme != "https") || !string.IsNullOrEmpty(uri.UserInfo)
                || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                return "Jwt cần apiBaseUrl HTTP/HTTPS hợp lệ, không chứa user/password/query.";
            if (uri.Scheme == "http" && !uri.IsLoopback)
                return "API từ xa cần HTTPS; HTTP chỉ dùng trên loopback (127.0.0.1/localhost).";
            return null;
        }

        public override string ToString() => $"ClientConfig(mode={Mode}, endpoint={Endpoint}, auth={AuthMode}, v{ProtocolVersion}, transport={Transport})";

        private static bool IsLoopbackHost(string host) => string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }
}
