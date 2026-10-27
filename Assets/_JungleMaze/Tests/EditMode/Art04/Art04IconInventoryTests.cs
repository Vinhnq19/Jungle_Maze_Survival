using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JungleMaze.Infrastructure.Config;
using JungleMaze.Presentation.UI.Hud;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JungleMaze.Tests.Art04
{
    /// <summary>
    /// ART-04 R2-a: the icon inventory CSV against the balance roster, the normalized PNG files measured pixel by pixel,
    /// and the HUD prefabs' dependencies. Only what UiArtCatalogTests (ADHOC) does not already assert: catalog coverage,
    /// importer settings, sprite size and the 1 MB budget stay there.
    /// </summary>
    public sealed class Art04IconInventoryTests
    {
        private const string CsvPath = "Docs/Tickets/ART-04_icon_inventory.csv";
        private const string Ui = "Assets/_JungleMaze/Prefabs/UI/";
        private const int Canvas = 64;
        private const int VisibleAlpha = 8;
        private const int MinPadding = 3;
        private static readonly string[] Statuses = { "Đã chuẩn hóa", "Dùng tạm" };

        // ---------- AC1: the CSV itself ----------

        [Test]
        public void Csv_34Rows_MatchRosterOneToOne_WithValidStatusAndRealSource()
        {
            var rows = ReadCsv();
            Assert.AreEqual(34, rows.Count, "one row per roster ID");
            var roster = Roster();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var problems = new List<string>();
            foreach (var row in rows)
            {
                var id = row["id"];
                if (!seen.Add(id)) problems.Add(id + ": duplicate row");
                if (!roster.TryGetValue(id, out var group)) { problems.Add(id + ": not in weapons/equipments JSON"); continue; }
                if (row["loai"] != group) problems.Add($"{id}: loai {row["loai"]}, JSON says {group}");
                if (Array.IndexOf(Statuses, row["trang_thai"]) < 0) problems.Add($"{id}: trang_thai '{row["trang_thai"]}'");
                var source = row["nguon_thuc"];
                if (string.IsNullOrWhiteSpace(source) || !File.Exists(source)) problems.Add($"{id}: nguon_thuc '{source}' missing");
            }
            foreach (var id in roster.Keys) if (!seen.Contains(id)) problems.Add(id + ": no CSV row");
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void Csv_TargetPath_IsTheSpriteTheCatalogServes()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<HudItemIconCatalog>(Ui + "HudItemIconCatalog.asset");
            var rows = ReadCsv();
            Assert.AreEqual(34, rows.Count, "one row per roster ID");
            foreach (var row in rows)
                Assert.AreEqual(row["dich_chuan_hoa"], AssetDatabase.GetAssetPath(catalog.GetIcon(row["id"])), row["id"]);
        }

        // ---------- AC3, AC4: pixels of the normalized files ----------

        [Test]
        public void Png_64x64_TransparentCorners_Content44To60_Padded3_Centered_FewSemiTransparent()
        {
            var problems = new List<string>();
            var rows = ReadCsv();
            Assert.AreEqual(34, rows.Count, "one row per roster ID");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                foreach (var row in rows)
                {
                    var id = row["id"];
                    Assert.True(texture.LoadImage(File.ReadAllBytes(row["dich_chuan_hoa"])), id + " decodes");
                    if (texture.width != Canvas || texture.height != Canvas) { problems.Add($"{id}: {texture.width}×{texture.height}"); continue; }
                    var px = texture.GetPixels32();
                    if (px[0].a != 0 || px[Canvas - 1].a != 0 || px[Canvas * (Canvas - 1)].a != 0 || px[Canvas * Canvas - 1].a != 0)
                        problems.Add(id + ": a corner is not fully transparent");
                    int minX = Canvas, minY = Canvas, maxX = -1, maxY = -1, visible = 0, semi = 0;
                    for (var i = 0; i < px.Length; i++)
                    {
                        var a = px[i].a;
                        if (a > 0) visible++;
                        if (a > 0 && a < 255) semi++;
                        if (a <= VisibleAlpha) continue;
                        int x = i % Canvas, y = i / Canvas;
                        minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                    }
                    if (maxX < 0) { problems.Add(id + ": no pixel with alpha > 8"); continue; }
                    var longest = Math.Max(maxX - minX + 1, maxY - minY + 1);
                    if (longest < 44 || longest > 60) problems.Add($"{id}: content {longest} px, expected 44–60");
                    // AC11 only sweeps 8 icons through the slots. At 28 px a source pixel is 0.4375 screen px and the
                    // UI-01 icon rect overlaps the label row by 1 px, so 3 transparent px per edge keeps every icon clear.
                    int left = minX, right = Canvas - 1 - maxX, bottom = minY, top = Canvas - 1 - maxY;
                    if (Math.Min(Math.Min(left, right), Math.Min(bottom, top)) < MinPadding)
                        problems.Add($"{id}: padding L{left} R{right} T{top} B{bottom}, expected ≥ {MinPadding} each");
                    var dx = (minX + maxX + 1) * 0.5f - Canvas * 0.5f;
                    var dy = (minY + maxY + 1) * 0.5f - Canvas * 0.5f;
                    if (Mathf.Abs(dx) > 2f || Mathf.Abs(dy) > 2f) problems.Add($"{id}: content centre off by ({dx}, {dy})");
                    var ratio = (float)semi / visible;
                    if (ratio > 0.10f) problems.Add($"{id}: {ratio:P1} of visible pixels are semi-transparent");
                }
            }
            finally { Object.DestroyImmediate(texture); }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        // ---------- AC17: no art pulled from Imported ----------

        [Test]
        public void HudPrefabsAndCatalog_DependOnNoImageUnderImported()
        {
            var roots = new[] { Ui + "Hud.prefab", Ui + "HudSlot.prefab", Ui + "HudPickupToast.prefab", Ui + "HudItemIconCatalog.asset" };
            foreach (var root in roots) Assert.NotNull(AssetDatabase.LoadMainAssetAtPath(root), root);
            var offenders = new List<string>();
            foreach (var path in AssetDatabase.GetDependencies(roots, true))
            {
                if (!path.StartsWith("Assets/Imported/", StringComparison.Ordinal)) continue;
                // CPO decision on AC17: only images count. The TMP shader under Imported/JSurviv has been a dependency of
                // the HUD fonts since UI-01 and is excluded on purpose; any texture or sprite from there is a failure.
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".png" || ext == ".psd" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga"
                    || AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(Texture2D)
                    || AssetDatabase.LoadAssetAtPath<Sprite>(path) != null)
                    offenders.Add(path);
            }
            Assert.IsEmpty(offenders, string.Join("\n", offenders));
        }

        [Test]
        public void HudPrefabs_HaveNoMissingScriptOnAnyGameObject()
        {
            var problems = new List<string>();
            foreach (var path in new[] { Ui + "Hud.prefab", Ui + "HudSlot.prefab", Ui + "HudPickupToast.prefab" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.NotNull(prefab, path);
                // The count only covers the GameObject passed in, so every child is checked on its own.
                foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                {
                    var missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    if (missing > 0) problems.Add($"{path} › {t.name}: {missing} missing script(s)");
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        // ---------- helpers ----------

        /// <summary>ID → "Weapon" / "Equipment", from the balance JSON.</summary>
        private static Dictionary<string, string> Roster()
        {
            var balance = new BalanceConfigProvider(Application.streamingAssetsPath, null);
            Assert.IsNull(balance.LastError);
            var roster = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var w in balance.Weapons) roster.Add(w.Id, "Weapon");
            foreach (var e in balance.Equipments) roster.Add(e.Id, "Equipment");
            Assert.AreEqual(34, roster.Count, "18 weapons + 16 equipments");
            return roster;
        }

        private static List<Dictionary<string, string>> ReadCsv()
        {
            Assert.True(File.Exists(CsvPath), CsvPath);
            var lines = File.ReadAllLines(CsvPath, Encoding.UTF8);
            var header = SplitCsvLine(lines[0].TrimStart('﻿'));
            var rows = new List<Dictionary<string, string>>();
            for (var i = 1; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) continue;
                var cells = SplitCsvLine(lines[i]);
                Assert.AreEqual(header.Count, cells.Count, "CSV line " + (i + 1));
                var row = new Dictionary<string, string>(StringComparer.Ordinal);
                for (var c = 0; c < header.Count; c++) row[header[c]] = cells[c];
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>RFC 4180 for one line: commas inside quotes, doubled quotes as a literal quote.</summary>
        private static List<string> SplitCsvLine(string line)
        {
            var cells = new List<string>();
            var cell = new StringBuilder();
            var quoted = false;
            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (quoted)
                {
                    if (ch != '"') cell.Append(ch);
                    else if (i + 1 < line.Length && line[i + 1] == '"') { cell.Append('"'); i++; }
                    else quoted = false;
                }
                else if (ch == '"') quoted = true;
                else if (ch == ',') { cells.Add(cell.ToString()); cell.Clear(); }
                else cell.Append(ch);
            }
            cells.Add(cell.ToString());
            return cells;
        }
    }
}
