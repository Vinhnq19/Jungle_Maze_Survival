using System;
using System.Collections.Generic;
using System.IO;
using JungleMaze.Domain.Hud;
using JungleMaze.Infrastructure.Config;
using JungleMaze.Presentation.UI;
using JungleMaze.Presentation.UI.Hud;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JungleMaze.Tests.UiArt
{
    /// <summary>
    /// ADHOC-UI-ASSETS-A: item / effect / menu catalogs against the balance roster, importer settings, the T-1 Resources
    /// fallback and the Boot binding. Checks IDs, references, size and filter only (no pixel or hash checks: icons are
    /// expected to be redrawn in place).
    /// </summary>
    public sealed class UiArtCatalogTests
    {
        private const string Ui = "Assets/_JungleMaze/Prefabs/UI/";
        private readonly List<Object> _created = new List<Object>();

        private static HudItemIconCatalog Items => AssetDatabase.LoadAssetAtPath<HudItemIconCatalog>(Ui + "HudItemIconCatalog.asset");

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private static List<string> RosterIds(out List<string> folders)
        {
            var balance = new BalanceConfigProvider(Application.streamingAssetsPath, null);
            Assert.IsNull(balance.LastError);
            var ids = new List<string>();
            folders = new List<string>();
            foreach (var w in balance.Weapons) { ids.Add(w.Id); folders.Add("Weapons"); }
            foreach (var e in balance.Equipments) { ids.Add(e.Id); folders.Add("Equipments"); }
            return ids;
        }

        [Test]
        public void ItemCatalog_Covers34RosterIds_EachWithItsOwnSprite()
        {
            var catalog = Items;
            Assert.NotNull(catalog);
            var ids = RosterIds(out var folders);
            Assert.AreEqual(34, ids.Count, "18 weapons + 16 equipments in the balance JSON");
            Assert.AreEqual(34, catalog.Count);
            var problems = new List<string>();
            Assert.True(catalog.Validate(problems), string.Join("; ", problems));
            var sprites = new HashSet<Sprite>();
            for (var i = 0; i < ids.Count; i++)
            {
                var sprite = catalog.GetIcon(ids[i]);
                Assert.NotNull(sprite, ids[i]);
                Assert.AreEqual("Assets/_JungleMaze/Art/UI/Icons/" + folders[i] + "/" + ids[i] + ".png", AssetDatabase.GetAssetPath(sprite), ids[i]);
                Assert.True(sprites.Add(sprite), "sprite reused by " + ids[i]);
            }
        }

        [Test]
        public void ItemCatalog_NullEmptyUnknownAndWrongCase_ReturnNull()
        {
            var catalog = Items;
            Assert.IsNull(catalog.GetIcon(null));
            Assert.IsNull(catalog.GetIcon(""));
            Assert.IsNull(catalog.GetIcon("NOT_AN_ITEM"));
            Assert.IsNull(catalog.GetIcon("axe"), "IDs are ordinal / case-sensitive like the balance JSON");
            Assert.NotNull(catalog.GetIcon("AXE"));
        }

        [Test]
        public void Validate_ReportsEmptyDuplicateAndMissingSprite()
        {
            var catalog = Track(ScriptableObject.CreateInstance<HudItemIconCatalog>());
            var sprite = Items.GetIcon("AXE");
            catalog.SetEntries(new[]
            {
                new HudItemIconCatalog.Entry { Id = "AXE", Icon = sprite },
                new HudItemIconCatalog.Entry { Id = "AXE", Icon = sprite },
                new HudItemIconCatalog.Entry { Id = "", Icon = sprite },
                new HudItemIconCatalog.Entry { Id = "BONE", Icon = null }
            });
            var problems = new List<string>();
            Assert.False(catalog.Validate(problems));
            Assert.AreEqual(3, problems.Count, string.Join("; ", problems));
            Assert.AreSame(sprite, catalog.GetIcon("AXE"), "first entry wins");
            Assert.IsNull(catalog.GetIcon("BONE"), "missing sprite reads as unknown");
        }

        [Test]
        public void ItemIcons_Import64PointNoMipUncompressed_UnderOneMegabyte()
        {
            long bytes = 0;
            foreach (var entry in Items.Entries)
            {
                var path = AssetDatabase.GetAssetPath(entry.Icon);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, path);
                Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode, path);
                Assert.AreEqual(FilterMode.Point, importer.filterMode, path);
                Assert.False(importer.mipmapEnabled, path);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, path);
                Assert.AreEqual(new Vector2(0.5f, 0.5f), entry.Icon.pivot / entry.Icon.rect.size, path);
                Assert.AreEqual(new Vector2(64, 64), entry.Icon.rect.size, path);
                Assert.AreEqual(Vector4.zero, entry.Icon.border, path);
                bytes += entry.Icon.texture.width * entry.Icon.texture.height * 4;
            }
            Assert.LessOrEqual(bytes, 1024 * 1024);
        }

        [Test]
        public void EffectCatalog_EightKindsHaveIcons_AdrenalineReusesItemIcon()
        {
            var effects = AssetDatabase.LoadAssetAtPath<HudEffectCatalog>(Ui + "HudEffectCatalog.asset");
            var kinds = new HashSet<EffectKind>();
            foreach (var entry in effects.Entries)
            {
                Assert.NotNull(entry.Icon, entry.Kind.ToString());
                Assert.True(kinds.Add(entry.Kind), "duplicate " + entry.Kind);
                Assert.AreEqual(FilterMode.Point, entry.Icon.texture.filterMode, entry.Kind.ToString());
                Assert.False(string.IsNullOrEmpty(entry.Label), entry.Kind + " keeps its fallback label");
            }
            for (var kind = EffectKind.Adrenaline; kind <= EffectKind.CcImmune; kind++) Assert.True(kinds.Contains(kind), kind.ToString());
            Assert.AreSame(Items.GetIcon("ADRENALINE"), effects.Icon(EffectKind.Adrenaline));
            Assert.AreEqual("Assets/_JungleMaze/Art/UI/Icons/Effects/Poison.png", AssetDatabase.GetAssetPath(effects.Icon(EffectKind.Poison)));
        }

        [Test]
        public void MenuArt_SixAssetsAssigned_AndArtSetLoadsFromResources()
        {
            var menu = AssetDatabase.LoadAssetAtPath<UiMenuArtCatalog>(Ui + "UiMenuArtCatalog.asset");
            Assert.AreEqual(6, menu.AssignedCount);
            foreach (var texture in new[] { menu.Background, menu.KeyArt, menu.Logo, menu.ButtonPrimary, menu.ButtonSecondary, menu.ButtonGhost })
                StringAssert.StartsWith("Assets/_JungleMaze/Art/UI/Menu/", AssetDatabase.GetAssetPath(texture));
            Assert.Greater(menu.ImguiBorder, 0);
            Assert.Less(menu.ImguiBorder * 2, 32, "IMGUI border must fit a 28-32 px button");
            // T-1: a Bootstrap auto-created outside Boot loads this once and gets the same catalogs.
            var set = Resources.Load<UiArtSet>(UiArtSet.ResourcePath);
            Assert.NotNull(set);
            Assert.AreSame(Items, set.ItemIcons);
            Assert.AreSame(menu, set.MenuArt);
        }

        [Test]
        public void BootScene_BootstrapReferencesTheArtSet()
        {
            var set = Resources.Load<UiArtSet>(UiArtSet.ResourcePath);
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(set));
            var yaml = File.ReadAllText("Assets/_JungleMaze/Scenes/Boot.unity");
            StringAssert.Contains("uiArt: {fileID: 11400000, guid: " + guid, yaml);
        }

        [Test]
        public void Lookup_KnownAndUnknown_DoesNotAllocateAfterWarmup()
        {
            var catalog = Items;
            catalog.GetIcon("AXE");
            catalog.GetIcon("NOT_AN_ITEM");
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
            {
                catalog.GetIcon("AXE");
                catalog.GetIcon("ELIXIR_OF_LIFE");
                catalog.GetIcon("NOT_AN_ITEM");
                catalog.GetIcon(null);
            }
            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        private T Track<T>(T o) where T : Object { _created.Add(o); return o; }
    }
}
