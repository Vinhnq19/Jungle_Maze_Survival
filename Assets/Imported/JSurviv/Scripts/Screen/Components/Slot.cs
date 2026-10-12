using _Codebase_.Entity.Player;
using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Screen.Components
{
    public class Slot : MonoBehaviour
    {
        [SerializeField] private Button RemoveBtn;
        [SerializeField] private Image playerImage;
        [SerializeField] private TextMeshProUGUI playerName;
        [SerializeField] private Image backgroundImage;

        public PlayerEntity Player { get; private set; }
        public bool IsHasPlayer { get; private set; }

        private void Awake()
        {
            if (RemoveBtn != null)
            {
                RemoveBtn.onClick.AddListener(() =>
                {
                    if (this.Player != null)
                    {
                        LobbyManager.I.RemovePlayer(this.Player);
                    }
                });
            }
            
            ClearSlot();
        }

        public void AddPlayerToSlot(PlayerEntity player)
        {
            if (this.IsHasPlayer && this.Player == player)
            {
                return;
            }

            this.IsHasPlayer = true;
            this.Player = player;
            
            if (backgroundImage != null) backgroundImage.sprite = GameUIManager.I.lobbyScreen.HasPlayerSlot;
            if (playerImage != null)
            {
                playerImage.sprite = player.GetBodyVisual();
                playerImage.gameObject.SetActive(true);
            }
            if (playerName != null) playerName.text = PlayerEntity.FormatName(player.Name, 10);
            if (RemoveBtn != null) RemoveBtn.gameObject.SetActive(true);
        }
        
        public void ClearSlot()
        {
            this.IsHasPlayer = false;
            this.Player = null;
            
            if (backgroundImage != null) backgroundImage.sprite = GameUIManager.I.lobbyScreen.NonPlayerSlot;
            if (playerImage != null)
            {
                playerImage.gameObject.SetActive(false);
                playerImage.sprite = null;
            }
            if (playerName != null) playerName.text = "";
            if (RemoveBtn != null) RemoveBtn.gameObject.SetActive(false);
        }

        public bool IsPlayer(PlayerEntity player)
        {
            return this.Player == player;
        }
    }
}