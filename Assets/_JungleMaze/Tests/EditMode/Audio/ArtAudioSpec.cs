using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JungleMaze.Domain.Audio;
using JungleMaze.Presentation.Audio;
using UnityEditor;
using UnityEngine;

namespace JungleMaze.Tests.Audio
{
    /// <summary>
    /// ART-05 targets as test data (Docs/Tickets/ART-05.md 2.3, 2.6): tier and effective level E, length window,
    /// baked level L window per SfxId; plus helpers to measure a clip the way G-7 defines it. Not runtime code.
    /// </summary>
    internal static class ArtAudioSpec
    {
        public const string CatalogPath = "Assets/_JungleMaze/Art/Audio/SfxCatalog.asset";
        public const string SfxFolder = "Assets/_JungleMaze/Art/Audio/Sfx/";
        public const string MusicFolder = "Assets/_JungleMaze/Art/Audio/Music/";
        public const string ManifestPath = "Docs/Tickets/ART-05_audio_manifest.csv";
        public const float ActiveFloor = 0.003f;
        public const string ImportedFolder = "Assets/Imported";
        /// <summary>Licence of a clip whose page says CC0 but whose evidence is not in the repo (CPO 4.9).</summary>
        public const string SelfDeclaredCc0 = "chưa kiểm chứng (nguồn tự khai CC0)";

        /// <summary>
        /// JSurviv clips that stay quieter than their target under the 6 dB limiter (CPO 4.9): lowest L allowed per
        /// file. Mirrors the recipe/manifest column "exception"; a test keeps the two lists equal.
        /// </summary>
        public static readonly Dictionary<string, float> LevelExceptions = new Dictionary<string, float>
        {
            { "bomb_fuse", -24.5f }, { "wear_armor_0", -23.5f }, { "wear_armor_1", -23.5f },
            { "wear_armor_2", -23.5f }, { "wear_armor_3", -23.5f },
        };

        /// <summary>Lowest effective level E allowed for an id below its tier (BombFuse: volume is already 1.0).</summary>
        public static readonly Dictionary<SfxId, float> EffectiveFloorExceptions = new Dictionary<SfxId, float>
        {
            { SfxId.BombFuse, -24.5f },
        };

        /// <summary>Skips a test that reads Assets/Imported when that reference folder has been cleaned up (spec 2.4).</summary>
        public static string ImportedOrIgnore(string relative)
        {
            var path = ProjectPath(relative);
            if (!Directory.Exists(path)) NUnit.Framework.Assert.Ignore(relative + " is gone: nothing to compare against");
            return path;
        }

        public readonly struct Row
        {
            public readonly SfxId Id;
            public readonly int Tier;
            public readonly float MinSeconds, MaxSeconds;

            public Row(SfxId id, int tier, float min, float max)
            {
                Id = id;
                Tier = tier;
                MinSeconds = min;
                MaxSeconds = max;
            }

            public float TierLevel => Tier == 1 ? -16f : Tier == 2 ? -20f : Tier == 3 ? -24f : -29f;
            public bool Footstep => Id == SfxId.FootstepGrass || Id == SfxId.FootstepSwamp;
            public float MinLevel => Footstep ? -26f : -20f;
            public float MaxLevel => Footstep ? -22f : -15f;
        }

        public static readonly Row[] Table =
        {
            new Row(SfxId.FootstepGrass, 4, 0.05f, 0.40f), new Row(SfxId.FootstepSwamp, 4, 0.08f, 0.50f),
            new Row(SfxId.BushEnter, 4, 0.10f, 0.60f), new Row(SfxId.MeleeSwing, 2, 0.15f, 0.60f),
            new Row(SfxId.MeleeHit, 2, 0.10f, 0.50f), new Row(SfxId.ShootRanged, 2, 0.15f, 1.20f),
            new Row(SfxId.ShootCrossbow, 2, 0.15f, 1.00f), new Row(SfxId.Throw, 2, 0.10f, 0.60f),
            new Row(SfxId.Reload, 3, 0.20f, 1.50f), new Row(SfxId.EmptyClick, 3, 0.03f, 0.30f),
            new Row(SfxId.BombFuse, 2, 0.80f, 1.50f), new Row(SfxId.Explosion, 2, 0.50f, 3.00f),
            new Row(SfxId.TrapTriggered, 2, 0.10f, 0.80f), new Row(SfxId.HitMarker, 1, 0.03f, 0.15f),
            new Row(SfxId.HurtLocal, 1, 0.20f, 0.60f), new Row(SfxId.ArmorAbsorb, 2, 0.10f, 0.40f),
            new Row(SfxId.Eliminated, 2, 0.30f, 1.50f), new Row(SfxId.PickupWeapon, 3, 0.15f, 0.80f),
            new Row(SfxId.PickupItem, 3, 0.15f, 0.80f), new Row(SfxId.WearArmor, 3, 0.30f, 1.20f),
            new Row(SfxId.UseHeal, 3, 0.50f, 1.50f), new Row(SfxId.UseDrink, 3, 0.50f, 1.50f),
            new Row(SfxId.UseCancel, 3, 0.10f, 0.40f), new Row(SfxId.ZoneBell3, 1, 0.40f, 0.75f),
            new Row(SfxId.DragonRoar, 1, 1.50f, 4.00f), new Row(SfxId.MatchEnd, 1, 1.00f, 3.00f),
        };

