using _Codebase_.Entity.Player;

namespace _Codebase_.Entity._Abstract_
{
    public interface IDamageable
    {
        void TakeDamage(float damage, PlayerEntity src = null);
    }
}