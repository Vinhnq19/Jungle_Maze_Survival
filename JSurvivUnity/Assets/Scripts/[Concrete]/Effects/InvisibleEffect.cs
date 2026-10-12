using _Codebase_.EffectSystem;
using _Codebase_.Entity.Player;

namespace _Concrete_.Effects
{
    //tàng hình là hiệu ứng duy trì theo thời gian
    [EffectInfo("INVISIBLE", true)]
    public class InvisibleEffect : ClockBasedEffect
    {
        public override void Apply(IAffectable target)
        {
            //Đối tượng nhận hiệu ứng tàng hình là player
            switch (target)
            {
                case PlayerEntity player:
                    this.ApplyInvisibility(player, true); //Bật tàng hình
                    break;
            }
        }

        public override void OnAttached(IAffectable target)
        {
            switch (target)
            {
                case PlayerEntity player:
                    this.ApplyInvisibility(player, true); //Bật tàng hình
                    player.OnPlayerAttack += this.OnBreakInvisibilityHandler;

                    break;
            }
        }

        public override void OnDetached(IAffectable target)
        {
            switch (target)
            {
                case PlayerEntity player:
                    this.ApplyInvisibility(player, false); //Tắt tàng hình
                    player.OnPlayerAttack -= this.OnBreakInvisibilityHandler;
                    break;
            }
        }

        public void ApplyInvisibility(PlayerEntity player, bool invisible)
        {
            foreach (var spriteRenderer in player.renderers)
            {
                if (spriteRenderer)
                {
                    var color = spriteRenderer.color;
                    color.a = invisible ? 0.5f : 1.0f;
                    spriteRenderer.color = color;

                    //spriteRenderer.enabled = !invisible;
                }
            }
        }

        private void OnBreakInvisibilityHandler(PlayerEntity player)
        {
            player.RemoveEffectOfType<InvisibleEffect>();
        }
    }
}