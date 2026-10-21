using JungleMaze.Core.Logging;
using UnityEngine;

namespace JungleMaze.Infrastructure.Logging
{
    /// <summary>ILog cho runtime Unity; Info bị tắt ở release build để không spam log.</summary>
    public sealed class UnityLog : ILog
    {
        public void Info(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(message);
#endif
        }

        public void Warn(string message) => Debug.LogWarning(message);
        public void Error(string message) => Debug.LogError(message);
    }
}
