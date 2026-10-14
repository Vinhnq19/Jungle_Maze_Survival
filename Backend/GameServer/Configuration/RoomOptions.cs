using System.Net;
using System.Text;
using JungleBackend.Rooms;

namespace GameServer.Configuration;

public sealed record RoomOptions(int MaxRoomPlayers = RoomLimits.PlayersPerRoom, int MaxTotalPlayers = RoomLimits.TotalPlayers,
    int MaxRooms = RoomLimits.Rooms, int MaxRoomsPerUser = RoomLimits.RoomsPerUser, int EmptyTtlSeconds = 600,
    string ControlToken = "", string ControlBind = "127.0.0.1", int ControlPort = 7780)
{
    public bool ControlEnabled => ControlToken.Length != 0;
    public override string ToString() => $"RoomOptions(maxRooms={MaxRooms}, totalPlayers={MaxTotalPlayers})";
    public static RoomOptions FromEnvironment() => new(
        Number("MAX_ROOM_PLAYERS", RoomLimits.PlayersPerRoom), Number("MAX_TOTAL_PLAYERS", RoomLimits.TotalPlayers), Number("MAX_ROOMS", RoomLimits.Rooms),
        Number("MAX_ROOMS_PER_USER", RoomLimits.RoomsPerUser), Number("ROOM_EMPTY_TTL_SECONDS", 600),
        Environment.GetEnvironmentVariable("ROOM_CONTROL_TOKEN") ?? "",
        Environment.GetEnvironmentVariable("ROOM_CONTROL_BIND_ADDRESS") ?? "127.0.0.1",
        Number("ROOM_CONTROL_PORT", 7780));
    public void Validate(int defaultPlayers)
    {
        if (MaxRoomPlayers is < 1 or > RoomLimits.PlayersPerRoom || MaxTotalPlayers is < 1 or > RoomLimits.TotalPlayers
            || MaxRooms is < 1 or > RoomLimits.Rooms || MaxRoomsPerUser is < 1 or > RoomLimits.RoomsPerUser
            || EmptyTtlSeconds is < 1 or > 86400 || defaultPlayers > MaxRoomPlayers
            || defaultPlayers > MaxTotalPlayers || ControlPort is < 1 or > 65535
            || !IPAddress.TryParse(ControlBind, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            throw new ArgumentException($"Room limits: <={RoomLimits.PlayersPerRoom} players/room, <={RoomLimits.TotalPlayers} total, <={RoomLimits.Rooms} rooms including default, <={RoomLimits.RoomsPerUser} created rooms/user. Check default capacity, TTL and control address too.");
        if (ControlEnabled && Encoding.UTF8.GetByteCount(ControlToken) < 32)
            throw new ArgumentException("ROOM_CONTROL_TOKEN needs at least 32 UTF-8 bytes; empty disables room HTTP control.");
    }
    private static int Number(string key, int fallback) => Environment.GetEnvironmentVariable(key) is not { } value
        ? fallback : int.TryParse(value, out var n) ? n : throw new ArgumentException($"{key} must be an integer.");
}
