using System;
// using System.Numerics;
using DG.Tweening;
using NUnit.Framework.Constraints;
using UnityEngine;

namespace _Codebase_.Entity.Player
{
    public partial class PlayerEntity
    {
        public void SetBodyVisual(Sprite sprite)
        {
            this.bodySprite = sprite;
        }

        public void SetHandVisual(Sprite sprite)
        {
            this.handSprite = sprite;
        }

        public Sprite GetBodyVisual()
        {
            return this.bodySprite;
        }

        public Sprite GetHandVisual()
        {
            return this.handSprite;
        }

        public override void Look(Vector2 direction, float duration)
        {
            float x = direction.x, y = direction.y;

            this.playerDirection = (x, y) switch
            {
                (0, -1) => Direction.Down,
                (0, 1) => Direction.Up,
                (1, 0) => Direction.Right,
                (-1, 0) => Direction.Left,
                _ => throw new ArgumentOutOfRangeException(),
            };

            this.shootTf.localPosition = this.playerDirection switch
            {
                Direction.Down => new Vector3(0f, -1f, 0f),
                Direction.Up => new Vector3(0f, 1f, 0f),
                Direction.Left => new Vector3(-1f, 0f, 0f),
                Direction.Right => new Vector3(1f, 0f, 0f),
                _ => Vector3.zero
            };
            //this.Visual.DORotate(new(0, 0, Mathf.Approximately(y, 1) ? 180 : 0 + x * 90), duration);
            float angle = this.playerDirection == Direction.Left || this.playerDirection == Direction.Up ? 180f : 0f;
            this.Visual.rotation = UnityEngine.Quaternion.Euler(0, angle, 0);

            if (this.currentWeapon)
            {
                if (this.currentWeapon.GetWeaponType() == Weapon.WeaponType.Ranged)
                {
                    float weaponAngle = this.playerDirection switch
                    {
                        Direction.Up => 90f,
                        Direction.Down => -90f,
                        Direction.Left => 0f,
                        Direction.Right => 0f,
                        _ => 0f
                    };
                    this.currentWeapon.transform.localRotation = Quaternion.Euler(
                        this.currentWeapon.transform.localRotation.x,
                        this.currentWeapon.transform.localRotation.y, weaponAngle
                        );
                }
                else if (this.currentWeapon.GetWeaponType() == Weapon.WeaponType.Melee)
                {
                    int layer = this.playerDirection switch
                    {
                        Direction.Up => 3,
                        Direction.Down => 1,
                        Direction.Left => 2,
                        Direction.Right => 1,
                        _ => 2,
                    };

                    this.currentWeapon.GetComponentInChildren<SpriteRenderer>().sortingOrder = layer;
                }

            }

        }


        public static string FormatName(string name, int length)
        {
            if (name.Length > length)
            {
                return name[..length] + "...";
            }

            return name;
        }
    }
}