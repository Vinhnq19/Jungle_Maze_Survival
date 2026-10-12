using System.Collections.Generic;
using UnityEngine;
using Utils;

namespace _Codebase_.Command
{
    public class CommandManager : MonoBehaviour
    {
        private readonly HashSet<ICommandResolver> _resolvers = new();

        private float startTime;
        private float timer;

        private void Update()
        {
            this.timer = Time.time;

            if (this.timer - this.startTime >= this.GetStepTime())
            {
                this.startTime = this.timer;

                foreach (var resolver in this._resolvers)
                {
                    resolver.OnFrameUpdate();
                }
            }
        }

        private void OnEnable()
        {
            this.startTime = Time.time;
            this.timer = 0;
        }

        public void RegisterResolver(ICommandResolver resolver)
        {
            this._resolvers.Add(resolver);
        }

        public void UnregisterResolver(ICommandResolver resolver)
        {
            this._resolvers.Remove(resolver);
        }

        #region Singleton

        public static CommandManager I { get; private set; }

        private void Awake()
        {
            I = this;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void RuntimeInitializeOnLoadMethod()
        {
            I = null;
        }

        #endregion

    }
}