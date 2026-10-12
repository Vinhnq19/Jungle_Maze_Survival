using _Codebase_.EffectSystem;
using _Codebase_.Entity.Player;
using _Codebase_.ObjectPool;
using _Concrete_.Configs;
using UnityEngine;

namespace _Concrete_.Effects
{
    [EffectInfo("BLEED", false)]
    public class BleedEffect : ClockBasedEffect
    {
        private float   _intervalCounter;
        public  VfxInfo _vfxInfo;

        // HP decease after every interval passed 
        public float Interval { get; set; } = 1.0f;
        public float Damage { get; set; } = 5.0f;

        public PlayerEntity DamageSource { get; set; }

        public override void Apply(IAffectable target)
        {
            this._intervalCounter += UnityEngine.Time.deltaTime;

            if (this._intervalCounter < this.Interval) return;

            this._intervalCounter = 0;

            switch (target)
            {
                case PlayerEntity player:
                    this.ApplyOnPlayer(player);
                    break;
            }
        }

        private void ApplyOnPlayer(PlayerEntity player)
        {
            // TODO: change this in future, I just made an example to decrease HP by fixed value (here is 5)  
            player.TakeDamage(this.Damage, this.DamageSource);
        }

        public override void OnAttached(IAffectable target)
        {
            switch (target)
            {
                case PlayerEntity player:
                    this._vfxInfo = VfxConfig.I.bleedVfx.SpawnVfx(new()
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