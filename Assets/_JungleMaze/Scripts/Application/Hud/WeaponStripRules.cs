using JungleMaze.Domain.Balance;

namespace JungleMaze.App.Hud
{
    /// <summary>Display rules for the held-weapon strip of the quick bar (G-5, G-12).</summary>
    public static class WeaponStripRules
    {
        /// <summary>G-5: ammo numbers only for ranged weapons with a magazine; melee/thrown/special/bare hands hide them.</summary>
        public static bool ShowAmmo(WeaponDefinition weapon) =>
            weapon != null && weapon.Category == WeaponCategory.Ranged
                           && weapon.Stats.MagazineSize.HasValue && weapon.Stats.MagazineSize.Value > 0;

        /// <summary>G-12: "R Nạp" only when reloading is possible: magazine not full and reserve &gt; 0.</summary>
        public static bool ShowReloadHint(WeaponDefinition weapon, int inMagazine, int reserve) =>
            ShowAmmo(weapon) && inMagazine < weapon.Stats.MagazineSize.Value && reserve > 0;
    }
}
