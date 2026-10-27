using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using JungleMaze.Telemetry.Server;

namespace TelemetryChecks;

/// <summary>Ring, JSON, writer, lỗi đĩa và host (AC24, AC25, AC26, AC27).</summary>
public static class CoreChecks
{
    public const string SampleCfgHash = "3fa1c2d0aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    public static void Run(Checker c, SchemaKeys schema)
    {
        Ring(c);
        Json(c);
        Writer(c, schema);
        DiskErrors(c);
        NonBlocking(c);
        Host(c);
    }

    private static void Ring(Checker c)
    {
        c.Section("Ring: bounded MPSC");
        var ring = new TelemetryRing(8, 4);
        var accepted = 0;
        for (var i = 0; i < 9; i++)
            if (ring.TryWrite(new TelemetryEvent { Kind = TelemetryKind.Damaged, I0 = i })) accepted++;
        c.Check(accepted == 8 && ring.DroppedCritical == 1, "Full ring rejects the 9th write and counts one critical drop");
        var inOrder = true;
        for (var i = 0; i < 8; i++)
            inOrder &= ring.TryRead(out var e) && e.I0 == i;
        c.Check(inOrder && !ring.TryRead(out _), "Reads return the 8 writes in order, then empty");

        var bulkRing = new TelemetryRing(16, 4);
        var bulkAccepted = 0;
        for (var i = 0; i < 6; i++)
            if (bulkRing.TryWrite(new TelemetryEvent { Kind = TelemetryKind.Snapshot }, bulk: true)) bulkAccepted++;
        var criticalAccepted = 0;
        for (var i = 0; i < 12; i++)
            if (bulkRing.TryWrite(new TelemetryEvent { Kind = TelemetryKind.Died })) criticalAccepted++;
        c.Check(bulkAccepted == 4 && bulkRing.DroppedBulk == 2 && criticalAccepted == 12,
            "Bulk stops at its limit while critical still fills the reserved slots (R5)",
            $"bulk={bulkAccepted} droppedBulk={bulkRing.DroppedBulk} critical={criticalAccepted}");

        // Ô đã đọc phải nhả tham chiếu byte[] để GC thu frame (TR2).
        var refRing = new TelemetryRing(4, 4);
        var weak = WriteAndReadFrame(refRing);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        c.Check(!weak.IsAlive, "Read slot releases the frame reference");

        // Nhiều luồng ghi + một luồng đọc: không mất, không trùng, giữ thứ tự theo từng luồng ghi.
        const int producers = 4, perProducer = 200_000;
        var mpsc = new TelemetryRing(1024, 1024);
        var last = new int[producers];
        Array.Fill(last, -1);
        var orderOk = true;
        long read = 0;
        var done = 0;
        var consumer = new Thread(() =>
        {
            while (Volatile.Read(ref done) < producers || mpsc.Count > 0)
            {
                while (mpsc.TryRead(out var e))
                {
                    if (e.I1 <= last[e.I0]) orderOk = false;
                    last[e.I0] = e.I1;
                    read++;
                }
                Thread.Yield();
            }
        });
        consumer.Start();
        var threads = Enumerable.Range(0, producers).Select(p => new Thread(() =>
        {
            for (var i = 0; i < perProducer; i++)
                mpsc.TryWrite(new TelemetryEvent { Kind = TelemetryKind.Damaged, I0 = p, I1 = i });
            Interlocked.Increment(ref done);
        })).ToArray();
        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();
        consumer.Join();
        c.Check(read + mpsc.Dropped == producers * perProducer && orderOk,
            "4 producers x 200k: read + dropped = written, per-producer FIFO",
            $"read={read} dropped={mpsc.Dropped} order={orderOk}");
    }

    private static WeakReference WriteAndReadFrame(TelemetryRing ring)
    {
        var frame = new byte[1024];
        ring.TryWrite(new TelemetryEvent { Kind = TelemetryKind.Frame, Ref = frame });
        ring.TryRead(out _);
        return new WeakReference(frame);
    }

