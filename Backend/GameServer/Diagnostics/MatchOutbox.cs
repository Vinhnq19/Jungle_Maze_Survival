#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using GameServer.Simulation;
using JungleBackend.Shared;

namespace GameServer.Diagnostics;

public sealed class MatchOutboxOptions
{
    public string ApiUrl { get; init; } = "http://127.0.0.1:5000/internal/match-results";
    public string Token { get; init; } = "";
    public string OutboxPath { get; init; } = "./data/match-outbox";
    public int MaxFiles { get; init; } = 1000;
    public long MaxBytes { get; init; } = 10 * 1024 * 1024; // 10 MiB
    public int QueueCapacity { get; init; } = 128;
    public int InitialBackoffSeconds { get; init; } = 2;
    public int MaxBackoffSeconds { get; init; } = 30;
}

/// <summary>
/// Bounded outbox worker outside simulation tick loop.
/// Drains completed match results independently to durable disk files with quota bounds (including quarantine),
/// and delivers to persistence API with exponential backoff (2->30s), quarantine on conflict/malformed payloads,
/// and periodic retry without starving other files.
/// </summary>
public sealed class MatchOutbox : IDisposable
{
    private readonly MatchOutboxOptions _options;
    private readonly ServerMetrics? _metrics;
    private readonly HttpClient _httpClient;
    private readonly Channel<MatchResult> _queue;
    private readonly Channel<bool> _deliverySignal;
    private readonly CancellationTokenSource _drainCts = new();
    private readonly CancellationTokenSource _deliveryCts = new();
    private Task? _drainTask;
    private Task? _deliveryTask;
    private Task? _workerTask;
    private readonly string _quarantinePath;
    private readonly bool _ownsHttpClient;
    private int _started;

    public bool IsEnabled { get; }

