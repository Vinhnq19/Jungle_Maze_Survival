using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using JungleMaze.App.Match;
using JungleMaze.App.Ports;
using Newtonsoft.Json.Linq;

namespace JungleMaze.Infrastructure.Config
{
    /// <summary>Validate the packaged copy against both its manifest and the server's canonical hash before LoadingAck.</summary>
    public sealed class ClientRuntimeData : IClientRuntimeData
    {
        private readonly string _directory;
        public IReadOnlyList<RuntimeCover> Covers { get; private set; } = Array.Empty<RuntimeCover>();
        public RuntimeMapBounds Bounds { get; private set; }
        public float PlayerSpeed { get; private set; }
        public float PlayerRadius { get; private set; }
        public ZoneSchedule ZoneSchedule { get; private set; }

        public ClientRuntimeData(string streamingAssets)
        {
            _directory = Path.Combine(streamingAssets, "JungleMaze", "runtime");
        }

        public string Verify(string expectedVersion, string expectedHash)
        {
            try
            {
                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(_directory, "manifest.json")));
                if ((string)manifest["version"] != expectedVersion) return "Dữ liệu trận khác phiên bản server; cập nhật client.";
                using (var aggregate = SHA256.Create())
                using (var stream = new MemoryStream())
                {
                    foreach (var name in new[] { "map_demo.json", "match_settings.json", "zone_schedule.json" })
                    {
                        var bytes = File.ReadAllBytes(Path.Combine(_directory, name));
                        using (var fileHash = SHA256.Create())
                            if (Hex(fileHash.ComputeHash(bytes)) != (string)manifest["files"][name]) return "Dữ liệu trận bị thay đổi; đóng gói lại client.";
                        var label = Encoding.UTF8.GetBytes(name);
                        stream.Write(label, 0, label.Length);
                        // Canonical aggregate is independent of Windows/Linux line endings and UTF-8 BOM.
                        var normalized = Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(_directory, name)).Replace("\r\n", "\n"));
                        stream.Write(normalized, 0, normalized.Length);
                    }
                    if (Hex(aggregate.ComputeHash(stream.ToArray())) != expectedHash) return "Map/luật trận không khớp server; cập nhật client.";
                }
                var map = JObject.Parse(File.ReadAllText(Path.Combine(_directory, "map_demo.json")));
                var bounds = map["mapBounds"];
                Bounds = new RuntimeMapBounds { MinX = (float)bounds["minX"], MinY = (float)bounds["minY"], MaxX = (float)bounds["maxX"], MaxY = (float)bounds["maxY"] };
                var covers = new List<RuntimeCover>();
                foreach (var item in (JArray)map["obstacles"])
                    covers.Add(new RuntimeCover { Name = (string)item["name"], MinX = (float)item["minX"], MinY = (float)item["minY"],
                        MaxX = (float)item["maxX"], MaxY = (float)item["maxY"] });
                var settings = JObject.Parse(File.ReadAllText(Path.Combine(_directory, "match_settings.json")));
                var speed = (float)settings["playerSpeed"];
                var radius = (float)settings["playerRadius"];
                if (!(speed > 0) || !(radius > 0) || float.IsInfinity(speed) || float.IsInfinity(radius))
                    return "Tốc độ/bán kính người chơi trong dữ liệu trận không hợp lệ.";
                Covers = covers;
                PlayerSpeed = speed;
                PlayerRadius = radius;
                TryLoadZoneSchedule();
                return null;
            }
            catch (Exception e) when (!(e is OutOfMemoryException)) { return "Thiếu hoặc sai dữ liệu trận; chạy công cụ đồng bộ dữ liệu trước khi build."; }
        }

        public string LoadLocal()
        {
            try
            {
                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(_directory, "manifest.json")));
                using (var hash = SHA256.Create())
                using (var stream = new MemoryStream())
                {
                    foreach (var name in new[] { "map_demo.json", "match_settings.json", "zone_schedule.json" })
                    {
                        var label = Encoding.UTF8.GetBytes(name);
                        var bytes = Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(_directory, name)).Replace("\r\n", "\n"));
                        stream.Write(label, 0, label.Length); stream.Write(bytes, 0, bytes.Length);
                    }
                    return Verify((string)manifest["version"], Hex(hash.ComputeHash(stream.ToArray())));
                }
            }
            catch (Exception e) when (!(e is OutOfMemoryException)) { return "Invalid offline data; Sync Runtime Data before playing."; }
        }

        private void TryLoadZoneSchedule()
        {
            try
            {
                var path = Path.Combine(_directory, "zone_schedule.json");
                if (!File.Exists(path)) throw new FileNotFoundException("Missing zone data.");
                var root = JObject.Parse(File.ReadAllText(path));
                var s = new ZoneSchedule
                {
                    Version = (string)root["version"] ?? "1.0.0",
                    InitialRadius = (float)root["initialRadius"],
                    WarningDurationSeconds = (double)root["warningDurationSeconds"],
                    ShrinkSpeedCellsPerSecond = (float)root["shrinkSpeedCellsPerSecond"],
                    DamagePerSecond = (int)root["damagePerSecond"]
                };
                if (root["initialCenter"] != null)
                {
                    s.InitialCenter = (
                        (float)root["initialCenter"]["x"],
                        (float)root["initialCenter"]["y"]
                    );
                }
                if (root["intervals"] is JArray intervals)
                {
                    foreach (var elem in intervals)
                    {
                        s.Intervals.Add(new ZoneInterval
                        {
                            Index = (int)elem["index"],
                            StartSecond = (double)elem["startSecond"],
                            EndSecond = (double)elem["endSecond"],
                            StartRadius = (float)elem["startRadius"],
                            EndRadius = (float)elem["endRadius"]
                        });
                    }
                }
                s.Validate();
                ZoneSchedule = s;
            }
            catch (Exception)
            {
                throw new InvalidDataException("Invalid zone data.");
            }
        }

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}
