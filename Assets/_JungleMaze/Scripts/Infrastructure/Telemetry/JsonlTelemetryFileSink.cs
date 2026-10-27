using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using JungleMaze.App.Telemetry;
using JungleMaze.Core.Logging;
using JungleMaze.Domain.Telemetry;

namespace JungleMaze.Infrastructure.Telemetry
{
    /// <summary>
    /// TEL-01 client sink: a single-producer ring (main thread writes, never blocks) drained by one background thread
    /// that owns JSON formatting and file I/O. One file per (match, wid): <c>&lt;dir&gt;/&lt;matchId&gt;.client.&lt;netId&gt;.&lt;wid&gt;.jsonl</c>,
    /// opened in append mode, UTF-8 without BOM, LF, <c>_header</c> first and <c>_trailer</c> last (Schema.vi.md 4).
    /// <c>seq</c> counts per match from 1 on the writer thread, so dropped records never leave a gap. Past the 8 MiB cap
    /// the file stops taking records and its trailer says <c>truncated</c>; an I/O error disables the sink with one warning.
    /// </summary>
    public sealed class JsonlTelemetryFileSink : ITelemetrySink, IDisposable
    {
        public const int DefaultCapacity = 256;
        public const long DefaultMaxFileBytes = 8L * 1024 * 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        private readonly string _directory;
        private readonly TelemetryContext _context;
        private readonly long _maxFileBytes;
        private readonly Func<string, Stream> _open;
        private readonly Func<long> _utcMs;
        private readonly ILog _log;
        private readonly PerfClientSample[] _ring;
        private readonly Thread _thread;
        private readonly ManualResetEvent _stop = new ManualResetEvent(false);
        private readonly StringBuilder _line = new StringBuilder(512);
        private readonly Dictionary<Guid, long> _seq = new Dictionary<Guid, long>();
        private long _head, _tail, _dropped;
        private volatile bool _enabled = true;
        private bool _disposed;

        // Writer-thread state of the open file.
        private Stream _file;
        private Guid _fileMatch;
        private long _fileBytes, _written, _droppedAtOpen, _droppedIo;
        private bool _truncated;

        public JsonlTelemetryFileSink(string directory, TelemetryContext context, ILog log = null, int capacity = DefaultCapacity,
            long maxFileBytes = DefaultMaxFileBytes, Func<string, Stream> open = null, Func<long> utcMs = null, int pollMs = 50)
        {
            _directory = directory ?? throw new ArgumentNullException(nameof(directory));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _ring = new PerfClientSample[capacity];
            _maxFileBytes = maxFileBytes;
            _open = open ?? (path => new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read));
            _utcMs = utcMs ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            _log = log ?? NullLog.Instance;
            PollMs = Math.Max(1, pollMs);
            _thread = new Thread(Run) { IsBackground = true, Name = "TelemetrySink" };
            _thread.Start();
        }

        public bool Enabled => _enabled;
        public long Dropped => Interlocked.Read(ref _dropped);
        public long DroppedIo => Interlocked.Read(ref _droppedIo);
        public int PollMs { get; }
        /// <summary>Path of the file being written (or the last one), for tests and the QA guide.</summary>
        public string LastPath { get; private set; }

        /// <summary>Main thread only. Copies the struct into the ring; false (and counted) when full or disabled.</summary>
        public bool TryWrite(in PerfClientSample sample)
        {
            if (!_enabled) { Interlocked.Increment(ref _dropped); return false; }
            var tail = Volatile.Read(ref _tail);
            if (tail - Volatile.Read(ref _head) >= _ring.Length) { Interlocked.Increment(ref _dropped); return false; }
            _ring[tail % _ring.Length] = sample;
            Volatile.Write(ref _tail, tail + 1);
            return true;
        }

        private void Run()
        {
            while (!_stop.WaitOne(PollMs)) Drain();
            Drain();
            CloseFile();
        }

        private void Drain()
        {
            var head = Volatile.Read(ref _head);
            var tail = Volatile.Read(ref _tail);
            while (head < tail)
            {
                var sample = _ring[head % _ring.Length];
                Volatile.Write(ref _head, ++head);
                if (_enabled) Write(in sample);
                else Interlocked.Increment(ref _droppedIo);
            }
            if (_file != null && _enabled) Guard(() => _file.Flush());
        }

        private void Write(in PerfClientSample s)
        {
            if (_file == null || s.MatchId != _fileMatch) OpenFile(s.MatchId, s.NetId);
            if (_file == null) return;
            if (_truncated) return;
            _seq.TryGetValue(s.MatchId, out var seq);
            _line.Clear();
            Envelope(s, seq + 1);
            Fields(s);
            _line.Append("}\n");
            var bytes = Utf8.GetBytes(_line.ToString());
            if (_fileBytes + bytes.Length > _maxFileBytes) { _truncated = true; return; }
            if (!Guard(() => _file.Write(bytes, 0, bytes.Length))) return;
            _seq[s.MatchId] = seq + 1;
            _fileBytes += bytes.Length;
            _written++;
        }

