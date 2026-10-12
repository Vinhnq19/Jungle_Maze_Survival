using System.Linq;
using _Codebase_.Entity._Abstract_;
using _Codebase_.Entity.Enemy;
using _Codebase_.Entity.Player;
using UnityEngine;

namespace _Codebase_.Entity.Projectile
{

    public class BulletEntity : ProjectileEntity
    {
        private const string WEAPON_TAG = "Weapon";
        private const string PROJECTILE_TAG = "Projectile";
        private const string ITEM_TAG = "Item";

        private Rigidbody2D rb;
        private void Start()
        {
            rb = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            this.traveledDistance += this.rb.linearVelocity.magnitude * Time.deltaTime;

            if (this.HasReachedDestination() || this.traveledDistance >= this.range)
            {
                this.SpawnVfxWhenDestroy();

                this.DestroySelf();
            }
        }


        public virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject.GetComponentInParent<PlayerEntity>() == this.src)
            {
                return;
            }

            if (other.gameObject.GetComponentInParent<IDamageable>() is { } damageable)
            {
                damageable.TakeDamage((int)this.damage, this.src);
            }

            if (!ShouldPassThrough(other))
            {
                this.SpawnVfxWhenDestroy();
                this.DestroySelf();
            }
    
        }
        
        private bool ShouldPassThrough(Collider2D other)
        {
            if (other.CompareTag("Weapon") || other.CompareTag("Projectile") || other.CompareTag("Item") || other.CompareTag("Border"))
            {
                return true;
            }

            var obstacle = other.GetComponentInParent<ObstacleBase>();
            if (obstacle != null && obstacle.Tags.Contains(ObstacleTagEnum.CanShootThrough))
            {
                return true;
            }

            if (other.GetComponentInParent<EnemyEntity>() != null)
            {
                return true;
            }

            return false;
        }
        
    }
}