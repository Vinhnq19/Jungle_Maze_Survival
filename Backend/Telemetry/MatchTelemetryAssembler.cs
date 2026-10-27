#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using JungleBackend.Protocol.Gameplay;
using LiteNetLib.Utils;

namespace JungleMaze.Telemetry.Server;

/// <summary>
/// Luồng nền: rút ring của một phòng, giải mã frame bằng PacketRegistry riêng, giữ trạng thái theo MatchId và
/// dựng các bản ghi 1, 2, 3, 5, 6, 8, 14, 15, 16 cùng bản ghi H7 (4, 7, 9–13). Không đọc DisplayName, ProfileKey,
/// RoomCode hay resume token. Quy tắc ghép: Docs/Telemetry/Schema.vi.md mục 3.2 và TEL-01 mục 4.3.
/// </summary>
public sealed class MatchTelemetryAssembler : ITelemetryPump
{
    private const int Slots = 10;
    private const byte ZoneSource = 255;

    private static readonly string[] AttackKinds = { "MELEE", "SHOT", "THROW", "SPECIAL" };
    private static readonly string[] CcKinds = { "STUN", "ROOT", "REVERSE" };
    private static readonly string[] CcEnds = { "EXPIRED", "CLEANSED", "DEATH", "MATCH_END" };
    private static readonly string[] PickupFroms = { "CONTAINER", "FLOOR", "CORPSE", "AIRDROP" };
    private static readonly string[] UseResults = { "DONE", "CANCELLED", "INTERRUPTED" };
    private static readonly string[] EquipActions = { "EQUIP", "UNEQUIP", "DROP", "BREAK", "SWAP" };
    private static readonly string[] SlotNames = { "WEAPON", "HELMET", "ARMOR" };
    private static readonly string[] HideEnds = { "EXIT", "DEATH", "MATCH_END" };
    private static readonly string[] ZoneStates = { "WARNING", "SHRINK_START", "SHRINK_END", "FINAL" };

    private enum Cause : byte { None, Weapon, Zone, Hazard, Disconnect, Other }

    private struct LastDamage
    {
        public Cause Cause;
        public byte Attacker;
        public string? Weapon;
        public string? Hazard;
    }

    private struct PendingSelf
    {
        public byte Victim;
        public int Raw, Applied, HpAfter;
        public TelemetryEvent Source;
    }

    private struct PendingDetail
    {
        public TelemetryEvent Detail;
    }

    private readonly MatchTelemetryObserver _observer;
    private readonly JsonlTelemetryWriter _writer;
    private readonly RoomStaleProbe? _stale;
    private readonly int _sampleEvery;
    private readonly PacketRegistry _packets = new();
    private readonly NetDataReader _reader = new();

    private TelemetryEvent _cur;
    private long _attributedDrops;
    private long _carryDrops;

    // Trạng thái theo trận.
    private Guid _matchId;
    private TelemetryFile? _file;
    private bool _matchClosed;
    private MatchPhase _phase = MatchPhase.Waiting;
    private uint? _inGameTick;
    private uint _endingTick;
    private bool _startEmitted, _endEmitted;
    private readonly Dictionary<string, byte> _sessionToNet = new(StringComparer.Ordinal);
    private readonly string?[] _sess = new string?[Slots];
    private readonly bool[] _spawned = new bool[Slots];
    private readonly bool[] _spawnPending = new bool[Slots];
    private readonly float[] _x = new float[Slots], _y = new float[Slots];
    private readonly int[] _hp = new int[Slots];
    private readonly bool[] _alive = new bool[Slots];
    private readonly LastDamage[] _lastDamage = new LastDamage[Slots];
    private readonly bool[] _eliminated = new bool[Slots];
    private readonly bool[] _elimPending = new bool[Slots];
    private readonly TelemetryEvent[] _elimEvent = new TelemetryEvent[Slots];
    private readonly bool[] _disconnected = new bool[Slots];
    private readonly uint[] _disconnectTick = new uint[Slots];
    private readonly bool[] _replayDone = new bool[Slots];
    private readonly string?[] _weapon = new string?[Slots];
    private readonly uint[] _hideDeathTickPlusOne = new uint[Slots];
    private bool _hideSeen;
    private readonly List<PendingSelf> _pendingSelf = new();
    private readonly List<PendingDetail> _pendingDetails = new();
    private int _snapshotIndex;

