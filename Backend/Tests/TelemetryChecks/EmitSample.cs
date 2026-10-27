namespace TelemetryChecks;

/// <summary>Sinh file JSONL do C# thật ghi (hai trận liên tiếp + kịch bản H7) để QA chạy validate của Python trên đó.</summary>
public static class EmitSample
{
    public static void Run(Checker c, string outDir)
    {
        c.Section($"Emit C# server samples to {outDir}");
        var root = Path.Combine(Path.GetTempPath(), "tel01-emit-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var flow = MatchFlowChecks.RunFlow(Path.Combine(root, "flow"));
            var h7 = MatchFlowChecks.RunH7(Path.Combine(root, "h7"));
            Directory.CreateDirectory(outDir);
            var targets = new List<(Guid MatchId, string Root, string Name)>
            {
                (flow.MatchIds[0], flow.Root, "server_flow_match1.jsonl"),
                (flow.MatchIds[1], flow.Root, "server_flow_match2.jsonl"),
                (h7.MatchId, h7.Root, "server_h7.jsonl")
            };
            foreach (var (matchId, sourceRoot, name) in targets)
            {
                var source = Directory.GetFiles(sourceRoot, $"{matchId:D}.server.*.jsonl", SearchOption.AllDirectories).Single();
                var target = Path.Combine(outDir, name);
                File.Copy(source, target, overwrite: true);
                var lines = File.ReadAllLines(target).Length;
                c.Check(lines > 2, $"{name}: {lines} lines");
            }
        }
        finally
        {
            CoreChecks.TryDelete(root);
        }
    }
}
