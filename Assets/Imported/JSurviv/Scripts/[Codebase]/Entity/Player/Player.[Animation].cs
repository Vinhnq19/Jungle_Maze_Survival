using _Codebase_.Entity.Armor;
using _Codebase_.Entity.Weapon;
using _Codebase_.Utils;
using DG.Tweening;
using UnityEngine;

namespace _Codebase_.Entity.Player
{
    public partial class PlayerEntity
    {
        private const int LIMIT_ARMOR_COUNT  = 2;
        private const int LIMIT_ITEM_COUNT   = 4;
        private const int LIMIT_WEAPON_COUNT = 4;

        private static readonly int HoldWeaponAnimation      = Animator.StringToHash("HoldWeapon");
        private static readonly int HoldGunAnimation         = Animator.StringToHash("HoldGun");
        private static readonly int HoldMeleeAnimation       = Animator.StringToHash("HoldMelee");
        private static readonly int HoldThrowableAnimation   = Animator.StringToHash("HoldThrowable");

        private static readonly int BasicAttackAnimation     = Animator.StringToHash("BasicAttack");
        private static readonly int GunAttackAnimation       = Animator.StringToHash("GunAttack");
        private static readonly int MeleeAttackAnimation     = Animator.StringToHash("MeleeAttack");
        private static readonly int ThrowableAttackAnimation = Animator.StringToHash("ThrowableAttack");

        private static readonly int DieAnimation = Animator.StringToHash("Die");


        public void RunMeleeAttackAnim()
        {
            if (this.currentWeapon.IsActiveAndType(WeaponType.Melee))
            {
                //this.animator.SetTrigger(MeleeAttackAnimation);
                this.playerAnimation.PlayAttackAnim();
            }
            else if (!this.currentWeapon)
            {
                //this.animator.SetTrigger(BasicAttackAnimation);
                this.playerAnimation.PlayAttackAnim();
            }
        }

        public void RangedAttackAnim()
        {
            //this.animator.SetTrigger(GunAttackAnimation);
            this.playerAnimation.PlayShootAnim();
        }

        public void ThrowableAttackAnim()
        {
            //this.animator.SetTrigger(ThrowableAttackAnimation);
        }

        public void RunUseItemAnimation(bool flag)
        {
            if (flag)
            {
                this.useItemPartical.Play();
            }
            else
            {
                this.useItemPartical.Stop();
            }
        }

        public void DieAnimOn()
        {
            //this.animator.SetBool(DieAnimation, true);
        }

        public void DieAnimOff()
        {
            //this.animator.SetBool(DieAnimation, false);
        }

        public void DieAnim()
        {
            var sequence = DOTween.Sequence();

            // fade all children then set inactive after 1s
            foreach (var child in this.GetComponentsInChildren<SpriteRenderer>())
            {
                sequence.Join(child.material.DOFade(0f, 1f).SetEase(Ease.InOutQuad));
            }

            sequence.OnComplete(() =>
            {
                this.holdPoint.gameObject.SetActive(false);
                this.Visual.gameObject.SetActive(false);

                foreach (var armor in this.Visual.GetComponentsInChildren<ArmorEntity>())
                {
                    armor.Drop();
                }
            });
        }

        public void ReviveAnim()
        {
            this.Visual.gameObject.SetActive(true);
            this.holdPoint.gameObject.SetActive(true);

            var sequence = DOTween.Sequence();

            foreach (var child in this.GetComponentsInChildren<SpriteRenderer>())
            {
                sequence.Join(child.material.DOFade(1f, 1f).SetEase(Ease.InOutQuad));
            }
        }
    }
}