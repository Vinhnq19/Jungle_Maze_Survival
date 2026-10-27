using System;
using JungleMaze.Domain.Telemetry;

namespace JungleMaze.App.Telemetry
{
    /// <summary>
    /// Port for client telemetry (TEL-01, hook contract 2.7): <see cref="TryWrite"/> never blocks, never allocates,
    /// does no I/O and never throws. A full queue or a disabled sink returns false and is counted, never waited on.
    /// </summary>
    public interface ITelemetrySink
    {
        bool Enabled { get; }
        long Dropped { get; }
        bool TryWrite(in PerfClientSample sample);
    }

    /// <summary>Envelope values fixed for the whole process: build, writer id, client balance hash.</summary>
    public sealed class TelemetryContext
    {
        public TelemetryContext(string build, string wid, string balHash)
        {
            Build = string.IsNullOrEmpty(build) ? "dev" : build.Length > 32 ? build.Substring(0, 32) : build;
            Wid = wid ?? throw new ArgumentNullException(nameof(wid));
            BalHash = balHash;
        }

        public string Build { get; }
        /// <summary>8 lowercase hex, random per process run.</summary>
        public string Wid { get; }
        /// <summary>64 lowercase hex, or null when the balance files could not be read.</summary>
        public string BalHash { get; }
    }
}