    private static void Json(Checker c)
    {
        c.Section("JSON: invariant, escaped, finite");
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var line = new TelemetryJson().Begin()
                .Num("a", 1.5).Num("b", 1.23456).Num("c", double.NaN).Num("d", -0.0001).Num("e", 25)
                .Str("s", "q\"\\\n\u0001").IntOrNull("n", -1).NetIdOrNull("id", 255).UIntOrNull("u", null)
                .BeginArray("arr").ItemString("KNIFE").ItemObject("id", "AMMO", "qty", 3).EndArray()
                .End();
            const string expected = "{\"a\":1.5,\"b\":1.235,\"c\":null,\"d\":0,\"e\":25,\"s\":\"q\\\"\\\\\\n\\u0001\",\"n\":null,\"id\":null,\"u\":null,\"arr\":[\"KNIFE\",{\"id\":\"AMMO\",\"qty\":3}]}";
            c.Check(line == expected, "Numbers use invariant culture with at most 3 decimals; NaN -> null; strings escaped", line);
            using var parsed = JsonDocument.Parse(line);
            c.Check(parsed.RootElement.GetProperty("s").GetString() == "q\"\\\n\u0001", "Escaped string round-trips through a JSON parser");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    public static bool WriteAttack(JsonlTelemetryWriter writer, TelemetryFile file, uint tick)
    {
        if (!writer.TryBegin(file, "attack", tick, tick, 1_700_000_000_000 + tick, out var j)) return false;
        j.UInt("netId", 1).Str("weapon", "KNIFE").Str("kind", "MELEE").Num("x", 1.5).Num("y", 2).IntOrNull("ammo", -1);
        return writer.Commit(file);
    }

    public static TelemetryFile? OpenSample(JsonlTelemetryWriter writer, Guid matchId)
    {
        var file = writer.Open(matchId);
        if (file == null) return null;
        file.RoomId = "room-1";
        file.Cfg = "1.0.0";
        file.CfgHash = SampleCfgHash;
        return file;
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tel01-checks-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Writer(Checker c, SchemaKeys schema)
    {
        c.Section("Writer: one file per match, header/trailer, caps");
        var root = NewTempDir();
        try
        {
            var writer = new JsonlTelemetryWriter(new TelemetryWriterOptions { RootPath = root, Log = TextWriter.Null });
            var matchA = Guid.NewGuid();
            var matchB = Guid.NewGuid();
            var fileA = OpenSample(writer, matchA)!;
            for (uint t = 1; t <= 5; t++) WriteAttack(writer, fileA, t);
            writer.Close(fileA);
            var fileB = OpenSample(writer, matchB)!;
            WriteAttack(writer, fileB, 1);
            writer.Stop(TimeSpan.FromSeconds(1));

            var files = Directory.GetFiles(root, "*.jsonl", SearchOption.AllDirectories);
            c.Check(files.Length == 2 && files.All(f => Path.GetFileName(f).Contains($".server.{writer.Wid}.jsonl")),
                "Two matches produce two files named <matchId>.server.<wid>.jsonl");
            var bytes = File.ReadAllBytes(fileA.Path);
            c.Check(bytes.Length > 3 && !(bytes[0] == 0xEF && bytes[1] == 0xBB) && !bytes.Contains((byte)'\r') && bytes[^1] == (byte)'\n',
                "File is UTF-8 without BOM, LF only, ends with a newline");
            var lines = File.ReadAllLines(fileA.Path);
            using var header = JsonDocument.Parse(lines[0]);
            using var trailer = JsonDocument.Parse(lines[^1]);
            c.Check(header.RootElement.GetProperty("type").GetString() == "_header" && trailer.RootElement.GetProperty("type").GetString() == "_trailer",
                "First line is _header, last line is _trailer");
            c.Check(trailer.RootElement.GetProperty("written").GetInt64() == lines.Length - 2,
                "_trailer.written equals the number of record lines", $"written={trailer.RootElement.GetProperty("written")} lines={lines.Length - 2}");
            var seqs = lines.Skip(1).Take(lines.Length - 2).Select(l => JsonDocument.Parse(l).RootElement.GetProperty("seq").GetInt64()).ToArray();
            c.Check(seqs.SequenceEqual(new long[] { 1, 2, 3, 4, 5 }), "seq runs 1..N without gaps");
            var mismatches = lines.Select(schema.Mismatch).Where(m => m != null).ToList();
            c.Check(mismatches.Count == 0, "Header, records and trailer keys match the shared schema in order", string.Join(" | ", mismatches));
            c.Check(fileB.IsClosed && File.ReadAllLines(fileB.Path)[^1].Contains("\"_trailer\""), "Stop closes still-open files with a trailer");

            // Trần mỗi file: dừng ghi, truncated, seq không hở, droppedIo đếm phần bị bỏ.
            var capRoot = Path.Combine(root, "cap");
            var capWriter = new JsonlTelemetryWriter(new TelemetryWriterOptions { RootPath = capRoot, MaxFileBytes = 4000, Log = TextWriter.Null });
            var capFile = OpenSample(capWriter, Guid.NewGuid())!;
            for (uint t = 1; t <= 100; t++) WriteAttack(capWriter, capFile, t);
            capWriter.Stop(TimeSpan.FromSeconds(1));
            var capLines = File.ReadAllLines(capFile.Path);
            using var capTrailer = JsonDocument.Parse(capLines[^1]);
            var written = capTrailer.RootElement.GetProperty("written").GetInt64();
            c.Check(capTrailer.RootElement.GetProperty("truncated").GetBoolean() && written == capLines.Length - 2
                    && capTrailer.RootElement.GetProperty("droppedIo").GetInt64() == 100 - written && new FileInfo(capFile.Path).Length < 4000 + 400,
                "Per-file cap stops writing, marks truncated, counts dropped records", $"written={written}");

            // Trần tổng của tiến trình.
            var totalRoot = Path.Combine(root, "total");
            var totalWriter = new JsonlTelemetryWriter(new TelemetryWriterOptions { RootPath = totalRoot, MaxTotalBytes = 6000, Log = TextWriter.Null });
            var t1 = OpenSample(totalWriter, Guid.NewGuid())!;
            var t2 = OpenSample(totalWriter, Guid.NewGuid())!;
            for (uint t = 1; t <= 50; t++) { WriteAttack(totalWriter, t1, t); WriteAttack(totalWriter, t2, t); }
            totalWriter.Stop(TimeSpan.FromSeconds(1));
            c.Check(t1.Truncated && t2.Truncated && totalWriter.TotalBytes < 6000 + 1000, "Total cap truncates every open file",
                $"total={totalWriter.TotalBytes}");
        }
        finally
        {
            TryDelete(root);
        }
    }

    private sealed class FailingStream : Stream
    {
        private readonly long _failAfter;
        private long _written;
        public FailingStream(long failAfter) => _failAfter = failAfter;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _written;
        public override long Position { get => _written; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_written + count > _failAfter) throw new IOException("There is not enough space on the disk.");
            _written += count;
        }
    }

    private static int CountErrors(StringWriter log) =>
        log.ToString().Split('\n').Count(l => l.StartsWith("[telemetry-error]", StringComparison.Ordinal));

    private static void DiskErrors(Checker c)
    {
        c.Section("Disk errors: self-disable, one warning, no exception");
        var root = NewTempDir();
        try
        {
            // Thư mục gốc thực ra là một file.
            var blocker = Path.Combine(root, "blocker");
            File.WriteAllText(blocker, "x");
            var log1 = new StringWriter();
            var w1 = new JsonlTelemetryWriter(new TelemetryWriterOptions { RootPath = blocker, Log = log1 });
            var f1 = w1.Open(Guid.NewGuid());
            var f1b = w1.Open(Guid.NewGuid());
            w1.Stop(TimeSpan.FromSeconds(1));
            c.Check(f1 == null && f1b == null && !w1.Enabled && CountErrors(log1) == 1, "Root path that is a file disables telemetry with one warning");

            // Chỉ đọc: mở file bị từ chối quyền.
            var log2 = new StringWriter();
            var w2 = new JsonlTelemetryWriter(new TelemetryWriterOptions
            {
                RootPath = root, Log = log2, OpenStream = _ => throw new UnauthorizedAccessException("Access to the path is denied.")
            });
            var f2 = w2.Open(Guid.NewGuid());
            w2.Stop(TimeSpan.FromSeconds(1));
            c.Check(f2 == null && !w2.Enabled && CountErrors(log2) == 1, "Read-only target disables telemetry with one warning");

            // Đĩa đầy giữa chừng.
            var log3 = new StringWriter();
            var w3 = new JsonlTelemetryWriter(new TelemetryWriterOptions { RootPath = root, Log = log3, OpenStream = _ => new FailingStream(1500) });
            var f3 = OpenSample(w3, Guid.NewGuid())!;
            var threw = false;
            try
            {
                for (uint t = 1; t <= 50; t++) WriteAttack(w3, f3, t);
                w3.PumpOnce();
            }
            catch
            {
                threw = true;
            }
            w3.Stop(TimeSpan.FromSeconds(1));
            c.Check(!threw && !w3.Enabled && CountErrors(log3) == 1 && f3.Written < 50 && f3.IsClosed,
                "Disk full mid-match disables telemetry, one warning, no exception", $"written={f3.Written}");
        }
        finally
        {
            TryDelete(root);
        }
    }

    private sealed class BlockingStream : Stream
    {
        private readonly ManualResetEventSlim _gate;
        public BlockingStream(ManualResetEventSlim gate) => _gate = gate;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => _gate.Wait();
    }

    /// <summary>Pump thử: rút ring và ghi mỗi tín hiệu thành một bản ghi attack.</summary>
    private sealed class RingToFilePump : ITelemetryPump
    {
        private readonly TelemetryRing _ring;
        private TelemetryFile? _file;
        public RingToFilePump(TelemetryRing ring) => _ring = ring;
        public void Pump(JsonlTelemetryWriter writer)
        {
            _file ??= OpenSample(writer, Guid.NewGuid());
            while (_ring.TryRead(out var e))
                if (_file != null) WriteAttack(writer, _file, e.Tick);
        }
        public void Complete(JsonlTelemetryWriter writer) { if (_file != null) writer.Close(_file); }
    }

    private static void NonBlocking(Checker c)
    {
        c.Section("TryWrite never waits for the disk (AC24)");
        using var gate = new ManualResetEventSlim(false);
        var ring = new TelemetryRing(4096, 2048);
        var writer = new JsonlTelemetryWriter(new TelemetryWriterOptions
        {
            RootPath = Path.GetTempPath(), Log = TextWriter.Null, IntervalMs = 5, OpenStream = _ => new BlockingStream(gate)
        });
        writer.Register(new RingToFilePump(ring));
        writer.Start();
        Thread.Sleep(30); // luồng nền đã vào Write và đang bị chặn
        var accepted = 0;
        var watch = Stopwatch.StartNew();
        for (uint i = 0; i < 10_000; i++)
            if (ring.TryWrite(new TelemetryEvent { Kind = TelemetryKind.Attack, Tick = i })) accepted++;
        watch.Stop();
        c.Check(watch.Elapsed.TotalMilliseconds < 50 && accepted + ring.Dropped == 10_000 && ring.Dropped > 0,
            "10,000 TryWrite with a blocked writer finish fast; overflow is dropped and counted",
            FormattableString.Invariant($"elapsedMs={watch.Elapsed.TotalMilliseconds:F2} accepted={accepted} dropped={ring.Dropped}"));
        gate.Set();
        writer.Stop(TimeSpan.FromSeconds(3));
    }

    private static void Host(Checker c)
    {
        c.Section("Host: disabled by default (AC27)");
        var none = TelemetryHost.FromValues(_ => null, TextWriter.Null);
        c.Check(!none.Enabled && none.Writer == null, "No TELEMETRY_PATH: host disabled, no writer thread");
        none.Dispose();

        var root = Path.Combine(Path.GetTempPath(), "tel01-host-" + Guid.NewGuid().ToString("N")[..8]);
        var log = new StringWriter();
        var publicHost = TelemetryHost.FromValues(k => k switch { "TELEMETRY_PATH" => root, "PUBLIC_HOSTING" => "TRUE", _ => null }, log);
        c.Check(!publicHost.Enabled && publicHost.Writer == null && !Directory.Exists(root) && log.ToString().Contains("PUBLIC_HOSTING"),
            "PUBLIC_HOSTING=true refuses telemetry and creates nothing");

        var host = TelemetryHost.FromValues(k => k switch
        {
            "TELEMETRY_PATH" => root, "TELEMETRY_SAMPLE_HZ" => "10", "TELEMETRY_MAX_FILE_MIB" => "abc", _ => null
        }, TextWriter.Null);
        c.Check(host.Enabled && host.Options.SampleHz == 2 && host.Options.MaxFileMiB == 8 && !Directory.Exists(root),
            "Enabled host clamps sample rate, ignores bad numbers, creates no folder until a match writes");
        host.Dispose();
        host.Dispose();
        c.Check(!host.Enabled || host.Writer != null, "Dispose is idempotent");
        TryDelete(root);
    }

    public static void TryDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
