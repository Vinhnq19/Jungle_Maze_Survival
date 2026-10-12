using Managers;
using UnityEngine;

namespace Game
{
    public class GameLobbyState : IGameState
    {
        public void Enter()
        {
            GameManager.I.IsPlaying = false;
            GameManager.I.GenerateGameId();

            GameUIManager.I.lobbyScreen.Show();
            
            GameUIManager.I.lobbyScreen.PlayBtn.onClick.AddListener(this.OnClickPlayBtn);
            GameUIManager.I.lobbyScreen.SetOnClickMapButton(this.OnSelectMap);

            GameUIManager.I.lobbyScreen.PlayBtn.interactable = false;

            if (LobbyManager.I != null)
            {
                LobbyManager.I.ResetState(); 
            }
        }

        public void Exit()
        {
            GameUIManager.I.lobbyScreen.Hide();
            GameUIManager.I.lobbyScreen.PlayBtn.onClick.RemoveListener(this.OnClickPlayBtn);
            GameUIManager.I.lobbyScreen.SetOnClickMapButton(null);
        }

        public void Tick()
        {
            if (LobbyManager.I.PlayerList.Count > 1 && LobbyManager.I.SelectedMap != null)
            {
                GameUIManager.I.lobbyScreen.PlayBtn.interactable = true;
            }
            else
            {
                GameUIManager.I.lobbyScreen.PlayBtn.interactable = false;
            }
        }

        private void OnSelectMap(int index) => LobbyManager.I.SetSelectedMap(index);

        private void OnClickPlayBtn()
        {
            if (LobbyManager.I.PlayerList.Count <= 1 || LobbyManager.I.SelectedMap == null || GameManager.I.CurrentGameId == null) return;
            
            GameManager.I.IsPlaying = true;
            GameManager.I.TransitionTo<GameLoadingState>();
        }
    }
}