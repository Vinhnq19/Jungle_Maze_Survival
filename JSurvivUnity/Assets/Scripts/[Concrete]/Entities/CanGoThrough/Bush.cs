using System.Linq;
using _Codebase_.EffectSystem;
using _Codebase_.Entity.Player;
using _Concrete_.Effects;
using Managers;
using UnityEngine;

namespace _Concrete_.Entities.CanGoThrough
{
    public class Bush : ObstacleBase 
    {
        private InvisibleEffect _invisibleEffefct;
        private SpriteRenderer spriteRenderer;

        protected override void Start()
        {
            base.Start(); 
            
            this._invisibleEffefct = new InvisibleEffect()
            {
                Time = 50000f,
            };

            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        protected override void OnPlayerEnter(PlayerEntity player)
        {
            player.currentInteractiveObstacle = this;
            Color color = spriteRenderer.color;
            color.a = 0.7f;
            spriteRenderer.color = color;   
        }

        protected override void OnPlayerStay(PlayerEntity player)
        {
            if (player.Effects.FirstOrDefault(e => e is InvisibleEffect) is not InvisibleEffect effect)
            {
                player.AddEffect(this._invisibleEffefct);
            }
            else
            {
                //effect.Time = Mathf.Max(1f, effect.Time
            }
        }

        protected override void OnPlayerExit(PlayerEntity player)
        {
            player.currentInteractiveObstacle = null; 
            player.RemoveEffect(this._invisibleEffefct);
            Color color = spriteRenderer.color;
            color.a = 1f;
            spriteRenderer.color = color;
        }
        
        public void RevealPlayer(PlayerEntity player)
        {
            this.OnPlayerExit(player);
        }

    }
}