using System;
using System.Collections.Generic;
using JungleMaze.App.Ports;
using JungleMaze.Core.Logging;
using JungleMaze.Domain.Balance;

namespace JungleMaze.App.Hud
{
    /// <summary>
    /// Item names and maxima for the HUD, read from balance JSON. The only place to change if item names move to
    /// another language field (Q10: English displayName for now). Unknown ids show the raw id and warn once each.
    /// </summary>
    public sealed class HudItemLabels
    {
        private readonly IBalanceConfigProvider _balance;
        private readonly ILog _log;
        private readonly HashSet<string> _warned = new HashSet<string>(StringComparer.Ordinal);

        public HudItemLabels(IBalanceConfigProvider balance, ILog log = null)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _log = log ?? NullLog.Instance;
        }

        /// <summary>displayName of a weapon or equipment id; empty for null; the id itself if unknown.</summary>
        public string DisplayName(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return "";
            if (_balance.TryGetWeapon(itemId, out var weapon)) return weapon.DisplayName;
            if (_balance.TryGetEquipment(itemId, out var equipment)) return equipment.DisplayName;
            WarnOnce(itemId);
            return itemId;
        }

        /// <summary>Weapon definition, or null for unknown ids / equipment.</summary>
        public WeaponDefinition Weapon(string itemId) =>
            !string.IsNullOrEmpty(itemId) && _balance.TryGetWeapon(itemId, out var weapon) ? weapon : null;

        /// <summary>durabilityPoints of a helmet/armor, or 0 when unknown.</summary>
        public float MaxDurability(string equipmentId)
        {
            if (!string.IsNullOrEmpty(equipmentId) && _balance.TryGetEquipment(equipmentId, out var equipment)
                                                    && equipment.Stats.DurabilityPoints.HasValue)
                return equipment.Stats.DurabilityPoints.Value;
            if (!string.IsNullOrEmpty(equipmentId)) WarnOnce(equipmentId);
            return 0f;
        }

        private void WarnOnce(string itemId)
        {
            if (_warned.Add(itemId)) _log.Warn($"[HUD] Unknown item id '{itemId}' in balance JSON; showing the raw id.");
        }
    }
}
