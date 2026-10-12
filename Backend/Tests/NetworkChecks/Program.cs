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

    (int Code, string Text) RunClient(string path)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in new[] { clientDll, "--config", path, "--duration", "2" }) start.ArgumentList.Add(argument);
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
