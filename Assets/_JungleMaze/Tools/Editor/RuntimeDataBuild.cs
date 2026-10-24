using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace JungleMaze.EditorTools
{
    /// <summary>Canonical backend data is copied explicitly, then verified before every player build.</summary>
    public sealed class RuntimeDataBuild : IPreprocessBuildWithReport
    {
        private static readonly string[] Files = { "map_demo.json", "match_settings.json", "zone_schedule.json" };
        private static string Source => Path.GetFullPath(Path.Combine(Application.dataPath, "../Backend/Shared/GameData"));
        private static string Target => Path.Combine(Application.streamingAssetsPath, "JungleMaze/runtime");
        public int callbackOrder => -1000;

        [MenuItem("Jungle Maze/Phase 2/Sync Runtime Data")]
        public static void Sync()
        {
            Directory.CreateDirectory(Target);
            foreach (var name in Files) File.Copy(Path.Combine(Source, name), Path.Combine(Target, name), true);
            File.WriteAllText(Path.Combine(Target, "manifest.json"), Manifest(), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log("[RuntimeData] Synced canonical match/map/zone and SHA256 manifest.");
        }

        [MenuItem("Jungle Maze/Phase 2/Verify Runtime Data")]
        public static void Verify()
        {
            foreach (var name in Files)
                if (!File.Exists(Path.Combine(Target, name)) || !File.ReadAllBytes(Path.Combine(Source, name)).SequenceEqual(File.ReadAllBytes(Path.Combine(Target, name))))
                    throw new BuildFailedException("Runtime data is outdated. Use Jungle Maze > Phase 2 > Sync Runtime Data.");
            if (!File.Exists(Path.Combine(Target, "manifest.json")) || File.ReadAllText(Path.Combine(Target, "manifest.json")) != Manifest())
                throw new BuildFailedException("Runtime data manifest is outdated; sync runtime data first.");
        }

        public void OnPreprocessBuild(BuildReport report) => Verify();

        private static string Manifest()
        {
            var settings = JsonUtility.FromJson<VersionInfo>(File.ReadAllText(Path.Combine(Source, "match_settings.json")));
            var text = new StringBuilder("{\n  \"version\": \"").Append(settings.version).Append("\",\n  \"files\": {\n");
            using (var sha = SHA256.Create())
                for (var i = 0; i < Files.Length; i++)
                {
                    var hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Source, Files[i])))).Replace("-", "").ToLowerInvariant();
                    text.Append("    \"").Append(Files[i]).Append("\": \"").Append(hash).Append(i == Files.Length - 1 ? "\"\n" : "\",\n");
                }
            return text.Append("  }\n}\n").ToString();
        }
        [Serializable] private sealed class VersionInfo { public string version; }
    }
}
