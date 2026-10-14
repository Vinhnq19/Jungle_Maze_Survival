namespace JungleBackend.Rooms;

// One policy shared by API validation, Swagger and the UDP server.
public static class RoomLimits
{
    public const int PlayersPerRoom = 10;
    public const int TotalPlayers = 50;
    public const int Rooms = 5; // Includes the permanent default room.
    public const int RoomsPerUser = 2;
    public const int JoinCodeLength = 6;
    // Uppercase letters/digits; omit I, O, 0, 1 to make codes easier to read.
    public const string JoinCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
}

// HTTP control DTO, không thay đổi wire protocol UDP v1. Không dùng record.ToString để lộ mã.
public sealed class RoomView
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public bool IsDefault { get; init; }
    public int MaxPlayers { get; init; }
    public int ActivePlayers { get; init; }
    public int PendingPlayers { get; init; }
    public string? RoomCode { get; init; }
}
public sealed class CreateRoomCommand
{
    public string OwnerId { get; init; } = "";
    public string Name { get; init; } = "";
    public int MaxPlayers { get; init; } = 10;
}
public sealed record RoomError(string Error);
public sealed record RoomResult(int Status, object? Body);
