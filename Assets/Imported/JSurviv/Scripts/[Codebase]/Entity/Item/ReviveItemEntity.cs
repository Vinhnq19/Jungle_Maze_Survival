using _Codebase_.EffectSystem;
using _Concrete_.Effects;
using UnityEngine;

namespace _Codebase_.Entity.Item
{
    public class ReviveItemEntity : BaseItemEntity
    {
        protected override void OnBeforeUsed()
        {
        }

        protected override void OnAfterUsed(bool success)
        {
            if (!this.MountedPlayer) return;

            this.MountedPlayer.AddEffect(new ReviveEffect
            {
                Time = 1,
            });
        }
        public override void Mount(Transform mountedTf)
        {
            base.Mount(mountedTf);
            if (this.MountedPlayer)
            {
               this.TryUse();
            }
        }
    }
}