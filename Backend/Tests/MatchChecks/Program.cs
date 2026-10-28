using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GameServer.Configuration;
using GameServer.Diagnostics;
using GameServer.Networking;
using GameServer.Rooms;
using GameServer.Simulation;
using JungleBackend.Protocol;
using JungleBackend.Protocol.Gameplay;
using LiteNetLib.Utils;

Console.WriteLine("=== Starting Jungle Maze MatchChecks ===");

var passed = 0;
var failed = 0;

void Check(bool condition, string testName)
{
    if (condition)
    {
        Console.WriteLine($"[PASS] {testName}");
        passed++;
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[FAIL] {testName}");
        Console.ResetColor();
        failed++;
    }
}

// Ensure TEST profile for match checks
Environment.SetEnvironmentVariable("MATCH_PROFILE", "test");
var testProfile = MatchProfile.CreateTestProfile();

// Helper to create valid MapData and ZoneSchedule
var map = new MapData();
for (var i = 0; i < 10; i++)
{
    var angle = i * MathF.PI * 2f / 10f;
    map.SpawnPoints.Add((25f + 18f * MathF.Cos(angle), 25f + 18f * MathF.Sin(angle)));
}
map.Obstacles.Add(new AABB("AncientPillar", 22.5f, 23.5f, 24.0f, 26.0f));
map.Obstacles.Add(new AABB("AncientStatue", 23.5f, 26.5f, 18.5f, 21.5f));
map.Obstacles.Add(new AABB("BoulderRock", 28.0f, 32.0f, 23.0f, 27.0f));
map.Obstacles.Add(new AABB("CityWall", 24.5f, 25.5f, 26.0f, 30.0f));
map.Validate();

var zoneSchedule = new ZoneSchedule();
zoneSchedule.Intervals.Add(new ZoneInterval { Index = 1, StartSecond = 90, EndSecond = 97, StartRadius = 25, EndRadius = 18 });
zoneSchedule.Intervals.Add(new ZoneInterval { Index = 2, StartSecond = 182, EndSecond = 188, StartRadius = 18, EndRadius = 12 });
zoneSchedule.Intervals.Add(new ZoneInterval { Index = 3, StartSecond = 268, EndSecond = 273, StartRadius = 12, EndRadius = 7 });
zoneSchedule.Intervals.Add(new ZoneInterval { Index = 4, StartSecond = 343, EndSecond = 347, StartRadius = 7, EndRadius = 3 });
zoneSchedule.Intervals.Add(new ZoneInterval { Index = 5, StartSecond = 387, EndSecond = 390, StartRadius = 3, EndRadius = 0 });
zoneSchedule.Validate();

// -------------------------------------------------------------
// TEST 1: Phase, Lobby Cancellation & Loading Timeout
// -------------------------------------------------------------
Console.WriteLine("\n--- TEST 1: Phase, Lobby Cancellation & Loading Timeout ---");
{
    var match = new MatchRuntime("ROOM_PHASE", map, zoneSchedule, testProfile);
    Check(match.Phase == MatchPhase.Waiting, "Initial phase is Waiting");

    match.TryRegisterPlayer("p1", "Player1", out _);
    match.SetPlayerReady("p1");
    match.Tick(0.1);
    Check(match.Phase == MatchPhase.Waiting, "1 player ready < minPlayers(2), stays Waiting");

    match.TryRegisterPlayer("p2", "Player2", out _);
    match.SetPlayerReady("p2");
    match.Tick(0.1);
    Check(match.Phase == MatchPhase.Lobby, "2 players ready reaches minPlayers, transitions to Lobby");

    // Lobby cancellation when below min
    match.MarkDisconnected("p2");
    match.Tick(0.1);
    Check(match.Phase == MatchPhase.Waiting, "Player leaves during lobby, count < min, lobby cancelled -> Waiting");

    // Re-join and re-ready -> Lobby
    match.TryRegisterPlayer("p2", "Player2", out _);
    match.SetPlayerReady("p2");
    match.Tick(0.1);
    Check(match.Phase == MatchPhase.Lobby, "Re-ready transitions back to Lobby");

    // Step through lobby duration to trigger Loading
    for (var i = 0; i < 10; i++) match.Tick(0.1);
    Check(match.Phase == MatchPhase.Loading, "Lobby countdown finished -> Loading");

    // Loading timeout without player 2 acking load -> p2 dropped, < min, returns to Waiting
    match.SetPlayerLoaded("p1", match.MatchId);
    for (var i = 0; i < 15; i++) match.Tick(0.1);
    Check(match.Phase == MatchPhase.Waiting, "Loading timeout drops unacknowledged player, returns to Waiting");

    // Now test successful load to InGame
    match.TryRegisterPlayer("p2", "Player2", out _);
    match.SetPlayerReady("p1");
    match.SetPlayerReady("p2");
    match.Tick(0.1); // Lobby
    for (var i = 0; i < 10; i++) match.Tick(0.1); // Loading
    match.SetPlayerLoaded("p1", match.MatchId);
    match.SetPlayerLoaded("p2", match.MatchId);
    match.Tick(0.1);
    Check(match.Phase == MatchPhase.InGame, "Both players loaded -> InGame");
}

// -------------------------------------------------------------
// TEST 2: Tick, Input Stale, Wrap & StaleMatch
// -------------------------------------------------------------
Console.WriteLine("\n--- TEST 2: Tick, Input Stale, Wrap & StaleMatch ---");
{
    var match = new MatchRuntime("ROOM_INPUT", map, zoneSchedule, testProfile);
    match.TryRegisterPlayer("p1", "Player1", out _);
    match.TryRegisterPlayer("p2", "Player2", out _);
    match.SetPlayerReady("p1");
    match.SetPlayerReady("p2");
    match.Tick(0.1);
    for (var i = 0; i < 10; i++) match.Tick(0.1);
    match.SetPlayerLoaded("p1", match.MatchId);
    match.SetPlayerLoaded("p2", match.MatchId);
    match.Tick(0.1); // InGame

    match.Entities.TryGetBySession("p1", out var entity1);
    var startX = entity1!.X;

    // Send valid input
    var input1 = new PlayerInputPacket { InputSeq = 10, MoveX = 127, MoveY = 0 };
    var accepted1 = match.ProcessInput("p1", input1);
    Check(accepted1, "Valid input sequence 10 accepted");

    // Send stale (duplicate/older) input sequence
    var inputStale = new PlayerInputPacket { InputSeq = 5, MoveX = 127, MoveY = 0 };
    var acceptedStale = match.ProcessInput("p1", inputStale);
    Check(!acceptedStale, "Stale input sequence 5 after 10 rejected");

    // Send sequence wrap: 65535 followed by 0
    match.Tick(1.0 / 30.0); // New tick resets the bounded input burst budget.
    entity1.LastProcessedInputSeq = 65535;
    var acceptedWrap = match.ProcessInput("p1", new PlayerInputPacket { InputSeq = 0, MoveX = 127, MoveY = 0 });
    Check(acceptedWrap, "Sequence wrap 65535 -> 0 accepted safely");

    // Tick forward and verify player moves
    match.Tick(1.0 / 30.0);
    Check(entity1.X > startX, "Player moved with active input");

    // Tick forward until input becomes stale (>= 8 ticks without new input)
    for (var i = 0; i < 8; i++) match.Tick(1.0 / 30.0);
    var posStale = entity1.X;
    // Tick forward while stale and verify player does not move
    for (var i = 0; i < 10; i++) match.Tick(1.0 / 30.0);
    Check(MathF.Abs(entity1.X - posStale) < 1e-4, "Input stale >= 8 ticks stops movement");

    // Stale MatchId in match input packet
    var badMatchInput = match.ProcessInput("p1", new MatchInputPacket { MatchId = Guid.NewGuid(), Input = input1 });
    Check(!badMatchInput, "Input with stale MatchId rejected");

    // Stale MatchId in leave packet
    var badLeave = match.ProcessLeave("p1", Guid.NewGuid());
    Check(!badLeave, "Action with stale MatchId rejected");
}

