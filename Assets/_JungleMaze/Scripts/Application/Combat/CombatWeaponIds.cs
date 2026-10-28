using System;
using JungleBackend.Protocol.Gameplay;

namespace JungleMaze.App.Combat
{
    /// <summary>
    /// Maps the wire <see cref="CombatWeaponId"/> to the id used by balance/weapons.json. The order is fixed by the
    /// protocol enum; the json id is what the HUD and the balance provider use.
    /// </summary>
    public static class CombatWeaponIds
    {
        public const int Count = 10;

        private static readonly string[] JsonIds =
        {
            "HAND", "TREE_BRANCH", "KNIFE", "BONE", "AXE", "MACE", "RUBBER_GUN", "SCEPTER", "CROSSBOW", "SHOTGUN"
        };

        /// <summary>Returns the balance id for a valid wire id.</summary>
        public static string ToJsonId(CombatWeaponId weapon) => JsonIds[(int)weapon];

        /// <summary>Returns false for ids outside the ten client weapons (thrown and special items are not handled here).</summary>
        public static bool TryFromJsonId(string id, out CombatWeaponId weapon)
        {
            for (var i = 0; i < JsonIds.Length; i++)
            {
                if (string.Equals(JsonIds[i], id, StringComparison.Ordinal))
                {
                    weapon = (CombatWeaponId)i;
                    return true;
                }
            }
            weapon = default;
            return false;
        }

        /// <summary>True when the wire value is one of the ten known weapons.</summary>
        public static bool IsKnown(CombatWeaponId weapon) => (int)weapon < Count;
    }
}
