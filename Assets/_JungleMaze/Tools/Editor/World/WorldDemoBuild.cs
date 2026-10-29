using System.Collections.Generic;
using System.IO;
using JungleMaze.Presentation.World.Phase2;
using JungleMaze.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JungleMaze.EditorTools
{
    public static class WorldDemoBuild
    {
        [MenuItem("Jungle Maze/Phase 2/Build World Demo")]
        public static void Build()
        {
            var resources="Assets/_JungleMaze/Art/World/Resources";Directory.CreateDirectory(resources);
            var prefabDir="Assets/_JungleMaze/Prefabs/World";Directory.CreateDirectory(prefabDir);
            var runtime="Assets/StreamingAssets/JungleMaze/runtime";Directory.CreateDirectory(runtime);
            foreach(var file in new[]{"world_settings.json","items_catalog.json"})File.Copy("Backend/Shared/GameData/"+file,runtime+"/"+file,true);
            AssetDatabase.Refresh();
            var set=AssetDatabase.LoadAssetAtPath<WorldVisualSet>(resources+"/WorldVisualSet.asset");
            if(set==null){set=ScriptableObject.CreateInstance<WorldVisualSet>();AssetDatabase.CreateAsset(set,resources+"/WorldVisualSet.asset");}
            var entries=new List<WorldVisualSet.Entry>();var report=new System.Text.StringBuilder("# ART-03 source audit\n\nPrototype visual extraction. Original JSurviv scripts/prefabs are not instantiated.\n\n| Kind | Source | Status |\n|---|---|---|\n");
            var kinds=new[]{WorldObjectKind.Chest,WorldObjectKind.Pot,WorldObjectKind.Bush,WorldObjectKind.Swamp,WorldObjectKind.Lava,WorldObjectKind.Portal,WorldObjectKind.Door,WorldObjectKind.Poison,WorldObjectKind.Supply};
            var paths=new[]{"Destructibles/CHEST1","Destructibles/Bình Hoa","Obstacles/CanGoThrough/Bush","Obstacles/CanGoThrough/POND","Indestructibles/LAVA","Obstacles/CanGoThrough/PORTAL","","Obstacles/CanGoThrough/HUNT_TRAP","Destructibles/DragonEgg"};
            for(var i=0;i<kinds.Length;i++)
            {
                var path=paths[i].Length==0?"":"Assets/Imported/JSurviv/Prefabs/"+paths[i]+".prefab";
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);var sprite=source!=null?source.GetComponentInChildren<SpriteRenderer>(true):null;
                if(sprite!=null&&sprite.sprite!=null)
                {
                    entries.Add(new WorldVisualSet.Entry{Kind=kinds[i],Sprite=sprite.sprite});
                    var go=new GameObject(kinds[i]+" Visual");var sr=go.AddComponent<SpriteRenderer>();sr.sprite=sprite.sprite;
                    PrefabUtility.SaveAsPrefabAsset(go,prefabDir+"/"+kinds[i]+"Visual.prefab");Object.DestroyImmediate(go);
                }
                report.Append('|').Append(kinds[i]).Append('|').Append(path.Length==0?"No suitable source verified":path).Append('|')
                    .Append(sprite!=null&&sprite.sprite!=null?"Sprite extracted; animation/recognition still needs review":"Graybox; source missing/not verified").Append("|\n");
            }
            set.Entries=entries.ToArray();EditorUtility.SetDirty(set);
            Directory.CreateDirectory("Docs/Art");File.WriteAllText("Docs/Art/ART03_Source_Audit.vi.md",report.ToString());
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=10;camera.transform.position=new Vector3(25,25,-10);camera.backgroundColor=new Color(.08f,.13f,.1f);
            var light=new GameObject("Main Light").AddComponent<Light>();light.type=LightType.Directional;
            new GameObject("World Prototype").AddComponent<WorldDemoController>();
            var hud=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_JungleMaze/Prefabs/UI/Hud.prefab");if(hud!=null)PrefabUtility.InstantiatePrefab(hud);
            var note=new GameObject("Prototype instructions").AddComponent<TextMesh>();note.text="WORLD PROTOTYPE — pending balance approval\nWASD move · F pickup · 1-4 select · 5-8 use\nBackspace drop · Space confirmed-hit fixture · H damage fixture";note.characterSize=.11f;note.fontSize=28;note.anchor=TextAnchor.UpperLeft;note.transform.position=new Vector3(1,49,0);note.color=Color.white;
            EditorSceneManager.SaveScene(scene,"Assets/_JungleMaze/Scenes/WorldDemo.unity");AssetDatabase.SaveAssets();
        }
    }
}
