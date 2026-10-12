using System;
using System.Collections.Generic;
using System.Linq;
using _Codebase_.Entity.Weapon;
using _Codebase_.Utils;
using _Concrete_.Entities.CanGoThrough;
using Cysharp.Threading.Tasks;
using Managers;
using Managers.DataObjects;
using UnityEngine;
using Utils;

namespace _Codebase_.Entity.Player
{
    public partial class PlayerEntity
    {
        // bui cay
        public ObstacleBase currentInteractiveObstacle;
        public Action<PlayerEntity> OnPlayerAttack { get; set; }

        public void DropWeapon()
        {
            if (!this.isAlive || this.currentWeapon == null) return;

            BaseWeaponEntity weaponToDrop = this.currentWeapon;

            NetworkManager.I.Send(new GamePlayerInventoryClear.Request
            {
                playerId = this.Id,
                id = weaponToDrop.Id,
                type = weaponToDrop.GetWeaponTypeAsString(),
            });

            weaponToDrop.Unmount();
            this.weapons.Remove(weaponToDrop);

            var meleeWeapon = this.GetWeaponOfType(WeaponType.Melee);

            if (meleeWeapon != null)
            {
                this.UseWeapon(meleeWeapon);
            }
            else
            {
                this.UseWeapon(null);
            }
        }

        // public void DropAllWeapons()
        // {
        //     if (!this.isAlive) return;

        //     this.weapons.ForEach(weapon =>
        //     {
        //         GameManager.I.Event.Invoke(SocketIOManager.Events.UPDATE_PLAYER_INVENTORY, new SocketIOManager.UpdatePlayerInventorySchema
        //         {
        //             action = "REMOVE",
        //             id = weapon.Id,
        //             item_type = weapon.GetWeaponTypeAsString(),
        //             player_name = this.Name,
        //         });

        //         weapon.Unmount();
        //     });

        //     this.weapons.Clear();
        //     this.UseWeapon(null);
        // }

        public void PickUpWeapon(BaseWeaponEntity pickupWeapon)
        {
            if (this.weapons.Count >= LIMIT_WEAPON_COUNT) return;

            // If this kind of weapon already exists in inventory
            // Then ignore it
            if (this.weapons.Any(weapon => pickupWeapon.GetWeaponType() == weapon.GetWeaponType()))
            {
                return;
            }

            // set all weapons as inactive
            this.weapons.ForEach(weapon => weapon.Hide());

            this.weapons.Add(pickupWeapon);
            pickupWeapon.Mount(this.holdPoint);

            this.UseWeapon(pickupWeapon);
            this.Score += pickupWeapon.PickupScore;
        }

        public BaseWeaponEntity GetCurrentWeapon()
        {
            return this.currentWeapon;
        }

        public List<BaseWeaponEntity> GetWeapons() => this.weapons;

        public void UseWeapon(BaseWeaponEntity weapon)
        {
            if (this.isCantUseWeapon) return;

            this.OnPlayerAttack?.Invoke(this);

            if (this.currentWeapon) this.currentWeapon.Hide();

            this.ForceSetField(nameof(this.currentWeapon), weapon);
            this.cooldownCounter = 0;

            if (this.currentWeapon) this.currentWeapon.Show();


            //this.animator.SetBool(HoldWeaponAnimation, weapon);

            SetWeaponHoldStyle(
                weapon
                    ? new Dictionary<WeaponHoldStyle, int>
                    {
                        [WeaponHoldStyle.Ranged] = HoldGunAnimation,
                        [WeaponHoldStyle.Melee] = HoldMeleeAnimation,
                        [WeaponHoldStyle.Throwable] = HoldThrowableAnimation,
                    }[weapon.HoldStyle]
                    : null
            );

            return;

            void SetWeaponHoldStyle(int? selectedHashKey)
            {
                // new[]
                // {
                //     HoldMeleeAnimation, HoldGunAnimation, HoldThrowableAnimation,
                // }.ForEach(key => this.animator.SetBool(key, key == selectedHashKey));
            }
        }

        public void ResetCooldown()
        {
            var cdSteps = this.currentWeapon
                ? this.currentWeapon.Cooldown
                : this.playerConfig.attackCooldown;

            this.cooldown = cdSteps * this.GetStepTime();
            this.cooldownCounter = this.cooldown;
        }

