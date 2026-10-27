#nullable enable
using System;
using System.Globalization;
using System.IO;
using GameServer.Rooms;
using GameServer.Simulation;

namespace JungleMaze.Telemetry.Server;

public sealed class TelemetryOptions
{
    public string RootPath { get; init; } = "";
    public double SampleHz { get; init; } = 1;
    public int MaxFileMiB { get; init; } = 8;
    public int MaxTotalMiB { get; init; } = 256;
    public string Build { get; init; } = "dev";
}

/// <summary>
/// Điểm nối H1. Không có TELEMETRY_PATH thì trả host tắt: không tạo thư mục, không tạo luồng, không đăng ký event.
/// PUBLIC_HOSTING=true thì từ chối bật (G-9).
/// </summary>
public sealed class TelemetryHost : IDisposable
{
    private int _disposed;

    public bool Enabled => Writer is { Enabled: true };
    public TelemetryOptions Options { get; }
    public JsonlTelemetryWriter? Writer { get; }

    private TelemetryHost(TelemetryOptions options, JsonlTelemetryWriter? writer)
    {
        Options = options;
        Writer = writer;
    }

    public static TelemetryHost Disabled() => new(new TelemetryOptions(), null);

    /// <summary>Bật telemetry với tùy chọn cho trước (test và harness). Luồng nền bắt đầu ngay.</summary>
    public static TelemetryHost Create(TelemetryOptions options, TextWriter? log = null, Func<string, Stream>? openStream = null,
        int intervalMs = 50)
    {
        var output = log ?? Console.Out;
        var writer = new JsonlTelemetryWriter(new TelemetryWriterOptions
        {
            RootPath = options.RootPath,
            MaxFileBytes = options.MaxFileMiB * 1024L * 1024L,
            MaxTotalBytes = options.MaxTotalMiB * 1024L * 1024L,
            Build = options.Build,
            IntervalMs = intervalMs,
            OpenStream = openStream,
            Log = output
        });
        writer.Start();
        output.WriteLine(FormattableString.Invariant(
            $"[telemetry] enabled wid={writer.Wid} sampleHz={options.SampleHz} maxFileMiB={options.MaxFileMiB} maxTotalMiB={options.MaxTotalMiB}"));
        return new TelemetryHost(options, writer);
    }

    public static TelemetryHost FromEnvironment(TextWriter? log = null) =>
        FromValues(Environment.GetEnvironmentVariable, log);

    public static TelemetryHost FromValues(Func<string, string?> env, TextWriter? log = null, Func<string, Stream>? openStream = null)
    {
        var output = log ?? Console.Out;
        var root = env("TELEMETRY_PATH");
        if (string.IsNullOrWhiteSpace(root)) return Disabled();

        if (string.Equals(env("PUBLIC_HOSTING"), "true", StringComparison.OrdinalIgnoreCase))
        {
            output.WriteLine("[telemetry] TELEMETRY_PATH ignored: telemetry is not allowed with PUBLIC_HOSTING=true.");
            return Disabled();
        }

        var options = new TelemetryOptions
        {
            RootPath = root.Trim(),
            SampleHz = Math.Clamp(ParseDouble(env("TELEMETRY_SAMPLE_HZ"), 1), 0.5, 2),
            MaxFileMiB = Math.Clamp(ParseInt(env("TELEMETRY_MAX_FILE_MIB"), 8), 1, 1024),
            MaxTotalMiB = Math.Clamp(ParseInt(env("TELEMETRY_MAX_TOTAL_MIB"), 256), 1, 65536),
            Build = env("TELEMETRY_BUILD") ?? "dev"
        };
        return Create(options, output, openStream);
    }

    private static double ParseDouble(string? value, double fallback) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : fallback;

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    /// <summary>Phòng không phải mặc định, trống và im lặng quá khoảng này thì gỡ observer (phòng đã hết hạn hoặc bị xóa).</summary>
    public TimeSpan StaleRoomAfter { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>H1: gắn telemetry vào một phòng. Host tắt thì không làm gì và trả null.</summary>
    public MatchTelemetryObserver? Attach(RoomManager.Room room)
    {
        var probe = room.IsDefault ? null : new RoomStaleProbe(() => room.Peers.Count == 0, StaleRoomAfter);
        return Attach(room.Match, probe);
    }

    /// <summary>Gắn vào một MatchRuntime (test, harness). probe = null: không bao giờ tự gỡ.</summary>
    public MatchTelemetryObserver? Attach(MatchRuntime match, RoomStaleProbe? probe = null, TelemetryRing? ring = null)
    {
        if (Writer is not { Enabled: true } writer) return null;
        var observer = new MatchTelemetryObserver(match, writer, ring);
        writer.Register(new MatchTelemetryAssembler(observer, writer, Options.SampleHz, probe));
        observer.Subscribe();
        if ((object)match is ITelemetryAware aware) aware.Telemetry = observer; // H7-prep: chỉ khi Vinh thêm interface
        return observer;
    }

    /// <summary>Flush tối đa 3 s và ghi _trailer cho mọi file đang mở. Gọi lại là no-op.</summary>
    public void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0) return;
        Writer?.Dispose();
    }
}
