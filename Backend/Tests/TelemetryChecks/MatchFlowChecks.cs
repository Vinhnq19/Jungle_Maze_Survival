using System.Text.Json;
using GameServer.Simulation;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.Telemetry.Server;

namespace TelemetryChecks;

/// <summary>
/// Trận MatchRuntime thật trong tiến trình (AC36 proxy, AC37 phần S0, AC4 phía server, AC26 hai trận, R3).
/// Dữ liệu game thật từ GameData, profile test (bo tăng tốc 25 lần, grace 1 s).
/// </summary>
public static class MatchFlowChecks
{
    private const double Dt = 1.0 / 30.0;
    private static readonly string[] Sessions = { "sess-secret-0", "sess-secret-1", "sess-secret-2", "sess-secret-3" };
    private const string ResumedSession = "sess-secret-2b";
    private const string RoomCode = "ROOMCODE_SECRET_9Z";

    public sealed class FlowResult
    {
        public required string Root { get; init; }
        public required List<Guid> MatchIds { get; init; }
        public required List<MatchResult> Results { get; init; }
        public required List<Guid> Tokens { get; init; }
        public required MapData Map { get; init; }
        public required MatchProfile Profile { get; init; }
        public required ZoneSchedule Zone { get; init; }
        public required MatchTelemetryObserver Observer { get; init; }
    }

    public sealed class H7Result
    {
        public required string Root { get; init; }
        public required Guid MatchId { get; init; }
    }

    public static (MapData Map, ZoneSchedule Zone, MatchProfile Profile) LoadData()
    {
        Environment.SetEnvironmentVariable("MATCH_PROFILE", "test");
        var map = MapData.Load(GameDataLoader.ReadGameDataFile("map_demo.json"));
        var zone = ZoneSchedule.Load(GameDataLoader.ReadGameDataFile("zone_schedule.json"));
        return (map, zone, MatchProfile.CreateTestProfile());
    }

    private static void TickUntil(MatchRuntime match, Func<bool> done, int maxTicks)
    {
        for (var i = 0; i < maxTicks && !done(); i++) match.Tick(Dt);
    }

    /// <summary>Hai trận liên tiếp trong cùng phòng: rớt/nối lại, hết grace, chết vì bo, rồi LOAD_TIMEOUT ở trận 2.</summary>
    public static FlowResult RunFlow(string root)
    {
        var (map, zone, profile) = LoadData();
        var host = TelemetryHost.Create(new TelemetryOptions { RootPath = root }, TextWriter.Null);
        var match = new MatchRuntime(RoomCode, map, zone, profile, "room-tel01");
        var observer = host.Attach(match)!;
        var tokens = new Dictionary<string, Guid>();
        var allTokens = new List<Guid>();
        var results = new List<MatchResult>();
        var matchIds = new List<Guid>();
        match.ResumeTokenRotated += (session, _, token) => { tokens[session] = token; allTokens.Add(token); };
        match.MatchCompleted += results.Add;

        for (var i = 0; i < Sessions.Length; i++)
        {
            match.TryRegisterPlayer(Sessions[i], $"SECRET_NAME_{i}", out _);
            match.SetPlayerReady(Sessions[i]);
        }

        // Trận 1.
        TickUntil(match, () => match.Phase == MatchPhase.Loading, 200);
        matchIds.Add(match.MatchId);
        foreach (var s in Sessions) match.SetPlayerLoaded(s, match.MatchId);
        TickUntil(match, () => match.Phase == MatchPhase.InGame, 10);
        TickUntil(match, () => false, 30);
        match.MarkDisconnected(Sessions[3]); // hết grace 1 s -> GRACE_EXPIRED + elimination DISCONNECT
        TickUntil(match, () => false, 45);
        match.MarkDisconnected(Sessions[2]);
        TickUntil(match, () => false, 10);
        match.TryResume(ResumedSession, tokens[Sessions[2]], out _, out _);
        TickUntil(match, () => match.Phase == MatchPhase.Ending, 30 * 120);
        TickUntil(match, () => match.Phase == MatchPhase.Waiting, 200);

        // Trận 2: chỉ hai người báo tải xong, người thứ ba bị LOAD_TIMEOUT.
        TickUntil(match, () => match.Phase == MatchPhase.Loading, 200);
        matchIds.Add(match.MatchId);
        match.SetPlayerLoaded(Sessions[0], match.MatchId);
        match.SetPlayerLoaded(Sessions[1], match.MatchId);
        TickUntil(match, () => match.Phase == MatchPhase.InGame, 60);
        TickUntil(match, () => match.Phase == MatchPhase.Ending, 30 * 120);
        TickUntil(match, () => match.Phase == MatchPhase.Waiting, 200);

        host.Dispose();
        return new FlowResult
        {
            Root = root, MatchIds = matchIds, Results = results, Tokens = allTokens, Map = map, Profile = profile, Zone = zone,
            Observer = observer
        };
    }

