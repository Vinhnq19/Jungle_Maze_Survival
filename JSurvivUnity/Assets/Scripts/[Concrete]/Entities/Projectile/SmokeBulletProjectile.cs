using _Codebase_.EffectSystem;
using _Codebase_.Entity._Abstract_;
using _Codebase_.Entity.Player;
using _Codebase_.Entity.Projectile;
using _Concrete_.Effects;
using UnityEngine;

namespace _Concrete_.Entities.Projectile
{
    public class SmokeBulletProjectile : ThrowableEntity
    {
        [field: SerializeField]
        public int BlindTime { get; private set; }

        protected override void Explode()
        {
            var colliders = Physics2D.OverlapCircleAll(this.transform.position, this.explosionRadius);

            foreach (var nearbyObject in colliders)
            {
                var damageable = nearbyObject.gameObject.GetComponentInParent<IDamageable>();

                if (damageable != null)
                {
                    // Check if the object is a player and has already been hit
                    // if (damageable is PlayerEntity nearbyPlayer)
                    // {
                    //     if (nearbyPlayer == this.src)
                    //     {
                    //         continue;
                    //     }
                    // }

                    // Apply damage
                    damageable.TakeDamage((int)this.damage, this.src);
                }

                var affectable = nearbyObject.gameObject.GetComponentInParent<IAffectable>();

                if (affectable != null)
                {
                    // Check if the object is a player and has already been hit
                    // if (affectable is PlayerEntity nearbyPlayer)
                    // {
                    //     if (nearbyPlayer == this.src)
                    //     {
                    //         continue;
                    //     }
                    // }

                    // add effect
                    var blindEffect = new BlindEffect
                    {
                        Time = this.BlindTime,
                    };

                    affectable.AddEffect(blindEffect);
                    
                    var invisibleEffect = new InvisibleEffect
                    {
                        Time = this.BlindTime,
                    };
                    
                    affectable.AddEffect(invisibleEffect);
                }
            }

            this.SpawnVfxWhenDestroy();

                Destroy(this.gameObject);
        }
    }
}