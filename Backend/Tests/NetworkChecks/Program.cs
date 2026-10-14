using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using System.Security.Cryptography;
using GameServer.Configuration;
using GameServer.Diagnostics;
using GameServer.Networking;
using GameServer.Simulation;
using JungleBackend.Protocol;
using LiteNetLib;
using LiteNetLib.Utils;
using GameServer.Rooms;
using JungleBackend.Rooms;

// Kiểm tra socket thật trên loopback/cổng ngẫu nhiên; không chạm .env, MongoDB hay container của team.
var backend = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var probes = new List<Probe>();
var passed = 0;
const string TestRoom = "Network_Check_Room";
try
{
    var port = FreeUdpPort();
    var metrics = new ServerMetrics();
    using var host = new NetworkHost(new ServerOptions(IPAddress.Loopback, port, 10, 30, 1, "RoomCode", TestRoom), metrics);
    var hosts = new List<NetworkHost> { host };
    host.Start();

    var wrongKey = AddProbe("wrong-key");
    PumpUntil(() => wrongKey.DisconnectReason is not null);
    Check(wrongKey.DisconnectReason == "InvalidRoomCode" && host.ReservedPeers == 0, "Mã phòng sai bị reject, không giữ slot");

    var truncated = new NetDataWriter();
    truncated.Put((byte)255); // Không đủ prefix của string connect.
    var malformedConnect = AddProbe(writer: truncated);
    PumpUntil(() => malformedConnect.DisconnectReason is not null);
    Check(malformedConnect.DisconnectReason == "MalformedRequest", "Connection payload thiếu byte không crash");

    foreach (var (writer, expected) in new[]
    {
        (RawConnect(99, 0, TestRoom, "Tester"), "UnsupportedVersion"),
        (RawConnect(1, 1, TestRoom, "Tester"), "AuthModeMismatch"),
        (RawConnect(1, 2, TestRoom, "Tester"), "AuthModeMismatch"),
        (RawConnect(1, 0, TestRoom, " "), "InvalidDisplayName"),
        (RawConnect(1, 0, TestRoom, new string('x', 33)), "InvalidDisplayName"),
        (RawConnect(1, 0, new string('x', 65), "Tester"), "InvalidRoomCode"),
        (RawConnect(1, 0, new string('界', 64), new string('界', 32)), "PayloadTooLarge")
    })
    {
        var rejected = AddProbe(writer: writer);
        PumpUntil(() => rejected.DisconnectReason is not null);
        Check(rejected.DisconnectReason == expected, "Request reject: " + expected);
    }
    var legacyWriter = new NetDataWriter();
    legacyWriter.Put("JungleMaze.SRV01.v1");
    var legacy = AddProbe(writer: legacyWriter);
    PumpUntil(() => legacy.DisconnectReason is not null);
    Check(legacy.DisconnectReason == "MalformedRequest", "Key smoke cũ không còn được accept");

    // Tạo 11 request cùng lượt: kiểm tra race giữa Accept và callback Connected.
    var batch = Enumerable.Range(0, 11).Select(_ => AddProbe()).ToArray();
    PumpUntil(() => batch.All(peer => peer.Joined || peer.DisconnectReason is not null));
    Check(batch.Count(peer => peer.Joined) == 10 && host.ReservedPeers == 10, "11 request đồng thời chỉ nhận 10");
    Check(batch.Single(peer => !peer.Joined).DisconnectReason == "RoomFull", "Peer thứ 11 nhận RoomFull");
    Check(batch.Where(peer => peer.Joined).Select(peer => peer.PlayerId).Distinct().Count() == 10, "RoomCode: server cấp 10 PlayerId khác nhau");

    var accepted = batch.Where(peer => peer.Joined).ToArray();
    foreach (var peer in accepted) peer.Send(NetworkProtocol.Ping, 100);
    PumpUntil(() => accepted.All(peer => peer.Pongs.Contains(100)));
    Check(true, "10 client nhận Pong với đúng sequence");

    var first = accepted[0];
    var badPackets = new[]
    {
        new byte[] { NetworkProtocol.Ping }, // Thiếu int sequence.
        new byte[] { NetworkProtocol.Ping, 1, 0, 0, 0, 99 }, // Thừa byte.
        new byte[] { 99, 2, 0, 0, 0 }, // Message type lạ.
        new byte[] { NetworkProtocol.Ping, 0, 0, 0, 0 }, // Sequence không hợp lệ.
        new byte[512] // Quá dài so với giao thức 5 byte, vẫn dưới MTU Unreliable của library.
    };
    var previousPongs = first.Pongs.Count;
    foreach (var packet in badPackets) first.Peer!.Send(packet, DeliveryMethod.Unreliable);
    PumpFor(400);
    Check(first.Pongs.Count == previousPongs, "Packet thiếu/thừa/lạ/sequence sai/quá dài không được Pong");
    first.Send(NetworkProtocol.Ping, 101);
    PumpUntil(() => first.Pongs.Contains(101));
    Check(true, "Sau packet lỗi, server vẫn trả Pong hợp lệ");

    first.Dispose();
    PumpUntil(() => host.ReservedPeers == 9);
    Check(true, "Disconnect trả slot");
    var replacement = AddProbe();
    PumpUntil(() => replacement.Joined);
    Check(replacement.PlayerId != first.PlayerId, "RoomCode reconnect nhận PlayerId mới");
    replacement.Send(NetworkProtocol.Ping, 102);
    PumpUntil(() => replacement.Pongs.Contains(102));
    Check(host.ReservedPeers == 10, "Client mới vào slot vừa trả và ping được");

    // Dừng socket không gửi disconnect, tương đương tiến trình client chết/mất mạng.
    replacement.StopSilently();
    PumpUntil(() => host.ReservedPeers == 9, 9000);
    Check(true, "Timeout client mất mạng cũng trả slot");
    foreach (var probe in probes) probe.Dispose();
    PumpUntil(() => host.ReservedPeers == 0);
    Check(true, "Dừng toàn bộ client dọn hết slot");

    // Nhánh Jwt dùng key ngẫu nhiên riêng; không dùng secret API thật hoặc gọi DB trong Tick.
    var jwtKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    var jwtSettings = new JwtSettings { Issuer = "JungleMaze.Local", Audience = "JungleMaze.Game", SigningKey = jwtKey };
    var jwtPort = FreeUdpPort();
    using var jwtHost = new NetworkHost(new ServerOptions(IPAddress.Loopback, jwtPort, 10, 30, 1, "Jwt", TestRoom, 800, jwtSettings), new ServerMetrics());
    hosts.Add(jwtHost);
    jwtHost.Start();
    var jwtUser = Guid.NewGuid().ToString();
    var validToken = Token(jwtUser, jwtKey);
    var validJwt = AddJwtProbe(validToken);
    PumpUntil(() => validJwt.Joined || validJwt.DisconnectReason is not null);
    Check(validJwt.PlayerId == jwtUser && jwtHost.ActivePeers == 1, "JWT hợp lệ: PlayerId lấy từ sub đã xác minh");
    validJwt.Send(NetworkProtocol.Ping, 201);
    PumpUntil(() => validJwt.Pongs.Contains(201));
    Check(true, "JWT accepted rồi mới Ping/Pong");
    var duplicate = AddJwtProbe(validToken);
    PumpUntil(() => duplicate.DisconnectReason is not null);
    Check(duplicate.DisconnectReason == "DuplicateSession" && jwtHost.ActivePeers == 1, "Cùng user không thể giữ hai phiên");
    validJwt.Dispose();
    PumpUntil(() => jwtHost.ReservedPeers == 0);
    var rejoin = AddJwtProbe(validToken);
    PumpUntil(() => rejoin.Joined);
    Check(rejoin.PlayerId == jwtUser, "Disconnect dọn user mapping, JWT join lại được cùng ID");
    rejoin.Dispose();
    PumpUntil(() => jwtHost.ReservedPeers == 0);

    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var cases = new (string Name, string Token)[]
    {
        ("malformed", "not-a-jwt"), ("sai chữ ký", Token(jwtUser, new string('z', 90))),
        ("hết hạn", Token(jwtUser, jwtKey, new() { ["exp"] = now - 120, ["nbf"] = now - 300 })),
        ("chưa có hiệu lực", Token(jwtUser, jwtKey, new() { ["nbf"] = now + 120 })),
        ("sai issuer", Token(jwtUser, jwtKey, new() { ["iss"] = "Other" })),
        ("sai audience", Token(jwtUser, jwtKey, new() { ["aud"] = "Other" })),
        ("thiếu exp", Token(jwtUser, jwtKey, new() { ["exp"] = null })),
        ("thiếu sub", Token(jwtUser, jwtKey, new() { ["sub"] = null })),
        ("sub trống", Token(" ", jwtKey)), ("HS512", Token(jwtUser, jwtKey, algorithm: "HS512")),
        ("unsigned", Token(jwtUser, jwtKey, algorithm: "none"))
    };
    foreach (var item in cases)
    {
        var invalidJwt = AddJwtProbe(item.Token);
        PumpUntil(() => invalidJwt.DisconnectReason is not null);
        Check(invalidJwt.DisconnectReason == "InvalidToken", "JWT reject: " + item.Name);
    }
    var fragmentedToken = Token(Guid.NewGuid().ToString(), jwtKey, new() { ["padding"] = new string('p', 1050) });
    Check(fragmentedToken.Length > 1024 && fragmentedToken.Length <= 2048, "Token test lớn hơn một datagram smoke");
    var fragmented = AddJwtProbe(fragmentedToken);
    PumpUntil(() => fragmented.Joined || fragmented.DisconnectReason is not null);
    Check(fragmented.Joined, "Auth ReliableOrdered có fragmentation vẫn xác thực được");
    fragmented.Dispose();
    PumpUntil(() => jwtHost.ReservedPeers == 0);

    var earlyPing = AddJwtProbe();
    PumpUntil(() => earlyPing.AuthRequested);
    earlyPing.Send(NetworkProtocol.Ping, 202);
    PumpUntil(() => earlyPing.DisconnectReason is not null);
    Check(earlyPing.DisconnectReason == "AuthenticationRequired" && earlyPing.Pongs.Count == 0,
        "Pending không được gửi Ping trước Auth");
    var emptyAuth = AddJwtProbe();
    PumpUntil(() => emptyAuth.AuthRequested);
    emptyAuth.Peer!.Send(NetworkProtocol.StringMessage(NetworkProtocol.Auth, ""), DeliveryMethod.ReliableOrdered);
    PumpUntil(() => emptyAuth.DisconnectReason is not null);
    Check(emptyAuth.DisconnectReason == "AuthenticationRequired", "Auth thiếu token bị reject");
    var oversizedAuth = AddJwtProbe();
    PumpUntil(() => oversizedAuth.AuthRequested);
    oversizedAuth.Peer!.Send(NetworkProtocol.StringMessage(NetworkProtocol.Auth, new string('x', 5000)), DeliveryMethod.ReliableOrdered);
    PumpUntil(() => oversizedAuth.DisconnectReason is not null);
    Check(oversizedAuth.DisconnectReason == "PayloadTooLarge", "Auth packet vượt 4096 byte bị reject");
    var longToken = AddJwtProbe();
    PumpUntil(() => longToken.AuthRequested);
    longToken.Peer!.Send(NetworkProtocol.StringMessage(NetworkProtocol.Auth, new string('x', 2049)), DeliveryMethod.ReliableOrdered);
    PumpUntil(() => longToken.DisconnectReason is not null);
    Check(longToken.DisconnectReason == "AuthenticationRequired", "Token vượt giới hạn 2048 ký tự bị reject");

    var pendingBatch = Enumerable.Range(0, 10).Select(_ => AddJwtProbe()).ToArray();
    PumpUntil(() => pendingBatch.All(peer => peer.AuthRequested));
    Check(jwtHost.PendingPeers == 10 && jwtHost.ActivePeers == 0, "10 pending giữ đủ 10 slot");
    var eleventhPending = AddJwtProbe();
    PumpUntil(() => eleventhPending.DisconnectReason is not null);
    Check(eleventhPending.DisconnectReason == "RoomFull", "Peer thứ 11 bị chặn dù 10 peer còn pending");
    PumpUntil(() => jwtHost.ReservedPeers == 0);
    Check(pendingBatch.All(peer => !peer.Joined), "Pending hết deadline trả hết slot, không tự join");
    PumpUntil(() => pendingBatch.All(peer => peer.DisconnectReason is not null));
    Check(pendingBatch.All(peer => peer.DisconnectReason == "AuthTimeout"), "Pending không Auth nhận AuthTimeout");

    var jwtBatch = Enumerable.Range(0, 11).Select(_ => AddJwtProbe(Token(Guid.NewGuid().ToString(), jwtKey))).ToArray();
    PumpUntil(() => jwtBatch.All(peer => peer.Joined || peer.DisconnectReason is not null));
    Check(jwtBatch.Count(peer => peer.Joined) == 10 && jwtHost.ActivePeers == 10, "10 JWT user khác nhau join, thứ 11 RoomFull");
    foreach (var peer in jwtBatch.Where(peer => peer.Joined)) peer.Send(NetworkProtocol.Ping, 203);
    PumpUntil(() => jwtBatch.Where(peer => peer.Joined).All(peer => peer.Pongs.Contains(203)));
    Check(true, "10 JWT user đều nhận Pong đúng sequence");
    foreach (var peer in probes) peer.Dispose();
    PumpUntil(() => jwtHost.ReservedPeers == 0);

    using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
    {
        var ticks = 0;
        var exactDelta = true;
        new FixedTickLoop(30, new ServerMetrics()).Run(() => { }, delta =>
        {
            ticks++;
            exactDelta &= delta == 1.0 / 30;
        }, () => { }, cancellation.Token);
        Check(exactDelta && ticks is >= 56 and <= 64, "Fixed delta 1/30 và nhịp gần 30Hz trong 2 giây");
    }

    // Chủ động gây stall: không để catch-up vô hạn và phải báo droppedTicks thay vì giấu mất mô phỏng.
    var output = new StringWriter();
    var originalOutput = Console.Out;
    var overloadMetrics = new ServerMetrics();
    var stalled = false;
    var stepsThisPoll = 0;
    var maximumSteps = 0;
    try
    {
        Console.SetOut(output);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1.4));
        new FixedTickLoop(30, overloadMetrics).Run(() =>
        {
            maximumSteps = Math.Max(maximumSteps, stepsThisPoll);
            stepsThisPoll = 0;
            if (!stalled) { stalled = true; Thread.Sleep(300); }
        }, _ => stepsThisPoll++, () => overloadMetrics.ReportIfDue(0, 1), cancellation.Token);
    }
    finally { Console.SetOut(originalOutput); }
    Check(maximumSteps <= 5 && Regex.IsMatch(output.ToString(), @"droppedTicks=[1-9]\d*"), "Stall 300ms: tối đa 5 catch-up tick, báo droppedTicks");

    // Kiểm tra loader/exit code của executable client thật, không lặp lại validation trong test.
    var clientDll = Path.Combine(backend, "NetSmokeClient/bin/Debug/net8.0/NetSmokeClient.dll");
    if (!File.Exists(clientDll)) throw new InvalidOperationException("Build JungleBackend.sln trước khi chạy test.");
    var temp = Path.Combine(backend, "../.utmp/network-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(temp);
    var config = new Dictionary<string, object?>
    {
        ["mode"] = "Local", ["apiBaseUrl"] = null, ["gameServerHost"] = "127.0.0.1",
        ["gameServerPort"] = FreeUdpPort(), ["authMode"] = "RoomCode",
        ["protocolVersion"] = 1, ["connectTimeoutMs"] = 500
    };
    var configPath = Path.Combine(temp, "client.json");
    foreach (var (field, invalid) in new (string, object?)[]
    {
        ("mode", "bad"), ("gameServerHost", "http://localhost:7777"), ("gameServerPort", 0),
        ("authMode", "bad"), ("protocolVersion", 99), ("connectTimeoutMs", 0)
    })
    {
        var invalidConfig = new Dictionary<string, object?>(config) { [field] = invalid };
        File.WriteAllText(configPath, JsonSerializer.Serialize(invalidConfig));
        var result = RunClient(configPath);
        Check(result.Code == 1 && result.Text.Contains("[client-error]"), $"Config sai {field}: báo lỗi và exit 1");
    }
    File.WriteAllText(configPath, "{bad json");
    foreach (var (url, mode) in new[] { ("file:///bad", "Local"), ("http://192.168.1.20:5000", "LAN") })
    {
        var unsafeApi = new Dictionary<string, object?>(config) { ["authMode"] = "Jwt", ["apiBaseUrl"] = url, ["mode"] = mode };
        File.WriteAllText(configPath, JsonSerializer.Serialize(unsafeApi));
        var result = RunClient(configPath);
        Check(result.Code == 1 && result.Text.Contains("[client-error]"), "JWT client từ chối API URL không được phép: " + mode);
    }
    File.WriteAllText(configPath, "{bad json");
    Check(RunClient(configPath).Code == 1, "JSON lỗi bị từ chối");
    Check(RunClient(Path.Combine(temp, "missing.json")).Code == 1, "File config không tồn tại bị từ chối");
    File.WriteAllText(configPath, JsonSerializer.Serialize(config));
    // Kiểm tra CLI room-plan ngay trong suite mạng, không cần script Python riêng.
    // --clients xung đột với plan: input hợp lệ cũng dừng trước khi mở socket.
    Dictionary<string, object?> PlanRoom(int count = 1) => new()
        { ["roomId"] = Guid.NewGuid(), ["roomCode"] = "TESTAB", ["clients"] = count };
    var duplicateRoom = PlanRoom();
    var emptyIdRoom = PlanRoom(); emptyIdRoom["roomId"] = Guid.Empty;
    var emptyCodeRoom = PlanRoom(); emptyCodeRoom["roomCode"] = " ";
    var nullCodeRoom = PlanRoom(); nullCodeRoom["roomCode"] = null;
    var longCodeRoom = PlanRoom(); longCodeRoom["roomCode"] = new string('X', 65);
    var unknownFieldRoom = PlanRoom(); unknownFieldRoom["password"] = "must-not-echo";
    var roomPlanPath = Path.Combine(temp, "private-plan.json");
    foreach (var (label, payload, valid) in new (string, object?, bool)[]
    {
        ("null-list", null, false), ("empty-list", Array.Empty<object>(), false),
        ("null-entry", new object?[] { null }, false), ("empty-id", new[] { emptyIdRoom }, false),
        ("empty-code", new[] { emptyCodeRoom }, false), ("null-code", new[] { nullCodeRoom }, false),
        ("long-code", new[] { longCodeRoom }, false), ("zero-clients", new[] { PlanRoom(0) }, false),
        ("too-many-per-room", new[] { PlanRoom(257) }, false),
        ("too-many-total", new[] { PlanRoom(128), PlanRoom(129) }, false),
        ("duplicate-id", new[] { duplicateRoom, duplicateRoom }, false),
        ("too-many-rooms", Enumerable.Range(0, 33).Select(_ => PlanRoom()).ToArray(), false),
        ("unknown-field", new[] { unknownFieldRoom }, false),
        ("valid-single-256", new[] { PlanRoom(256) }, true),
        ("valid-multi-256", new[] { PlanRoom(128), PlanRoom(128) }, true),
        ("valid-32-rooms", Enumerable.Range(0, 32).Select(_ => PlanRoom()).ToArray(), true)
    })
    {
        File.WriteAllText(roomPlanPath, JsonSerializer.Serialize(payload));
        var result = RunClient(configPath, "--room-plan", roomPlanPath, "--clients", "1");
        var validPlan = result.Text.Contains("supplies its own client counts");
        Check(result.Code == 1 && validPlan == valid && result.Text.Contains("[client-error]")
            && !result.Text.Contains("[config]") && !result.Text.Contains("must-not-echo"), "Room-plan CLI: " + label);
    }
    File.Delete(roomPlanPath);
    var timeout = RunClient(configPath);
    Check(timeout.Code == 1 && (timeout.Text.Contains("ConnectTimeout") || timeout.Text.Contains("ConnectionFailed")), "Server không chạy: client timeout, không exit thành công giả");

    // Chỉ thay biến môi trường của process test và khôi phục ngay; không sửa .env của team.
    foreach (var (name, value) in new[]
    {
        ("GAME_BIND_ADDRESS", "bad-ip"), ("GAME_PORT", "65536"),
        ("MAX_PLAYERS", "11"), ("TICK_RATE", "60"), ("AUTH_MODE", "bad"),
        ("ROOM_CODE", ""), ("AUTH_TIMEOUT_MS", "0"),
        ("METRICS_INTERVAL_SECONDS", "0")
    })
    {
        var names = new[] { "GAME_BIND_ADDRESS", "GAME_PORT", "MAX_PLAYERS", "TICK_RATE", "AUTH_MODE", "METRICS_INTERVAL_SECONDS", "ROOM_CODE", "AUTH_TIMEOUT_MS" };
        var saved = names.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        var rejected = false;
        try
        {
            foreach (var key in names) Environment.SetEnvironmentVariable(key, null);
            Environment.SetEnvironmentVariable("ROOM_CODE", TestRoom);
            Environment.SetEnvironmentVariable(name, value);
            try { ServerOptions.FromEnvironment(); }
            catch (ArgumentException) { rejected = true; }
        }
        finally { foreach (var item in saved) Environment.SetEnvironmentVariable(item.Key, item.Value); }
        Check(rejected, $"Server config sai {name}: dừng trước khi mở socket");
    }

    // Multi-room: controllable time tests the full 10-minute TTL without sleeping.
    var clock = new TestClock();
    var roomConfig = new RoomOptions(MaxTotalPlayers: 12);
    var roomOptions = new ServerOptions(IPAddress.Loopback, FreeUdpPort(), 10, 30, 1, "RoomCode", TestRoom, Rooms: roomConfig);
    var registry = new RoomManager(roomOptions, clock);
    RoomView Create(string owner, int size = 2) => (RoomView)registry.Create(new() { OwnerId = owner, Name = "Test", MaxPlayers = size }).Body!;
    var a = Create("alice"); var b = Create("alice");
    Check(registry.Create(new() { OwnerId = "alice", Name = "Third", MaxPlayers = 2 }).Status == 409, "Owner quota: at most 2 added rooms");
    Check(registry.Create(new() { OwnerId = "bob", Name = "Too large", MaxPlayers = 13 }).Status == 400, "Room capacity cannot exceed total server capacity");
    Check(a.RoomCode != b.RoomCode && Regex.IsMatch(a.RoomCode!, "^[A-HJ-NP-Z2-9]{6}$") && Regex.IsMatch(b.RoomCode!, "^[A-HJ-NP-Z2-9]{6}$"), "Distinct six-character readable join codes");
    var aliceRooms = (RoomView[])registry.List("alice").Body!;
    Check(aliceRooms.Length == 3 && aliceRooms.Single(r => r.IsDefault).RoomCode is null, "Owner list includes default without its code");
    Check(((RoomView[])registry.List("bob").Body!).Length == 1, "Other user cannot list private rooms or codes");
    Check(registry.Delete(a.Id, "bob").Status == 403, "Non-owner cannot delete room");
    Check(registry.Delete(aliceRooms.Single(r => r.IsDefault).Id, "alice").Status == 409, "Default room cannot be deleted");
    var live = registry.Find(a.RoomCode!)!;
    registry.Reserve(live, 9000, false);
    Check(registry.Delete(a.Id, "alice").Status == 409, "Pending JWT also prevents deleting a room");
    clock.Advance(599); registry.ExpireEmpty();
    Check(registry.Count == 3, "Empty rooms survive before TTL");
    clock.Advance(1); registry.ExpireEmpty();
    Check(registry.Count == 2 && registry.Find(a.RoomCode!) is not null, "600s expires empty rooms, preserves occupied/default");
    registry.Release(live, 9000); clock.Advance(599); registry.ExpireEmpty();
    Check(registry.Count == 2, "Empty TTL starts again after last pending/active peer leaves");
    clock.Advance(1); registry.ExpireEmpty();
    Check(registry.Count == 1, "Released room expires after full empty TTL");
    Check(new RoomManager(roomOptions).Count == 1, "Restart restores only default room");
    for (var i = 0; i < 4; i++) Create("owner" + i);
    Check(registry.Count == 5 && registry.Create(new() { OwnerId = "another", Name = "Sixth", MaxPlayers = 1 }).Status == 409, "Maximum 5 rooms includes default; reject sixth");
    var queue = new RoomCommands();
    var queued = Enumerable.Range(0, 129).Select(_ => queue.Submit(r => r.List("alice"), CancellationToken.None)).ToArray();
    Check(queued[128].IsCompletedSuccessfully && queued[128].Result.Status == 503, "Bounded control queue rejects overflow");
    for (var i = 0; i < 8; i++) queue.Drain(registry);
    Check(Task.WhenAll(queued).GetAwaiter().GetResult().Take(128).All(r => r.Status == 200), "Control commands drain on UDP application thread");
    using (var cancelled = new CancellationTokenSource())
    {
        var ghost = queue.Submit(r => r.Delete(a.Id, "alice"), cancelled.Token);
        cancelled.Cancel(); ghost.GetAwaiter().GetResult(); queue.Drain(registry);
        Check(registry.Count == 5, "Cancelled queued operation does not mutate room registry");
    }

    // Current product caps are enforced even when stale env values request more.
    new RoomOptions().Validate(10);
    Check(new RoomOptions().MaxTotalPlayers == 50 && new RoomOptions().MaxRooms == 5,
        "Selected policy defaults: 10 players/room, 5 rooms, 50 total, 2 created rooms/user");
    foreach (var invalid in new[] { new RoomOptions(MaxRoomPlayers: 11), new RoomOptions(MaxTotalPlayers: 51),
        new RoomOptions(MaxRooms: 6), new RoomOptions(MaxRoomsPerUser: 3) })
    {
        var rejectedLimit = false;
        try { invalid.Validate(10); } catch (ArgumentException) { rejectedLimit = true; }
        Check(rejectedLimit, "Reject configuration above the selected room policy");
    }
    var totalGuard = new RoomManager(roomOptions with { MaxPlayers = 2, Rooms = new RoomOptions(MaxTotalPlayers: 3) });
    Check(totalGuard.Create(new() { OwnerId = "owner", Name = "Too large for total", MaxPlayers = 4 }).Status == 400, "Created room cannot exceed configured total capacity");

    var multiPort = FreeUdpPort();
    using var multi = new NetworkHost(roomOptions with { Port = multiPort, MaxPlayers = 2, Rooms = roomConfig with { MaxTotalPlayers = 3 } }, new ServerMetrics());
    hosts.Add(multi); multi.Start();
    var second = (RoomView)multi.Rooms.Create(new() { OwnerId = "owner", Name = "B", MaxPlayers = 2 }).Body!;
    Probe Multi(string room)
    {
        var probe = new Probe(); probes.Add(probe);
        probe.Start(multiPort, NetworkProtocol.WriteConnect(JoinAuthMode.RoomCode, room, "Multi")); return probe;
    }
    var ma = new[] { Multi(TestRoom), Multi(TestRoom), Multi(TestRoom) };
    PumpUntil(() => ma.All(p => p.Joined || p.DisconnectReason is not null));
    Check(ma.Count(p => p.Joined) == 2 && ma.Single(p => !p.Joined).DisconnectReason == "RoomFull", "Room A reaches its own capacity");
    var mb = Multi(second.RoomCode!); PumpUntil(() => mb.Joined);
    Check(multi.ReservedPeers == 3, "Full room A does not block room B");
    var overflow = Multi(second.RoomCode!); PumpUntil(() => overflow.DisconnectReason is not null);
    Check(overflow.DisconnectReason == "ServerFull" && multi.ReservedPeers == 3, "Total server capacity includes all rooms");
    Check(multi.Rooms.Delete(second.Id, "owner").Status == 409, "Occupied room cannot be deleted");
    mb.Dispose(); PumpUntil(() => multi.ReservedPeers == 2);
    Check(multi.Rooms.Find(TestRoom)!.Peers.Count == 2 && multi.Rooms.Find(second.RoomCode!)!.Peers.Count == 0, "Disconnect returns only the correct room slot");
    Check(multi.Rooms.Delete(second.Id, "owner").Status == 204, "Owner can delete empty room");
    foreach (var p in ma) p.Dispose(); PumpUntil(() => multi.ReservedPeers == 0);
    var aa = Enumerable.Range(0, 2).Select(_ => Multi(TestRoom)).ToArray();
    var rr = (RoomView)multi.Rooms.Create(new() { OwnerId = "owner", Name = "C", MaxPlayers = 2 }).Body!;
    var bb = Enumerable.Range(0, 2).Select(_ => Multi(rr.RoomCode!)).ToArray();
    PumpUntil(() => aa.Concat(bb).All(p => p.Joined || p.DisconnectReason is not null));
    Check(aa.Concat(bb).Count(p => p.Joined) == 3 && multi.ReservedPeers == 3, "Simultaneous joins across rooms never exceed total cap");
    foreach (var p in aa.Concat(bb)) p.Dispose(); PumpUntil(() => multi.ReservedPeers == 0);
    Check(RoomControlHost.Start(new RoomOptions(), new RoomCommands()) is null, "Missing control secret keeps default UDP available without HTTP");
    var shortSecretRejected = false;
    try { new RoomOptions(ControlToken: "short").Validate(10); } catch (ArgumentException) { shortSecretRejected = true; }
    Check(shortSecretRejected, "Configured control secret must contain at least 32 bytes");
    var pendingPort = FreeUdpPort();
    using var multiJwt = new NetworkHost(roomOptions with { Port = pendingPort, MaxPlayers = 2, AuthMode = "Jwt", Jwt = jwtSettings,
        AuthTimeoutMs = 3000, Rooms = roomConfig with { MaxTotalPlayers = 3 } }, new ServerMetrics());
    hosts.Add(multiJwt); multiJwt.Start();
    var pendingRoom = (RoomView)multiJwt.Rooms.Create(new() { OwnerId = "owner", Name = "Pending B", MaxPlayers = 2 }).Body!;
    Probe Pending(string code, string? auth = null)
    {
        var p = new Probe { AuthToken = auth }; probes.Add(p);
        p.Start(pendingPort, NetworkProtocol.WriteConnect(JoinAuthMode.Jwt, code, "PendingTest")); return p;
    }
    var p1 = Pending(TestRoom); var p2 = Pending(TestRoom);
    PumpUntil(() => p1.AuthRequested && p2.AuthRequested);
    var p3 = Pending(TestRoom); PumpUntil(() => p3.DisconnectReason is not null);
    Check(p3.DisconnectReason == "RoomFull" && multiJwt.PendingPeers == 2, "JWT pending occupies capacity in its own room");
    var p4 = Pending(pendingRoom.RoomCode!); PumpUntil(() => p4.AuthRequested);
    var p5 = Pending(pendingRoom.RoomCode!); PumpUntil(() => p5.DisconnectReason is not null);
    Check(p5.DisconnectReason == "ServerFull" && multiJwt.PendingPeers == 3, "JWT pending in multiple rooms occupies total server capacity");
    PumpUntil(() => multiJwt.ReservedPeers == 0, 6000);
    Check(multiJwt.Rooms.Find(TestRoom)!.Peers.Count == 0 && multiJwt.Rooms.Find(pendingRoom.RoomCode!)!.Peers.Count == 0, "JWT auth deadline returns the correct slots across rooms");
    var authenticated = Pending(pendingRoom.RoomCode!, Token("multi-user", jwtKey));
    PumpUntil(() => authenticated.Joined);
    Check(multiJwt.ActivePeers == 1 && multiJwt.Rooms.Find(pendingRoom.RoomCode!)!.Peers.Values.Single(), "JWT activation after timeout uses the selected room");
    authenticated.Dispose(); PumpUntil(() => multiJwt.ReservedPeers == 0);
    using (var tcp = new TcpListener(IPAddress.Loopback, 0))
    {
        tcp.Start(); var controlPort = ((IPEndPoint)tcp.LocalEndpoint).Port; tcp.Stop();
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var control = RoomControlHost.Start(new RoomOptions(ControlToken: secret, ControlPort: controlPort), multi.Commands)!;
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{controlPort}") };
            Check((int)http.GetAsync("/internal/rooms?ownerId=owner").GetAwaiter().GetResult().StatusCode == 401, "Private control requires its own secret");
            http.DefaultRequestHeaders.Add("X-Room-Control-Key", "wrong");
            Check((int)http.GetAsync("/internal/rooms?ownerId=owner").GetAwaiter().GetResult().StatusCode == 401, "Wrong private secret rejected");
            http.DefaultRequestHeaders.Remove("X-Room-Control-Key"); http.DefaultRequestHeaders.Add("X-Room-Control-Key", secret);
            var task = http.GetAsync("/internal/rooms?ownerId=owner");
            PumpUntil(() => task.IsCompleted);
            Check(task.GetAwaiter().GetResult().IsSuccessStatusCode, "Authorized HTTP control completes through UDP command queue");
        }
        finally { control.StopAsync().GetAwaiter().GetResult(); control.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }

    Console.WriteLine($"PASS {passed} network checks. Không nghiệm thu LAN/VPN hoặc gameplay.");
    return 0;

    Probe AddProbe(string room = TestRoom, NetDataWriter? writer = null)
    {
        var probe = new Probe();
        probes.Add(probe);
        probe.Start(port, writer ?? NetworkProtocol.WriteConnect(JoinAuthMode.RoomCode, room, "NetworkTest"));
        return probe;
    }

    Probe AddJwtProbe(string? token = null)
    {
        var probe = new Probe { AuthToken = token };
        probes.Add(probe);
        probe.Start(jwtPort, NetworkProtocol.WriteConnect(JoinAuthMode.Jwt, TestRoom, "NetworkTest"));
        return probe;
    }

    void PumpUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var watch = Stopwatch.StartNew();
        while (!condition() && watch.ElapsedMilliseconds < timeoutMs)
        {
            foreach (var server in hosts) server.PollEvents();
            foreach (var peer in probes) peer.Poll();
            Thread.Sleep(5);
        }
        if (!condition()) throw new InvalidOperationException("Timed out waiting for network condition.");
    }

    void PumpFor(int milliseconds)
    {
        var watch = Stopwatch.StartNew();
        PumpUntil(() => watch.ElapsedMilliseconds >= milliseconds, milliseconds + 1000);
    }

    (int Code, string Text) RunClient(string path, params string[] extraArguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in new[] { clientDll, "--config", path, "--duration", "2" }) start.ArgumentList.Add(argument);
        foreach (var argument in extraArguments) start.ArgumentList.Add(argument);
        start.Environment["NET_ROOM_CODE"] = TestRoom;
        start.Environment["NET_DISPLAY_NAME"] = "ConfigTest";
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(10000)) { process.Kill(true); throw new InvalidOperationException("Client did not exit."); }
        return (process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }
}
catch (Exception error)
{
    Console.Error.WriteLine($"FAIL after {passed} checks: {error}");
    return 1;
}
finally
{
    foreach (var probe in probes) probe.Dispose();
}

