using System;
using System.Collections.Generic;
using System.Linq;
using _Codebase_.Configs;
using _Codebase_.Entity.Player;
using Managers.DataObjects;
using Screen;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Managers
{
    public class LobbyManager : MonoBehaviour
    {
        [SerializeField] private PlayerEntity playerPrefab;
        [SerializeField] private LobbyScreen lobbyScreen;
        [SerializeField] private List<PlayerEntity> playerList;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private PlayerImageConfig playerImageConfig;
        [SerializeField] private PlayerSkinsConfig playerSkinsConfig;
        [SerializeField] private Image[] mapSlots;
        [SerializeField] private Sprite selectedMapSprite;
        [SerializeField] private Sprite unSelectedMapSprite;
        [field: SerializeField] private List<Map.Map> Maps;

        private List<int> _numberList;

        public List<PlayerEntity> PlayerList => playerList;
        public Map.Map SelectedMap { get; private set; }
        
        public void ResetState()
        {
            if (playerList != null && playerList.Count > 0)
            {
                RemoveAllPlayers();
            }

            playerList = new List<PlayerEntity>();
            _numberList = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            
            SetSelectedMap(-1); 
            RefreshLobbySlots(); 

            Debug.Log("LobbyManager: State has been fully reset.");
        }

        public void AddPlayer(string id, string playerName)
        {
            if (playerList.Count >= lobbyScreen.Slots.Count) return;

            var player = Instantiate(playerPrefab);
            player.gameObject.SetActive(false);

            if (_numberList.Count > 0)
            {
                var randomIndex = Random.Range(0, _numberList.Count);
                var selectedNumber = _numberList[randomIndex];
                _numberList.RemoveAt(randomIndex);
                
                player.Name = playerName;
                player.Id = id;
                player.RandomSkinNumber = selectedNumber;
                player.SetBodyVisual(playerImageConfig.playerImages[selectedNumber]);
                player.SetHandVisual(playerImageConfig.playerHands[selectedNumber]);
                player.gameObject.GetComponent<PlayerAnimation>().AnimationSkinData = playerSkinsConfig.skinDatas[selectedNumber];
        
                playerList.Add(player);
                RefreshLobbySlots();
            }
            else
            {
                Destroy(player.gameObject);
            }
        }

        public void AddPlayer(string playerName) => AddPlayer(Guid.NewGuid().ToString(), playerName);

        public void RemovePlayer(string playerId)
        {
            var player = playerList.FirstOrDefault(p => p.Id == playerId);
            if (player != null) RemovePlayer(player);
        }
        
        public void RemovePlayer(PlayerEntity player)
        {
            if (player == null || !playerList.Contains(player)) return;
    
            NetworkManager.I.Send(new GamePlayerKick.Request { playerId = player.Id });
    
            _numberList.Add(player.RandomSkinNumber);
            playerList.Remove(player);
            Destroy(player.gameObject);
    
            RefreshLobbySlots();
        }

        public void SetSelectedMap(int index)
        {
            if (index >= 0 && index < Maps.Count)
            {
                SelectedMap = Maps[index];
                for (int i = 0; i < mapSlots.Length; i++)
                {
                    mapSlots[i].sprite = (i == index) ? selectedMapSprite : unSelectedMapSprite;
                }
            }
            else
            {
                SelectedMap = null;
                foreach (var slot in mapSlots) slot.sprite = unSelectedMapSprite;
            }
        }

        public void RemoveAllPlayers()
        {
            for (int i = playerList.Count - 1; i >= 0; i--)
            {
                if (playerList[i] != null)
                {
                    _numberList.Add(playerList[i].RandomSkinNumber);
                    Destroy(playerList[i].gameObject);
                }
            }
            playerList.Clear();
        }
        
        private void RefreshLobbySlots()
        {
            for (int i = 0; i < lobbyScreen.Slots.Count; i++)
            {
                if (i < playerList.Count)
                {
                    lobbyScreen.Slots[i].AddPlayerToSlot(playerList[i]);
                }
                else
                {
                    lobbyScreen.Slots[i].ClearSlot();
                }
            }
            playerCountText.text = playerList.Count.ToString();
        }

        #region Singleton
        public static LobbyManager I { get; private set; }
        private void Awake() => I = this;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void RuntimeInitializeOnLoadMethod() => I = null;
        #endregion
    }
}