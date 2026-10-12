using _Codebase_.Configs;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Screen
{
    public class GameConfigScreen : MonoBehaviour
    {
        private GameConfig _gameConfig;

        [SerializeField] private TMP_InputField serverUrl;
        [SerializeField] private TMP_InputField secretKey;
        [SerializeField] private TMP_InputField stepTime;
        [SerializeField] private Button connectButton;

        private void Start()
        {
            _gameConfig = GameConfig.I;

            if (_gameConfig == null)
            {
                Debug.LogError("GameConfig not loaded.");
                return;
            }

            // Populate fields
            serverUrl.text = _gameConfig.serverUrl;
            secretKey.text = _gameConfig.secretKey;
            stepTime.text = _gameConfig.stepTime.ToString("0.##");

            // Add listener
            connectButton.onClick.AddListener(OnConnectClicked);
        }

        private void OnConnectClicked()
        {
            _gameConfig.serverUrl = serverUrl.text;
            _gameConfig.secretKey = secretKey.text;

            _gameConfig.stepTime = float.TryParse(stepTime.text, out float parsedStepTime) ? Mathf.Max(0.5f, parsedStepTime) : 0.5f;

            _gameConfig.SaveToPrefs(); // Save to PlayerPrefs

            SceneManager.LoadScene("GameMain");
        }

    }
}