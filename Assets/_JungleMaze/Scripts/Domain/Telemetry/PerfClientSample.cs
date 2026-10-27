using System;

namespace JungleMaze.Domain.Telemetry
{
    /// <summary>Transport of the window; <see cref="None"/> is written as null.</summary>
    public enum TelemetryTransport : byte
    {
        None = 0,
        Udp = 1,
        Wss = 2
    }

    /// <summary>
    /// One perf_client record before serialization (schema type 18). A plain struct: strings only reference values
    /// that already exist (the phase packet's config version and hash), so filling it allocates nothing. Optional
    /// numbers use NaN for null. Envelope fields that are the same for the whole process (src, wid, build, balHash)
    /// and the per-file <c>seq</c> are added by the sink on its own thread.
    /// </summary>
    public struct PerfClientSample
    {
        public Guid MatchId;
        public uint Tick;
        /// <summary>Ticks since InGame started; negative when unknown (written as null).</summary>
        public long MatchTick;
        public long UtcMs;
        public string Cfg;
        public string CfgHash;
        public byte NetId;
        public double WindowSec;
        public int Frames;
        public double FpsAvg;
        public double Fps1Low;
        public double FrameMsP95;
        public double FrameMsMax;
        public TelemetryTransport Transport;
        public double RttP50;
        public double RttP95;
        public double JitterMs;
        public double LossPct;
        public int Entities;
    }
}
