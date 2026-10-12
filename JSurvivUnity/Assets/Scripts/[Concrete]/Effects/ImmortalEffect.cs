using _Codebase_.EffectSystem;
using _Codebase_.Entity.Player;
using _Codebase_.ObjectPool;
using _Concrete_.Configs;
using UnityEngine;

namespace _Concrete_.Effects
{
    [EffectInfo("UNDEAD", true)]
    public class ImmortalEffect : ClockBasedEffect
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
                    // Tạo hiệu ứng VFX cho Immunity
                    this._vfxInfo = VfxConfig.I.immortalVfx.SpawnVfx(new()
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
                player.IsImmortal = false;
                this._vfxInfo?.Dispose();
            }
        }

        //Xóa OnDetach
        private void ApplyOnPlayer(PlayerEntity player)
        {
            player.IsImmortal = true;
            player.RemoveAllDebuffs();
        }
    }
}