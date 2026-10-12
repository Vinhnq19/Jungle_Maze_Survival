using System;
using System.Collections.Generic;
using System.Linq;
using _Codebase_.Command;
using _Codebase_.EffectSystem;
using _Codebase_.Entity._Abstract_;
using _Codebase_.Entity.Armor;
using _Codebase_.Entity.Destructible;
using _Codebase_.Entity.Item;
using _Codebase_.Entity.Weapon;
using _Concrete_.Effects;
using _Concrete_.Entities.CanGoThrough;
using _Concrete_.Entities.Destructible;
using DG.Tweening;
using Managers;
using Managers.DataObjects;
using UnityEngine;
using Utils;

namespace _Codebase_.Entity.Player
{

    public partial class PlayerEntity : BaseMovableEntity, IEntity, IDamageable, IAffectable, ICommandResolver
    {
        private const float DEFAULT_LOOK_DURATION = 0.15f;

        private readonly BaseWeaponEntity currentWeapon;

        private Tween _movingTween;

        private string _name;
        private Tween  _shakeScaleTween;

        private Sprite bodySprite;

        private float  cooldown;
        private float  cooldownCounter;
        private Sprite handSprite;
        public bool   isAlive = true;
        private bool   isFinalTime;

        public  Queue<MoveCommand> MovementQueue = new();
        public  ICommand           PlayerAction;
        private BaseItemEntity     usingItem;

        public float ReviveTimeBase { get; set; }
        public float ReviveTimeCounter { get; private set; }
        public Vector2 LookDirection => -this.Visual.up;

        public int Score { get; set; }
        public int Kill { get; set; }
        public int KillStreak { get; private set; }
        public int Dead { get; set; }

        public bool IsDead => this.ReviveTimeCounter > 0;
        public bool IsCoolingDown => this.cooldownCounter > 0;

        public int RandomSkinNumber { get; set; }
        public int BulletLeft => this.GetWeaponOfType(WeaponType.Ranged)?.CurrentAmount ?? 0;

        public string Id { get; set; }

        public string Name
        {
            get => this._name;
            set
            {
                this._name = value;

                if (this.nameTextMeshPro != null)
                {
                    this.nameTextMeshPro.text = FormatName(this._name, 10);
                }
            }
        }

        public bool IsUsingItem => this.usingItem;

        public bool IsOut { get; set; }

        public bool IsImmortal { get; set; }
        public bool isCantUseWeapon { get; set; }
        public float DamageMultiplier { get; set; } = 1f;
        public bool IsReverseMovement { get; set; }

        public Func<float, PlayerEntity, bool> OnTakeDamageIntercept { get; set; }

        
        public PlayerState PlayerState { get; private set; }
        

        private void Start()
        {
            this.body.GetComponent<SpriteRenderer>().sprite = this.bodySprite;
            this.lHand.GetComponent<SpriteRenderer>().sprite = this.handSprite;
            this.rHand.GetComponent<SpriteRenderer>().sprite = this.handSprite;
            // this.nameTextMeshPro.text = FormatName(this.Name, 10);

            this.isFinalTime = false;
            this.IsOut = false;
            this.SetAlpha(1.0f);

            this.PlayerState = PlayerState.Idle;
        }

        private void Update()
        {
            this.ApplyEffects();

            // Armor
            this.Stats.SetStatValue(StatEnum.Armor, this.GetDamageReduction());

            // // Debugging
            //this.DebugUpdate();

            // Cooldown
            if (this.cooldownCounter >= 0)
            {
                this.cooldownCounter -= Time.deltaTime;
            }

            if (this.IsDead)
            {
                if (this.isFinalTime)
                {
                    this.IsOut = true;
                }
                else
                {
                    this.ReviveTimeCounter -= Time.deltaTime;
                    this.MovementQueue?.Clear();
                    this.PlayerAction = null;

                    if (!this.IsDead)
                    {
                        this.OnRevive();
                    }
                }
            }
        }

        private void OnEnable()
        {
            this.SetAlpha(1.0f);
            CommandManager.I.RegisterResolver(this);
        }

        private void OnDisable()
        {
            this.SetAlpha(1.0f);
            CommandManager.I.UnregisterResolver(this);
        }