    // Bo.
    private bool _hasZone, _zonePrevWarn, _zoneShrinking, _zoneFinal;
    private int _zonePrevPi, _zonePhaseLast = -1;
    private uint _zoneShrinkMark;
    private uint _zoneEmitted;
    private ZoneStatePacket _zone;

    // Kết quả giải mã của frame hiện tại.
    private bool _gotWorld, _gotZone;
    private WorldSnapshotPacket _world;

    public long DecodeErrors { get; private set; }
    public Guid CurrentMatchId => _matchId;

    public MatchTelemetryAssembler(MatchTelemetryObserver observer, JsonlTelemetryWriter writer, double sampleHz, RoomStaleProbe? stale = null)
    {
        _observer = observer;
        _writer = writer;
        _stale = stale;
        _sampleEvery = Math.Max(1, (int)Math.Round(30.0 / Math.Clamp(sampleHz, 0.5, 2)));
        _packets.Subscribe<MatchPhasePacket>(OnPhase);
        _packets.Subscribe<MatchRosterPacket>(OnRoster);
        _packets.Subscribe<MatchEntityEventPacket>(OnEntityEvent);
        _packets.Subscribe<MatchResultPacket>(OnResult);
        _packets.Subscribe<WorldSnapshotPacket>(p => { _world = p; _gotWorld = true; });
        _packets.Subscribe<ZoneStatePacket>(p => { _zone = p; _gotZone = true; });
        ResetMatch(Guid.Empty);
    }

    public void Pump(JsonlTelemetryWriter writer)
    {
        var ring = _observer.Ring;
        var any = false;
        while (ring.TryRead(out var e))
        {
            any = true;
            if (!_writer.Enabled) continue; // writer đã tắt: chỉ rút để nhả byte[]
            _cur = e;
            BeforeEvent(e);
            Handle(e);
        }
        AttributeDrops();
        if (_stale != null && _stale.IsStale(any))
        {
            FlushPending();
            CloseFile();
            _observer.Detach();
            _writer.Unregister(this);
        }
    }

    public void Complete(JsonlTelemetryWriter writer)
    {
        FlushPending();
        AttributeDrops();
        CloseFile();
    }

    private void AttributeDrops()
    {
        var dropped = _observer.Ring.Dropped;
        var delta = dropped - _attributedDrops;
        if (delta <= 0) return;
        _attributedDrops = dropped;
        if (_file != null && !_file.IsClosed) _file.DroppedQueue += delta;
        else _carryDrops += delta;
    }

    // ---------------------------------------------------------------- dispatch

    private void BeforeEvent(in TelemetryEvent e)
    {
        if (_pendingSelf.Count > 0 && e.Kind is not (TelemetryKind.Damaged or TelemetryKind.Died or TelemetryKind.GraceExpired))
            FlushSelf();
        if (_pendingDetails.Count > 0 &&
            (e.Kind is not (TelemetryKind.Damaged or TelemetryKind.DamageDetail or TelemetryKind.Died) || e.Tick != _pendingDetails[0].Detail.Tick))
            FlushDetails();
    }

    private void FlushPending()
    {
        FlushSelf();
        FlushDetails();
    }

    private void Handle(in TelemetryEvent e)
    {
        switch (e.Kind)
        {
            case TelemetryKind.Frame: DecodeFrame((byte[])e.Ref!); break;
            case TelemetryKind.Snapshot: OnSnapshotFrame((byte[])e.Ref!); break;
            case TelemetryKind.Damaged: OnDamaged(e); break;
            case TelemetryKind.Died: break; // nguyên nhân lấy từ damage cuối; elimination chờ Eliminated + roster
            case TelemetryKind.GraceExpired: OnGraceExpired((string)e.Ref!); break;
            case TelemetryKind.PlayerDropped: OnPlayerDropped((string)e.Ref!); break;
            case TelemetryKind.ResumeRotated: OnResume(e.A, (string)e.Ref!); break;
            case TelemetryKind.Disconnected: OnDisconnected((string)e.Ref!); break;
            case TelemetryKind.Left: OnLeft((string)e.Ref!); break;
            case TelemetryKind.ReplayAcked: OnReplayAcked((string)e.Ref!); break;
            case TelemetryKind.Attack: WriteAttack(e); break;
            case TelemetryKind.DamageDetail: _pendingDetails.Add(new PendingDetail { Detail = e }); break;
            case TelemetryKind.CcSpan: WriteCcSpan(e); break;
            case TelemetryKind.LootDrop: WriteLootDrop(e); break;
            case TelemetryKind.ItemPickup: WriteItemPickup(e); break;
            case TelemetryKind.ItemUse: WriteItemUse(e); break;
            case TelemetryKind.EquipChange: WriteEquipChange(e); break;
            case TelemetryKind.HideSpan: WriteHideSpan(e); break;
        }
    }

