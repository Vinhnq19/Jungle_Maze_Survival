using System;
using _Codebase_.Entity._Abstract_;
using _Codebase_.Entity.Player;
using Managers;
using Managers.DataObjects;
using Misc;
using UnityEngine;

namespace _Codebase_.Entity.Armor
{
    public class ArmorEntity : MonoBehaviour, IHasRarity
    {
        public bool IsPickup => this.MountedPlayer;
        public PlayerEntity MountedPlayer { get; private set; }

        public Vector2 Position
            => this.transform.position - Vector3.one * 0.5f;

        private void OnEnable()
        {
            ItemManager.I.Armors.Add(this);
        }

        private void OnDisable()
        {
            ItemManager.I.Armors.Remove(this);
        }

        public void Mount(Transform mountedTf)
        {
            this.gameObject.SetActive(false);
            // if (this.IsPickup) return;

            // this.MountedPlayer = mountedTf.GetComponentInParent<PlayerEntity>();
            // if (!this.MountedPlayer) return;

            // this.SpriteRenderer.sprite = this.OnPlayerSprite;

            // this.transform.SetParent(mountedTf);
            // this.transform.localPosition = Vector3.zero;
            // this.transform.localRotation = Quaternion.identity;
            // this.transform.localScale = Vector3.one;

            // this.transform.GetComponent<SpriteRenderer>().sortingOrder = 10;
        }

        public void Drop()
        {
            if (!this.IsPickup) return;
            Destroy(this.gameObject);
        }

        public void ReduceDurability(float damage)
        {
            this.Durability -= damage;

            // if (this.Durability <= 0)
            // {
            //     this.Drop();
            // }
        }

        public string GetArmorTypeAsString()
        {
            return this.Type switch
            {
                ArmorType.Body => InventorySlotEnum.ARMOR,
                ArmorType.Head => InventorySlotEnum.HELMET,
                _ => throw new ArgumentOutOfRangeException(),
            };
        }


        [field: Header("Config")]

        #region Config

        [field: SerializeField]
        public string Id { get; private set; }

        [field: SerializeField]
        public ArmorType Type { get; private set; }

        [field: SerializeField]
        public float DamageReduction { get; private set; }

        [field: SerializeField]
        public float Rarity { get; private set; }

        [field: SerializeField]
        public int PickupScore { get; private set; }

        [field: SerializeField]
        public float Durability { get; private set; }

        #endregion

        [field: Header("Visuals")]

        #region Visuals

        [field: SerializeField]
        private SpriteRenderer SpriteRenderer { get; set; }

        [field: SerializeField]
        public Sprite OnGroundSprite { get; set; }

        [field: SerializeField]
        private Sprite OnPlayerSprite { get; set; }

        #endregion

    }
}