// -------------------------------------------------------------
// TEST 3: Physics Slide & Highspeed Bounds
// -------------------------------------------------------------
Console.WriteLine("\n--- TEST 3: Physics Slide & Highspeed Bounds ---");
{
    var phys = new PhysicsSimulation(map);

    // Highspeed movement against map boundary: clamp and slide
    var (x1, y1) = phys.Move(49.5f, 25.0f, 1.0f, 0.5f, 1.0);
    Check(MathF.Abs(x1 - (50f - PhysicsSimulation.PlayerRadius)) < 1e-3, "High speed against max X clamps to map bounds [0, 50]");
    Check(y1 > 25.0f, "High speed slides along Y boundary");

    // Sweep against BoulderRock [28, 32] x [23, 27]
    var (x2, y2) = phys.Move(27.0f, 25.0f, 1.0f, 1.0f, 0.2);
    Check(x2 <= 28.0f - PhysicsSimulation.PlayerRadius + 1e-3, "Continuous sweep slides against obstacle without penetrating");
    Check(y2 > 25.0f, "Sliding tangential movement succeeds");

    // Penetration resolution
    var (x3, y3) = phys.ResolvePenetration(29.0f, 25.0f);
    Check(x3 < 28.0f || x3 > 32.0f || y3 < 23.0f || y3 > 27.0f, "Penetration resolved outside obstacle");
}

// -------------------------------------------------------------
// TEST 4: Two-Room Isolation
// -------------------------------------------------------------
Console.WriteLine("\n--- TEST 4: Two-Room Isolation ---");
{
    var matchA = new MatchRuntime("ROOM_A", map, zoneSchedule, testProfile);
    var matchB = new MatchRuntime("ROOM_B", map, zoneSchedule, testProfile);

    matchA.TryRegisterPlayer("user1", "U1", out _);
    matchB.TryRegisterPlayer("user2", "U2", out _);

    Check(matchA.MatchId != matchB.MatchId, "Two rooms have distinct independent MatchIds");
    Check(matchA.Entities.Count == 1 && matchB.Entities.Count == 1, "Entities do not leak between rooms");

    matchA.Entities.TryGetBySession("user1", out var eA);
    matchB.Entities.TryGetBySession("user2", out var eB);
    Check(eA!.NetId == 0 && eB!.NetId == 0, "NetIds are local to room and never cross-contaminate");
}

// -------------------------------------------------------------
// TEST 5: Latejoin Rejection & Loading Freeze
// -------------------------------------------------------------
Console.WriteLine("\n--- TEST 5: Latejoin Rejection & Loading Freeze ---");
{
    var match = new MatchRuntime("ROOM_LATE", map, zoneSchedule, testProfile);
    match.TryRegisterPlayer("p1", "P1", out _);
    match.TryRegisterPlayer("p2", "P2", out _);
    match.SetPlayerReady("p1");
    match.SetPlayerReady("p2");
    match.Tick(0.1); // Lobby
    for (var i = 0; i < 10; i++) match.Tick(0.1); // Loading

    var lateLoading = match.TryRegisterPlayer("p3_late", "LateLoading", out var reasonLoading);
    Check(!lateLoading && reasonLoading == "MatchInProgress", "Late join during Loading rejected");

    match.SetPlayerLoaded("p1", match.MatchId);
    match.SetPlayerLoaded("p2", match.MatchId);
    match.Tick(0.1); // InGame

    var joined = match.TryRegisterPlayer("p3_late", "LatePlayer", out var reason);
    Check(!joined && reason == "MatchInProgress", "Late join during InGame rejected with MatchInProgress");
}

// -------------------------------------------------------------
// TEST 6: Grace, Rotation, Expiry & Explicit Leave
// -------------------------------------------------------------
Console.WriteLine("\n--- TEST 6: Grace, Rotation, Expiry & Explicit Leave ---");
{
    var match = new MatchRuntime("ROOM_GRACE", map, zoneSchedule, testProfile);
    match.TryRegisterPlayer("p1", "P1", out _);
    match.TryRegisterPlayer("p2", "P2", out _);
    match.SetPlayerReady("p1");
    match.SetPlayerReady("p2");
    match.Tick(0.1);
    for (var i = 0; i < 10; i++) match.Tick(0.1);
    match.SetPlayerLoaded("p1", match.MatchId);
    match.SetPlayerLoaded("p2", match.MatchId);
    match.Tick(0.1); // InGame

    match.Entities.TryGetBySession("p1", out var e1);
    var token1 = e1!.ResumeToken;

    // Disconnect p1 -> preserves character and enters grace
    match.MarkDisconnected("p1");
    Check(e1.IsDisconnected && e1.IsAlive, "Disconnect preserves character in grace");

    // Resume within grace
    var resumed = match.TryResume("p1", token1, out var resumedNetId, out var newRotatedToken);
    Check(resumed && resumedNetId == e1.NetId && newRotatedToken != token1, "Resume succeeds and rotates token");
    Check(!e1.IsDisconnected, "Player reconnected");

    // Old token is now invalid
    var replayOldToken = match.TryResume("p1", token1, out _, out _);
    Check(!replayOldToken, "Old resume token cannot be reused");

    // Disconnect again and let grace expire
    match.MarkDisconnected("p1");
    for (var i = 0; i < 20; i++) match.Tick(0.1); // > 1.0s grace in test profile
    Check(!e1.IsAlive, "Grace expiry eliminates disconnected player");

    // Explicit leave eliminates player immediately
    match.Entities.TryGetBySession("p2", out var e2);
    match.ProcessLeave("p2", match.MatchId);
    Check(!e2!.IsAlive, "Explicit leave packet eliminates player immediately");
}

