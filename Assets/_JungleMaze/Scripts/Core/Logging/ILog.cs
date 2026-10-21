namespace JungleMaze.Core.Logging
{
    /// <summary>Core/Domain không tham chiếu UnityEngine nên log qua interface; Infrastructure cấp bản Unity.</summary>
    public interface ILog
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }

    public sealed class NullLog : ILog
    {
        public static readonly NullLog Instance = new NullLog();
        private NullLog() { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
