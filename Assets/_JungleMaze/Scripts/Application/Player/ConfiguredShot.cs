using System;
using JungleMaze.App.Ports;
using JungleMaze.Domain.Balance;

namespace JungleMaze.App.Player
{
    /// <summary>
    /// Small config consumer for the local acceptance probe; not a combat/ammo/damage system.
    /// Every shot resolves the selected weapon ID from the injected provider, never hardcodes its balance.
    /// </summary>
    public readonly struct ConfiguredShot
    {
        public readonly float Damage, Speed, Range, Interval;

        public ConfiguredShot(IBalanceConfigProvider balance, string weaponId)
        {
            if (!balance.TryGetWeapon(weaponId, out var weapon) || weapon.Category != WeaponCategory.Ranged)
                throw new ArgumentException("The local projectile probe requires a ranged weapon ID.", nameof(weaponId));
            var stats = weapon.Stats;
            if (!stats.ProjectileSpeed.HasValue || !stats.FireRatePerSecond.HasValue ||
                stats.ProjectileSpeed <= 0 || stats.FireRatePerSecond <= 0 || stats.Range <= 0)
                throw new ArgumentException("The ranged weapon requires positive speed, range and fire rate.");
            Damage = stats.Damage;
            Speed = stats.ProjectileSpeed.Value;
            Range = stats.Range;
            Interval = 1f / stats.FireRatePerSecond.Value;
        }
    }
}
