#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace JungleMaze.App.Hud.Mocks
{
    /// <summary>
    /// Dev-only presets on top of the three mocks (G-8): the approved v2 mockup (AC14), HP step scenarios for the
    /// smoothness check (AC21) and a deterministic stress step that writes every kind of value (AC18).
    /// Not compiled into release builds.
    /// </summary>
    public sealed class HudMockScenarios
    {
        private static readonly string[] StressWeapons = { "CROSSBOW", "RUBBER_GUN", "SCEPTER", "SHOTGUN", "HAND", "KNIFE" };
        private static readonly string[] StressArmors = { "WOODEN_ARMOR", "ARMOR", "MAGIC_ARMOR", null };
        private static readonly string[] StressHelmets = { "WOODEN_HELMET", "SPIKE_HELMET", "MAGIC_HELMET", null };
        private static readonly string[] StressConsumables = { "GOD_LEAF", "UNICORN_BLOOD", "ADRENALINE", null };

        public MockHudState Hud { get; }
        public MockInventoryState Inventory { get; }
        public MockEffectState Effects { get; }

        public HudMockScenarios(MockHudState hud, MockInventoryState inventory, MockEffectState effects)
        {
            Hud = hud ?? throw new ArgumentNullException(nameof(hud));
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            Effects = effects ?? throw new ArgumentNullException(nameof(effects));
        }

        /// <summary>
        /// "AC14 = mockup v2" (41-2_hud.png): weapons {HAND, CROSSBOW, RUBBER_GUN, empty} with slot 2 selected,
        /// consumables {GOD_LEAF ×6, UNICORN_BLOOD ×1, empty, ADRENALINE ×4}, Spiked Armor 48, Wooden Helmet 12,
        /// Adrenaline 5/8 s, Crossbow 4/30, zone 84 s, 7/10 alive, HP 72.
        /// </summary>
        public void MockupV2()
        {
            Inventory.SetSlot(0, "HAND");
            Inventory.SetSlot(1, "CROSSBOW");
            Inventory.SetSlot(2, "RUBBER_GUN");
            Inventory.SetSlot(3, null);
            Inventory.SetSlot(4, "GOD_LEAF", 6);
            Inventory.SetSlot(5, "UNICORN_BLOOD", 1);
            Inventory.SetSlot(6, null, 0);
            Inventory.SetSlot(7, "ADRENALINE", 4);
            Inventory.Select(1);
            Hud.Hold("CROSSBOW");
            Hud.SetAmmo(4, 30);
            Hud.SetHp(72);
            Hud.EquipArmor("ARMOR", 48f);
            Hud.EquipHelmet("WOODEN_HELMET", 12f);
            Hud.Zone(84f);
            Hud.Alive(7, 10);
            Effects.Adrenaline(8f, 5f);
        }

        /// <summary>One AC21 step: HP down by <paramref name="step"/> (min 0). Returns false once HP is 0.</summary>
        public bool HpStepDown(int step = 10)
        {
            Hud.Damage(step);
            return Hud.Hp > 0;
        }

        /// <summary>One AC21 step: HP up by <paramref name="step"/> (max HP). Returns false once HP is full.</summary>
        public bool HealStepUp(int step = 10)
        {
            Hud.Heal(step);
            return Hud.Hp < Hud.MaxHp;
        }

        /// <summary>
        /// AC18: writes every kind of value, deterministically from <paramref name="frame"/> (call 30 times per second).
        /// </summary>
        public void StressStep(int frame)
        {
            var i = frame < 0 ? -frame : frame;
            Hud.SetHp(i % (Hud.MaxHp + 1));
            Hud.EquipArmor(StressArmors[i % StressArmors.Length], i % 130);
            Hud.EquipHelmet(StressHelmets[(i / 3) % StressHelmets.Length], i % 75);
            var weapon = StressWeapons[(i / 7) % StressWeapons.Length];
            Hud.Hold(weapon);
            Hud.SetAmmo(i % 13, (i * 7) % 61);
            Hud.Zone(600f - i % 600);
            Hud.Alive(10 - i % 11, 10);
            Hud.Pose(i % 50 + 0.5f, (i * 3) % 50 + 0.5f, i * 5 % 360);
            Inventory.SetSlot(i % 4, StressWeapons[i % StressWeapons.Length]);
            Inventory.SetSlot(4 + i % 4, StressConsumables[(i / 4) % StressConsumables.Length], 1 + i % 6);
            Inventory.Select(i % MockInventoryState.SlotCount);
            if (i % 45 == 0) Effects.Adrenaline(8f, 8f);
            if (i % 200 == 199) Effects.Clear();
        }
    }
}
#endif
