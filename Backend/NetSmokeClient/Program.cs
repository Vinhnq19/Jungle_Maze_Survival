using System.Diagnostics;
using System.Text.Json;
using NetSmokeClient.Configuration;
using NetSmokeClient.Networking;
using NetSmokeClient.Services;

// Tool riêng để chứng minh UDP trước khi gắn NetworkManager vào Unity.
if (args.Contains("--help"))
{
    Console.WriteLine("NetSmokeClient --config <JSON> [--name <displayName>] [--clients 1..11] [--duration 0..3600]");
    Console.WriteLine("duration tính bằng giây; 0 = đến Ctrl+C. Nhập mã phòng; Jwt cần login hoặc NET_ACCESS_TOKEN trong môi trường process.");
    return 0;
}
var sessions = new List<SmokeSession>();
try
{
    string? configPath = null;
    var clients = 1;
    var durationSeconds = 0;
    string? displayName = Environment.GetEnvironmentVariable("NET_DISPLAY_NAME");
    for (var index = 0; index < args.Length; index += 2)
    {
        if (index + 1 >= args.Length) throw new ArgumentException($"Thiếu giá trị cho {args[index]}.");
        switch (args[index])
        {
            case "--config": configPath = args[index + 1]; break;
            case "--clients": clients = ParseInt(args[index + 1], 1, 11); break;
            case "--duration": durationSeconds = ParseInt(args[index + 1], 0, 3600); break;
            case "--name": displayName = args[index + 1]; break;
            default: throw new ArgumentException($"Tham số không hỗ trợ: {args[index]}. Dùng --help.");
        }
    }
    if (string.IsNullOrWhiteSpace(configPath)) throw new ArgumentException("Cần --config <đường dẫn JSON>.");
    var options = ClientOptions.Load(configPath);
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
    var roomCode = CredentialInput.ReadSecret("NET_ROOM_CODE", "Mã phòng (ẩn khi nhập): ", shutdown.Token);
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
    for (var number = 1; number <= clients; number++)
    {
        var name = clients == 1 ? displayName : displayName[..Math.Min(29, displayName.Length)] + "-" + number;
        var session = new SmokeSession(number, options, roomCode, name, accessToken);
        sessions.Add(session); // Ghi vào danh sách trước Start để lỗi giữa chừng vẫn được Dispose.
        session.Start();
    }
    var started = Stopwatch.GetTimestamp();
    while (!shutdown.IsCancellationRequested && sessions.Any(session => !session.Finished)
        && (durationSeconds == 0 || Stopwatch.GetElapsedTime(started).TotalSeconds < durationSeconds))
    {
        foreach (var session in sessions) session.Update();
        Thread.Sleep(5);
    }
    foreach (var session in sessions) session.PrintSummary();
    var connected = sessions.Count(session => session.ConnectedOnce);
    var joined = sessions.Count(session => session.Joined);
    var failures = sessions.Count(session => session.Failed || !session.Joined || session.Pongs == 0);
    Console.WriteLine($"[summary] requested={clients} connected={connected} joined={joined} failed={failures} pongs={sessions.Sum(session => session.Pongs)}");
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
