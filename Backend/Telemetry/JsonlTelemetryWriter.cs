#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace JungleMaze.Telemetry.Server;

/// <summary>Nguồn dữ liệu được luồng nền gọi định kỳ: rút ring, dựng bản ghi qua writer.</summary>
public interface ITelemetryPump
{
    /// <summary>Gọi mỗi chu kỳ trên luồng nền.</summary>
    void Pump(JsonlTelemetryWriter writer);

    /// <summary>Gọi một lần khi writer dừng, sau lần Pump cuối: đóng file đang mở.</summary>
    void Complete(JsonlTelemetryWriter writer);
}

public sealed class TelemetryWriterOptions
{
    public string RootPath { get; init; } = "";
    public long MaxFileBytes { get; init; } = 8L * 1024 * 1024;
    public long MaxTotalBytes { get; init; } = 256L * 1024 * 1024;
    public string Build { get; init; } = "dev";
    public int IntervalMs { get; init; } = 50;
    /// <summary>Seam cho test lỗi đĩa; mặc định mở FileStream ghi nối.</summary>
    public Func<string, Stream>? OpenStream { get; init; }
    public TextWriter? Log { get; init; }
}

/// <summary>Một file JSONL cho (trận, nguồn server, wid). Chỉ luồng nền chạm vào.</summary>
public sealed class TelemetryFile
{
    internal TelemetryFile(Guid matchId, string path) { MatchId = matchId; Path = path; }

    public Guid MatchId { get; }
    public string Path { get; }
    public string? RoomId { get; set; }
    public string? Cfg { get; set; }
    public string? CfgHash { get; set; }
    public long Written { get; internal set; }
    public long DroppedQueue { get; set; }
    public long DroppedIo { get; internal set; }
    public bool Truncated { get; internal set; }
    public bool IsClosed { get; internal set; }
    internal Stream? Stream;
    internal long Bytes;
    internal long Seq;
}

/// <summary>
/// Một Thread nền gom các pump mỗi IntervalMs, ghi `${root}/<yyyy-MM-dd>/<matchId>.server.<wid>.jsonl`
/// có _header/_trailer, trần dung lượng theo file và tổng. Lỗi I/O: tự tắt, in một dòng [telemetry-error].
/// Không có API nào ở đây được gọi trên luồng tick.
/// </summary>
public sealed class JsonlTelemetryWriter : IDisposable
{
    private static readonly UTF8Encoding Utf8 = new(false);
    private static readonly byte[] NewLine = { (byte)'\n' };

    private readonly TelemetryWriterOptions _options;
    private readonly TextWriter _log;
    private readonly TelemetryJson _json = new();
    private readonly List<TelemetryFile> _open = new();
    private readonly object _pumpGate = new();
    private ITelemetryPump[] _pumps = Array.Empty<ITelemetryPump>();
    private readonly ManualResetEventSlim _stop = new(false);
    private Thread? _thread;
    private volatile bool _enabled = true;
    private int _errorReported;
    private int _stopped;
    private long _totalBytes;
    private TelemetryFile? _building;
    private long _buildingSeq;

    public string Wid { get; }
    public string Build { get; }
    public bool Enabled => _enabled;
    public long TotalBytes => Interlocked.Read(ref _totalBytes);

    public JsonlTelemetryWriter(TelemetryWriterOptions options)
    {
        _options = options;
        _log = options.Log ?? Console.Out;
        Wid = NewWid();
        Build = SanitizeBuild(options.Build);
    }

