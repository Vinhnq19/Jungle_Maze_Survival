using System.Collections.Generic;
using _Codebase_.Entity.Weapon;
using Managers;
using Managers.DataObjects;
using UnityEngine;

namespace Game
{
    public class GameLoadingState : IGameState
    {
        public void Enter()
        {
            GameManager.I.WaitingToStartTimer = 1f;

            GameManager.I.CountDownTimer = 3.99f;

            GameUIManager.I.loadingScreen.Show();

            //NetworkManager.I.Send(new GameSessionCreate.Request
            //{
            //    JoinCode = GameManager.I.CurrentGameId.ToString(),
            //});

            if (GameManager.I.IsPlaying)
            {
                MapManager.I.SetUp();

                GameUIManager.I.gameScreen.ReviveTimeLimit = MapManager.I.Map.mapConfig.limitTimeToRevive;
                var destructiblePosList = MapManager.I.GenerateDestructiblePositions();
                var weaponPosList = WeaponManager.I.SpawnWithWeaponAmount(WeaponType.Ranged, MapManager.I.Map.mapConfig.InitialWeaponSpawnCount, destructiblePosList);

                var excludingPosition = new List<Vector2>();
                excludingPosition.AddRange(weaponPosList);
                excludingPosition.AddRange(destructiblePosList);

                PlayerManager.I.SpawnPlayersExcludingPosition(LobbyManager.I.PlayerList, excludingPosition);
                
                Debug.Log("MapManager is loading config file: " + MapManager.I.Map.mapConfig.name);
                PlayerManager.I.Initialize(PlayerManager.I.Players, MapManager.I.Map.mapConfig.baseReviveTime);

                WeaponManager.I.SetUp(PlayerManager.I.Players.Count, MapManager.I.Map);

                GameUIManager.I.rankingScreen.SetGameIdText(GameManager.I.CurrentGameId ?? 0);
            }
        }

        public void Exit()
        {
            GameUIManager.I.loadingScreen.Hide();
        }

        public void Tick()
        {
            if (GameUIManager.I.loadingScreen.IsDone)
            {
                if (GameManager.I.IsPlaying)
                {
                    GameManager.I.TransitionTo<GameWaitingState>();
                }
                else
                {
                    GameManager.I.TransitionTo<GameLobbyState>();
                }
            }
        }
    }
}