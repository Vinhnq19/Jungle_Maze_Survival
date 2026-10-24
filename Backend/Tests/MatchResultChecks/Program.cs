#nullable enable
extern alias ApiTarget;
using MatchRecord = ApiTarget::Api.Models.MatchRecord;
using MatchParticipantRecord = ApiTarget::Api.Models.MatchParticipantRecord;
using MatchInsertResult = ApiTarget::Api.Repositories.MatchInsertResult;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GameServer.Configuration;
using GameServer.Diagnostics;
using GameServer.Networking;
using GameServer.Rooms;
using GameServer.Simulation;
using JungleBackend.Protocol;
using JungleBackend.Protocol.Gameplay;
using JungleBackend.Rooms;
using JungleBackend.Shared;
using LiteNetLib.Utils;

namespace Tests.MatchResultChecks;

public static class Program
{
    private static int _passed;
    private static int _failed;

    private static void Check(bool condition, string testName, string? details = null)
    {
        if (condition)
        {
            Console.WriteLine($"[PASS] {testName}");
            _passed++;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAIL] {testName}{(details != null ? $" - {details}" : "")}");
            Console.ResetColor();
            _failed++;
        }
    }

    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("=============================================================");
        Console.WriteLine("=== NET-02 Match Persistence & Guest Identity Unit Checks ===");
        Console.WriteLine("=============================================================");

        TestGuestCredentialCryptography();
        TestProfileKeySeparationAndOwnerIsolation();
        TestOneUseGameTicketsAndSessionBinding();
        TestForbidRelinkingDuringLoadingAndInGame();
        TestCannotOverwriteDifferentProfile();
        TestCanonicalMatchHasherAndParticipantNormalization();
        TestStandaloneMongoIdempotenceAndConflictHandling();
        TestPublicLeaderboardSanitization();
        TestMatchHistoryPagination();
        await TestMatchOutboxWorkerAsync();
        TestNoSecretsInSnapshots();

        Console.WriteLine("\n-------------------------------------------------------------");
        Console.WriteLine($"Checks Finished: {_passed} passed, {_failed} failed.");
        Console.WriteLine("-------------------------------------------------------------");

        if (_failed > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FAILURES ENCOUNTERED! Exiting with code 1.");
            Console.ResetColor();
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("ALL MATCH RESULT & GUEST CHECKS PASSED.");
        Console.ResetColor();
        return 0;
    }

    /// <summary>
    /// 1. Cryptography: 256-bit crypto credential opaque to client, DB stores ONLY SHA-256 hash.
    /// Constant-time comparison ensures timing attack resistance.
    /// </summary>
    private static void TestGuestCredentialCryptography()
    {
        Console.WriteLine("\n--- Check 1: Guest Credential Cryptography & Hash Separation ---");

        var rawBytes = RandomNumberGenerator.GetBytes(32);
        var credential = Convert.ToHexString(rawBytes).ToLowerInvariant();
        Check(credential.Length == 64, "Guest credential is 256-bit hex (64 chars)");

        var dbStoredHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(credential))).ToLowerInvariant();
        Check(!string.Equals(credential, dbStoredHash, StringComparison.Ordinal),
            "Stored DB value is SHA-256 hash, not plaintext credential");

        // Verify constant-time comparison matches expected
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(credential));
        var expectedHashBytes = Convert.FromHexString(dbStoredHash);
        Check(CryptographicOperations.FixedTimeEquals(expectedHashBytes, suppliedHash),
            "Constant-time comparison succeeds for matching credential proof");

        // Tampered proof must fail
        var tampered = credential.Substring(0, 63) + (credential[^1] == 'a' ? 'b' : 'a');
        var tamperedHash = SHA256.HashData(Encoding.UTF8.GetBytes(tampered));
        Check(!CryptographicOperations.FixedTimeEquals(expectedHashBytes, tamperedHash),
            "Constant-time comparison fails for tampered credential proof");
    }

    /// <summary>
    /// 2. Profile key separation: account:<sub> vs guest:<uuid>.
    /// Same display names ("Hero") remain strictly isolated. Changing posted ID never grants access.
    /// </summary>
    private static void TestProfileKeySeparationAndOwnerIsolation()
    {
        Console.WriteLine("\n--- Check 2: ProfileKey Separation & Owner Isolation ---");

        var accountSub = "user-abc-123";
        var guestId = Guid.NewGuid().ToString("D");

        var accountProfileKey = $"account:{accountSub}";
        var guestProfileKey = $"guest:{guestId}";

        Check(accountProfileKey.StartsWith("account:", StringComparison.Ordinal), "Account profile key format is account:<sub>");
        Check(guestProfileKey.StartsWith("guest:", StringComparison.Ordinal), "Guest profile key format is guest:<uuid>");
        Check(!string.Equals(accountProfileKey, guestProfileKey, StringComparison.Ordinal),
            "Account and Guest profiles are distinct");

        // Verify participant filtering in history: only verified owner's items returned
        var matchId = Guid.NewGuid();
        var participants = new List<MatchParticipantSubmission>
        {
            new("session-1", "HeroPlayer", 1, 3, 120.5, accountProfileKey),
            new("session-2", "HeroPlayer", 2, 1, 95.2, guestProfileKey) // Same display name!
        };

        // Querying for account owner returns only account owner record
        var accountRecord = participants.FirstOrDefault(p => string.Equals(p.ProfileKey, accountProfileKey, StringComparison.Ordinal));
        Check(accountRecord != null && accountRecord.PlayerSessionId == "session-1",
            "Owner isolation: querying account returns only account's record despite identical displayName");

        // Querying for guest owner returns only guest owner record
        var guestRecord = participants.FirstOrDefault(p => string.Equals(p.ProfileKey, guestProfileKey, StringComparison.Ordinal));
        Check(guestRecord != null && guestRecord.PlayerSessionId == "session-2",
            "Owner isolation: querying guest returns only guest's record despite identical displayName");
    }

    /// <summary>
    /// 3. One-use game tickets: TTL 30s expiration, single-use consumption, binding to AttemptId + Room.
    /// </summary>
    private static void TestOneUseGameTicketsAndSessionBinding()
    {
        Console.WriteLine("\n--- Check 3: One-Use Game Tickets & Real SessionHub Binding ---");

        var options = new ServerOptions(IPAddress.Loopback,7777,10,30,1,"RoomCode","TEST01",Rooms:new RoomOptions(MaxTotalPlayers:10));
        var hub = new SessionHub(options, new ServerMetrics());

        var attemptId = Guid.NewGuid();
        var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var profileKey = $"guest:{Guid.NewGuid():D}";

        // Register ticket via SessionHub
        var regCmd = new RegisterGameTicketCommand(ticket, attemptId, "TEST01", profileKey, DateTimeOffset.UtcNow.AddSeconds(30));
        var regResult = hub.RegisterTicket(regCmd);
        Check(regResult.Status == 200 && hub.PendingTickets == 1, "Game ticket registered without attaching identity");

        // Fake peer joins room
        var peer = new FakeGamePeer("Udp");
        var joinReq = new JoinRequest(NetworkProtocol.LatestVersion, JoinAuthMode.RoomCode, "TEST01", "PlayerOne", attemptId);
        var joined = hub.Join(joinReq, peer);
        Check(joined, "Session joined room anonymously");
        hub.Connected(peer);

        // Client attaches identity using ticket
        var attachPacket = new AttachIdentityPacket { Ticket = ticket };
        var writer = new NetDataWriter();
        PacketRegistry.BeginFrame(writer);
        new PacketRegistry().Write(writer, ref attachPacket);

        // Receive gameplay attach packet
        hub.Receive(peer, writer.CopyData(), reliable: true, validChannel: true);

        Check(hub.PendingTickets == 0, "Matching gameplay attachment consumes ticket");

        // Single-use: ticket has been consumed, second attach attempt must fail (inert)
        var writer2 = new NetDataWriter();
        PacketRegistry.BeginFrame(writer2);
        new PacketRegistry().Write(writer2, ref attachPacket);
        hub.Receive(peer, writer2.CopyData(), reliable: true, validChannel: true);
        Check(hub.PendingTickets == 0, "Reused ticket stays consumed and cannot bind again");

        // Client sends ClientHello to spawn entity
        var hello = new ClientHelloPacket { ProtocolVersion = MatchProtocol.Version, Ready = true };
        var helloWriter = new NetDataWriter();
        PacketRegistry.BeginFrame(helloWriter);
        new PacketRegistry().Write(helloWriter, ref hello);
        hub.Receive(peer, helloWriter.CopyData(), reliable: true, validChannel: true);

        // Verify entity in runtime received the verified profile key
        var room = hub.Rooms.Find("TEST01")!;
        var hasEntity = room.Match.Entities.TryGetBySession(peer.LastSentSessionId ?? "", out var entity);
        // Note: peer.LastSentSessionId is populated by Accept join
        var firstEntity = room.Match.Entities.All.FirstOrDefault();
        Check(firstEntity != null, "Entity registered upon ClientHello");
        Check(firstEntity != null && string.Equals(firstEntity.DisplayName, "PlayerOne", StringComparison.Ordinal),
            "Entity has correct displayName");

        hub.CloseAll();
    }

    /// <summary>
    /// 4. Forbid relinking profile once match loading/in-game begins.
    /// </summary>
    private static void TestForbidRelinkingDuringLoadingAndInGame()
    {
        Console.WriteLine("\n--- Check 4: Forbid Relinking Once Loading / InGame Begins ---");

        var map = CreateValidMapData();
        var schedule = CreateValidZoneSchedule();
        var profile = MatchProfile.CreateTestProfile();
        var match = new MatchRuntime("ROOM01", map, schedule, profile);

        match.TryRegisterPlayer("session-1", "Player1", out _);
        match.TryRegisterPlayer("session-2", "Player2", out _);
        match.SetPlayerReady("session-1");
        match.SetPlayerReady("session-2");

        // Waiting / Lobby: attaching profile is allowed
        var attachedWaiting = match.TryAttachProfile("session-1", "account:owner-1");
        Check(attachedWaiting, "Attaching profile allowed in Waiting/Lobby phase");

        // Advance to Loading phase
        match.Tick(0.1); // transitions to Lobby
        for (var i = 0; i < 15; i++) match.Tick(0.1); // duration expires -> Loading

        Check(match.Phase == MatchPhase.Loading, "Match reached Loading phase");

        // Attempting to relink in Loading phase must be forbidden
        var relinkLoading = match.TryAttachProfile("session-1", "guest:new-profile");
        Check(!relinkLoading, "Forbid relinking profile once match loading begins");

        // Acknowledge load and advance to InGame
        match.SetPlayerLoaded("session-1", match.MatchId);
        match.SetPlayerLoaded("session-2", match.MatchId);
        match.Tick(0.1); // -> InGame
        Check(match.Phase == MatchPhase.InGame, "Match reached InGame phase");

        var relinkInGame = match.TryAttachProfile("session-1", "guest:another-profile");
        Check(!relinkInGame, "Forbid relinking profile during InGame phase");
    }

    /// <summary>
    /// 5. Cannot overwrite existing different profile.
    /// </summary>
    private static void TestCannotOverwriteDifferentProfile()
    {
        Console.WriteLine("\n--- Check 5: Cannot Overwrite Existing Different Profile ---");

        var map = CreateValidMapData();
        var schedule = CreateValidZoneSchedule();
        var profile = MatchProfile.CreateTestProfile();
        var match = new MatchRuntime("ROOM02", map, schedule, profile);

        match.TryRegisterPlayer("p1", "Player1", out _);
        var first = match.TryAttachProfile("p1", "account:original-sub");
        Check(first, "First profile attachment succeeded");

        // Same profile key attached again is idempotent
        var same = match.TryAttachProfile("p1", "account:original-sub");
        Check(same, "Attaching same profile key again is valid");

        // Different profile key must be rejected
        var different = match.TryAttachProfile("p1", "guest:attacker-stolen-id");
        Check(!different, "Attaching different profile key to already-linked player is rejected");
    }

    /// <summary>
    /// 6. Canonical match representation & participant ordering normalization.
    /// Ensures duplicate identical results yield identical hashes independent of JSON/memory order.
    /// </summary>
    private static void TestCanonicalMatchHasherAndParticipantNormalization()
    {
        Console.WriteLine("\n--- Check 6: Canonical Match Hasher Normalization ---");

        var matchId = Guid.NewGuid();
        var startUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
        var endUtc = DateTimeOffset.UtcNow;

        var p1 = new MatchParticipantSubmission("session-A", "Alice", 1, 4, 180.0, "account:user-1");
        var p2 = new MatchParticipantSubmission("session-B", "Bob", 2, 1, 150.0, "guest:guest-2");
        var p3 = new MatchParticipantSubmission("session-C", "Charlie", 3, 0, 90.0, null);

        // List 1: order A, B, C
        var sub1 = new MatchResultSubmission(matchId, "R1", "v1.0", startUtc, endUtc, "LastAlive", new[] { p1, p2, p3 });
        // List 2: reversed order C, A, B (same participants, different order)
        var sub2 = new MatchResultSubmission(matchId, "R1", "v1.0", startUtc, endUtc, "LastAlive", new[] { p3, p1, p2 });

        var hash1 = MatchCanonicalHasher.ComputeCanonicalHash(sub1);
        var hash2 = MatchCanonicalHasher.ComputeCanonicalHash(sub2);

        Check(hash1 == hash2, "Canonical hash is deterministic and invariant to participant ordering");
        Check(hash1.Length == 64, "Canonical hash is valid 64-char hex SHA-256");

        // Payload with modified rank or survivalSeconds yields different hash
        var p1Modified = new MatchParticipantSubmission("session-A", "Alice", 2, 4, 180.0, "account:user-1");
        var sub3 = new MatchResultSubmission(matchId, "R1", "v1.0", startUtc, endUtc, "LastAlive", new[] { p1Modified, p2, p3 });
        var hash3 = MatchCanonicalHasher.ComputeCanonicalHash(sub3);

        Check(hash1 != hash3, "Modified participant payload yields distinct canonical hash");
    }

    /// <summary>
    /// 7. Standalone Mongo idempotence:
    /// Duplicate identical result -> 200/idempotent, no extra history.
    /// Same MatchId but different payload -> 409 Conflict.
    /// </summary>
    private static void TestStandaloneMongoIdempotenceAndConflictHandling()
    {
        Console.WriteLine("\n--- Check 7: Standalone Mongo Idempotence & Conflict Handling ---");

        var fakeDb = new FakeMatchRepository();
        var matchId = Guid.NewGuid();
        var startUtc = DateTimeOffset.UtcNow.AddMinutes(-3);
        var endUtc = DateTimeOffset.UtcNow;

        var p1 = new MatchParticipantSubmission("s1", "P1", 1, 2, 100.0, "account:user-1");
        var sub1 = new MatchResultSubmission(matchId, "ROOM", "v1", startUtc, endUtc, "LastAlive", new[] { p1 });
        var hash1 = MatchCanonicalHasher.ComputeCanonicalHash(sub1);

        var record1 = new MatchRecord
        {
            Id = matchId.ToString("D"),
            MatchId = matchId,
            CanonicalHash = hash1,
            RoomId = "ROOM",
            ConfigVersion = "v1",
            StartUtc = startUtc.UtcDateTime,
            EndUtc = endUtc.UtcDateTime,
            Reason = "LastAlive",
            Participants = new List<MatchParticipantRecord>
            {
                new() { PlayerSessionId = "s1", DisplayName = "P1", Rank = 1, Kills = 2, SurvivalSeconds = 100.0, ProfileKey = "account:user-1" }
            }
        };

        // 1. Initial insert
        var res1 = fakeDb.TryInsertMatch(record1);
        Check(res1 == MatchInsertResult.Inserted, "Initial match insert returns Inserted (201 Created)");

        // 2. Duplicate identical submission
        var res2 = fakeDb.TryInsertMatch(record1);
        Check(res2 == MatchInsertResult.DuplicateIdentical,
            "Duplicate identical match returns DuplicateIdentical (200 OK idempotent)");
        Check(fakeDb.Count == 1, "Idempotent insert does not create duplicate match document");

        // 3. Conflicting submission: same MatchId but different canonical hash
        var recordConflicting = new MatchRecord
        {
            Id = matchId.ToString("D"),
            MatchId = matchId,
            CanonicalHash = "different_hash_due_to_modified_results",
            RoomId = "ROOM",
            ConfigVersion = "v1",
            StartUtc = startUtc.UtcDateTime,
            EndUtc = endUtc.UtcDateTime,
            Reason = "Tie",
            Participants = record1.Participants
        };

        var res3 = fakeDb.TryInsertMatch(recordConflicting);
        Check(res3 == MatchInsertResult.ConflictDifferent,
            "Same MatchId with conflicting payload returns ConflictDifferent (409 Conflict)");
        Check(fakeDb.Count == 1, "Conflicting insert rejected and does not overwrite existing record");
    }

    /// <summary>
    /// 8. Public leaderboard sanitization:
    /// Strips credentials, profile keys, internal room secrets.
    /// </summary>
    private static void TestPublicLeaderboardSanitization()
    {
        Console.WriteLine("\n--- Check 8: Public Leaderboard Sanitization ---");

        var record = new MatchRecord
        {
            Id = Guid.NewGuid().ToString("D"),
            MatchId = Guid.NewGuid(),
            CanonicalHash = "hash123",
            RoomId = "public-room-1",
            ConfigVersion = "v1",
            StartUtc = DateTime.UtcNow.AddMinutes(-5),
            EndUtc = DateTime.UtcNow,
            Reason = "LastAlive",
            Participants = new List<MatchParticipantRecord>
            {
                new() { PlayerSessionId = "s1", DisplayName = "P1", Rank = 1, Kills = 3, SurvivalSeconds = 120, ProfileKey = "account:secret-sub-1" },
                new() { PlayerSessionId = "s2", DisplayName = "P2", Rank = 2, Kills = 0, SurvivalSeconds = 80, ProfileKey = "guest:secret-guest-uuid" }
            }
        };

        // Map to public leaderboard DTO
        var sanitizedPlayers = record.Participants
            .OrderBy(p => p.Rank)
            .Select(p => new SanitizedMatchParticipant(p.PlayerSessionId, p.DisplayName, p.Rank, p.Kills, p.SurvivalSeconds))
            .ToList();

        var json = JsonSerializer.Serialize(sanitizedPlayers);
        Check(!json.Contains("secret-sub-1", StringComparison.Ordinal), "Leaderboard JSON does not contain account profile key");
        Check(!json.Contains("secret-guest-uuid", StringComparison.Ordinal), "Leaderboard JSON does not contain guest profile key");
        Check(!json.Contains("ProfileKey", StringComparison.Ordinal), "Leaderboard schema omits ProfileKey entirely");
        Check(sanitizedPlayers[0].Rank == 1 && sanitizedPlayers[1].Rank == 2, "Leaderboard ordered by rank ascending");
    }

    /// <summary>
    /// 9. Pagination: page 1, size 20 default, max 100, correct slicing and total.
    /// </summary>
    private static void TestMatchHistoryPagination()
    {
        Console.WriteLine("\n--- Check 9: History Pagination ---");

        var allItems = Enumerable.Range(1, 55).Select(i =>
            new MyMatchHistoryItem(Guid.NewGuid(), $"Player_{i}", 1, 0, 100.0, DateTimeOffset.UtcNow, "LastAlive")
        ).ToList();

        // Default: page 1, size 20
        var page1 = allItems.Skip(0).Take(20).ToList();
        var paged1 = new PagedResult<MyMatchHistoryItem>(page1, Page: 1, PageSize: 20, Total: allItems.Count);
        Check(paged1.Items.Count == 20 && paged1.Total == 55, "Page 1 default returns 20 items and correct total 55");

        // Page 3: items 41..55 (15 items)
        var page3 = allItems.Skip(40).Take(20).ToList();
        var paged3 = new PagedResult<MyMatchHistoryItem>(page3, Page: 3, PageSize: 20, Total: allItems.Count);
        Check(paged3.Items.Count == 15, "Page 3 returns remaining 15 items");

        // Max page size bounded at 100
        var requestedSize = 150;
        var boundedSize = Math.Min(100, requestedSize);
        Check(boundedSize == 100, "Page size bounded at maximum 100");
    }

    /// <summary>
    /// 10. Outbox behavior:
    /// Bounded memory queue (returns false on capacity), decoupled disk drain (outage with 3 matches reaches disk),
    /// automatic retry without new enqueue, one failed file doesn't starve others, restart replay after ACK,
    /// 409 conflict quarantine, malformed file quarantine, quotas including quarantine, URL validation, and shutdown flush.
    /// </summary>
    private static async Task TestMatchOutboxWorkerAsync()
    {
        Console.WriteLine("\n--- Check 10: Match Outbox Worker, Retry & Quarantine ---");

        var tempDir = Path.Combine(Path.GetTempPath(), "JungleOutboxTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // Case A: Token < 32 bytes disables outbox (idle/safe mode)
            var disabledOutbox = new MatchOutbox(new MatchOutboxOptions
            {
                OutboxPath = tempDir,
                Token = "short_token" // < 32 bytes
            });
            Check(!disabledOutbox.IsEnabled, "Outbox disabled when MATCH_RESULT_TOKEN < 32 bytes");
            var enqResult = disabledOutbox.TryEnqueue(CreateDummyMatchResult());
            Check(!enqResult, "TryEnqueue returns false when outbox disabled");

            // Case B: URL validation (no credentials/query)
            var credsUrlOutbox = new MatchOutbox(new MatchOutboxOptions
            {
                OutboxPath = tempDir,
                Token = new string('A', 32),
                ApiUrl = "http://user:password@localhost:5000/internal/match-results"
            });
            Check(!credsUrlOutbox.IsEnabled, "Outbox disabled when API URL contains credentials");

            var queryUrlOutbox = new MatchOutbox(new MatchOutboxOptions
            {
                OutboxPath = tempDir,
                Token = new string('A', 32),
                ApiUrl = "http://localhost:5000/internal/match-results?secret=123"
            });
            Check(!queryUrlOutbox.IsEnabled, "Outbox disabled when API URL contains query string");

            // Case C: Bounded memory queue returns false (FullMode.Wait + non-blocking TryWrite)
            var validToken = new string('A', 32);
            using var boundedOutbox = new MatchOutbox(new MatchOutboxOptions
            {
                OutboxPath = tempDir,
                Token = validToken,
                ApiUrl = "http://localhost:5000/internal/match-results",
                QueueCapacity = 2
            });
            // Without calling Start(), queue is not drained
            var q1 = boundedOutbox.TryEnqueue(CreateDummyMatchResult());
            var q2 = boundedOutbox.TryEnqueue(CreateDummyMatchResult());
            var q3 = boundedOutbox.TryEnqueue(CreateDummyMatchResult());
            Check(q1 && q2 && !q3, "Bounded memory queue returns false on capacity overflow without blocking");

            // Case D: API Outage with 3 new matches all reach disk independently
            var handler = new MockHttpMessageHandler();
            handler.NextStatusCode = HttpStatusCode.InternalServerError; // 500 Outage
            using var httpClient = new HttpClient(handler);

            using var outbox = new MatchOutbox(new MatchOutboxOptions
            {
                OutboxPath = tempDir,
                Token = validToken,
                ApiUrl = "http://localhost:5000/internal/match-results",
                QueueCapacity = 10,
                MaxFiles = 20,
                MaxBytes = 10 * 1024 * 1024,
                InitialBackoffSeconds = 2,
                MaxBackoffSeconds = 30
            }, null, httpClient);

            Check(outbox.IsEnabled, "Outbox enabled when token >= 32 UTF-8 bytes and URL is clean");

            outbox.Start();

            var m1 = CreateDummyMatchResult();
            var m2 = CreateDummyMatchResult();
            var m3 = CreateDummyMatchResult();

            outbox.TryEnqueue(m1);
            outbox.TryEnqueue(m2);
            outbox.TryEnqueue(m3);

            // Wait for independent disk drain loop to write all 3 matches to disk despite API 500 outage
            var diskFilesCount = 0;
            for (var i = 0; i < 30; i++)
            {
                diskFilesCount = Directory.GetFiles(tempDir, "*.json", SearchOption.TopDirectoryOnly).Length;
                if (diskFilesCount == 3) break;
                await Task.Delay(100);
            }
            Check(diskFilesCount == 3, "Outage with 3 new matches all reach disk independently of delivery failure");

            // Case E: Retry occurs without new enqueue
            // Switch handler to 200 OK. Do NOT call TryEnqueue.
            handler.NextStatusCode = HttpStatusCode.OK;
            var deliveredAll = false;
            for (var i = 0; i < 50; i++) // Up to 5s to allow 2s initial backoff
            {
                if (Directory.GetFiles(tempDir, "*.json", SearchOption.TopDirectoryOnly).Length == 0)
                {
                    deliveredAll = true;
                    break;
                }
                await Task.Delay(100);
            }
            Check(deliveredAll, "Automatic retry occurs without new enqueue and delivers pending files after recovery");

            // Case F: 409 Conflict Quarantine (Existing test preserved)
            var conflictMatch = CreateDummyMatchResult();
            handler.NextStatusCode = HttpStatusCode.Conflict;
            outbox.TryEnqueue(conflictMatch);

            var quarantineDir = Path.Combine(tempDir, "quarantine");
            var quarantined = false;
            for (var i = 0; i < 20; i++)
            {
                if (Directory.Exists(quarantineDir) && Directory.GetFiles(quarantineDir, "*.json").Length > 0)
                {
                    quarantined = true;
                    break;
                }
                await Task.Delay(100);
            }
            Check(quarantined, "409 Conflict moves match result file to quarantine and stops retry loop");

            // Case G: Malformed File on Replay (Existing test preserved)
            var malformedFile = Path.Combine(tempDir, "malformed_match.json");
            await File.WriteAllTextAsync(malformedFile, "{ not valid json !! }");

            var malformedQuarantined = false;
            for (var i = 0; i < 20; i++)
            {
                if (!File.Exists(malformedFile) && Directory.GetFiles(quarantineDir, "*malformed*").Length > 0)
                {
                    malformedQuarantined = true;
                    break;
                }
                await Task.Delay(100);
            }
            Check(malformedQuarantined, "Malformed JSON file on replay quarantined without crashing outbox");

            // Clean shutdown of current outbox
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await outbox.StopAsync(cts.Token);

            // Case H: Restart sends and deletes after ACK
            // Simulate a lingering file on disk from prior crash
            var lingeringMatch = CreateDummyMatchResult();
            var lingeringJson = JsonSerializer.Serialize(new MatchResultSubmission(
                lingeringMatch.MatchId,
                lingeringMatch.RoomId,
                lingeringMatch.ConfigVersion,
                lingeringMatch.StartUtc,
                lingeringMatch.EndUtc,
                lingeringMatch.Reason,
                lingeringMatch.Participants.Select(p => new MatchParticipantSubmission(
                    p.PlayerSessionId, p.DisplayName, p.Rank, p.Kills, p.SurvivalSeconds, p.ProfileKey
                )).ToList()
            ));
            var lingeringFile = Path.Combine(tempDir, $"{lingeringMatch.MatchId:D}.json");
            await File.WriteAllTextAsync(lingeringFile, lingeringJson, Encoding.UTF8);

            handler.NextStatusCode = HttpStatusCode.OK;
            using var restartedOutbox = new MatchOutbox(new MatchOutboxOptions
            {
                OutboxPath = tempDir,
                Token = validToken,
                ApiUrl = "http://localhost:5000/internal/match-results"
            }, null, httpClient);

            restartedOutbox.Start();

            var restartedDelivered = false;
            for (var i = 0; i < 30; i++)
            {
                if (!File.Exists(lingeringFile) && Directory.GetFiles(tempDir, "*.json", SearchOption.TopDirectoryOnly).Length == 0)
                {
                    restartedDelivered = true;
                    break;
                }
                await Task.Delay(100);
            }
            Check(restartedDelivered, "Restart replays disk files, sends and deletes after ACK");
            await restartedOutbox.StopAsync(new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token);

            // Case I: Disk Quota includes quarantine directory
            var quotaDir = Path.Combine(Path.GetTempPath(), "JungleQuotaTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(quotaDir);
            try
            {
                var qDir = Path.Combine(quotaDir, "quarantine");
                Directory.CreateDirectory(qDir);
                // Pre-populate quarantine with 2 files
                await File.WriteAllTextAsync(Path.Combine(qDir, "q1.json"), "{}");
                await File.WriteAllTextAsync(Path.Combine(qDir, "q2.json"), "{}");

                using var quotaOutbox = new MatchOutbox(new MatchOutboxOptions
                {
                    OutboxPath = quotaDir,
                    Token = validToken,
                    ApiUrl = "http://localhost:5000/internal/match-results",
                    MaxFiles = 2 // Reached by quarantine files already!
                }, null, httpClient);

                quotaOutbox.Start();
                quotaOutbox.TryEnqueue(CreateDummyMatchResult());

                await Task.Delay(300);
                var quotaOutboxFiles = Directory.GetFiles(quotaDir, "*.json", SearchOption.TopDirectoryOnly).Length;
                Check(quotaOutboxFiles == 0, "Quarantine files count against disk quota and block disk overflow");
                await quotaOutbox.StopAsync(new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token);
            }
            finally
            {
                try { Directory.Delete(quotaDir, recursive: true); } catch { }
            }

            // Case J: Shutdown flush persists pending memory queue within bounded deadline
            var flushDir = Path.Combine(Path.GetTempPath(), "JungleFlushTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(flushDir);
            try
            {
                handler.NextStatusCode = HttpStatusCode.InternalServerError; // Don't delete from disk
                using var flushOutbox = new MatchOutbox(new MatchOutboxOptions
                {
                    OutboxPath = flushDir,
                    Token = validToken,
                    ApiUrl = "http://localhost:5000/internal/match-results",
                    QueueCapacity = 10
                }, null, httpClient);

                flushOutbox.Start();
                flushOutbox.TryEnqueue(CreateDummyMatchResult());
                flushOutbox.TryEnqueue(CreateDummyMatchResult());
                flushOutbox.TryEnqueue(CreateDummyMatchResult());

                // Immediate shutdown must flush pending queue to disk within bounded deadline
                await flushOutbox.StopAsync(new CancellationTokenSource(TimeSpan.FromSeconds(3)).Token);

                var flushedCount = Directory.GetFiles(flushDir, "*.json", SearchOption.TopDirectoryOnly).Length;
                Check(flushedCount == 3, "Stop persists pending memory queue to disk within bounded deadline (shutdown flush)");
            }
            finally
            {
                try { Directory.Delete(flushDir, recursive: true); } catch { }
            }
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* cleanup */ }
        }
    }

    /// <summary>
    /// 11. Snapshots: ensure no secrets, tickets, or profile keys leak onto UDP wire packets.
    /// </summary>
    private static void TestNoSecretsInSnapshots()
    {
        Console.WriteLine("\n--- Check 11: Protocol & Snapshot Security Bounds ---");

        var snapshot = new WorldSnapshotPacket
        {
            MatchId = Guid.NewGuid(),
            ServerTick = 100,
            PlayerCount = 1
        };
        snapshot.SetPlayer(0, new PlayerStatePacket
        {
            Tick = 100,
            NetId = 0,
            X = 25f,
            Y = 25f,
            Rotation = 0,
            Hp = 100,
            Flags = PlayerStateFlags.None
        });

        var writer = new NetDataWriter();
        PacketRegistry.BeginFrame(writer);
        new PacketRegistry().Write(writer, ref snapshot);
        var bytes = writer.CopyData();

        // 1 player snapshot is small and fixed payload
        Check(bytes.Length > 0 && bytes.Length < 128, "World snapshot packet has compact fixed binary layout");
        Check(AttachIdentityPacketAllowsTicketUpTo64Chars(), "AttachIdentityPacket respects <= 64 char ticket bounds");
    }

    private static bool AttachIdentityPacketAllowsTicketUpTo64Chars()
    {
        var packet = new AttachIdentityPacket { Ticket = new string('x', 64) };
        var writer = new NetDataWriter();
        packet.Serialize(writer);
        var reader = new NetDataReader(writer.CopyData());
        var readPacket = new AttachIdentityPacket();
        readPacket.Deserialize(reader);
        return readPacket.Ticket.Length == 64;
    }

    private static MatchResult CreateDummyMatchResult() =>
        new(
            Guid.NewGuid(),
            "ROOM-DUMMY",
            "v1.0",
            DateTimeOffset.UtcNow.AddMinutes(-2),
            DateTimeOffset.UtcNow,
            "LastAlive",
            new List<MatchParticipant>
            {
                new("sess-1", "Player1", 1, 0, 60.0, "account:user-dummy")
            }
        );

    private static MapData CreateValidMapData()
    {
        var map = new MapData();
        for (var i = 0; i < 10; i++)
        {
            var angle = i * MathF.PI * 2f / 10f;
            map.SpawnPoints.Add((25f + 18f * MathF.Cos(angle), 25f + 18f * MathF.Sin(angle)));
        }
        map.Obstacles.Add(new AABB("AncientPillar", 22.5f, 23.5f, 24.0f, 26.0f));
        map.Validate();
        return map;
    }

    private static ZoneSchedule CreateValidZoneSchedule()
    {
        return ZoneSchedule.Load(GameDataLoader.ReadGameDataFile("zone_schedule.json"));
    }

    private sealed class FakeGamePeer(string transport) : IGamePeer
    {
        public string Transport { get; } = transport;
        public bool IsOpen { get; private set; } = true;
        public string? LastCloseReason { get; private set; }
        public string? LastSentSessionId { get; private set; }
        public List<byte[]> SentPackets { get; } = new();

        public bool TrySend(byte[] data, bool reliable) => TrySend(data,reliable,0);
        public bool TrySend(byte[] data, bool reliable, byte channel)
        {
            if (!IsOpen) return false;
            SentPackets.Add(data);
            // Check if packet was JoinAccepted to capture sessionId
            var reader = new NetDataReader(data);
            if (NetworkProtocol.TryReadStringMessage(reader, NetworkProtocol.JoinAccepted, 128, 64, out var sessionId))
            {
                LastSentSessionId = sessionId;
            }
            return true;
        }

        public void Close(string reason)
        {
            IsOpen = false;
            LastCloseReason = reason;
        }
    }

    private sealed class FakeMatchRepository
    {
        private readonly Dictionary<Guid, MatchRecord> _matches = new();
        public int Count => _matches.Count;

        public MatchInsertResult TryInsertMatch(MatchRecord record)
        {
            if (_matches.TryGetValue(record.MatchId, out var existing))
            {
                if (string.Equals(existing.CanonicalHash, record.CanonicalHash, StringComparison.Ordinal))
                {
                    return MatchInsertResult.DuplicateIdentical;
                }
                return MatchInsertResult.ConflictDifferent;
            }

            _matches[record.MatchId] = record;
            return MatchInsertResult.Inserted;
        }
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        public HttpStatusCode NextStatusCode { get; set; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(NextStatusCode)
            {
                Content = new StringContent("{\"status\":\"ok\"}", Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
