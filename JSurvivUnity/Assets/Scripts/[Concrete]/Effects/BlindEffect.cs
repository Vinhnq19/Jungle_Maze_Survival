using _Codebase_.EffectSystem;
using _Codebase_.Entity.Player;
using _Codebase_.ObjectPool;
using _Concrete_.Configs;
using UnityEngine;

namespace _Concrete_.Effects
{
    [EffectInfo("BLIND", false)]
    public class BlindEffect : ClockBasedEffect
    {
        private VfxInfo _vfxInfo;

        public override void Apply(IAffectable target)
        {
        }

        public override void OnAttached(IAffectable target)
        {
            switch (target)
            {
                case PlayerEntity player:
                    this._vfxInfo = VfxConfig.I.blindVfx.SpawnVfx(new()
                    {
                        Position = player.transform.TransformPoint(Vector3.down * 0.1f),
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
                    this._vfxInfo?.Dispose();
                    break;
            }
        }
    }
}