        public static SfxCatalog LoadCatalog() => AssetDatabase.LoadAssetAtPath<SfxCatalog>(CatalogPath);

        public static float Db(float v) => 20f * Mathf.Log10(Mathf.Max(v, 1e-12f));

        /// <summary>Samples after import (mono clips only; channels are averaged otherwise).</summary>
        public static float[] Samples(AudioClip clip)
        {
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            var raw = new float[clip.samples * clip.channels];
            if (!clip.GetData(raw, 0)) throw new InvalidOperationException("GetData failed: " + clip.name);
            if (clip.channels == 1) return raw;
            var mono = new float[clip.samples];
            for (var i = 0; i < mono.Length; i++)
            {
                var sum = 0f;
                for (var c = 0; c < clip.channels; c++) sum += raw[i * clip.channels + c];
                mono[i] = sum / clip.channels;
            }
            return mono;
        }

        public static float Peak(float[] x)
        {
            var peak = 0f;
            foreach (var s in x) peak = Mathf.Max(peak, Mathf.Abs(s));
            return Db(peak);
        }

        /// <summary>L (G-7): RMS between the first and the last sample with |x| ≥ 0.003, in dBFS.</summary>
        public static float Level(float[] x)
        {
            int first = -1, last = -1;
            for (var i = 0; i < x.Length; i++)
            {
                if (Mathf.Abs(x[i]) < ActiveFloor) continue;
                if (first < 0) first = i;
                last = i;
            }
            if (first < 0) return -120f;
            double sum = 0;
            for (var i = first; i <= last; i++) sum += (double)x[i] * x[i];
            return Db((float)Math.Sqrt(sum / (last - first + 1)));
        }

        public static float Edge(float[] x, int rate, bool head)
        {
            var n = Mathf.Max(1, Mathf.RoundToInt(0.002f * rate));
            var peak = 0f;
            for (var i = 0; i < n && i < x.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(x[head ? i : x.Length - 1 - i]));
            return Db(peak);
        }

        public static string ProjectPath(string relative) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));

        /// <summary>Rows of the manifest as column → value (RFC 4180 quoting).</summary>
        public static List<Dictionary<string, string>> ReadManifest()
        {
            var lines = ParseCsv(File.ReadAllText(ProjectPath(ManifestPath), Encoding.UTF8));
            var header = lines[0];
            var rows = new List<Dictionary<string, string>>();
            for (var i = 1; i < lines.Count; i++)
            {
                if (lines[i].Count == 1 && lines[i][0].Length == 0) continue;
                var row = new Dictionary<string, string>();
                for (var c = 0; c < header.Count; c++) row[header[c]] = c < lines[i].Count ? lines[i][c] : "";
                rows.Add(row);
            }
            return rows;
        }

        private static List<List<string>> ParseCsv(string text)
        {
            var lines = new List<List<string>>();
            var line = new List<string>();
            var field = new StringBuilder();
            var quoted = false;
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (quoted)
                {
                    if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else if (ch == '"') quoted = false;
                    else field.Append(ch);
                }
                else if (ch == '"') quoted = true;
                else if (ch == ',') { line.Add(field.ToString()); field.Clear(); }
                else if (ch == '\n' || ch == '\r')
                {
                    if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    line.Add(field.ToString());
                    field.Clear();
                    lines.Add(line);
                    line = new List<string>();
                }
                else field.Append(ch);
            }
            if (field.Length > 0 || line.Count > 0) { line.Add(field.ToString()); lines.Add(line); }
            return lines;
        }
    }
}
