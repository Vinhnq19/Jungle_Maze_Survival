using _Codebase_.EffectSystem;
using _Codebase_.Entity._Abstract_;
using _Codebase_.Entity.Player;
using _Codebase_.Entity.Projectile;
using _Concrete_.Effects;
using UnityEngine;

namespace _Concrete_.Entities.Projectile
{
    public class SeedBulletProjectile : ThrowableEntity
    {
        protected override void Explode()
        {
            var colliders = Physics2D.OverlapCircleAll(this.transform.position, this.explosionRadius);

            foreach (var nearbyObject in colliders)
            {
                var damageable = nearbyObject.gameObject.GetComponentInParent<IDamageable>();

                if (damageable != null)
                {
                    // Check if the object is a player and has already been hit
                    if (damageable is PlayerEntity nearbyPlayer)
                    {
                        if (nearbyPlayer == this.src)
                        {
                            continue;
                        }

                        this.hitPlayers.Add(nearbyPlayer);
                    }

                    // Apply damage
                    damageable.TakeDamage((int)this.damage, this.src);

                }
                var effectable = nearbyObject.gameObject.GetComponentInParent<IAffectable>();

                if (effectable != null)
                {
                    // Check if the object is a player and has already been hit
                    if (effectable is PlayerEntity nearbyPlayer)
                    {
                        if (nearbyPlayer == this.src)
                        {
                            continue;
                        }

                        this.hitPlayers.Add(nearbyPlayer);
                    }


                    // add effect
                    var stunEffect = new StunEffect
                    {
                        Time = this.StunTime
                    };

                    effectable.AddEffect(stunEffect);

                }
            }

            this.SpawnVfxWhenDestroy();
            Destroy(this.gameObject);
        }
        [field: SerializeField]
        public int StunTime { get; private set; }
    }
}