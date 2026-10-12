using System.Collections.Generic;
using System.Linq;
using _Codebase_.EffectSystem;
using _Codebase_.Entity.Player;
using _Codebase_.Utils;
using _Concrete_.Effects;
using JetBrains.Annotations;
using UnityEngine;

namespace Managers
{
    public class PlayerManager : MonoBehaviour
    {
        [field: SerializeField]
        private PlayerEntity PlayerPrefab { get; set; }

        [SerializeField]
        private List<PlayerEntity> _players = new();

        public List<PlayerEntity> Players
        {
            get
            {
                _players.RemoveAll(p => p == null);
                return _players;
            }
        }
        public void Initialize(List<PlayerEntity> playerList, float reviveTime)
        {
            this._players = playerList;

            foreach (var player in playerList)
            {
                player.AddEffect(new StunEffect
                {
                    Time = 99999,
                });

                player.ReviveTimeBase = reviveTime;
            }
        }


        public void SpawnPlayersExcludingPosition(List<PlayerEntity> playersToSpawn, [CanBeNull] List<Vector2> excludingPosition)
        {
            DeSpawnAllPlayers();

            var newPlayers = new List<PlayerEntity>();
            var playerListPosition = new List<Vector2>();

            if (excludingPosition != null)
            {
                playerListPosition.AddRange(excludingPosition);
            }

            foreach (var player in playersToSpawn)
            {
                var position = MapManager.I.GetSafeSpawnPosition(playerListPosition);

                if (position == Vector2.zero)
                {
                    Debug.LogWarning("Unable to find valid position to spawn player.");
                    continue; 
                }

                var playerObj = Instantiate(player, position, Quaternion.identity);

                playerObj.SetBodyVisual(player.GetBodyVisual());
                playerObj.SetHandVisual(player.GetHandVisual());

                playerObj.transform.SetParent(this.transform);
                playerObj.Id = player.Id;
                playerObj.Name = player.Name;
                newPlayers.Add(playerObj);

                playerObj.gameObject.SetActive(true);

                playerListPosition.Add(Vector2Int.FloorToInt(position));
            }

            this._players = newPlayers;
        }

        public void RespawnPlayer()
        {
        }

        public void DeSpawnPlayer(PlayerEntity player)
        {
            if (!player) return;
            this._players.Remove(player);
            Destroy(player.gameObject);
        }


        public void DeSpawnAllPlayers()
        {
            for (int i = this._players.Count - 1; i >= 0; i--)
            {
                if (this._players[i] != null)
                {
                    Destroy(this._players[i].gameObject);
                }
            }

            this._players.Clear();
            Debug.Log("PlayerManager: All in-game players have been despawned.");
        }

        public void SetFinalTime()
        {
            this._players.Where(player => !player.IsDead).ForEach(player => player.SetFinalTime());
        }

        public void BonusScoreBySurvive(int score = 200)
        {
            this._players.Where(player => !player.IsDead).ForEach(player => player.Score += score);
        }

        public List<PlayerEntity> GetRankingList()
        {
            var rankingList = this.Players;
            var n = rankingList.Count;

            for (var i = 0; i < n - 1; i++)
            {
                var swapped = false;

                for (var j = 0; j < n - i - 1; j++)
                {
                    PlayerEntity temp;

                    if (rankingList[j].Score < rankingList[j + 1].Score)
                    {
                        temp = rankingList[j];
                        rankingList[j] = rankingList[j + 1];
                        rankingList[j + 1] = temp;
                        swapped = true;
                    }
                    else if (rankingList[j].Score == rankingList[j + 1].Score)
                    {
                        if (rankingList[j].Kill < rankingList[j + 1].Kill)
                        {
                            temp = rankingList[j];
                            rankingList[j] = rankingList[j + 1];
                            rankingList[j + 1] = temp;
                            swapped = true;
                        }
                        else if (rankingList[j].Kill == rankingList[j + 1].Kill)
                        {
                            if (rankingList[j].Dead > rankingList[j + 1].Dead)
                            {
                                temp = rankingList[j];
                                rankingList[j] = rankingList[j + 1];
                                rankingList[j + 1] = temp;
                                swapped = true;
                            }
                        }
                    }
                }

                if (swapped == false)
                    break;
            }

            return rankingList;
        }
        
        public void RemovePreGameStunFromAllPlayers()
        {
            foreach (var player in this.Players)
            {
                player.RemoveEffectOfType<StunEffect>();
            }
        }

        #region Singleton

        public static PlayerManager I { get; private set; }

        private void Awake()
        {
            I = this;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void RuntimeInitializeOnLoadMethod()
        {
            I = null;
        }

        #endregion
    }
}