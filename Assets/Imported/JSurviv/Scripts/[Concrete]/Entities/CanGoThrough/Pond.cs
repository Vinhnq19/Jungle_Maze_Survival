using System.Collections.Generic;
using _Codebase_.Entity.CanGoThrough;
using _Codebase_.Entity.Player;
using _Codebase_.ObjectPool;
using _Concrete_.Configs;
using UnityEngine;

namespace _Concrete_.Entities.CanGoThrough
{
    public class Pond : ObstacleBase
    {
        private readonly Dictionary<PlayerEntity, VfxInfo> _vfxInfo = new();

        protected override void OnPlayerEnter(PlayerEntity player)
        {
            if (this._vfxInfo.ContainsKey(player))
            {
                return;
            }

            this._vfxInfo[player] = VfxConfig.I.waterVfx.SpawnVfx(new()
            {
                Position = player.transform.position,
                Scale = Vector3.one * 5f,
                Parent = player.transform,
            });
        }

        protected override void OnPlayerStay(PlayerEntity player)
        {
        }

        protected override void OnPlayerExit(PlayerEntity player)
        {
            if (this._vfxInfo.TryGetValue(player, out var vfxInfo))
            {
                vfxInfo.Dispose();
                this._vfxInfo.Remove(player);
            }
        }
    }
}