using TelemetryChecks;

// Chế độ: (mặc định) kiểm chức năng; --bench vi đo AC32.
var mode = args.Length > 0 ? args[0] : "";
var checker = new Checker();
Console.WriteLine("=== Starting Jungle Maze TelemetryChecks ===");

switch (mode)
{
    case "--bench":
        Bench.Run(checker);
        break;
    case "":
        CoreChecks.Run(checker, SchemaKeys.Load());
        break;
    default:
        Console.Error.WriteLine($"Unknown mode '{mode}'. Use no argument or --bench.");
        return 1;
}

Console.WriteLine($"\n=== TelemetryChecks: {checker.Passed} passed, {checker.Failed} failed ===");
return checker.Failed == 0 ? 0 : 1;
