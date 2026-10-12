using System.Diagnostics;

namespace GameServer.Diagnostics;

/// <summary>Số đo theo cửa sổ vài giây; không log từng tick hoặc giữ mẫu vô hạn.</summary>
public sealed class ServerMetrics
{
    private readonly List<double> _tickMs = new();
    private readonly List<double> _loopMs = new();
    private long _totalTicks;
    private int _pings, _invalidPackets, _rejections, _tickOverruns, _droppedTicks;
    private double _windowStarted = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    private double _cpuStartedMs = ReadCpuMs();

    public void RecordPing() => _pings++;
    public void RecordInvalidPacket() => _invalidPackets++;
    public void RecordRejection() => _rejections++;
    public void RecordDroppedTicks(int count) => _droppedTicks += count;

    public void RecordTick(double elapsedMs, double budgetMs)
    {
        _totalTicks++;
        _tickMs.Add(elapsedMs);
        if (elapsedMs > budgetMs) _tickOverruns++;
    }

    public void RecordLoop(double elapsedMs)
    {
        // Tối đa 20.000 mẫu mỗi cửa sổ: không cấp bộ nhớ vô hạn khi poll nhanh.
        if (_loopMs.Count < 20000) _loopMs.Add(elapsedMs);
    }

    public void ReportIfDue(int peers, int intervalSeconds, int active = 0, int pending = 0)
    {
        var now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        var seconds = now - _windowStarted;
        if (seconds < intervalSeconds) return;
        var cpuMs = ReadCpuMs();
        var cpuPercent = (cpuMs - _cpuStartedMs) / (seconds * 1000) * 100;
        // CPU một core: 100% = một core bận toàn thời gian, có thể >100%.
        Console.WriteLine(FormattableString.Invariant(
            $"[metrics] peers={peers} active={active} pending={pending} ticks={_totalTicks} ticksPerSecond={_tickMs.Count / seconds:F2} tickP95Ms={Percentile(_tickMs, .95):F3} tickP99Ms={Percentile(_tickMs, .99):F3} loopP95Ms={Percentile(_loopMs, .95):F3} loopP99Ms={Percentile(_loopMs, .99):F3} cpuOneCorePercent={cpuPercent:F2} tickOverruns={_tickOverruns} droppedTicks={_droppedTicks} pings={_pings} invalidPackets={_invalidPackets} rejected={_rejections}"));
        _tickMs.Clear();
        _loopMs.Clear();
        _pings = _invalidPackets = _rejections = _tickOverruns = _droppedTicks = 0;
        _windowStarted = now;
        _cpuStartedMs = cpuMs;
    }

    private static double ReadCpuMs()
    {
        using var process = Process.GetCurrentProcess();
        return process.TotalProcessorTime.TotalMilliseconds;
    }

    private static double Percentile(List<double> samples, double percentile)
    {
        if (samples.Count == 0) return 0;
        // Nearest rank: ceil(percentile * N) trên danh sách đã sắp tăng dần.
        samples.Sort();
        return samples[(int)Math.Ceiling(samples.Count * percentile) - 1];
    }
}
