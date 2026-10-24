namespace JungleMaze.Domain.Hud
{
    /// <summary>
    /// The 4 weapon + 4 consumable quick-bar slots and the selected slot. Out-of-range indices are ignored;
    /// setters return the change flags (None when nothing changed) and never throw.
    /// </summary>
    public sealed class InventorySlots
    {
        public const int WeaponSlotCount = 4;
        public const int ConsumableSlotCount = 4;
        public const int TotalSlotCount = WeaponSlotCount + ConsumableSlotCount;

        private readonly InventorySlot[] _slots = new InventorySlot[TotalSlotCount];

        /// <summary>0..3 weapon slots, 4..7 consumable slots.</summary>
        public int ActiveSlot { get; private set; }

        /// <summary>Slot 0..7 (weapons first); empty when out of range.</summary>
        public InventorySlot GetSlot(int index) => index >= 0 && index < TotalSlotCount ? _slots[index] : default;

        public InventoryChange SetWeaponSlot(int index, in InventorySlot slot) =>
            index >= 0 && index < WeaponSlotCount ? Set(index, slot) : InventoryChange.None;

        public InventoryChange SetConsumableSlot(int index, in InventorySlot slot) =>
            index >= 0 && index < ConsumableSlotCount ? Set(WeaponSlotCount + index, slot) : InventoryChange.None;

        public InventoryChange SetActiveSlot(int index)
        {
            if (index < 0 || index >= TotalSlotCount || index == ActiveSlot) return InventoryChange.None;
            ActiveSlot = index;
            return InventoryChange.ActiveSlot;
        }

        /// <summary>G-7: <paramref name="firstWeapon"/> in slot 1 and selected, every other slot empty.</summary>
        public InventoryChange Reset(in InventorySlot firstWeapon)
        {
            for (var i = 0; i < TotalSlotCount; i++) _slots[i] = default;
            _slots[0] = firstWeapon;
            ActiveSlot = 0;
            return InventoryChange.All;
        }

        private InventoryChange Set(int index, in InventorySlot slot)
        {
            if (slot.Equals(_slots[index])) return InventoryChange.None;
            _slots[index] = slot;
            return InventoryChange.Slots;
        }
    }
}
