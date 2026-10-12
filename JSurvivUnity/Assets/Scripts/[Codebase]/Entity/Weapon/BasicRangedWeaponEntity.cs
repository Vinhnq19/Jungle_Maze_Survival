using _Codebase_.Entity.Projectile;
using _Codebase_.ObjectPool;
using _Codebase_.Utils;
using UnityEngine;

namespace _Codebase_.Entity.Weapon
{
    public class BasicRangedWeaponEntity : BaseWeaponEntity
    {

        protected override void OnAttack()
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
                    //Rotation = Quaternion.AngleAxis(-90, Vector3.forward) * this.MountedPlayer.Visual.rotation,
                    Rotation = Quaternion.LookRotation(Vector3.forward, shootDirectionVector) * Quaternion.Euler(0, 0, 90f),
                });

            //projectile.Destination = this.MountedPlayer.Position + shootDirection * this.Range;
            projectile.Destination = (Vector2)this.MountedPlayer.Position + shootDirectionVector * this.Range.y;

            this.MountedPlayer.RangedAttackAnim();

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
            return WeaponType.Ranged;
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