        public List<IEffect> Effects { get; } = new();

        public void OnFrameUpdate()
        {
            if (this.IsUsingItem) return;

            if (this.PlayerAction != null)
            {
                this.PlayerAction.Execute();
                this.PlayerAction = null;
                return;
            }

            if (this.MovementQueue.Count == 0) return;
            var commandToExecute = this.MovementQueue.Dequeue();
            commandToExecute.Execute();
        }

        public void TakeDamage(float damage, PlayerEntity source = null)
        {
            if (!(this.OnTakeDamageIntercept?.Invoke(damage, source) ?? true))
            {
                return;
            }

            if (this.IsImmortal) return;
            if (!this.isAlive) return;

            var finalDamage = this.DameReduction(damage) * (source?.DamageMultiplier ?? 1f);

            if (this.currentInteractiveObstacle is Bush bush) 
            {
                bush.RevealPlayer(this); 
            }

            if (source && source != this)
            {
                source.Score += (int)finalDamage;
            }

            var healthValue = this.Stats.GetStatValue(StatEnum.Health);

            if (healthValue <= 0)
                return;

            healthValue -= finalDamage;
            this.Stats.SetStatValue(StatEnum.Health, healthValue <= 0 ? 0 : healthValue);

            if (healthValue <= 0)
            {
                if (source && source != this)
                {
                    source.KillStreak++;
                    var killStreakBonus = 0;

                    if (source.KillStreak > 1)
                    {
                        killStreakBonus = 20 * ((source.KillStreak - 1) * source.KillStreak / 2);
                    }

                    var scoreForKill = 100 + killStreakBonus;
                    source.Score += scoreForKill;
                    Debug.Log($"Player {source.Id} killed {this.Id}. Kill Streak: {source.KillStreak}. Score Earned: {scoreForKill} (Base: 100, Bonus: {killStreakBonus}). Total Score: {source.Score}");

                    source.Kill++;
                }

                this.Dead++;
                this.OnDead();
                return;
            }

            if (this._shakeScaleTween.IsActive())
            {
                this._shakeScaleTween.Kill();
            }

            this._shakeScaleTween = DOTween.Sequence().Append(this.transform.DOShakeScale(0.1f, 0.1f));

            this.OnTookDamage__Armor(finalDamage);
        }

        public void Heal(float amount)
        {
            if (!this.isAlive) return;

            var healthValue = this.Stats.GetStatValue(StatEnum.Health);

            if (healthValue <= 0)
                return;

            healthValue = Mathf.Clamp(healthValue + amount, 0, 100);

            this.Stats.SetStatValue(StatEnum.Health, healthValue);
        }

        public override void Move(Vector2 direction, bool canLook = true)
        {
            if (this.IsReverseMovement)
            {
                direction = -direction;
            }

            base.Move(direction, canLook);

            this.PlayerState = PlayerState.Move;
        }

        public float GetDamageReduction()
        {
            return this.armors.Where(a => a != null).Sum(item => item.DamageReduction);
        }

        private void SetAlpha(float alpha)
        {
            foreach (var spriteRenderer in this.renderers)
            {
                var color = spriteRenderer.color;
                color.a = alpha;
                spriteRenderer.color = color;
            }
        }

        private void OnRevive()
        {
            Debug.Log($"Player {this.Id} is revived");
            this.Stats.SetStatValue(StatEnum.Health, 100);

            this.SetAlpha(1.0f);
            this.isAlive = true;
            this.RemoveAllEffects();

            this.rb.simulated = true;

            this.ReviveAnim();
            
            foreach (var child in this.GetComponentsInChildren<Collider2D>())
            {
                child.enabled = true;
            }

            Vector2 safeSpawnPoint = MapManager.I.GetSafeSpawnPosition();
            this.transform.position = safeSpawnPoint;

            this.Position = new Vector2(this.transform.position.x - 0.5f, this.transform.position.y - 0.5f);
        }

