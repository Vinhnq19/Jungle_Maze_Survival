using System.Collections.Generic;
using _Codebase_.Entity.Player;
using Screen.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Screen
{
    public class RankingScreen : BaseScreen
    {
        [field: SerializeField]
        public Button PlayNewGameBtn { get; private set; }

        [field: SerializeField]
        public Transform rowContainer;

        [field: SerializeField]
        public Row rowPrefab;

        [SerializeField]
        private TextMeshProUGUI gameIdText;

        public void SetData(List<PlayerEntity> rankingData)
        {
            foreach (Transform child in this.rowContainer)
            {
                Destroy(child.gameObject);
            }

            var currentRanking = 1;

            for (var i = 0; i < rankingData.Count; i++)
            {
                var row = Instantiate(this.rowPrefab, this.rowContainer);

                if (i != 0 && this.checkScore(rankingData[i], rankingData[i - 1]))
                {
                    row.SetData(currentRanking, rankingData[i].GetBodyVisual(), rankingData[i].Name, rankingData[i].Score, rankingData[i].Kill, rankingData[i].Dead);
                }
                else
                {
                    currentRanking = i + 1;
                    row.SetData(currentRanking, rankingData[i].GetBodyVisual(), rankingData[i].Name, rankingData[i].Score, rankingData[i].Kill, rankingData[i].Dead);
                }
            }
        }

        public void SetGameIdText(int gameId)
        {
            this.gameIdText.text = "Game ID: " + gameId;
        }

        private bool checkScore(PlayerEntity i, PlayerEntity j)
        {
            return i.Score == j.Score && i.Kill == j.Kill && i.Dead == j.Dead;
        }
    }
}