using System.Diagnostics;
using JungleMaze.Telemetry.Server;

namespace TelemetryChecks;

/// <summary>AC32: vi đo TryWrite, 1 triệu lần, p99 ≤ 10 µs và 0 byte cấp phát sau khởi động.</summary>
public static class Bench
{
    public const int Iterations = 1_000_000;
    public const double P99LimitUs = 10.0;

    public static void Run(Checker c, ITelemetrySink? sinkUnderTest = null, string sinkName = "ring")
    {
        c.Section($"Bench: TryWrite x {Iterations:N0} ({sinkName})");
        var tickToUs = 1_000_000.0 / Stopwatch.Frequency;
        Console.WriteLine(FormattableString.Invariant(
            $"[bench] cpus={Environment.ProcessorCount} os={Environment.OSVersion} runtime={Environment.Version} clockResolutionUs={tickToUs:F3}"));

        var samples = new long[Iterations];

        // Chế độ 1: ring được rút liên tục, đo đường ghi thành công (tải thật ≤ 1 000 bản ghi/s nên ring gần như rỗng).
        var ring = new TelemetryRing();
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
        var draining = Measure(ring, sinkUnderTest, samples, tickToUs, "draining");
        Volatile.Write(ref stop, 1);
        consumer.Join();

        // Chế độ 2: ring đầy, không ai rút (đường bỏ bản ghi).
        var fullRing = new TelemetryRing(1024, 512);
        while (fullRing.TryWrite(new TelemetryEvent { Kind = TelemetryKind.Damaged })) { }
        var full = Measure(fullRing, null, samples, tickToUs, "full");

        c.Check(draining.P99Us <= P99LimitUs && full.P99Us <= P99LimitUs,
            FormattableString.Invariant($"TryWrite p99 <= {P99LimitUs} us (draining {draining.P99Us:F3}, full {full.P99Us:F3})"));
        c.Check(draining.AllocatedBytes == 0 && full.AllocatedBytes == 0,
            $"Zero bytes allocated after warm-up (draining {draining.AllocatedBytes}, full {full.AllocatedBytes})");
    }

    public readonly record struct Result(double P50Us, double P99Us, double MaxUs, long AllocatedBytes, long Accepted);

    private static Result Measure(TelemetryRing ring, ITelemetrySink? sink, long[] samples, double tickToUs, string label)
    {
        var frame = new byte[256];
        var e = new TelemetryEvent { Kind = TelemetryKind.Damaged, A = 3, B = 7, C = 80, I0 = 20, Ref = frame };

        // Khởi động: JIT, tier-up.
        for (var i = 0; i < 200_000; i++)
        {
            if (sink != null) sink.TryWrite(in e);
            else { e.Tick = (uint)i; e.Utc = DateTime.UtcNow.Ticks; ring.TryWrite(in e); }
        }

        long accepted = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < samples.Length; i++)
        {
            var start = Stopwatch.GetTimestamp();
            bool ok;
            if (sink != null) ok = sink.TryWrite(in e);
            else
            {
                e.Tick = (uint)i;
                e.Utc = DateTime.UtcNow.Ticks;
                ok = ring.TryWrite(in e);
            }
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
