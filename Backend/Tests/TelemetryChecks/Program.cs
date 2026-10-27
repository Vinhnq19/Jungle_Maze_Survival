using System.Globalization;
using TelemetryChecks;

// Chế độ: (mặc định) kiểm chức năng; --bench vi đo AC32; --tick-budget [--runs 3 --seconds 60 --load 70,1000 --out file]
// đo ngân sách tick AC33/AC34; --emit-sample <dir> sinh file JSONL thật cho kiểm chéo với Python.
var mode = args.Length > 0 ? args[0] : "";
var checker = new Checker();
Console.WriteLine("=== Starting Jungle Maze TelemetryChecks ===");

string? Option(string name) => Array.IndexOf(args, name) is var i and >= 0 && i + 1 < args.Length ? args[i + 1] : null;

switch (mode)
{
    case "":
        var schema = SchemaKeys.Load();
        CoreChecks.Run(checker, schema);
        MatchFlowChecks.Run(checker, schema);
        break;
    case "--bench":
        Bench.Run(checker);
        break;
    case "--tick-budget":
        var runs = int.Parse(Option("--runs") ?? "3", CultureInfo.InvariantCulture);
        var seconds = int.Parse(Option("--seconds") ?? "60", CultureInfo.InvariantCulture);
        var loads = (Option("--load") ?? "70,1000").Split(',').Select(v => int.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        TickBudget.Run(checker, runs, seconds, loads, Option("--out"));
        break;
    case "--emit-sample":
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: --emit-sample <dir>");
            return 1;
        }
        EmitSample.Run(checker, args[1]);
        break;
    default:
        Console.Error.WriteLine($"Unknown mode '{mode}'. Use no argument, --bench, --tick-budget or --emit-sample <dir>.");
        return 1;
}

Console.WriteLine($"\n=== TelemetryChecks: {checker.Passed} passed, {checker.Failed} failed ===");
return checker.Failed == 0 ? 0 : 1;
