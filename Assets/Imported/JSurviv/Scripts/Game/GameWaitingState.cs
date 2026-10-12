
using System;
using System.Linq;
using Managers;
using Managers.DataObjects;
using UnityEngine;

namespace Game
{
    public class GameWaitingState : IGameState
    {
        private SubState subState;

        public void Enter()
        {
            this.subState = SubState.WaitingToStart;
            Time.timeScale = 0;
            CameraManager.I.ShowCams();
            CameraManager.I.SelectAll();

            NetworkManager.I.Send(
                new GameStateInit.Request
                {
                    mapSize = MapManager.I.Map.mapConfig.mapSize,
                    
                    obstacles = MapManager.I.Obstacles
                        .Where(obs => obs.Type != "TRAP")
                        .SelectMany(obs => obs.OccupiedPositions.Select(pos => new ObstacleSchema
                        {
                            Id = obs.Id,      
                            Type = obs.Type,
                            X = (int)pos.x,    
                            Y = (int)pos.y    
                        })).ToArray()
                }
            );
        }

        public void Exit()
        {
            GameUIManager.I.countDownScreen.Hide();
        }

        public void Tick()
        {
            switch (this.subState)
            {
                case SubState.WaitingToStart:
                    GameManager.I.WaitingToStartTimer -= Time.unscaledDeltaTime;
                    if (GameManager.I.WaitingToStartTimer <= 0)
                    {
                        this.subState = SubState.CountDownToStart;
                    }
                    break;

                case SubState.CountDownToStart:
                    GameUIManager.I.countDownScreen.Show();
                    GameManager.I.CountDownTimer -= Time.unscaledDeltaTime;
                    if (GameManager.I.CountDownTimer <= 0)
                    {
                        this.subState = SubState.GamePlaying;
                    }
                    break;

                case SubState.GamePlaying:
                    foreach (var player in PlayerManager.I.Players)
                    {
                        if (player != null && player.isAlive) 
                        {
                            player.Heal(100); 
                        }
                    }
                    GameManager.I.TransitionTo<GamePlayingState>();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public enum SubState
    {
        WaitingToStart,
        CountDownToStart,
        GamePlaying,
    }
}