using _Codebase_.Entity.Armor;
using _Codebase_.Entity.Destructible;
using _Codebase_.Entity.Item;
using _Codebase_.Entity.Weapon;
using _Codebase_.Utils;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _Concrete_.Entities.Destructible
{
    public class Chest : DestructibleEntity
    {
        [SerializeField]
        private float oddOfRanged;

        [SerializeField]
        private BaseWeaponEntity[] ranged;

        [SerializeField]
        private float oddOfMelee;

        [SerializeField]
        private BaseWeaponEntity[] melee;

        [SerializeField]
        private float oddOfItem;

        [SerializeField]
        private BaseItemEntity[] items;

        [SerializeField]
        private float oddOfArmor;

        [SerializeField]
        private ArmorEntity[] armor;

        [SerializeField]
        private float oddOfThrowable;

        [SerializeField]
        private BaseWeaponEntity[] throwable;
        
        [SerializeField]
        private float oddOfSpecial;

        [SerializeField]
        private BaseWeaponEntity[] special;

        public override void OnBroken()
        {
            foreach (var pos in this.Positions)
            {
                Instantiate(this.GetRandomObject(), pos + new Vector2(0.5f, 0.5f), Quaternion.identity);
            }
        }

        private GameObject GetRandomObject()
        {
            var totalRarity = this.oddOfRanged + this.oddOfMelee + this.oddOfItem + this.oddOfArmor + this.oddOfThrowable + this.oddOfSpecial ;
            var random = Random.Range(0, totalRarity);

            if (random < this.oddOfRanged)
            {
                return this.ranged.RandomWithRarity().gameObject;
            }

            if (random < this.oddOfRanged + this.oddOfMelee)
            {
                return this.melee.RandomWithRarity().gameObject;
            }

            if (random < this.oddOfRanged + this.oddOfMelee + this.oddOfItem)
            {
                return this.items.RandomWithRarity().gameObject;
            }

            if (random < this.oddOfRanged + this.oddOfMelee + this.oddOfItem + this.oddOfArmor)
            {
                return this.armor.RandomWithRarity().gameObject;
            }
            if (random < this.oddOfRanged + this.oddOfMelee + this.oddOfItem + this.oddOfArmor + this.oddOfThrowable)
            {
                return this.throwable.RandomWithRarity().gameObject;
            }
            
            return this.special.RandomWithRarity().gameObject;
        }
    }
}