using _Codebase_.EffectSystem;
using _Codebase_.Entity.Player;
using _Codebase_.ObjectPool;
using _Concrete_.Configs;
using UnityEngine;

namespace _Concrete_.Effects
{
    [EffectInfo("REVERSE", false)]
    public class ReverseMovementEffect : ClockBasedEffect
    {
        public VfxInfo _vfxInfo;

        public override void Apply(IAffectable target)
        {
            if (target is PlayerEntity player)
            {
                player.IsReverseMovement = true;
            }
        }

        public override void OnAttached(IAffectable target)
        {
            switch (target)
            {
                case PlayerEntity player:
                    // Tạo hiệu ứng VFX
                    this._vfxInfo = VfxConfig.I.reverseVfx.SpawnVfx(new()
                    {
                        Position = player.transform.TransformPoint(Vector3.up * 0.2f),
                        Parent = player.transform,
                    });

                    break;
            }
        }

        public override void OnDetached(IAffectable target)
        {
            if (target is PlayerEntity player)
            {
                player.IsReverseMovement = false;
                this._vfxInfo?.Dispose();
            }
        }
    }
}