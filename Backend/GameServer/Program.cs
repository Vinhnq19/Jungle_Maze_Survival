using GameServer.Configuration;
using GameServer.Diagnostics;
using GameServer.Networking;
using GameServer.Simulation;
using System.Runtime.InteropServices;
using GameServer.Rooms;

// Composition root: cấu hình -> mở UDP -> poll + tick -> đóng socket khi dừng.
try
{
    var options = ServerOptions.FromEnvironment();
    using var shutdown = new CancellationTokenSource();
    Console.CancelKeyPress += (_, args) =>
    {
        args.Cancel = true; // Cho using/Dispose dọn socket thay vì kết thúc ngay lập tức.
        shutdown.Cancel();
    };
    // docker stop gửi SIGTERM trên Linux, không gửi Ctrl+C.
    using var terminate = !OperatingSystem.IsWindows()
        ? PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            shutdown.Cancel();
        }) : null;
    var metrics = new ServerMetrics();
    using var network = new NetworkHost(options, metrics);
    network.Start();
    await using var webSocket = await WebSocketHost.StartFromEnvironment(network.Hub, (options.Rooms ?? new()).MaxTotalPlayers);
    var roomControl = RoomControlHost.Start(options.Rooms ?? new(), network.Commands, network.Hub);

    // Persistence match outbox outside simulation tick
    var outboxApiUrl = Environment.GetEnvironmentVariable("MATCH_RESULTS_API_URL") ?? "http://127.0.0.1:5000/internal/match-results";
    var outboxToken = Environment.GetEnvironmentVariable("MATCH_RESULT_TOKEN") ?? "";
    var outboxPath = Environment.GetEnvironmentVariable("MATCH_OUTBOX_PATH") ?? "./data/match-outbox";

    using var outbox = new MatchOutbox(new MatchOutboxOptions
    {
        ApiUrl = outboxApiUrl,
        Token = outboxToken,
        OutboxPath = outboxPath
    }, metrics);
    outbox.Start();

    void HookRoomMatch(RoomManager.Room room)
    {
        room.Match.MatchCompleted += result => outbox.TryEnqueue(result);
    }
    network.Hub.Rooms.RoomCreated += HookRoomMatch;
    foreach (var room in network.Hub.Rooms.AllRooms)
    {
        HookRoomMatch(room);
    }

    var loop = new FixedTickLoop(options.TickRate, metrics);
    try {
        loop.Run(
            () => { webSocket?.PollEvents(); network.PollEvents(); },
            deltaSeconds => network.Hub.Tick(deltaSeconds),
            () => metrics.ReportIfDue(network.ReservedPeers, options.MetricsIntervalSeconds,
                network.ActivePeers, network.PendingPeers, network.Rooms.Count, network.Rooms.Occupancy()),
            shutdown.Token);
    }
    finally {
        if (roomControl is not null) { await roomControl.StopAsync(); await roomControl.DisposeAsync(); }
        using var outboxDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await outbox.StopAsync(outboxDeadline.Token);
    }
    return 0;
}
catch (Exception error) when (error is ArgumentException or InvalidOperationException
    or System.Net.Sockets.SocketException)
{
    Console.Error.WriteLine($"[startup-error] {error.Message}");
    return 1;
}
