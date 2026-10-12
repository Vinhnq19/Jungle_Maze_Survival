using _Codebase_.Configs;

namespace Utils
{
    public static class Extensions
    {
        public static float GetStepTime(this object _)
        {
            return GameConfig.I.stepTime;
        }
    }
}