void Check(bool success, string description)
{
    if (!success) throw new InvalidOperationException(description);
    Console.WriteLine($"PASS {++passed}: {description}");
}

static int FreeUdpPort()
{
    using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
    return ((IPEndPoint)socket.Client.LocalEndPoint!).Port;
}

static NetDataWriter RawConnect(ushort version, byte mode, string room, string name)
{
    var writer = new NetDataWriter();
    writer.Put(version); writer.Put(mode); writer.Put(room); writer.Put(name);
    return writer;
}

static string Token(string userId, string key, Dictionary<string, object?>? overrides = null, string algorithm = "HS256")
{
    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var payload = new Dictionary<string, object?>
    {
        ["sub"] = userId, ["iss"] = "JungleMaze.Local", ["aud"] = "JungleMaze.Game",
        ["iat"] = now, ["nbf"] = now - 1, ["exp"] = now + 300
    };
    if (overrides is not null) foreach (var item in overrides)
        { if (item.Value is null) payload.Remove(item.Key); else payload[item.Key] = item.Value; }
    static string Encode(object value) => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(value))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    var body = Encode(new { alg = algorithm, typ = "JWT" }) + "." + Encode(payload);
    if (algorithm == "none") return body + ".";
    var secret = Encoding.UTF8.GetBytes(key);
    var signature = algorithm == "HS512" ? HMACSHA512.HashData(secret, Encoding.ASCII.GetBytes(body))
        : HMACSHA256.HashData(secret, Encoding.ASCII.GetBytes(body));
    return body + "." + Convert.ToBase64String(signature).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

