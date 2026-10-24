using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Match;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Network;
using JungleMaze.Domain.Player;
using JungleMaze.Infrastructure.Auth;
using JungleMaze.Infrastructure.Config;
using JungleMaze.Tests.App;
using LiteNetLib.Utils;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace JungleMaze.Tests.Protocol
{
    public sealed class Phase2Tests
    {
        [Test]
        public void OptionalIdentityIsSentBeforeGameplayCapability()
        {
            var bus = new EventBus(); var network = new FakeNetwork(bus);
            using var match = new MatchSession(network, network, network, bus, new PlayerRegistry());
            match.Initialize(); match.DeferHello = true;
            network.SetState(NetworkState.Joined, DisconnectReason.None); match.Tick(.1f);
            Assert.AreEqual(0, network.Sent, "Hello must not freeze the roster before identity preparation.");
            match.AttachIdentity("fixture-ticket"); Assert.IsTrue(match.IdentityTicketSent);
            Assert.AreEqual(1, network.Sent);
            match.AnnounceReady(); Assert.AreEqual(2, network.Sent);
            match.Tick(.1f); Assert.AreEqual(2, network.Sent, "Hello is announced once.");
        }
        [Test]
        public void SnapshotHotCodec_DoesNotAllocate()
        {
            var registry = new PacketRegistry();
            var writer = new NetDataWriter(true, 1024);
            var reader = new NetDataReader();
            WorldSnapshotPacket actual = default;
            registry.Subscribe<WorldSnapshotPacket>(p => actual = p);
            var packet = new WorldSnapshotPacket { MatchId = Guid.NewGuid(), ServerTick = 10, PlayerCount = 10 };
            for (var i = 0; i < 10; i++) packet.SetPlayer(i, new PlayerStatePacket { NetId = (byte)i, X = i, Y = 20, Hp = 100 });
            TestDelegate roundtrip = () => { registry.WriteFrame(writer, ref packet); reader.SetSource(writer.Data, 0, writer.Length); registry.TryReadFrame(reader); };
            roundtrip();
            Assert.That(roundtrip, Is.Not.AllocatingGCMemory());
            Assert.AreEqual(packet.MatchId, actual.MatchId);
            Assert.AreEqual(9, actual.GetPlayer(9).NetId);
        }

        [Test]
        public void SnapshotRejectsTruncationAndTooManyPlayers()
        {
            var registry = new PacketRegistry(); var writer = new NetDataWriter();
            registry.Subscribe<WorldSnapshotPacket>(_ => { });
            var packet = new WorldSnapshotPacket { MatchId = Guid.NewGuid(), PlayerCount = 1 };
            packet.SetPlayer(0, new PlayerStatePacket { Hp = 100 }); registry.WriteFrame(writer, ref packet);
            Assert.IsFalse(registry.TryReadFrame(new NetDataReader(writer.Data, 0, writer.Length - 1)));
            packet.PlayerCount = 11;
            Assert.Throws<InvalidOperationException>(() => registry.WriteFrame(writer, ref packet));
        }

        [Test]
        public void OldMatchPacketsCannotOverwriteNextRound()
        {
            var bus = new EventBus(); var network = new FakeNetwork(bus); var players = new PlayerRegistry();
            using var match = new MatchSession(network, network, network, bus, players);
            match.Initialize(); network.SetState(NetworkState.Joined, DisconnectReason.None);
            var first = Guid.NewGuid(); var second = Guid.NewGuid();
            network.Push(new MatchPhasePacket { MatchId = first, Phase = MatchPhase.InGame });
            network.Push(new MatchPhasePacket { MatchId = second, Phase = MatchPhase.InGame });
            network.Push(new ResumeTokenPacket { MatchId = second, NetId = 2, ResumeToken = Guid.NewGuid() });
            network.Push(new MatchPhasePacket { MatchId = first, Phase = MatchPhase.Ending });
            network.Push(new ZoneStatePacket { MatchId = first, CurrentRadius = 99 });
            network.Push(new MatchResultPacket { MatchId = first });
            var snapshot = new WorldSnapshotPacket { MatchId = second, ServerTick = 3, PlayerCount = 1 };
            snapshot.SetPlayer(0, new PlayerStatePacket { NetId = 2, Hp = 95 }); network.Push(snapshot);
            snapshot.ServerTick = 2; snapshot.SetPlayer(0, new PlayerStatePacket { NetId = 2, Hp = 10 }); network.Push(snapshot);
            Assert.AreEqual(second, match.MatchId); Assert.AreEqual(MatchPhase.InGame, match.Phase);
            Assert.AreEqual(95, match.OwnHp); Assert.IsFalse(match.HasZone); Assert.IsFalse(match.HasResult);
            Assert.AreEqual(1, match.Snapshots);
        }

        [TestCase("https://example.com", true)]
        [TestCase("http://127.0.0.1:5000", true)]
        [TestCase("http://192.168.1.10:5000", false)]
        [TestCase("https://user:password@example.com", false)]
        public void PersistentProofUsesSecureOrigin(string url, bool expected) => Assert.AreEqual(expected, GuestHistoryClient.IsSecureEndpoint(new Uri(url)));

        [Test]
        public void WindowsGuestProofIsEncryptedAndSeparatedByEndpoint()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) Assert.Ignore("Windows DPAPI acceptance only.");
            var directory = Path.Combine(Path.GetTempPath(), "jungle-guest-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new GuestCredentialStore(directory); var local = new Uri("http://127.0.0.1:5000");
                var proof = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                store.Save(local, new GuestCredential { ProfileId = "guest", Proof = proof });
                Assert.AreEqual(proof, new GuestCredentialStore(directory).Load(local).Proof);
                store.Save(local, new GuestCredential { ProfileId = "second-process", Proof = "another-private-credential" });
                Assert.AreEqual(proof, store.Load(local).Proof, "Concurrent startup cannot replace a persisted profile.");
                Assert.IsNull(store.Load(new Uri("https://junglemaze.duckdns.org")));
                StringAssert.DoesNotContain(proof, Encoding.UTF8.GetString(File.ReadAllBytes(Directory.GetFiles(directory)[0])));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [Test]
        public void PackagedMapHashMustMatchServer()
        {
            var source = Path.Combine(Application.streamingAssetsPath, "JungleMaze/runtime");
            using var stream = new MemoryStream();
            foreach (var name in new[] { "map_demo.json", "match_settings.json", "zone_schedule.json" })
            {
                var label = Encoding.UTF8.GetBytes(name); var bytes = Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(source, name)).Replace("\r\n", "\n"));
                stream.Write(label, 0, label.Length); stream.Write(bytes, 0, bytes.Length);
            }
            using var sha = SHA256.Create();
            var hash = BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
            var data = new ClientRuntimeData(Application.streamingAssetsPath);
            Assert.IsNull(data.Verify("1.0.0", hash)); Assert.AreEqual(4, data.Covers.Count);
            Assert.AreEqual(50, data.Bounds.MaxX); Assert.AreEqual(0, data.Bounds.MinY);
            Assert.IsNotNull(data.Verify("1.0.0", new string('0', 64)));
        }

        [Test]
        public async Task InvalidEndpointSwitchCannotReusePreviousGuestProof()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) Assert.Ignore("Windows DPAPI acceptance only.");
            var directory = Path.Combine(Path.GetTempPath(), "jungle-guest-switch-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new GuestCredentialStore(directory);
                store.Save(new Uri("http://127.0.0.1:15000"), new GuestCredential { ProfileId = "fixture", Proof = "private-fixture-credential" });
                using var history = new GuestHistoryClient(store);
                Assert.IsTrue(await history.EnsureGuestAsync("http://127.0.0.1:15000"));
                Assert.IsTrue(history.IsReady);
                Assert.IsFalse(await history.EnsureGuestAsync("http://192.168.1.10:5000"));
                Assert.IsFalse(history.IsReady);
                Assert.IsNull(await history.TicketAsync(Guid.NewGuid(), "fixture-room"));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [Test]
        public void CanonicalHashAcceptsWindowsLineEndingsAndBom()
        {
            var root = Path.Combine(Path.GetTempPath(), "jungle-data-" + Guid.NewGuid().ToString("N"));
            var destination = Path.Combine(root, "JungleMaze/runtime");
            try
            {
                Directory.CreateDirectory(destination);
                using var aggregate = new MemoryStream();
                using var sha = SHA256.Create();
                var manifest = new StringBuilder("{\"version\":\"1.0.0\",\"files\":{");
                var first = true;
                foreach (var name in new[] { "map_demo.json", "match_settings.json", "zone_schedule.json" })
                {
                    var text = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "JungleMaze/runtime", name)).Replace("\r\n", "\n");
                    var label = Encoding.UTF8.GetBytes(name); var canonical = Encoding.UTF8.GetBytes(text);
                    aggregate.Write(label, 0, label.Length); aggregate.Write(canonical, 0, canonical.Length);
                    var target = Path.Combine(destination, name);
                    File.WriteAllText(target, text.Replace("\n", "\r\n"), new UTF8Encoding(true));
                    if (!first) manifest.Append(','); first = false;
                    manifest.Append('"').Append(name).Append("\":\"").Append(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(target))).Replace("-", "").ToLowerInvariant()).Append('"');
                }
                File.WriteAllText(Path.Combine(destination, "manifest.json"), manifest.Append("}}").ToString());
                var expected = BitConverter.ToString(sha.ComputeHash(aggregate.ToArray())).Replace("-", "").ToLowerInvariant();
                Assert.IsNull(new ClientRuntimeData(root).Verify("1.0.0", expected));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
    }
}
