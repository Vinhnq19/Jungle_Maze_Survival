using System;
using System.Linq;
using _Codebase_.Entity.Player;
using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Screen
{

    public class GameScreen : BaseScreen
    {

        [field: SerializeField]
        public TMP_Text TextTime { get; private set; }

        [field: SerializeField]
        public TMP_Text BorderTime { get; private set; }

        [field: SerializeField]
        public GameObject RankingContainer { get; private set; }

        [field: SerializeField]
        public Transform RankingContentTf { get; private set; }

        [field: SerializeField]
        public GameScreenRankingRow GameScreenRankingRowPrefab { get; private set; }

        [SerializeField]
        private Transform playerInfo;

        [SerializeField]
        private Image[] WeaponsImage;

        [SerializeField]
        private Image[] ItemsImage;

        [SerializeField]
        private Image[] ArmorsImage;

        [SerializeField]
        private TextMeshProUGUI scoreText;

        [SerializeField]
        private TextMeshProUGUI killText;

        [SerializeField]
        private TextMeshProUGUI deathText;

        [SerializeField]
        private Transform reviveTimeTransform;

        [SerializeField]
        private Transform outTransform;

        [SerializeField]
        private TextMeshProUGUI reviveTimeText;

        [SerializeField]
        private TextMeshProUGUI playerName;

        public Action onTimeout;
        public static GameScreen Instance { get; set; }

        public TimeSpan RemainingTime { get; set; }

        /// <summary>
        ///     The remaining time of the game that player can not revive
        /// </summary>
        public float ReviveTimeLimit { get; set; }


        private void Awake()
        {
            Instance = this;

            // Duplicate rows
            for (var i = 0; i < 15; i++)
            {
                Instantiate(
                    this.GameScreenRankingRowPrefab,
                    this.RankingContentTf
                );
            }
        }

        private void Update()
        {
            var player = CameraManager.I.PlayerCams.Where(s => s.Selected).Select(p => p.PlayerInfo).ToList();

            if (player.Count == 1)
            {
                this.playerInfo.gameObject.SetActive(true);
                this.UpdateInventory();
                this.UpdateKDA();
                this.UpdateBotName();

                if (player[0].IsDead)
                {
                    if (player[0].IsOut)
                    {
                        this.outTransform.gameObject.SetActive(true);
                        this.reviveTimeTransform.gameObject.SetActive(false);
                    }
                    else
                    {
                        this.outTransform.gameObject.SetActive(false);
                        this.reviveTimeTransform.gameObject.SetActive(true);
                        this.UpdateReviveTime();
                    }
                }
                else
                {
                    this.outTransform.gameObject.SetActive(false);
                    this.reviveTimeTransform.gameObject.SetActive(false);
                }
            }
            else
            {
                this.playerInfo.gameObject.SetActive(false);
            }

            if (this.RemainingTime.TotalSeconds > 0)
            {
                if (this.RemainingTime.TotalSeconds <= this.ReviveTimeLimit)
                {
                    // Player
                    PlayerManager.I.SetFinalTime();
                }

                this.RemainingTime = this.RemainingTime.Subtract(TimeSpan.FromSeconds(Time.deltaTime));
                var ts = this.RemainingTime;
                this.TextTime.text = $"{ts.Minutes:00}:{ts.Seconds:00}";

                if (this.RemainingTime.TotalSeconds <= 0)
                {
                    this.onTimeout?.Invoke();
                }
            }
        }

        public void UpdateInventory()
        {
            var list = CameraManager.I.PlayerCams.Where(s => s.Selected).Select(p => p.PlayerInfo).ToList();

            if (list.Count == 1)
            {
                for (var i = 0; i < this.ItemsImage.Length; i++)
                {
                    if (i < 4) this.WeaponsImage[i].enabled = false;
                    this.ItemsImage[i].enabled = false;
                    if (i < 2) this.ArmorsImage[i].enabled = false;
                }

                for (var i = 0; i < this.ItemsImage.Length; i++)
                {
                    try
                    {
                        if (list[0].weapons[i] != null)
                        {
                            this.WeaponsImage[i].enabled = true;
                            this.WeaponsImage[i].sprite = list[0].weapons[i].OnGroundSprite;
                        }
                    }
                    catch
                    {
                        // ignored
                    }

                    try
                    {
                        if (list[0].items[i] != null)
                        {
                            this.ItemsImage[i].enabled = true;
                            this.ItemsImage[i].sprite = list[0].items[i].SpriteRenderer.sprite;
                        }
                    }
                    catch
                    {
                        // ignored
                    }

                    try
                    {
                        if (list[0].armors[i] != null)
                        {
                            this.ArmorsImage[i].enabled = true;
                            this.ArmorsImage[i].sprite = list[0].armors[i].OnGroundSprite;
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
            else
            {
                this.ClearItems();
            }
        }

        private void ClearItems()
        {
            for (var i = 0; i < this.ItemsImage.Length; i++)
            {
                if (i < 3) this.WeaponsImage[i].enabled = false;
                this.ItemsImage[i].enabled = false;
                if (i < 2) this.ArmorsImage[i].enabled = false;
            }
        }

        private void UpdateKDA()
        {
            var list = CameraManager.I.PlayerCams.Where(s => s.Selected).Select(p => p.PlayerInfo).ToList();

            if (list.Count == 1)
            {
                this.scoreText.text = list[0].Score.ToString();
                this.killText.text = list[0].Kill.ToString();
                this.deathText.text = list[0].Dead.ToString();
            }
        }

        private void UpdateReviveTime()
        {
            var list = CameraManager.I.PlayerCams.Where(s => s.Selected).Select(p => p.PlayerInfo).ToList();

            if (list.Count == 1)
            {
                this.reviveTimeText.text = Mathf.Floor(list[0].ReviveTimeCounter) + "s";
            }
        }

        private void UpdateBotName()
        {
            var list = CameraManager.I.PlayerCams.Where(s => s.Selected).Select(p => p.PlayerInfo).ToList();

            if (list.Count == 1)
            {
                this.playerName.text = PlayerEntity.FormatName(list[0].Name, 25);
            }
        }
    }
}