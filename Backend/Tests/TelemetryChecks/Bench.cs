using System.Diagnostics;
using GameServer.Simulation;
using JungleMaze.Telemetry.Server;

namespace TelemetryChecks;

/// <summary>AC32: vi đo TryWrite, 1 triệu lần, p99 ≤ 10 µs và 0 byte cấp phát sau khởi động.</summary>
public static class Bench
{
    public const int Iterations = 1_000_000;
    public const double P99LimitUs = 10.0;

    public static void Run(Checker c)
    {
        c.Section($"Bench: TryWrite x {Iterations:N0}");
        var tickToUs = 1_000_000.0 / Stopwatch.Frequency;
        Console.WriteLine(FormattableString.Invariant(
            $"[bench] cpus={Environment.ProcessorCount} os={Environment.OSVersion} runtime={Environment.Version} clockResolutionUs={tickToUs:F3}"));
        var samples = new long[Iterations];

        // Sink H7 thật của một phòng (đóng dấu tick + utc rồi vào ring), ring được rút liên tục:
        // đường ghi thành công. Tải thật ≤ 1 000 bản ghi/s nên ring gần như luôn rỗng.
        var (map, zone, profile) = MatchFlowChecks.LoadData();
        var match = new MatchRuntime("BENCH", map, zone, profile, "room-bench");
        var writer = new JsonlTelemetryWriter(new TelemetryWriterOptions { RootPath = Path.GetTempPath(), Log = TextWriter.Null });
        var observer = new MatchTelemetryObserver(match, writer);
        var draining = MeasureWithConsumer(observer, observer.Ring, samples, tickToUs, "sink-draining");

        // Ring đầy, không ai rút: đường bỏ bản ghi.
        var fullRing = new TelemetryRing(1024, 512);
        var fullObserver = new MatchTelemetryObserver(match, writer, fullRing);
        while (fullRing.TryWrite(new TelemetryEvent { Kind = TelemetryKind.Damaged })) { }
        var full = Measure(fullObserver, samples, tickToUs, "sink-full");

        c.Check(draining.P99Us <= P99LimitUs && full.P99Us <= P99LimitUs,
            FormattableString.Invariant($"TryWrite p99 <= {P99LimitUs} us (draining {draining.P99Us:F3}, full {full.P99Us:F3})"));
        c.Check(draining.AllocatedBytes == 0 && full.AllocatedBytes == 0,
            $"Zero bytes allocated after warm-up (draining {draining.AllocatedBytes}, full {full.AllocatedBytes})");
        c.Check(draining.Accepted >= Iterations * 99L / 100 && full.Accepted == 0,
            $"Draining mode accepts >= 99% of writes ({draining.Accepted}); full mode drops every write ({full.Accepted} accepted)");
    }

    public readonly record struct Result(double P50Us, double P99Us, double MaxUs, long AllocatedBytes, long Accepted);

    private static Result MeasureWithConsumer(ITelemetrySink sink, TelemetryRing ring, long[] samples, double tickToUs, string label)
    {
        var stop = 0;
        var consumer = new Thread(() =>
        {
            var spin = new SpinWait();
            while (Volatile.Read(ref stop) == 0)
            {
                if (ring.TryRead(out _)) continue;
                spin.SpinOnce(sleep1Threshold: -1);
            }
        }) { IsBackground = true };
        consumer.Start();
        var result = Measure(sink, samples, tickToUs, label);
        Volatile.Write(ref stop, 1);
        consumer.Join();
        return result;
    }

    private static Result Measure(ITelemetrySink sink, long[] samples, double tickToUs, string label)
    {
        var e = TelemetryEvent.DamageDetail(3, 7, "KNIFE", null, 30, 5, 10, -1);

        for (var i = 0; i < 200_000; i++) sink.TryWrite(in e); // khởi động: JIT, tier-up

        long accepted = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < samples.Length; i++)
        {
            var start = Stopwatch.GetTimestamp();
            var ok = sink.TryWrite(in e);
            samples[i] = Stopwatch.GetTimestamp() - start;
            if (ok) accepted++;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Array.Sort(samples);
        double At(double q) => samples[(int)Math.Ceiling(q * samples.Length) - 1] * tickToUs;
        var result = new Result(At(0.50), At(0.99), samples[^1] * tickToUs, allocated, accepted);
        Console.WriteLine(FormattableString.Invariant(
            $"[bench] mode={label} n={samples.Length} p50Us={result.P50Us:F3} p99Us={result.P99Us:F3} maxUs={result.MaxUs:F3} allocatedBytes={allocated} accepted={accepted}"));
        return result;
    }
}
