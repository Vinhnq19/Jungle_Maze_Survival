using System;
using JungleMaze.App.Hud.Feedback;
using JungleMaze.App.Hud.State;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Hud;
using NUnit.Framework;

namespace JungleMaze.Tests.Hud.Feedback
{
    /// <summary>Acceptance-value matrices for clocks, source refreshes and compass boundaries.</summary>
    public sealed class FeedbackTimingMatrixTests
    {
        [TestCase(EffectKind.Poison,3f)] [TestCase(EffectKind.Stun,1f)] [TestCase(EffectKind.Stun,2f)]
        [TestCase(EffectKind.Stun,5f)] [TestCase(EffectKind.Root,1.5f)] [TestCase(EffectKind.Invisible,5f)] [TestCase(EffectKind.CcImmune,7f)]
        public void TimedEffects_ReadSourceCountdown_AndCeilSeconds(EffectKind kind,float total)
        {
            var source=new EffectStateStore(new EventBus());var model=new EffectChipFeedback(HudFeedbackTuning.Default);source.Apply(kind,total,total);
            for(var quarter=0;quarter<total*4;quarter++){
                if(quarter>0)source.Tick(.25f);model.BeginSync();for(var i=0;i<source.Count;i++)model.Sync(source.GetAt(i));model.Tick(quarter==0?0:.25f);
                var expected=total-quarter*.25f;Assert.AreEqual(1,model.Count);Assert.AreEqual((int)Math.Ceiling(expected),model.GetAt(0).Seconds);Assert.That(model.GetAt(0).Fill,Is.EqualTo(expected/total).Within(.0001));
            }
        }
        [Test] public void PoisonRefreshEveryHalfSecond_ForFourSeconds_NeverShowsZero()
        {
            var source=new EffectStateStore(new EventBus());var model=new EffectChipFeedback(HudFeedbackTuning.Default);
            for(var frame=0;frame<120;frame++){
                if(frame%15==0)source.Apply(EffectKind.Poison,3,3);source.Tick(1f/30);model.BeginSync();for(var i=0;i<source.Count;i++)model.Sync(source.GetAt(i));model.Tick(1f/30);
                Assert.AreEqual(1,model.Count);Assert.Greater(model.GetAt(0).Seconds,0);Assert.Greater(model.GetAt(0).Fill,0);
            }
        }
        [Test] public void PickupMerge_RestartsItsOwnThreeAndHalfSecondLifetime()
        {
            var model=new PickupFeed(HudFeedbackTuning.Default);model.Add(new ItemPickup("ARROW","Arrows",12));model.Tick(1);model.Add(new ItemPickup("ARROW","Arrows",12));Assert.AreEqual(24,model.GetAt(0).Count);
            model.Tick(3.49f);Assert.AreEqual(1,model.Count);model.Tick(.01f);Assert.AreEqual(0,model.Count);
        }
        [TestCase(4,0,0,"Đông")] [TestCase(3,3,1,"Đông Bắc")] [TestCase(0,4,2,"Bắc")] [TestCase(-3,3,3,"Tây Bắc")]
        [TestCase(-4,0,4,"Tây")] [TestCase(-3,-3,5,"Tây Nam")] [TestCase(0,-4,6,"Nam")] [TestCase(3,-3,7,"Đông Nam")]
        public void Banner_EightDirections_ArePrintedFromSourceVectors(float dx,float dy,int index,string direction)
        {
            Assert.AreEqual(index,HudFeedbackFormat.CompassIndex(dx,dy));var output=new DangerBannerOutput{Warning=new DangerWarning(1,DangerKind.FusedBomb,1.5f,1.5f,true,dx,dy),HasDirection=true,Compass=index,Distance=4};var text=new char[128];
            Assert.AreEqual("! Bom nổ sau 2s · hướng "+direction+", 4 ô",new string(text,0,HudFeedbackFormat.Banner(text,output)));
        }
        [TestCase(0,330,0,0)] [TestCase(90,0,190,90)] [TestCase(180,-330,0,0)] [TestCase(270,0,-190,90)]
        public void Ring_CardinalPointsAndTangentsMatchApprovedCoordinates(float degrees,float x,float y,float tangent)
        {
            HudEdgeRing.Point(degrees,330,190,out var actualX,out var actualY,out var rotation);Assert.That(actualX,Is.EqualTo(x).Within(.001));Assert.That(actualY,Is.EqualTo(y).Within(.001));Assert.AreEqual(tangent,rotation);
        }
    }
}
