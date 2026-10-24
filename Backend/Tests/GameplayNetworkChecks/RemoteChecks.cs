using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using JungleBackend.Protocol.Gameplay;

internal static class RemoteChecks
{
    // Explicit local-container fixture; no automatic remote endpoint discovery or cloud management.
    public static async Task Run(string fixture, string mode)
    {
        if (mode is not ("Udp" or "Wss" or "Mixed")) throw new ArgumentException("Use Udp, Wss or Mixed.");
        var c = JsonNode.Parse(await File.ReadAllTextAsync(fixture))!;
        if (!new Uri(c["wsUrl"]!.GetValue<string>()).IsLoopback) throw new ArgumentException("Only loopback fixtures are allowed.");
        var clients = new List<MatchNetworkClient>(); var passed = new List<string>(); var clock = Stopwatch.StartNew(); double nextInput = 0;
        void Check(bool ok, string text) { if (!ok) throw new Exception(text); passed.Add(text); Console.WriteLine("PASS " + text); }
        void Pump(Func<bool> done, int timeout = 5000)
        {
            var deadline = clock.ElapsedMilliseconds + timeout;
            while (!done())
            {
                foreach (var client in clients) client.Poll();
                var batches = 0;
                while (clock.Elapsed.TotalSeconds >= nextInput && batches++ < 3)
                { foreach (var client in clients) client.Input(.1f, .1f); nextInput += 1.0 / 30; }
                if (clock.Elapsed.TotalSeconds - nextInput > .1) nextInput = clock.Elapsed.TotalSeconds;
                if (clock.ElapsedMilliseconds > deadline) throw new TimeoutException("Docker gameplay deadline");
                Thread.Sleep(1);
            }
        }
        async Task Connect(MatchNetworkClient client) => await client.ConnectAsync("127.0.0.1", c["udpPort"]!.GetValue<int>(), c["wsUrl"]!.GetValue<string>(), c["roomCode"]!.GetValue<string>());
        try
        {
            for (var i = 0; i < 10; i++)
            {
                var client = new MatchNetworkClient(mode == "Mixed" ? i % 2 == 0 ? "Udp" : "Wss" : mode) { AutoReady = false };
                clients.Add(client); await Connect(client);
            }
            Pump(() => clients.All(p => p.PlayerId != null)); Check(clients.All(p=>p.PlayerId!=null), "Ten real Docker connections joined");
            await using (var extra = new MatchNetworkClient("Udp"))
            {
                await Connect(extra); Pump(() => { extra.Poll(); return extra.Rejected != null; });
                Check(extra.Rejected == "RoomFull", "Docker eleventh session rejected RoomFull");
            }
            foreach (var client in clients) { client.AutoReady = true; client.Hello(); }
            Pump(() => clients.All(p => p.Phase == MatchPhase.InGame && p.SnapshotCount >= 5));
            var snapshots = clients.Select(p => p.SnapshotCount).ToArray(); var inputs = clients.Select(p => p.SentInputs).ToArray();
            var began = clock.Elapsed.TotalSeconds; Pump(() => clock.Elapsed.TotalSeconds - began >= 5, 7000); var seconds = clock.Elapsed.TotalSeconds - began;
            var rates = clients.Select((p, i) => new { p.Transport, snapshotHz = (p.SnapshotCount - snapshots[i]) / seconds, inputHz = (p.SentInputs - inputs[i]) / seconds, p.InvalidFrames, p.DuplicateSnapshots }).ToArray();
            Check(rates.All(p => p.inputHz >= 28.5), "Generator valid: each client sends at least 28.5Hz");
            Check(rates.All(p => p.snapshotHz >= 27 && p.InvalidFrames == 0 && p.DuplicateSnapshots == 0), "Docker gameplay snapshots and zone stream stay coherent");
            Pump(() => clients.All(p => p.Result.HasValue), 50000);
            Check(clients.All(p => p.Result!.Value.ParticipantCount == 10), "Ten Docker gameplay clients receive full result");
            var report = new { mode, passed, measuredSeconds = seconds, rates, matchId = clients[0].MatchId };
            var output = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(fixture))!, "docker-gameplay-" + mode + ".json");
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { foreach (var client in clients) await client.DisposeAsync(); }
    }
}
