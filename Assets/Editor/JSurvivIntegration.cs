using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Scoped importer validation. Does not overwrite project render settings or source assets.</summary>
public static class JSurvivIntegration
{
    const string ImportedRoot = "Assets/Imported/JSurviv";
    const string OutputRoot = "Library/JSurvivIntegration";

    [Serializable] public class SceneResult
    {
        public string path;
        public int missingScripts;
        public int brokenObjectReferences;
        public string screenshot;
    }

    [Serializable] public class Report
    {
        public string unityVersion;
        public string pipeline;
        public int convertedMaterials;
        public List<string> materialChanges = new List<string>();
        public List<string> unsupportedMaterials = new List<string>();
        public List<string> unresolvedGuids = new List<string>();
        public List<SceneResult> scenes = new List<SceneResult>();
        public int prefabsChecked;
        public int prefabMissingScripts;
        public bool gameConfigLoaded;
        public bool vfxConfigLoaded;
    }

    [MenuItem("Tools/JSurviv/Convert Imported Materials and Validate")]
    public static void Run()
    {
        Directory.CreateDirectory(OutputRoot);
        var report = new Report { unityVersion = Application.unityVersion,
            pipeline = QualitySettings.renderPipeline == null ? "None" : QualitySettings.renderPipeline.name };
        var previousActiveScene = SceneManager.GetActiveScene();
        try
        {
            ConvertMaterials(report);
            AssetDatabase.SaveAssets();
            AuditGuids(report);
            AuditPrefabs(report);
            report.gameConfigLoaded = Resources.Load<ScriptableObject>("GameConfig") != null;
            report.vfxConfigLoaded = Resources.Load<ScriptableObject>("VfxConfig") != null;
            foreach (var name in new[] { "GameConfig", "GameMain", "GameMainTrial", "CreateMap" })
                InspectScene(ImportedRoot + "/Scripts/Scenes/" + name + ".unity", report);
            InspectScene("Assets/Scenes/SampleScene.unity", report);
        }
        finally
        {
            File.WriteAllText(OutputRoot + "/validation.json", JsonUtility.ToJson(report, true));
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);
        }
        Debug.Log("JSURVIV_VALIDATION_COMPLETE " + JsonUtility.ToJson(report));
        if (Application.isBatchMode && (report.prefabMissingScripts > 0 ||
            report.scenes.Any(scene => scene.missingScripts > 0) || !report.gameConfigLoaded || !report.vfxConfigLoaded))
            EditorApplication.Exit(2);
    }

    static void ConvertMaterials(Report report)
    {
        var upgraders = MaterialUpgrader.FetchAllUpgradersForPipeline(typeof(UniversalRenderPipelineAsset));
        var supported = new HashSet<Material>(MaterialUpgrader.FetchAllUpgradableMaterialsForPipeline(typeof(UniversalRenderPipelineAsset)));
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { ImportedRoot }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.shader == null)
            {
                report.unsupportedMaterials.Add(path + ": missing shader");
                continue;
            }
            var oldShader = material.shader.name;
            if (oldShader.StartsWith("Universal Render Pipeline/") || oldShader.StartsWith("TextMeshPro/") ||
                oldShader.StartsWith("Sprites/") || oldShader.StartsWith("UI/")) continue;
            var backupPath = OutputRoot + "/MaterialBackup/" + path.Substring(ImportedRoot.Length + 1);
            if (!File.Exists(backupPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
                File.Copy(path, backupPath);
            }
            if (supported.Contains(material))
                MaterialUpgrader.Upgrade(material, upgraders, MaterialUpgrader.UpgradeFlags.None);
            if (material.shader.name == oldShader && oldShader.IndexOf("Particle", StringComparison.OrdinalIgnoreCase) >= 0)
                ConvertLegacyParticle(material, oldShader);
            if (material.shader.name != oldShader)
            {
                EditorUtility.SetDirty(material);
                report.convertedMaterials++;
                report.materialChanges.Add(path + ": " + oldShader + " -> " + material.shader.name);
            }
            else if (!material.shader.isSupported)
                report.unsupportedMaterials.Add(path + ": " + oldShader);
        }
    }

    static void ConvertLegacyParticle(Material material, string oldShader)
    {
        var texture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
        var scale = material.HasProperty("_MainTex") ? material.GetTextureScale("_MainTex") : Vector2.one;
        var offset = material.HasProperty("_MainTex") ? material.GetTextureOffset("_MainTex") : Vector2.zero;
        var color = material.HasProperty("_TintColor") ? material.GetColor("_TintColor") :
            material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
        var additive = oldShader.IndexOf("Additive", StringComparison.OrdinalIgnoreCase) >= 0;
        var multiply = oldShader.IndexOf("Multiply", StringComparison.OrdinalIgnoreCase) >= 0;
        material.shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        material.SetTexture("_BaseMap", texture);
        material.SetTextureScale("_BaseMap", scale);
        material.SetTextureOffset("_BaseMap", offset);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Surface", 1);
        material.SetFloat("_Blend", additive ? 2 : multiply ? 3 : 0);
        material.SetFloat("_SrcBlend", (float)(multiply ? BlendMode.DstColor : BlendMode.SrcAlpha));
        material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : multiply ? BlendMode.Zero : BlendMode.OneMinusSrcAlpha));
        material.SetFloat("_ZWrite", 0);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    static void AuditGuids(Report report)
    {
        var knownMissing = new HashSet<string>();
        var pattern = new Regex(@"guid:\s*([0-9a-f]{32})", RegexOptions.Compiled);
        var extensions = new HashSet<string> { ".unity", ".prefab", ".asset", ".mat", ".controller", ".anim" };
        foreach (var path in Directory.EnumerateFiles(ImportedRoot, "*", SearchOption.AllDirectories))
        {
            if (!extensions.Contains(Path.GetExtension(path))) continue;
            foreach (Match match in pattern.Matches(File.ReadAllText(path)))
            {
                var guid = match.Groups[1].Value;
                if (guid.StartsWith("0000000000000000") || !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid))) continue;
                if (knownMissing.Add(guid)) report.unresolvedGuids.Add(path.Replace('\\', '/') + ": " + guid);
            }
        }
    }

    static void AuditPrefabs(Report report)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { ImportedRoot }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab == null) continue;
            report.prefabsChecked++;
            foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                report.prefabMissingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
        }
    }

    static void InspectScene(string path, Report report)
    {
        var scene = SceneManager.GetSceneByPath(path);
        var openedForValidation = !scene.IsValid() || !scene.isLoaded;
        if (openedForValidation) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        var result = new SceneResult { path = path };
        try
        {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                result.missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                foreach (var component in transform.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var iterator = new SerializedObject(component).GetIterator();
                    while (iterator.NextVisible(true))
                        if (iterator.propertyType == SerializedPropertyType.ObjectReference &&
                            iterator.objectReferenceValue == null && iterator.objectReferenceInstanceIDValue != 0)
                            result.brokenObjectReferences++;
                }
            }
        result.screenshot = CaptureScene(scene, Path.GetFileNameWithoutExtension(path));
        report.scenes.Add(result);
        }
        finally
        {
            if (openedForValidation) EditorSceneManager.CloseScene(scene, true);
        }
    }

    static string CaptureScene(Scene scene, string name)
    {
        var camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true))
            .FirstOrDefault(candidate => candidate.enabled);
        if (camera == null) return "No enabled camera";
        var canvases = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
            .Where(canvas => canvas.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var oldCameras = canvases.Select(canvas => canvas.worldCamera).ToArray();
        var target = RenderTexture.GetTemporary(1280, 720, 24);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; }
            Canvas.ForceUpdateCanvases();
            camera.targetTexture = target;
            if (GraphicsSettings.currentRenderPipeline != null || QualitySettings.renderPipeline != null)
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            else
                camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            var output = OutputRoot + "/" + name + ".png";
            File.WriteAllBytes(output, image.EncodeToPNG());
            return output;
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            for (var i = 0; i < canvases.Length; i++) { canvases[i].renderMode = RenderMode.ScreenSpaceOverlay; canvases[i].worldCamera = oldCameras[i]; }
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
