using System;

namespace JungleMaze.App.Hud.Feedback
{
    /// <summary>A true-angle ray intersection with the approved rectangular HUD ring.</summary>
    public static class HudEdgeRing
    {
        public static void Point(float degrees,float halfWidth,float halfHeight,out float x,out float y,out float tangent)
        {
            var angle=degrees*Math.PI/180;var dx=Math.Cos(angle);var dy=Math.Sin(angle);
            var tx=Math.Abs(dx)<1e-10?double.MaxValue:halfWidth/Math.Abs(dx);var ty=Math.Abs(dy)<1e-10?double.MaxValue:halfHeight/Math.Abs(dy);
            var scale=Math.Min(tx,ty);x=(float)(dx*scale);y=(float)(dy*scale);tangent=tx<ty?0:90;
        }
        public static float Normalize(float degrees){degrees%=360;return degrees<0?degrees+360:degrees;}
        public static float Difference(float a,float b){var d=Math.Abs(Normalize(a)-Normalize(b));return Math.Min(d,360-d);}
    }
}
