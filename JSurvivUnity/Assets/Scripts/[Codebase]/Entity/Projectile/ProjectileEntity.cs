using _Codebase_.Entity.Player;
using _Codebase_.ObjectPool;
using Managers;
using UnityEngine;
using Utils;

namespace _Codebase_.Entity.Projectile
{
    public abstract class ProjectileEntity : MonoBehaviour
    {

        [SerializeField]
        protected ParticleSystem vfxWhenDestroy;

        protected float        damage;
        protected float        explosionRadius;
        protected int          range;
        protected float        speed;
        protected PlayerEntity src;

        protected float traveledDistance;
        public float Velocity => this.GetComponent<Rigidbody2D>().linearVelocity.magnitude;
        public Vector2 Position => this.transform.position - Vector3.one * 0.5f;

        public Vector2 Destination { get; set; }

        private void OnEnable()
        {
            if (GameManager.I != null)
            {
                GameManager.I.Projectiles.Add(this);
            }
        }

        private void OnDisable()
        {
            if (GameManager.I != null)
            {
                GameManager.I.Projectiles.Remove(this);
            }
        }

        public PlayerEntity GetSource()
        {
            return this.src;
        }

        public float GetDamage()
        {
            return this.damage;
        }
        public int GetSpeed()
        {
            return (int)this.speed;
        }

        public void Release(int releaseRange, float releaseDamage, float releaseSpeed, float releaseExplosionRadius, PlayerEntity source)
        {
            this.traveledDistance = 0f;
            this.range = releaseRange;
            this.damage = releaseDamage;
            this.speed = releaseSpeed / this.GetStepTime();
            this.src = source;
            this.explosionRadius = releaseExplosionRadius;
            this.GetComponent<Rigidbody2D>().linearVelocity = this.transform.right * this.speed;
        }

        public void DestroySelf()
        {
            if (ObjectPoolManager.I.Free(this.gameObject)) return;

            Destroy(this.gameObject);
        }

        protected bool HasReachedDestination()
        {
            return Vector2.Distance(this.transform.position, this.Destination + Vector2.one * 0.5f) <= 0.1f;
        }
        
        protected void SpawnVfxWhenDestroy()
        {
            if (this.vfxWhenDestroy)
            {
                this.vfxWhenDestroy.SpawnVfx(new()
                {
                    Position = this.transform.position,
                    Scale = Vector3.one * this.explosionRadius,
                });
            }
        }
    }
}