#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using JungleMaze.App.Hud.State;
using JungleMaze.App.Ports;
using JungleMaze.Domain.Balance;

namespace JungleMaze.App.Hud.Mocks
{
    /// <summary>
    /// Dev-only stand-in for the inventory owner (ITM-01) (G-8, AC16). Writes through <see cref="IInventoryStateWriter"/>.
    /// <see cref="CycleItem"/> steps a slot through every id in balance JSON (then empty) for quick visual checks.
    /// Not compiled into release builds.
    /// </summary>
    public sealed class MockInventoryState
    {
        public const int SlotCount = 8;
        private const int WeaponSlots = 4;
        private const int DefaultConsumableCount = 1;

        private readonly IInventoryStateWriter _writer;
        private readonly IBalanceConfigProvider _balance;
        private string _usingId;
        private readonly List<string> _weaponIds = new List<string>();
        private readonly List<string> _consumableIds = new List<string>();
        private readonly Dictionary<string, int> _maxCarry = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly string[] _ids = new string[SlotCount];
        private readonly int[] _counts = new int[SlotCount];

        public MockInventoryState(IInventoryStateWriter writer, IBalanceConfigProvider balance)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            _balance = balance;
            foreach (var weapon in balance.Weapons) _weaponIds.Add(weapon.Id);
            foreach (var equipment in balance.Equipments)
            {
                if (equipment.Type == EquipmentType.Helmet || equipment.Type == EquipmentType.Armor) continue;
                _consumableIds.Add(equipment.Id);
                _maxCarry[equipment.Id] = equipment.Stats.MaxCarry;
            }
            ResetLocalCopy();
        }

        public int ActiveSlot { get; private set; }
        public IReadOnlyList<string> WeaponIds => _weaponIds;
        public IReadOnlyList<string> ConsumableIds => _consumableIds;

        /// <summary>Item id this mock last put in slot 0..7 (null = empty).</summary>
        public string GetId(int slot) => slot >= 0 && slot < SlotCount ? _ids[slot] : null;
        public int GetCount(int slot) => slot >= 0 && slot < SlotCount ? _counts[slot] : 0;

        /// <summary>Slot 0..7: weapons for 0..3 (count ignored), consumables for 4..7.</summary>
        public void SetSlot(int slot, string itemId, int count = 1)
        {
            if (slot < 0 || slot >= SlotCount) return;
            var empty = string.IsNullOrEmpty(itemId) || count <= 0;
            _ids[slot] = empty ? null : itemId;
            _counts[slot] = empty ? 0 : slot < WeaponSlots ? 1 : count;
            if (slot < WeaponSlots) _writer.SetWeaponSlot(slot, _ids[slot]);
            else _writer.SetConsumableSlot(slot - WeaponSlots, _ids[slot], _counts[slot]);
        }

        /// <summary>Steps slot 0..7 to the next id of its group in JSON order, then to empty, then wraps.</summary>
        public void CycleItem(int slot)
        {
            if (slot < 0 || slot >= SlotCount) return;
            var ids = slot < WeaponSlots ? _weaponIds : _consumableIds;
            var next = ids.IndexOf(_ids[slot] ?? "") + 1;
            if (next >= ids.Count) { SetSlot(slot, null, 0); return; }
            var id = ids[next];
            SetSlot(slot, id, slot < WeaponSlots ? 1 : MaxCarry(id));
        }

        /// <summary>Changes the count of a consumable slot by <paramref name="delta"/>; 0 empties it.</summary>
        public void AddCount(int slot, int delta)
        {
            if (slot < WeaponSlots || slot >= SlotCount || _ids[slot] == null) return;
            SetSlot(slot, _ids[slot], Math.Max(0, _counts[slot] + delta));
        }

        public void Select(int slot)
        {
            if (slot < 0 || slot >= SlotCount) return;
            ActiveSlot = slot;
            _writer.SetActiveSlot(slot);
        }

        public void BeginUse(int slot,bool cancellable=true)
        {
            var id=GetId(slot);if(id==null||!_balance.TryGetEquipment(id,out var equipment)||equipment.Stats.UseSeconds<=0)return;
            _usingId=id;_writer.BeginUse(slot,id,equipment.Stats.UseSeconds,equipment.Stats.UseSeconds,cancellable);
        }
        public void CancelUse(){if(_usingId!=null)_writer.CancelUse(_usingId);_usingId=null;}
        public void CompleteUse(){if(_usingId!=null)_writer.CompleteUse(_usingId);_usingId=null;}

        public void Reset()
        {
            _usingId=null;
            ResetLocalCopy();
            _writer.Reset();
        }

        private int MaxCarry(string id) => _maxCarry.TryGetValue(id, out var carry) && carry > 0 ? carry : DefaultConsumableCount;

        private void ResetLocalCopy()
        {
            for (var i = 0; i < SlotCount; i++) { _ids[i] = null; _counts[i] = 0; }
            _ids[0] = HudStateDefaults.BareHandsId;
            _counts[0] = 1;
            ActiveSlot = 0;
        }
    }
}
#endif
