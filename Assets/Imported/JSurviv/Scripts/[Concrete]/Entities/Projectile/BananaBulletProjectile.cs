using _Codebase_.EffectSystem;
using _Codebase_.Entity._Abstract_;
using _Codebase_.Entity.Player;
using _Codebase_.Entity.Projectile;
using _Concrete_.Effects;
using UnityEngine;

namespace _Concrete_.Entities.Projectile
{
    public class BananaBulletProjectile : ThrowableEntity
    {
        protected override void Update()
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
                // spawn banana peel trap
                Instantiate(this.bananaPeel, this.transform.position, Quaternion.identity);

                this.DestroySelf();
            }
        }
        [field: SerializeField]
        public GameObject bananaPeel { get; private set; }
    }
}

    

