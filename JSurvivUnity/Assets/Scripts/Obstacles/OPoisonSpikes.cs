using _Codebase_.EffectSystem;
using _Codebase_.Entity.Player;
using _Concrete_.Effects;
using System.Collections.Generic;
using UnityEngine;

public class OPoisonSpikes : ObstacleBase
{
    private readonly Dictionary<PlayerEntity, PoisonsEffect> _activeEffects = new();

    [field: SerializeField]
    public int poisonDamage { get; private set; }

    [field: SerializeField]
    public int poisonTime { get; private set; }

    protected override void OnPlayerEnter(PlayerEntity playerEntity)
    {
        ApplyOrRefreshEffect(playerEntity);
    }

    protected override void OnPlayerStay(PlayerEntity playerEntity)
    {
        ApplyOrRefreshEffect(playerEntity);
    }

    protected override void OnPlayerExit(PlayerEntity playerEntity)
    {
        _activeEffects.Remove(playerEntity); 
    }

    private void ApplyOrRefreshEffect(PlayerEntity playerEntity)
    {
        if (_activeEffects.TryGetValue(playerEntity, out var existingEffect))
        {
            existingEffect.Time = this.poisonTime;
        }
        else
        {
            var newEffect = new PoisonsEffect
            {
                Damage = this.poisonDamage,
                Time = this.poisonTime
            };
            playerEntity.AddEffect(newEffect);
            _activeEffects[playerEntity] = newEffect;
        }
    }
}
