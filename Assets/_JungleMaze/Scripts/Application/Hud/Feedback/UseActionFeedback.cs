using System;
using JungleMaze.Domain.Hud;

namespace JungleMaze.App.Hud.Feedback
{
    /// <summary>Display lifetime for source-authoritative use phases.</summary>
    public sealed class UseActionFeedback
    {
        private readonly HudFeedbackTuning _t;
        private float _linger;
        public ItemUseStatus Status { get; private set; }
        public bool Visible { get; private set; }
        public float Fill { get; private set; }
        public bool Hint => Visible && Status.Phase == ItemUsePhase.Using && Status.Cancellable;
        public int SlotInUse => Visible && Status.Phase == ItemUsePhase.Using ? Status.SlotIndex : -1;
        public UseActionFeedback(HudFeedbackTuning tuning) { _t=tuning; }
        public void Accept(in ItemUseStatus status)
        {
            Status=status; Visible=status.Phase!=ItemUsePhase.Idle;
            _linger=status.Phase==ItemUsePhase.Cancelled?_t.UseCancelledLinger:status.Phase==ItemUsePhase.Completed?_t.UseCompletedLinger:0;
            Fill=status.Phase==ItemUsePhase.Completed?1:Ratio(status);
        }
        public void Tick(float dt, in ItemUseStatus current)
        {
            if (!Visible) return;
            if(Status.Phase==ItemUsePhase.Using) { Status=current;Fill=Ratio(current); }
            else { _linger-=Math.Max(0,dt);if(_linger<=.00001f)Visible=false; }
        }
        private static float Ratio(in ItemUseStatus status) => status.TotalSeconds>0?Math.Max(0,Math.Min(1,1-status.RemainingSeconds/status.TotalSeconds)):0;
    }
}
