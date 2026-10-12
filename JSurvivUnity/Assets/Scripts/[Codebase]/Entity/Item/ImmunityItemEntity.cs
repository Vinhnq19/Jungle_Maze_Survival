using _Codebase_.EffectSystem;
using _Concrete_.Effects;
using UnityEngine;

namespace _Codebase_.Entity.Item
{
    public class ImmortalItemEntity : BaseItemEntity
    {
        [field: SerializeField]
        public float Duration { get; private set; }
        [field: SerializeField]
        public float HealAmount { get; private set; }

        protected override void OnBeforeUsed()
        {
        }

        protected override void OnAfterUsed(bool success)
        {
            if (!this.MountedPlayer) return;

            this.MountedPlayer.AddEffect(new ImmunityEffect
            {
                Time = this.Duration,
            });
            this.MountedPlayer.Heal(this.HealAmount);
        }
    }
}