    /// <summary>Gọi mọi factory H7 trên một trận thật, kể cả damage chi tiết ghép với ApplyDamage và hạ gục bằng vũ khí.</summary>
    public static H7Result RunH7(string root)
    {
        var (map, zone, profile) = LoadData();
        var host = TelemetryHost.Create(new TelemetryOptions { RootPath = root }, TextWriter.Null);
        var match = new MatchRuntime("ROOMCODE_H7", map, zone, profile, "room-h7");
        var sink = host.Attach(match)!;
        match.TryRegisterPlayer("sess-h7-0", "SECRET_NAME_A", out _);
        match.TryRegisterPlayer("sess-h7-1", "SECRET_NAME_B", out _);
        match.SetPlayerReady("sess-h7-0");
        match.SetPlayerReady("sess-h7-1");
        TickUntil(match, () => match.Phase == MatchPhase.Loading, 200);
        var matchId = match.MatchId;
        match.SetPlayerLoaded("sess-h7-0", matchId);
        match.SetPlayerLoaded("sess-h7-1", matchId);
        TickUntil(match, () => match.Phase == MatchPhase.InGame, 10);
        TickUntil(match, () => false, 5);

        var start = match.CurrentTick;
        sink.TryWrite(TelemetryEvent.EquipChange(0, TelemetryEquipAction.Equip, TelemetrySlot.Weapon, "KNIFE", null));
        sink.TryWrite(TelemetryEvent.Attack(0, "KNIFE", TelemetryAttackKind.Melee, 10.5f, 11.25f));
        sink.TryWrite(TelemetryEvent.DamageDetail(1, 0, "KNIFE", null, raw: 30, absorbed: 5, helmetDur: 10));
        match.Health.ApplyDamage(1, 25, 0);
        TickUntil(match, () => false, 1);
        sink.TryWrite(TelemetryEvent.DamageDetail(1, 0, "KNIFE", null, raw: 10, absorbed: 10)); // giáp chặn hết, không ApplyDamage
        TickUntil(match, () => false, 1);
        sink.TryWrite(TelemetryEvent.LootDrop("CHEST_WOODEN", 0, 12f, 12f,
            new[] { new TelemetryLootItem("AMMO", 12), new TelemetryLootItem("ELIXIR", 1) }));
        sink.TryWrite(TelemetryEvent.ItemPickup(0, "AMMO", 12, TelemetryPickupFrom.Container, 12f, 12f));
        TickUntil(match, () => false, 40);
        sink.TryWrite(TelemetryEvent.CcSpan(1, 0, "ROPE", TelemetryCcKind.Root, start, 1500, TelemetryCcEnd.Expired));
        sink.TryWrite(TelemetryEvent.ItemUse(0, "ELIXIR", start, 2000, TelemetryUseResult.Done, 80, 100));
        sink.TryWrite(TelemetryEvent.HideSpan(0, start, "BUSH", 13f, 13f, TelemetryHideEnd.Exit, false));
        sink.TryWrite(TelemetryEvent.DamageDetail(1, 0, "KNIFE", null, raw: 200, absorbed: 0));
        match.Health.ApplyDamage(1, 200, 0);
        TickUntil(match, () => match.Phase == MatchPhase.Ending, 10);
        TickUntil(match, () => match.Phase == MatchPhase.Waiting, 200);
        host.Dispose();
        return new H7Result { Root = root, MatchId = matchId };
    }

    public static List<JsonElement> ReadLines(string path) =>
        File.ReadAllLines(path).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();

    private static string FileFor(string root, Guid matchId) =>
        Directory.GetFiles(root, $"{matchId:D}.server.*.jsonl", SearchOption.AllDirectories).Single();

