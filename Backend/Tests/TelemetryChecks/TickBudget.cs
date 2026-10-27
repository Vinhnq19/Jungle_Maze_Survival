using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GameServer.Diagnostics;
using GameServer.Simulation;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.Telemetry.Server;

namespace TelemetryChecks;

/// <summary>
/// AC33/AC34 (Phase A, proxy, tải giả trong tiến trình): FixedTickLoop + ServerMetrics thật, một phòng 10 phiên giả,
/// tắt/bật telemetry xen kẽ ABBA. Số tick lấy từ chính dòng [metrics] của ServerMetrics, không tự đo.
/// </summary>
public static partial class TickBudget
{
    private const int Players = 10;
    private const int MetricsIntervalSeconds = 10;
    public const double MaxDeltaMs = 0.5;
    public const double TickBudgetMs = 1000.0 / 30;

    public sealed record RunResult(int Load, int Run, bool On, int Windows, double TickP95Ms, double TickP99Ms, int TickOverruns,
        int DroppedTicks, double WorkingSetMiB, long Records, long DroppedQueue);

    [GeneratedRegex(@"(\w+)=([^\s]+)")]
    private static partial Regex KeyValue();

    public static void Run(Checker c, int runs, int seconds, int[] loads, string? outPath)
    {
        c.Section($"Tick budget: {runs} runs x {seconds}s x loads [{string.Join(",", loads)}] (proxy, synthetic load, in-process)");
        var (map, zone, _) = MatchFlowChecks.LoadData();
        var profile = new MatchProfile
        {
            Version = "1.0.0", Name = "test", ConfigHash = GameDataLoader.DataHash, MinPlayers = 2, MaxPlayers = 10,
            LobbyDurationSeconds = 0.1, LoadingTimeoutSeconds = 5, DisconnectGraceSeconds = 15, EndingDurationSeconds = 1,
            TargetMatchDurationSeconds = 420, ZoneTimeScale = 1, PlayerSpeed = 4, PlayerRadius = 0.4f, PlayerHp = 100, InputStaleMs = 250
        };
        profile.Validate();

        var results = new List<RunResult>();
        var root = Path.Combine(Path.GetTempPath(), "tel01-budget-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            foreach (var load in loads)
            {
                for (var run = 1; run <= runs; run++)
                {
                    var order = run % 2 == 1 ? new[] { false, true } : new[] { true, false }; // ABBA
                    foreach (var on in order)
                    {
                        var r = RunOnce(map, zone, profile, on, load, run, seconds, Path.Combine(root, $"{load}-{run}-{(on ? "on" : "off")}"));
                        results.Add(r);
                        Console.WriteLine(FormattableString.Invariant(
                            $"[budget] load={r.Load} run={r.Run} telemetry={(r.On ? "on" : "off")} windows={r.Windows} tickP95Ms={r.TickP95Ms:F3} tickP99Ms={r.TickP99Ms:F3} tickOverruns={r.TickOverruns} droppedTicks={r.DroppedTicks} workingSetMiB={r.WorkingSetMiB:F1} records={r.Records} droppedQueue={r.DroppedQueue}"));
                    }
                }
            }
        }
        finally
        {
            CoreChecks.TryDelete(root);
        }

        foreach (var load in loads)
        {
            var off = results.Where(r => r.Load == load && !r.On).OrderBy(r => r.Run).ToList();
            var on = results.Where(r => r.Load == load && r.On).OrderBy(r => r.Run).ToList();
            var deltas = on.Zip(off, (a, b) => a.TickP95Ms - b.TickP95Ms).OrderBy(d => d).ToList();
            var median = Median(deltas);
            Console.WriteLine(FormattableString.Invariant(
                $"[budget] load={load} medianDeltaTickP95Ms={median:F3} deltas=[{string.Join(",", deltas.Select(d => d.ToString("F3", CultureInfo.InvariantCulture)))}]"));
            c.Check(median <= MaxDeltaMs, FormattableString.Invariant($"load {load}/s: median(on - off) tickP95Ms = {median:F3} <= {MaxDeltaMs}"));
            c.Check(results.Where(r => r.Load == load).All(r => r.TickP95Ms < TickBudgetMs && r.Windows > 0),
                $"load {load}/s: every run tickP95Ms < 33.3");
            c.Check(results.Where(r => r.Load == load).All(r => r.DroppedTicks == 0), $"load {load}/s: droppedTicks = 0 in every run");
            c.Check(on.Sum(r => r.TickOverruns) <= off.Sum(r => r.TickOverruns),
                $"load {load}/s: tickOverruns on ({on.Sum(r => r.TickOverruns)}) <= off ({off.Sum(r => r.TickOverruns)})");
            c.Check(on.All(r => r.Records > 0), $"load {load}/s: telemetry-on runs wrote records; droppedQueue visible in _trailer (AC34)");
        }

        if (outPath != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
            var csv = new StringBuilder("load,run,telemetry,windows,tickP95Ms,tickP99Ms,tickOverruns,droppedTicks,workingSetMiB,records,droppedQueue\n");
            foreach (var r in results)
                csv.Append(FormattableString.Invariant(
                    $"{r.Load},{r.Run},{(r.On ? "on" : "off")},{r.Windows},{r.TickP95Ms:F3},{r.TickP99Ms:F3},{r.TickOverruns},{r.DroppedTicks},{r.WorkingSetMiB:F1},{r.Records},{r.DroppedQueue}\n"));
            File.WriteAllText(outPath, csv.ToString(), new UTF8Encoding(false));
            Console.WriteLine($"[budget] wrote {outPath}");
        }
    }

