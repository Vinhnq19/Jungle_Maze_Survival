using System.Text.Json;

namespace TelemetryChecks;

/// <summary>In [PASS]/[FAIL] giống MatchChecks; mã thoát ≠ 0 khi có FAIL.</summary>
public sealed class Checker
{
    public int Passed { get; private set; }
    public int Failed { get; private set; }

    public void Check(bool condition, string name, string? detail = null)
    {
        if (condition)
        {
            Console.WriteLine($"[PASS] {name}");
            Passed++;
            return;
        }
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(detail is null ? $"[FAIL] {name}" : $"[FAIL] {name} :: {detail}");
        Console.ResetColor();
        Failed++;
    }

    public void Section(string title) => Console.WriteLine($"\n--- {title} ---");
}

/// <summary>Đọc schema chung để kiểm tên và thứ tự khóa của dòng C# sinh ra (kiểm chéo với Python).</summary>
public sealed class SchemaKeys
{
    public IReadOnlyList<string> Envelope { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Types { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Aux { get; }

    private SchemaKeys(IReadOnlyList<string> envelope, IReadOnlyDictionary<string, IReadOnlyList<string>> types,
        IReadOnlyDictionary<string, IReadOnlyList<string>> aux)
    {
        Envelope = envelope;
        Types = types;
        Aux = aux;
    }

    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Docs", "Telemetry", "telemetry.schema.v1.json")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root with Docs/Telemetry not found.");
    }

    public static SchemaKeys Load()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "Docs", "Telemetry", "telemetry.schema.v1.json")));
        var root = doc.RootElement;
        var envelope = root.GetProperty("envelope").EnumerateObject().Select(p => p.Name).Where(n => n != "fake").ToList();
        var types = root.GetProperty("types").EnumerateObject().ToDictionary(
            t => t.Name, t => (IReadOnlyList<string>)t.Value.GetProperty("fields").EnumerateObject().Select(p => p.Name).ToList());
        var aux = root.GetProperty("aux").EnumerateObject().ToDictionary(
            t => t.Name, t => (IReadOnlyList<string>)t.Value.GetProperty("fields").EnumerateObject().Select(p => p.Name).Where(n => n != "fake").ToList());
        return new SchemaKeys(envelope, types, aux);
    }

    /// <summary>Trả null khi khóa và thứ tự khớp schema, không thì mô tả chỗ lệch.</summary>
    public string? Mismatch(string line)
    {
        using var doc = JsonDocument.Parse(line);
        var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        var type = doc.RootElement.GetProperty("type").GetString() ?? "";
        IReadOnlyList<string> expected;
        if (Aux.TryGetValue(type, out var auxKeys)) expected = auxKeys;
        else if (Types.TryGetValue(type, out var fields)) expected = Envelope.Concat(fields).ToList();
        else return $"unknown type {type}";
        return keys.SequenceEqual(expected) ? null : $"{type}: got [{string.Join(",", keys)}] expected [{string.Join(",", expected)}]";
    }
}
