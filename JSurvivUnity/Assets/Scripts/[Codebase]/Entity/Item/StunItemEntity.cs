using System.Collections.Generic;
using UnityEngine;
using _Codebase_.EffectSystem;
using _Concrete_.Effects;
using _Codebase_.Entity.Player;
using Managers;

namespace _Codebase_.Entity.Item
{
    public class StunItemEntity : BaseItemEntity
    {
        [field: SerializeField]
        public float Duration { get; private set; }

        [field: SerializeField]
        public float Range { get; private set; } = 4.5f; // 9x9 -> bán kính 4.5

        protected override void OnBeforeUsed()
        {
        }

        protected override void OnAfterUsed(bool success)
        {
            if (!this.MountedPlayer) return;

            var center = this.MountedPlayer.Position;

            foreach (var player in PlayerManager.I.Players)
            {
                if (player == this.MountedPlayer) continue;

                if (Mathf.Abs(player.Position.x - center.x) <= 4.5f && Mathf.Abs(player.Position.y - center.y) <= 4.5f)
                {
                    player.AddEffect(new StunEffect
                    {
                        Time = this.Duration
                    });
                }
            }
        }
    }
}
