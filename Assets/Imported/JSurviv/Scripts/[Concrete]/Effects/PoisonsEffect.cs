using _Codebase_.EffectSystem;
using _Codebase_.Entity.Player;
using _Codebase_.ObjectPool;
using _Concrete_.Configs;
using UnityEngine;

namespace _Concrete_.Effects
{
    	
    [EffectInfo("POISON", false)]
    public class PoisonsEffect : ClockBasedEffect
    {
        private float   _intervalCounter;
        public  VfxInfo _vfxInfo;

        public float IntervalTime { get; set; } = 1.0f;
        public PlayerEntity DamageSource { get; set; }

        public float Damage { get; set; } = 5.0f;

        public override void Apply(IAffectable target)
        {
            this._intervalCounter += UnityEngine.Time.deltaTime;

            if (this._intervalCounter < this.IntervalTime) return;

            this._intervalCounter = 0;

            switch (target)
            {
                case PlayerEntity player:
                    this.applyPoisonPlayer(player);
                    break;
            }
        }

        private void applyPoisonPlayer(PlayerEntity player)
        {
            player.TakeDamage(this.Damage, this.DamageSource);
        }

        public override void OnAttached(IAffectable target)
        {
            switch (target)
            {
                case PlayerEntity player:
                    this._vfxInfo = VfxConfig.I.poisonVfx.SpawnVfx(new()
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