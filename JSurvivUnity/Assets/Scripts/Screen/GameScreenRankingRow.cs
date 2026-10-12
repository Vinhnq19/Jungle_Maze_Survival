using TMPro;
using UnityEngine;

namespace Screen
{
    public class GameScreenRankingRow : MonoBehaviour
    {
        [field: SerializeField]
        private TMP_Text TextName { get; set; }

        [field: SerializeField]
        private TMP_Text TextPoint { get; set; }

        public string Name
        {
            get => this.TextName.text;
            set => this.TextName.text = value;
        }

        public int Point
        {
            get => int.TryParse(this.TextPoint.text, out var point) ? point : 0;
            set => this.TextPoint.text = value.ToString();
        }
    }
}