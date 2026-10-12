using System.Collections.Generic;
using _Codebase_.Entity._Abstract_;
using _Codebase_.Entity.Player;
using _Concrete_.Entities.Projectile;
using DG.Tweening;
using UnityEngine;

namespace _Codebase_.Entity.Projectile
{
    public class ThrowableEntity : ProjectileEntity
    {

        [SerializeField]
        public bool canRotate { get; private set; }

        protected readonly HashSet<PlayerEntity> hitPlayers = new();


        public void Start()
        {
            this.ApplyScalingEffect();
        }

        protected virtual void Update()
        {
            this.traveledDistance += this.GetComponent<Rigidbody2D>().linearVelocity.magnitude * Time.deltaTime;

            if (this.canRotate)
            {
                this.transform.Rotate(Vector3.forward * 360 * Time.deltaTime);
            }

            if (this.traveledDistance >= this.range || this.HasReachedDestination())
            {
                this.SpawnVfxWhenDestroy();
                this.Explode();
                this.DestroySelf();
            }
        }


        //private void OnDrawGizmosSelected()
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(this.transform.position, this.explosionRadius);
        }

        protected virtual void Explode()
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
                        if (nearbyPlayer == this.src || this.hitPlayers.Contains(nearbyPlayer))
                        {
                            continue;
                        }

                        this.hitPlayers.Add(nearbyPlayer);
                    }

                    // Apply damage
                    damageable.TakeDamage((int)this.damage, this.src);
                }
            }

            this.SpawnVfxWhenDestroy();
            Destroy(this.gameObject);
        }

        private void ApplyScalingEffect()
        {
            // Save the original scale
            var originalScale = this.transform.localScale;

            // Create the scaling sequence
            var scaleSequence = DOTween.Sequence();
            var halfwayDuration = this.range / (2 * this.speed);

            scaleSequence.Append(this.transform.DOScale(originalScale * 3f, halfwayDuration))
                .Append(this.transform.DOScale(originalScale, halfwayDuration))
                .SetEase(Ease.InOutSine); // Smooth easing for better effect
        }
    }
}