        public void OnPickUp()
        {
            if (!this.isAlive) return;

            var tempWeapon = WeaponManager.I.Weapons
                .FirstOrDefault(w => w != null && w.Position == this.Position && !w.IsMounted);


            if (tempWeapon)
            {
                if (this.weapons.Any(w => w.GetWeaponType() == tempWeapon.GetWeaponType()))
                {
                    return;
                }

                this.PickUpWeapon(tempWeapon);
                //this.animator.SetBool(HoldWeaponAnimation, true);

                NetworkManager.I.Send(new GamePlayerInventoryAdd.Request
                {
                    playerId = this.Id,
                    item = new()
                    {
                        Id = tempWeapon.Id,
                        Type = tempWeapon.GetWeaponTypeAsString(),
                        Level = 1,
                        Durability = tempWeapon.Capacity,
                    },
                });

                return;
            }

            var tempArmor = ItemManager.I.Armors
                .FirstOrDefault(a => a.Position == this.Position && !a.IsPickup);

            if (tempArmor)
            {
                this.PickUpArmor(tempArmor);

                NetworkManager.I.Send(new GamePlayerInventoryAdd.Request
                {
                    playerId = this.Id,
                    item = new()
                    {
                        Id = tempArmor.Id,
                        Type = tempArmor.GetArmorTypeAsString(),
                        Level = 1,
                        Durability = tempArmor.Durability,
                    },
                });

                return;
            }

            var tempItem = ItemManager.I.Items
                .FirstOrDefault(i => i.Position == this.Position && !i.IsOwned);

            if (tempItem)
            {
                var slotName = this.PickUpItem(tempItem);

                if (slotName != null)
                {
                    NetworkManager.I.Send(new GamePlayerInventoryAdd.Request
                    {
                        playerId = this.Id,
                        item = new()
                        {
                            Id = tempItem.Id,
                            Type = "SUPPORT_ITEM",
                            Level = 1,
                            Durability = 1,
                        },
                    });
                }
            }
        }

        public void OnMeleeAttack()
        {
            var range = this.currentWeapon
                ? this.currentWeapon.Range
                : this.handRange;

            var attackWidth = range.x;
            var attackLength = range.y;

            Vector2 dir;

            switch (this.playerDirection)
            {
                case Direction.Up: dir = Vector2.up; break;
                case Direction.Down: dir = Vector2.down; break;
                case Direction.Left: dir = Vector2.left; break;
                case Direction.Right: dir = Vector2.right; break;
                default: dir = Vector2.up; break;
            }

            var startPosition = (Vector2)this.transform.position + dir * attackLength;
            if(attackLength==3) startPosition = (Vector2)this.transform.position + dir * 2;
            

            Vector2 boxSize;

            if (this.playerDirection is Direction.Left or Direction.Right)
            {
                boxSize = new(attackLength, attackWidth);
            }
            else
            {
                boxSize = new(attackWidth, attackLength);
            }

            this.DebugDrawBox(startPosition, boxSize, 0, Color.magenta, 2f);

            var hits = new List<RaycastHit2D>();

            var contactFilter = new ContactFilter2D
            {
                layerMask = LayerMask.GetMask("Player", "Destructible"),
                useLayerMask = true,
            };

           

            Physics2D.BoxCast(startPosition, boxSize, 0.0f, Vector2.zero, contactFilter, hits, 0f);
            
            Physics2D.SyncTransforms();

            var targets = hits
                .Select(hit => hit.collider.GetComponentInParent<IDamageable>())
                .Where(target => target != null)
                .ToHashSet();

            foreach (var target in targets)
            {
                switch (target)
                {
                    case PlayerEntity when !ReferenceEquals(target, this):
                        var playerDamage = this.currentWeapon ? this.currentWeapon.FinalDamage : this.handDamage;
                        target.TakeDamage(playerDamage, this);
                        break;
                    case DestructibleEntity:
                        var obstacleDamage = this.currentWeapon ? this.currentWeapon.ObstacleDamage : this.playerConfig.obstacleDamage;
                        target.TakeDamage(obstacleDamage, this);
                        break;
                }
            }
        }
        
