using _Codebase_.Entity.Weapon;

namespace _Codebase_.Utils
{
    public static class WeaponExtensions
    {
        public static bool IsActiveAndType(this BaseWeaponEntity weapon, WeaponType type)
        {
            return weapon && weapon.GetWeaponType() == type;
        }
    }
}