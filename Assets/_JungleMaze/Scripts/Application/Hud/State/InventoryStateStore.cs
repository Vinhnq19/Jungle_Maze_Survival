using System;
using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using JungleMaze.Core.Events;
using JungleMaze.Core.Logging;
using JungleMaze.Core.Ticking;
using JungleMaze.Domain.Hud;
using JungleMaze.Domain.Network;

namespace JungleMaze.App.Hud.State
{
    /// <summary>
    /// Default inventory state: 4 + 4 quick-bar slots and the selected slot, display names resolved at write time.
    /// Raises <see cref="Changed"/> only on real changes; resets to G-7 (slot 1 = HAND, selected) on disconnect.
    /// </summary>
    public sealed class InventoryStateStore : IInventoryState, IInventoryStateWriter, IInitializable, IDisposable, ITickable
    {
        private readonly InventorySlots _slots = new InventorySlots();
        private readonly IEventBus _bus;
        private readonly IBalanceConfigProvider _balance;
        private readonly HudItemLabels _labels;
        private readonly Action<NetworkStateChanged> _onNetwork;
        private readonly ItemUseTracker _use = new ItemUseTracker();

        public InventoryStateStore(IEventBus bus, IBalanceConfigProvider balance, ILog log = null)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _labels = new HudItemLabels(balance, log);
            _onNetwork = OnNetworkStateChanged;
            _slots.Reset(FirstWeapon());
        }

        public event Action<InventoryChange> Changed;
        public event Action<ItemUseStatus> UseChanged;
        public ItemUseStatus Use => _use.Status;
        public void Tick(float dt) => _use.Tick(dt);
        public void BeginUse(int slotIndex, string itemId, float totalSeconds, float remainingSeconds, bool cancellable)
        {
            if (_use.Begin(new ItemUseStatus(ItemUsePhase.Using, itemId, string.IsNullOrEmpty(itemId) ? null : _labels.DisplayName(itemId), slotIndex, totalSeconds, remainingSeconds, cancellable))) UseChanged?.Invoke(Use);
        }
        public void CancelUse(string itemId) { if (_use.Cancel(itemId)) UseChanged?.Invoke(Use); }
        public void CompleteUse(string itemId) { if (_use.Complete(itemId)) UseChanged?.Invoke(Use); }

        public int WeaponSlotCount => InventorySlots.WeaponSlotCount;
        public int ConsumableSlotCount => InventorySlots.ConsumableSlotCount;
        public int ActiveSlot => _slots.ActiveSlot;
        public InventorySlot GetSlot(int index) => _slots.GetSlot(index);

        public void Initialize() => _bus.Subscribe(_onNetwork);
        public void Dispose() => _bus.Unsubscribe(_onNetwork);

        public void SetWeaponSlot(int index, string weaponId) => Raise(_slots.SetWeaponSlot(index, Slot(weaponId, 1)));

        public void SetConsumableSlot(int index, string equipmentId, int count) =>
            Raise(_slots.SetConsumableSlot(index, Slot(equipmentId, count)));

        public void SetActiveSlot(int index) => Raise(_slots.SetActiveSlot(index));
        public void Reset()
        {
            if (_use.Reset()) UseChanged?.Invoke(Use);
            Raise(_slots.Reset(FirstWeapon()));
        }

        private InventorySlot Slot(string itemId, int count) =>
            string.IsNullOrEmpty(itemId) || count <= 0 ? default : new InventorySlot(itemId, _labels.DisplayName(itemId), count);

        private InventorySlot FirstWeapon() =>
            _balance.TryGetWeapon(HudStateDefaults.BareHandsId, out _) ? Slot(HudStateDefaults.BareHandsId, 1) : default;

        private void Raise(InventoryChange change)
        {
            if (change != InventoryChange.None) Changed?.Invoke(change);
        }

        private void OnNetworkStateChanged(NetworkStateChanged change)
        {
            if (change.Current == NetworkState.Disconnected || change.Current == NetworkState.Failed) Reset();
        }
    }
}
