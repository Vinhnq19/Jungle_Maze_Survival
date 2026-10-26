using System;
using JungleMaze.Domain.Hud;

namespace JungleMaze.App.Hud.Feedback
{
    /// <summary>One bounded display chip; overflow chips have Overflow greater than zero.</summary>
    public struct EffectChipOutput
    {
        public EffectKind Kind;
        public EffectPolarity Polarity;
        public bool Timed;
        public float Fill, Alpha;
        public int Seconds, Overflow;
    }
    /// <summary>Stable effect ordering with fading removed snapshots and a bounded overflow chip.</summary>
    public sealed class EffectChipFeedback
    {
        private struct Entry { public ActiveEffect Effect; public int Sequence; public bool Seen, Ghost; public float Fade, Age; }
        private readonly Entry[] _items=new Entry[32];
        private readonly int[] _order=new int[32];
        private readonly EffectChipOutput[] _output=new EffectChipOutput[6];
        private readonly HudFeedbackTuning _t;
        private int _count,_sequence;
        public int Count {get;private set;}
        public EffectChipOutput GetAt(int index)=>index>=0&&index<Count?_output[index]:default;
        public EffectChipFeedback(HudFeedbackTuning tuning){_t=tuning;}
        public void BeginSync(){for(var i=0;i<_count;i++){var e=_items[i];e.Seen=false;_items[i]=e;}}
        public void Sync(in ActiveEffect effect)
        {
            if(!EffectKinds.ShowsAsChip(effect.Kind))return;
            var index=-1;for(var i=0;i<_count;i++)if(_items[i].Effect.Kind==effect.Kind){index=i;break;}
            if(index<0){if(_count==_items.Length)return;index=_count++;_items[index]=new Entry{Sequence=_sequence++};}
            var e=_items[index];e.Effect=effect;e.Seen=true;e.Ghost=false;e.Fade=0;_items[index]=e;
        }
        public void Tick(float dt)
        {
            dt=Math.Max(0,dt);
            for(var i=0;i<_count;i++){
                var e=_items[i];e.Age+=dt;
                if(!e.Seen&&!e.Ghost){e.Ghost=true;e.Fade=_t.ChipFadeOut;}
                else if(e.Ghost)e.Fade-=dt;
                if(e.Ghost&&e.Fade<=.00001f){for(var j=i;j<_count-1;j++)_items[j]=_items[j+1];_items[--_count]=default;i--;continue;}
                _items[i]=e;
            }
            for(var i=0;i<_count;i++){
                var pos=i;var rank=Rank(_items[i]);
                while(pos>0&&Rank(_items[_order[pos-1]])>rank){_order[pos]=_order[pos-1];pos--;}_order[pos]=i;
            }
            var max=Math.Max(1,Math.Min(6,_t.MaxChips));Count=Math.Min(max,_count);
            for(var i=0;i<Count;i++){
                if(_count>max&&i==max-1){_output[i]=new EffectChipOutput{Overflow=_count-(max-1),Alpha=1};continue;}
                var e=_items[_order[i]];var a=e.Ghost?Math.Max(0,e.Fade/Math.Max(.00001f,_t.ChipFadeOut)):1;
                if(!e.Ghost&&e.Effect.IsTimed&&e.Effect.RemainingSeconds<=_t.ChipBlinkBelow&&_t.ChipBlinkPeriod>0&&e.Age%_t.ChipBlinkPeriod>=_t.ChipBlinkPeriod*.5f)a=_t.ChipBlinkMinAlpha;
                _output[i]=new EffectChipOutput{Kind=e.Effect.Kind,Polarity=EffectKinds.PolarityOf(e.Effect.Kind),Timed=e.Effect.IsTimed,
                    Fill=e.Effect.IsTimed?Math.Max(0,Math.Min(1,e.Effect.RemainingSeconds/e.Effect.DurationSeconds)):0,
                    Seconds=(int)Math.Ceiling(e.Effect.RemainingSeconds),Alpha=a};
            }
        }
        private static long Rank(in Entry e)=>(EffectKinds.PolarityOf(e.Effect.Kind)==EffectPolarity.Harmful?0L:1000000000L)+e.Sequence;
    }
}
