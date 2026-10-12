using _Codebase_.Entity._Abstract_;
using _Codebase_.Entity.Projectile;
using _Codebase_.ObjectPool;
using _Codebase_.Utils;
using _Concrete_.Entities.Projectile;
using UnityEngine;

namespace _Codebase_.Entity.Weapon
{
    public class BaseSpecialWeaponEntity : BaseWeaponEntity
    {
        [Header("Special Weapon Config")] [SerializeField]
        public ProjectileEntity ProjectilePrefab;

        [SerializeField] public int ExplosionRadius;

        public override WeaponType GetWeaponType()
        {
            return WeaponType.Special;
        }

        protected override void OnAttack()
        {
            if (this.ExplosionRadius > 0)
            {
                var projectile = ObjectPoolManager.I[this.ProjectilePrefab.gameObject]
                    .Take<ProjectileEntity>()
                    .SetTransformValues(new()
                    {
                        Position = this.MountedPlayer.transform.position,
                        Rotation = Quaternion.identity,
                    });

                if (projectile is BellBulletProjectile bellBullet)
                {
                    bellBullet.Explode(this.ExplosionRadius, this.FinalDamage, this.MountedPlayer);
                }
                else
                {
                    Destroy(projectile.gameObject);
                }
            }

            else
            {
                Vector2 shootDirectionVector;
                switch (this.MountedPlayer.playerDirection)
                {
                    case Player.Direction.Up:
                        shootDirectionVector = Vector2.up;
                        break;
                    case Player.Direction.Down:
                        shootDirectionVector = Vector2.down;
                        break;
                    case Player.Direction.Left:
                        shootDirectionVector = Vector2.left;
                        break;
                    case Player.Direction.Right:
                        shootDirectionVector = Vector2.right;
                        break;
                    default:
                        shootDirectionVector = Vector2.up;
                        break;
                }

                var projectile = ObjectPoolManager.I[this.ProjectilePrefab.gameObject]
                    .Take<ProjectileEntity>()
                    .SetTransformValues(new()
                    {
                        Position = (Vector2)this.MountedPlayer.shootTf.position,
                        Rotation = Quaternion.LookRotation(Vector3.forward, shootDirectionVector) *
                                   Quaternion.Euler(0, 0, 90f),
                    });

                projectile.Release(
                    (int)this.Range.y,
                    this.FinalDamage,
                    this.Speed,
                    this.ExplosionRadius,
                    this.MountedPlayer
                );
            }

            this.MountedPlayer.RunMeleeAttackAnim();
        }
    }
}