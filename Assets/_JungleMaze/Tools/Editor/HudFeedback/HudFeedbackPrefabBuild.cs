using System;
using JungleMaze.Domain.Hud;
using JungleMaze.Presentation.UI.Hud;
using JungleMaze.Presentation.UI.Hud.Feedback;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace JungleMaze.EditorTools
{
    /// <summary>Repeatable prefab assembly for the approved feedback layout, invoked through Unity MCP.</summary>
    public static class HudFeedbackPrefabBuild
    {
        private const string Ui="Assets/_JungleMaze/Prefabs/UI/";
        private static TMP_FontAsset TextFont=>AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_JungleMaze/Art/Fonts/TMP/HudText SDF.asset");
        private static TMP_FontAsset NumberFont=>AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_JungleMaze/Art/Fonts/TMP/HudNumber SDF.asset");
        private static Sprite Sprite(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_JungleMaze/Art/Hud/"+name+".png");
        /// <summary>Raised after a rebuild so art bindings (UiArt editor tool) are re-applied to the regenerated prefabs.</summary>
        public static event Action AfterBuild;
        [MenuItem("Jungle Maze/HUD/Build Feedback Layer")]
        public static void Build()
        {
            BuildChildPrefabs();BuildCatalog();BuildSlot();
            var go=PrefabUtility.LoadPrefabContents(Ui+"Hud.prefab");
            try{
                var core=go.GetComponent<HudRoot>();var old=go.GetComponent<HudFeedbackRoot>();if(old!=null)Object.DestroyImmediate(old);
                foreach(var name in new[]{"EffectsRow","PickupFeed","DangerBanner","FeedbackOverlay"}){var child=go.transform.Find(name);if(child!=null)Object.DestroyImmediate(child.gameObject);}
                var previousUse=core.Quickbar.transform.Find("UseActionStrip");if(previousUse!=null)Object.DestroyImmediate(previousUse.gameObject);
                var feedback=go.AddComponent<HudFeedbackRoot>();var root=(RectTransform)go.transform;
                var palette=new SerializedObject(core);palette.FindProperty("palette").FindPropertyRelative("Green").colorValue=HudPalette.Default.Green;palette.FindProperty("palette").FindPropertyRelative("HitMarker").colorValue=Color.white;palette.FindProperty("palette").FindPropertyRelative("DangerFill").colorValue=HudPalette.Default.DangerFill;palette.ApplyModifiedPropertiesWithoutUndo();
                var stripGroup=core.Quickbar.WeaponStrip.GetComponent<CanvasGroup>();if(stripGroup==null)stripGroup=core.Quickbar.WeaponStrip.gameObject.AddComponent<CanvasGroup>();stripGroup.alpha=1;Set(core.Quickbar,"weaponStripGroup",stripGroup);
                // The original slot prefab is nested in Hud; sync its new serialized overlay reference on each instance.
                for(var i=0;i<core.Quickbar.SlotViewCount;i++){
                    var slot=core.Quickbar.GetSlotView(i);var frame=slot.transform.Find("InUseFrame");
                    if(frame==null){var rect=Rect("InUseFrame",slot.transform,Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(64,64));frame=rect;var img=Image(rect,"HudRound6Stroke2");img.enabled=false;}
                    Set(slot,"inUseFrame",frame.GetComponent<Image>());
                }
                var effects=Rect("EffectsRow",root,Vector2.zero,Vector2.zero,new Vector2(24,196),new Vector2(294,58));effects.gameObject.AddComponent<Canvas>();var row=effects.gameObject.AddComponent<EffectRowView>();var chips=new Object[6];
                for(var i=0;i<6;i++){var child=Instantiate("HudEffectChip",effects);child.anchoredPosition=new Vector2(i*50,0);chips[i]=child.GetComponent<EffectChipView>();}SetArray(row,"chips",chips);
                var pickup=Rect("PickupFeed",root,new Vector2(1,0),new Vector2(1,0),new Vector2(-24,24),new Vector2(141,132));var feed=pickup.gameObject.AddComponent<PickupFeedView>();var toasts=new Object[3];
                for(var i=0;i<3;i++){var child=Instantiate("HudPickupToast",pickup);child.anchorMin=child.anchorMax=new Vector2(1,0);child.pivot=new Vector2(1,0);child.anchoredPosition=new Vector2(0,(2-i)*37);toasts[i]=child.GetComponent<PickupToastView>();}SetArray(feed,"rows",toasts);
                var overflow=Rect("Overflow",pickup,new Vector2(1,1),new Vector2(1,1),Vector2.zero,new Vector2(92,21));var obg=Image(overflow,"HudRound6");var og=Group(overflow);var ol=Text("Label",overflow,4,2,84,17,"+2 món khác",TextFont);Set(feed,"overflowGroup",og);Set(feed,"overflowLabel",ol);Set(feed,"overflowBackground",obg);
                var use=Rect("UseActionStrip",core.Quickbar.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(53,-6),new Vector2(458,32));use.gameObject.AddComponent<Canvas>();var ubg=Image(use,"HudRound6");var ug=Group(use);var uv=use.gameObject.AddComponent<UseActionView>();
                var ul=Text("Label",use,15,7,132,18,"Đang dùng",TextFont);ul.characterSpacing=-1;var us=Text("Seconds",use,306,7,34,18,"0.5s",NumberFont);var hint=Text("Hint",use,344,8,112,18,"Di chuyển để hủy",TextFont);var ufill=Bar(use,156,13,140,6);
                Set(uv,"group",ug);Set(uv,"label",ul);Set(uv,"seconds",us);Set(uv,"hint",hint);Set(uv,"fill",ufill);Set(uv,"background",ubg);
                var danger=Rect("DangerBanner",root,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(.5f,-24),new Vector2(301,40));var dbg=Image(danger,"HudRound6");var dg=Group(danger);var dv=danger.gameObject.AddComponent<DangerBannerView>();var border=Rect("Border",danger,Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(301,40));var db=Image(border,"HudRound6Stroke1");
                var dl=Text("Label",danger,12,8,110,24,"! Bom nổ sau",TextFont);var ds=Text("Seconds",danger,122,8,28,24,"2s",NumberFont);var suffix=Text("Direction",danger,152,9,137,22,"· hướng Tây Nam, 4 ô",TextFont);
                Set(dv,"group",dg);Set(dv,"label",dl);Set(dv,"timer",ds);Set(dv,"suffix",suffix);Set(dv,"background",dbg);Set(dv,"border",db);
                var overlay=Rect("FeedbackOverlay",root,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(1280,720));overlay.gameObject.AddComponent<Canvas>();
                var hit=Rect("HitMarker",overlay,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(56,56));var hg=Group(hit);var hv=hit.gameObject.AddComponent<HitMarkerView>();var ticks=new Object[4];
                for(var i=0;i<4;i++){var signX=i%2==0?-1:1;var signY=i<2?-1:1;var tick=Rect("Tick"+i,hit,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(signX*17,signY*17),new Vector2(14,3));tick.localEulerAngles=new Vector3(0,0,signX*signY*45);ticks[i]=Image(tick,null);}Set(hv,"group",hg);SetArray(hv,"ticks",ticks);
                var arcs=new Object[4];for(var i=0;i<4;i++){var child=Instantiate("HudDamageArc",overlay);child.anchorMin=child.anchorMax=child.pivot=new Vector2(.5f,.5f);child.anchoredPosition=Vector2.zero;arcs[i]=child.GetComponent<DamageArcView>();}
                var arrows=new Object[3];for(var i=0;i<3;i++){var child=Instantiate("HudDangerArrow",overlay);child.anchorMin=child.anchorMax=child.pivot=new Vector2(.5f,.5f);child.anchoredPosition=Vector2.zero;arrows[i]=child.GetComponent<DangerArrowView>();}
                Set(feedback,"catalog",AssetDatabase.LoadAssetAtPath<HudEffectCatalog>(Ui+"HudEffectCatalog.asset"));Set(feedback,"useAction",uv);Set(feedback,"effectRow",row);Set(feedback,"pickupFeed",feed);Set(feedback,"hitMarker",hv);Set(feedback,"dangerBanner",dv);SetArray(feedback,"damageArcs",arcs);SetArray(feedback,"dangerArrows",arrows);
                foreach(var graphic in go.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
                core.ApplyPalette();PrefabUtility.SaveAsPrefabAsset(go,Ui+"Hud.prefab");
            }finally{PrefabUtility.UnloadPrefabContents(go);}
            AssetDatabase.SaveAssets();
            AfterBuild?.Invoke();
        }
        private static void BuildSlot()
        {
            var slot=PrefabUtility.LoadPrefabContents(Ui+"HudSlot.prefab");try{var frame=slot.transform.Find("InUseFrame");if(frame!=null)Object.DestroyImmediate(frame.gameObject);var r=Rect("InUseFrame",slot.transform,Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(64,64));var img=Image(r,"HudRound6Stroke2");img.color=HudPalette.Default.Amber;img.enabled=false;Set(slot.GetComponent<QuickbarSlotView>(),"inUseFrame",img);PrefabUtility.SaveAsPrefabAsset(slot,Ui+"HudSlot.prefab");}finally{PrefabUtility.UnloadPrefabContents(slot);}
        }
        private static void BuildCatalog()
        {
            var path=Ui+"HudEffectCatalog.asset";var catalog=AssetDatabase.LoadAssetAtPath<HudEffectCatalog>(path);if(catalog==null){catalog=ScriptableObject.CreateInstance<HudEffectCatalog>();AssetDatabase.CreateAsset(catalog,path);}
            var so=new SerializedObject(catalog);var entries=so.FindProperty("entries");entries.arraySize=8;var labels=new[]{"Adrenaline","Độc","Chậm","Ẩn","Ngất","Trói","Đảo","Miễn"};for(var i=0;i<8;i++){var e=entries.GetArrayElementAtIndex(i);e.FindPropertyRelative("Kind").enumValueIndex=i+1;e.FindPropertyRelative("Label").stringValue=labels[i];}so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void BuildChildPrefabs()
        {
            var chip=Rect("HudEffectChip",null,Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(44,58));try{var g=Group(chip);var v=chip.gameObject.AddComponent<EffectChipView>();var box=Rect("Box",chip,new Vector2(0,1),new Vector2(0,1),Vector2.zero,new Vector2(44,40));var bg=Image(box,"HudRound6");var border=Rect("Border",box,Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(44,40));var bi=Image(border,"HudRound6Stroke1");var icon=Rect("Icon",box,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(20,20));var ii=Image(icon,null);ii.enabled=false;var label=Text("Label",box,2,9,40,20,"Độc",TextFont);label.alignment=TextAlignmentOptions.Center;var seconds=Text("Seconds",chip,0,42,44,16,"3s",NumberFont);seconds.alignment=TextAlignmentOptions.Center;var fill=Bar(box,4,34,36,3);Set(v,"group",g);Set(v,"background",bg);Set(v,"border",bi);Set(v,"icon",ii);Set(v,"label",label);Set(v,"seconds",seconds);Set(v,"fill",fill);PrefabUtility.SaveAsPrefabAsset(chip.gameObject,Ui+"HudEffectChip.prefab");}finally{Object.DestroyImmediate(chip.gameObject);}
            var toast=Rect("HudPickupToast",null,Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(141,31));try{var bg=Image(toast,"HudRound6");var g=Group(toast);var v=toast.gameObject.AddComponent<PickupToastView>();var plus=Text("Plus",toast,6,6,14,19,"+",TextFont);var icon=Rect("Icon",toast,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-6.5f),new Vector2(18,18));var ii=Image(icon,"HudRound6");var name=Text("Name",toast,48,.75f,64,29.5f,"God Leaf",TextFont);name.enableWordWrapping=true;name.lineSpacing=-30;var count=Text("Count",toast,115,7,24,17,"×2",NumberFont);Set(v,"background",bg);Set(v,"group",g);Set(v,"plus",plus);Set(v,"icon",ii);Set(v,"label",name);Set(v,"count",count);PrefabUtility.SaveAsPrefabAsset(toast.gameObject,Ui+"HudPickupToast.prefab");}finally{Object.DestroyImmediate(toast.gameObject);}
            var arc=Rect("HudDamageArc",null,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(22,58));try{var graphic=Rect("Graphic",arc,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(16,54));var img=Image(graphic,"HudArcFilled");img.type=UnityEngine.UI.Image.Type.Simple;var g=Group(arc);var v=arc.gameObject.AddComponent<DamageArcView>();Set(v,"group",g);Set(v,"image",img);PrefabUtility.SaveAsPrefabAsset(arc.gameObject,Ui+"HudDamageArc.prefab");}finally{Object.DestroyImmediate(arc.gameObject);}
            var arrow=Rect("HudDangerArrow",null,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(30,30));try{var g=Group(arrow);var v=arrow.gameObject.AddComponent<DangerArrowView>();var shape=Rect("Arrow",arrow,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(16,24));var img=Image(shape,"HudArrowOutline");img.type=UnityEngine.UI.Image.Type.Simple;var timer=Text("Seconds",arrow,32,7,30,16,"2s",NumberFont);Set(v,"group",g);Set(v,"arrow",shape);Set(v,"seconds",timer);PrefabUtility.SaveAsPrefabAsset(arrow.gameObject,Ui+"HudDangerArrow.prefab");}finally{Object.DestroyImmediate(arrow.gameObject);}
        }
        private static RectTransform Instantiate(string name,Transform parent){var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Ui+name+".prefab"),parent);return (RectTransform)go.transform;}
        private static RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 pivot,Vector2 pos,Vector2 size){var go=new GameObject(name,typeof(RectTransform));var r=(RectTransform)go.transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=anchor;r.pivot=pivot;r.anchoredPosition=pos;r.sizeDelta=size;return r;}
        private static CanvasGroup Group(RectTransform r){var g=r.gameObject.AddComponent<CanvasGroup>();g.alpha=0;g.interactable=false;g.blocksRaycasts=false;return g;}
        private static Image Image(RectTransform r,string sprite){var i=r.gameObject.AddComponent<Image>();i.raycastTarget=false;if(sprite!=null){i.sprite=Sprite(sprite);i.type=UnityEngine.UI.Image.Type.Sliced;}return i;}
        private static TMP_Text Text(string name,Transform parent,float x,float y,float w,float h,string text,TMP_FontAsset font){var r=Rect(name,parent,new Vector2(0,1),new Vector2(0,1),new Vector2(x,-y),new Vector2(w,h));var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=13;t.enableAutoSizing=false;t.enableWordWrapping=false;t.overflowMode=TextOverflowModes.Ellipsis;t.raycastTarget=false;t.text=text;t.alignment=TextAlignmentOptions.MidlineLeft;return t;}
        private static RectTransform Bar(Transform parent,float x,float y,float w,float h){var track=Rect("Track",parent,new Vector2(0,1),new Vector2(0,1),new Vector2(x,-y),new Vector2(w,h));var image=Image(track,null);image.color=HudPalette.Default.Track;var fill=Rect("Fill",track,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);fill.anchorMax=Vector2.one;fill.offsetMin=fill.offsetMax=Vector2.zero;Image(fill,null);return fill;}
        private static void Set(Object target,string field,Object value){var so=new SerializedObject(target);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
        private static void SetArray(Object target,string field,Object[] values){var so=new SerializedObject(target);var array=so.FindProperty(field);array.arraySize=values.Length;for(var i=0;i<values.Length;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();}
    }
}
