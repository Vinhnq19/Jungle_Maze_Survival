using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using JungleBackend.Protocol.Gameplay;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

/// <summary>Explicit loopback-only integration against an isolated API/DB/server fixture. Never targets AWS.</summary>
internal static class ExternalChecks
{
    public static async Task Run(string fixturePath)
    {
        var config = JsonNode.Parse(await File.ReadAllTextAsync(fixturePath))!;
        var baseUrl = new Uri(config["api"]!.GetValue<string>());
        if (!baseUrl.IsLoopback) throw new ArgumentException("Integration checks require loopback API.");
        using var http = new HttpClient { BaseAddress = baseUrl, Timeout = TimeSpan.FromSeconds(8) };
        var passed = new List<string>();
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); passed.Add(name); Console.WriteLine("PASS " + name); }
        async Task<(int Code, JsonNode? Body)> Request(HttpMethod method, string path, object? body = null, string? jwt = null, string? guest = null, string? secret = null)
        {
            using var request = new HttpRequestMessage(method, path);
            if (body != null) request.Content = JsonContent.Create(body);
            if (jwt != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            if (guest != null) request.Headers.Add("X-Guest-Credential", guest);
            if (secret != null) request.Headers.Add("X-Match-Result-Token", secret);
            using var response = await http.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            JsonNode? value = null; try { if (!string.IsNullOrEmpty(text)) value = JsonNode.Parse(text); } catch (JsonException) { }
            return ((int)response.StatusCode, value);
        }
        var run = Guid.NewGuid().ToString("N");
        async Task<string> Account(string suffix)
        {
            var body = new { username = "p2_" + run[..12] + suffix, password = "P2_" + run + "!aA1" };
            Check((await Request(HttpMethod.Post, "/api/auth/register", body)).Code == 201, "Register fixture account " + suffix);
            var login = await Request(HttpMethod.Post, "/api/auth/login", body);
            Check(login.Code == 200, "Login fixture account " + suffix);
            return login.Body!["accessToken"]!.GetValue<string>();
        }
        var owner = await Account("a"); var other = await Account("b");
        var guest0 = await Request(HttpMethod.Post, "/api/guests", new { });
        var guest1 = await Request(HttpMethod.Post, "/api/guests", new { });
        Check(guest0.Code == 201 && guest1.Code == 201, "Issue distinct Guest profiles");
        var proof0 = guest0.Body!["credential"]!.GetValue<string>(); var proof1 = guest1.Body!["credential"]!.GetValue<string>();
        Check(proof0 != proof1, "Guest credentials are independent");
        var roomBody = new { name = "Integration", maxPlayers = 10 };
        Check((await Request(HttpMethod.Post, "/api/rooms", roomBody)).Code == 401, "Missing JWT cannot create room");
        Check((await Request(HttpMethod.Post, "/api/rooms", roomBody, jwt: "invalid")).Code == 401, "Invalid JWT cannot create room");
        var expired = new JwtSecurityToken("JungleMaze.Local", "JungleMaze.Game", new[] { new Claim("sub", "expired-fixture") },
            DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(-2),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["jwtKey"]!.GetValue<string>())), SecurityAlgorithms.HmacSha256));
        Check((await Request(HttpMethod.Post, "/api/rooms", roomBody, jwt: new JwtSecurityTokenHandler().WriteToken(expired))).Code == 401, "Expired signed JWT cannot create room");
        Check((await Request(HttpMethod.Post, "/api/rooms", roomBody, guest: proof0)).Code == 401, "Guest proof cannot create room");
        Check((await Request(HttpMethod.Get, "/api/rooms", guest: proof0)).Code == 401, "Guest proof cannot list rooms");
        var room = await Request(HttpMethod.Post, "/api/rooms", roomBody, jwt: owner);
        Check(room.Code == 201, "Valid account JWT creates room");
        var roomId = room.Body!["id"]!.GetValue<string>();
        Check((await Request(HttpMethod.Delete, "/api/rooms/" + roomId, jwt: other)).Code == 403, "Different owner cannot delete room");
        Check((await Request(HttpMethod.Delete, "/api/rooms/" + roomId, jwt: owner)).Code == 204, "Owner deletes empty room");
        var extra1 = await Request(HttpMethod.Post, "/api/rooms", roomBody, jwt: owner);
        var extra2 = await Request(HttpMethod.Post, "/api/rooms", roomBody, jwt: owner);
        Check(extra1.Code == 201 && extra2.Code == 201, "Owner can create two additional rooms");
        Check((await Request(HttpMethod.Post, "/api/rooms", roomBody, jwt: owner)).Code == 409, "Third additional room exceeds owner quota");
        await Request(HttpMethod.Post, "/api/rooms", roomBody, jwt: owner);
        Check((await Request(HttpMethod.Post, "/api/rooms", roomBody, jwt: owner)).Code == 429, "Sixth create request is rate limited");
        await Request(HttpMethod.Delete, "/api/rooms/" + extra1.Body!["id"]!.GetValue<string>(), jwt: owner);
        await Request(HttpMethod.Delete, "/api/rooms/" + extra2.Body!["id"]!.GetValue<string>(), jwt: owner);
        Check((await Request(HttpMethod.Get, "/api/me/matches", guest: "invalid")).Code == 401, "Invalid Guest proof cannot read history");
        Check((await Request(HttpMethod.Get, "/api/me/matches", guest: proof0)).Body!["total"]!.GetValue<int>() == 0, "New profile history starts empty");
        var swagger = await http.GetStringAsync("/swagger/v1/swagger.json");
        Check(swagger.Contains("/api/game-tickets") && swagger.Contains("/api/me/matches") && swagger.Contains("/api/guests"), "Swagger exposes Phase 2 APIs");
        var clients = new List<MatchNetworkClient>(); var timer = Stopwatch.StartNew();
        void Pump(Func<bool> done, int timeout = 5000)
        {
            var deadline = timer.ElapsedMilliseconds + timeout;
            while (!done()) { foreach (var client in clients) client.Poll(); if (timer.ElapsedMilliseconds > deadline) throw new TimeoutException("External match deadline"); Thread.Sleep(1); }
        }
        try
        {
            var roomCode = config["roomCode"]!.GetValue<string>();
            for (var i = 0; i < 3; i++)
            {
                var client = new MatchNetworkClient(i == 1 ? "Wss" : "Udp") { AutoReady = false }; clients.Add(client);
                await client.ConnectAsync("127.0.0.1", config["udpPort"]!.GetValue<int>(), config["wsUrl"]!.GetValue<string>(), roomCode);
            }
            Pump(() => clients.All(c => c.PlayerId != null));
            for (var i = 0; i < 3; i++)
            {
                var ticket = await Request(HttpMethod.Post, "/api/game-tickets", new { attemptId = clients[i].AttemptId, roomCode }, jwt: i == 2 ? owner : null, guest: i == 2 ? null : i == 0 ? proof0 : proof1);
                Check(ticket.Code == 200, "Verified Guest/account obtains connection-bound ticket " + i);
                clients[i].Hello(); clients[i].Attach(ticket.Body!["ticket"]!.GetValue<string>());
                clients[i].AutoReady = true; Pump(() => clients[i].OwnNetId != 255);
            }
            Pump(() => clients.All(c => c.Phase == MatchPhase.InGame));
            Pump(() => clients.All(c => c.Result.HasValue), 60000);
            var matchId = clients[0].Result!.Value.MatchId;
            Check(clients.All(c => c.Result!.Value.ParticipantCount == 3), "UDP/WS receive result while persistence endpoint is unavailable");
            var outbox = config["outbox"]!.GetValue<string>();
            Pump(() => Directory.Exists(outbox) && Directory.GetFiles(outbox, "*.json").Length > 0);
            var payloadPath = Directory.GetFiles(outbox, "*.json").Single();
            var payload = JsonNode.Parse(await File.ReadAllTextAsync(payloadPath))!;
            var secret = config["resultToken"]!.GetValue<string>();
            Check((await Request(HttpMethod.Post, "/internal/match-results", payload)).Code == 401, "Public player cannot submit rank");
            Check((await Request(HttpMethod.Post, "/internal/match-results", payload, secret: secret)).Code is 200 or 201, "Service result stores in real Mongo");
            Check((await Request(HttpMethod.Post, "/internal/match-results", payload, secret: secret)).Code == 200, "Identical result is idempotent");
            var conflict = payload.DeepClone(); conflict["participants"]![0]!["kills"] = 1;
            Check((await Request(HttpMethod.Post, "/internal/match-results", conflict, secret: secret)).Code == 409, "Same MatchId with changed result rejected");
            var duplicate = payload.DeepClone();
            duplicate["participants"]![1]!["playerSessionId"] = duplicate["participants"]![0]!["playerSessionId"]!.GetValue<string>();
            Check((await Request(HttpMethod.Post, "/internal/match-results", duplicate, secret: secret)).Code == 400, "Duplicate PlayerSessionId is rejected");
            var history0 = await Request(HttpMethod.Get, "/api/me/matches", guest: proof0);
            var history1 = await Request(HttpMethod.Get, "/api/me/matches", guest: proof1);
            Check(history0.Body!["total"]!.GetValue<int>() == 1 && history1.Body!["total"]!.GetValue<int>() == 1, "Same names remain separate verified histories, one result each");
            Check((await Request(HttpMethod.Get, "/api/me/matches", jwt: owner)).Body!["total"]!.GetValue<int>() == 1, "Verified account receives its authoritative match history");
            var board = await Request(HttpMethod.Get, "/api/matches/" + matchId + "/leaderboard");
            var serialized = board.Body!.ToJsonString();
            Check(board.Code == 200 && !serialized.Contains("profileKey", StringComparison.OrdinalIgnoreCase) && !serialized.Contains(proof0) && !serialized.Contains(proof1), "Public leaderboard excludes private identity and credential");
            Check((await Request(HttpMethod.Get, "/api/me/matches", jwt: other)).Body!["total"]!.GetValue<int>() == 0, "Unrelated account cannot read Guest results");
            var output = config["output"]!.GetValue<string>();
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { passed, matchId, clients = clients.Select(c => new { c.Transport, c.SnapshotCount, c.InvalidFrames }), outboxRetained = true }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"ALL {passed.Count} REAL API/MONGO CHECKS PASS");
        }
        finally { foreach (var client in clients) await client.DisposeAsync(); }
    }
}