    private bool Decode(byte[] frame)
    {
        _reader.SetSource(frame);
        if (_packets.TryReadFrame(_reader)) return true;
        DecodeErrors++;
        return false;
    }

    private void DecodeFrame(byte[] frame) => Decode(frame);

    // ---------------------------------------------------------------- match lifecycle

    private void EnsureMatch(Guid matchId)
    {
        if (matchId == _matchId) return;
        FlushPending();
        CloseFile();
        ResetMatch(matchId);
    }

    private void ResetMatch(Guid matchId)
    {
        _matchId = matchId;
        _file = null;
        _matchClosed = false;
        _phase = MatchPhase.Waiting;
        _inGameTick = null;
        _endingTick = 0;
        _startEmitted = _endEmitted = false;
        _sessionToNet.Clear();
        Array.Clear(_sess);
        Array.Clear(_spawned);
        Array.Clear(_spawnPending);
        Array.Fill(_hp, -1);
        Array.Clear(_alive);
        Array.Clear(_lastDamage);
        Array.Clear(_eliminated);
        Array.Clear(_elimPending);
        Array.Clear(_disconnected);
        Array.Clear(_replayDone);
        Array.Clear(_weapon);
        Array.Clear(_hideDeathTickPlusOne);
        _hideSeen = false;
        _pendingSelf.Clear();
        _pendingDetails.Clear();
        _snapshotIndex = 0;
        _hasZone = _zonePrevWarn = _zoneShrinking = _zoneFinal = false;
        _zonePrevPi = 0;
        _zonePhaseLast = -1;
        _zoneShrinkMark = 0;
        _zoneEmitted = 0;
    }

    private TelemetryFile? EnsureFile()
    {
        if (_file != null) return _file.IsClosed ? null : _file;
        if (_matchClosed || _matchId == Guid.Empty || !_writer.Enabled) return null;
        _file = _writer.Open(_matchId);
        if (_file == null) return null;
        _file.RoomId = _observer.RoomId;
        _file.Cfg = _observer.ConfigVersion;
        _file.CfgHash = _observer.ConfigHash;
        _file.DroppedQueue += _carryDrops;
        _carryDrops = 0;
        return _file;
    }

    private void CloseFile()
    {
        if (_file != null) _writer.Close(_file);
        _file = null;
        _matchClosed = true;
    }

    private void OnPhase(MatchPhasePacket p)
    {
        EnsureMatch(p.MatchId);
        _phase = p.Phase;
        switch (p.Phase)
        {
            case MatchPhase.InGame:
                if (_startEmitted) break;
                _inGameTick = _cur.Tick;
                _startEmitted = true;
                if (Begin("match_start", _cur.Tick, out var j))
                {
                    j.Str("profile", _observer.ProfileName).UInt("players", p.PlayerCount).UInt("minPlayers", p.MinPlayers)
                        .Str("seedHash", Hash16(_matchId.ToString("D")));
                    Commit();
                }
                break;
            case MatchPhase.Ending:
                _endingTick = _cur.Tick;
                break;
            case MatchPhase.Cleanup:
                FlushPending();
                EmitReplayTimeouts();
                CloseFile();
                break;
        }
    }

