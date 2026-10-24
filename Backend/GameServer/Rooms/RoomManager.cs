using System.Security.Cryptography;
using GameServer.Configuration;
using JungleBackend.Protocol.Gameplay;
using JungleBackend.Rooms;

namespace GameServer.Rooms;

using GameServer.Simulation;

/// <summary>Chỉ truy cập trên thread PollEvents. Không lock/HTTP/DB trong tick.</summary>
public sealed class RoomManager
{
    public sealed class Room(Guid id, string name, string code, string? owner, int max, DateTimeOffset now, MatchRuntime match)
    {
        public Guid Id { get; } = id;
        public string Name { get; } = name;
        public string Code { get; } = code;
        public string? Owner { get; } = owner;
        public int MaxPlayers { get; } = max;
        public bool IsDefault => Owner is null;
        public Dictionary<int, bool> Peers { get; } = new(); // false=pending JWT; true=active
        public DateTimeOffset EmptySince { get; set; } = now;
        public MatchRuntime Match { get; } = match;
        public override string ToString() => $"Room(id={Id}, peers={Peers.Count})";
    }
    private readonly RoomOptions _options;
    private readonly TimeProvider _time;
    private readonly Dictionary<string, Room> _byCode = new(StringComparer.Ordinal);
    private readonly MapData _map;
    private readonly ZoneSchedule _zoneSchedule;
    private readonly MatchProfile _profile;

    public event Action<Room>? RoomCreated;

    public RoomManager(ServerOptions server, TimeProvider? time = null, MatchProfile? profile = null, MapData? map = null, ZoneSchedule? zoneSchedule = null)
    {
        _options = server.Rooms ?? new RoomOptions();
        _time = time ?? TimeProvider.System;
        _profile = profile ?? MatchProfile.Current;

        // Invalid or missing canonical data is a startup error; never substitute a different map.
        _map = map ?? MapData.Load(GameDataLoader.ReadGameDataFile("map_demo.json"));
        _zoneSchedule = zoneSchedule ?? ZoneSchedule.Load(GameDataLoader.ReadGameDataFile("zone_schedule.json"));

        var roomId = Guid.NewGuid();
        var defaultMatch = new MatchRuntime(server.RoomCode, _map, _zoneSchedule, _profile, roomId.ToString());
        var room = new Room(roomId, "Default", server.RoomCode, null, server.MaxPlayers, _time.GetUtcNow(), defaultMatch);
        _byCode.Add(room.Code, room);
    }

    public int Count => _byCode.Count;
    public int ReservedPeers => _byCode.Values.Sum(room => room.Peers.Count);
    public int ActivePeers => _byCode.Values.Sum(room => room.Peers.Values.Count(active => active));
    public Room? Find(string code) => _byCode.GetValueOrDefault(code);
    public IEnumerable<Room> AllRooms => _byCode.Values;

    public string? CapacityError(Room room) =>
        room.Peers.Count >= room.MaxPlayers ? "RoomFull"
        : ReservedPeers >= _options.MaxTotalPlayers ? "ServerFull" : null;

    public void Reserve(Room room, int peerId, bool active) => room.Peers.Add(peerId, active);
    public void Activate(Room room, int peerId) => room.Peers[peerId] = true;

    public void Release(Room room, int peerId, string? playerId = null)
    {
        if (playerId is not null)
        {
            room.Match.MarkDisconnected(playerId);
        }

        // If InGame and player entity is alive, preserve character and slot for 15s grace
        if (playerId is not null && room.Match.Phase == MatchPhase.InGame &&
            room.Match.Entities.TryGetBySession(playerId, out var entity) && entity is { IsAlive: true })
        {
            return;
        }

        if (room.Peers.Remove(peerId) && room.Peers.Count == 0) room.EmptySince = _time.GetUtcNow();
    }

    public void ReleaseSlot(Room room, int peerId)
    {
        if (room.Peers.Remove(peerId) && room.Peers.Count == 0) room.EmptySince = _time.GetUtcNow();
    }

    public void Tick(double dt)
    {
        foreach (var room in _byCode.Values)
        {
            room.Match.Tick(dt);
        }
    }

    public RoomResult Create(CreateRoomCommand command)
    {
        ExpireEmpty();
        if (string.IsNullOrWhiteSpace(command.OwnerId) || command.OwnerId.Length > 64
            || string.IsNullOrWhiteSpace(command.Name) || command.Name.Trim().Length > 48
            || command.MaxPlayers < 1 || command.MaxPlayers > Math.Min(_options.MaxRoomPlayers, _options.MaxTotalPlayers))
            return new(400, new RoomError("InvalidRoom"));
        if (_byCode.Values.Count(room => room.Owner == command.OwnerId) >= _options.MaxRoomsPerUser)
            return new(409, new RoomError("UserRoomLimitReached"));
        if (Count >= _options.MaxRooms) return new(409, new RoomError("RoomLimitReached"));

        string code;
        Span<char> characters = stackalloc char[RoomLimits.JoinCodeLength];
        do
        {
            for (var i = 0; i < characters.Length; i++)
                characters[i] = RoomLimits.JoinCodeAlphabet[RandomNumberGenerator.GetInt32(RoomLimits.JoinCodeAlphabet.Length)];
            code = new string(characters);
        } while (_byCode.ContainsKey(code));

        var roomId = Guid.NewGuid();
        var match = new MatchRuntime(code, _map, _zoneSchedule, _profile, roomId.ToString());
        var created = new Room(roomId, command.Name.Trim(), code, command.OwnerId, command.MaxPlayers, _time.GetUtcNow(), match);
        _byCode.Add(code, created);
        RoomCreated?.Invoke(created);
        Console.WriteLine($"[room-created] roomId={created.Id} capacity={created.MaxPlayers}");
        return new(201, View(created, command.OwnerId));
    }

    public RoomResult List(string owner) => new(200, _byCode.Values.Where(room => room.IsDefault || room.Owner == owner)
        .Select(room => View(room, owner)).ToArray());

    public RoomResult Delete(Guid id, string owner)
    {
        var room = _byCode.Values.FirstOrDefault(room => room.Id == id);
        if (room is null) return new(404, new RoomError("RoomNotFound"));
        if (room.IsDefault) return new(409, new RoomError("DefaultRoomProtected"));
        if (room.Owner != owner) return new(403, new RoomError("NotRoomOwner"));
        if (room.Peers.Count != 0) return new(409, new RoomError("RoomNotEmpty"));
        _byCode.Remove(room.Code);
        return new(204, null);
    }

    public void ExpireEmpty()
    {
        var now = _time.GetUtcNow();
        foreach (var room in _byCode.Values.Where(room => !room.IsDefault && room.Peers.Count == 0
                     && now - room.EmptySince >= TimeSpan.FromSeconds(_options.EmptyTtlSeconds)).ToArray())
        {
            _byCode.Remove(room.Code);
            Console.WriteLine($"[room-expired] roomId={room.Id}");
        }
    }

    private static RoomView View(Room room, string owner) => new()
    {
        Id = room.Id, Name = room.Name, IsDefault = room.IsDefault, MaxPlayers = room.MaxPlayers,
        ActivePlayers = room.Peers.Values.Count(active => active), PendingPlayers = room.Peers.Values.Count(active => !active),
        RoomCode = !room.IsDefault && room.Owner == owner ? room.Code : null
    };

    public string Occupancy() => string.Join(',', _byCode.Values.Select(r => $"{r.Id}:{r.Peers.Count}/{r.MaxPlayers}"));
}
