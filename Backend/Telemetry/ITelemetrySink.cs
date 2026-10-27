#nullable enable
namespace JungleMaze.Telemetry.Server;

/// <summary>
/// Cổng ghi telemetry (hợp đồng H7). TryWrite không chặn, không cấp phát, không I/O, không khóa,
/// không ném lỗi; trả false khi tắt hoặc hàng đầy. Xem Docs/Telemetry/Hook_Contract.vi.md.
/// </summary>
public interface ITelemetrySink
{
    bool Enabled { get; }
    long Dropped { get; }
    bool TryWrite(in TelemetryEvent e);
}

/// <summary>Sink mặc định khi telemetry tắt.</summary>
public sealed class NullTelemetrySink : ITelemetrySink
{
    public static readonly NullTelemetrySink Instance = new();
    private NullTelemetrySink() { }
    public bool Enabled => false;
    public long Dropped => 0;
    public bool TryWrite(in TelemetryEvent e) => false;
}

/// <summary>H7-prep: MatchRuntime implement interface này để TelemetryHost.Attach gắn sink của phòng.</summary>
public interface ITelemetryAware
{
    ITelemetrySink Telemetry { get; set; }
}
