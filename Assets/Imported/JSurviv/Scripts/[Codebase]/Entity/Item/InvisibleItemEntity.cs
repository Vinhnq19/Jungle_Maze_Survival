using _Codebase_.EffectSystem;
using _Concrete_.Effects;
using UnityEngine;

namespace _Codebase_.Entity.Item
{
    public class InvisibleItemEntity : BaseItemEntity
    {
        [field: SerializeField]
        public float Duration { get; private set; }

        protected override void OnBeforeUsed()
        {
        }

        protected override void OnAfterUsed(bool success)
        {
            if (!this.MountedPlayer) return;

            this.MountedPlayer.AddEffect(new InvisibleEffect
            {
                Time = this.Duration,
            });
        }
    }
}