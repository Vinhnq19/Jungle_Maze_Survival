using System.Collections.Generic;
using JungleMaze.Domain.Balance;

namespace JungleMaze.App.Ports
{
    /// <summary>
    /// Data-driven gameplay balance (weapons/equipments/match config), loaded from
    /// StreamingAssets/JungleMaze/balance/*.json. Implemented in Infrastructure so changing the
    /// JSON files changes balance without touching gameplay code or rebuilding.
    /// </summary>
    public interface IBalanceConfigProvider
    {
        MatchConfigData Match { get; }
        IReadOnlyList<WeaponDefinition> Weapons { get; }
        IReadOnlyList<EquipmentDefinition> Equipments { get; }

        bool TryGetWeapon(string id, out WeaponDefinition weapon);
        bool TryGetEquipment(string id, out EquipmentDefinition equipment);

        /// <summary>Non-null if any file failed to load/validate; a safe fallback is still available.</summary>
        string LastError { get; }
    }
}
