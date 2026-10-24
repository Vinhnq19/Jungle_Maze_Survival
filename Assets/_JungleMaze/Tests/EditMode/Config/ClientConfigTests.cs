using System.Collections.Generic;
using System.IO;
using JungleBackend.Protocol;
using JungleMaze.App.Config;
using JungleMaze.Infrastructure.Config;
using NUnit.Framework;

namespace JungleMaze.Tests.Config
{
    public sealed class ClientConfigTests
    {
        private const string ValidJson = @"{
  ""mode"": ""LAN"",
  ""apiBaseUrl"": null,
  ""gameServerHost"": ""192.168.1.20"",
  ""gameServerPort"": 7777,
  ""authMode"": ""RoomCode"",
  ""protocolVersion"": 1,
  ""connectTimeoutMs"": 5000
}";

        private static ClientConfig Config(string host = "127.0.0.1", int port = 7777, ushort version = 1,
            int timeout = 5000, JoinAuthMode auth = JoinAuthMode.RoomCode, string api = null,
            ConnectionMode mode = ConnectionMode.Local, string transport = "Udp") =>
            new ClientConfig(mode, api, host, port, auth, version, timeout, transport);

        [Test]
        public void Validate_AcceptsLocalLanVpnHosts()
        {
            Assert.IsNull(Config("127.0.0.1").Validate());
            Assert.IsNull(Config("192.168.1.20", mode: ConnectionMode.LAN).Validate());
            Assert.IsNull(Config("100.80.1.20", mode: ConnectionMode.Online).Validate());
            Assert.IsNull(Config("junglemaze.duckdns.org", mode: ConnectionMode.Online, version: 2).Validate());
        }

        [TestCase("http://127.0.0.1")]
        [TestCase("127.0.0.1:7777")]
        [TestCase(" 127.0.0.1")]
        [TestCase("")]
        public void Validate_RejectsBadHost(string host) => Assert.IsNotNull(Config(host).Validate());

        [Test]
        public void Validate_RejectsPortVersionTimeoutTransport()
        {
            Assert.IsNotNull(Config(port: 0).Validate());
            Assert.IsNotNull(Config(port: 70000).Validate());
            Assert.IsNull(Config(version: 3).Validate());
            Assert.IsNotNull(Config(version: 4).Validate());
            Assert.IsNotNull(Config(timeout: 100).Validate());
            StringAssert.Contains("Udp", Config(transport: "Wss").Validate());
        }

        [Test]
        public void Validate_Jwt_RequiresSecureApi()
        {
            Assert.IsNotNull(Config(auth: JoinAuthMode.Jwt).Validate());
            Assert.IsNull(Config(auth: JoinAuthMode.Jwt, api: "http://127.0.0.1:5000").Validate());
            Assert.IsNull(Config(auth: JoinAuthMode.Jwt, api: "https://junglemaze.duckdns.org").Validate());
            Assert.IsNotNull(Config(auth: JoinAuthMode.Jwt, api: "http://192.168.1.20:5000", mode: ConnectionMode.LAN).Validate());
            Assert.IsNull(Config(auth: JoinAuthMode.Jwt, api: "http://100.80.1.20:5000", mode: ConnectionMode.Online).Validate());
        }

        [Test]
        public void Json_ParsesSchema()
        {
            Assert.IsTrue(ClientConfigJson.TryParse(ValidJson, "test", out var config, out var error), error);
            Assert.AreEqual(ConnectionMode.LAN, config.Mode);
            Assert.AreEqual("192.168.1.20:7777", config.Endpoint);
            Assert.IsNull(config.ApiBaseUrl);
            Assert.AreEqual("Udp", config.Transport);
        }

        [Test]
        public void Json_RejectsUnknownAndMissingKeys()
        {
            Assert.IsFalse(ClientConfigJson.TryParse(ValidJson.Replace("\"mode\"", "\"modee\""), "t", out _, out var unknown));
            StringAssert.Contains("modee", unknown);
            Assert.IsFalse(ClientConfigJson.TryParse(@"{ ""mode"": ""Local"" }", "t", out _, out var missing));
            StringAssert.Contains("thiếu", missing);
            Assert.IsFalse(ClientConfigJson.TryParse("{ not json", "t", out _, out _));
        }