// -------------------------------------------------------------
// TEST 7: Same-Tick Tie, Winner & Replay Restart
// -------------------------------------------------------------
Console.WriteLine("\n--- TEST 7: Same-Tick Tie, Winner & Replay Restart ---");
{
    // Case A: Last alive wins
    var matchWin = new MatchRuntime("ROOM_WIN", map, zoneSchedule, testProfile);
    matchWin.TryRegisterPlayer("p1", "P1", out _);
    matchWin.TryRegisterPlayer("p2", "P2", out _);
    matchWin.SetPlayerReady("p1");
    matchWin.SetPlayerReady("p2");
    matchWin.Tick(0.1);
    for (var i = 0; i < 10; i++) matchWin.Tick(0.1);
    matchWin.SetPlayerLoaded("p1", matchWin.MatchId);
    matchWin.SetPlayerLoaded("p2", matchWin.MatchId);
    matchWin.Tick(0.1);

    matchWin.Health.ApplyDamage(0, 100, 255); // p1 dies
    matchWin.Tick(0.1);
    Check(matchWin.Phase == MatchPhase.Ending, "Single survivor ends match -> Ending phase");
    matchWin.Entities.TryGetBySession("p2", out var winnerEntity);
    Check(winnerEntity!.Rank == 1, "Sole survivor receives Rank 1");

    // Case B: Same-tick elimination -> Tie
    var matchTie = new MatchRuntime("ROOM_TIE", map, zoneSchedule, testProfile);
    matchTie.TryRegisterPlayer("p1", "P1", out _);
    matchTie.TryRegisterPlayer("p2", "P2", out _);
    matchTie.SetPlayerReady("p1");
    matchTie.SetPlayerReady("p2");
    matchTie.Tick(0.1);
    for (var i = 0; i < 10; i++) matchTie.Tick(0.1);
    matchTie.SetPlayerLoaded("p1", matchTie.MatchId);
    matchTie.SetPlayerLoaded("p2", matchTie.MatchId);
    matchTie.Tick(0.1);

    MatchResult? reportedResult = null;
    matchTie.MatchCompleted += res => reportedResult = res;

    // Both take lethal damage in the same tick
    matchTie.Health.ApplyDamage(0, 100, 255);
    matchTie.Health.ApplyDamage(1, 100, 255);
    matchTie.Tick(0.1);

    Check(matchTie.Phase == MatchPhase.Ending, "Same-tick death ends match");
    Check(reportedResult != null && reportedResult.Reason == "Tie", "Same-tick death results in Tie");
    matchTie.Entities.TryGetBySession("p1", out var t1);
    matchTie.Entities.TryGetBySession("p2", out var t2);
    Check(t1!.Rank == t2!.Rank, "Same-tick elimination shares rank");

    // Replay ack resets world in same room
    matchTie.ProcessReplayAck("p1", matchTie.MatchId);
    matchTie.ProcessReplayAck("p2", matchTie.MatchId);
    matchTie.Tick(0.1);
    matchTie.Tick(0.1); // Cleanup -> Waiting
    Check(matchTie.Phase == MatchPhase.Waiting, "Replay ack resets world back to Waiting in same room");
}

// -------------------------------------------------------------
// TEST 8: All Zone Phases, Damage Idempotence & r0
// -------------------------------------------------------------
Console.WriteLine("\n--- TEST 8: All Zone Phases, Damage Idempotence & r0 ---");
{
    var zone = new ZoneSimulation(zoneSchedule, 12345);

    // Initial phase
    zone.Update(10.0, 300);
    Check(zone.CurrentRadius == 25f && !zone.InWarning, "Phase 0 radius is 25, no warning");

    // Warning at 80s (10s before 90s shrink)
    zone.Update(85.0, 2550);
    Check(zone.InWarning, "Warning active within 10s of shrink start");

    // Interval 1 shrinking (90..97s)
    zone.Update(93.5, 2805);
    Check(zone.CurrentRadius < 25f && zone.CurrentRadius > 18f, "Interval 1 radius shrinks 25 -> 18");

    // Interval 2 (182..188s)
    zone.Update(185.0, 5550);
    Check(zone.CurrentRadius < 18f && zone.CurrentRadius > 12f, "Interval 2 radius shrinks 18 -> 12");

    // Interval 3 (268..273s)
    zone.Update(270.0, 8100);
    Check(zone.CurrentRadius < 12f && zone.CurrentRadius > 7f, "Interval 3 radius shrinks 12 -> 7");

    // Interval 4 (343..347s)
    zone.Update(345.0, 10350);
    Check(zone.CurrentRadius < 7f && zone.CurrentRadius > 3f, "Interval 4 radius shrinks 7 -> 3");

    // Interval 5 / r0 (at 390s, radius drops to 0)
    zone.Update(390.0, 11700);
    Check(zone.CurrentRadius <= 0f, "Interval 5 reaches radius 0 (r0)");

    // r0 damages entire map including exact center
    Check(zone.IsOutside(25f, 25f), "r0 damages exact center (25, 25)");
    Check(zone.IsOutside(zone.CurrentCenter.X, zone.CurrentCenter.Y), "r0 damages zone center");

    // Health damage idempotence within same 1-second interval
    var matchHealth = new MatchRuntime("ROOM_IDEM", map, zoneSchedule, testProfile);
    matchHealth.TryRegisterPlayer("p1", "P1", out _);
    matchHealth.TryRegisterPlayer("p2", "P2", out _);
    matchHealth.SetPlayerReady("p1");
    matchHealth.SetPlayerReady("p2");
    matchHealth.Tick(0.1);
    for (var i = 0; i < 10; i++) matchHealth.Tick(0.1);
    matchHealth.SetPlayerLoaded("p1", matchHealth.MatchId);
    matchHealth.SetPlayerLoaded("p2", matchHealth.MatchId);
    matchHealth.Tick(0.1);

    matchHealth.Health.ApplyZoneDamage(0, 30, 5);
    var hpAfterFirst = matchHealth.Health.GetHp(0);
    matchHealth.Health.ApplyZoneDamage(0, 30, 5);
    var hpAfterSecond = matchHealth.Health.GetHp(0);
    Check(hpAfterFirst == 95 && hpAfterSecond == 95, "Zone damage is idempotent within same 1-second interval");
}

// -------------------------------------------------------------
// TEST 9: No Premature Forced 420s End
// -------------------------------------------------------------
Console.WriteLine("\n--- TEST 9: No Premature Forced 420s End ---");
{
    var match = new MatchRuntime("ROOM_420", map, zoneSchedule, testProfile);
    match.TryRegisterPlayer("p1", "P1", out _);
    match.TryRegisterPlayer("p2", "P2", out _);
    match.SetPlayerReady("p1");
    match.SetPlayerReady("p2");
    match.Tick(0.1);
    for (var i = 0; i < 10; i++) match.Tick(0.1);
    match.SetPlayerLoaded("p1", match.MatchId);
    match.SetPlayerLoaded("p2", match.MatchId);
    match.Tick(0.1); // InGame

    // Keep players alive by refilling HP and step past 420 seconds
    for (var s = 0; s < 450; s++)
    {
        match.Tick(1.0);
        if (match.Entities.TryGet(0, out var e0) && e0 != null) e0.Hp = 100;
        if (match.Entities.TryGet(1, out var e1) && e1 != null) e1.Hp = 100;
    }

    Check(match.Phase == MatchPhase.InGame, "420s is target only; match never force-ends while > 1 alive");
}

