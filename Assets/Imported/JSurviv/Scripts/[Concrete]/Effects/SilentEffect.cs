using _Codebase_.EffectSystem;
using _Codebase_.Entity.Player;

namespace _Concrete_.Effects
{
    public class SilentEffect : ClockBasedEffect
    {
        public override void Apply(IAffectable target)
        {
            switch (target)
            {
                case PlayerEntity player:
                    this.ApplyOnPlayer(player);
                    break;
            }
        }

        private void ApplyOnPlayer(PlayerEntity player)
        {
            //Silent
            player.PlayerAction = null;
        }
    }
}