using System.Collections.Generic;
using System.Linq;
using _Codebase_.Command;
using _Codebase_.Entity._Abstract_;
using _Codebase_.Entity.Weapon;
using Managers;
using Managers.DataObjects;
using UnityEngine;
using Utils;
using _Concrete_.Entities.Destructible;

namespace Game
{
    public class GamePlayingState : IGameState, ICommandResolver
    {
        private float time = 46f;

        public void Enter()
        {
            PlayerManager.I.RemovePreGameStunFromAllPlayers();

            Time.timeScale = 1;

            NetworkManager.I.Send(new GameStatusStart.Request());

            GameUIManager.I.gameScreen.Show();

            GameUIManager.I.OpenGameScreen(this.OnTimeout, this.OnSkip);

            CameraManager.I.DeselectAll();

            CameraManager.I.PlayerCams[Random.Range(0, PlayerManager.I.Players.Count)].Selected = true;

            MapManager.I.Map.borderSystem.SetGameStarted(true);

            CommandManager.I.gameObject.SetActive(true);

            CommandManager.I.RegisterResolver(this);
        }

        public void Exit()
        {
            GameUIManager.I.CloseGameScreen();
            MapManager.I.HideBorder();
            CommandManager.I.UnregisterResolver(this);
        }

        public void Tick()
        {
            // first spawn in the frame the border is activated
            if (MapManager.I.Map.borderSystem.IsBorderActivated)
            {
                // spawn weapons and then spawn after 46 seconds
                if (this.time < 46)
                {
                    this.time += Time.deltaTime;
                }
                else
                {
                    if (WeaponManager.I.SpawnTime > 0)
                    {
                        this.time = 0;
                        // get all the position of player
                        var playerPositions = PlayerManager.I.Players.Select(player => player.Position).ToList();

                        //get all the position of spawned weapons
                        var weaponPositions = WeaponManager.I.Weapons.Select(weapon => weapon.Position).ToList();

                        // get all the position of items
                        var itemPositions = ItemManager.I.Items
                            .Select(item => (Vector2)(item.transform.position - Vector3.one * 0.5f)).ToList();

                        // get all the position of destructible objects
                        var destructiblePositions = MapManager.I.Destructibles
                            .Select(destructible => destructible.Positions).ToList();
                        List<Vector2> excludingPos = new();
                        excludingPos.AddRange(playerPositions);
                        excludingPos.AddRange(weaponPositions);
                        excludingPos.AddRange(itemPositions);
                        excludingPos.AddRange(destructiblePositions.SelectMany(pos => pos).ToList());
                        WeaponManager.I.SpawnWithBotAmount(WeaponType.Ranged, PlayerManager.I.Players.Count,
                            excludingPos);
                    }
                }
            }
        }

        private void OnTimeout()
        {
            NetworkManager.I.Send(new GameStatusFinish.Request());

            GameUIManager.I.timeOutScreen.Show();
            CommandManager.I.gameObject.SetActive(false);
            PlayerManager.I.BonusScoreBySurvive();
            GameUIManager.I.rankingScreen.SetData(PlayerManager.I.GetRankingList());
        }

        private void OnSkip()
        {
            GameUIManager.I.timeOutScreen.Hide();
            GameManager.I.TransitionTo<GameEndState>();
        }

