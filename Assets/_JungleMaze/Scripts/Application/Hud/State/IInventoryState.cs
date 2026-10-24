using System;
using JungleMaze.Domain.Hud;

namespace JungleMaze.App.Hud.State
{
    /// <summary>Read port of the inventory state (InventoryState): 4 weapon + 4 consumable slots and the selected slot.</summary>
    public interface IInventoryState
    {
        event Action<InventoryChange> Changed;
        int WeaponSlotCount { get; }
        int ConsumableSlotCount { get; }
        /// <summary>0..3 weapon slots, 4..7 consumable slots.</summary>
        InventorySlot GetSlot(int index);
        int ActiveSlot { get; }
    }

    /// <summary>Write port of the inventory state (ITM-01, dev-only mocks). Invalid input is ignored.</summary>
    public interface IInventoryStateWriter
    {
        void SetWeaponSlot(int index, string weaponId);
        /// <summary>count &lt;= 0 empties the slot.</summary>
        void SetConsumableSlot(int index, string equipmentId, int count);
        void SetActiveSlot(int index);
        /// <summary>G-7: slot 1 = HAND and selected, others empty.</summary>
        void Reset();
    }
}
