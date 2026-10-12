using System;
using Screen;
using UnityEngine;

namespace Managers
{

    public class GameUIManager : MonoBehaviour
    {

        [field: SerializeField]
        public LobbyScreen lobbyScreen;

        [field: SerializeField]
        public GameScreen gameScreen;

        [SerializeField]
        public RankingScreen rankingScreen;

        [SerializeField]
        public CountDownScreen countDownScreen;

        [SerializeField]
        public LoadingScreen loadingScreen;

        [SerializeField]
        public TimeOutScreen timeOutScreen;

        [SerializeField]
        private double remainingTime;

        private void Start()
        {
            this.lobbyScreen.Show();
            this.gameScreen.Hide();
            this.rankingScreen.Hide();
            this.countDownScreen.Hide();
            this.loadingScreen.Hide();
            this.timeOutScreen.Hide();
        }

        public void OpenGameScreen(Action onTimeoutAction, Action onSkipAction)
        {
            this.gameScreen.Show();
            this.remainingTime = MapManager.I.MapTime / 60;
            this.gameScreen.RemainingTime = TimeSpan.FromMinutes(this.remainingTime);
            this.gameScreen.onTimeout = onTimeoutAction;
            this.timeOutScreen.OnSkip = onSkipAction;
        }

        public void CloseGameScreen()
        {
            this.gameScreen.Hide();
            this.gameScreen.onTimeout = null;
        }

        #region Singleton

        public static GameUIManager I { get; private set; }

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