        public void OnFrameUpdate()
        {
            var allEntities = new List<EntitySchema>();

            var enemyEntities = MapManager.I.Enemies.Select(e => new EntitySchema
            {
                Id = e.EnemyConfig.EnemyId.ToString(),
                Type = e.TypeEnemy,
                X = (int)e.Position.x,
                Y = (int)e.Position.y,
                Attributes = new
                {
                    is_cooldown = e.is_cooldown,
                    cooldown_step_left = e.cooldown_step_left
                }
            });
            allEntities.AddRange(enemyEntities);

            var weaponEntities = WeaponManager.I.Weapons.Where(w => w.IsMounted == false).Select(w => new EntitySchema
            {
                Id = w.Id,
                Type = w.GetWeaponTypeAsString(),
                X = (int)w.Position.x,
                Y = (int)w.Position.y,
            });
            allEntities.AddRange(weaponEntities);

            var itemHealEntities = ItemManager.I.Items.Where(i => i.IsOwned == false).Select(i => new EntitySchema
            {
                Id = i.Id,
                Type = "SUPPORT_ITEM",
                X = (int)i.transform.position.x,
                Y = (int)i.transform.position.y,
            });
            allEntities.AddRange(itemHealEntities);

            var itemArmorEntities = ItemManager.I.Armors.Where(a => a.IsPickup == false).Select(i => new EntitySchema
            {
                Id = i.Id,
                Type = i.GetArmorTypeAsString(),
                X = (int)i.Position.x,
                Y = (int)i.Position.y,
            });
            allEntities.AddRange(itemArmorEntities);

            foreach (var eggInstance in MapManager.I.DragonEggs)
            {
                var eggGameObject = eggInstance.EggObject;

                if (eggInstance && eggGameObject && eggGameObject.activeInHierarchy)
                {
                    var eggPosition = eggGameObject.transform.position;
                    var eggGridPosition = Vector2Int.FloorToInt(eggPosition);
                    
                    var chestComponent = eggGameObject.GetComponent<Chest>();
                    var currentHp = 50f;
                    if (chestComponent != null)
                    {
                        
                        currentHp = chestComponent.PublicHealth;
                        Debug.Log($"Đọc máu của Trứng Rồng: {chestComponent.PublicHealth}");
                    }

                    var eggEntity = new EntitySchema
                    {
                        Id = "DRAGON_EGG",
                        Type = "CHEST",
                        X = eggGridPosition.x,
                        Y = eggGridPosition.y,
                        Attributes = new { current_hp = currentHp }
                    };
                    allEntities.Add(eggEntity);
                }
            }

            var destructibleEntities = MapManager.I.Destructibles
                .Where(d => d.PublicPosition.HasValue
                            && !allEntities.Any(e =>
                                e.X == (int)d.PublicPosition.Value.x && e.Y == (int)d.PublicPosition.Value.y))
                .Select(d => new EntitySchema
                {
                    Id = "CHEST",
                    Type = "CHEST",
                    X = (int)d.PublicPosition.Value.x,
                    Y = (int)d.PublicPosition.Value.y,
                    Attributes = new { current_hp = d.PublicHealth }
                });
            allEntities.AddRange(destructibleEntities);

            var trapEntities = MapManager.I.Obstacles
                .Where(obs => obs.Type == "TRAP") 
                .Select(obs => new EntitySchema
                {
                    Id = obs.Id,
                    Type = obs.Type, 
                    X = (int)obs.Position.x,
                    Y = (int)obs.Position.y,
                });
            allEntities.AddRange(trapEntities);

            var projectileEntities = GameManager.I.Projectiles.Select(p =>
            {
                var sourcePlayer = p.GetSource();
                var dmg = p.GetDamage();
                var speedBullet = p.GetSpeed();

                return new EntitySchema
                {
                    Type = "BULLET",
                    X = (int)p.Position.x,
                    Y = (int)p.Position.y,
                    Attributes = new
                    {
                        speed = speedBullet,
                        damage = dmg,
                        destination_x = (int)p.Destination.x,
                        destination_y = (int)p.Destination.y,
                    }
                };
            });
            allEntities.AddRange(projectileEntities);


            NetworkManager.I.Send(new GameStateUpdate.Request
            {
                safeZoneRadius = MapManager.I.Map.borderSystem.GetCurrentWidth() / 2,

                players = PlayerManager.I.Players.Select(p => new PlayerSchema
                {
                    Id = p.Id,
                    Health = p.Stats.GetStatValue(StatEnum.Health),
                    Score = p.Score,
                    X = (int)p.Position.x,
                    Y = (int)p.Position.y,
                    CanBeSeenBy = p.GetCanBeSeenBy().ToArray(),
                    Attributes = null,
                    Type = "PLAYER"
                }).ToArray(),

                entities = allEntities.ToArray()
            });
        }
    }
}