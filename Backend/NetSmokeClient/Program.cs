using System.Diagnostics;
using System.Text.Json;
using NetSmokeClient.Configuration;
using NetSmokeClient.Networking;
using NetSmokeClient.Services;

// Tool riêng để chứng minh UDP trước khi gắn NetworkManager vào Unity.
if (args.Contains("--help"))
{
    Console.WriteLine("NetSmokeClient --config <JSON> [--name <displayName>] [--clients 1..256] [--duration 0..3600] [--warmup 0..300] [--ping-rate 1..60] [--quiet] [--report <JSON>] [--room-label <non-secret ID>] [--room-plan <private JSON>]");
    Console.WriteLine("duration tính bằng giây; 0 = đến Ctrl+C. Nhập mã phòng; Jwt cần login hoặc NET_ACCESS_TOKEN trong môi trường process.");
    return 0;
}
var sessions = new List<SmokeSession>();
try
{
    string? configPath = null;
    var clients = 1;
    var durationSeconds = 0;
    var warmupSeconds = 0;
    var pingRate = 1;
    var quiet = false;
    string? reportPath = null, roomLabel = null, roomPlanPath = null;
    string? displayName = Environment.GetEnvironmentVariable("NET_DISPLAY_NAME");
    for (var index = 0; index < args.Length; index++)
    {
        if (args[index] == "--quiet") { quiet = true; continue; }
        if (index + 1 >= args.Length) throw new ArgumentException($"Thiếu giá trị cho {args[index]}.");
        switch (args[index])
        {
            case "--config": configPath = args[index + 1]; break;
            case "--clients": clients = ParseInt(args[index + 1], 1, 256); break;
            case "--duration": durationSeconds = ParseInt(args[index + 1], 0, 3600); break;
            case "--warmup": warmupSeconds = ParseInt(args[index + 1], 0, 300); break;
            case "--ping-rate": pingRate = ParseInt(args[index + 1], 1, 60); break;
            case "--report": reportPath = args[index + 1]; break;
            case "--room-plan": roomPlanPath = args[index + 1]; break;
            case "--room-label": roomLabel = args[index + 1]; break;
            case "--name": displayName = args[index + 1]; break;
            default: throw new ArgumentException($"Tham số không hỗ trợ: {args[index]}. Dùng --help.");
        }
        index++;
    }
    if (string.IsNullOrWhiteSpace(configPath)) throw new ArgumentException("Cần --config <đường dẫn JSON>.");
    var options = ClientOptions.Load(configPath);
    var roomPlan = roomPlanPath is null ? null : RoomLoadTarget.Load(roomPlanPath);
    if (roomPlan is not null)
    {
        if (options.AuthMode != "RoomCode" || args.Contains("--clients"))
            throw new ArgumentException("--room-plan requires RoomCode mode and supplies its own client counts; omit --clients.");
        clients = roomPlan.Sum(r => r.Clients);
    }
    Console.WriteLine($"[config] file={Path.GetFullPath(configPath)} mode={options.Mode} udp={options.GameServerHost}:{options.GameServerPort} protocol={options.ProtocolVersion} clients={clients}");
    using var shutdown = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; shutdown.Cancel(); };
    if (displayName is null)
    {
        Console.Write("Tên hiển thị (Enter = Player): ");
        displayName = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(displayName)) displayName = "Player";
    }
    displayName = displayName.Trim();
    if (displayName.Length is < 1 or > 32) throw new ArgumentException("Tên hiển thị cần 1–32 ký tự.");
    var roomCode = roomPlan is null
        ? CredentialInput.ReadSecret("NET_ROOM_CODE", "Mã phòng (ẩn khi nhập): ", shutdown.Token) : "";
    string? accessToken = null;
    if (options.AuthMode == "Jwt")
    {
        accessToken = Environment.GetEnvironmentVariable("NET_ACCESS_TOKEN");
        if (accessToken is null)
        {
            var username = Environment.GetEnvironmentVariable("NET_USERNAME");
            if (username is null) { Console.Write("Tài khoản: "); username = Console.ReadLine(); }
            if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Cần tên tài khoản để login.");
            var password = CredentialInput.ReadSecret("NET_PASSWORD", "Mật khẩu (ẩn khi nhập): ", shutdown.Token);
            accessToken = await AccountLogin.LoginAsync(options, username, password, shutdown.Token);
            Console.WriteLine("[login] API cấp token; chờ GameServer xác minh trước khi join.");
        }
    }
    var assignments = roomPlan is null ? Enumerable.Repeat((Code: roomCode, Label: roomLabel), clients)
        : roomPlan.SelectMany(r => Enumerable.Repeat((Code: r.RoomCode, Label: (string?)r.RoomId.ToString()), r.Clients));
    var number = 0;
    foreach (var assignment in assignments)
    {
        number++;
        var name = clients == 1 ? displayName : displayName[..Math.Min(29, displayName.Length)] + "-" + number;
        var session = new SmokeSession(number, options, assignment.Code, name, accessToken, pingRate, quiet, assignment.Label);
        sessions.Add(session); // Ghi vào danh sách trước Start để lỗi giữa chừng vẫn được Dispose.
        session.Start();
    }
    var started = Stopwatch.GetTimestamp();
    var measuring = false;
    long measuredAt = 0;
    DateTime? measurementUtc = null;
    var cpuAt = TimeSpan.Zero;
    using var processInfo = Process.GetCurrentProcess();
    while (!shutdown.IsCancellationRequested && sessions.Any(session => !session.Finished)
        && (!measuring || durationSeconds == 0 || Stopwatch.GetElapsedTime(measuredAt).TotalSeconds < durationSeconds))
    {
        if (!measuring && Stopwatch.GetElapsedTime(started).TotalSeconds >= warmupSeconds)
        {
            foreach (var session in sessions) session.BeginMeasurement();
            measuring = true; measuredAt = Stopwatch.GetTimestamp(); measurementUtc = DateTime.UtcNow;
            cpuAt = processInfo.TotalProcessorTime;
        }
        foreach (var session in sessions) session.Update();
        Thread.Sleep(1);
    }
    var measuredSeconds = measuring ? Stopwatch.GetElapsedTime(measuredAt).TotalSeconds : 0;
    var cpuMs = (processInfo.TotalProcessorTime - cpuAt).TotalMilliseconds;
    // Stop traffic, then allow the last pongs to arrive or time out before counting loss.
    foreach (var session in sessions) session.StopSending();
    var drainedAt = Stopwatch.GetTimestamp();
    while (sessions.Any(s => s.Outstanding > 0 && !s.Finished) && Stopwatch.GetElapsedTime(drainedAt).TotalSeconds < 5.2)
    {
        foreach (var session in sessions) session.Update();
        Thread.Sleep(1);
    }
    if (!quiet) foreach (var session in sessions) session.PrintSummary();
    var connected = sessions.Count(session => session.ConnectedOnce);
    var joined = sessions.Count(session => session.Joined);
    var failures = sessions.Count(session => session.Failed || !session.Joined || session.Pongs == 0);
    Console.WriteLine($"[summary] requested={clients} connected={connected} joined={joined} failed={failures} pongs={sessions.Sum(session => session.Pongs)}");
    if (reportPath is not null)
    {
        var report = new { room = roomLabel, requested = clients, connected, joined, failed = failures,
            pingRate, warmupSeconds, measuredSeconds, measurementUtc, measurementEndUtc = measurementUtc?.AddSeconds(measuredSeconds),
            generatorCpuOneCorePercent = measuredSeconds > 0 ? cpuMs / (measuredSeconds * 10) : 0,
            rejected = sessions.Count(s => s.Failed && !s.Joined), disconnected = sessions.Count(s => s.Failed && s.Joined),
            cancelled = shutdown.IsCancellationRequested, clients = sessions.Select(s => s.Report(measuredSeconds)).ToArray() };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }
    // Chỉ exit 0 khi mọi client có pong thật. Reject thứ 11 cố ý vẫn trả exit 1.
    return failures == 0 ? 0 : 1;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("[cancelled] Đã dừng client.");
    return 130;
}
catch (Exception error) when (error is ArgumentException or IOException or JsonException
    or InvalidOperationException or System.Net.Sockets.SocketException or HttpRequestException)
{
    Console.Error.WriteLine($"[client-error] {error.Message}");
    return 1;
}
finally
{
    foreach (var session in sessions) session.Dispose();
}

static int ParseInt(string raw, int minimum, int maximum)
{
    if (!int.TryParse(raw, out var value) || value < minimum || value > maximum)
        throw new ArgumentException($"Giá trị số phải nằm trong [{minimum}, {maximum}].");
    return value;
}
