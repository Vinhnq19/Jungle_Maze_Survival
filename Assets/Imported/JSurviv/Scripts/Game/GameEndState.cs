using Managers;
using UnityEngine;

namespace Game
{
    public class GameEndState : IGameState
    {
        public void Enter()
        {
            GameManager.I.IsPlaying = false;

            if (GameUIManager.I != null)
            {
                GameUIManager.I.rankingScreen.Show();
                GameUIManager.I.rankingScreen.PlayNewGameBtn.onClick.AddListener(this.OnPlayAgain);
            }

            if (WeaponManager.I != null) WeaponManager.I.DeSpawnAllWeapons();
            if (ItemManager.I != null) ItemManager.I.DeSpawnAllItems();
            if (CameraManager.I != null) CameraManager.I.DeSpawnAllCams();
            if (MapManager.I != null) MapManager.I.DespawnMap();
            if (PlayerManager.I != null) PlayerManager.I.DeSpawnAllPlayers();
            
            if (LobbyManager.I != null) LobbyManager.I.RemoveAllPlayers();
            if (GameManager.I != null) GameManager.I.Projectiles.Clear();

            EventManager.I.ClearAllListeners();
        }

        public void Exit()
        {
            if (GameUIManager.I != null)
            {
                GameUIManager.I.rankingScreen.Hide();
                GameUIManager.I.rankingScreen.PlayNewGameBtn.onClick.RemoveListener(this.OnPlayAgain);
            }
        }

        public void Tick() { }

        private void OnPlayAgain()
        {
            if (GameManager.I != null)
            {
                GameManager.I.ReloadToLobby();
            }
        }
    }
}