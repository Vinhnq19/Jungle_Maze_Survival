using _Codebase_.EffectSystem;
using _Codebase_.Entity.Player;
using _Codebase_.ObjectPool;
using _Concrete_.Configs;
using UnityEngine;

namespace _Concrete_.Effects
{
    [EffectInfo("BUFF_DAMAGE", true)]
    public class BuffDamageEffect : ClockBasedEffect
    {
        private VfxInfo _vfxInfo;
        public float DamageBonus { get; set; }

        public override void OnAttached(IAffectable target)
        {
            switch (target)
            {
                case PlayerEntity player:
                    player.DamageMultiplier += this.DamageBonus;

                    this._vfxInfo = VfxConfig.I.buffDamageVfx.SpawnVfx(new()
                    {
                        Position = player.transform.TransformPoint(Vector3.up * 0.2f),
                        Parent = player.transform,
                    });

                    break;
            }
        }

        public override void Apply(IAffectable target)
        {
        }

        public override void OnDetached(IAffectable target)
        {
            switch (target)
            {
                case PlayerEntity player:
                    player.DamageMultiplier -= this.DamageBonus;
                    this._vfxInfo?.Dispose();
                    break;
            }
        }
    }
}