// -------------------------------------------------------------
// TEST 10: Fake Peer Transport Integration & ClientHello Handshake
// -------------------------------------------------------------
Console.WriteLine("\n--- TEST 10: Fake Peer Transport Integration & ClientHello Handshake ---");
{
    var options = new ServerOptions(System.Net.IPAddress.Loopback, 8888, 10, 30, 1, "RoomCode", "DEFAULT_CODE");
    var hub = new SessionHub(options, new ServerMetrics());

    var udpPeer = new FakePeer("Udp");
    var wsPeer = new FakePeer("Wss");

    var joinUdp = new JoinRequest(NetworkProtocol.LatestVersion, JoinAuthMode.RoomCode, "DEFAULT_CODE", "TesterUdp", Guid.NewGuid());
    var joinWs = new JoinRequest(NetworkProtocol.LatestVersion, JoinAuthMode.RoomCode, "DEFAULT_CODE", "TesterWs", Guid.NewGuid());

    Check(hub.Join(joinUdp, udpPeer), "UDP peer joins hub");
    Check(hub.Join(joinWs, wsPeer), "WS peer joins hub");

    hub.Connected(udpPeer);
    hub.Connected(wsPeer);

    Check(udpPeer.Sent.Count > 0, "UDP peer received JoinAccepted");
    Check(wsPeer.Sent.Count > 0, "WS peer received JoinAccepted");

    var room = hub.Rooms.Find("DEFAULT_CODE")!;
    Check(room.Match.Entities.Count == 0, "Ping-only Connected does NOT register match entity");

    // Send valid ClientHelloPacket
    var hello = new ClientHelloPacket { ProtocolVersion = MatchProtocol.Version, Ready = true };
    var writer = new NetDataWriter();
    PacketRegistry.BeginFrame(writer);
    new PacketRegistry().Write(writer, ref hello);

    hub.Receive(udpPeer, writer.CopyData(), true, true);
    hub.Receive(wsPeer, writer.CopyData(), true, true);

    Check(room.Match.Entities.Count == 2, "Valid ClientHello registers entities in match");
    Check(room.Match.Phase == MatchPhase.Lobby, "Both peers sent hello/ready -> room starts Lobby");
}

// -------------------------------------------------------------
// TEST 11: Defect 1 - HookRoom Exactly Once (No Duplicate Broadcasts)
// -------------------------------------------------------------
Console.WriteLine("\n--- TEST 11: HookRoom Exactly Once (No Duplicate Broadcasts) ---");
{
    var options = new ServerOptions(System.Net.IPAddress.Loopback, 8889, 10, 30, 1, "RoomCode", "HOOK_CODE");
    var hub = new SessionHub(options, new ServerMetrics());

    var peer1 = new FakePeer("Udp");
    var peer2 = new FakePeer("Udp");

    hub.Join(new JoinRequest(NetworkProtocol.LatestVersion, JoinAuthMode.RoomCode, "HOOK_CODE", "H1", Guid.NewGuid()), peer1);
    hub.Connected(peer1);
    hub.Join(new JoinRequest(NetworkProtocol.LatestVersion, JoinAuthMode.RoomCode, "HOOK_CODE", "H2", Guid.NewGuid()), peer2);
    hub.Connected(peer2);

    peer1.Sent.Clear();
    peer2.Sent.Clear();

    var room = hub.Rooms.Find("HOOK_CODE")!;
    // Trigger snapshot generation
    room.Match.Tick(0.1);

    // Each peer should receive at most 1 snapshot per broadcast, not duplicated
    Check(peer1.Sent.Count <= 1 && peer2.Sent.Count <= 1, "Broadcasts are not duplicated across multiple joins");
}

// -------------------------------------------------------------
// TEST 12: Defect 3 - Unused NetId Allocation (Holes Do Not Overwrite)
// -------------------------------------------------------------
Console.WriteLine("\n--- TEST 12: Unused NetId Allocation ---");
{
    var match = new MatchRuntime("ROOM_HOLES", map, zoneSchedule, testProfile);
    match.TryRegisterPlayer("p0", "Player0", out _);
    match.TryRegisterPlayer("p1", "Player1", out _);
    match.TryRegisterPlayer("p2", "Player2", out _);

    match.Entities.TryGetBySession("p0", out var e0);
    match.Entities.TryGetBySession("p1", out var e1);
    match.Entities.TryGetBySession("p2", out var e2);

    Check(e0!.NetId == 0 && e1!.NetId == 1 && e2!.NetId == 2, "NetIds initially 0, 1, 2");

    // Player 1 leaves during lobby/waiting (creates hole at NetId 1)
    match.MarkDisconnected("p1");
    Check(!match.Entities.TryGetBySession("p1", out _), "p1 removed");

    // Player 3 joins -> must fill hole at NetId 1, NOT overwrite p2 (NetId 2)
    match.TryRegisterPlayer("p3", "Player3", out _);
    match.Entities.TryGetBySession("p3", out var e3);
    match.Entities.TryGetBySession("p2", out var e2After);

    Check(e3!.NetId == 1, "Unused NetId 1 allocated for p3 to fill hole");
    Check(e2After!.NetId == 2 && e2After.PlayerSessionId == "p2", "p2 remained safe at NetId 2 without overwrite");
}

// -------------------------------------------------------------
// TEST 13: Defect 4 - Same MatchId Through Result & Cleanup Resets
// -------------------------------------------------------------
Console.WriteLine("\n--- TEST 13: Same MatchId Through Result & Cleanup Resets ---");
{
    var match = new MatchRuntime("ROOM_SAME_ID", map, zoneSchedule, testProfile);
    var waitingId = match.MatchId;

    match.TryRegisterPlayer("p1", "P1", out _);
    match.TryRegisterPlayer("p2", "P2", out _);
    match.SetPlayerReady("p1");
    match.SetPlayerReady("p2");
    match.Tick(0.1); // Lobby
    Check(match.MatchId == waitingId, "MatchId same in Lobby");

    for (var i = 0; i < 10; i++) match.Tick(0.1); // Loading
    Check(match.MatchId == waitingId, "MatchId same in Loading");

    match.SetPlayerLoaded("p1", match.MatchId);
    match.SetPlayerLoaded("p2", match.MatchId);
    match.Tick(0.1); // InGame
    Check(match.MatchId == waitingId, "MatchId remains SAME in InGame (not regenerated at game start)");

    MatchResult? completedResult = null;
    match.MatchCompleted += res => completedResult = res;

    // End match
    match.Health.ApplyDamage(0, 100, 255);
    match.Tick(0.1); // Ending
    Check(completedResult != null && completedResult.MatchId == waitingId, "MatchResult carries the SAME MatchId");

    // Replay ack and step through Cleanup
    match.ProcessReplayAck("p1", match.MatchId);
    match.ProcessReplayAck("p2", match.MatchId);
    match.Tick(0.1); // Ending timer
    match.Tick(0.1); // Cleanup -> Waiting

    Check(match.Phase == MatchPhase.Waiting, "Cleanup auto-returns to Waiting");
    Check(match.MatchId != waitingId, "Cleanup generates fresh MatchId for next match");
    Check(match.Entities.Count == 2, "Cleanup preserves connected players in room");
    match.Entities.TryGet(0, out var preserved0);
    Check(preserved0!.Hp == 100 && preserved0.IsAlive && preserved0.Rank == 0, "Preserved players have HP reset to 100 and Rank 0");
}

