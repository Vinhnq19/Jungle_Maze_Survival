using System;
using JungleMaze.Domain.Hud;

namespace JungleMaze.App.Hud.Feedback
{
    /// <summary>One retriggerable marker for confirmed Hero hits.</summary>
    public sealed class HitMarkerFeedback
    {
        private readonly HudFeedbackTuning _t;private float _age;private bool _active;
        public HitMarkerFeedback(HudFeedbackTuning tuning){_t=tuning;}
        public float Alpha=>!_active?0:_age<=_t.HitHold?1:Math.Max(0,1-(_age-_t.HitHold)/Math.Max(.00001f,_t.HitFade));
        public void Add(HitTargetKind target){if(target!=HitTargetKind.Hero)return;_age=0;_active=true;}
        public void Tick(float dt){if(!_active)return;_age+=Math.Max(0,dt);if(_age>=_t.HitHold+_t.HitFade-.00001f)_active=false;}
    }
}
