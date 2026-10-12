using Managers;
using TMPro;
using UnityEngine;

namespace Screen.Components
{
    public class ScreenGameId : MonoBehaviour
    {
        private TextMeshProUGUI gameIdText;

        private void Start()
        {
            this.gameIdText = this.GetComponentInChildren<TextMeshProUGUI>();
        }

        private void Update()
        {
            this.gameIdText.text = GameManager.I.CurrentGameId?.ToString();

            if (Input.GetKeyUp(KeyCode.Mouse0) || Input.GetKeyUp(KeyCode.KeypadEnter))
            {
                this.gameObject.SetActive(false);
            }
        }
    }
}