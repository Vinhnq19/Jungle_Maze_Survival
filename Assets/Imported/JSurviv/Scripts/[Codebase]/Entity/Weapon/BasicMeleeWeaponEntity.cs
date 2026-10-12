namespace _Codebase_.Entity.Weapon
{
    public class BasicMeleeWeaponEntity : BaseWeaponEntity
    {

        protected override void OnAttack()
        {
            if (this.MountedPlayer != null)
            {
                this.MountedPlayer.OnMeleeAttack();
            }
        }

        public override WeaponType GetWeaponType()
        {
            return WeaponType.Melee;
        }
    }
}