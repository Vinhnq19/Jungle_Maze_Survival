using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Screen.Components
{
    public class Row : MonoBehaviour
    {
        public TextMeshProUGUI numberOrder;
        public Image           avatar;
        public TextMeshProUGUI playerName;
        public TextMeshProUGUI score;
        public TextMeshProUGUI kill;
        public TextMeshProUGUI death;

        public void SetData(int numberOrder, Sprite avatar, string playerName, int score, int kill, int death)
        {
            this.numberOrder.text = "#" + numberOrder;
            this.avatar.sprite = avatar;
            this.playerName.text = playerName;
            this.score.text = score.ToString();
            this.kill.text = kill.ToString();
            this.death.text = death.ToString();
        }
    }
}