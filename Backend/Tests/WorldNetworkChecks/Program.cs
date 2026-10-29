using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using GameServer.Configuration;
using GameServer.Diagnostics;
using GameServer.Networking;
using JungleBackend.Protocol.Gameplay;
using JungleBackend.Rooms;
using JungleMaze.World;

// Isolated real loopback UDP/WebSocket fixture. Confirmed damage and positioning below are
// authoritative test setup only; no gameplay packet permits clients to damage containers.
var passed = new List<string>();
void Check(bool value, string label) { if (!value) throw new Exception("FAIL " + label); passed.Add(label); Console.WriteLine("PASS " + label); }
int FreePort(SocketType type) { using var s = new Socket(AddressFamily.InterNetwork, type, type == SocketType.Stream ? ProtocolType.Tcp : ProtocolType.Udp); s.Bind(new IPEndPoint(IPAddress.Loopback, 0)); return ((IPEndPoint)s.LocalEndPoint!).Port; }
var mode = args.FirstOrDefault() ?? "Mixed";
if (mode is not ("Udp" or "Wss" or "Mixed")) throw new ArgumentException("Use Udp, Wss or Mixed.");
Environment.SetEnvironmentVariable("WORLD_ENABLED", "1");
Environment.SetEnvironmentVariable("MATCH_PROFILE", "test");
Environment.SetEnvironmentVariable("WS_ENABLED", "true");
Environment.SetEnvironmentVariable("WS_BIND_ADDRESS", "127.0.0.1");
var udpPort = FreePort(SocketType.Dgram); var wsPort = FreePort(SocketType.Stream);
Environment.SetEnvironmentVariable("WS_PORT", wsPort.ToString());
using var host = new NetworkHost(new ServerOptions(IPAddress.Loopback, udpPort, 10, 30, 1, "RoomCode", "WORLD_TEST"), new ServerMetrics());
host.Start();
await using var ws = await WebSocketHost.StartFromEnvironment(host.Hub, 50);
var clients = new List<WorldNetworkClient>(); var clock = Stopwatch.StartNew(); double nextTick = 0;
void Pump(Func<bool> done, int timeoutMs = 7000)
{
    var end = clock.ElapsedMilliseconds + timeoutMs;
    while (!done())
    {
        ws!.PollEvents(); host.PollEvents(); foreach (var c in clients) c.Poll();
        var now = clock.Elapsed.TotalSeconds;
        while (now >= nextTick) { host.Hub.Tick(1.0 / 30); nextTick += 1.0 / 30; }
        if (clock.ElapsedMilliseconds > end) throw new TimeoutException("World network fixture timed out");
        Thread.Sleep(1);
    }
}
void WaitTicks(uint ticks)
{
    var match = host.Hub.Rooms.Find("WORLD_TEST")!.Match; var start = match.CurrentTick;
    Pump(() => unchecked(match.CurrentTick - start) >= ticks, (int)(ticks * 1000 / 30 + 3000));
}
void Send(WorldNetworkClient c, uint requestId, WorldActionKind action, ushort target = 0, Guid? match = null, byte slot = 255)
{
    var packet = new WorldActionRequestPacket { MatchId = match ?? c.MatchId, RequestId = requestId, Action = action, TargetId = target, Slot = slot };
    c.Send(ref packet, true);
}
try
{
    for (var i = 0; i < 2; i++)
    {
        var c = new WorldNetworkClient(mode == "Mixed" ? i == 0 ? "Udp" : "Wss" : mode);
        clients.Add(c); await c.ConnectAsync("127.0.0.1", udpPort, $"ws://127.0.0.1:{wsPort}/game", "WORLD_TEST");
    }
    Pump(() => clients.All(c => c.Phase == MatchPhase.InGame && c.SnapshotCount > 2));
    var serverMatch = host.Hub.Rooms.Find("WORLD_TEST")!.Match;
    Check(serverMatch.World != null, "configured world created at match start without client mutation");
    var world = serverMatch.World!;
    foreach (var c in clients) Send(c, 1, WorldActionKind.Resync);
    Pump(() => clients.All(c => c.Manifest.HasValue && c.WorldObjects.Length >= 201 && c.Inventory.HasValue));
    Check(clients.All(c => c.Manifest!.Value.Hash == world.Layout.Hash && c.Manifest.Value.MatchId == serverMatch.MatchId), "both network clients verify same match map identity");
    Check(clients[0].WorldObjects.Select(o => o.Id).SequenceEqual(clients[1].WorldObjects.Select(o => o.Id)), "paged initial world state matches both clients");
    Check(clients.All(c => c.Inventory!.Value.Slots.Length == 8 && c.Inventory.Value.ActiveWeapon == 255), "network inventory has 4+4 slots with unarmed outside slots");

    var chest = world.Layout.Objects.First(o => o.Kind == WorldObjectKind.Chest);
    Check(world.Destructibles.DamageConfirmed(chest.Id, 30, 0, 1), "fixture confirms chest destruction on server only");
    Check(!world.Destructibles.DamageConfirmed(chest.Id, 30, 0, 1), "duplicate confirmed hit cannot create loot twice");
    Pump(() => clients.All(c => c.WorldObjects.Any(o => o.Id == chest.Id && (o.Flags & 1) != 0) && c.WorldObjects.Any(o => o.Kind == 100)));
    var drop = world.Floor.Items.First();
    Check(world.Floor.Items.Count is >= 3 and <= 4, "one chest yields one 3-4 item batch");
    Check(clients[0].WorldObjects.Count(o => o.Kind == 100) == clients[1].WorldObjects.Count(o => o.Kind == 100), "both transports receive same floor loot");
    foreach (var actor in serverMatch.Entities.All) { actor.X = drop.X; actor.Y = drop.Y; }
    world.SyncActors();
    WaitTicks(1);
    Send(clients[0], 2, WorldActionKind.Pickup, drop.Id);
    Send(clients[1], 2, WorldActionKind.Pickup, drop.Id);
    Pump(() => clients.Take(2).All(c => c.Results.Any(r => r.RequestId == 2)));
    Check(clients.Take(2).Sum(c => c.Results.Count(r => r.RequestId == 2 && r.Accepted)) == 1, "simultaneous network pickup accepted by exactly one actor");
    Check(!world.Floor.TryGet(drop.Id, out _), "picked floor item removed authoritatively");
    var owner = clients[0].Results.Any(r => r.RequestId == 2 && r.Accepted) ? clients[0] : clients[1];
    Pump(() => owner.Inventory!.Value.Slots.Any(s => s.ItemId == drop.Stack.ItemId) || owner.Inventory.Value.Helmet.ItemId == drop.Stack.ItemId || owner.Inventory.Value.Armor.ItemId == drop.Stack.ItemId);
    var floorCount = world.Floor.Items.Count;
    WaitTicks(1); Send(owner, 2, WorldActionKind.Pickup, drop.Id);
    var oldResults = owner.Results.Count;
    Pump(() => owner.Results.Count > oldResults);
    Check(!owner.Results.Last().Accepted && world.Floor.Items.Count == floorCount, "duplicate network request cannot consume or duplicate loot");
    WaitTicks(1); Send(owner, 3, WorldActionKind.Drop, 0, Guid.NewGuid(), 0);
    WaitTicks(2);
    Check(world.Floor.Items.Count == floorCount, "wrong-match request cannot mutate current room");

    var roomResult = host.Hub.Rooms.Create(new CreateRoomCommand("world-qa", "isolated", 2));
    Check(roomResult.StatusCode == 201, "isolated second room created");
    var secondRoom = host.Hub.Rooms.AllRooms.First(r => !r.IsDefault);
    var isolated = new WorldNetworkClient("Udp"); clients.Add(isolated);
    await isolated.ConnectAsync("127.0.0.1", udpPort, $"ws://127.0.0.1:{wsPort}/game", secondRoom.Code);
    Pump(() => isolated.PlayerId != null);
    WaitTicks(20);
    Check(isolated.WorldPacketsSeen == 0, "world state never broadcasts into another room");

    var beforeResync = owner.WorldPacketsSeen;
    WaitTicks(35); Send(owner, 4, WorldActionKind.Resync);
    Pump(() => owner.WorldPacketsSeen > beforeResync);
    Check(owner.WorldObjects.Any(o => o.Id == chest.Id && (o.Flags & 1) != 0) && !owner.WorldObjects.Any(o => o.Id == drop.Id), "resync keeps chest broken and picked loot absent");
    Check(clients.All(c => c.InvalidFrames == 0), "all real packet frames deserialize without protocol errors");
    var oldMatch = serverMatch.MatchId;
    var victim = clients.Take(2).First(c => c != owner);
    serverMatch.Health.ApplyDamage(victim.OwnNetId, 100, owner.OwnNetId);
    Pump(() => clients.Take(2).All(c => c.Result.HasValue));
    foreach (var c in clients.Take(2)) c.Replay();
    Pump(() => clients.Take(2).All(c => c.MatchId != oldMatch && c.Phase == MatchPhase.InGame), 8000);
    Check(serverMatch.World != null && serverMatch.World.MatchId != oldMatch && serverMatch.World.Floor.Items.Count == 0, "next round resets world floor and MatchId");
    Check(serverMatch.World!.Inventory.GetSnapshot(owner.OwnNetId).Weapons.All(s => s.IsEmpty), "next round clears previous inventory");
    var report = new { mode, passed, elapsedSeconds = clock.Elapsed.TotalSeconds, realSocketClients = clients.Select(c => new { c.Transport, c.SnapshotCount, c.WorldPacketsSeen, c.InvalidFrames }) };
    if (args.Length > 1) { var path = Path.GetFullPath(args[1]); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true })); }
    Console.WriteLine($"WorldNetworkChecks {mode}: {passed.Count} passed, 0 failed");
}
finally { foreach (var c in clients) await c.DisposeAsync(); }