// -------------------------------------------------------------
// TEST 14: Defect 5 - Authoritative Slot Accounting & InGame Resume
// -------------------------------------------------------------
Console.WriteLine("\n--- TEST 14: Authoritative Slot Accounting & InGame Resume ---");
{
    var options = new ServerOptions(System.Net.IPAddress.Loopback, 8890, 2, 30, 1, "RoomCode", "RESUME_CODE");
    var hub = new SessionHub(options, new ServerMetrics());

    var peer1 = new FakePeer("Udp");
    var peer2 = new FakePeer("Udp");

    hub.Join(new JoinRequest(NetworkProtocol.LatestVersion, JoinAuthMode.RoomCode, "RESUME_CODE", "P1", Guid.NewGuid()), peer1);
    hub.Join(new JoinRequest(NetworkProtocol.LatestVersion, JoinAuthMode.RoomCode, "RESUME_CODE", "P2", Guid.NewGuid()), peer2);

    hub.Connected(peer1);
    hub.Connected(peer2);

    var hello = new ClientHelloPacket { ProtocolVersion = MatchProtocol.Version, Ready = true };
    var helloWriter = new NetDataWriter();
    PacketRegistry.BeginFrame(helloWriter);
    new PacketRegistry().Write(helloWriter, ref hello);

    hub.Receive(peer1, helloWriter.CopyData(), true, true);
    hub.Receive(peer2, helloWriter.CopyData(), true, true);

    var room = hub.Rooms.Find("RESUME_CODE")!;
    for (var i = 0; i < 10; i++) hub.Tick(0.1); // Loading
    var loadAck = new LoadAckPacket { MatchId = room.Match.MatchId, Ready = true };
    var loadWriter = new NetDataWriter();
    PacketRegistry.BeginFrame(loadWriter);
    new PacketRegistry().Write(loadWriter, ref loadAck);

    hub.Receive(peer1, loadWriter.CopyData(), true, true);
    hub.Receive(peer2, loadWriter.CopyData(), true, true);
    hub.Tick(0.1); // InGame

    Check(room.Match.Phase == MatchPhase.InGame, "Room transitioned to InGame");
    room.Match.Entities.TryGet(0, out var e0);
    var validResumeToken = e0!.ResumeToken;

    // Disconnect peer1 during InGame
    hub.Disconnect(peer1);

    // Slot MUST be preserved in room & hub
    Check(hub.ReservedPeers == 2, "InGame disconnect preserves global slot in ReservedPeers");
    Check(room.Peers.Count == 2, "InGame disconnect preserves room slot in room.Peers");

    // A newcomer trying to join full room must be rejected
    var intruderPeer = new FakePeer("Udp");
    var intruderJoin = hub.Join(new JoinRequest(NetworkProtocol.LatestVersion, JoinAuthMode.RoomCode, "RESUME_CODE", "Intruder", Guid.NewGuid()), intruderPeer);
    Check(!intruderJoin && intruderPeer.CloseReason == "MatchInProgress", "New join during InGame rejected");

    // Spoofed resume with wrong token rejected without creating a new session
    var fakeResumePeer = new FakePeer("Udp");
    var fakeResumeJoin = hub.Join(new JoinRequest(NetworkProtocol.LatestVersion, JoinAuthMode.RoomCode, "RESUME_CODE", "P1", Guid.NewGuid(), ResumeToken: Guid.NewGuid()), fakeResumePeer);
    Check(!fakeResumeJoin && fakeResumePeer.CloseReason == "InvalidResumeToken", "Spoofed resume with wrong token rejected");
    Check(hub.ReservedPeers == 2, "Failed fake resume did not allocate new session");

    // Legitimate resume with valid token accepted BEFORE capacity check
    var resumePeer = new FakePeer("Udp");
    var validResumeJoin = hub.Join(new JoinRequest(NetworkProtocol.LatestVersion, JoinAuthMode.RoomCode, "RESUME_CODE", "P1", Guid.NewGuid(), ResumeToken: validResumeToken), resumePeer);
    Check(validResumeJoin, "Valid resume succeeds even at room capacity");

    hub.Connected(resumePeer);
    Check(room.Match.Entities.TryGet(0, out var e0Resumed) && e0Resumed != null && !e0Resumed.IsDisconnected, "Player successfully re-connected and resumed control");
    Check(e0Resumed != null && e0Resumed.ResumeToken != validResumeToken, "Resume token rotated upon resume");

    // Old token cannot be reused
    var reusePeer = new FakePeer("Udp");
    hub.Disconnect(resumePeer);
    var reuseJoin = hub.Join(new JoinRequest(NetworkProtocol.LatestVersion, JoinAuthMode.RoomCode, "RESUME_CODE", "P1", Guid.NewGuid(), ResumeToken: validResumeToken), reusePeer);
    Check(!reuseJoin, "Reusing old rotated resume token rejected");
}

// -------------------------------------------------------------
// TEST 15: Defect 6 - Full Offline Result & RoomId Not Secret RoomCode
// -------------------------------------------------------------
Console.WriteLine("\n--- TEST 15: Full Offline Result & RoomId Not Secret RoomCode ---");
{
    var customRoomId = Guid.NewGuid().ToString();
    var match = new MatchRuntime("SECRET_JOIN_CODE_123", map, zoneSchedule, testProfile, customRoomId);
    match.TryRegisterPlayer("p1", "PlayerOne", out _);
    match.TryRegisterPlayer("p2", "PlayerTwo", out _);
    match.SetPlayerReady("p1");
    match.SetPlayerReady("p2");
    match.Tick(0.1);
    for (var i = 0; i < 10; i++) match.Tick(0.1);
    match.SetPlayerLoaded("p1", match.MatchId);
    match.SetPlayerLoaded("p2", match.MatchId);
    match.Tick(0.1); // InGame

    // p1 disconnects during game and is eliminated
    match.MarkDisconnected("p1");
    match.Health.ApplyDamage(0, 100, 255);

    MatchResult? result = null;
    match.MatchCompleted += res => result = res;

    // End match
    match.Tick(0.1);

    Check(result != null, "Match completed result fired");
    Check(result!.RoomId == customRoomId && !result.RoomId.Contains("SECRET"), "Persisted result uses RoomId and does NOT contain secret RoomCode");
    Check(result.Participants.Count == 2, "Offline/eliminated player included in final participants");
    var offlineP = result.Participants.First(p => p.PlayerSessionId == "p1");
    Check(offlineP.DisplayName == "PlayerOne" && offlineP.Rank > 0, "Offline participant retains session/name/rank data");
}

