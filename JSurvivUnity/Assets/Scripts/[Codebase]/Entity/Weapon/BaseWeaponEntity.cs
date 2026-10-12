using System;
using System.Collections.Generic;
using System.Linq;
using _Codebase_.Entity._Abstract_;
using _Codebase_.Entity.Player;
using DG.Tweening;
using Managers;
using Managers.DataObjects;
using UnityEngine;

namespace _Codebase_.Entity.Weapon
{
    public enum WeaponType
    {
        Melee,
        Ranged,
        Throwable,
        Special,
    }

    public enum WeaponHoldStyle
    {
        Melee,
        Ranged,
        Throwable,
    }

    public class WeaponBuff
    {
        public float BonusDamage { get; set; }
        public float Duration { get; set; }
    }

    public abstract class BaseWeaponEntity : MonoBehaviour, IHasRarity
    {
        public int CurrentAmount { get; private set; }

        public bool IsMounted => this.MountedPlayer;
        public PlayerEntity MountedPlayer { get; private set; }

        public Vector2 Position
            => this.transform.position - Vector3.one * 0.5f;

        public List<WeaponBuff> Buffs { get; } = new();

        public float FinalDamage => this.Buffs.Aggregate(this.Damage, (dmg, buff) => dmg * (1 + buff.BonusDamage));

        private void Start()
        {
            this.CurrentAmount = this.Capacity;
        }
        private void Update()
        {


            if (this.IsMounted)
            {
                // this.transform.localPosition = Vector3.zero;
                //this.transform.localEulerAngles = Vector3.back * 90f;

                foreach (var weaponBuff in this.Buffs)
                {
                    weaponBuff.Duration -= Time.deltaTime;
                }

                this.Buffs.RemoveAll(buff => buff.Duration < 0);
            }
        }

        private void OnEnable()
        {
            WeaponManager.I.Weapons.Add(this);
        }

        private void OnDisable()
        {
            WeaponManager.I.Weapons.Remove(this);
        }

        public string GetWeaponTypeAsString()
        {
            return this.GetWeaponType() switch
            {
                WeaponType.Melee => InventorySlotEnum.MELEE,
                WeaponType.Ranged => InventorySlotEnum.RANGED,
                WeaponType.Throwable => InventorySlotEnum.THROWN,
                WeaponType.Special => InventorySlotEnum.SPECIAL,
                _ => throw new ArgumentOutOfRangeException(),
            };
        }

        public abstract WeaponType GetWeaponType();

        public virtual void Mount(Transform mountedTf)
        {
            if (this.IsMounted) return;

            this.MountedPlayer = mountedTf.GetComponentInParent<PlayerEntity>();
            if (!this.MountedPlayer) return;

            this.SpriteRenderer.sprite = this.OnHandSprite;

            this.transform.SetParent(mountedTf);
            // this.transform.position = new Vector3(1000f, 1000f, 0f);
            this.transform.localEulerAngles = Vector3.zero;
            this.transform.localPosition = Vector3.zero;

            var visualizer = this.transform.GetComponent<HandVisualizer>();

            if (visualizer)
            {
                visualizer.OnPickUp();
            }

            var sr = this.transform.GetComponentInChildren<SpriteRenderer>();

            if (sr)
            {
                sr.sortingLayerName = "Default";
                sr.sortingOrder = 2;
                if(this.GetWeaponType() == Weapon.WeaponType.Ranged)
                {
                    float weaponAngle = this.MountedPlayer.playerDirection switch
                    {
                        Direction.Up => 90f,
                        Direction.Down => -90f,
                        Direction.Left => 0f,
                        Direction.Right => 0f,
                        _ => 0f
                    };
                    this.transform.localRotation = Quaternion.Euler(
                        this.transform.localRotation.x,
                        this.transform.localRotation.y, weaponAngle
                        );
                }
                else if (this.GetWeaponType() == Weapon.WeaponType.Melee)
                {
                    int layer = this.MountedPlayer.playerDirection switch
                    {
                        Direction.Up => 3,
                        Direction.Down => 1,
                        Direction.Left => 2,
                        Direction.Right => 1,
                        _ => 2,
                    };

                    sr.sortingOrder = layer;
                }
            }

        }

        public virtual void Unmount()
        {
            if (!this.IsMounted) return;
            this.SpriteRenderer.sprite = this.OnGroundSprite;
            Destroy(this.gameObject);
        }

        protected abstract void OnAttack();

        public void Attack()
        {
            if (!this.MountedPlayer) return;
            if (this.CurrentAmount <= 0) return;

            this.OnAttack();

            this.CurrentAmount--;

            if (this.CurrentAmount <= 0)
            {
                this.MountedPlayer.DropWeapon();
            }
        }

        public void Show()
        {
            this.gameObject.SetActive(true);
        }

        public void Hide()
        {
            this.gameObject.SetActive(false);
        }

        [field: Header("Config")]

        #region Config

        [field: SerializeField]
        public string Id { get; private set; }

        [field: SerializeField]
        public WeaponHoldStyle HoldStyle { get; set; }

        [field: SerializeField]
        public float Rarity { get; private set; }

        [field: SerializeField]
        public int PickupScore { get; private set; }

        [field: SerializeField]
        public float HitPoint { get; private set; }

        [field: SerializeField]
        public float Damage { get; private set; }

        [field: SerializeField]
        public float ObstacleDamage { get; private set; }

        [field: SerializeField]
        public float Cooldown { get; private set; }

        [field: SerializeField]
        public int Capacity { get; private set; }

        [field: SerializeField]
        public Vector2 Range { get; private set; }

        [field: SerializeField]
        public float Speed { get; private set; }

        #endregion

        [field: Header("Visuals")]

        #region Visuals

        [field: SerializeField]
        private SpriteRenderer SpriteRenderer { get; set; }

        [field: SerializeField]
        public Sprite OnGroundSprite { get; set; }

        [field: SerializeField]
        private Sprite OnHandSprite { get; set; }

        #endregion

    }
}