    private void OnRoster(MatchRosterPacket p)
    {
        EnsureMatch(p.MatchId);
        if (p.Players == null) return;
        foreach (var player in p.Players)
        {
            if (player.NetId >= Slots) continue;
            var session = player.PlayerSessionId ?? "";
            if (session.Length > 0)
            {
                _sessionToNet[session] = player.NetId;
                _sess[player.NetId] ??= Hash16("tel1:" + session);
            }
            if (_elimPending[player.NetId] && player.Rank > 0)
                EmitElimination(player.NetId, player.Rank, player.SurvivalTicks);
        }
    }

    private void OnEntityEvent(MatchEntityEventPacket p)
    {
        EnsureMatch(p.MatchId);
        var n = p.NetId;
        if (n >= Slots) return;
        switch (p.Kind)
        {
            case MatchEntityEvent.Spawn:
                _spawned[n] = true;
                _spawnPending[n] = true;
                _alive[n] = true;
                break;
            case MatchEntityEvent.Eliminated:
                if (_spawned[n] && !_eliminated[n] && !_elimPending[n])
                {
                    _elimPending[n] = true;
                    _elimEvent[n] = _cur;
                }
                break;
        }
    }

    private void OnResult(MatchResultPacket p)
    {
        EnsureMatch(p.MatchId);
        if (_endEmitted) return;
        _endEmitted = true;
        var alive = 0;
        for (var n = 0; n < Slots; n++)
            if (_spawned[n] && !_eliminated[n] && !_elimPending[n]) alive++;
        if (!Begin("match_end", _cur.Tick, out var j)) return;
        j.Str("reason", p.Reason switch
            {
                MatchEndReason.LastAlive => "LastAlive",
                MatchEndReason.Tie => "Tie",
                MatchEndReason.Timeout => "Timeout",
                _ => "Cancelled"
            })
            .NetIdOrNull("winnerNetId", p.WinnerNetId >= Slots ? TelemetryEvent.NoNetId : p.WinnerNetId)
            .UInt("durTicks", _inGameTick is { } start ? unchecked(_cur.Tick - start) : 0)
            .UInt("alive", (uint)alive);
        Commit();
    }

    // ---------------------------------------------------------------- snapshot: spawn, zone, samples

    private void OnSnapshotFrame(byte[] frame)
    {
        _gotWorld = _gotZone = false;
        if (!Decode(frame) || !_gotWorld) return;
        if (_world.MatchId != _matchId) EnsureMatch(_world.MatchId);

        for (var i = 0; i < _world.PlayerCount; i++)
        {
            var player = _world.GetPlayer(i);
            if (player.NetId >= Slots) continue;
            _x[player.NetId] = player.X;
            _y[player.NetId] = player.Y;
            _hp[player.NetId] = player.Hp;
        }

        if (_inGameTick is { } spawnTick) EmitPendingSpawns(spawnTick);
        if (_gotZone) OnZone(_zone, _world.ServerTick);

        if (_snapshotIndex++ % _sampleEvery == 0) EmitSamples();
    }

    private void EmitPendingSpawns(uint spawnTick)
    {
        for (var i = 0; i < _world.PlayerCount; i++)
        {
            var player = _world.GetPlayer(i);
            var n = player.NetId;
            if (n >= Slots || !_spawnPending[n]) continue;
            _spawnPending[n] = false;
            if (!Begin("player_spawn", spawnTick, out var j)) continue;
            j.UInt("netId", n).Str("sess", _sess[n] ?? Hash16("tel1:netId-" + n)).Num("x", player.X).Num("y", player.Y)
                .BeginArray("loadout").EndArray();
            Commit();
        }
    }

    private void OnZone(in ZoneStatePacket z, uint tick)
    {
        _hasZone = true;
        var pi = (int)z.PhaseIndex;
        var final = z.CurrentRadius <= 0.0001f;
        if (pi > _zonePrevPi)
        {
            if (_zoneShrinking) EmitZone(_zonePrevPi - 1, 2, z, tick);
            EmitZone(pi - 1, 1, z, tick);
            _zoneShrinking = true;
            _zoneShrinkMark = z.ShrinkStartTick;
        }
        else if (_zoneShrinking && (z.ShrinkStartTick != _zoneShrinkMark || final))
        {
            EmitZone(pi - 1, 2, z, tick);
            _zoneShrinking = false;
        }
        if (final && !_zoneFinal)
        {
            if (_zoneShrinking)
            {
                EmitZone(pi - 1, 2, z, tick);
                _zoneShrinking = false;
            }
            EmitZone(pi - 1, 3, z, tick);
            _zoneFinal = true;
        }
        if (z.InWarning && !_zonePrevWarn) EmitZone(pi, 0, z, tick);
        _zonePrevPi = pi;
        _zonePrevWarn = z.InWarning;
    }

