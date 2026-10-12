using System;
using System.Collections.Generic;
using _Codebase_.Configs; 
using _Codebase_.Entity.Projectile;
using Cysharp.Threading.Tasks;
using Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace Managers
{

    public class GameManager : MonoBehaviour
    {
        private readonly Queue<Action> mainThreadActionQueue = new();

        public int? CurrentGameId { get; private set; }

        public IGameState CurrentState { get; private set; }

        public HashSet<ProjectileEntity> Projectiles { get; } = new();

        public float WaitingToStartTimer { get; set; }
        public float CountDownTimer { get; set; }
        public bool IsPlaying { get; set; }

        private void Start()
        {
            UniTask
                .DelayFrame(1)
                .ContinueWith(this.TransitionTo<GameLobbyState>)
                .Forget();
        }

        private void Update()
        {
            while (this.mainThreadActionQueue.TryDequeue(out var queue))
            {
                queue();
            }

            this.CurrentState?.Tick();
            this.PauseGame();
        }


        public void RunOnMainThread(Action action)
        {
            this.mainThreadActionQueue.Enqueue(action);
        }

        public void TransitionTo<T>()
            where T : IGameState, new()
        {
            var state = new T();
            this.CurrentState?.Exit();
            this.CurrentState = state;
            this.CurrentState?.Enter();
        }

        public void ReloadToLobby()
        {
            GameConfig.I.LoadFromPrefs();
            Debug.Log("GameConfig reloaded from PlayerPrefs.");

            if (NetworkManager.I != null)
            {
                NetworkManager.I.ResetConnection();
            }
            else
            {
                Debug.LogError("NetworkManager not found, cannot reset connection.");
            }

            TransitionTo<GameLobbyState>();
            Debug.Log("Transitioning to GameLobbyState to create a new room.");
        }

        public void PauseGame()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Time.timeScale = Time.timeScale == 0f ? 1f : 0f;
            }
        }

        public void GenerateGameId()
        {
            this.CurrentGameId = (int)((Random.value + 1) * 100000);
            Debug.Log("New Game Id Generated: " + this.CurrentGameId);
        }

        public void ExitGame()
        {
            Application.Quit();
        }

        #region Singleton

        public static GameManager I { get; private set; }

        private void Awake()
        {
            I = this;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void RuntimeInitializeOnLoad()
        {
            I = null;
        }

    #endregion

    }
}