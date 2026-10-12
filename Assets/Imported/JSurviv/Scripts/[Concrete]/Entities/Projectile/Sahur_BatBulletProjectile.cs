using _Codebase_.EffectSystem;
using _Codebase_.Entity._Abstract_;
using _Codebase_.Entity.Projectile;
using _Codebase_.Entity.Player;
using _Concrete_.Effects;
using UnityEngine;

namespace _Concrete_.Entities.Projectile
{
    public class Sahur_BatBulletProjectile : BulletEntity
    {
        [field: SerializeField]
        public int StunTime { get; private set; }

        [field: SerializeField]
        public int knockBackPower { get; private set; }

        public override void OnTriggerEnter2D(Collider2D other)
        {
            var affectable = other.gameObject.GetComponentInParent<IAffectable>();

            if (affectable != null && ReferenceEquals(affectable, this.src))
            {
                return;
            }

            if (affectable is IDamageable damageable)
            {
                damageable.TakeDamage((int)this.damage, this.src);
            }

            if (affectable != null)
            {
                Vector2 knockbackDirection;
                switch (this.src.playerDirection) // Sử dụng playerDirection
                {
                    case Direction.Up:
                        knockbackDirection = Vector2.up;
                        break;
                    case Direction.Down:
                        knockbackDirection = Vector2.down;
                        break;
                    case Direction.Left:
                        knockbackDirection = Vector2.left;
                        break;
                    case Direction.Right:
                        knockbackDirection = Vector2.right;
                        break;
                    default:
                        knockbackDirection = Vector2.zero; 
                        break;
                }

                var knockBackEffect = new KnockBackEffect
                {
                    Direction = knockbackDirection, 
                    KnockbackPower = this.knockBackPower,
                    StunOnBlockedDuration = this.StunTime,
                    Time = 1,
                };
                affectable.AddEffect(knockBackEffect);
            }

            Destroy(this.gameObject);
        }
    }
}