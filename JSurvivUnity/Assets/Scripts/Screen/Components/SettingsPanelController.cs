using UnityEngine;
using UnityEngine.UI;
using TMPro;
using _Codebase_.Configs;
using Managers; 

public class SettingsPanelController : MonoBehaviour
{
    [Header("UI Input Fields")]
    [SerializeField] private TMP_InputField serverUrlInputField;
    [SerializeField] private TMP_InputField secretKeyInputField;
    [SerializeField] private TMP_InputField stepTimeInputField;

    [Header("Control Buttons")]
    [SerializeField] private Button saveButton;
    [SerializeField] private Button backButton;

    void Start()
    {
        saveButton.onClick.AddListener(OnSaveButtonPressed);
        backButton.onClick.AddListener(OnBackButtonPressed);
    }

    private void OnEnable()
    {
        LoadDataFromConfig();
    }

    private void LoadDataFromConfig()
    {
        if (GameConfig.I == null)
        {
            Debug.LogError("GameConfig not found! Cannot load settings.");
            return;
        }

        serverUrlInputField.text = GameConfig.I.serverUrl;
        secretKeyInputField.text = GameConfig.I.secretKey;
        stepTimeInputField.text = GameConfig.I.stepTime.ToString();
    }

    private void OnSaveButtonPressed()
    {
        if (GameConfig.I == null) return;

        GameConfig.I.serverUrl = serverUrlInputField.text;
        GameConfig.I.secretKey = secretKeyInputField.text;

        if (float.TryParse(stepTimeInputField.text, out float newStepTime))
        {
            GameConfig.I.stepTime = Mathf.Max(0.5f, newStepTime); 
        }
        else
        {
            Debug.LogWarning($"Invalid input for Step Time: '{stepTimeInputField.text}'. Using original value.");
        }

        GameConfig.I.SaveToPrefs();
        Debug.Log("Settings have been saved successfully!");

        GameManager.I.ReloadToLobby();

        gameObject.SetActive(false);
    }

    private void OnBackButtonPressed()
    {
        gameObject.SetActive(false);
    }
}