        [Test]
        public void Json_RoundTripSerialize()
        {
            ClientConfigJson.TryParse(ValidJson, "a", out var config, out _);
            Assert.IsTrue(ClientConfigJson.TryParse(ClientConfigJson.Serialize(config), "b", out var again, out var error), error);
            Assert.AreEqual(config.Endpoint, again.Endpoint);
            Assert.AreEqual(config.Mode, again.Mode);
            Assert.AreEqual(config.ProtocolVersion, again.ProtocolVersion);
        }

        /// <summary>Unity đọc được đúng file NetSmokeClient đang dùng: hai bên chung một schema.</summary>
        [Test]
        public void Json_ParsesEveryNetSmokeClientProfile()
        {
            var directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Backend/NetSmokeClient"));
            var files = Directory.GetFiles(directory, "client.*.json");
            Assert.IsNotEmpty(files);
            foreach (var file in files)
                Assert.IsTrue(ClientConfigJson.TryParse(File.ReadAllText(file), file, out _, out var error), error);
        }

        [Test]
        public void Overrides_CommandLineBeatsEnvironment()
        {
            var env = new Dictionary<string, string> { ["NET_GAME_SERVER_HOST"] = "10.0.0.1", ["NET_GAME_SERVER_PORT"] = "8000" };
            var overrides = ConfigOverrides.From(new[] { "-host", "192.168.1.50", "-mode", "LAN", "-room", "ABC123" },
                key => env.TryGetValue(key, out var v) ? v : null);
            Assert.IsNull(overrides.Apply(Config(), out var result));
            Assert.AreEqual("192.168.1.50:8000", result.Endpoint);
            Assert.AreEqual(ConnectionMode.LAN, result.Mode);
            Assert.AreEqual("ABC123", overrides.RoomCode);
        }

        [Test]
        public void Overrides_BadPort_IsError()
        {
            var overrides = ConfigOverrides.From(new[] { "-port", "abc" }, null);
            Assert.IsNotNull(overrides.Apply(Config(), out _));
        }

        [Test]
        public void FileProvider_UserFileBeatsProfile_AndSaveRoundTrips()
        {
            var root = Path.Combine(Path.GetTempPath(), "jm-config-" + System.Guid.NewGuid().ToString("N"));
            var streaming = Path.Combine(root, "streaming");
            var persistent = Path.Combine(root, "persistent");
            try
            {
                Directory.CreateDirectory(Path.Combine(streaming, FileConfigProvider.ProfileFolder));
                Directory.CreateDirectory(persistent);
                File.WriteAllText(Path.Combine(streaming, FileConfigProvider.ProfileFolder, "client.local.json"),
                    ValidJson.Replace("LAN", "Local").Replace("192.168.1.20", "127.0.0.1"));
                File.WriteAllText(Path.Combine(streaming, FileConfigProvider.ProfileFolder, "client.lan.json"), ValidJson);

                var first = new FileConfigProvider(streaming, persistent, new string[0], null, null);
                Assert.AreEqual("127.0.0.1:7777", first.Current.Endpoint);
                CollectionAssert.AreEqual(new[] { "lan", "local" }, first.Profiles);

                Assert.IsNull(first.SelectProfile("lan"));
                Assert.AreEqual("192.168.1.20:7777", first.Current.Endpoint);
                Assert.IsNull(first.SaveUserConfig(first.Current.WithEndpoint("192.168.1.99", 7778, ConnectionMode.LAN, "ui")));

                var second = new FileConfigProvider(streaming, persistent, new string[0], null, null);
                Assert.AreEqual("192.168.1.99:7778", second.Current.Endpoint);
                Assert.IsNotNull(first.SelectProfile("../evil"));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