    private static double Median(IReadOnlyList<double> sorted)
    {
        if (sorted.Count == 0) return double.NaN;
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }

    private static RunResult RunOnce(MapData map, ZoneSchedule zone, MatchProfile profile, bool on, int load, int run, int seconds, string root)
    {
        var match = new MatchRuntime("BUDGET", map, zone, profile, "room-budget");
        var host = on ? TelemetryHost.Create(new TelemetryOptions { RootPath = root }, TextWriter.Null) : null;
        ITelemetrySink sink = host?.Attach(match) ?? (ITelemetrySink)NullTelemetrySink.Instance;

        var sessions = Enumerable.Range(0, Players).Select(i => $"budget-{i}").ToArray();
        foreach (var s in sessions)
        {
            match.TryRegisterPlayer(s, s, out _);
            match.SetPlayerReady(s);
        }
        for (var i = 0; i < 200 && match.Phase != MatchPhase.Loading; i++) match.Tick(1.0 / 30);
        foreach (var s in sessions) match.SetPlayerLoaded(s, match.MatchId);
        for (var i = 0; i < 10 && match.Phase != MatchPhase.InGame; i++) match.Tick(1.0 / 30);

        var random = new Random(load * 31 + run);
        var seq = new ushort[Players];
        var moveX = new sbyte[Players];
        var moveY = new sbyte[Players];
        var perTick = load / 30.0;
        var accumulator = 0.0;
        var emitted = 0L;

        void Tick(double dt)
        {
            if (match.CurrentTick % 30 == 0)
                for (var p = 0; p < Players; p++)
                {
                    moveX[p] = (sbyte)random.Next(-127, 128);
                    moveY[p] = (sbyte)random.Next(-127, 128);
                }
            for (var p = 0; p < Players; p++)
            {
                var input = new PlayerInputPacket { Tick = match.CurrentTick, InputSeq = ++seq[p], MoveX = moveX[p], MoveY = moveY[p] };
                match.ProcessInput(sessions[p], in input);
            }
            match.Tick(dt);
            // Tải H7 giả (mô phỏng WPN-02 gọi sink): ~load bản ghi/s, xen kẽ attack và damage chi tiết.
            accumulator += perTick;
            while (accumulator >= 1)
            {
                accumulator -= 1;
                var netId = (byte)(emitted % Players);
                var e = (emitted & 1) == 0
                    ? TelemetryEvent.Attack(netId, "KNIFE", TelemetryAttackKind.Melee, 10f, 10f)
                    : TelemetryEvent.DamageDetail((byte)((netId + 1) % Players), netId, "KNIFE", null, 22, 0);
                sink.TryWrite(in e);
                emitted++;
            }
        }

        var metrics = new ServerMetrics();
        var loop = new FixedTickLoop(30, metrics);
        var captured = new StringWriter();
        var console = Console.Out;
        Console.SetOut(captured);
        try
        {
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
            loop.Run(() => { }, Tick, () => metrics.ReportIfDue(Players, MetricsIntervalSeconds, Players, 0, 1, ""), stop.Token);
        }
        finally
        {
            Console.SetOut(console);
        }
        host?.Dispose();

        // Bỏ cửa sổ đầu (khởi động JIT, cấp phát ban đầu).
        var windows = captured.ToString().Split('\n').Where(l => l.StartsWith("[metrics]", StringComparison.Ordinal))
            .Select(l => KeyValue().Matches(l).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value)).Skip(1).ToList();
        double D(Dictionary<string, string> w, string k) => double.Parse(w[k], CultureInfo.InvariantCulture);
        var p95 = windows.Select(w => D(w, "tickP95Ms")).OrderBy(v => v).ToList();
        var p99 = windows.Select(w => D(w, "tickP99Ms")).OrderBy(v => v).ToList();

        long records = 0, droppedQueue = 0;
        if (on && Directory.Exists(root))
        {
            foreach (var file in Directory.GetFiles(root, "*.jsonl", SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(file);
                records += Math.Max(0, lines.Length - 2);
                var trailer = lines.LastOrDefault(l => l.Contains("\"_trailer\""));
                if (trailer != null)
                    droppedQueue += System.Text.Json.JsonDocument.Parse(trailer).RootElement.GetProperty("droppedQueue").GetInt64();
            }
        }

        return new RunResult(load, run, on, windows.Count, Median(p95), Median(p99),
            windows.Sum(w => (int)D(w, "tickOverruns")), windows.Sum(w => (int)D(w, "droppedTicks")),
            windows.Count == 0 ? 0 : windows.Max(w => D(w, "workingSetMiB")), records, droppedQueue);
    }
}