    public MatchOutbox(MatchOutboxOptions options, ServerMetrics? metrics = null, HttpClient? httpClient = null)
    {
        _options = options;
        _metrics = metrics;
        _ownsHttpClient = httpClient == null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _quarantinePath = Path.Combine(_options.OutboxPath, "quarantine");

        // Token must be present and >= 32 UTF-8 bytes
        var tokenBytes = Encoding.UTF8.GetByteCount(_options.Token ?? "");
        var tokenValid = !string.IsNullOrEmpty(_options.Token) && tokenBytes >= 32;

        // URL must be valid HTTP/HTTPS without user credentials or query parameters
        var urlValid = IsValidApiUrl(_options.ApiUrl);

        // Idle / no configured secret or invalid target leaves game functional and history explicitly disabled
        IsEnabled = tokenValid && urlValid;

        // BoundedChannelFullMode.Wait ensures TryWrite returns false on overflow without blocking
        _queue = Channel.CreateBounded<MatchResult>(new BoundedChannelOptions(_options.QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        _deliverySignal = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        if (!tokenValid)
        {
            Console.WriteLine("[outbox] MATCH_RESULT_TOKEN missing or < 32 bytes. Match persistence outbox disabled.");
        }

        if (!urlValid)
        {
            Console.WriteLine("[outbox] MATCH_RESULTS_API_URL invalid (must be HTTP/HTTPS without credentials or query). Match persistence outbox disabled.");
        }
    }

    private static bool IsValidApiUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;
        if (!string.IsNullOrEmpty(uri.UserInfo)) return false;
        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return false;
        return true;
    }

    public void Start()
    {
        if (!IsEnabled) return;
        if (Interlocked.Exchange(ref _started, 1) != 0) return;

        try
        {
            Directory.CreateDirectory(_options.OutboxPath);
            Directory.CreateDirectory(_quarantinePath);

            // Clean up any stale temp files left from a prior crash
            foreach (var tmpFile in Directory.GetFiles(_options.OutboxPath, "*.tmp"))
            {
                try { File.Delete(tmpFile); } catch { /* best effort */ }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[outbox-init-error] Failed to initialize directory: {ex.Message}");
        }

        _drainTask = Task.Run(() => DrainQueueAsync(_drainCts.Token));
        _deliveryTask = Task.Run(() => ProcessDeliveryLoopAsync(_deliveryCts.Token));
        _workerTask = Task.WhenAll(_drainTask, _deliveryTask);

        // Signal initial delivery pass to replay existing files from disk
        SignalDelivery();

        Console.WriteLine($"[outbox] started path={_options.OutboxPath} target={_options.ApiUrl}");
    }

    /// <summary>
    /// Non-blocking enqueue on match completion. Never blocks simulation tick.
    /// Returns false immediately when memory queue is at bounded capacity.
    /// </summary>
    public bool TryEnqueue(MatchResult result)
    {
        if (!IsEnabled) return false;

        if (!_queue.Writer.TryWrite(result))
        {
            Console.Error.WriteLine($"[outbox-full] Memory queue full ({_options.QueueCapacity}). History unavailable for match {result.MatchId}");
            _metrics?.RecordHistoryUnavailable();
            return false;
        }

        return true;
    }

    private void SignalDelivery()
    {
        _deliverySignal.Writer.TryWrite(true);
    }

    /// <summary>
    /// Decoupled drain loop: immediately drains incoming match results from memory channel
    /// to durable storage on disk, independently of API delivery state or outages.
    /// </summary>
    private async Task DrainQueueAsync(CancellationToken ct)
    {
        try
        {
            while (await _queue.Reader.WaitToReadAsync(ct))
            {
                while (_queue.Reader.TryRead(out var result))
                {
                    await WriteResultToDiskAsync(result, ct);
                    SignalDelivery();
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Drain loop stopped
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[outbox-drain-error] {ex.Message}");
        }
    }

    private async Task WriteResultToDiskAsync(MatchResult result, CancellationToken ct)
    {
        try
        {
            // Check capacity bounds including quarantine directory
            var (fileCount, totalBytes) = GetStorageUsage();
            if (fileCount >= _options.MaxFiles)
            {
                Console.Error.WriteLine($"[outbox-limit] File limit ({_options.MaxFiles}) reached. Match {result.MatchId} history unavailable.");
                _metrics?.RecordHistoryUnavailable();
                return;
            }

            if (totalBytes >= _options.MaxBytes)
            {
                Console.Error.WriteLine($"[outbox-limit] Size limit ({_options.MaxBytes} bytes) reached. Match {result.MatchId} history unavailable.");
                _metrics?.RecordHistoryUnavailable();
                return;
            }

            var submission = new MatchResultSubmission(
                result.MatchId,
                result.RoomId,
                result.ConfigVersion,
                result.StartUtc,
                result.EndUtc,
                result.Reason,
                result.Participants.Select(p => new MatchParticipantSubmission(
                    p.PlayerSessionId,
                    p.DisplayName,
                    p.Rank,
                    p.Kills,
                    p.SurvivalSeconds,
                    p.ProfileKey
                )).ToList()
            );

            var json = JsonSerializer.Serialize(submission, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var serializedBytes=Encoding.UTF8.GetByteCount(json)+3;
            if (totalBytes+serializedBytes > _options.MaxBytes) { Console.Error.WriteLine("[outbox-limit] New result exceeds storage quota; history unavailable."); return; }
            var existingPath=Path.Combine(_options.OutboxPath,$"{result.MatchId:D}.json");
            if(File.Exists(existingPath))
            {
                var existing=JsonSerializer.Deserialize<MatchResultSubmission>(await File.ReadAllTextAsync(existingPath,ct),new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if(existing==null || MatchCanonicalHasher.ComputeCanonicalHash(existing)!=MatchCanonicalHasher.ComputeCanonicalHash(submission)) Console.Error.WriteLine("[outbox-conflict] Existing pending result preserved; new conflicting result rejected.");
                return;
            }
            var tmpFile = Path.Combine(_options.OutboxPath, $"{result.MatchId:D}.tmp");
            var finalFile = Path.Combine(_options.OutboxPath, $"{result.MatchId:D}.json");

            // Atomic temp file write + rename
            await File.WriteAllTextAsync(tmpFile, json, Encoding.UTF8, ct);
            File.Move(tmpFile, finalFile, overwrite: true);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Cancelled
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[outbox-write-error] Failed to persist match {result.MatchId}: {ex.Message}");
        }
    }

    private (int fileCount, long totalBytes) GetStorageUsage()
    {
        var count = 0;
        var bytes = 0L;
        try
        {
            if (Directory.Exists(_options.OutboxPath))
            {
                var dir = new DirectoryInfo(_options.OutboxPath);
                foreach (var fi in dir.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    try { var length=fi.Length; count++; bytes+=length; }
                    catch (FileNotFoundException) { } // Delivery can remove an acknowledged file concurrently.
                }
            }

            if (Directory.Exists(_quarantinePath))
            {
                var fullOutbox = Path.GetFullPath(_options.OutboxPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var fullQuarantine = Path.GetFullPath(_quarantinePath);
                if (!fullQuarantine.StartsWith(fullOutbox, StringComparison.OrdinalIgnoreCase))
                {
                    var qDir = new DirectoryInfo(_quarantinePath);
                    foreach (var fi in qDir.EnumerateFiles("*", SearchOption.AllDirectories))
                    {
                        count++;
                        bytes += fi.Length;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[outbox-storage-check-error] {ex.Message}");
            return (_options.MaxFiles,_options.MaxBytes); // Fail closed rather than undercounting storage.
        }
        return (count, bytes);
    }

    /// <summary>
    /// Delivery loop: delivers pending disk files with exponential backoff (2->30s).
    /// One failed file does not starve others; periodic retries happen automatically
    /// without requiring a new match to be enqueued.
    /// </summary>
    private async Task ProcessDeliveryLoopAsync(CancellationToken ct)
    {
        var backoffSeconds = _options.InitialBackoffSeconds;

        while (!ct.IsCancellationRequested)
        {
            bool hasPending;
            bool anyTransient;

            try
            {
                (hasPending, anyTransient) = await DeliverPendingFilesAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[outbox-delivery-loop-error] {ex.Message}");
                hasPending = true;
                anyTransient = true;
            }

            if (ct.IsCancellationRequested) break;

            if (hasPending)
            {
                // Wait for backoff duration OR until signaled by new file arrival or cancellation
                try
                {
                    using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    var delayTask = Task.Delay(TimeSpan.FromSeconds(backoffSeconds), delayCts.Token);
                    var signalTask = _deliverySignal.Reader.WaitToReadAsync(delayCts.Token).AsTask();

                    var completed = await Task.WhenAny(delayTask, signalTask);
                    delayCts.Cancel();

                    if (completed == signalTask && signalTask.IsCompletedSuccessfully && signalTask.Result)
                    {
                        while (_deliverySignal.Reader.TryRead(out _)) { }
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }

                if (anyTransient)
                {
                    backoffSeconds = Math.Min(_options.MaxBackoffSeconds, backoffSeconds * 2);
                }
            }
            else
            {
                // No pending files: reset backoff and wait for next signal
                backoffSeconds = _options.InitialBackoffSeconds;

                try
                {
                    // Scan disk periodically even if no new match arrives (restart/manual recovery).
                    using var idleCts=CancellationTokenSource.CreateLinkedTokenSource(ct);
                    await Task.WhenAny(_deliverySignal.Reader.WaitToReadAsync(idleCts.Token).AsTask(),Task.Delay(1000,idleCts.Token));
                    idleCts.Cancel();
                    while (_deliverySignal.Reader.TryRead(out _)) { }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private async Task<(bool hasPending, bool anyTransient)> DeliverPendingFilesAsync(CancellationToken ct)
    {
        string[] files;
        try
        {
            files = Directory.GetFiles(_options.OutboxPath, "*.json", SearchOption.TopDirectoryOnly)
                             .OrderBy(f => f)
                             .ToArray();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[outbox-scan-error] {ex.Message}");
            return (false, false);
        }

        if (files.Length == 0)
        {
            return (false, false);
        }

        var anyTransientFailure = false;

        foreach (var file in files)
        {
            if (ct.IsCancellationRequested) break;

            string json;
            try
            {
                json = await File.ReadAllTextAsync(file, Encoding.UTF8, ct);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[outbox-read-error] {file}: {ex.Message}");
                anyTransientFailure = true;
                continue;
            }

            // Verify payload is valid JSON before sending
            try
            {
                if(new FileInfo(file).Length > _options.MaxBytes) { QuarantineFile(file); continue; }
                var item=JsonSerializer.Deserialize<MatchResultSubmission>(json,new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if(item==null || item.MatchId==Guid.Empty || item.Participants==null || item.Participants.Count==0 || item.Participants.Count>10) throw new JsonException("Invalid match result schema.");
            }
            catch (JsonException ex)
            {
                // Malformed file quarantined and reported; do not prevent valid sends
                Console.Error.WriteLine($"[outbox-quarantine] Malformed JSON in {file}: {ex.Message}");
                QuarantineFile(file);
                continue;
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _options.ApiUrl);
                request.Headers.Add("X-Match-Result-Token", _options.Token);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(request, ct);

                if (response.IsSuccessStatusCode)
                {
                    // Delete ONLY after ACK
                    try { File.Delete(file); } catch { /* ignore */ }
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    // 409 Conflict: same MatchId but different payload.
                    // Must stop retry and quarantine; never hot loop infinitely.
                    Console.Error.WriteLine($"[outbox-quarantine] Conflict 409 for {file}. Stopping retry.");
                    QuarantineFile(file);
                }
                else if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    // Client rejection (e.g. 400 bad schema): quarantine to avoid blockage
                    Console.Error.WriteLine($"[outbox-quarantine] API rejected {(int)response.StatusCode} for {file}. Quarantining.");
                    QuarantineFile(file);
                }
                else
                {
                    // 5xx or server temporary issue: mark transient failure and move to next file
                    Console.Error.WriteLine($"[outbox-retry] API returned {(int)response.StatusCode} for {file}. Will retry later.");
                    anyTransientFailure = true;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[outbox-network-error] Delivery failed for {file}: {ex.Message}. Will retry later.");
                anyTransientFailure = true;
            }
        }

        var remaining = 0;
        try
        {
            remaining = Directory.GetFiles(_options.OutboxPath, "*.json", SearchOption.TopDirectoryOnly).Length;
        }
        catch { /* best effort */ }

        return (remaining > 0, anyTransientFailure);
    }

    private void QuarantineFile(string filePath)
    {
        try
        {
            Directory.CreateDirectory(_quarantinePath);
            var dest = Path.Combine(_quarantinePath, Path.GetFileName(filePath));
            File.Move(filePath, dest, overwrite: true);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[outbox-quarantine-error] Could not move {filePath} to quarantine: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops the outbox worker.
    /// Completes the queue writer and persists all pending queued items to disk within the
    /// bounded deadline before cancelling delivery tasks.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled) return;

        // 1. Complete queue writer so drain loop drains remaining items and finishes
        _queue.Writer.TryComplete();

        // 2. Wait for drain loop to persist all pending queue items to disk within bounded deadline
        if (_drainTask != null)
        {
            try
            {
                await _drainTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[outbox-stop-drain-error] {ex.Message}");
            }
        }

        // 3. Stop delivery loop
        _deliveryCts.Cancel();
        if (_deliveryTask != null)
        {
            try
            {
                await _deliveryTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[outbox-stop-delivery-error] {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        _drainCts.Cancel();
        _drainCts.Dispose();
        _deliveryCts.Cancel();
        _deliveryCts.Dispose();
        if (_ownsHttpClient) _httpClient.Dispose();
    }
}