sealed class Probe : IDisposable
{
    private readonly NetManager _manager;
    private bool _disposed;
    public NetPeer? Peer { get; private set; }
    public bool Connected { get; private set; }
    public string? PlayerId { get; private set; }
    public bool Joined => PlayerId is not null;
    public bool AuthRequested { get; private set; }
    public string? AuthToken { get; init; }
    public string? DisconnectReason { get; private set; }
    public List<int> Pongs { get; } = new();

    public Probe()
    {
        var listener = new EventBasedNetListener();
        _manager = new NetManager(listener) { AutoRecycle = true, IPv6Enabled = false, DisconnectTimeout = 5000 };
        listener.PeerConnectedEvent += peer => { Peer = peer; Connected = true; };
        listener.PeerDisconnectedEvent += (_, info) => DisconnectReason =
            NetworkProtocol.TryReadStringMessage(info.AdditionalData, NetworkProtocol.JoinRejected, 256, 64, out var reason)
                ? reason : info.Reason.ToString();
        listener.NetworkReceiveEvent += (_, reader, _, method) =>
        {
            if (reader.AvailableBytes == 0) return;
            var type = reader.PeekByte();
            if (method == DeliveryMethod.ReliableOrdered && type == NetworkProtocol.AuthRequired)
            {
                AuthRequested = true;
                if (AuthToken is not null) Peer!.Send(NetworkProtocol.WriteAuth(AuthToken), DeliveryMethod.ReliableOrdered);
            }
            else if (type == NetworkProtocol.JoinAccepted
                && NetworkProtocol.TryReadStringMessage(reader, NetworkProtocol.JoinAccepted, 256, 64, out var playerId)) PlayerId = playerId;
            else if (type == NetworkProtocol.JoinRejected
                && NetworkProtocol.TryReadStringMessage(reader, NetworkProtocol.JoinRejected, 256, 64, out var reason)) DisconnectReason = reason;
            else if (NetworkProtocol.TryRead(reader, NetworkProtocol.Pong, out var sequence)) Pongs.Add(sequence);
        };
    }

    public void Start(int port, NetDataWriter writer)
    {
        if (!_manager.Start()) throw new InvalidOperationException("Cannot bind test socket.");
        Peer = _manager.Connect("127.0.0.1", port, writer);
    }
    public void Send(byte type, int sequence)
    {
        var writer = new NetDataWriter();
        NetworkProtocol.Write(writer, type, sequence);
        Peer!.Send(writer, DeliveryMethod.Unreliable);
    }
    public void StopSilently() { _disposed = true; _manager.Stop(false); }
    public void Poll() { if (!_disposed) _manager.PollEvents(); }
    public void Dispose() { if (!_disposed) { _disposed = true; _manager.Stop(); } }
}

sealed class TestClock : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(int seconds) => _now = _now.AddSeconds(seconds);
}
