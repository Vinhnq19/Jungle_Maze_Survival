using System.Collections.Generic;
using System.Linq;
using _Codebase_.Entity._Abstract_;

namespace Utils
{

    public static class StatHelper
    {
        public static float GetStatValue(this List<Stat> stats, StatEnum statEnum)
        {
            return stats.First(e => e.statEnum == statEnum).value;
        }

        public static void SetStatValue(this List<Stat> stats, StatEnum statEnum, float value)
        {
            stats.First(e => e.statEnum == statEnum).value = value;
        }

        public static void AddValueToStat(this List<Stat> stats, StatEnum statEnum, float value)
        {
            stats.First(e => e.statEnum == statEnum).value += value;
        }
    }
}