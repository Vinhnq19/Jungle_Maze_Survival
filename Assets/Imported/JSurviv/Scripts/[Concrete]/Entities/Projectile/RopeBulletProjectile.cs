using _Codebase_.EffectSystem;
using _Codebase_.Entity._Abstract_;
using _Codebase_.Entity.Player;
using _Codebase_.Entity.Projectile;
using _Concrete_.Effects;
using System.Linq;
using UnityEngine;

namespace _Concrete_.Entities.Projectile
{
    public class RopeBulletProjectile : BulletEntity
    {
        [field: SerializeField]
        public int StunTime { get; private set; }

        public override void OnTriggerEnter2D(Collider2D other)
        {
            if ((PlayerEntity)other.gameObject.GetComponentInParent<IDamageable>() == this.src)
            {
                return;
            }
            var obstacle = other.gameObject.GetComponentInParent<ObstacleBase>();
            if (obstacle != null && obstacle.Tags.Any(tag => tag == ObstacleTagEnum.PullableRope))
            {
                this.src.AddEffect(new PullEffect
                {
                    Direction = (this.transform.position - this.src.transform.position).normalized,
                    Range = 6,
                    StunDuration = this.StunTime,
                    Time = 1,
                });
                return; 
            }
            if (other.gameObject.GetComponentInParent<IDamageable>() is { } damageable)
            {
                damageable.TakeDamage((int)this.damage, this.src);
                this.src.AddEffect(new PullEffect
                {
                    Direction = ((Vector2)other.transform.position - (Vector2)this.src.transform.position).normalized,
                    Range = 6,
                    StunDuration = this.StunTime,
                    Time = 1,
                });
            }

            //if (this.vfxWhenDestroy) Instantiate(this.vfxWhenDestroy, this.transform.position, Quaternion.identity);
            //Destroy(this.gameObject);
        }
    }
}