    private static IEnumerable<JsonElement> OfType(List<JsonElement> lines, string type) =>
        lines.Where(l => l.GetProperty("type").GetString() == type);

    private static int? NetIdOrNull(JsonElement e, string key) =>
        e.GetProperty(key).ValueKind == JsonValueKind.Null ? null : e.GetProperty(key).GetInt32();

    /// <summary>Kiểm định dạng chung cho mọi file server: khóa theo schema, seq liên tục, trailer khớp, không có fake.</summary>
    public static void CheckFileShape(Checker c, SchemaKeys schema, string label, List<JsonElement> lines, Guid matchId)
    {
        var raw = lines.Select(l => l.GetRawText()).ToList();
        var mismatches = raw.Select(schema.Mismatch).Where(m => m != null).Take(3).ToList();
        c.Check(mismatches.Count == 0, $"{label}: every line has exactly the schema keys in order", string.Join(" | ", mismatches));
        var records = lines.Skip(1).Take(lines.Count - 2).ToList();
        c.Check(records.Select(r => r.GetProperty("seq").GetInt64()).SequenceEqual(Enumerable.Range(1, records.Count).Select(i => (long)i)),
            $"{label}: seq runs 1..{records.Count} without gaps");
        var trailer = lines[^1];
        c.Check(trailer.GetProperty("written").GetInt64() == records.Count && trailer.GetProperty("droppedQueue").GetInt64() == 0
                && !trailer.GetProperty("truncated").GetBoolean(),
            $"{label}: _trailer.written = {records.Count}, nothing dropped or truncated");
        c.Check(records.All(r => r.GetProperty("matchId").GetString() == matchId.ToString("D") && r.GetProperty("src").GetString() == "server"
                                 && r.GetProperty("cfg").GetString() == "1.0.0" && r.GetProperty("cfgHash").GetString()!.Length == 64
                                 && r.GetProperty("balHash").ValueKind == JsonValueKind.Null),
            $"{label}: envelope carries matchId, src=server, cfg, cfgHash and balHash=null");
        c.Check(raw.All(l => !l.Contains("\"fake\"")), $"{label}: real data never carries the fake flag");
    }

    public static void Run(Checker c, SchemaKeys schema)
    {
        var root = Path.Combine(Path.GetTempPath(), "tel01-flow-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            CheckFlow(c, schema, RunFlow(Path.Combine(root, "flow")));
            CheckH7(c, schema, RunH7(Path.Combine(root, "h7")));
            CheckLifecycle(c, root);
        }
        finally
        {
            CoreChecks.TryDelete(root);
        }
    }

