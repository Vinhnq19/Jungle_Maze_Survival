using System;
using JungleMaze.Domain.Hud;

namespace JungleMaze.App.Hud.Feedback
{
    /// <summary>A damage arc's true-angle placement and display opacity.</summary>
    public struct DamageArcOutput {public float Degrees,Alpha,X,Y,Rotation;}
    /// <summary>At most four independent arcs, merging nearby directions around the wrap boundary.</summary>
    public sealed class DamageArcFeedback
    {
        private readonly float[] _angles=new float[4],_ages=new float[4];private readonly HudFeedbackTuning _t;
        public int Count {get;private set;}
        public DamageArcFeedback(HudFeedbackTuning tuning){_t=tuning;}
        public void Add(in DamageTaken damage)
        {
            if(!damage.HasDirection||float.IsNaN(damage.DirectionDegrees)||float.IsInfinity(damage.DirectionDegrees))return;
            var target=-1;for(var i=0;i<Count;i++)if(HudEdgeRing.Difference(_angles[i],damage.DirectionDegrees)<=_t.ArcMergeDegrees){target=i;break;}
            if(target<0){if(Count<Math.Max(1,Math.Min(4,_t.MaxArcs)))target=Count++;else {target=0;for(var i=1;i<Count;i++)if(_ages[i]>_ages[target])target=i;}}
            _angles[target]=HudEdgeRing.Normalize(damage.DirectionDegrees);_ages[target]=0;
        }
        public void Tick(float dt){for(var i=0;i<Count;i++){_ages[i]+=Math.Max(0,dt);if(_ages[i]>=_t.ArcFade-.00001f){for(var j=i;j<Count-1;j++){_angles[j]=_angles[j+1];_ages[j]=_ages[j+1];}Count--;i--;}}}
        public DamageArcOutput GetAt(int i)
        {
            if(i<0||i>=Count)return default;HudEdgeRing.Point(_angles[i],_t.RingHalfWidth,_t.RingHalfHeight,out var x,out var y,out var tangent);
            return new DamageArcOutput{Degrees=_angles[i],Alpha=Math.Max(0,1-_ages[i]/Math.Max(.00001f,_t.ArcFade)),X=x,Y=y,Rotation=tangent};
        }
    }
}