        public void Attack(Vector2 direction)
        {
            if (this.isCantUseWeapon) return;
            this.OnPlayerAttack?.Invoke(this);
            if (!this.isAlive) return;
            this.Look(direction, DEFAULT_LOOK_DURATION);

            UniTask
                .Delay(250)
                .ContinueWith(() =>
                {
                    if (this.currentWeapon == null)
                    {
                        if (this.IsCoolingDown) return;
                        this.ResetCooldown();
                        this.OnMeleeAttack();
                        this.RunMeleeAttackAnim();
                        return;
                    }

                    // Case 2: Đang cầm vũ khí Melee
                    if (this.currentWeapon.GetWeaponType() == WeaponType.Melee)
                    {
                        if (this.IsCoolingDown) return;
                        this.ResetCooldown();
                        this.currentWeapon.Attack();
                        this.RunMeleeAttackAnim();
                    }
                    else
                    {
                        var meleeWeapon = this.GetWeaponOfType(WeaponType.Melee);
                        if (meleeWeapon != null)
                        {
                            this.UseWeapon(meleeWeapon);
                            if (this.IsCoolingDown) return;
                            this.ResetCooldown();
                            this.currentWeapon.Attack();
                            this.RunMeleeAttackAnim();
                        }
                        else
                        {
                            this.UseWeapon(null);
                            if (this.IsCoolingDown) return;
                            this.ResetCooldown();
                            this.OnMeleeAttack();
                            this.RunMeleeAttackAnim();
                        }
                    }
                })
                .Forget();
        }

        public void Shoot(Vector2 direction)
        {
            if (this.isCantUseWeapon) return;
            this.OnPlayerAttack?.Invoke(this);
            if (!this.isAlive) return;
            this.Look(direction, DEFAULT_LOOK_DURATION);

            UniTask
                .Delay(250)
                .ContinueWith(() =>
                {
                    if (this.currentInteractiveObstacle is Bush bush)
                    {
                        bush.RevealPlayer(this);
                    }

                    if (this.currentWeapon.IsActiveAndType(WeaponType.Ranged))
                    {
                        if (this.IsCoolingDown) return;

                        this.ResetCooldown();

                        this.currentWeapon.Attack();
                    }
                    else
                    {
                        var rangedWeapon = this.GetWeaponOfType(WeaponType.Ranged);

                        if (rangedWeapon)
                        {
                            this.UseWeapon(rangedWeapon);

                            this.currentWeapon.Attack();
                        }
                    }
                })
                .Forget();
        }

        public void Throw(Vector2 direction)
        {
            if (this.isCantUseWeapon) return;
            if (!this.isAlive) return;
            this.Look(direction, DEFAULT_LOOK_DURATION);

            UniTask
                .Delay(250)
                .ContinueWith(() =>
                {
                    if (this.currentWeapon.IsActiveAndType(WeaponType.Throwable))
                    {
                        if (this.IsCoolingDown) return;

                        this.ResetCooldown();

                        this.currentWeapon.Attack();
                    }
                    else
                    {
                        // weapon tag is not throwable
                        var throwableWeapon = this.GetWeaponOfType(WeaponType.Throwable);

                        if (throwableWeapon)
                        {
                            this.UseWeapon(throwableWeapon);
                            this.currentWeapon.Attack();
                        }
                    }
                })
                .Forget();
        }

        public void UseSpecial(Vector2 direction)
        {
            if (this.isCantUseWeapon) return;
            this.OnPlayerAttack?.Invoke(this);
            if (!this.isAlive) return;
            this.Look(direction, DEFAULT_LOOK_DURATION);

            UniTask
                .Delay(250)
                .ContinueWith(() =>
                {
                    if (this.currentInteractiveObstacle is Bush bush)
                    {
                        bush.RevealPlayer(this);
                    }

                    if (this.currentWeapon.IsActiveAndType(WeaponType.Special))
                    {
                        if (this.IsCoolingDown) return;
                        this.ResetCooldown();
                        this.currentWeapon.Attack();
                    }
                    else
                    {
                        var specialWeapon = this.GetWeaponOfType(WeaponType.Special);
                        if (specialWeapon)
                        {
                            this.UseWeapon(specialWeapon);
                            if (this.IsCoolingDown) return;
                            this.ResetCooldown();
                            this.currentWeapon.Attack();
                        }
                    }
                })
                .Forget();
        }

        public BaseWeaponEntity GetWeaponOfType(WeaponType type)
        {
            return this.weapons.FirstOrDefault(weapon => weapon.GetWeaponType() == type);
        }
    }
}