    public static string NewWid()
    {
        Span<byte> bytes = stackalloc byte[4];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string SanitizeBuild(string? build)
    {
        if (string.IsNullOrWhiteSpace(build)) return "dev";
        var trimmed = build.Trim();
        if (trimmed.Length > 32) trimmed = trimmed[..32];
        foreach (var c in trimmed)
            if (c < 0x20 || c == '"' || c == '\\') return "dev";
        return trimmed;
    }

    public void Register(ITelemetryPump pump)
    {
        lock (_pumpGate)
        {
            var next = new ITelemetryPump[_pumps.Length + 1];
            Array.Copy(_pumps, next, _pumps.Length);
            next[^1] = pump;
            _pumps = next;
        }
    }

    public void Unregister(ITelemetryPump pump)
    {
        lock (_pumpGate)
        {
            _pumps = Array.FindAll(_pumps, p => !ReferenceEquals(p, pump));
        }
    }

    public void Start()
    {
        if (_thread != null) return;
        _thread = new Thread(Run) { IsBackground = true, Name = "telemetry-writer" };
        _thread.Start();
    }

    private void Run()
    {
        while (!_stop.IsSet)
        {
            PumpOnce();
            _stop.Wait(_options.IntervalMs);
        }
    }

    /// <summary>Một chu kỳ gom; public để test chạy đồng bộ khi chưa Start.</summary>
    public void PumpOnce()
    {
        var pumps = Volatile.Read(ref _pumps);
        foreach (var pump in pumps)
        {
            try { pump.Pump(this); }
            catch (Exception error) { ReportError("pump", error); }
        }
        FlushOpen();
    }

    /// <summary>Dừng luồng nền (chờ tối đa timeout), gom lần cuối, đóng mọi file với _trailer.</summary>
    public void Stop(TimeSpan timeout)
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0) return;
        _stop.Set();
        var joined = _thread == null || _thread.Join(timeout);
        if (!joined)
        {
            _log.WriteLine("[telemetry-error] writer did not stop in time; trailers skipped.");
            return;
        }
        PumpOnce();
        foreach (var pump in Volatile.Read(ref _pumps))
        {
            try { pump.Complete(this); }
            catch (Exception error) { ReportError("complete", error); }
        }
        foreach (var file in _open.ToArray()) Close(file);
    }

    public void Dispose()
    {
        Stop(TimeSpan.FromSeconds(3));
        _stop.Dispose();
    }

    /// <summary>Mở file của một trận và ghi _header. Trả null khi writer đã tắt.</summary>
    public TelemetryFile? Open(Guid matchId)
    {
        if (!_enabled) return null;
        var now = DateTime.UtcNow;
        var directory = System.IO.Path.Combine(_options.RootPath, now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        var path = System.IO.Path.Combine(directory, $"{matchId:D}.server.{Wid}.jsonl");
        var file = new TelemetryFile(matchId, path);
        try
        {
            Directory.CreateDirectory(directory);
            file.Stream = _options.OpenStream?.Invoke(path)
                ?? new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 64 * 1024);
        }
        catch (Exception error) when (IsIoError(error))
        {
            ReportError("open", error);
            return null;
        }

        _json.Begin()
            .Int("v", 1).Str("type", "_header").Str("schema", "telemetry.v1").Str("src", "server")
            .Str("wid", Wid).Guid("matchId", matchId).Str("build", Build).Int("opened", ToUnixMs(now));
        if (!WriteLine(file, _json.End(), enforceCap: false))
        {
            return null;
        }
        _open.Add(file);
        return file;
    }

    /// <summary>
    /// Bắt đầu một bản ghi: ghi envelope rồi trả builder để thêm trường riêng, sau đó gọi <see cref="Commit"/>.
    /// Trả false (đếm droppedIo) khi writer tắt, file đóng hoặc đã chạm trần.
    /// </summary>
    public bool TryBegin(TelemetryFile file, string type, uint tick, uint? matchTick, long utcMs, out TelemetryJson json)
    {
        json = _json;
        if (!_enabled || file.IsClosed || file.Truncated)
        {
            file.DroppedIo++;
            return false;
        }
        _building = file;
        _buildingSeq = file.Seq + 1;
        _json.Begin()
            .Int("v", 1).Str("type", type).Guid("matchId", file.MatchId).Str("roomId", file.RoomId).Str("src", "server")
            .Str("wid", Wid).Int("seq", _buildingSeq).UInt("tick", tick).UIntOrNull("mt", matchTick)
            .Int("utc", utcMs).Str("build", Build).Str("cfg", file.Cfg ?? "").Str("cfgHash", file.CfgHash ?? "")
            .Null("balHash");
        return true;
    }

    /// <summary>Ghi bản ghi đã dựng. Chạm trần thì không ghi, đặt truncated, không tiêu seq.</summary>
    public bool Commit(TelemetryFile file)
    {
        if (!ReferenceEquals(_building, file)) throw new InvalidOperationException("Commit without matching TryBegin.");
        _building = null;
        var line = _json.End();
        if (!WriteLine(file, line, enforceCap: true)) return false;
        file.Seq = _buildingSeq;
        file.Written++;
        return true;
    }

    /// <summary>Ghi _trailer và đóng file. Gọi lại là no-op.</summary>
    public void Close(TelemetryFile file)
    {
        if (file.IsClosed) return;
        if (_enabled && file.Stream != null)
        {
            _json.Begin()
                .Int("v", 1).Str("type", "_trailer").Str("src", "server").Str("wid", Wid).Guid("matchId", file.MatchId)
                .Int("written", file.Written).Int("droppedQueue", file.DroppedQueue).Int("droppedIo", file.DroppedIo)
                .Bool("truncated", file.Truncated).Int("closed", ToUnixMs(DateTime.UtcNow));
            WriteLine(file, _json.End(), enforceCap: false);
        }
        file.IsClosed = true;
        try { file.Stream?.Dispose(); }
        catch (Exception error) when (IsIoError(error)) { ReportError("close", error); }
        file.Stream = null;
        _open.Remove(file);
    }

    private bool WriteLine(TelemetryFile file, string line, bool enforceCap)
    {
        if (!_enabled || file.Stream == null)
        {
            file.DroppedIo++;
            return false;
        }
        var bytes = Utf8.GetBytes(line);
        var size = bytes.Length + 1L;
        if (enforceCap && (file.Bytes + size > _options.MaxFileBytes || TotalBytes + size > _options.MaxTotalBytes))
        {
            file.Truncated = true;
            file.DroppedIo++;
            return false;
        }
        try
        {
            file.Stream.Write(bytes, 0, bytes.Length);
            file.Stream.Write(NewLine, 0, 1);
        }
        catch (Exception error) when (IsIoError(error))
        {
            file.DroppedIo++;
            ReportError("write", error);
            return false;
        }
        file.Bytes += size;
        Interlocked.Add(ref _totalBytes, size);
        return true;
    }

    private void FlushOpen()
    {
        foreach (var file in _open)
        {
            try { file.Stream?.Flush(); }
            catch (Exception error) when (IsIoError(error)) { ReportError("flush", error); return; }
        }
    }

    private static bool IsIoError(Exception error) =>
        error is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException
            or ArgumentException or ObjectDisposedException;

    /// <summary>Lỗi đầu tiên tắt writer và in đúng một dòng; các lỗi sau chỉ bị bỏ qua.</summary>
    private void ReportError(string stage, Exception error)
    {
        _enabled = false;
        if (Interlocked.Exchange(ref _errorReported, 1) != 0) return;
        _log.WriteLine($"[telemetry-error] {stage} failed ({error.GetType().Name}): {error.Message} Telemetry disabled; gameplay unaffected.");
        foreach (var file in _open.ToArray())
        {
            file.IsClosed = true;
            try { file.Stream?.Dispose(); } catch { /* best effort */ }
            file.Stream = null;
        }
        _open.Clear();
    }

    public static long ToUnixMs(DateTime utc) => (utc.Ticks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerMillisecond;
}