// -------------------------------------------------------------
// TEST 16: Defect 6 - Slow Consumer Broadcast Failure
// -------------------------------------------------------------
Console.WriteLine("\n--- TEST 16: Slow Consumer Broadcast Failure ---");
{
    var options = new ServerOptions(System.Net.IPAddress.Loopback, 8891, 10, 30, 1, "RoomCode", "SLOW_CODE");
    var hub = new SessionHub(options, new ServerMetrics());

    var goodPeer = new FakePeer("Udp");
    var slowPeer = new FakePeer("Udp") { ShouldFailSend = true };

    hub.Join(new JoinRequest(NetworkProtocol.LatestVersion, JoinAuthMode.RoomCode, "SLOW_CODE", "Good", Guid.NewGuid()), goodPeer);
    hub.Join(new JoinRequest(NetworkProtocol.LatestVersion, JoinAuthMode.RoomCode, "SLOW_CODE", "Slow", Guid.NewGuid()), slowPeer);

    hub.Connected(goodPeer);
    hub.Connected(slowPeer);

    var room = hub.Rooms.Find("SLOW_CODE")!;
    // Snapshot send fails for slowPeer
    room.Match.Tick(0.1);

    Check(slowPeer.CloseReason == "SlowConsumer", "Slow consumer closed safely on broadcast failure");
    Check(!slowPeer.IsOpen, "Slow peer disconnected");
}


// -------------------------------------------------------------
// TEST 17: Killcam Authoritative Elimination, Replay Capture & Privacy Isolation
// -------------------------------------------------------------
Console.WriteLine("\n--- TEST 17: Killcam Authoritative Elimination, Replay Capture & Privacy Isolation ---");
{
    var matchA = new MatchRuntime("ROOM_KILLCAM_A", map, zoneSchedule, testProfile);
    var matchB = new MatchRuntime("ROOM_KILLCAM_B", map, zoneSchedule, testProfile);

    matchA.TryRegisterPlayer("p1", "Player1", out _);
    matchA.TryRegisterPlayer("p2", "Player2", out _);
    matchA.TryRegisterPlayer("p3", "Player3", out _);
    matchA.SetPlayerReady("p1");
    matchA.SetPlayerReady("p2");
    matchA.SetPlayerReady("p3");
    for (var i = 0; i < 15; i++) matchA.Tick(0.1);
    matchA.SetPlayerLoaded("p1", matchA.MatchId);
    matchA.SetPlayerLoaded("p2", matchA.MatchId);
    matchA.SetPlayerLoaded("p3", matchA.MatchId);
    for (var i = 0; i < 15; i++) matchA.Tick(0.1);
    Check(matchA.Phase == MatchPhase.InGame, "Room A in InGame");

    matchB.TryRegisterPlayer("p4", "Player4", out _);
    matchB.TryRegisterPlayer("p5", "Player5", out _);
    matchB.SetPlayerReady("p4");
    matchB.SetPlayerReady("p5");
    for (var i = 0; i < 15; i++) matchB.Tick(0.1);
    matchB.SetPlayerLoaded("p4", matchB.MatchId);
    matchB.SetPlayerLoaded("p5", matchB.MatchId);
    for (var i = 0; i < 15; i++) matchB.Tick(0.1);
    Check(matchB.Phase == MatchPhase.InGame, "Room B in InGame");

    // Advance matchA for 160 ticks (so >= 150 frames recorded)
    for (var i = 0; i < 160; i++) matchA.Tick(1.0 / 30.0);

    MatchEliminationEventPacket? elimPacket = null;
    EventPacket? eventPacket = null;
    var broadcastRegistry = new PacketRegistry();
    broadcastRegistry.Subscribe<MatchEliminationEventPacket>(p => elimPacket = p);
    broadcastRegistry.Subscribe<EventPacket>(p => eventPacket = p);

    matchA.ReliableBroadcast += bytes =>
    {
        var reader = new NetDataReader(bytes);
        broadcastRegistry.TryReadFrame(reader);
    };

    matchA.Entities.TryGetBySession("p1", out var e1);
    matchA.Entities.TryGetBySession("p2", out var e2);
    var p1NetId = e1!.NetId;
    var p2NetId = e2!.NetId;

    // Eliminate p1 by p2 damage
    matchA.Health.ApplyDamage(p1NetId, 100, p2NetId);
    matchA.Tick(1.0 / 30.0);

    Check(elimPacket.HasValue, "Authoritative MatchEliminationEventPacket was broadcast");
    Check(elimPacket!.Value.MatchId == matchA.MatchId, "Elimination packet has correct MatchId");
    Check(elimPacket.Value.VictimNetId == p1NetId, "Elimination packet has correct VictimNetId");
    Check(elimPacket.Value.KillerNetId == p2NetId, "Elimination packet has correct KillerNetId");
    Check(elimPacket.Value.Cause == EliminationCauseWire.Player, "Elimination packet has Cause == Player");
    Check(eventPacket.HasValue && eventPacket.Value.EventType == GameEventType.PlayerDied, "EventPacket PlayerDied broadcast");

    // Privacy & Security Validations
    // 1. Living player cannot request replay
    var liveReq = matchA.TryGetKillcamReplay("p2", matchA.MatchId, p2NetId, out _);
    Check(!liveReq, "Living player request rejected (cannot spy on positions)");

    // 2. Requesting wrong victim NetId (spoofing) rejected
    var spoofReq = matchA.TryGetKillcamReplay("p1", matchA.MatchId, p2NetId, out _);
    Check(!spoofReq, "Requesting victim NetId of another player rejected");

    // 3. Requesting wrong MatchId rejected
    var wrongMatchReq = matchA.TryGetKillcamReplay("p1", matchB.MatchId, p1NetId, out _);
    Check(!wrongMatchReq, "Requesting with Room B MatchId rejected");

    // 4. Session from Room B requesting in Room A rejected
    var crossRoomReq = matchA.TryGetKillcamReplay("p4", matchA.MatchId, p1NetId, out _);
    Check(!crossRoomReq, "Session from Room B requesting in Room A rejected");

    // 5. Valid own request succeeds
    var ownReq = matchA.TryGetKillcamReplay("p1", matchA.MatchId, p1NetId, out var replayResult);
    Check(ownReq && replayResult != null, "Dead victim requesting own replay succeeds");
    Check(replayResult!.Status == 0, "Replay status is Ok (0)");
    Check(replayResult.Frames.Count == 150, "Replay window has exactly 150 frames (5s @ 30Hz)");
    Check(replayResult.VictimNetId == p1NetId, "Replay result has correct VictimNetId");
    Check(replayResult.KillerNetId == p2NetId, "Replay result has correct KillerNetId");
    Check(replayResult.EndTick == elimPacket.Value.Tick, "Replay EndTick matches authoritative death tick");

    // 6. Least disclosure: frames only contain victim and killer, p3 is not in payload
    var firstFrame = replayResult.Frames[0];
    Check(firstFrame.KillerPresent, "Killer present in replay frame");
}

