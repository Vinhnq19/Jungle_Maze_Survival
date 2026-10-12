using System.Globalization;
using Managers;
using TMPro;
using UnityEngine;

namespace Screen
{
    public class CountDownScreen : BaseScreen
    {
        [SerializeField]
        private TextMeshProUGUI countDownText;

        private float countDown;

        private void Update()
        {
            this.countDown = Mathf.Ceil(GameManager.I.CountDownTimer) - 1;

            if (this.countDown > 0)
            {
                this.countDownText.text = this.countDown.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                this.countDownText.text = "Start!";
            }
        }
    }
}