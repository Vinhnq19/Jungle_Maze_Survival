using System;
using System.Collections.Generic;
using System.Globalization;
using JungleBackend.Protocol;
using JungleMaze.App.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JungleMaze.Infrastructure.Config
{
    /// <summary>
    /// Đọc/ghi JSON cùng schema với NetSmokeClient (client.*.json). Key lạ hoặc thiếu key bắt buộc là lỗi,
    /// giống UnmappedMemberHandling.Disallow phía backend, để gõ sai tên field không bị bỏ qua lặng lẽ.
    /// </summary>
    public static class ClientConfigJson
    {
        private static readonly string[] Required =
            { "mode", "gameServerHost", "gameServerPort", "authMode", "protocolVersion", "connectTimeoutMs" };

        private static readonly HashSet<string> Known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mode", "apiBaseUrl", "gameServerHost", "gameServerPort", "authMode", "protocolVersion",
            "connectTimeoutMs", "transport", "webSocketUrl", "webSocketConnectIp"
        };

        public static bool TryParse(string json, string source, out ClientConfig config, out string error)
        {
            config = null;
            JObject root;
            try { root = JObject.Parse(json ?? ""); }
            catch (JsonException e) { error = $"{source}: JSON không hợp lệ ({e.Message})."; return false; }

            var values = new Dictionary<string, JToken>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in root.Properties())
            {
                if (!Known.Contains(property.Name)) { error = $"{source}: key không hỗ trợ '{property.Name}'."; return false; }
                values[property.Name] = property.Value;
            }
            foreach (var key in Required)
                if (!values.ContainsKey(key)) { error = $"{source}: thiếu key bắt buộc '{key}'."; return false; }

            var connectIp = String(values, "webSocketConnectIp");
            if (!string.IsNullOrEmpty(connectIp))
            {
                error = $"{source}: Unity client không hỗ trợ 'webSocketConnectIp'.";
                return false;
            }

            if (!TryMode(String(values, "mode"), out var mode)) { error = $"{source}: mode phải là Local, LAN hoặc Online."; return false; }
            if (!TryAuthMode(String(values, "authMode"), out var authMode)) { error = $"{source}: authMode phải là RoomCode hoặc Jwt."; return false; }
            if (!TryInt(values["gameServerPort"], out var port)) { error = $"{source}: gameServerPort phải là số nguyên."; return false; }
            if (!TryInt(values["protocolVersion"], out var version) || version < 0 || version > ushort.MaxValue)
                { error = $"{source}: protocolVersion phải là số nguyên."; return false; }
            if (!TryInt(values["connectTimeoutMs"], out var timeout)) { error = $"{source}: connectTimeoutMs phải là số nguyên."; return false; }

            config = new ClientConfig(mode, String(values, "apiBaseUrl"), String(values, "gameServerHost"), port,
                authMode, (ushort)version, timeout, String(values, "transport") ?? "Udp", source, String(values, "webSocketUrl"));
            error = null;
            return true;
        }

        public static string Serialize(ClientConfig config)
        {
            var root = new JObject
            {
                ["mode"] = config.Mode.ToString(),
                ["apiBaseUrl"] = config.ApiBaseUrl,
                ["gameServerHost"] = config.GameServerHost,
                ["gameServerPort"] = config.GameServerPort,
                ["authMode"] = config.AuthMode.ToString(),
                ["protocolVersion"] = config.ProtocolVersion,
                ["connectTimeoutMs"] = config.ConnectTimeoutMs,
                ["transport"] = config.Transport
            };
            if (!string.IsNullOrEmpty(config.WebSocketUrl))
                root["webSocketUrl"] = config.WebSocketUrl;
            return root.ToString(Formatting.Indented);
        }

        public static bool TryMode(string text, out ConnectionMode mode)
        {
            switch (text)
            {
                case "Local": mode = ConnectionMode.Local; return true;
                case "LAN": mode = ConnectionMode.LAN; return true;
                case "Online": mode = ConnectionMode.Online; return true;
                default: mode = default; return false;
            }
        }

        private static bool TryAuthMode(string text, out JoinAuthMode mode)
        {
            switch (text)
            {
                case "RoomCode": mode = JoinAuthMode.RoomCode; return true;
                case "Jwt": mode = JoinAuthMode.Jwt; return true;
                default: mode = default; return false;
            }
        }

        private static string String(Dictionary<string, JToken> values, string key) =>
            values.TryGetValue(key, out var token) && token.Type != JTokenType.Null ? token.ToString() : null;

        private static bool TryInt(JToken token, out int value)
        {
            if (token.Type == JTokenType.Integer)
            {
                var raw = token.Value<long>();
                if (raw >= int.MinValue && raw <= int.MaxValue) { value = (int)raw; return true; }
            }
            if (token.Type == JTokenType.String)
                return int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            value = 0;
            return false;
        }
    }
}
