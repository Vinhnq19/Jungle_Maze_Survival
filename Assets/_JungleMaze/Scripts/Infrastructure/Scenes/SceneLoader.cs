using System;
using JungleMaze.App.Ports;
using JungleMaze.Core.Logging;
using UnityEngine.SceneManagement;

namespace JungleMaze.Infrastructure.Scenes
{
    /// <summary>Load scene async (Single); scene phải có trong Build Settings.</summary>
    public sealed class SceneLoader : ISceneLoader
    {
        private readonly ILog _log;
        private bool _loading;

        public SceneLoader(ILog log) => _log = log ?? NullLog.Instance;

        public string ActiveScene => SceneManager.GetActiveScene().name;

        public void Load(string sceneName, Action onLoaded = null)
        {
            if (_loading) { _log.Warn($"[Scene] Đang load scene khác, bỏ qua {sceneName}."); return; }
            if (ActiveScene == sceneName) { onLoaded?.Invoke(); return; }
            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null) { _log.Error($"[Scene] Không load được {sceneName}; đã thêm vào Build Settings chưa?"); return; }
            _loading = true;
            operation.completed += _ =>
            {
                _loading = false;
                onLoaded?.Invoke();
            };
        }
    }
}
