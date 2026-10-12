using System.Linq;
using _Codebase_.Entity._Abstract_;
using _Codebase_.Entity.Armor;
using Utils;
using System.Collections.Generic;
using Managers;
using Managers.DataObjects;
using UnityEngine;

namespace _Codebase_.Entity.Player
{
    public partial class PlayerEntity
    {
        public void PickUpArmor(ArmorEntity pickupArmor)
        {
            if (this.armors.Count >= LIMIT_ARMOR_COUNT) return;

            if (this.armors.Any(armor => pickupArmor.Type == armor.Type))
            {
                return;
            }

            this.armors.Add(pickupArmor);
            pickupArmor.Mount(this.Visual);

            this.Score += pickupArmor.PickupScore;
        }

        private float DameReduction(float damage)
        {
            return damage * (1 - this.Stats.GetStatValue(StatEnum.Armor) / 100);
        }

        private void OnTookDamage__Armor(float finalDamage)
        {
            var armorToTakeDamage = this.armors.FirstOrDefault();
            if (armorToTakeDamage == null) return;

            armorToTakeDamage.ReduceDurability(finalDamage);

            if (armorToTakeDamage.Durability <= 0)
            {
                Debug.Log($"Armor {armorToTakeDamage.Id} of player {this.Id} broke.");

                NetworkManager.I.Send(new GamePlayerInventoryClear.Request
                {
                    playerId = this.Id,
                    id = armorToTakeDamage.Id,
                    type = armorToTakeDamage.GetArmorTypeAsString(),
                });
        
                this.armors.Remove(armorToTakeDamage);

                armorToTakeDamage.Drop();
            }
        }
        
        public List<ArmorEntity> GetArmors() => this.armors;
    }
}