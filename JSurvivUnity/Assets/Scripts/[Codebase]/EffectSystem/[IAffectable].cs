using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using _Concrete_.Effects;
using _Codebase_.Entity.Player; 
using Managers;                  
using Managers.DataObjects; 

namespace _Codebase_.EffectSystem
{
    // ReSharper disable once IdentifierTypo
    public interface IAffectable
    {
        List<IEffect> Effects { get; }
    }

    // ReSharper disable once IdentifierTypo
    public static class AffectableExtensions
    {
        public static bool HasEffectOfType<T>(this IAffectable target)
        {
            return target.Effects.Any(e => e is T);
        }

        public static void ApplyEffects(this IAffectable target)
        {
            foreach (var effect in target.Effects.ToArray())
            {
                effect.Apply(target);
            }
        }

        public static void AddEffect(this IAffectable target, IEffect effect)
        {
            if (!effect.ShouldStacking(target)) return;

            target.Effects.Add(effect);
            effect.OnAttached(target);

            if (target is PlayerEntity player)
            {
                var request = new GamePlayerEffectsApply.Request
                {
                    playerId = player.Id,
                    effect = effect.ToEffectSchema() 
                };
                NetworkManager.I.Send(request);
            }
        }

        public static void RemoveEffect(this IAffectable target, IEffect effect)
        {
            if (!target.Effects.Contains(effect)) return;
            
            target.Effects.Remove(effect);
            effect.OnDetached(target);
            
            if (target is PlayerEntity player)
            {
                var request = new GamePlayerEffectsClear.Request
                {
                    playerId = player.Id,
                    effect = effect.GetEffectName() 
                };
                NetworkManager.I.Send(request);
            }
        }

        public static void RemoveEffectOfType<T>(this IAffectable target) where T : IEffect
        {
            var effectsToRemove = target.Effects.OfType<T>().ToArray();
            
            foreach (var effect in effectsToRemove)
            {
                target.RemoveEffect(effect);
            }
        }

        public static void RemoveAllEffects(this IAffectable target)
        {
            var effects = target.Effects.ToArray();
            target.Effects.Clear();

            foreach (var effect in effects)
            {
                effect.OnDetached(target);
            }

            if (target is PlayerEntity player)
            {
                var request = new GamePlayerEffectsClear.Request
                {
                    playerId = player.Id,
                    effect = null 
                };
                NetworkManager.I.Send(request);
            }
        }
        
        public static void RemoveAllBuffs(this IAffectable target)
        {
            var effects = target.Effects.Where(effect =>
            {
                var info = effect.GetType().GetCustomAttribute<EffectInfoAttribute>();
                return info is { IsBuff: true };
            }).ToArray();

            foreach (var effect in effects)
            {
                target.RemoveEffect(effect);
            }
        }

        public static void RemoveAllDebuffs(this IAffectable target)
        {
            var effects = target.Effects.Where(effect =>
            {
                var info = effect.GetType().GetCustomAttribute<EffectInfoAttribute>();
                return info is null or { IsBuff: false };
            }).ToArray();

            foreach (var effect in effects)
            {
                target.RemoveEffect(effect);
            }
        }
    }
}