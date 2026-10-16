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
    var roomControl = RoomControlHost.Start(options.Rooms ?? new(), network.Commands);
    var loop = new FixedTickLoop(options.TickRate, metrics);
    try { loop.Run(() => { webSocket?.PollEvents(); network.PollEvents(); }, Tick,
        () => metrics.ReportIfDue(network.ReservedPeers, options.MetricsIntervalSeconds,
            network.ActivePeers, network.PendingPeers, network.Rooms.Count, network.Rooms.Occupancy()), shutdown.Token); }
    finally { if (roomControl is not null) { await roomControl.StopAsync(); await roomControl.DisposeAsync(); } }
    return 0;
}
catch (Exception error) when (error is ArgumentException or InvalidOperationException
    or System.Net.Sockets.SocketException)
{
    Console.Error.WriteLine($"[startup-error] {error.Message}");
    return 1;
}

static void Tick(double fixedDeltaSeconds)
{
    // Chưa có gameplay: điểm gắn hàng đợi input/mô phỏng authoritative sau này.
    // Không gọi HTTP/MongoDB hoặc await trong Tick: tác vụ chậm phá ngân sách 33,3ms.
}
