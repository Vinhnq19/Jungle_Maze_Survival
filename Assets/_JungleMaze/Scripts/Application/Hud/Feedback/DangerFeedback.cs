using System;
using JungleMaze.Domain.Hud;

namespace JungleMaze.App.Hud.Feedback
{
    /// <summary>Source warning selected for the single banner.</summary>
    public struct DangerBannerOutput { public bool Visible,HasDirection; public DangerWarning Warning;public int OtherCount,Compass,Distance;public float Alpha; }
    /// <summary>One offscreen warning arrow, with true-angle ring placement.</summary>
    public struct DangerArrowOutput { public int Id,Seconds;public bool Timed;public float X,Y,Degrees,Alpha; }
    /// <summary>Warning urgency, stale traps and expired-bomb display snapshots.</summary>
    public sealed class DangerFeedback
    {
        private struct Entry {public DangerWarning Warning;public float Age;public bool Ghost;public long Order;}
        private readonly Entry[] _items=new Entry[32];private readonly int[] _order=new int[32];
        private readonly DangerArrowOutput[] _arrows=new DangerArrowOutput[3];private readonly HudFeedbackTuning _t;
        private int _count;private long _sequence;
        public DangerBannerOutput Banner {get;private set;}
        public int ArrowCount {get;private set;}
        public DangerArrowOutput GetArrow(int i)=>i>=0&&i<ArrowCount?_arrows[i]:default;
        public DangerFeedback(HudFeedbackTuning tuning){_t=tuning;}
        public void Upsert(in DangerWarning warning)
        {
            var index=IndexOf(warning.Id);if(index<0){if(_count==_items.Length)return;index=_count++;_items[index]=new Entry{Order=_sequence++};}
            var e=_items[index];e.Warning=warning;e.Age=0;e.Ghost=false;_items[index]=e;
        }
        public void SyncSnapshot(in DangerWarning warning){var i=IndexOf(warning.Id);if(i>=0){var e=_items[i];e.Warning=warning;_items[i]=e;}}
        public void Remove(int id,bool expired)
        {
            var i=IndexOf(id);if(i<0)return;
            if(!expired){RemoveAt(i);return;}
            var e=_items[i];var w=e.Warning;e.Ghost=true;e.Age=0;e.Warning=new DangerWarning(w.Id,w.Kind,w.TotalSeconds,0,w.HasSource,w.SourceX,w.SourceY);_items[i]=e;
        }
        public void Tick(float dt,bool hasPose,in HeroPose pose,IWorldViewport viewport)
        {
            dt=Math.Max(0,dt);
            for(var i=0;i<_count;i++){
                var e=_items[i];e.Age+=dt;
                if((e.Ghost&&e.Age>=_t.BombZeroHold+_t.BombFade-.00001f)||(!e.Ghost&&!e.Warning.IsTimed&&e.Age>=_t.TrapStaleSeconds-.00001f)){RemoveAt(i--);continue;}
                _items[i]=e;
            }
            for(var i=0;i<_count;i++){
                var pos=i;while(pos>0&&ComesBefore(_items[i],_items[_order[pos-1]])){_order[pos]=_order[pos-1];pos--;}_order[pos]=i;
            }
            Banner=default;ArrowCount=0;
            for(var rank=0;rank<_count;rank++){
                var e=_items[_order[rank]];var w=e.Warning;
                var alpha=e.Ghost&&e.Age>_t.BombZeroHold?Math.Max(0,1-(e.Age-_t.BombZeroHold)/Math.Max(.00001f,_t.BombFade)):1;
                var direction=hasPose&&w.HasSource;var dx=w.SourceX-pose.X;var dy=w.SourceY-pose.Y;
                if(rank==0)Banner=new DangerBannerOutput{Visible=true,Warning=w,OtherCount=_count-1,Alpha=alpha,HasDirection=direction,
                    Compass=direction?HudFeedbackFormat.CompassIndex(dx,dy):0,Distance=direction?HudFeedbackFormat.Distance(dx,dy):0};
                if(!direction||viewport==null||viewport.Contains(w.SourceX,w.SourceY)||ArrowCount>=Math.Max(0,Math.Min(3,_t.MaxArrows)))continue;
                var deg=(float)(Math.Atan2(dy,dx)*180/Math.PI);HudEdgeRing.Point(deg,_t.RingHalfWidth,_t.RingHalfHeight,out var x,out var y,out _);
                _arrows[ArrowCount++]=new DangerArrowOutput{Id=w.Id,Timed=w.IsTimed,Seconds=(int)Math.Ceiling(w.RemainingSeconds),X=x,Y=y,Degrees=deg,Alpha=alpha};
            }
        }
        private static bool ComesBefore(in Entry a,in Entry b)
        {
            if(a.Warning.IsTimed!=b.Warning.IsTimed)return a.Warning.IsTimed;
            if(a.Warning.IsTimed&&a.Warning.RemainingSeconds!=b.Warning.RemainingSeconds)return a.Warning.RemainingSeconds<b.Warning.RemainingSeconds;
            return a.Order<b.Order;
        }
        private int IndexOf(int id){for(var i=0;i<_count;i++)if(_items[i].Warning.Id==id)return i;return -1;}
        private void RemoveAt(int i){for(;i<_count-1;i++)_items[i]=_items[i+1];_items[--_count]=default;}
    }
}
