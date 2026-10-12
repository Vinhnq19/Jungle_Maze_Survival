using _Codebase_.EffectSystem;
using _Codebase_.Entity._Abstract_;
using _Codebase_.Entity.Player;
using _Codebase_.Entity.Projectile;
using _Concrete_.Effects;
using UnityEngine;

namespace _Concrete_.Entities.Projectile
{
    public class BellBulletProjectile : BulletEntity
    {
        public void Explode(float radius, float damageAmount, PlayerEntity sourcePlayer)
        {
            Collider2D[] targetsInRadius = Physics2D.OverlapCircleAll(this.transform.position, radius);

            foreach (var targetCollider in targetsInRadius)
            {
                var targetAsPlayer = targetCollider.GetComponentInParent<PlayerEntity>();

                if (targetAsPlayer != null && ReferenceEquals(targetAsPlayer, sourcePlayer))
                {
                    continue; 
                }

                var damageableTarget = targetCollider.GetComponentInParent<IDamageable>();
                if (damageableTarget != null)
                {
                    damageableTarget.TakeDamage(damageAmount, sourcePlayer);
                }

                var affectableTarget = targetCollider.GetComponentInParent<IAffectable>();
                if (affectableTarget != null)
                {
                    var reverseEffect = new ReverseMovementEffect { Time = this.ReverseTime };
                    affectableTarget.AddEffect(reverseEffect);
                }
            }

            this.SpawnVfxWhenDestroy();
            Destroy(this.gameObject);
        }

        [field: SerializeField] public int ReverseTime { get; private set; }
    }
}