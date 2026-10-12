using _Codebase_.Entity.Projectile;
using _Codebase_.ObjectPool;
using _Codebase_.Utils;
using UnityEngine;

namespace _Codebase_.Entity.Weapon
{
    public class BasicThrowableWeaponEntity : BaseWeaponEntity
    {

        protected override void OnAttack()
        {
            var shootDirection = this.MountedPlayer.playerDirection.ToVector2();

            var projectile = ObjectPoolManager.I[this.ProjectilePrefab.gameObject]
                .Take<ProjectileEntity>()
                .SetTransformValues(new()
                {
                    Position = this.MountedPlayer.Position + shootDirection + Vector2.one * 0.5f,
                    Rotation = Quaternion.LookRotation(Vector3.forward, shootDirection) * Quaternion.Euler(0, 0, 90f),
                });

            projectile.Destination = this.MountedPlayer.Position + shootDirection * this.Range.y;

            this.MountedPlayer.ThrowableAttackAnim();

            projectile.Release(
                (int)this.Range.y,
                this.FinalDamage,
                this.Speed,
                this.ExplosionRadius,
                this.MountedPlayer
            );
        }

        public override WeaponType GetWeaponType()
        {
            return WeaponType.Throwable;
        }

        [field: Header("Extras")]

        #region Config

        [field: SerializeField]
        public ProjectileEntity ProjectilePrefab { get; private set; }

        [field: SerializeField]
        public int ExplosionRadius { get; private set; }

        #endregion

    }
}