using System;
using System.IO;

namespace JungleMaze.Infrastructure.Telemetry
{
    /// <summary>
    /// Client telemetry switches from the command line (TEL-01 G1 T-3; ClientConfig stays untouched). Off unless
    /// <c>-telemetry</c> is present; <c>-telemetryDir &lt;path&gt;</c> overrides the default
    /// <c>&lt;persistentDataPath&gt;/Telemetry</c>.
    /// </summary>
    public readonly struct ClientTelemetryOptions
    {
        public const string Flag = "-telemetry";
        public const string DirFlag = "-telemetryDir";

        public ClientTelemetryOptions(bool enabled, string directory)
        {
            Enabled = enabled;
            Directory = directory;
        }

        public bool Enabled { get; }
        public string Directory { get; }

        public static ClientTelemetryOptions FromArgs(string[] args, string persistentDataPath)
        {
            var enabled = false;
            string directory = null;
            for (var i = 0; args != null && i < args.Length; i++)
            {
                if (string.Equals(args[i], Flag, StringComparison.OrdinalIgnoreCase)) enabled = true;
                else if (string.Equals(args[i], DirFlag, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) directory = args[++i];
            }
            return new ClientTelemetryOptions(enabled, string.IsNullOrWhiteSpace(directory) ? Path.Combine(persistentDataPath ?? ".", "Telemetry") : directory);
        }
    }
}
