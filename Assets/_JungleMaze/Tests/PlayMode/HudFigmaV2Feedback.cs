using UnityEngine;

namespace JungleMaze.Tests.PlayMode
{
    /// <summary>Approved Figma 48:239 geometry in the 1280 by 720 reference canvas.</summary>
    public static class HudFigmaV2Feedback
    {
        public const float Tolerance=2;
        public static readonly Rect Effects=new Rect(24,466,294,58);
        public static readonly Rect Pickup=new Rect(1115,564,141,132);
        public static readonly Rect Banner=new Rect(490,24,301,40);
        public static readonly Rect Use=new Rect(411,572,458,32);
        public static readonly Rect Center=new Rect(320,180,640,360);
    }
}
