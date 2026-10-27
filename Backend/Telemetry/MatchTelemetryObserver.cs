#nullable enable
using System;
using System.Reflection;
using GameServer.Simulation;
using JungleBackend.Protocol.Gameplay;

namespace JungleMaze.Telemetry.Server;

/// <summary>
/// Gắn vào một MatchRuntime (H2/H3). Mọi handler chạy trên luồng gọi event (thường là luồng tick) và chỉ
/// chép một struct vào ring: không cấp phát, không khóa, không I/O. Delegate tạo một lần trong constructor.
/// Cũng là sink H7 của phòng (ITelemetrySink): đóng dấu tick và utc lúc nhận.
/// </summary>
public sealed class MatchTelemetryObserver : ITelemetrySink
{
    private readonly MatchRuntime _match;
    private readonly JsonlTelemetryWriter _writer;
    private readonly Action<byte[]> _onFrame;
    private readonly Action<byte[]> _onSnapshot;
    private readonly Action<string> _onDropped;
    private readonly Action<string> _onGrace;
    private readonly Action<string, byte, Guid> _onResume;
    private readonly Action<byte, byte, byte> _onDamaged;
    private readonly Action<byte, byte> _onDied;
    private readonly Action<string> _onDisconnected;
    private readonly Action<string> _onLeft;
    private readonly Action<string> _onReplay;
    private EventInfo? _disconnectedEvent, _leftEvent, _replayEvent;
    private IHealthService? _health;
    private int _detached;

    public TelemetryRing Ring { get; }
    public string RoomId { get; }
    public string ProfileName { get; }
    public string ConfigVersion { get; }
    public string ConfigHash { get; }
    public double DisconnectGraceSeconds { get; }
    public bool HasDisconnectHook => _disconnectedEvent != null;
    public bool HasLeftHook => _leftEvent != null;
    public bool HasReplayHook => _replayEvent != null;
    public bool IsDetached => System.Threading.Volatile.Read(ref _detached) != 0;

    public bool Enabled => _writer.Enabled && !IsDetached;
    public long Dropped => Ring.Dropped;

    public MatchTelemetryObserver(MatchRuntime match, JsonlTelemetryWriter writer, TelemetryRing? ring = null)
    {
        _match = match;
        _writer = writer;
        Ring = ring ?? new TelemetryRing();
        RoomId = match.RoomId;
        ProfileName = match.Profile.Name;
        ConfigVersion = match.Profile.Version;
        ConfigHash = match.Profile.ConfigHash;
        DisconnectGraceSeconds = match.Profile.DisconnectGraceSeconds;

        _onFrame = OnFrame;
        _onSnapshot = OnSnapshot;
        _onDropped = session => Push(TelemetryKind.PlayerDropped, 0, 0, 0, 0, session);
        _onGrace = session => Push(TelemetryKind.GraceExpired, 0, 0, 0, 0, session);
        _onResume = (session, netId, _) => Push(TelemetryKind.ResumeRotated, netId, 0, 0, 0, session); // token bỏ ngay
        _onDamaged = (victim, damage, source) => Push(TelemetryKind.Damaged, victim, source, _match.Health.GetHp(victim), damage, null);
        _onDied = (victim, source) => Push(TelemetryKind.Died, victim, source, 0, 0, null);
        _onDisconnected = session => Push(TelemetryKind.Disconnected, 0, 0, 0, 0, session);
        _onLeft = session => Push(TelemetryKind.Left, 0, 0, 0, 0, session);
        _onReplay = session => Push(TelemetryKind.ReplayAcked, 0, 0, 0, 0, session);
    }

    /// <summary>Đăng ký event public. H5/H6 tìm theo tên một lần ở đây (không chạy trên luồng tick).</summary>
    public void Subscribe()
    {
        _match.ReliableBroadcast += _onFrame;
        _match.SnapshotGenerated += _onSnapshot;
        _match.PlayerDropped += _onDropped;
        _match.GraceExpired += _onGrace;
        _match.ResumeTokenRotated += _onResume;
        _disconnectedEvent = BindOptional("PlayerDisconnected", _onDisconnected);
        _leftEvent = BindOptional("PlayerLeft", _onLeft);
        _replayEvent = BindOptional("ReplayAcked", _onReplay);
        RebindHealth();
    }

    /// <summary>Gỡ mọi đăng ký; an toàn từ luồng nền vì add/remove của event là nguyên tử.</summary>
    public void Detach()
    {
        if (System.Threading.Interlocked.Exchange(ref _detached, 1) != 0) return;
        _match.ReliableBroadcast -= _onFrame;
        _match.SnapshotGenerated -= _onSnapshot;
        _match.PlayerDropped -= _onDropped;
        _match.GraceExpired -= _onGrace;
        _match.ResumeTokenRotated -= _onResume;
        _disconnectedEvent?.RemoveEventHandler(_match, _onDisconnected);
        _leftEvent?.RemoveEventHandler(_match, _onLeft);
        _replayEvent?.RemoveEventHandler(_match, _onReplay);
        var health = _health;
        if (health != null)
        {
            health.EntityDamaged -= _onDamaged;
            health.EntityDied -= _onDied;
        }
    }

    private EventInfo? BindOptional(string name, Action<string> handler)
    {
        var info = typeof(MatchRuntime).GetEvent(name, BindingFlags.Instance | BindingFlags.Public);
        if (info == null || info.EventHandlerType != typeof(Action<string>)) return null;
        info.AddEventHandler(_match, handler);
        return info;
    }

    /// <summary>HealthService được tạo mới ở Cleanup mà không phát event (R3, X6): so tham chiếu ở mỗi frame.</summary>
    private void RebindHealth()
    {
        var current = _match.Health;
        if (ReferenceEquals(current, _health)) return;
        if (_health != null)
        {
            _health.EntityDamaged -= _onDamaged;
            _health.EntityDied -= _onDied;
        }
        current.EntityDamaged += _onDamaged;
        current.EntityDied += _onDied;
        _health = current;
    }

    private void OnFrame(byte[] frame)
    {
        if (IsDetached) return;
        RebindHealth();
        if (!_writer.Enabled) return;
        var e = new TelemetryEvent { Kind = TelemetryKind.Frame, Ref = frame, Tick = _match.CurrentTick, Utc = DateTime.UtcNow.Ticks };
        Ring.TryWrite(in e);
    }

    private void OnSnapshot(byte[] frame)
    {
        if (!_writer.Enabled || IsDetached) return;
        var e = new TelemetryEvent { Kind = TelemetryKind.Snapshot, Ref = frame, Tick = _match.CurrentTick, Utc = DateTime.UtcNow.Ticks };
        Ring.TryWrite(in e, bulk: true);
    }

    private void Push(TelemetryKind kind, byte a, byte b, byte c, int i0, string? session)
    {
        if (!_writer.Enabled || IsDetached) return;
        var e = new TelemetryEvent
        {
            Kind = kind, A = a, B = b, C = c, I0 = i0, Ref = session, Tick = _match.CurrentTick, Utc = DateTime.UtcNow.Ticks
        };
        Ring.TryWrite(in e);
    }

    /// <summary>Sink H7: chủ ticket gameplay gọi tại điểm quyết định.</summary>
    public bool TryWrite(in TelemetryEvent e)
    {
        if (!_writer.Enabled || IsDetached) return false;
        var copy = e;
        copy.Tick = _match.CurrentTick;
        copy.Utc = DateTime.UtcNow.Ticks;
        return Ring.TryWrite(in copy);
    }
}
