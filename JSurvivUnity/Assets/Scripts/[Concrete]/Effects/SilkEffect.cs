using System;
using System.Collections.Generic;
using _Codebase_.EffectSystem;
using _Codebase_.Entity.Player;

namespace _Concrete_.Effects
{
    public class SilkEffect : ClockBasedEffect
    {

        /**
     * Key: Player
     * Value: Silk stack
     */
        private static readonly Dictionary<PlayerEntity, int> s_silkedPlayerDict = new();

        private float intervalCounter;

        private bool isApplied;
        public float Interval { get; set; } = 10f;
        public float SilkDamage { get; set; } = 1f;
        public int MaxSilkLevel { get; set; } = 2;
        public PlayerEntity DamageSource { get; set; }

        public override void Apply(IAffectable target)
        {
            switch (target)
            {
                case PlayerEntity player:
                    if (!this.isApplied)
                    {
                        this.ApplyOnPlayer(player);
                        this.isApplied = true;
                    }

                    this.intervalCounter += UnityEngine.Time.deltaTime;

                    if (this.intervalCounter >= this.Interval)
                    {
                        s_silkedPlayerDict[player] = Math.Max(0, s_silkedPlayerDict[player] - 1);
                        this.intervalCounter = 0;
                    }

                    break;
            }
        }

        public override void OnAttached(IAffectable target)
        {
        }

        public override void OnDetached(IAffectable target)
        {
        }


        private void ApplyOnPlayer(PlayerEntity player)
        {
            if (s_silkedPlayerDict.ContainsKey(player))
            {
                this.DealDamageByStack(player);
            }
            else
            {
                s_silkedPlayerDict.Add(player, 1);
            }
        }

        private void DealDamageByStack(PlayerEntity player)
        {
            var stack = ++s_silkedPlayerDict[player];

            if (stack >= this.MaxSilkLevel)
            {
                player.TakeDamage(this.MaxSilkLevel * this.SilkDamage, this.DamageSource);
                s_silkedPlayerDict[player] = 0;
            }
        }
    }
}