    private void EmitZone(int phase, int state, in ZoneStatePacket z, uint tick)
    {
        if (phase < 0 || phase > 4) return;
        var bit = 1u << (phase * 4 + state);
        if ((_zoneEmitted & bit) != 0) return;
        _zoneEmitted |= bit;
        _zonePhaseLast = phase;
        if (!Begin("zone_phase", tick, out var j)) return;
        j.Int("phase", phase).Str("state", ZoneStates[state]).Num("cx", z.CurrentCenterX).Num("cy", z.CurrentCenterY)
            .Num("r", z.CurrentRadius).Num("targetR", z.TargetRadius);
        Commit();
    }

    private bool InZone(float x, float y)
    {
        if (!_hasZone) return true;
        if (_zone.CurrentRadius <= 0.0001f) return false;
        var dx = x - _zone.CurrentCenterX;
        var dy = y - _zone.CurrentCenterY;
        return MathF.Sqrt(dx * dx + dy * dy) <= _zone.CurrentRadius;
    }

    private void EmitSamples()
    {
        for (var i = 0; i < _world.PlayerCount; i++)
        {
            var player = _world.GetPlayer(i);
            var n = player.NetId;
            if (n >= Slots || !_spawned[n] || player.IsDead || player.Hp == 0) continue;
            if (!Begin("position_sample", _world.ServerTick, out var j)) return;
            j.UInt("netId", n).Num("x", player.X).Num("y", player.Y).UInt("hp", player.Hp).Str("weapon", _weapon[n])
                .Bool("inZone", InZone(player.X, player.Y));
            Commit();
        }
    }

    // ---------------------------------------------------------------- damage and elimination

    private void OnDamaged(in TelemetryEvent e)
    {
        var victim = e.A;
        if (victim >= Slots) return;
        var source = e.B;
        var raw = e.I0;
        int hpAfter = e.C;
        var applied = _hp[victim] >= 0 ? Math.Clamp(_hp[victim] - hpAfter, 0, raw) : raw;
        _hp[victim] = hpAfter;

        if (source == ZoneSource)
        {
            WriteDamage(e, victim, Cause.Zone, TelemetryEvent.NoNetId, null, "ZONE", raw, applied, -1, hpAfter, -1, -1);
            return;
        }
        if (source == victim)
        {
            _pendingSelf.Add(new PendingSelf { Victim = victim, Raw = raw, Applied = applied, HpAfter = hpAfter, Source = e });
            return;
        }

        // Ghép chi tiết H7 (DamageDetail ngay trước ApplyDamage cùng tick).
        for (var i = 0; i < _pendingDetails.Count; i++)
        {
            var d = _pendingDetails[i].Detail;
            if (d.A != victim || d.B != source) continue;
            _pendingDetails.RemoveAt(i);
            var hazard = d.Ref2 as string;
            var cause = hazard != null ? Cause.Hazard : Cause.Weapon;
            WriteDamage(e, victim, cause, cause == Cause.Weapon ? source : TelemetryEvent.NoNetId, cause == Cause.Weapon ? d.Ref as string : null,
                hazard, Math.Max(d.I0, applied), applied, d.I1, hpAfter, d.I2, d.I3);
            return;
        }
        WriteDamage(e, victim, Cause.Weapon, source, null, null, raw, applied, -1, hpAfter, -1, -1);
    }

    private void FlushSelf(byte? disconnectedVictim = null)
    {
        for (var i = 0; i < _pendingSelf.Count; i++)
        {
            var p = _pendingSelf[i];
            if (disconnectedVictim is { } only && p.Victim != only) continue;
            var cause = disconnectedVictim.HasValue ? Cause.Disconnect : Cause.Other;
            WriteDamage(p.Source, p.Victim, cause, TelemetryEvent.NoNetId, null, null, p.Raw, p.Applied, -1, p.HpAfter, -1, -1);
            _pendingSelf.RemoveAt(i);
            i--;
        }
    }

