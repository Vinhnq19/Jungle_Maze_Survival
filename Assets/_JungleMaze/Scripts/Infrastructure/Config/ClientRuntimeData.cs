using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
        public ClientRuntimeData(string streamingAssets) => _directory = Path.Combine(streamingAssets, "JungleMaze", "runtime");
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
                Covers = covers;
                return null;
            }
            catch (Exception e) when (!(e is OutOfMemoryException)) { return "Thiếu hoặc sai dữ liệu trận; chạy công cụ đồng bộ dữ liệu trước khi build."; }
        }
        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}
