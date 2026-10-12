using _Codebase_.EffectSystem;
using _Codebase_.Entity.Player;
using _Codebase_.ObjectPool;
using _Concrete_.Configs;
using UnityEngine;

namespace _Concrete_.Effects
{
    [EffectInfo("STUN", false)]
    public class StunEffect : ClockBasedEffect
    {
        private VfxInfo _vfxInfo;

        public override void Apply(IAffectable target)
        {
            switch (target)
            {
                case PlayerEntity player:
                    this.ApplyOnPlayer(player);
                    break;
            }
        }

        public override void OnAttached(IAffectable target)
        {
            switch (target)
            {
                case PlayerEntity player:
                    // Tạo hiệu ứng VFX cho Stun
                    this._vfxInfo = VfxConfig.I.stunnedVfx.SpawnVfx(new()
                    {
                        Position = player.transform.TransformPoint(Vector3.up * 0.3f),
                        Parent = player.transform,
                    });

                    break;
            }
        }

        public override void OnDetached(IAffectable target)
        {
            switch (target)
            {
                case PlayerEntity player:
                    // Xóa hiệu ứng VFX
                    this._vfxInfo?.Dispose();
                    break;
            }
        }

        private void ApplyOnPlayer(PlayerEntity player)
        {
            player.MovementQueue.Clear();
            player.PlayerAction = null;
        }
    }
}