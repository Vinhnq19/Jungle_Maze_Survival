using System;
using JungleMaze.App.Hud.Feedback;
using JungleMaze.Domain.Hud;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is=UnityEngine.TestTools.Constraints.Is;

namespace JungleMaze.Tests.Hud.Feedback
{
    public sealed class FeedbackModelsTests
    {
        private static readonly HudFeedbackTuning T=HudFeedbackTuning.Default;
        [Test] public void Use_UsesSourceProgress_AndPreservesCancelAndCompleteLingers()
        {
            var m=new UseActionFeedback(T);var s=new ItemUseStatus(ItemUsePhase.Using,"A","A",4,.5f,.5f,true);m.Accept(s);Assert.True(m.Hint);Assert.AreEqual(0,m.Fill);
            m.Tick(.25f,new ItemUseStatus(ItemUsePhase.Using,"A","A",4,.5f,.25f,true));Assert.AreEqual(.5f,m.Fill);
            m.Accept(new ItemUseStatus(ItemUsePhase.Cancelled,"A","A",4,.5f,.25f,true));m.Tick(.59f,default);Assert.True(m.Visible);m.Tick(.01f,default);Assert.False(m.Visible);
            m.Accept(new ItemUseStatus(ItemUsePhase.Completed,"A","A",4,.5f,0,true));Assert.AreEqual(1,m.Fill);m.Tick(.25f,default);Assert.False(m.Visible);
        }
        [TestCase(30)] [TestCase(60)] [TestCase(144)] public void Use_MonotonicAtSupportedFrameRates(int hz)
        {
            var m=new UseActionFeedback(T);var last=0f;m.Accept(new ItemUseStatus(ItemUsePhase.Using,"A","A",4,1,1,false));
            for(var i=0;i<=hz;i++){var s=new ItemUseStatus(ItemUsePhase.Using,"A","A",4,1,1f-i/(float)hz,false);m.Tick(1f/hz,s);Assert.GreaterOrEqual(m.Fill,last);last=m.Fill;}Assert.AreEqual(1,m.Fill);
        }
        [Test] public void Effects_HarmfulFirst_OverflowAndAdrenalineExcluded()
        {
            var m=new EffectChipFeedback(T);m.BeginSync();m.Sync(new ActiveEffect(EffectKind.Adrenaline,8,8));m.Sync(new ActiveEffect(EffectKind.Invisible,8,8));
            for(var i=0;i<7;i++)m.Sync(new ActiveEffect((EffectKind)(10+i),3,3));m.Tick(0);Assert.AreEqual(6,m.Count);Assert.AreEqual(3,m.GetAt(5).Overflow);Assert.AreEqual(EffectPolarity.Harmful,m.GetAt(0).Polarity);
        }
        [Test] public void Effects_ExpireFade_CompactAndUntimed()
        {
            var m=new EffectChipFeedback(T);m.BeginSync();m.Sync(new ActiveEffect(EffectKind.Poison,3,2));m.Sync(new ActiveEffect(EffectKind.Slow,0,0));m.Tick(0);
            Assert.AreEqual(2,m.GetAt(0).Seconds);Assert.AreEqual(2f/3,m.GetAt(0).Fill,.001);
            m.BeginSync();m.Sync(new ActiveEffect(EffectKind.Slow,0,0));m.Tick(.1f);Assert.AreEqual(2,m.Count);m.Tick(.1f);Assert.AreEqual(2,m.Count);m.Tick(.1f);Assert.AreEqual(1,m.Count);Assert.False(m.GetAt(0).Timed);
        }
        [Test] public void Effects_BlinkPeriodAndRefreshSnapshot()
        {
            var m=new EffectChipFeedback(T);m.BeginSync();m.Sync(new ActiveEffect(EffectKind.Poison,3,.8f));m.Tick(.13f);Assert.AreEqual(.5f,m.GetAt(0).Alpha);
            m.BeginSync();m.Sync(new ActiveEffect(EffectKind.Poison,3,3));m.Tick(.01f);Assert.AreEqual(3,m.GetAt(0).Seconds);Assert.AreEqual(1,m.GetAt(0).Fill);
        }
        [Test] public void Pickup_MergeOverflowLifetimeAndOrdering()
        {
            var m=new PickupFeed(T);m.Add(new ItemPickup("A","A",1));m.Tick(2);m.Add(new ItemPickup("A","A",2));Assert.AreEqual(3,m.GetAt(0).Count);
            m.Add(new ItemPickup("B","B",1));m.Add(new ItemPickup("C","C",1));m.Add(new ItemPickup("D","D",1));m.Add(new ItemPickup("E","E",1));
            Assert.AreEqual(3,m.Count);Assert.AreEqual(2,m.Overflow);Assert.AreEqual("C",m.GetAt(0).ItemId);Assert.AreEqual("E",m.GetAt(2).ItemId);
            m.Tick(3.25f);Assert.AreEqual(.5f,m.GetAt(0).Alpha,.001);m.Tick(.25f);Assert.AreEqual(0,m.Count);Assert.AreEqual(0,m.Overflow);
        }
        [Test] public void Hit_OnlyHero_RepeatedHitsResetSingleMarker()
        {
            var m=new HitMarkerFeedback(T);m.Add(HitTargetKind.Other);Assert.AreEqual(0,m.Alpha);m.Add(HitTargetKind.Hero);m.Tick(.15f);Assert.AreEqual(.5f,m.Alpha,.001);
            for(var i=0;i<10;i++)m.Add(HitTargetKind.Hero);Assert.AreEqual(1,m.Alpha);m.Tick(.2f);Assert.AreEqual(0,m.Alpha);
        }
        [Test] public void Arcs_MergeWrapBoundary_ReplaceOldestAndFade()
        {
            var m=new DamageArcFeedback(T);m.Add(new DamageTaken(false,40));Assert.AreEqual(0,m.Count);m.Add(new DamageTaken(true,355));m.Add(new DamageTaken(true,5));Assert.AreEqual(1,m.Count);Assert.AreEqual(5,m.GetAt(0).Degrees);
            m.Tick(.1f);m.Add(new DamageTaken(true,90));m.Add(new DamageTaken(true,180));m.Add(new DamageTaken(true,270));m.Add(new DamageTaken(true,45));Assert.AreEqual(4,m.Count);Assert.AreEqual(45,m.GetAt(0).Degrees);m.Tick(1);Assert.AreEqual(0,m.Count);
        }
        [Test] public void Ring_TrueAnglesOutsideCenterEvery15Degrees()
        {
            for(var d=0;d<360;d+=15){HudEdgeRing.Point(d,330,190,out var x,out var y,out _);Assert.True(Math.Abs(x)>=329.99||Math.Abs(y)>=189.99);var actual=(float)(Math.Atan2(y,x)*180/Math.PI);Assert.Less(HudEdgeRing.Difference(actual,d),.01);}
            HudEdgeRing.Point(45,330,190,out var cx,out var cy,out var tangent);Assert.AreEqual(190,cx,.001);Assert.AreEqual(190,cy,.001);Assert.AreEqual(90,tangent);
        }
        [Test] public void Danger_Urgency_OffscreenLimit_ExpiryAndTrapRefresh()
        {
            var m=new DangerFeedback(T);var viewport=new OutsideViewport();var pose=new HeroPose(10,10,0);
            for(var i=0;i<4;i++)m.Upsert(new DangerWarning(i,DangerKind.FusedBomb,2+i,2+i,true,7,7));
            m.Tick(0,true,pose,viewport);Assert.AreEqual(0,m.Banner.Warning.Id);Assert.AreEqual(3,m.Banner.OtherCount);Assert.AreEqual(3,m.ArrowCount);Assert.AreEqual(5,m.Banner.Compass);Assert.AreEqual(4,m.Banner.Distance);
            m.Remove(0,true);m.Tick(0,true,pose,viewport);Assert.AreEqual(0,m.Banner.Warning.RemainingSeconds);m.Tick(.3f,true,pose,viewport);Assert.AreEqual(1,m.Banner.Alpha);m.Tick(.1f,true,pose,viewport);Assert.AreEqual(.5f,m.Banner.Alpha,.001);m.Tick(.1f,true,pose,viewport);Assert.AreEqual(1,m.Banner.Warning.Id);
            for(var i=1;i<4;i++)m.Remove(i,false);m.Upsert(new DangerWarning(5,DangerKind.Trap,0,0,false,0,0));
            m.Tick(3,true,pose,viewport);Assert.True(m.Banner.Visible);m.Upsert(new DangerWarning(5,DangerKind.Trap,0,0,false,0,0));m.Tick(3,true,pose,viewport);Assert.True(m.Banner.Visible);Assert.False(m.Banner.HasDirection);Assert.AreEqual(0,m.ArrowCount);m.Tick(1,true,pose,viewport);Assert.False(m.Banner.Visible);
        }
        [Test] public void Format_BannerCompassAndCeilingTenths()
        {
            var b=new char[128];Assert.AreEqual("0.3s",new string(b,0,HudFeedbackFormat.Tenths(b,.25f)));
            Assert.AreEqual(1,HudFeedbackFormat.CompassIndex((float)Math.Cos(Math.PI/8),(float)Math.Sin(Math.PI/8)));
            var output=new DangerBannerOutput{Warning=new DangerWarning(1,DangerKind.FusedBomb,1.5f,1.5f,true,7,7),HasDirection=true,Compass=5,Distance=4};
            Assert.AreEqual("! Bom nổ sau 2s · hướng Tây Nam, 4 ô",new string(b,0,HudFeedbackFormat.Banner(b,output)));
        }
        [Test] public void Danger_NewActiveWarningEvictsOldGhostWhenSnapshotCapacityIsFull()
        {
            var m=new DangerFeedback(T);for(var wave=0;wave<2;wave++)for(var i=0;i<16;i++){var id=wave*16+i;m.Upsert(new DangerWarning(id,DangerKind.FusedBomb,1,1,false,0,0));m.Remove(id,true);}
            m.Upsert(new DangerWarning(99,DangerKind.FusedBomb,.1f,.1f,false,0,0));m.Tick(0,false,default,null);
            m.Tick(.5f,false,default,null);Assert.True(m.Banner.Visible);Assert.AreEqual(99,m.Banner.Warning.Id);
        }
        [Test] public void Models_TickDoesNotAllocate()
        {
            var h=new HitMarkerFeedback(T);h.Add(HitTargetKind.Hero);h.Tick(.01f);var p=new PickupFeed(T);p.Add(new ItemPickup("A","A",1));p.Tick(.01f);var a=new DamageArcFeedback(T);a.Add(new DamageTaken(true,0));a.Tick(.01f);
            Assert.That(()=>{h.Tick(.01f);p.Tick(.01f);a.Tick(.01f);},Is.Not.AllocatingGCMemory());
        }
        private sealed class OutsideViewport:IWorldViewport{public bool Contains(float x,float y)=>false;}
    }
}