    private static void CheckFlow(Checker c, SchemaKeys schema, FlowResult flow)
    {
        c.Section("Real MatchRuntime: two matches in one room");
        var files = Directory.GetFiles(flow.Root, "*.jsonl", SearchOption.AllDirectories);
        c.Check(files.Length == 2 && flow.MatchIds.Distinct().Count() == 2 && flow.Results.Count == 2,
            "Two consecutive matches give two files (new MatchId after Cleanup opens a new file)", $"files={files.Length}");
        c.Check(!flow.Observer.HasDisconnectHook && !flow.Observer.HasLeftHook && !flow.Observer.HasReplayHook,
            "Without H5/H6 no optional event is bound (reflection finds nothing)");

        var text = string.Concat(files.Select(File.ReadAllText));
        var leaks = new List<string>();
        foreach (var secret in new[] { "SECRET_NAME", RoomCode, "sess-secret" })
            if (text.Contains(secret, StringComparison.Ordinal)) leaks.Add(secret);
        foreach (var token in flow.Tokens)
            if (text.Contains(token.ToString("D"), StringComparison.Ordinal)) leaks.Add("resume token");
        c.Check(leaks.Count == 0 && flow.Tokens.Count > 0, "No display name, RoomCode, raw session id or resume token in any file (AC4)",
            string.Join(",", leaks.Distinct()));

        // Trận 1.
        var m1 = ReadLines(FileFor(flow.Root, flow.MatchIds[0]));
        CheckFileShape(c, schema, "match 1", m1, flow.MatchIds[0]);
        var start = OfType(m1, "match_start").ToList();
        c.Check(start.Count == 1 && start[0].GetProperty("players").GetInt32() == 4 && start[0].GetProperty("mt").GetInt32() == 0
                && start[0].GetProperty("profile").GetString() == "test",
            "match 1: one match_start, 4 players, mt = 0, profile test");
        var inGameTick = start[0].GetProperty("tick").GetUInt32();

        var spawns = OfType(m1, "player_spawn").ToList();
        c.Check(spawns.Count == 4 && spawns.Select(s => s.GetProperty("netId").GetInt32()).OrderBy(n => n).SequenceEqual(new[] { 0, 1, 2, 3 })
                && spawns.All(s => flow.Map.SpawnPoints.Any(p => MathF.Abs(p.X - s.GetProperty("x").GetSingle()) < 0.01f
                                                                && MathF.Abs(p.Y - s.GetProperty("y").GetSingle()) < 0.01f))
                && spawns.All(s => s.GetProperty("sess").GetString()!.Length == 16),
            "match 1: player_spawn = 4, at map spawn points, sess is a 16-hex hash");

        var zone = OfType(m1, "zone_phase").ToList();
        var expectedZone = new List<(int, string)>();
        for (var p = 0; p < 5; p++) expectedZone.AddRange(new[] { (p, "WARNING"), (p, "SHRINK_START"), (p, "SHRINK_END") });
        expectedZone.Add((4, "FINAL"));
        var gotZone = zone.Select(z => (z.GetProperty("phase").GetInt32(), z.GetProperty("state").GetString()!)).ToList();
        c.Check(gotZone.OrderBy(z => z).SequenceEqual(expectedZone.OrderBy(z => z)) && zone.Count == 16,
            "match 1: zone_phase has all 5 phases x WARNING/SHRINK_START/SHRINK_END plus FINAL, once each",
            string.Join(" ", gotZone.Select(z => $"{z.Item1}:{z.Item2}")));
        var startTicksOk = Enumerable.Range(0, 5).All(p =>
        {
            var rec = zone.First(z => z.GetProperty("phase").GetInt32() == p && z.GetProperty("state").GetString() == "SHRINK_START");
            var expected = flow.Zone.Intervals[p].StartSecond * flow.Profile.ZoneTimeScale * 30;
            return Math.Abs(rec.GetProperty("tick").GetUInt32() - inGameTick - expected) <= 2;
        });
        c.Check(startTicksOk, "match 1: SHRINK_START ticks match zone_schedule x zoneTimeScale (±2 ticks)");

        var elims = OfType(m1, "elimination").ToList();
        var result1 = flow.Results[0];
        var sessionByNet = new[] { Sessions[0], Sessions[1], ResumedSession, Sessions[3] };
        var winners = result1.Reason == "LastAlive" ? 1 : 0;
        c.Check(elims.Count == 4 - winners && elims.Select(e => e.GetProperty("victim").GetInt32()).Distinct().Count() == elims.Count,
            $"match 1: elimination count = eliminated players ({4 - winners}), one per victim");
        var ranksMatch = elims.All(e =>
        {
            var session = sessionByNet[e.GetProperty("victim").GetInt32()];
            return result1.Participants.Single(p => p.PlayerSessionId == session).Rank == e.GetProperty("rank").GetInt32();
        });
        c.Check(ranksMatch, "match 1: elimination.rank equals the server MatchResult rank (copied, not recomputed)");
        var p3 = elims.SingleOrDefault(e => e.GetProperty("victim").GetInt32() == 3);
        c.Check(p3.ValueKind == JsonValueKind.Object && p3.GetProperty("cause").GetString() == "DISCONNECT" && NetIdOrNull(p3, "killer") == null,
            "match 1: grace expiry gives elimination cause DISCONNECT, killer null (M8)");
        var zoneElims = elims.Where(e => e.GetProperty("victim").GetInt32() != 3).ToList();
        c.Check(zoneElims.All(e => e.GetProperty("cause").GetString() == "ZONE" && e.GetProperty("hazard").GetString() == "ZONE"
                                   && e.GetProperty("zonePhase").GetInt32() >= 0),
            "match 1: zone deaths give cause ZONE, hazard ZONE, zonePhase set");

        var end = OfType(m1, "match_end").ToList();
        c.Check(end.Count == 1 && end[0].GetProperty("reason").GetString() == result1.Reason
                && end[0].GetProperty("alive").GetInt32() == winners
                && end[0].GetProperty("durTicks").GetUInt32() == end[0].GetProperty("tick").GetUInt32() - inGameTick,
            $"match 1: match_end.reason = MatchResult.Reason ({result1.Reason}), alive and durTicks consistent");

        var sessions = OfType(m1, "session_state").ToList();
        c.Check(sessions.Count(s => s.GetProperty("kind").GetString() == "RESUMED") == 1
                && sessions.Single(s => s.GetProperty("kind").GetString() == "RESUMED").GetProperty("netId").GetInt32() == 2,
            "match 1: exactly one RESUMED (netId 2); the token rotation at match start is not a resume (X1)");
        c.Check(sessions.Count(s => s.GetProperty("kind").GetString() == "GRACE_EXPIRED") == 1
                && sessions.Single(s => s.GetProperty("kind").GetString() == "GRACE_EXPIRED").GetProperty("netId").GetInt32() == 3
                && sessions.All(s => s.GetProperty("kind").GetString() is "RESUMED" or "GRACE_EXPIRED"),
            "match 1: exactly one GRACE_EXPIRED (netId 3); no DROPPED/LEFT without H5");

        var damage = OfType(m1, "damage").ToList();
        c.Check(damage.Count(d => d.GetProperty("cause").GetString() == "DISCONNECT") == 1
                && damage.Any(d => d.GetProperty("cause").GetString() == "ZONE")
                && damage.All(d => d.GetProperty("applied").GetInt32() <= d.GetProperty("raw").GetInt32()),
            "match 1: damage has zone hits and one DISCONNECT hit; applied <= raw");

        var samples = OfType(m1, "position_sample").Where(s => s.GetProperty("netId").GetInt32() == 0)
            .Select(s => s.GetProperty("tick").GetUInt32()).ToList();
        c.Check(samples.Count > 10 && samples.Zip(samples.Skip(1), (a, b) => b - a).All(d => d == 30),
            "match 1: position_sample at 1 Hz (every 30 ticks) per living player");
        var final = zone.First(z => z.GetProperty("state").GetString() == "FINAL").GetProperty("tick").GetUInt32();
        c.Check(OfType(m1, "position_sample").Where(s => s.GetProperty("tick").GetUInt32() > final).All(s => !s.GetProperty("inZone").GetBoolean()),
            "match 1: after the zone reaches radius 0 every sample is outside the zone");

        // Trận 2.
        var m2 = ReadLines(FileFor(flow.Root, flow.MatchIds[1]));
        CheckFileShape(c, schema, "match 2", m2, flow.MatchIds[1]);
        var load = OfType(m2, "session_state").Where(s => s.GetProperty("kind").GetString() == "LOAD_TIMEOUT").ToList();
        c.Check(load.Count == 1 && load[0].GetProperty("netId").GetInt32() == 2 && load[0].GetProperty("mt").ValueKind == JsonValueKind.Null,
            "match 2: one LOAD_TIMEOUT for the player that never loaded, mt = null");
        c.Check(OfType(m2, "match_start").Single().GetProperty("players").GetInt32() == 2,
            "match 2: match_start counts only the 2 loaded players");
        c.Check(OfType(m2, "damage").Any(d => d.GetProperty("cause").GetString() == "ZONE"),
            "match 2: damage still recorded after HealthService is recreated (R3 rebind)");
        c.Check(OfType(m2, "match_end").Single().GetProperty("reason").GetString() == flow.Results[1].Reason,
            "match 2: match_end.reason = MatchResult.Reason");
    }

