using System;
using JungleMaze.Domain.MatchUi;

namespace JungleMaze.App.MatchUi
{
    /// <summary>
    /// Text of the Eliminated screen (AC19). Each method returns null when its data is missing, and the view hides
    /// that line instead of printing "0", "null" or a dash (AC20). Rank is printed as given, ties included (AC22).
    /// Built once per elimination, so plain string formatting is fine here.
    /// </summary>
    public static class EliminationText
    {
        public const string Title = "Bạn đã bị loại";

        public static string Rank(in EliminationInfo info) => info.HasRank ? "#" + info.Rank + " / " + info.StartCount : null;

        public static string Cause(in EliminationInfo info)
        {
            switch (info.Cause)
            {
                case EliminationCause.Player: return KilledBy(info);
                case EliminationCause.Zone: return "Bị bo thu hẹp loại";
                case EliminationCause.LeftMatch: return "Bạn đã rời trận";
                case EliminationCause.Disconnected: return "Mất kết nối quá lâu";
                default: return "Không rõ nguyên nhân";
            }
        }

        public static string Kills(in EliminationInfo info) => info.HasKills ? "Hạ gục: " + info.Kills : null;

        public static string Survival(in EliminationInfo info) => info.HasSurvival ? "Sống sót: " + Clock(info.SurvivalSeconds) : null;

        /// <summary>mm:ss, minutes not capped (a match is shorter than 100 minutes).</summary>
        public static string Clock(float seconds)
        {
            var total = (int)Math.Floor(Math.Max(0f, seconds));
            var m = total / 60;
            var s = total % 60;
            return (m < 10 ? "0" : "") + m + ":" + (s < 10 ? "0" : "") + s;
        }

        private static string KilledBy(in EliminationInfo info)
        {
            var text = info.HasKillerName ? "Bị " + info.KillerName + " hạ" : "Bị người chơi khác hạ";
            if (info.HasWeapon) text += " bằng " + info.WeaponName;
            if (info.HasDistance) text += " · cách " + (int)Math.Round(info.DistanceCells) + " ô";
            return text;
        }
    }
}