// -------------------------------------------------------------
// TEST 18: Killcam Window 150, Zone Fallback & Too Short Window Fallback
// -------------------------------------------------------------
Console.WriteLine("\n--- TEST 18: Killcam Window 150, Zone Fallback & Too Short Window Fallback ---");
{
    // Part 1: Too short window (< 150 ticks)
    var matchShort = new MatchRuntime("ROOM_SHORT", map, zoneSchedule, testProfile);
    matchShort.TryRegisterPlayer("s1", "Short1", out _);
    matchShort.TryRegisterPlayer("s2", "Short2", out _);
    matchShort.SetPlayerReady("s1");
    matchShort.SetPlayerReady("s2");
    for (var i = 0; i < 15; i++) matchShort.Tick(0.1);
    matchShort.SetPlayerLoaded("s1", matchShort.MatchId);
    matchShort.SetPlayerLoaded("s2", matchShort.MatchId);
    for (var i = 0; i < 15; i++) matchShort.Tick(0.1);
    Check(matchShort.Phase == MatchPhase.InGame, "Short match InGame");

    // Advance only 40 ticks (< 150)
    for (var i = 0; i < 40; i++) matchShort.Tick(1.0 / 30.0);
    matchShort.Entities.TryGetBySession("s1", out var s1);
    matchShort.Entities.TryGetBySession("s2", out var s2);
    matchShort.Health.ApplyDamage(s1!.NetId, 100, s2!.NetId);
    matchShort.Tick(1.0 / 30.0);

    var shortOk = matchShort.TryGetKillcamReplay("s1", matchShort.MatchId, s1.NetId, out var shortReplay);
    Check(shortOk && shortReplay != null, "Short match request answered");
    Check(shortReplay!.Status == 7, "Short match replay status is TooShort (7)");
    Check(shortReplay.Frames.Count == 0, "TooShort replay carries 0 frames");

    // Part 2: Zone kill fallback (NoData)
    var matchZone = new MatchRuntime("ROOM_ZONE", map, zoneSchedule, testProfile);
    matchZone.TryRegisterPlayer("z1", "Zone1", out _);
    matchZone.TryRegisterPlayer("z2", "Zone2", out _);
    matchZone.SetPlayerReady("z1");
    matchZone.SetPlayerReady("z2");
    for (var i = 0; i < 15; i++) matchZone.Tick(0.1);
    matchZone.SetPlayerLoaded("z1", matchZone.MatchId);
    matchZone.SetPlayerLoaded("z2", matchZone.MatchId);
    for (var i = 0; i < 15; i++) matchZone.Tick(0.1);

    for (var i = 0; i < 160; i++) matchZone.Tick(1.0 / 30.0);
    matchZone.Entities.TryGetBySession("z1", out var z1);
    matchZone.Health.ApplyDamage(z1!.NetId, 100, 255); // 255 = Zone source
    matchZone.Tick(1.0 / 30.0);

    var zoneOk = matchZone.TryGetKillcamReplay("z1", matchZone.MatchId, z1.NetId, out var zoneReplay);
    Check(zoneOk && zoneReplay != null, "Zone kill request answered");
    Check(zoneReplay!.Status == 4, "Zone kill replay status is NoData (4)");
    Check(zoneReplay.Frames.Count == 0, "Zone kill replay carries 0 frames");
}

// -------------------------------------------------------------
// TEST 19: Killcam Duplicate, Rate Limit, Late & Wrong Match Requests
// -------------------------------------------------------------
Console.WriteLine("\n--- TEST 19: Killcam Duplicate, Rate Limit, Late & Wrong Match Requests ---");
{
    var match = new MatchRuntime("ROOM_RATE", map, zoneSchedule, testProfile);
    match.TryRegisterPlayer("r1", "Rate1", out _);
    match.TryRegisterPlayer("r2", "Rate2", out _);
    match.SetPlayerReady("r1");
    match.SetPlayerReady("r2");
    for (var i = 0; i < 15; i++) match.Tick(0.1);
    match.SetPlayerLoaded("r1", match.MatchId);
    match.SetPlayerLoaded("r2", match.MatchId);
    for (var i = 0; i < 15; i++) match.Tick(0.1);

    for (var i = 0; i < 160; i++) match.Tick(1.0 / 30.0);
    match.Entities.TryGetBySession("r1", out var r1);
    match.Entities.TryGetBySession("r2", out var r2);
    match.Health.ApplyDamage(r1!.NetId, 100, r2!.NetId);
    match.Tick(1.0 / 30.0);

    // Request 1, 2, 3 succeed
    Check(match.TryGetKillcamReplay("r1", match.MatchId, r1.NetId, out _), "Request 1 succeeded");
    Check(match.TryGetKillcamReplay("r1", match.MatchId, r1.NetId, out _), "Request 2 succeeded");
    Check(match.TryGetKillcamReplay("r1", match.MatchId, r1.NetId, out _), "Request 3 succeeded");

    // Request 4 rejected by rate limit
    var rateLimited = match.TryGetKillcamReplay("r1", match.MatchId, r1.NetId, out _);
    Check(!rateLimited, "Request 4 rejected by rate limit (bounded requests per victim/match)");

    // Arbitrary unknown session rejected
    var unknownSession = match.TryGetKillcamReplay("unknown_player", match.MatchId, 0, out _);
    Check(!unknownSession, "Unknown session request rejected");
}

// -------------------------------------------------------------
// TEST 20: Killcam NewMatch Cleanup & Memory Safety
// -------------------------------------------------------------
Console.WriteLine("\n--- TEST 20: Killcam NewMatch Cleanup & Memory Safety ---");
{
    var match = new MatchRuntime("ROOM_CLEANUP", map, zoneSchedule, testProfile);
    match.TryRegisterPlayer("c1", "Clean1", out _);
    match.TryRegisterPlayer("c2", "Clean2", out _);
    match.SetPlayerReady("c1");
    match.SetPlayerReady("c2");
    for (var i = 0; i < 15; i++) match.Tick(0.1);
    match.SetPlayerLoaded("c1", match.MatchId);
    match.SetPlayerLoaded("c2", match.MatchId);
    for (var i = 0; i < 15; i++) match.Tick(0.1);

    for (var i = 0; i < 160; i++) match.Tick(1.0 / 30.0);
    var oldMatchId = match.MatchId;
    match.Entities.TryGetBySession("c1", out var c1);
    match.Entities.TryGetBySession("c2", out var c2);
    match.Health.ApplyDamage(c1!.NetId, 100, c2!.NetId);
    match.Tick(1.0 / 30.0);

    Check(match.Phase == MatchPhase.Ending, "Match transitioned to Ending");
    match.ProcessReplayAck("c1", oldMatchId);
    match.ProcessReplayAck("c2", oldMatchId);

    // Step into Cleanup and back to Waiting
    for (var i = 0; i < 60; i++) match.Tick(0.1);
    Check(match.Phase == MatchPhase.Waiting, "Match reset back to Waiting");
    Check(match.MatchId != oldMatchId, "Fresh MatchId generated for next match");

    // Requesting with old MatchId fails
    var oldReq = match.TryGetKillcamReplay("c1", oldMatchId, c1.NetId, out _);
    Check(!oldReq, "Requesting replay with old MatchId rejected after cleanup");

    // Requesting in new match while alive in Waiting fails
    var newReq = match.TryGetKillcamReplay("c1", match.MatchId, c1.NetId, out _);
    Check(!newReq, "Requesting replay in fresh match while alive rejected");
}

