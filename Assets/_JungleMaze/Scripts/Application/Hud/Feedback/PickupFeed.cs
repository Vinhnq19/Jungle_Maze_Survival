using System;
using JungleMaze.Domain.Hud;

namespace JungleMaze.App.Hud.Feedback
{
    /// <summary>One visible pickup line, ordered oldest first.</summary>
    public struct PickupOutput { public string ItemId,Name;public int Count;public float Alpha; }
    /// <summary>Bounded pickup feed with merge, overflow and independent display clocks.</summary>
    public sealed class PickupFeed
    {
        private readonly PickupOutput[] _rows=new PickupOutput[3];
        private readonly float[] _ages=new float[3];
        private readonly HudFeedbackTuning _t;
        private float _overflowAge;
        public int Count {get;private set;}
        public int Overflow {get;private set;}
        public float OverflowAlpha=>Alpha(_overflowAge);
        public PickupFeed(HudFeedbackTuning tuning){_t=tuning;}
        public PickupOutput GetAt(int i)=>i>=0&&i<Count?_rows[i]:default;
        public void Add(in ItemPickup pickup)
        {
            if(string.IsNullOrEmpty(pickup.ItemId)||pickup.Count<=0)return;
            for(var i=0;i<Count;i++)if(_rows[i].ItemId==pickup.ItemId){var r=_rows[i];r.Count=(int)Math.Min(int.MaxValue,(long)r.Count+pickup.Count);r.Alpha=1;_rows[i]=r;_ages[i]=0;return;}
            var max=Math.Max(1,Math.Min(3,_t.MaxPickupRows));
            if(Count==max){RemoveAt(0);Overflow++;_overflowAge=0;}
            _rows[Count]=new PickupOutput{ItemId=pickup.ItemId,Name=pickup.DisplayName,Count=pickup.Count,Alpha=1};_ages[Count++]=0;
        }
        public void Tick(float dt)
        {
            dt=Math.Max(0,dt);
            for(var i=0;i<Count;i++){_ages[i]+=dt;if(_ages[i]>=_t.PickupHold+_t.PickupFade-.00001f){RemoveAt(i--);continue;}var r=_rows[i];r.Alpha=Alpha(_ages[i]);_rows[i]=r;}
            if(Overflow>0){_overflowAge+=dt;if(_overflowAge>=_t.PickupHold+_t.PickupFade-.00001f)Overflow=0;}
        }
        private float Alpha(float age)=>age<=_t.PickupHold?1:Math.Max(0,1-(age-_t.PickupHold)/Math.Max(.00001f,_t.PickupFade));
        private void RemoveAt(int index){for(var j=index;j<Count-1;j++){_rows[j]=_rows[j+1];_ages[j]=_ages[j+1];}_rows[--Count]=default;_ages[Count]=0;}
    }
}
