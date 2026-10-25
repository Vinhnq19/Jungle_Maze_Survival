#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using JungleMaze.App.Hud;
using JungleMaze.App.Hud.Feedback;
using JungleMaze.App.Hud.Mocks;
using JungleMaze.App.Hud.State;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Hud;
using JungleMaze.Infrastructure.Config;
using JungleMaze.Presentation.UI.Hud;
using JungleMaze.Presentation.World;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace JungleMaze.Tests.PlayMode
{
    /// <summary>Feedback acceptance on the real HUD prefab, with deterministic source clocks.</summary>
    public sealed class HudFeedbackAcceptanceTests
    {
        private const string Path="Assets/_JungleMaze/Prefabs/UI/Hud.prefab";
        private readonly List<Object> _objects=new List<Object>();
        private HudStateStore _hud;private InventoryStateStore _inventory;private EffectStateStore _effects;
        private BalanceConfigProvider _balance;private HudRoot _core;private HudFeedbackRoot _feedback;private HudFeedbackScenarios _scenarios;
        private float _timeScale;private RectTransform _canvas;
        [SetUp] public void SetUp()
        {
            _timeScale=Time.timeScale;Time.timeScale=0;
            _balance=new BalanceConfigProvider(Application.streamingAssetsPath,null);Assert.IsNull(_balance.LastError);
            var bus=new EventBus();_hud=new HudStateStore(bus,_balance);_inventory=new InventoryStateStore(bus,_balance);_effects=new EffectStateStore(bus);
            _scenarios=new HudFeedbackScenarios(new MockHudState(_hud,_balance),new MockInventoryState(_inventory,_balance),new MockEffectState(_effects),_inventory);
        }
        [TearDown] public void TearDown(){Time.timeScale=_timeScale;for(var i=_objects.Count-1;i>=0;i--)if(_objects[i]!=null)Object.DestroyImmediate(_objects[i]);_objects.Clear();}
        private T Track<T>(T item) where T:Object{_objects.Add(item);return item;}
        private IEnumerator Spawn()
        {
            var go=Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path)));_core=go.GetComponent<HudRoot>();_feedback=go.GetComponent<HudFeedbackRoot>();
            _core.Construct(_hud,_inventory,_effects,new NullItemIconProvider());_feedback.Construct(_hud,_inventory,_effects);
            yield return null;Assert.IsNotNull(_core.Presenter);Assert.IsNotNull(_feedback.Presenter);_core.enabled=false;_feedback.enabled=false;
            go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;go.GetComponent<CanvasScaler>().enabled=false;
            _canvas=(RectTransform)go.transform;_canvas.sizeDelta=new Vector2(1280,720);_canvas.position=Vector3.zero;_canvas.rotation=Quaternion.identity;_canvas.localScale=Vector3.one;
            Tick(0);Canvas.ForceUpdateCanvases();
        }
        private void Tick(float dt,float aimX=0,float aimY=0){_hud.Tick(dt);_inventory.Tick(dt);_effects.Tick(dt);_scenarios.Tick(dt);_core.Presenter.Tick(dt);_feedback.Presenter.Tick(dt,aimX,aimY);Canvas.ForceUpdateCanvases();}
        private Rect Measure(Transform target)
        {
            var corners=new Vector3[4];((RectTransform)target).GetWorldCorners(corners);var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            foreach(var corner in corners){var local=_canvas.InverseTransformPoint(corner);min=Vector2.Min(min,local);max=Vector2.Max(max,local);}
            return new Rect(min.x-_canvas.rect.xMin,_canvas.rect.yMax-max.y,max.x-min.x,max.y-min.y);
        }
        private static void RectIs(Rect expected,Rect actual,string label){Assert.That(actual.x,Is.EqualTo(expected.x).Within(2),label+" x");Assert.That(actual.y,Is.EqualTo(expected.y).Within(2),label+" y");Assert.That(actual.width,Is.EqualTo(expected.width).Within(2),label+" width");Assert.That(actual.height,Is.EqualTo(expected.height).Within(2),label+" height");}
        private Rect[] CoreRects(){var list=new List<Rect>{Measure(_core.Vitals.transform),Measure(_core.Quickbar.transform),Measure(_core.Minimap.transform),Measure(_core.MatchStatus.transform)};for(var i=0;i<8;i++)list.Add(Measure(_core.Quickbar.GetSlotView(i).transform));return list.ToArray();}
        private Rect[] FeedbackRects()=>new[]{Measure(_feedback.EffectRow.transform),Measure(_feedback.PickupFeed.transform),Measure(_feedback.DangerBanner.transform),Measure(_feedback.UseAction.transform)};
        private static float Overlap(Rect a,Rect b)=>Mathf.Max(0,Mathf.Min(a.xMax,b.xMax)-Mathf.Max(a.xMin,b.xMin))*Mathf.Max(0,Mathf.Min(a.yMax,b.yMax)-Mathf.Max(a.yMin,b.yMin));
        private static int Visible<T>(T[] views) where T:Component{var n=0;foreach(var view in views)if(view.GetComponent<CanvasGroup>().alpha>0)n++;return n;}
        [UnityTest] public IEnumerator FeedbackLayout_BlocksMatchFigma_At1280x720()
        {
            yield return Spawn();RectIs(HudFigmaV2Feedback.Effects,Measure(_feedback.EffectRow.transform),"effects");RectIs(HudFigmaV2Feedback.Pickup,Measure(_feedback.PickupFeed.transform),"pickup");RectIs(HudFigmaV2Feedback.Banner,Measure(_feedback.DangerBanner.transform),"banner");RectIs(HudFigmaV2Feedback.Use,Measure(_feedback.UseAction.transform),"use");
            for(var i=0;i<6;i++)RectIs(new Rect(24+i*50,466,44,58),Measure(_feedback.EffectRow.Chips[i].transform),"chip"+i);
            _hud.NotifyPickup("GOD_LEAF",1);_hud.NotifyPickup("SPIRIT_TEAR",1);_hud.NotifyPickup("MERMAID_TAIL",1);_hud.NotifyPickup("UNICORN_BLOOD",1);_hud.NotifyPickup("ADRENALINE",1);Tick(0);
            for(var i=0;i<3;i++)RectIs(new Rect(1115,591+i*37,141,31),Measure(_feedback.PickupFeed.Rows[i].transform),"toast"+i);
            RectIs(new Rect(1164,564,92,21),Measure(_feedback.PickupFeed.OverflowGroup.transform),"overflow");
        }
        [UnityTest] public IEnumerator FeedbackLayout_ScalesAt1080And1440_WithSafeMargins()
        {
            yield return Spawn();var roots=new[]{_feedback.EffectRow.transform,_feedback.PickupFeed.transform,_feedback.DangerBanner.transform,_feedback.UseAction.transform};var expected=FeedbackRects();
            foreach(var height in new[]{1080f,1440f}){var scale=height/720;_canvas.localScale=Vector3.one*scale;Canvas.ForceUpdateCanvases();for(var i=0;i<roots.Length;i++){RectIs(expected[i],Measure(roots[i]),roots[i].name);var corners=new Vector3[4];((RectTransform)roots[i]).GetWorldCorners(corners);Assert.That(corners[2].x-corners[0].x,Is.EqualTo(expected[i].width*scale).Within(expected[i].width*scale*.01f));}var canvasCorners=new Vector3[4];var pickupCorners=new Vector3[4];_canvas.GetWorldCorners(canvasCorners);((RectTransform)_feedback.PickupFeed.transform).GetWorldCorners(pickupCorners);Assert.That(canvasCorners[2].x-pickupCorners[2].x,Is.EqualTo(24*scale).Within(.01));Assert.That(pickupCorners[0].y-canvasCorners[0].y,Is.EqualTo(24*scale).Within(.01));}
        }
        [UnityTest] public IEnumerator FeedbackLayout_NoOverlapWithCoreAndCenter()
        {
            yield return Spawn();var fixedRects=FeedbackRects();var core=new[]{Measure(_core.Vitals.transform),new Rect(358,612,564,84),Measure(_core.Minimap.transform),Measure(_core.MatchStatus.transform),new Rect(24,24,171,90),HudFigmaV2Feedback.Center};
            foreach(var a in fixedRects)foreach(var b in core)Assert.AreEqual(0,Overlap(a,b),a+" versus "+b);Assert.LessOrEqual(Measure(_feedback.UseAction.transform).yMax,604);
        }
        [UnityTest] public IEnumerator Use_GodLeafJsonProgress_HintAndSlotFrame_SelectedKept()
        {
            yield return Spawn();_inventory.SetConsumableSlot(0,"GOD_LEAF",6);_inventory.SetActiveSlot(4);_inventory.BeginUse(4,"GOD_LEAF",.5f,.5f,true);Tick(0);
            Assert.AreEqual(1,_feedback.UseAction.Group.alpha);Assert.AreEqual("Đang dùng God Leaf",_feedback.UseAction.Label.GetParsedText());Assert.AreEqual("0.5s",_feedback.UseAction.Seconds.GetParsedText());Assert.AreEqual(0,_feedback.UseAction.Fill.anchorMax.x);Assert.True(_feedback.UseAction.Hint.enabled);
            Assert.True(_core.Quickbar.GetSlotView(4).InUseFrame.enabled);Assert.AreEqual(SlotVisual.Selected,_core.Quickbar.GetSlotView(4).Visual);Assert.AreEqual(_core.Palette.Amber,_core.Quickbar.GetSlotView(4).InUseFrame.color);Assert.AreEqual(0,_core.Quickbar.WeaponStrip.GetComponent<CanvasGroup>().alpha);
            Tick(.25f);Assert.That(_feedback.UseAction.Fill.anchorMax.x,Is.EqualTo(.5f).Within(.05f));Assert.AreEqual("0.3s",_feedback.UseAction.Seconds.GetParsedText());var fillRect=Measure(_feedback.UseAction.Fill.parent);var timeRect=Measure(_feedback.UseAction.Seconds.transform);
            _inventory.BeginUse(4,"GOD_LEAF",.5f,.25f,false);Tick(0);Assert.False(_feedback.UseAction.Hint.enabled);Assert.AreEqual(fillRect,Measure(_feedback.UseAction.Fill.parent));Assert.AreEqual(timeRect,Measure(_feedback.UseAction.Seconds.transform));
        }
        [UnityTest] public IEnumerator Use_CancelAndComplete_ReturnWeaponStripAndKeepCounts()
        {
            yield return Spawn();var strip=Measure(_core.Quickbar.WeaponStrip);_inventory.SetConsumableSlot(0,"GOD_LEAF",6);_inventory.BeginUse(4,"GOD_LEAF",.5f,.5f,true);Tick(0);_inventory.CancelUse("GOD_LEAF");Tick(0);
            Assert.AreEqual("Đã hủy",_feedback.UseAction.Label.GetParsedText());Assert.AreEqual(_core.Palette.Faint,_feedback.UseAction.Label.color);Tick(.59f);Assert.AreEqual(1,_feedback.UseAction.Group.alpha);Tick(.01f);Assert.AreEqual(0,_feedback.UseAction.Group.alpha);Assert.AreEqual(strip,Measure(_core.Quickbar.WeaponStrip));
            _inventory.BeginUse(4,"GOD_LEAF",.5f,.5f,true);Tick(.5f);_inventory.CompleteUse("GOD_LEAF");Tick(0);Assert.AreEqual("Xong",_feedback.UseAction.Label.GetParsedText());Assert.AreEqual(1,_feedback.UseAction.Fill.anchorMax.x);Tick(.25f);Assert.AreEqual(0,_feedback.UseAction.Group.alpha);Assert.AreEqual(1,_core.Quickbar.WeaponStrip.GetComponent<CanvasGroup>().alpha);Assert.AreEqual(6,_inventory.GetSlot(4).Count);Assert.AreEqual(100,_hud.Hp);
        }
        [UnityTest] public IEnumerator Effects_PositionsColorsCountdownUntimedAndAdrenaline()
        {
            yield return Spawn();_effects.Apply(EffectKind.Poison,3,3);_effects.Apply(EffectKind.Invisible,5,5);_effects.Apply(EffectKind.Adrenaline,8,8);Tick(0);
            var first=_feedback.EffectRow.Chips[0];var second=_feedback.EffectRow.Chips[1];Assert.AreEqual(2,Visible(_feedback.EffectRow.Chips));Assert.AreEqual("Độc",first.Label.GetParsedText());Assert.AreEqual("Ẩn",second.Label.GetParsedText());Assert.AreEqual(_core.Palette.Red,first.Border.color);Assert.AreEqual(_core.Palette.Green,second.Border.color);Assert.AreEqual("3s",first.Seconds.GetParsedText());
            Tick(1);Assert.AreEqual("2s",first.Seconds.GetParsedText());Assert.That(first.Fill.anchorMax.x,Is.EqualTo(2f/3).Within(.02f));_effects.Apply(EffectKind.Slow,0,0);Tick(0);var untimed=_feedback.EffectRow.Chips[1];Assert.AreEqual("Chậm",untimed.Label.GetParsedText());Assert.False(untimed.Seconds.enabled);Assert.False(untimed.Fill.gameObject.activeSelf);
            for(var i=0;i<300;i++)Tick(1f/30);Assert.AreEqual(1,Visible(_feedback.EffectRow.Chips));Assert.AreEqual("Chậm",first.Label.GetParsedText());_effects.Remove(EffectKind.Slow);for(var i=0;i<10;i++)Tick(1f/30);Assert.AreEqual(0,Visible(_feedback.EffectRow.Chips));
        }
        [UnityTest] public IEnumerator Pickup_NewestAtBottom_MergesAndDoesNotWriteInventory()
        {
            yield return Spawn();var hp=_hud.Hp;_hud.NotifyPickup("RUBBER_GUN",1);Tick(0);Assert.AreEqual(1,Visible(_feedback.PickupFeed.Rows));Assert.AreEqual(665,Measure(_feedback.PickupFeed.Rows[0].transform).y);Assert.AreEqual("Rubber Slingshot",_feedback.PickupFeed.Rows[0].Label.GetParsedText());
            Tick(1);_hud.NotifyPickup("RUBBER_GUN",1);Tick(0);Assert.AreEqual(1,Visible(_feedback.PickupFeed.Rows));Assert.AreEqual(2,_feedback.Presenter.Pickups.GetAt(0).Count);Assert.AreEqual(hp,_hud.Hp);for(var i=1;i<8;i++)Assert.True(_inventory.GetSlot(i).IsEmpty);
            Tick(3.25f);Assert.That(_feedback.PickupFeed.Rows[0].Group.alpha,Is.EqualTo(.5).Within(.01));Tick(.25f);Assert.AreEqual(0,Visible(_feedback.PickupFeed.Rows));
        }
        [UnityTest] public IEnumerator HitMarker_FollowsThreeAimPoints_AndOtherTargetsAreIgnored()
        {
            yield return Spawn();_hud.NotifyHitDealt(HitTargetKind.Other);Tick(0);Assert.AreEqual(0,_feedback.HitMarker.Group.alpha);
            foreach(var aim in new[]{new Vector2(820,288),new Vector2(200,500),new Vector2(1100,100)}){_hud.NotifyHitDealt(HitTargetKind.Hero);Tick(0,aim.x-640,360-aim.y);var rect=Measure(_feedback.HitMarker.transform);Assert.AreEqual(new Vector2(56,56),rect.size);Assert.That(Vector2.Distance(rect.center,aim),Is.LessThan(2));Assert.AreEqual(1,_feedback.HitMarker.Group.alpha);}
            Tick(.1f);Assert.AreEqual(1,_feedback.HitMarker.Group.alpha);Tick(.1f);Assert.AreEqual(0,_feedback.HitMarker.Group.alpha);foreach(var tick in _feedback.HitMarker.Ticks)Assert.AreEqual(Color.white,tick.color);
        }
        [UnityTest] public IEnumerator DamageArc_PositionOnRing_TrueAnglesEvery15Degrees()
        {
            yield return Spawn();for(var degree=0;degree<360;degree+=15){_hud.NotifyDamageTaken(true,degree);Tick(0);var rect=Measure(_feedback.DamageArcs[0].transform);HudEdgeRing.Point(degree,330,190,out var x,out var y,out _);Assert.That(Vector2.Distance(rect.center,new Vector2(640+x,360-y)),Is.LessThan(2));GraphicOutsideCenter(Measure(_feedback.DamageArcs[0].Image.transform),rect.center);Tick(1);}
            _hud.NotifyDamageTaken(false,0);Tick(0);Assert.AreEqual(0,Visible(_feedback.DamageArcs));
        }
        [UnityTest] public IEnumerator DangerArrow_OnlyWhenOffscreen_EightDirectionsAndViewportBoundary()
        {
            yield return Spawn();var cameraObject=Track(new GameObject("Feedback test camera"));var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=5;camera.aspect=16f/9;camera.transform.position=new Vector3(10,10,-10);
            var viewport=new CameraWorldViewport(camera);var recorder=new ArrowRecorder(_feedback.DangerArrows);var banner=new BannerRecorder(_feedback.DangerBanner);
            using(var presenter=new HudFeedbackPresenter(_hud,_inventory,_effects,HudFeedbackTuning.Default,viewport,_core.Quickbar,_feedback.UseAction,_feedback.EffectRow,_feedback.PickupFeed,_feedback.HitMarker,new JungleMaze.Presentation.UI.Hud.Feedback.DamageArcViewGroup(_feedback.DamageArcs),banner,recorder)){
                _hud.SetPose(10,10,0);for(var d=0;d<360;d+=45){var radians=d*Mathf.Deg2Rad;var x=10+Mathf.Cos(radians)*20;var y=10+Mathf.Sin(radians)*20;Assert.False(viewport.Contains(x,y));_hud.SetDanger(1,DangerKind.FusedBomb,1.5f,true,x,y);presenter.Tick(0,0,0);Assert.AreEqual(1,Visible(_feedback.DangerArrows));HudEdgeRing.Point(d,330,190,out var rx,out var ry,out _);var rect=Measure(_feedback.DangerArrows[0].transform);Assert.That(Vector2.Distance(rect.center,new Vector2(640+rx,360-ry)),Is.LessThan(2));Assert.AreEqual(new Vector2(30,30),rect.size);GraphicOutsideCenter(Measure(_feedback.DangerArrows[0].Arrow),rect.center);_hud.ClearDanger(1);presenter.Tick(0,0,0);}
                var edge=5*camera.aspect;foreach(var offset in new[]{edge-1,edge+1}){_hud.SetDanger(1,DangerKind.FusedBomb,1.5f,true,10+offset,10);presenter.Tick(0,0,0);Assert.AreEqual(offset>edge?1:0,Visible(_feedback.DangerArrows));Assert.AreEqual(1,_feedback.DangerBanner.Group.alpha);}
            }
        }
        [UnityTest] public IEnumerator Danger_SourceOptional_ZeroHoldAndNoUnreportedBanner()
        {
            yield return Spawn();Assert.AreEqual(0,_feedback.DangerBanner.Group.alpha);_hud.SetDanger(1,DangerKind.FusedBomb,1.5f,false,100,100);Tick(0);Assert.AreEqual(0,Visible(_feedback.DangerArrows));Assert.AreEqual(1,_feedback.DangerBanner.Group.alpha);
            Tick(.5f);Assert.AreEqual(1,_feedback.Presenter.Dangers.Banner.Warning.RemainingSeconds);Tick(1);Assert.AreEqual(0,_feedback.Presenter.Dangers.Banner.Warning.RemainingSeconds);Tick(.29f);Assert.AreEqual(1,_feedback.DangerBanner.Group.alpha);Tick(.11f);Assert.That(_feedback.DangerBanner.Group.alpha,Is.EqualTo(.5).Within(.02));Tick(.1f);Assert.AreEqual(0,_feedback.DangerBanner.Group.alpha);
        }
        [UnityTest] public IEnumerator FeedbackHintAndLongestBanner_AreNotEllipsized()
        {
            yield return Spawn();_inventory.BeginUse(4,"GOD_LEAF",.5f,.5f,true);_hud.SetPose(10,10,0);
            for(var i=0;i<16;i++)_hud.SetDanger(i,DangerKind.FusedBomb,1.5f+i*.1f,true,18.5f,18.5f);Tick(0);
            Assert.That(_feedback.UseAction.Hint.preferredWidth,Is.LessThanOrEqualTo(_feedback.UseAction.Hint.rectTransform.rect.width+.01f));
            foreach(var text in new[]{_feedback.DangerBanner.Label,_feedback.DangerBanner.Timer,_feedback.DangerBanner.Suffix}){Assert.That(text.preferredWidth,Is.LessThanOrEqualTo(text.rectTransform.rect.width+.01f),text.name+" must fit actual TMP metrics");Assert.False(text.isTextOverflowing,text.name);}
            StringAssert.Contains("+15",_feedback.DangerBanner.Suffix.GetParsedText());StringAssert.Contains("Đông Bắc",_feedback.DangerBanner.Suffix.GetParsedText());StringAssert.Contains("12 ô",_feedback.DangerBanner.Suffix.GetParsedText());Assert.AreEqual(_core.Palette.Red,_feedback.DangerBanner.Timer.color);
        }
        [UnityTest] public IEnumerator Burst_EveryFrameWithinLimits_ThenClearFiveSeconds_AndCoreRectsUnchanged()
        {
            yield return Spawn();var before=CoreRects();var feedback=FeedbackRects();_scenarios.Burst();for(var i=0;i<30;i++){Tick(1f/30);WithinLimits();}for(var i=0;i<150;i++){Tick(1f/30);WithinLimits();}
            CollectionAssert.AreEqual(before,CoreRects());CollectionAssert.AreEqual(feedback,FeedbackRects());Assert.AreEqual(0,Visible(_feedback.PickupFeed.Rows));Assert.AreEqual(0,Visible(_feedback.EffectRow.Chips));Assert.AreEqual(0,Visible(_feedback.DamageArcs));Assert.AreEqual(0,Visible(_feedback.DangerArrows));Assert.AreEqual(0,_feedback.DangerBanner.Group.alpha);Assert.AreEqual(0,_feedback.UseAction.Group.alpha);Assert.AreEqual(0,_feedback.HitMarker.Group.alpha);
        }
        [UnityTest] public IEnumerator Burst60s_NoErrorsOrMissingReferences()
        {
            yield return Spawn();for(var second=0;second<60;second++){_scenarios.Burst();for(var frame=0;frame<30;frame++){Tick(1f/30);WithinLimits();}if(second%10==0)yield return null;}LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator FeedbackText_MinSize13_VietnameseGlyphsAndFontRoles()
        {
            yield return Spawn();var phrases="Đang dùng Đã hủy Di chuyển để hủy hướng Tây Nam món khác Có bẫy Độc Chậm Ẩn Ngất Trói Đảo Miễn";
            foreach(var text in _feedback.GetComponentsInChildren<TMP_Text>(true)){Assert.GreaterOrEqual(text.fontSize,13,text.name);Assert.False(text.enableAutoSizing,text.name);Assert.NotNull(text.font,text.name);Assert.True(text.font.faceInfo.familyName.Contains("Be Vietnam Pro")||text.font.faceInfo.familyName.Contains("Pixelify Sans"));}
            foreach(var chip in _feedback.EffectRow.Chips){Assert.True(chip.Label.font.HasCharacters(phrases,out uint[] missing,false,false),missing==null?"":string.Join(",",missing));Assert.AreEqual(TextOverflowModes.Ellipsis,chip.Label.overflowMode);Assert.True(chip.Seconds.font.faceInfo.familyName.Contains("Pixelify"));}
            Assert.True(_feedback.UseAction.Seconds.font.faceInfo.familyName.Contains("Pixelify"));Assert.True(_feedback.UseAction.Label.font.faceInfo.familyName.Contains("Be Vietnam"));foreach(var e in _feedback.Catalog.Entries)if(e.Kind!=EffectKind.Adrenaline)Assert.LessOrEqual(e.Label.Length,4);
        }
        [UnityTest] public IEnumerator SwapPlaceholderSprites_LeavesGeometryAndBehaviorUnchanged()
        {
            yield return Spawn();var before=FeedbackRects();var core=CoreRects();var texture=Track(new Texture2D(16,16));var sprite=Track(Sprite.Create(texture,new Rect(0,0,16,16),Vector2.one*.5f));var catalog=Track(Object.Instantiate(_feedback.Catalog));for(var i=0;i<catalog.Entries.Length;i++){var e=catalog.Entries[i];e.Icon=sprite;catalog.Entries[i]=e;}_feedback.EffectRow.Configure(_core.Palette,catalog);
            foreach(var view in new Component[]{_feedback.UseAction,_feedback.EffectRow,_feedback.PickupFeed,_feedback.HitMarker,_feedback.DangerBanner})foreach(var image in view.GetComponentsInChildren<Image>(true))image.sprite=sprite;
            foreach(var arc in _feedback.DamageArcs)arc.Image.sprite=sprite;foreach(var arrow in _feedback.DangerArrows)arrow.Arrow.GetComponent<Image>().sprite=sprite;
            _scenarios.Burst();for(var i=0;i<30;i++){Tick(1f/30);WithinLimits();}CollectionAssert.AreEqual(before,FeedbackRects());CollectionAssert.AreEqual(core,CoreRects());
            var arcPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_JungleMaze/Prefabs/UI/HudDamageArc.prefab");var arrowPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_JungleMaze/Prefabs/UI/HudDangerArrow.prefab");Assert.NotNull(arcPrefab.GetComponent<JungleMaze.Presentation.UI.Hud.Feedback.DamageArcView>());Assert.IsNull(arcPrefab.GetComponent<JungleMaze.Presentation.UI.Hud.Feedback.DangerArrowView>());Assert.NotNull(arrowPrefab.GetComponent<JungleMaze.Presentation.UI.Hud.Feedback.DangerArrowView>());Assert.AreNotSame(_feedback.DamageArcs[0].gameObject,_feedback.DangerArrows[0].gameObject);Assert.AreNotSame(arcPrefab.GetComponentInChildren<Image>().sprite,arrowPrefab.GetComponentInChildren<Image>().sprite);
            foreach(var g in _feedback.GetComponentsInChildren<Graphic>(true))Assert.False(g.raycastTarget);
        }
        [UnityTest] public IEnumerator AssignedGround_IsFloorOnly_AndWallsAndDimensionsRemainUnchanged()
        {
            var go=Track(new GameObject("Ground hook test"));var grid=go.AddComponent<MapGridView>();var sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_JungleMaze/Art/Foundation/Ground96.png");var so=new SerializedObject(grid);so.FindProperty("groundSprite").objectReferenceValue=sprite;so.ApplyModifiedPropertiesWithoutUndo();grid.Build(50,48);
            Assert.AreEqual(sprite,go.transform.Find("Grid floor").GetComponent<SpriteRenderer>().sprite);Assert.AreEqual(new Vector2(50,50),go.transform.Find("Grid floor").GetComponent<SpriteRenderer>().size);Assert.AreEqual(5,go.transform.childCount);
            foreach(var name in new[]{"West boundary","East boundary","South boundary","North boundary"}){var wall=go.transform.Find(name);Assert.NotNull(wall.GetComponent<BoxCollider2D>());Assert.AreNotEqual(sprite,wall.GetComponent<SpriteRenderer>().sprite);}
            yield return null;
        }
        private void WithinLimits(){Assert.LessOrEqual(Visible(_feedback.PickupFeed.Rows),3);Assert.LessOrEqual(Visible(_feedback.EffectRow.Chips),6);Assert.LessOrEqual(Visible(_feedback.DamageArcs),4);Assert.LessOrEqual(Visible(_feedback.DangerArrows),3);Assert.LessOrEqual(_feedback.DangerBanner.Group.alpha,1);Assert.LessOrEqual(_feedback.HitMarker.Group.alpha,1);Assert.LessOrEqual(_feedback.UseAction.Group.alpha,1);}
        private static void GraphicOutsideCenter(Rect graphic,Vector2 center)
        {
            var clean=HudFigmaV2Feedback.Center;Assert.False(clean.Contains(center));
            if(Overlap(graphic,clean)<=0)return;
            if(center.x<=clean.xMin)Assert.LessOrEqual(Mathf.Max(0,graphic.xMax-clean.xMin),8.01f);if(center.x>=clean.xMax)Assert.LessOrEqual(Mathf.Max(0,clean.xMax-graphic.xMin),8.01f);
            if(center.y<=clean.yMin)Assert.LessOrEqual(Mathf.Max(0,graphic.yMax-clean.yMin),8.01f);if(center.y>=clean.yMax)Assert.LessOrEqual(Mathf.Max(0,clean.yMax-graphic.yMin),8.01f);
        }
        private sealed class ArrowRecorder:IDangerArrowView{private readonly JungleMaze.Presentation.UI.Hud.Feedback.DangerArrowViewGroup _views;public ArrowRecorder(JungleMaze.Presentation.UI.Hud.Feedback.DangerArrowView[] views){_views=new JungleMaze.Presentation.UI.Hud.Feedback.DangerArrowViewGroup(views);}public void SetArrow(int i,bool visible,float x,float y,float degrees,bool timed,int seconds,float alpha)=>_views.SetArrow(i,visible,x,y,degrees,timed,seconds,alpha);}
        private sealed class BannerRecorder:IDangerBannerView{private readonly JungleMaze.Presentation.UI.Hud.Feedback.DangerBannerView _view;public BannerRecorder(JungleMaze.Presentation.UI.Hud.Feedback.DangerBannerView view){_view=view;}public void Show(bool visible,char[] text,int length,float alpha)=>_view.Show(visible,text,length,alpha);}
    }
}
#endif
