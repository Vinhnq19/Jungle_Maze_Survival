using _Codebase_.Configs;
using _Codebase_.EffectSystem;
using _Codebase_.Entity._Abstract_;
using _Codebase_.Entity.Player;
using DG.Tweening;
using Managers;
using UnityEngine;
using Utils;

namespace _Concrete_.Effects
{
    [EffectInfo("REVIVAL", true)]
    public class ReviveEffect : TimeBasedEffect
    {
        private PlayerEntity player;

        public override bool Apply(IAffectable target)
        {
            return false;
        }

        public override void OnAttached(IAffectable target)
        {
            if (target is PlayerEntity player)
            {
                this.player = player;
                player.OnTakeDamageIntercept += this.OnTakeDamage;
            }
        }

        public override void OnDetached(IAffectable target)
        {
            if (target is PlayerEntity player)
            {
                player.OnTakeDamageIntercept -= this.OnTakeDamage;
            }
        }

        private bool OnTakeDamage(float damage, PlayerEntity source)
        {
            var hp = this.player.Stats.GetStatValue(StatEnum.Health);

            if (hp - damage <= 0)
            {
                this.player.RemoveEffect(this);

                this.player.Stats.SetStatValue(StatEnum.Health, 100);

                this.player.AddEffect(new ImmortalEffect
                {
                    Time = 2,
                });

                var randomPos = MapManager.I.GetRandomAvailablePosition();

                this.player.Position = new(
                    randomPos.x + 0.5f,
                    randomPos.y + 0.5f
                );

                this.player.transform.DOMove(new Vector2(
                        randomPos.x,
                        randomPos.y
                    ),
                    GameConfig.I.stepTime - 0.01f
                );

                return false;
            }

            return true;
        }
    }
}