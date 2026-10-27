using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using JungleMaze.App.Telemetry;
using JungleMaze.Core.Logging;

namespace JungleMaze.Infrastructure.Telemetry
{
    /// <summary>
    /// Process-wide telemetry envelope values, computed once at composition: a random 8-hex <c>wid</c>, the build
    /// (<c>Application.version</c> or "dev") and <c>balHash</c> over the client balance files.
    /// </summary>
    public static class ClientTelemetryIdentity
    {
        /// <summary>Order matters: the hash covers these three files in this order (Schema.vi.md 5).</summary>
        public static readonly string[] BalanceFiles = { "weapons.json", "equipments.json", "match.json" };

        public static TelemetryContext Create(string streamingAssetsPath, string build, ILog log = null) =>
            new TelemetryContext(build, NewWid(), BalanceHash(Path.Combine(streamingAssetsPath, "JungleMaze", "balance"), log));

        public static string NewWid() => Guid.NewGuid().ToString("N").Substring(0, 8);

        /// <summary>
        /// Same recipe as the server's GameDataLoader.DataHash: for each file, SHA-256 over the UTF-8 leaf name, then
        /// the UTF-8 content with CRLF turned into LF; lowercase hex. Null (with one warning) if a file cannot be read.
        /// </summary>
        public static string BalanceHash(string balanceDirectory, ILog log = null)
        {
            try
            {
                using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                {
                    foreach (var file in BalanceFiles)
                    {
                        hash.AppendData(Encoding.UTF8.GetBytes(file));
                        hash.AppendData(Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(balanceDirectory, file)).Replace("\r\n", "\n")));
                    }
                    var bytes = hash.GetHashAndReset();
                    var hex = new StringBuilder(bytes.Length * 2);
                    foreach (var b in bytes) hex.Append(b.ToString("x2"));
                    return hex.ToString();
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                (log ?? NullLog.Instance).Warn("[Telemetry] balHash unavailable: " + e.Message);
                return null;
            }
        }
    }
}
