using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetSmokeClient.Configuration;

/// <summary>Private load-test input; never serialize join codes into the result report.</summary>
public sealed class RoomLoadTarget
{
    public Guid RoomId { get; init; }
    public string RoomCode { get; init; } = "";
    public int Clients { get; init; } = 1;

    public static List<RoomLoadTarget> Load(string path)
    {
        var rooms = JsonSerializer.Deserialize<List<RoomLoadTarget>>(File.ReadAllText(path), new JsonSerializerOptions
        { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow });
        if (rooms is null || rooms.Count is < 1 or > 32 || rooms.Any(r => r is null || r.RoomId == Guid.Empty
            || string.IsNullOrWhiteSpace(r.RoomCode) || r.RoomCode.Length > 64 || r.Clients is < 1 or > 256)
            || rooms.Sum(r => r.Clients) > 256 || rooms.Select(r => r.RoomId).Distinct().Count() != rooms.Count)
            throw new ArgumentException("Room load plan needs unique room IDs, valid codes and 1..256 total clients in at most 32 rooms.");
        return rooms;
    }
}