    /// <summary>Chi tiết không có EntityDamaged theo sau (giáp chặn hết): vẫn ghi damage với applied = 0.</summary>
    private void FlushDetails()
    {
        foreach (var pending in _pendingDetails)
        {
            var d = pending.Detail;
            if (d.A >= Slots) continue;
            var hazard = d.Ref2 as string;
            var cause = hazard != null ? Cause.Hazard : Cause.Weapon;
            WriteDamage(d, d.A, cause, cause == Cause.Weapon ? d.B : TelemetryEvent.NoNetId, cause == Cause.Weapon ? d.Ref as string : null,
                hazard, Math.Max(0, d.I0), 0, d.I1, Math.Max(0, _hp[d.A]), d.I2, d.I3);
        }
        _pendingDetails.Clear();
    }

    private void WriteDamage(in TelemetryEvent at, byte victim, Cause cause, byte attacker, string? weapon, string? hazard,
        int raw, int applied, int absorbed, int hpAfter, int helmetDur, int armorDur)
    {
        _lastDamage[victim] = new LastDamage { Cause = cause, Attacker = attacker, Weapon = weapon, Hazard = hazard };
        if (!Begin("damage", at.Tick, at.Utc, out var j)) return;
        j.UInt("victim", victim).NetIdOrNull("attacker", attacker).Str("cause", CauseName(cause)).Str("weapon", weapon).Str("hazard", hazard)
            .Int("raw", raw).Int("applied", Math.Min(applied, raw)).IntOrNull("absorbed", absorbed).Int("hpAfter", Math.Clamp(hpAfter, 0, 255))
            .IntOrNull("helmetDur", helmetDur).IntOrNull("armorDur", armorDur);
        Commit();
    }

    private void EmitElimination(byte victim, byte rank, uint survTicks)
    {
        _elimPending[victim] = false;
        _eliminated[victim] = true;
        _alive[victim] = false;
        var at = _elimEvent[victim];
        var last = _lastDamage[victim];
        var cause = last.Cause == Cause.None ? Cause.Other : last.Cause;
        bool? hidden = _hideSeen ? _hideDeathTickPlusOne[victim] == at.Tick + 1 : null;
        if (!Begin("elimination", at.Tick, at.Utc, out var j)) return;
        j.UInt("victim", victim).NetIdOrNull("killer", cause == Cause.Weapon ? last.Attacker : TelemetryEvent.NoNetId)
            .Str("cause", CauseName(cause)).Str("weapon", cause == Cause.Weapon ? last.Weapon : null)
            .Str("hazard", cause is Cause.Zone or Cause.Hazard ? last.Hazard : null)
            .UInt("rank", rank).UInt("survTicks", survTicks).Num("x", _x[victim]).Num("y", _y[victim])
            .Int("zonePhase", _zonePhaseLast).BoolOrNull("hidden", hidden);
        Commit();
    }

    private static string CauseName(Cause cause) => cause switch
    {
        Cause.Weapon => "WEAPON",
        Cause.Zone => "ZONE",
        Cause.Hazard => "HAZARD",
        Cause.Disconnect => "DISCONNECT",
        _ => "OTHER"
    };

    // ---------------------------------------------------------------- sessions and replay

    private bool TryNet(string session, out byte netId) => _sessionToNet.TryGetValue(session, out netId) && netId < Slots;

    private void OnGraceExpired(string session)
    {
        if (!TryNet(session, out var n)) return;
        FlushSelf(n);
        _disconnected[n] = false;
        WriteSession(n, "GRACE_EXPIRED", null);
    }

    private void OnPlayerDropped(string session)
    {
        if (_phase != MatchPhase.Loading || !TryNet(session, out var n)) return;
        WriteSession(n, "LOAD_TIMEOUT", null);
    }

