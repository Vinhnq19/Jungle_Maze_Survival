namespace NetSmokeClient.Services;

/// <summary>Không lưu mã phòng/password/token trong JSON hoặc command line/history.</summary>
public static class CredentialInput
{
    public static string ReadSecret(string variable, string prompt, CancellationToken cancellation)
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(variable);
        if (fromEnvironment is not null) return fromEnvironment;
        Console.Write(prompt);
        if (Console.IsInputRedirected)
            return Console.In.ReadLineAsync(cancellation).AsTask().GetAwaiter().GetResult()
                ?? throw new ArgumentException("Thiếu dữ liệu nhập cho client.");
        var value = new System.Text.StringBuilder();
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!Console.KeyAvailable) { Thread.Sleep(50); continue; }
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return value.ToString(); }
            if (key.Key == ConsoleKey.Backspace) { if (value.Length > 0) value.Length--; continue; }
            if (!char.IsControl(key.KeyChar) && value.Length < 2048) value.Append(key.KeyChar);
        }
    }
}
