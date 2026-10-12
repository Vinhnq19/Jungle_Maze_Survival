using System.Diagnostics;
using GameServer.Diagnostics;

namespace GameServer.Simulation;

/// <summary>Stopwatch/accumulator quyết định nhịp; Sleep chỉ nhường CPU giữa các lần poll.</summary>
public sealed class FixedTickLoop(int tickRate, ServerMetrics metrics)
{
    public const int MaxCatchUpSteps = 5;

    public void Run(Action pollNetwork, Action<double> tick, Action reportMetrics,
        CancellationToken cancellationToken)
    {
        var step = 1.0 / tickRate;
        var previous = Stopwatch.GetTimestamp();
        var accumulator = 0.0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var loopStarted = Stopwatch.GetTimestamp();
            accumulator += (loopStarted - previous) / (double)Stopwatch.Frequency;
            previous = loopStarted;
            pollNetwork();
            var steps = 0;
            while (accumulator >= step && steps < MaxCatchUpSteps)
            {
                var tickStarted = Stopwatch.GetTimestamp();
                tick(step); // Gameplay luôn nhận delta cố định 1/30, không nhận delta khung hình.
                metrics.RecordTick(Stopwatch.GetElapsedTime(tickStarted).TotalMilliseconds, step * 1000);
                accumulator -= step;
                steps++;
            }
            if (accumulator >= step)
            {
                // Bỏ backlog nguyên tick, giữ dư để tránh spiral of death. Báo quá tải rõ ràng.
                var dropped = (int)(accumulator / step);
                metrics.RecordDroppedTicks(dropped);
                accumulator %= step;
            }
            // Work-loop gồm network + tick; không tính sleep và log định kỳ.
            metrics.RecordLoop(Stopwatch.GetElapsedTime(loopStarted).TotalMilliseconds);
            reportMetrics();
            var remainingMs = (step - accumulator - Stopwatch.GetElapsedTime(loopStarted).TotalSeconds) * 1000;
            // Poll khoảng 5ms khi rảnh; không Sleep(33) rồi coi đó là lịch tick.
            if (remainingMs >= 1) Thread.Sleep((int)Math.Min(5, remainingMs));
            else Thread.Sleep(1);
        }
    }
}