    private void OnResume(byte netId, string session)
    {
        if (netId >= Slots) return;
        _sessionToNet[session] = netId;
        // StartInGame cũng xoay token cho mọi người, trước frame phase InGame: đó không phải resume (X1).
        if (_phase != MatchPhase.InGame || !_startEmitted) return;
        uint? absentMs = _disconnected[netId] ? (uint)(unchecked(_cur.Tick - _disconnectTick[netId]) * 1000L / 30) : null;
        _disconnected[netId] = false;
        WriteSession(netId, "RESUMED", absentMs);
    }

    private void OnDisconnected(string session)
    {
        if (_phase != MatchPhase.InGame || !TryNet(session, out var n)) return;
        _disconnected[n] = true;
        _disconnectTick[n] = _cur.Tick;
        WriteSession(n, "DROPPED", (uint)Math.Round(_observer.DisconnectGraceSeconds * 1000));
    }

    private void OnLeft(string session)
    {
        if (!TryNet(session, out var n)) return;
        WriteSession(n, "LEFT", null);
    }

    private void WriteSession(byte netId, string kind, uint? graceMs)
    {
        if (!Begin("session_state", _cur.Tick, out var j)) return;
        j.UInt("netId", netId).Str("kind", kind).Null("transport").UIntOrNull("graceMs", graceMs);
        Commit();
    }

    private void OnReplayAcked(string session)
    {
        if (_phase != MatchPhase.Ending || !TryNet(session, out var n) || _replayDone[n]) return;
        _replayDone[n] = true;
        WriteReplay(n, "REPLAY", (uint)(unchecked(_cur.Tick - _endingTick) * 1000L / 30));
    }

    /// <summary>Hết Ending: ai chưa ack là TIMEOUT hoặc DISCONNECTED. Chỉ khi có H6, không thì không đoán.</summary>
    private void EmitReplayTimeouts()
    {
        if (!_observer.HasReplayHook || !_endEmitted) return;
        for (byte n = 0; n < Slots; n++)
        {
            if (!_spawned[n] || _replayDone[n]) continue;
            _replayDone[n] = true;
            if (_disconnected[n]) WriteReplay(n, "DISCONNECTED", null);
            else WriteReplay(n, "TIMEOUT", (uint)(unchecked(_cur.Tick - _endingTick) * 1000L / 30));
        }
    }

    private void WriteReplay(byte netId, string choice, uint? afterMs)
    {
        if (!Begin("replay_choice", _cur.Tick, out var j)) return;
        j.UInt("netId", netId).Str("choice", choice).UIntOrNull("afterMs", afterMs);
        Commit();
    }

    // ---------------------------------------------------------------- H7

    private static string Name(string[] names, byte index) => index < names.Length ? names[index] : names[^1];

    private void WriteAttack(in TelemetryEvent e)
    {
        if (e.A >= Slots || !Begin("attack", e.Tick, e.Utc, out var j)) return;
        j.UInt("netId", e.A).Str("weapon", e.Ref as string).Str("kind", Name(AttackKinds, e.B)).Num("x", e.F0).Num("y", e.F1)
            .IntOrNull("ammo", e.I0);
        Commit();
    }

    private void WriteCcSpan(in TelemetryEvent e)
    {
        if (e.A >= Slots || !Begin("cc_span", e.Tick, e.Utc, out var j)) return;
        var start = unchecked((uint)e.I0);
        j.UInt("victim", e.A).NetIdOrNull("source", e.B >= Slots ? TelemetryEvent.NoNetId : e.B).Str("by", e.Ref as string)
            .Str("ccKind", Name(CcKinds, e.C)).UInt("startTick", start).UInt("endTick", Math.Max(start, e.Tick))
            .UInt("plannedMs", unchecked((uint)e.I1)).Str("end", Name(CcEnds, e.D));
        Commit();
    }

    private void WriteLootDrop(in TelemetryEvent e)
    {
        if (!Begin("loot_drop", e.Tick, e.Utc, out var j)) return;
        j.Str("container", e.Ref as string).NetIdOrNull("by", e.A >= Slots ? TelemetryEvent.NoNetId : e.A).BeginArray("items");
        if (e.Ref2 is TelemetryLootItem[] items)
            for (var i = 0; i < items.Length && i < 8; i++)
                j.ItemObject("id", items[i].Id, "qty", Math.Clamp(items[i].Qty, 1, 999));
        j.EndArray().Num("x", e.F0).Num("y", e.F1);
        Commit();
    }

