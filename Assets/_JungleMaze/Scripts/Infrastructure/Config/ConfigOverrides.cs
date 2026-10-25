using System;
using System.Collections.Generic;
using System.Globalization;
using JungleMaze.App.Config;

namespace JungleMaze.Infrastructure.Config
{
    /// <summary>
    /// Override endpoint từ biến môi trường (cùng tên với NetSmokeClient) rồi tới tham số dòng lệnh.
    /// Chỉ đổi endpoint; auth/protocol vẫn lấy từ file để không lẫn cấu hình server.
    /// </summary>
    public sealed class ConfigOverrides
    {
        public string Mode, Host, Port, ApiBaseUrl, Transport, WebSocketUrl;
        public string ConfigPath, Profile, RoomCode, DisplayName;
        public readonly List<string> Applied = new List<string>();

        /// <summary>-clientConfig path, -profile lan, -mode LAN, -host ip, -port n, -apiBaseUrl url, -room code, -name tên.</summary>
        public static ConfigOverrides From(string[] args, Func<string, string> env)
        {
            var result = new ConfigOverrides
            {
                Mode = env?.Invoke("NET_MODE"),
                Host = env?.Invoke("NET_GAME_SERVER_HOST"),
                Port = env?.Invoke("NET_GAME_SERVER_PORT"),
                ApiBaseUrl = env?.Invoke("NET_API_BASE_URL"),
                RoomCode = env?.Invoke("NET_ROOM_CODE"),
                Transport = env?.Invoke("NET_TRANSPORT"),
                WebSocketUrl = env?.Invoke("NET_WS_URL")
            };
            if (args == null) return result;
            for (var i = 0; i < args.Length - 1; i++)
            {
                var value = args[i + 1];
                switch (args[i].ToLowerInvariant())
                {
                    case "-clientconfig": result.ConfigPath = value; i++; break;
                    case "-profile": result.Profile = value; i++; break;
                    case "-mode": result.Mode = value; i++; break;
                    case "-host": result.Host = value; i++; break;
                    case "-port": result.Port = value; i++; break;
                    case "-apibaseurl": result.ApiBaseUrl = value; i++; break;
                    case "-room": result.RoomCode = value; i++; break;
                    case "-name": result.DisplayName = value; i++; break;
                    case "-transport": result.Transport = value; i++; break;
                    case "-websocketurl": result.WebSocketUrl = value; i++; break;
                }
            }
            return result;
        }

        /// <summary>Áp override lên config file; trả lỗi nếu giá trị override sai thay vì lặng lẽ dùng lại file.</summary>
        public string Apply(ClientConfig config, out ClientConfig result)
        {
            result = config;
            var mode = config.Mode;
            var host = config.GameServerHost;
            var port = config.GameServerPort;
            if (Mode != null)
            {
                if (!ClientConfigJson.TryMode(Mode, out mode)) return "Override mode phải là Local, LAN hoặc Online.";
                Applied.Add("mode");
            }
            if (Host != null) { host = Host; Applied.Add("host"); }
            if (Port != null)
            {
                if (!int.TryParse(Port, NumberStyles.Integer, CultureInfo.InvariantCulture, out port))
                    return "Override port phải là số nguyên trong [1, 65535].";
                Applied.Add("port");
            }
            if (Transport != null) Applied.Add("transport");
            if (WebSocketUrl != null) Applied.Add("webSocketUrl");
            if (Applied.Count == 0 && ApiBaseUrl == null) return null;
            var source = config.Source + " + override(" + string.Join(",", Applied) + (ApiBaseUrl != null ? ",apiBaseUrl" : "") + ")";
            result = config.WithEndpoint(host, port, mode, source);
            if (ApiBaseUrl != null) result = result.WithApiBaseUrl(ApiBaseUrl, source);
            if (Transport != null || WebSocketUrl != null) result = result.WithTransport(Transport ?? config.Transport, WebSocketUrl ?? config.WebSocketUrl, source);
            return null;
        }
    }
}
