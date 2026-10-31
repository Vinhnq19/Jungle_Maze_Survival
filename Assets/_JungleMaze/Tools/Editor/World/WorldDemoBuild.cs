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
            BuildVisualAssets();
            var runtime="Assets/StreamingAssets/JungleMaze/runtime";Directory.CreateDirectory(runtime);
            foreach(var file in new[]{"world_settings.json","items_catalog.json"})File.Copy("Backend/Shared/GameData/"+file,runtime+"/"+file,true);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=10;camera.transform.position=new Vector3(25,25,-10);camera.backgroundColor=new Color(.08f,.13f,.1f);
            var light=new GameObject("Main Light").AddComponent<Light>();light.type=LightType.Directional;
            new GameObject("World Prototype").AddComponent<WorldDemoController>();
            var hud=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_JungleMaze/Prefabs/UI/Hud.prefab");if(hud!=null)PrefabUtility.InstantiatePrefab(hud);
            var note=new GameObject("Prototype instructions").AddComponent<TextMesh>();note.text="OFFLINE WORLD PREVIEW\nWASD move · F pickup · 1-4 select · 5-8 use\nLeft click attack · R reload · Backspace drop\nSpace debug hit · H debug damage";note.characterSize=.11f;note.fontSize=28;note.anchor=TextAnchor.UpperLeft;note.transform.position=new Vector3(1,49,0);note.color=Color.white;
            EditorSceneManager.SaveScene(scene,"Assets/_JungleMaze/Scenes/WorldDemo.unity");AssetDatabase.SaveAssets();
        }

        /// <summary>Rebuilds sprite-only world assets without replacing the active scene.</summary>
        [MenuItem("Jungle Maze/Phase 2/Build World Visual Assets")]
        public static void BuildVisualAssets()
        {
            var resources="Assets/_JungleMaze/Art/World/Resources";Directory.CreateDirectory(resources);
            var prefabDir="Assets/_JungleMaze/Prefabs/World";Directory.CreateDirectory(prefabDir);
            AssetDatabase.Refresh();
            var set=AssetDatabase.LoadAssetAtPath<WorldVisualSet>(resources+"/WorldVisualSet.asset");
            if(set==null){set=ScriptableObject.CreateInstance<WorldVisualSet>();AssetDatabase.CreateAsset(set,resources+"/WorldVisualSet.asset");}
            var entries=new List<WorldVisualSet.Entry>();var report=new System.Text.StringBuilder("# ART-03 source audit\n\nSprite references and timed sprite keyframes only. No legacy scripts, controllers, float curves or animation events are copied. Damage flashes require replicated HP decrease; broken state requires the server flag. Portal teleport cues require a confirmed source event and are not inferred from proximity.\n\n| Kind | Source | Status |\n|---|---|---|\n");
            var kinds=new[]{WorldObjectKind.Chest,WorldObjectKind.Pot,WorldObjectKind.Bush,WorldObjectKind.Swamp,WorldObjectKind.Lava,WorldObjectKind.Portal,WorldObjectKind.Door,WorldObjectKind.Poison,WorldObjectKind.Supply};
            var paths=new[]{"Destructibles/CHEST1","Destructibles/Bình Hoa","Obstacles/CanGoThrough/Bush","Obstacles/CanGoThrough/POND","Indestructibles/LAVA","Obstacles/CanGoThrough/PORTAL","","Obstacles/CanGoThrough/HUNT_TRAP","Destructibles/DragonEgg"};
            for(var i=0;i<kinds.Length;i++)
            {
                var path=paths[i].Length==0?"":"Assets/Imported/JSurviv/Prefabs/"+paths[i]+".prefab";
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);var sprite=source!=null?source.GetComponentInChildren<SpriteRenderer>(true):null;
                var entry=new WorldVisualSet.Entry{Kind=kinds[i],Sprite=sprite!=null?sprite.sprite:null,Source=path};
                var animationRoot="Assets/Imported/JSurviv/AnimationClips/Obstacle/";
                if(kinds[i]==WorldObjectKind.Swamp)entry.Idle=ExtractFrames(animationRoot+"POND/POND.idle.anim",true);
                if(kinds[i]==WorldObjectKind.Lava)entry.Idle=ExtractFrames(animationRoot+"LAVA/LAVA.idle.anim",true);
                if(kinds[i]==WorldObjectKind.Portal)entry.Idle=ExtractFrames(animationRoot+"PORTAL/PORTAL.idle.anim",true);
                if(kinds[i]==WorldObjectKind.Door)
                {
                    path="Assets/Imported/JSurviv/Sprites/OBSTACLES/Tàn tích bỏ hoang/Abandoned_door_";
                    entry.Open=ExtractFrames(path+"open.anim",false);entry.Close=ExtractFrames(path+"close.anim",false);
                    var idle=ExtractFrames(path+"idle.anim",false);entry.Sprite=idle?.Sample(0);entry.Source=path+"idle/open/close.anim";
                }
                if(kinds[i]==WorldObjectKind.Pot)
                {
                    var debris=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported/JSurviv/Prefabs/Indestructibles/Mảnh sành điệu.prefab");
                    var debrisRenderer=debris!=null?debris.GetComponentInChildren<SpriteRenderer>(true):null;
                    entry.BrokenSprite=debrisRenderer!=null?debrisRenderer.sprite:null;
                }
                if(entry.Sprite!=null)
                {
                    entries.Add(entry);
                    var go=new GameObject(kinds[i]+" Visual");var sr=go.AddComponent<SpriteRenderer>();sr.sprite=entry.Sprite;
                    PrefabUtility.SaveAsPrefabAsset(go,prefabDir+"/"+kinds[i]+"Visual.prefab");Object.DestroyImmediate(go);
                }
                var status=entry.Sprite==null?"Graybox; source missing/not verified":"Static sprite; replicated damage flash";
                if(entry.Idle!=null)status="Animated sprite curve: "+entry.Idle.Sprites.Length+" frames";
                if(entry.Open!=null&&entry.Close!=null)status="Server-state open/close sprite curves; legacy callback events discarded";
                if(kinds[i]==WorldObjectKind.Pot)status+="; verified broken-pot debris sprite only (no poison script)";
                if(kinds[i]==WorldObjectKind.Chest)status+="; broken artwork absent, hide after confirmed break";
                if(kinds[i]==WorldObjectKind.Poison)status+="; hunt-trap artwork is a provisional marker, poison-specific art unverified";
                if(kinds[i]==WorldObjectKind.Supply)status+="; DragonEgg is a provisional supply visual, DRG-01 is outside scope";
                report.Append('|').Append(kinds[i]).Append('|').Append(entry.Source).Append('|').Append(status).Append("|\n");
            }
            set.Entries=entries.ToArray();EditorUtility.SetDirty(set);
            Directory.CreateDirectory("Docs/Art");File.WriteAllText("Docs/Art/ART03_Source_Audit.vi.md",report.ToString());
            AssetDatabase.SaveAssets();
        }

        private static WorldVisualSet.Frames ExtractFrames(string path,bool loop)
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(clip==null)return null;
            foreach(var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if(binding.type!=typeof(SpriteRenderer)||binding.propertyName!="m_Sprite")continue;
                var keys=AnimationUtility.GetObjectReferenceCurve(clip,binding);var sprites=new List<Sprite>();var times=new List<float>();
                foreach(var key in keys)if(key.value is Sprite frame){sprites.Add(frame);times.Add(key.time);}
                if(sprites.Count>0)return new WorldVisualSet.Frames{Sprites=sprites.ToArray(),Times=times.ToArray(),Duration=Mathf.Max(clip.length,.01f),Loop=loop};
            }
            return null;
        }
    }
}
