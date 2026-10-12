using UnityEngine;

namespace _Codebase_.Configs
{

    [CreateAssetMenu(fileName = "GameConfig", menuName = "ScriptableObjects/GameConfig", order = 1)]
    public class GameConfig : ScriptableObject
    {

        #region Configs

        public string secretKey = "";
        public string serverUrl = "https://cf25-server.jsclub.dev";
        public float  stepTime  = .5f;

        #endregion

        #region Singleton

        public static GameConfig I { get; private set; }
        
        public void SaveToPrefs()
        {
            PlayerPrefs.SetString("serverUrl", serverUrl);
            PlayerPrefs.SetString("secretKey", secretKey);
            PlayerPrefs.SetFloat("stepTime", stepTime);
            PlayerPrefs.Save();
        }

        public void LoadFromPrefs()
        {
            if (PlayerPrefs.HasKey("serverUrl"))
                serverUrl = PlayerPrefs.GetString("serverUrl");

            if (PlayerPrefs.HasKey("secretKey"))
                secretKey = PlayerPrefs.GetString("secretKey");

            if (PlayerPrefs.HasKey("stepTime"))
                stepTime = Mathf.Max(0.5f, PlayerPrefs.GetFloat("stepTime")); // Ensure min 0.5
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RuntimeInitializeOnload()
        {
            I = Resources.Load<GameConfig>("GameConfig");

            if (I == null)
            {
                Debug.LogError("GameConfig not found");
            }
            else
            {
                I.LoadFromPrefs(); // <- Load saved settings from previous sessions
            }
        }


        #endregion

    }
}