using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using JungleBackend.Rooms;

namespace Api.DTOs;
public sealed class CreateRoomRequest
{
    [Required, StringLength(48, MinimumLength = 1)] public string Name { get; init; } = "";
    [Range(1, RoomLimits.PlayersPerRoom), DefaultValue(RoomLimits.PlayersPerRoom)]
    public int MaxPlayers { get; init; } = RoomLimits.PlayersPerRoom;
}
