using JungleMaze.App.Hud.Feedback;
using JungleMaze.App.Hud.State;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Hud;
using JungleMaze.Presentation.UI.Hud;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is=UnityEngine.TestTools.Constraints.Is;

namespace JungleMaze.Tests.Hud.Feedback
{
    /// <summary>Non-allocating fake view shared by presenter tests.</summary>
    public sealed class FeedbackViewRecorder:IQuickbarFeedbackHooks,IUseActionView,IEffectRowView,IPickupFeedView,IHitMarkerView,IDamageArcView,IDangerBannerView,IDangerArrowView,IWorldViewport
    {
        public int Writes,LastSlot=-1,PickupCount,ArcCount,ArrowCount;public bool StripVisible,UseVisible,Hint,InUse;public float Fill,HitAlpha,AimX,AimY;public string PickupId;
        public readonly char[] Text=new char[128];public int TextLength;
        public bool Contains(float x,float y)=>false;
        public void SetWeaponStripVisible(bool visible){Writes++;StripVisible=visible;}
        public void SetSlotInUse(int index,bool value){Writes++;LastSlot=index;InUse=value;}
        public void Show(bool visible,char[] label,int length,char[] seconds,int secondsLength,float fill,bool hint,bool cancelled){Writes++;UseVisible=visible;Fill=fill;Hint=hint;System.Array.Copy(label,Text,length);TextLength=length;}
        public void SetChip(int i,bool visible,EffectKind kind,EffectPolarity polarity,bool timed,float fill,int seconds,int overflow,float alpha){Writes++;}
        public void SetRow(int i,bool visible,string id,string name,int count,float alpha){Writes++;if(visible){PickupId=id;PickupCount=count;}}
        public void SetOverflow(int count,float alpha){Writes++;}
        public void Show(float alpha,float x,float y){Writes++;HitAlpha=alpha;AimX=x;AimY=y;}
        public void SetArc(int i,bool visible,float x,float y,float rotation,float alpha){Writes++;if(visible)ArcCount++;}
        public void Show(bool visible,char[] text,int length,float alpha){Writes++;}
        public void SetArrow(int i,bool visible,float x,float y,float deg,bool timed,int seconds,float alpha){Writes++;if(visible)ArrowCount++;}
    }
    public sealed class HudFeedbackPresenterTests
    {
        private static HudFeedbackPresenter Presenter(IHudState h,IInventoryState i,IEffectState e,FeedbackViewRecorder v)=>new HudFeedbackPresenter(h,i,e,HudFeedbackTuning.Default,v,v,v,v,v,v,v,v,v);
        [Test] public void Idle_NoWritesAndNoGc_WithoutStateChanges()
        {
            var h=new FakeHudState();var i=new FakeInventoryState();var e=new FakeEffectState();var v=new FeedbackViewRecorder();using(var p=Presenter(h,i,e,v)){
                p.Tick(0,0,0);var writes=v.Writes;p.Tick(.016f,20,30);Assert.AreEqual(writes,v.Writes);
                Assert.That(()=>p.Tick(.016f,20,30),Is.Not.AllocatingGCMemory());}
        }
        [Test] public void SlotHookOnAndOff_CompleteDoesNotWriteGameplayCounts()
        {
            var balance=StoreTestUtil.Balance();var bus=new EventBus();var h=new HudStateStore(bus,balance);var i=new InventoryStateStore(bus,balance);var e=new EffectStateStore(bus);var v=new FeedbackViewRecorder();i.SetConsumableSlot(0,"GOD_LEAF",6);
            using(var p=Presenter(h,i,e,v)){p.Tick(0,0,0);i.BeginUse(4,"GOD_LEAF",.5f,.5f,true);p.Tick(0,0,0);Assert.True(v.UseVisible);Assert.False(v.StripVisible);Assert.True(v.InUse);Assert.AreEqual(4,v.LastSlot);i.Tick(.25f);p.Tick(.25f,0,0);Assert.AreEqual(.5f,v.Fill);i.CompleteUse("GOD_LEAF");p.Tick(0,0,0);Assert.False(v.InUse);p.Tick(.25f,0,0);Assert.True(v.StripVisible);Assert.AreEqual(6,i.GetSlot(4).Count);Assert.AreEqual(100,h.Hp);}
        }
        [Test] public void OptionalDirectionAndHitTargetRules_AndDisposeUnsubscribes()
        {
            var h=new FakeHudState();var i=new FakeInventoryState();var e=new FakeEffectState();var v=new FeedbackViewRecorder();var p=Presenter(h,i,e,v);p.Tick(0,0,0);
            h.RaiseDamage(new DamageTaken(false,45));h.RaiseHit(new HitDealt(HitTargetKind.Other));p.Tick(0,10,20);Assert.AreEqual(0,v.ArcCount);Assert.AreEqual(0,v.HitAlpha);
            h.RaiseHit(new HitDealt(HitTargetKind.Hero));p.Tick(0,30,40);Assert.AreEqual(1,v.HitAlpha);Assert.AreEqual(30,v.AimX);Assert.AreEqual(40,v.AimY);
            p.Dispose();var writes=v.Writes;h.RaiseDamage(new DamageTaken(true,45));h.RaisePickup(new ItemPickup("A","A",1));p.Tick(0,0,0);Assert.AreEqual(writes,v.Writes);
        }
        [Test] public void TwoReadPortImplementations_ProduceTheSameOutput()
        {
            var balance=StoreTestUtil.Balance();var bus=new EventBus();var h=new HudStateStore(bus,balance);var i=new InventoryStateStore(bus,balance);var e=new EffectStateStore(bus);var fh=new FakeHudState();var fi=new FakeInventoryState();var fe=new FakeEffectState();var a=new FeedbackViewRecorder();var b=new FeedbackViewRecorder();
            using(var p=Presenter(h,i,e,a))using(var q=Presenter(fh,fi,fe,b)){
                p.Tick(0,0,0);q.Tick(0,0,0);h.NotifyPickup("GOD_LEAF",2);fh.RaisePickup(new ItemPickup("GOD_LEAF","God Leaf",2));h.NotifyHitDealt(HitTargetKind.Hero);fh.RaiseHit(new HitDealt(HitTargetKind.Hero));i.BeginUse(4,"GOD_LEAF",.5f,.5f,true);fi.RaiseUse(new ItemUseStatus(ItemUsePhase.Using,"GOD_LEAF","God Leaf",4,.5f,.5f,true));
                p.Tick(0,12,34);q.Tick(0,12,34);Assert.AreEqual(a.Writes,b.Writes);Assert.AreEqual(a.UseVisible,b.UseVisible);Assert.AreEqual(a.Fill,b.Fill);Assert.AreEqual(a.PickupCount,b.PickupCount);Assert.AreEqual(a.HitAlpha,b.HitAlpha);Assert.AreEqual(new string(a.Text,0,a.TextLength),new string(b.Text,0,b.TextLength));
            }
        }
        [Test] public void ActiveFeedback_TickDoesNotAllocate()
        {
            var balance=StoreTestUtil.Balance();var bus=new EventBus();var h=new HudStateStore(bus,balance);var i=new InventoryStateStore(bus,balance);var e=new EffectStateStore(bus);var v=new FeedbackViewRecorder();
            using(var p=Presenter(h,i,e,v)){i.BeginUse(4,"GOD_LEAF",.5f,.5f,true);e.Apply(EffectKind.Poison,3,3);h.NotifyDamageTaken(true,0);h.NotifyHitDealt(HitTargetKind.Hero);h.NotifyPickup("GOD_LEAF",2);h.SetPose(10,10,0);h.SetDanger(1,DangerKind.FusedBomb,1.5f,true,7,7);p.Tick(.01f,0,0);Assert.That(()=>p.Tick(.01f,10,20),Is.Not.AllocatingGCMemory());}
        }
    }
}