    private static void CheckH7(Checker c, SchemaKeys schema, H7Result h7)
    {
        c.Section("H7 factories on a real match");
        var lines = ReadLines(FileFor(h7.Root, h7.MatchId));
        CheckFileShape(c, schema, "h7", lines, h7.MatchId);
        var counts = new[] { "attack", "cc_span", "loot_drop", "item_pickup", "item_use", "equip_change", "hide_span" }
            .ToDictionary(t => t, t => OfType(lines, t).Count());
        c.Check(counts.Values.All(n => n == 1), "Each H7 type gives exactly one record",
            string.Join(",", counts.Select(kv => $"{kv.Key}={kv.Value}")));

        var damage = OfType(lines, "damage").ToList();
        var first = damage.FirstOrDefault();
        c.Check(damage.Count == 3 && first.GetProperty("weapon").GetString() == "KNIFE" && first.GetProperty("raw").GetInt32() == 30
                && first.GetProperty("applied").GetInt32() == 25 && first.GetProperty("absorbed").GetInt32() == 5
                && first.GetProperty("helmetDur").GetInt32() == 10 && first.GetProperty("armorDur").ValueKind == JsonValueKind.Null
                && first.GetProperty("attacker").GetInt32() == 0 && first.GetProperty("cause").GetString() == "WEAPON",
            "DamageDetail merges with the following EntityDamaged into ONE damage record with weapon/absorbed/durability",
            string.Join(" | ", damage.Select(d => d.GetRawText())));
        c.Check(damage.Count == 3 && damage[1].GetProperty("applied").GetInt32() == 0 && damage[1].GetProperty("absorbed").GetInt32() == 10,
            "A fully absorbed hit (detail without EntityDamaged) is still recorded with applied = 0");

        var elim = OfType(lines, "elimination").Single();
        c.Check(elim.GetProperty("victim").GetInt32() == 1 && elim.GetProperty("killer").GetInt32() == 0
                && elim.GetProperty("weapon").GetString() == "KNIFE" && elim.GetProperty("cause").GetString() == "WEAPON"
                && elim.GetProperty("hidden").GetBoolean() == false,
            "Weapon kill: elimination killer and weapon come from the last damage; hidden=false once a bush system reports");
        var end = OfType(lines, "match_end").Single();
        c.Check(end.GetProperty("reason").GetString() == "LastAlive" && end.GetProperty("winnerNetId").GetInt32() == 0,
            "match_end: LastAlive, winner netId 0");
        var loot = OfType(lines, "loot_drop").Single();
        c.Check(loot.GetProperty("items").GetArrayLength() == 2 && loot.GetProperty("items")[0].GetProperty("id").GetString() == "AMMO"
                && loot.GetProperty("items")[0].GetProperty("qty").GetInt32() == 12,
            "loot_drop items serialise as [{id, qty}]");
        var equipTick = OfType(lines, "equip_change").Single().GetProperty("tick").GetUInt32();
        c.Check(OfType(lines, "position_sample").Where(s => s.GetProperty("netId").GetInt32() == 0 && s.GetProperty("tick").GetUInt32() > equipTick)
                .All(s => s.GetProperty("weapon").GetString() == "KNIFE"),
            "position_sample.weapon follows the last WEAPON equip_change");
        var cc = OfType(lines, "cc_span").Single();
        c.Check(cc.GetProperty("endTick").GetUInt32() >= cc.GetProperty("startTick").GetUInt32() && cc.GetProperty("ccKind").GetString() == "ROOT"
                && cc.GetProperty("end").GetString() == "EXPIRED" && cc.GetProperty("by").GetString() == "ROPE",
            "cc_span carries ROOT/EXPIRED, by=ROPE, endTick >= startTick");
    }

