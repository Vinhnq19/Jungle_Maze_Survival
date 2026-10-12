using System;
using System.Reflection;
using _Codebase_.Configs;
using Managers.DataObjects;
using UnityEngine;

namespace _Codebase_.EffectSystem
{
    public interface IEffect
    {
        bool ShouldStacking(IAffectable target);

        void Apply(IAffectable target);

        void OnAttached(IAffectable target);
        void OnDetached(IAffectable target);

        EffectSchema ToEffectSchema()
        {
            float stepTime = GameConfig.I.stepTime;

            return this switch
            {
                ClockBasedEffect effect => new EffectSchema()
                {
                    Id = effect.GetEffectName(),
                    Duration = (int)Mathf.Ceil(effect.Time / stepTime),
                },
                TimeBasedEffect effect => new EffectSchema()
                {
                    Id = effect.GetEffectName(),
                    Duration = effect.Time,
                },
                _ => throw new System.ArgumentOutOfRangeException()
            };
        }
    }
    
    public static class EffectExtensions
    {
        public static string GetEffectName(this IEffect effect)
        {
            var effectInfo = effect.GetType().GetCustomAttribute<EffectInfoAttribute>();

            return effectInfo?.Id ?? effect.GetType().Name;
        }
    }
}