    private void WriteItemPickup(in TelemetryEvent e)
    {
        if (e.A >= Slots || !Begin("item_pickup", e.Tick, e.Utc, out var j)) return;
        j.UInt("netId", e.A).Str("item", e.Ref as string).Int("qty", Math.Clamp(e.I0, 1, 999)).Str("from", Name(PickupFroms, e.B))
            .Num("x", e.F0).Num("y", e.F1);
        Commit();
    }

    private void WriteItemUse(in TelemetryEvent e)
    {
        if (e.A >= Slots || !Begin("item_use", e.Tick, e.Utc, out var j)) return;
        var start = unchecked((uint)e.I0);
        j.UInt("netId", e.A).Str("item", e.Ref as string).UInt("startTick", start).UInt("endTick", Math.Max(start, e.Tick))
            .UInt("plannedMs", unchecked((uint)e.I1)).Str("result", Name(UseResults, e.B)).UInt("hpBefore", e.C).UInt("hpAfter", e.D);
        Commit();
    }

    private void WriteEquipChange(in TelemetryEvent e)
    {
        if (e.A >= Slots) return;
        var item = e.Ref as string;
        if (e.C == (byte)TelemetrySlot.Weapon)
        {
            _weapon[e.A] = e.B is (byte)TelemetryEquipAction.Equip or (byte)TelemetryEquipAction.Swap ? item : null;
        }
        if (!Begin("equip_change", e.Tick, e.Utc, out var j)) return;
        j.UInt("netId", e.A).Str("action", Name(EquipActions, e.B)).Str("slot", Name(SlotNames, e.C)).Str("item", item)
            .Str("from", e.Ref2 as string);
        Commit();
    }

    private void WriteHideSpan(in TelemetryEvent e)
    {
        if (e.A >= Slots) return;
        _hideSeen = true;
        if (e.B == (byte)TelemetryHideEnd.Death) _hideDeathTickPlusOne[e.A] = e.Tick + 1;
        if (!Begin("hide_span", e.Tick, e.Utc, out var j)) return;
        var start = unchecked((uint)e.I0);
        j.UInt("netId", e.A).UInt("startTick", start).UInt("endTick", Math.Max(start, e.Tick)).Str("bush", e.Ref as string)
            .Num("x", e.F0).Num("y", e.F1).Str("end", Name(HideEnds, e.B)).Bool("attackedWhileHidden", e.C != 0);
        Commit();
    }

    // ---------------------------------------------------------------- envelope helpers

    private bool Begin(string type, uint tick, out TelemetryJson json) => Begin(type, tick, _cur.Utc, out json);

    private bool Begin(string type, uint tick, long utcTicks, out TelemetryJson json)
    {
        var file = EnsureFile();
        if (file == null)
        {
            json = null!;
            return false;
        }
        uint? mt = _inGameTick is { } start && tick >= start ? tick - start : null;
        return _writer.TryBegin(file, type, tick, mt, (utcTicks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerMillisecond, out json);
    }

    private void Commit() => _writer.Commit(_file!);

    public static string Hash16(string value)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(value), hash);
        return Convert.ToHexString(hash[..8]).ToLowerInvariant();
    }
}

/// <summary>
/// Phát hiện phòng không còn dùng (không phải phòng mặc định, không còn peer và không có tín hiệu trong một khoảng)
/// để gỡ observer và nhả ring. RoomManager không có event xóa phòng nên đây là cách duy nhất không sửa GameServer.
/// </summary>
public sealed class RoomStaleProbe
{
    private readonly Func<bool> _isEmptyNonDefault;
    private readonly TimeSpan _after;
    private DateTime _quietSince = DateTime.UtcNow;

    public RoomStaleProbe(Func<bool> isEmptyNonDefault, TimeSpan after)
    {
        _isEmptyNonDefault = isEmptyNonDefault;
        _after = after;
    }

    public bool IsStale(bool hadEvents)
    {
        var now = DateTime.UtcNow;
        if (hadEvents || !_isEmptyNonDefault())
        {
            _quietSince = now;
            return false;
        }
        return now - _quietSince >= _after;
    }
}
