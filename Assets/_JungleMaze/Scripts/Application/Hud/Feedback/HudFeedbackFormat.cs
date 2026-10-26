using System;
using JungleMaze.Domain.Hud;

namespace JungleMaze.App.Hud.Feedback
{
    /// <summary>Allocation-free HUD feedback text formatting into caller-owned buffers.</summary>
    public static class HudFeedbackFormat
    {
        public const int BufferSize=128;
        private static readonly string[] Directions={"Đông","Đông Bắc","Bắc","Tây Bắc","Tây","Tây Nam","Nam","Đông Nam"};
        public static int CompassIndex(float dx,float dy){var a=Math.Atan2(dy,dx)*180/Math.PI;if(a<0)a+=360;return (int)Math.Floor((a+22.5)/45)%8;}
        public static int Distance(float dx,float dy){var d=Math.Sqrt((double)dx*dx+(double)dy*dy);return (int)Math.Min(int.MaxValue,Math.Round(d,MidpointRounding.AwayFromZero));}
        public static int UseLabel(char[] buffer,in ItemUseStatus status)
        {
            var n=0;Append(buffer,ref n,status.Phase==ItemUsePhase.Cancelled?"Đã hủy":status.Phase==ItemUsePhase.Completed?"Xong":"Đang dùng ");
            if(status.Phase==ItemUsePhase.Using)Append(buffer,ref n,status.DisplayName??status.ItemId);return n;
        }
        public static int Tenths(char[] b,float seconds)
        {
            var n=0;var value=(int)Math.Min(int.MaxValue,Math.Ceiling(Math.Max(0,seconds)*10-0.00001));Number(b,ref n,value/10);Char(b,ref n,'.');Char(b,ref n,(char)('0'+value%10));Char(b,ref n,'s');return n;
        }
        public static int Seconds(char[] b,int seconds){var n=0;Number(b,ref n,Math.Max(0,seconds));Char(b,ref n,'s');return n;}
        public static int Count(char[] b,int count){var n=0;Char(b,ref n,'×');Number(b,ref n,count);return n;}
        public static int Overflow(char[] b,int count,bool items){var n=0;Char(b,ref n,'+');Number(b,ref n,count);if(items)Append(b,ref n," món khác");return n;}
        public static int Banner(char[] b,in DangerBannerOutput output)
        {
            var n=0;var w=output.Warning;
            Append(b,ref n,w.IsTimed?"! Bom nổ sau ":"! Có bẫy");if(w.IsTimed){Number(b,ref n,(int)Math.Ceiling(w.RemainingSeconds));Char(b,ref n,'s');}
            if(output.HasDirection){Append(b,ref n," · hướng ");Append(b,ref n,Directions[output.Compass]);Append(b,ref n,", ");Number(b,ref n,output.Distance);Append(b,ref n," ô");}
            if(output.OtherCount>0){Append(b,ref n," +");Number(b,ref n,output.OtherCount);}return n;
        }
        private static void Append(char[] b,ref int n,string s){if(s==null)return;for(var i=0;i<s.Length&&n<b.Length;i++)b[n++]=s[i];}
        private static void Char(char[] b,ref int n,char c){if(n<b.Length)b[n++]=c;}
        private static void Number(char[] b,ref int n,int value)
        {
            value=Math.Max(0,value);var divisor=1;while(value/divisor>=10&&divisor<=100000000)divisor*=10;
            do{Char(b,ref n,(char)('0'+value/divisor%10));divisor/=10;}while(divisor>0);
        }
    }
}
