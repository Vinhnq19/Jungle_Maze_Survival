using System;

namespace JungleMaze.App.Hud.Feedback
{
    /// <summary>Presentation timings and capacities, serialized on the HUD prefab.</summary>
    [Serializable]
    public struct HudFeedbackTuning
    {
        public float UseCancelledLinger, UseCompletedLinger;
        public int MaxChips;
        public float ChipFadeOut, ChipBlinkBelow, ChipBlinkPeriod, ChipBlinkMinAlpha;
        public int MaxPickupRows;
        public float PickupHold, PickupFade, HitHold, HitFade;
        public int MaxArcs;
        public float ArcMergeDegrees, ArcFade, BombZeroHold, BombFade, TrapStaleSeconds;
        public int MaxArrows;
        public float RingHalfWidth, RingHalfHeight;
        public static HudFeedbackTuning Default => new HudFeedbackTuning {
            UseCancelledLinger=.6f,UseCompletedLinger=.25f,MaxChips=6,ChipFadeOut=.2f,ChipBlinkBelow=1,
            ChipBlinkPeriod=.25f,ChipBlinkMinAlpha=.5f,MaxPickupRows=3,PickupHold=3,PickupFade=.5f,
            HitHold=.1f,HitFade=.1f,MaxArcs=4,ArcMergeDegrees=15,ArcFade=1,BombZeroHold=.3f,BombFade=.2f,
            TrapStaleSeconds=4,MaxArrows=3,RingHalfWidth=330,RingHalfHeight=190 };
    }
}
