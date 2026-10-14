using System.Globalization;
using System.Net;

namespace GameServer.Configuration;

/// <summary>Đọc biến môi trường trước khi mở socket; cấu hình sai thì dừng ngay.</summary>
public sealed record ServerOptions(IPAddress BindAddress, int Port, int MaxPlayers, int TickRate,
    int MetricsIntervalSeconds, string AuthMode, string RoomCode, int AuthTimeoutMs = 5000, JwtSettings? Jwt = null,
    bool PublicHosting = false, RoomOptions? Rooms = null)
{
    public JungleBackend.Protocol.JoinAuthMode Mode => AuthMode == "Jwt"
        ? JungleBackend.Protocol.JoinAuthMode.Jwt : JungleBackend.Protocol.JoinAuthMode.RoomCode;

    // Không in RoomCode hoặc SigningKey khi debug record.
    public override string ToString() => $"ServerOptions(bind={BindAddress}:{Port}, authMode={AuthMode}, maxPlayers={MaxPlayers})";

    public static ServerOptions FromEnvironment()
    {
        var bind = Environment.GetEnvironmentVariable("GAME_BIND_ADDRESS") ?? "0.0.0.0";
        if (!IPAddress.TryParse(bind, out var address)
            || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            throw new ArgumentException("GAME_BIND_ADDRESS phải là IPv4 hợp lệ (Docker: 0.0.0.0).");
        var authMode = Environment.GetEnvironmentVariable("AUTH_MODE") ?? "RoomCode";
        var result = new ServerOptions(address, ReadInt("GAME_PORT", 7777, 1, 65535),
            ReadInt("MAX_PLAYERS", 10, 1, 10), ReadInt("TICK_RATE", 30, 30, 30),
            ReadInt("METRICS_INTERVAL_SECONDS", 5, 1, 60), authMode,
            Environment.GetEnvironmentVariable("ROOM_CODE") ?? string.Empty,
            ReadInt("AUTH_TIMEOUT_MS", 5000, 500, 30000), authMode == "Jwt" ? JwtSettings.FromEnvironment() : null,
            ReadBool("PUBLIC_HOSTING"), RoomOptions.FromEnvironment());
        result.Validate();
        return result;
    }

    public void Validate()
    {
        (Rooms ?? new RoomOptions()).Validate(MaxPlayers);
        // Kiểm tra cả options được tạo trực tiếp (test/DI), không chỉ đường FromEnvironment.
        if (BindAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || Port is < 1 or > 65535
            || MaxPlayers is < 1 or > 10 || TickRate != 30 || MetricsIntervalSeconds is < 1 or > 60
            || AuthTimeoutMs is < 500 or > 30000)
            throw new ArgumentException("Server options cần IPv4/cổng hợp lệ, <=10 người, 30Hz và timeout/metrics đúng phạm vi.");
        if (AuthMode is not ("RoomCode" or "Jwt")) throw new ArgumentException("AUTH_MODE phải là RoomCode hoặc Jwt.");
        // Đợt AWS này chưa mã hóa Auth UDP. Không cho JWT public đi qua dù env bị đổi nhầm.
        if (PublicHosting && AuthMode != "RoomCode")
            throw new ArgumentException("PUBLIC_HOSTING chỉ hỗ trợ RoomCode; JWT UDP public cần mã hóa trước.");
        if (string.IsNullOrWhiteSpace(RoomCode) || RoomCode.Length > JungleBackend.Protocol.NetworkProtocol.MaxRoomChars)
            throw new ArgumentException("ROOM_CODE cần 1–64 ký tự, không được trống.");
        if (AuthMode == "Jwt")
        {
            if (Jwt is null) throw new ArgumentException("Thiếu cấu hình JWT; không fallback sang RoomCode.");
            Jwt.Validate();
        }
    }

    private static int ReadInt(string name, int fallback, int minimum, int maximum)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (raw is null) return fallback;
        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            || value < minimum || value > maximum)
            throw new ArgumentException($"{name} phải nằm trong [{minimum}, {maximum}].");
        return value;
    }
    private static bool ReadBool(string name)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (raw is null) return false;
        if (!bool.TryParse(raw, out var value)) throw new ArgumentException($"{name} phải là true/false.");
        return value;
    }
}