        private void OpenFile(Guid matchId, byte netId)
        {
            CloseFile();
            var path = Path.Combine(_directory, matchId.ToString("D") + ".client." + netId.ToString(CultureInfo.InvariantCulture) + "." + _context.Wid + ".jsonl");
            if (!Guard(() =>
                {
                    Directory.CreateDirectory(_directory);
                    _file = _open(path);
                })) return;
            LastPath = path;
            _fileMatch = matchId;
            _fileBytes = _written = 0;
            _truncated = false;
            _droppedAtOpen = Dropped;
            _line.Clear();
            _line.Append("{\"v\":1,\"type\":\"_header\",\"schema\":\"telemetry.v1\",\"src\":\"client\",\"wid\":");
            Str(_context.Wid).Append(",\"matchId\":");
            Str(matchId.ToString("D")).Append(",\"build\":");
            Str(_context.Build).Append(",\"opened\":").Append(_utcMs().ToString(CultureInfo.InvariantCulture)).Append("}\n");
            var bytes = Utf8.GetBytes(_line.ToString());
            if (Guard(() => _file.Write(bytes, 0, bytes.Length))) _fileBytes += bytes.Length;
        }

        private void CloseFile()
        {
            if (_file == null) return;
            if (_enabled)
            {
                _line.Clear();
                _line.Append("{\"v\":1,\"type\":\"_trailer\",\"src\":\"client\",\"wid\":");
                Str(_context.Wid).Append(",\"matchId\":");
                Str(_fileMatch.ToString("D")).Append(",\"written\":").Append(_written.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"droppedQueue\":").Append((Dropped - _droppedAtOpen).ToString(CultureInfo.InvariantCulture))
                    .Append(",\"droppedIo\":").Append(DroppedIo.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"truncated\":").Append(_truncated ? "true" : "false")
                    .Append(",\"closed\":").Append(_utcMs().ToString(CultureInfo.InvariantCulture)).Append("}\n");
                var bytes = Utf8.GetBytes(_line.ToString());
                Guard(() => { _file.Write(bytes, 0, bytes.Length); _file.Flush(); });
            }
            try { _file.Dispose(); } catch (Exception) { /* already failed and reported */ }
            _file = null;
        }

        // Key order = schema envelope order, then perf_client fields in schema order.
        private void Envelope(in PerfClientSample s, long seq)
        {
            _line.Append("{\"v\":1,\"type\":\"perf_client\",\"matchId\":");
            Str(s.MatchId.ToString("D")).Append(",\"roomId\":null,\"src\":\"client\",\"wid\":");
            Str(_context.Wid).Append(",\"seq\":").Append(seq.ToString(CultureInfo.InvariantCulture))
                .Append(",\"tick\":").Append(s.Tick.ToString(CultureInfo.InvariantCulture)).Append(",\"mt\":");
            if (s.MatchTick >= 0) _line.Append(s.MatchTick.ToString(CultureInfo.InvariantCulture)); else _line.Append("null");
            _line.Append(",\"utc\":").Append(s.UtcMs.ToString(CultureInfo.InvariantCulture)).Append(",\"build\":");
            Str(_context.Build).Append(",\"cfg\":");
            Str(s.Cfg).Append(",\"cfgHash\":");
            Str(s.CfgHash).Append(",\"balHash\":");
            Str(_context.BalHash);
        }

        private void Fields(in PerfClientSample s)
        {
            _line.Append(",\"netId\":").Append(s.NetId.ToString(CultureInfo.InvariantCulture));
            Num(",\"windowSec\":", s.WindowSec);
            _line.Append(",\"frames\":").Append(s.Frames.ToString(CultureInfo.InvariantCulture));
            Num(",\"fpsAvg\":", s.FpsAvg);
            Num(",\"fps1Low\":", s.Fps1Low);
            Num(",\"frameMsP95\":", s.FrameMsP95);
            Num(",\"frameMsMax\":", s.FrameMsMax);
            _line.Append(",\"transport\":").Append(s.Transport == TelemetryTransport.Udp ? "\"UDP\"" : s.Transport == TelemetryTransport.Wss ? "\"WSS\"" : "null");
            Num(",\"rttP50\":", s.RttP50);
            Num(",\"rttP95\":", s.RttP95);
            Num(",\"jitterMs\":", s.JitterMs);
            Num(",\"lossPct\":", s.LossPct);
            _line.Append(",\"entities\":").Append(s.Entities.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>At most 3 decimals, invariant culture; NaN/Infinity become null.</summary>
        private void Num(string key, double value)
        {
            _line.Append(key);
            if (double.IsNaN(value) || double.IsInfinity(value)) _line.Append("null");
            else _line.Append(Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture));
        }

        private StringBuilder Str(string value)
        {
            if (value == null) return _line.Append("null");
            _line.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': _line.Append("\\\""); break;
                    case '\\': _line.Append("\\\\"); break;
                    default:
                        if (c < 0x20) _line.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else _line.Append(c);
                        break;
                }
            }
            return _line.Append('"');
        }

        /// <summary>Runs an I/O step; on failure disables the sink with one warning instead of throwing.</summary>
        private bool Guard(Action io)
        {
            try
            {
                io();
                return true;
            }
            catch (Exception e)
            {
                if (_enabled)
                {
                    _enabled = false;
                    _log.Warn("[Telemetry] disabled after an I/O error: " + e.GetType().Name + ": " + e.Message);
                }
                Interlocked.Increment(ref _droppedIo);
                return false;
            }
        }

        /// <summary>Stops the writer thread after draining (at most 3 s) and closes the file with its trailer.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _stop.Set();
            if (!_thread.Join(3000)) _log.Warn("[Telemetry] writer thread did not stop within 3 s.");
            _stop.Dispose();
        }
    }
}
