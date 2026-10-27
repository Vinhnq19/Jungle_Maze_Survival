using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using JungleMaze.Composition;
using JungleMaze.Infrastructure.Config;
using JungleMaze.Presentation.UI;
using JungleMaze.Presentation.UI.Hud;
using JungleMaze.Presentation.UI.Hud.Feedback;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JungleMaze.EditorTools
{
    /// <summary>
    /// Repeatable import + binding of the direction A UI art (ADHOC-UI-ASSETS-A): texture importer settings, item /
    /// effect / menu catalogs, palette and frame overlays on the HUD prefabs, and the Boot scene Bootstrap reference.
    /// Idempotent: running it twice finds the overlays it created instead of adding new ones, and it never touches
    /// RectTransform geometry of existing blocks. Also re-applied after <see cref="HudFeedbackPrefabBuild.Build"/>.
    /// </summary>
    [InitializeOnLoad]
    public static class UiArtAssetBuild
    {
        public const string Ui = "Assets/_JungleMaze/Prefabs/UI/";
        public const string Hud = "Assets/_JungleMaze/Art/Hud/";
        public const string Icons = "Assets/_JungleMaze/Art/UI/Icons/";
        public const string Menu = "Assets/_JungleMaze/Art/UI/Menu/";
        public const string ItemCatalogPath = Ui + "HudItemIconCatalog.asset";
        public const string MenuCatalogPath = Ui + "UiMenuArtCatalog.asset";
        public const string EffectCatalogPath = Ui + "HudEffectCatalog.asset";
        public const string ArtSetPath = "Assets/_JungleMaze/Resources/" + UiArtSet.ResourcePath + ".asset";
        public const string BootScene = "Assets/_JungleMaze/Scenes/Boot.unity";
        public const string EdgeName = "ArtEdge";
        public const string BevelName = "ArtBevel";

        /// <summary>9-slice border (px, all sides) per sprite name, from Docs/Art/Production_A/UI/asset_mapping.json.</summary>
        public static readonly Dictionary<string, int> Borders = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "HudRound10", 24 }, { "HudRound6", 20 }, { "HudRound6Stroke2", 20 }, { "HudRound6Dashed", 20 },
            { "HudRound6Stroke1", 16 }, { "BarFill", 6 }, { "BarTrack", 6 }, { "KeyCap", 12 }, { "MinimapFrame", 32 },
            { "EffectFrame", 18 }, { "ToastBg", 20 }, { "DangerBanner", 28 }, { "ABevel96", 24 }, { "ABevelSlot128", 20 },
            { "AFrameEdge96", 24 }, { "AFrameEdgeSlot128", 20 }, { "HudCircle16", 0 }, { "Crosshair", 0 }, { "HitMarker", 0 },
            { "ButtonPrimary", 32 }, { "ButtonSecondary", 32 }, { "ButtonGhost", 32 }, { "Logo", 0 }, { "KeyArt", 0 }, { "Background", 0 }
        };

        static UiArtAssetBuild() => HudFeedbackPrefabBuild.AfterBuild += ApplyPrefabs;

        [MenuItem("Jungle Maze/UI Art/Build And Bind Direction A")]
        public static void BuildAll()
        {
            ConfigureImporters();
            BuildCatalogs();
            ApplyPrefabs();
            BindBootScene();
            AssetDatabase.SaveAssets();
            var problems = Validate();
            if (problems.Count == 0) Debug.Log("[UiArt] Direction A art built and bound; validator clean.");
            else Debug.LogError("[UiArt] Validator: " + string.Join("; ", problems));
        }

        /// <summary>Sprite (2D and UI), Single, Point, no mipmaps, uncompressed, pivot centre, FullRect, border per table.</summary>
        public static void ConfigureImporters()
        {
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_JungleMaze/Art/UI" })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var name in Borders.Keys)
                if (File.Exists(Hud + name + ".png")) paths.Add(Hud + name + ".png");
            foreach (var path in paths)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var name = Path.GetFileNameWithoutExtension(path);
                var border = path.Contains("/Icons/") ? 0 : Borders.TryGetValue(name, out var b) ? b : 0;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                var dirty = importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single
                    || importer.filterMode != FilterMode.Point || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed
                    || settings.spriteBorder != new Vector4(border, border, border, border) || settings.spriteMeshType != SpriteMeshType.FullRect
                    || settings.spriteAlignment != (int)SpriteAlignment.Center || !Mathf.Approximately(importer.spritePixelsPerUnit, 100);
                if (!dirty) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.spritePixelsPerUnit = 100;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spritePivot = new Vector2(0.5f, 0.5f);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteBorder = new Vector4(border, border, border, border);
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        /// <summary>Item catalog from the balance roster (weapons then equipments), effect icons and menu art.</summary>
        public static void BuildCatalogs()
        {
            var balance = new BalanceConfigProvider(Application.streamingAssetsPath, null);
            var entries = new List<HudItemIconCatalog.Entry>();
            foreach (var weapon in balance.Weapons) entries.Add(new HudItemIconCatalog.Entry { Id = weapon.Id, Icon = IconSprite("Weapons", weapon.Id) });
            foreach (var equipment in balance.Equipments) entries.Add(new HudItemIconCatalog.Entry { Id = equipment.Id, Icon = IconSprite("Equipments", equipment.Id) });
            var items = LoadOrCreate<HudItemIconCatalog>(ItemCatalogPath);
            items.SetEntries(entries.ToArray());
            EditorUtility.SetDirty(items);

            var effects = AssetDatabase.LoadAssetAtPath<HudEffectCatalog>(EffectCatalogPath);
            if (effects != null)
            {
                var so = new SerializedObject(effects);
                var array = so.FindProperty("entries");
                for (var i = 0; i < array.arraySize; i++)
                {
                    var entry = array.GetArrayElementAtIndex(i);
                    var kind = entry.FindPropertyRelative("Kind");
                    var name = kind.enumNames[kind.enumValueIndex];
                    entry.FindPropertyRelative("Icon").objectReferenceValue = name == "Adrenaline"
                        ? IconSprite("Equipments", "ADRENALINE")
                        : AssetDatabase.LoadAssetAtPath<Sprite>(Icons + "Effects/" + name + ".png");
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var menu = LoadOrCreate<UiMenuArtCatalog>(MenuCatalogPath);
            var menuSo = new SerializedObject(menu);
            foreach (var name in new[] { "Background", "KeyArt", "Logo", "ButtonPrimary", "ButtonSecondary", "ButtonGhost" })
                menuSo.FindProperty(char.ToLowerInvariant(name[0]) + name.Substring(1)).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(Menu + name + ".png");
            menuSo.ApplyModifiedPropertiesWithoutUndo();

            if (!AssetDatabase.IsValidFolder("Assets/_JungleMaze/Resources")) AssetDatabase.CreateFolder("Assets/_JungleMaze", "Resources");
            var set = LoadOrCreate<UiArtSet>(ArtSetPath);
            var setSo = new SerializedObject(set);
            setSo.FindProperty("itemIcons").objectReferenceValue = items;
            setSo.FindProperty("menuArt").objectReferenceValue = menu;
            setSo.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Palette A, frame overlays and A sprites on the HUD child prefabs, then on Hud.prefab.</summary>
        public static void ApplyPrefabs()
        {
            Edit("HudSlot", slot =>
            {
                var view = slot.GetComponent<QuickbarSlotView>();
                var frame = EnsureFrame(slot.transform, "AFrameEdgeSlot128", "ABevelSlot128", 1, 1f);
                Set(view, "frame", frame);
                var icon = slot.transform.Find("Icon").GetComponent<Image>();
                icon.pixelsPerUnitMultiplier = 2f; // 20 px placeholder border inside a 28 px square
                icon.preserveAspect = false;
                foreach (var name in new[] { "Border", "InUseFrame" }) slot.transform.Find(name).GetComponent<Image>().pixelsPerUnitMultiplier = 1f;
            });
            Edit("HudKeyCap", key => StyleKeyCap(key.transform));
            Edit("HudEffectChip", chip =>
            {
                var border = chip.transform.Find("Box/Border").GetComponent<Image>();
                SetSprite(border, "EffectFrame", 1f);
                var icon = chip.transform.Find("Box/Icon").GetComponent<Image>();
                icon.preserveAspect = true;
                icon.type = Image.Type.Simple;
                icon.color = Color.white;
            });
            Edit("HudPickupToast", toast =>
            {
                SetSprite(toast.GetComponent<Image>(), "ToastBg", 2f); // 31 px tall toast, 20 px border
                var icon = toast.transform.Find("Icon").GetComponent<Image>();
                icon.pixelsPerUnitMultiplier = 3f; // 18 px placeholder square
                icon.preserveAspect = false;
            });
            Edit("Hud", hud =>
            {
                var root = hud.GetComponent<HudRoot>();
                var palette = new SerializedObject(root).FindProperty("palette");
                var so = palette.serializedObject;
                object defaults = HudPalette.Default;
                foreach (var field in typeof(HudPalette).GetFields(BindingFlags.Public | BindingFlags.Instance))
                    palette.FindPropertyRelative(field.Name).colorValue = (Color)field.GetValue(defaults);
                so.ApplyModifiedPropertiesWithoutUndo();
                var frames = new List<Object>();
                foreach (var path in new[] { "Vitals", "Minimap", "MatchStatus" })
                    frames.Add(EnsureFrame(hud.transform.Find(path), "AFrameEdge96", "ABevel96", 0, 1f));
                frames.Add(EnsureFrame(hud.transform.Find("QuickBar/WeaponStrip"), "AFrameEdge96", "ABevel96", 0, 2f)); // 38 px tall strip
                SetArray(root, "artFrames", frames.ToArray());
                StyleKeyCap(hud.transform.Find("QuickBar/WeaponStrip/ReloadHint/KeyCap"));
                var use = hud.transform.Find("QuickBar/UseActionStrip");
                if (use != null) use.GetComponent<Image>().pixelsPerUnitMultiplier = 2f; // 32 px tall, 20 px border
                var overflow = hud.transform.Find("PickupFeed/Overflow");
                if (overflow != null) SetSprite(overflow.GetComponent<Image>(), "ToastBg", 2f);
                var danger = hud.transform.Find("DangerBanner");
                if (danger != null) SetSprite(danger.GetComponent<Image>(), "DangerBanner", 2f);
                foreach (var graphic in hud.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
                root.ApplyPalette();
            });
            AssetDatabase.SaveAssets();
        }

        /// <summary>Points the Boot scene Bootstrap at the art set (other scenes use the Resources fallback).</summary>
        public static void BindBootScene()
        {
            var set = AssetDatabase.LoadAssetAtPath<UiArtSet>(ArtSetPath);
            var scene = SceneManager.GetSceneByPath(BootScene);
            var opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(BootScene, OpenSceneMode.Additive);
            try
            {
                foreach (var go in scene.GetRootGameObjects())
                foreach (var bootstrap in go.GetComponentsInChildren<Bootstrap>(true))
                {
                    var so = new SerializedObject(bootstrap);
                    var property = so.FindProperty("uiArt");
                    if (property.objectReferenceValue == set) continue;
                    property.objectReferenceValue = set;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.MarkSceneDirty(scene);
                }
                if (scene.isDirty) EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>Every binding problem found (empty list = valid). Used by the menu build and by QA.</summary>
        public static List<string> Validate()
        {
            var problems = new List<string>();
            var items = AssetDatabase.LoadAssetAtPath<HudItemIconCatalog>(ItemCatalogPath);
            if (items == null) problems.Add("item catalog missing");
            else
            {
                items.Validate(problems);
                var balance = new BalanceConfigProvider(Application.streamingAssetsPath, null);
                foreach (var weapon in balance.Weapons) if (items.GetIcon(weapon.Id) == null) problems.Add("no icon for " + weapon.Id);
                foreach (var equipment in balance.Equipments) if (items.GetIcon(equipment.Id) == null) problems.Add("no icon for " + equipment.Id);
                foreach (var entry in items.Entries)
                    if (entry.Icon != null && (entry.Icon.rect.width != 64 || entry.Icon.rect.height != 64 || entry.Icon.texture.filterMode != FilterMode.Point))
                        problems.Add(entry.Id + " icon is not 64x64 Point");
            }
            var effects = AssetDatabase.LoadAssetAtPath<HudEffectCatalog>(EffectCatalogPath);
            if (effects == null) problems.Add("effect catalog missing");
            else foreach (var entry in effects.Entries) if (entry.Icon == null) problems.Add("no effect icon for " + entry.Kind);
            var menu = AssetDatabase.LoadAssetAtPath<UiMenuArtCatalog>(MenuCatalogPath);
            if (menu == null || menu.AssignedCount != 6) problems.Add("menu art incomplete");
            var set = AssetDatabase.LoadAssetAtPath<UiArtSet>(ArtSetPath);
            if (set == null || set.ItemIcons != items || set.MenuArt != menu) problems.Add("UiArtSet does not reference both catalogs");
            return problems;
        }

        private static void StyleKeyCap(Transform key)
        {
            if (key == null) return;
            SetSprite(key.GetComponent<Image>(), "KeyCap", 1f);
            var border = key.Find("Border");
            if (border != null) border.GetComponent<Image>().pixelsPerUnitMultiplier = 2f; // 24 px cap, 16 px border
        }

        /// <summary>Finds or creates the edge and bevel overlay children (stretched, sliced, no raycast, ignored by layout).</summary>
        private static HudArtFrameView EnsureFrame(Transform block, string edgeSprite, string bevelSprite, int index, float pixelsPerUnitMultiplier)
        {
            var frame = block.GetComponent<HudArtFrameView>();
            if (frame == null) frame = block.gameObject.AddComponent<HudArtFrameView>();
            var edge = Overlay(block, EdgeName, edgeSprite, index, pixelsPerUnitMultiplier);
            var bevel = Overlay(block, BevelName, bevelSprite, index + 1, pixelsPerUnitMultiplier);
            Set(frame, "edge", edge);
            Set(frame, "bevel", bevel);
            return frame;
        }

        private static Image Overlay(Transform parent, string name, string sprite, int index, float pixelsPerUnitMultiplier)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
                child = go.transform;
                child.SetParent(parent, false);
            }
            child.SetSiblingIndex(index);
            var rect = (RectTransform)child;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var layout = child.GetComponent<LayoutElement>();
            if (layout == null) layout = child.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            var image = child.GetComponent<Image>();
            SetSprite(image, sprite, pixelsPerUnitMultiplier);
            image.raycastTarget = false;
            return image;
        }

        private static void SetSprite(Image image, string sprite, float pixelsPerUnitMultiplier)
        {
            if (image == null) return;
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Hud + sprite + ".png");
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
        }

        private static Sprite IconSprite(string folder, string id)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Icons + folder + "/" + id + ".png");
            if (sprite == null) Debug.LogError("[UiArt] Missing icon " + folder + "/" + id);
            return sprite;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void Edit(string prefab, Action<GameObject> edit)
        {
            var path = Ui + prefab + ".prefab";
            var go = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(go);
                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var array = so.FindProperty(field);
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
