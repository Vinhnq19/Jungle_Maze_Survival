using System;

namespace JungleMaze.App.Ports
{
    /// <summary>State gọi qua port này thay vì gọi SceneManager trực tiếp.</summary>
    public interface ISceneLoader
    {
        string ActiveScene { get; }
        void Load(string sceneName, Action onLoaded = null);
    }
}