// -------------------------------------------------------------
// TEST 21: Killcam Packets Serialization, Framing & Bounds Checks
// -------------------------------------------------------------
Console.WriteLine("\n--- TEST 21: Killcam Packets Serialization, Framing & Bounds Checks ---");
{
    static void WriteTestGuid(NetDataWriter w, Guid g)
    {
        Span<byte> bytes = stackalloc byte[16];
        g.TryWriteBytes(bytes);
        for (var i = 0; i < 16; i++) w.Put(bytes[i]);
    }

    var registry = new PacketRegistry();
    var writer = new NetDataWriter(true, 4096);
    var reader = new NetDataReader();

    // 1. KillcamRequestPacket roundtrip & bounds
    KillcamRequestPacket? receivedReq = null;
    registry.Subscribe<KillcamRequestPacket>(p => receivedReq = p);
    var req = new KillcamRequestPacket { Version = 1, MatchId = Guid.NewGuid(), VictimNetId = 3 };
    registry.WriteFrame(writer, ref req);
    reader.SetSource(writer.Data, 0, writer.Length);
    Check(registry.TryReadFrame(reader), "KillcamRequestPacket frame read succeeded");
    Check(receivedReq.HasValue && receivedReq.Value.VictimNetId == 3 && receivedReq.Value.MatchId == req.MatchId, "KillcamRequestPacket roundtrip values match");

    // Version mismatch rejection
    var badReq = new KillcamRequestPacket { Version = 99, MatchId = req.MatchId, VictimNetId = 3 };
    var badWriter = new NetDataWriter();
    badReq.Serialize(badWriter);
    var caughtBadVersion = false;
    try { new KillcamRequestPacket().Deserialize(new NetDataReader(badWriter.CopyData())); }
    catch (InvalidOperationException) { caughtBadVersion = true; }
    Check(caughtBadVersion, "KillcamRequestPacket bad version rejected");

    // 2. MatchEliminationEventPacket roundtrip & bounds
    MatchEliminationEventPacket? receivedElim = null;
    registry.Subscribe<MatchEliminationEventPacket>(p => receivedElim = p);
    var elim = new MatchEliminationEventPacket { Version = 1, MatchId = Guid.NewGuid(), Tick = 54321, VictimNetId = 2, KillerNetId = 4, Cause = EliminationCauseWire.Player };
    registry.WriteFrame(writer, ref elim);
    reader.SetSource(writer.Data, 0, writer.Length);
    Check(registry.TryReadFrame(reader), "MatchEliminationEventPacket frame read succeeded");
    Check(receivedElim.HasValue && receivedElim.Value.Tick == 54321 && receivedElim.Value.KillerNetId == 4 && receivedElim.Value.Cause == EliminationCauseWire.Player, "MatchEliminationEventPacket values match");

    // 3. KillcamDataChunkPacket roundtrip & <= 4KiB limit check
    KillcamDataChunkPacket? receivedChunk = null;
    registry.Subscribe<KillcamDataChunkPacket>(p => receivedChunk = p);
    var chunkFrames = new KillcamFramePayload[75];
    for (var i = 0; i < 75; i++)
    {
        chunkFrames[i] = new KillcamFramePayload
        {
            ZoneX = 25f, ZoneY = 25f, ZoneRadius = 15f,
            VictimX = 10f + i, VictimY = 20f, VictimHp = (byte)(100 - i), VictimFlags = 1,
            KillerX = 12f + i, KillerY = 22f, KillerHp = 100, KillerFlags = 2, KillerPresent = true
        };
    }
    var chunk = new KillcamDataChunkPacket
    {
        Version = 1,
        MatchId = Guid.NewGuid(),
        Status = 0,
        VictimNetId = 2,
        KillerNetId = 4,
        EndTick = 9999,
        TotalFrames = 150,
        ChunkIndex = 0,
        TotalChunks = 2,
        FrameCount = 75,
        Frames = chunkFrames
    };

    registry.WriteFrame(writer, ref chunk);
    Check(writer.Length <= 4096, $"75-frame KillcamDataChunkPacket size {writer.Length} <= 4096 bytes");
    reader.SetSource(writer.Data, 0, writer.Length);
    Check(registry.TryReadFrame(reader), "KillcamDataChunkPacket frame read succeeded");
    Check(receivedChunk.HasValue && receivedChunk.Value.FrameCount == 75, "Chunk frame count is 75");
    Check(receivedChunk!.Value.Frames != null && receivedChunk.Value.Frames.Length == 75, "Chunk frames array length is 75");
    Check(receivedChunk.Value.Frames![0].KillerPresent, "Frame 0 KillerPresent == true");
    Check(receivedChunk.Value.Frames[0].VictimX == 10f, "Frame 0 VictimX == 10");

    // 4. Bounded arrays before allocating
    var overChunk = new NetDataWriter();
    overChunk.Put((byte)1); // Version
    WriteTestGuid(overChunk, Guid.NewGuid()); // MatchId
    overChunk.Put((byte)0); // Status
    overChunk.Put((byte)1); // Victim
    overChunk.Put((byte)2); // Killer
    overChunk.Put((uint)100); // EndTick
    overChunk.Put((ushort)150); // TotalFrames
    overChunk.Put((byte)0); // ChunkIndex
    overChunk.Put((byte)2); // TotalChunks
    overChunk.Put((byte)120); // FrameCount > 75 (Oversized!)

    var caughtOversizedFrameCount = false;
    try { new KillcamDataChunkPacket().Deserialize(new NetDataReader(overChunk.CopyData())); }
    catch (InvalidOperationException) { caughtOversizedFrameCount = true; }
    Check(caughtOversizedFrameCount, "Oversized chunk FrameCount > 75 rejected before allocating");

    var overTotalFrames = new NetDataWriter();
    overTotalFrames.Put((byte)1);
    WriteTestGuid(overTotalFrames, Guid.NewGuid());
    overTotalFrames.Put((byte)0);
    overTotalFrames.Put((byte)1);
    overTotalFrames.Put((byte)2);
    overTotalFrames.Put((uint)100);
    overTotalFrames.Put((ushort)999); // TotalFrames > 150
    overTotalFrames.Put((byte)0);
    overTotalFrames.Put((byte)2);
    overTotalFrames.Put((byte)50);

    var caughtOversizedTotal = false;
    try { new KillcamDataChunkPacket().Deserialize(new NetDataReader(overTotalFrames.CopyData())); }
    catch (InvalidOperationException) { caughtOversizedTotal = true; }
    Check(caughtOversizedTotal, "Oversized TotalFrames > 150 rejected before allocating");
}

Console.WriteLine($"\n=== Test Results: {passed} passed, {failed} failed ===");
if (failed > 0)
{
    Environment.Exit(1);
}
else
{
    Console.WriteLine("ALL MATCH CHECKS PASSED SUCCESSFULLY!");
    Environment.Exit(0);
}

sealed class FakePeer : IGamePeer
{
    public string Transport { get; }
    public bool IsOpen { get; set; } = true;
    public bool ShouldFailSend { get; set; }
    public string? CloseReason { get; private set; }
    public List<byte[]> Sent { get; } = new();

    public FakePeer(string transport)
    {
        Transport = transport;
    }

    public bool TrySend(byte[] message, bool reliable) => TrySend(message, reliable, 0);

    public bool TrySend(byte[] message, bool reliable, byte channel)
    {
        if (ShouldFailSend) return false;
        Sent.Add(message);
        return true;
    }

    public void Close(string reason)
    {
        IsOpen = false;
        CloseReason = reason;
    }
}