        private void ClearInventory()
        {
            if (this.currentWeapon)
            {
                this.currentWeapon.Unmount();
                this.UseWeapon(null);

                //this.animator.SetBool(HoldWeaponAnimation, false);
            }

            this.usingItem = null;

            foreach (var weapon in this.weapons)
            {
                NetworkManager.I.Send(new GamePlayerInventoryClear.Request
                {
                    playerId = this.Id,
                    id = weapon.Id,
                    type = weapon.GetWeaponTypeAsString(),
                    
                });
            }

            this.weapons.Clear();

            foreach (var item in this.items)
            {
                NetworkManager.I.Send(new GamePlayerInventoryClear.Request
                {
                    playerId = this.Id,
                    id = item.Id,
                    type = item.Type,
                });
            }

            this.items.Clear();

            foreach (var armor in this.armors)
            {
                NetworkManager.I.Send(new GamePlayerInventoryClear.Request
                {
                    playerId = this.Id,
                    id = armor.Id,
                    type = armor.GetArmorTypeAsString(),
                    
                });
            }

            this.armors.Clear();
        }       
    
        private void OnDead()
        {
            Debug.Log($"Player {this.Id} is dead. ReviveTimeBase = {this.ReviveTimeBase}");

            this.KillStreak = 0;

            this.ReviveTimeCounter = this.ReviveTimeBase;

            this.SetAlpha(0.5f);

            this.RemoveAllEffects();

            this.AddEffect(new StunEffect
            {
                Time = 99999,
            });

            this.isAlive = false;
            this.MovementQueue.Clear();
            this.PlayerAction = null;
            this.ClearInventory();
            this.rb.simulated = false;

            foreach (var child in this.GetComponentsInChildren<Collider2D>())
            {
                child.enabled = false;
            }

            this.DieAnim();

            this.Stats.SetStatValue(StatEnum.Health, 0f);

            foreach (var item in this.Visual.GetComponentsInChildren<BaseItemEntity>())
            {
                item.Drop();
            }

            this.Score = Math.Max(0, this.Score - 100);
        }

        public List<string> GetCanBeSeenBy()
        {
            return PlayerManager.I.Players
                .Where(p => p != this && p.isAlive && this.CanBeSeenBy(p))
                .Select(p => p.Id)
                .ToList();
        }

        private bool CanBeSeenBy(PlayerEntity other)
        {
            return !other.HasEffectOfType<BlindEffect>() && !this.HasEffectOfType<InvisibleEffect>();
        }

        private void DebugDrawBox(Vector2 point, Vector2 size, float angle, Color color, float duration)
        {
            var extents = size / 2;

            var corners = new Vector2[]
            {
                new(-extents.x, -extents.y),
                new(extents.x, -extents.y),
                new(extents.x, extents.y),
                new(-extents.x, extents.y),
            };

            var rotation = Quaternion.Euler(0, 0, angle);

            for (var i = 0; i < 4; i++)
            {
                corners[i] = (Vector2)(rotation * corners[i]) + point;
            }

            Debug.DrawLine(corners[0], corners[1], color, duration);
            Debug.DrawLine(corners[1], corners[2], color, duration);
            Debug.DrawLine(corners[2], corners[3], color, duration);
            Debug.DrawLine(corners[3], corners[0], color, duration);
        }

        public override bool CanMove(Vector2 newPosition)
        {
            MapManager.I.Destructibles.RemoveAll(item => item == null);

            if (MapManager.I.IndestructiblePositions.Any(v => v == newPosition) ||
                PlayerManager.I.Players.Any(p => p.Position == newPosition && p.isAlive) ||
                MapManager.I.Destructibles.Any(v => v.Positions.Count > 0 && v.Positions[0] == newPosition && v is Chest))
            {
                return false;
            }


            Vector2 worldPositionToCheck = newPosition + new Vector2(0.5f, 0.5f);

            Collider2D[] colliders = Physics2D.OverlapPointAll(worldPositionToCheck);

            foreach (var collider in colliders)
            {
                ObstacleBase obstacle = collider.GetComponentInParent<ObstacleBase>();

                if (obstacle != null && !obstacle.Tags.Contains(ObstacleTagEnum.CanGoThrough))
                {
                    return false; 
                }
            }
            
            return true;
        }

        public void SetFinalTime()
        {
            this.isFinalTime = true;
        }
    }


    public enum Direction
    {
        Up,
        Down,
        Left,
        Right,
    }

    public enum PlayerState
    {
        Idle,
        Move,
        Attack,
        Shoot,
    }
}