    private static void CheckLifecycle(Checker c, string root)
    {
        c.Section("Attach lifecycle");
        var (map, zone, profile) = LoadData();
        var disabled = TelemetryHost.Disabled();
        c.Check(disabled.Attach(new MatchRuntime("R", map, zone, profile)) == null, "Disabled host: Attach is a no-op and returns null");

        var staleRoot = Path.Combine(root, "stale");
        var host = TelemetryHost.Create(new TelemetryOptions { RootPath = staleRoot }, TextWriter.Null, intervalMs: 5);
        var match = new MatchRuntime("R2", map, zone, profile, "room-stale");
        var observer = host.Attach(match, new RoomStaleProbe(() => true, TimeSpan.Zero))!;
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!observer.IsDetached && DateTime.UtcNow < deadline) Thread.Sleep(10);
        match.TryRegisterPlayer("a", "A", out _);
        match.SetPlayerReady("a");
        for (var i = 0; i < 30; i++) match.Tick(Dt);
        c.Check(observer.IsDetached && observer.Ring.Count == 0 && !observer.TryWrite(TelemetryEvent.Attack(0, "KNIFE", TelemetryAttackKind.Melee, 0, 0)),
            "An empty, quiet non-default room is detached and releases its ring");
        host.Dispose();
    }
}
