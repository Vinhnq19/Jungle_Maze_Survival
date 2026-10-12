using UnityEngine;

namespace _Codebase_.Entity.Item
{
    public class HealingItemEntity : BaseItemEntity
    {
        [field: SerializeField]
        public float HealAmount { get; private set; }

        protected override void OnBeforeUsed()
        {
        }

        protected override void OnAfterUsed(bool success)
        {
            if (!this.MountedPlayer) return;

            if (!success) return;
            this.MountedPlayer.Heal(this.HealAmount);
        }
    }
}