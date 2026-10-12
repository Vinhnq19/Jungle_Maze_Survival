using System.Collections.Generic;
using _Codebase_.EffectSystem;
using _Codebase_.Entity._Abstract_;
using _Codebase_.Entity.Player;
using _Concrete_.Effects;
using Managers;
using UnityEngine;
using Utils;

public class OAncientWell : ObstacleBase
{
    [SerializeField]
    private float blessChance = 0.5f;

    [SerializeField]
    private float curseChance = 0.5f;

    [SerializeField]
    private float interactionDuration = 2f;

    private readonly HashSet<PlayerEntity> interactedPlayers = new();

    private readonly Dictionary<PlayerEntity, float> interactInfos = new();

    protected override void OnPlayerStay(PlayerEntity playerEntity)
    {
        if (!playerEntity.GetCurrentWeapon())
        {
            this.interactInfos.Remove(playerEntity);
            return;
        }

        if (this.interactInfos.TryGetValue(playerEntity, out var duration))
        {
            duration += Time.deltaTime;
        }

        this.interactInfos[playerEntity] = duration;

        if (
            duration >= this.interactionDuration &&
            this.interactedPlayers.Add(playerEntity)
        )
        {
            this.DropPlayerCurrentWeapon(playerEntity);
        }
    }

    protected override void OnPlayerExit(PlayerEntity playerEntity)
    {
        this.interactInfos.Remove(playerEntity);
    }

    private void DropPlayerCurrentWeapon(PlayerEntity playerEntity)
    {
        var currentWeapon = playerEntity.GetCurrentWeapon();

        if (currentWeapon)
        {
            var random = Random.Range(0, this.blessChance + this.curseChance);

            if (random < this.blessChance)
            {
                var weapon = WeaponManager.I.SpawnWeapon(
                    currentWeapon,
                    playerEntity.transform.position
                );

                weapon.Buffs.Add(new()
                {
                    BonusDamage = 0.5f,
                    Duration = 150f,
                });
            }
            else
            {
                // lost 50% hp and weapon, also can't use weapon in 20s

                var currentHp = playerEntity.Stats.GetStatValue(StatEnum.Health);
                playerEntity.Stats.SetStatValue(StatEnum.Health, currentHp * 0.5f);

                playerEntity.AddEffect(new CantUseWeaponEffect
                {
                    Time = 20f,
                });
            }

            playerEntity.DropWeapon();
        }
    }
}