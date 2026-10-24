using System;
using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using JungleMaze.Core.Events;
using JungleMaze.Core.Logging;
using JungleMaze.Domain.Hud;
using JungleMaze.Domain.Network;

namespace JungleMaze.App.Hud.State
{
    /// <summary>
    /// Default HUD state: holds what data owners write and raises <see cref="Changed"/> only on real changes.
    /// Names, durability maxima and weapon definitions are resolved from balance JSON at write time, so the HUD
    /// never needs the config provider. Resets to G-7 on disconnect.
    /// </summary>
    public sealed class HudStateStore : IHudState, IHudStateWriter, IInitializable, IDisposable
    {
        private readonly HudStatus _status = new HudStatus();
        private readonly IEventBus _bus;
        private readonly IBalanceConfigProvider _balance;
        private readonly HudItemLabels _labels;
        private readonly Action<NetworkStateChanged> _onNetwork;

        public HudStateStore(IEventBus bus, IBalanceConfigProvider balance, ILog log = null)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _labels = new HudItemLabels(balance, log);
            _onNetwork = OnNetworkStateChanged;
            if (!balance.TryGetWeapon(HudStateDefaults.BareHandsId, out _))
                (log ?? NullLog.Instance).Warn($"[HUD] weapons.json has no '{HudStateDefaults.BareHandsId}'; the held weapon shows the raw id.");
            _status.Reset(balance.Match.HeroMaxHp, BareHands());
        }

        public event Action<HudStateChange> Changed;

        public int Hp => _status.Hp;
        public int MaxHp => _status.MaxHp;
        public GearStatus Armor => _status.Armor;
        public GearStatus Helmet => _status.Helmet;
        public HeldWeaponStatus HeldWeapon => _status.HeldWeapon;
        public AmmoStatus Ammo => _status.Ammo;
        public MatchStatusInfo MatchStatus => _status.MatchStatus;
        public int MapSizeCells => _balance.Match.MapSizeCells;
        public bool HasPose => _status.HasPose;
        public HeroPose Pose => _status.Pose;

        public void Initialize() => _bus.Subscribe(_onNetwork);
        public void Dispose() => _bus.Unsubscribe(_onNetwork);

        public void SetHp(int hp) => Raise(_status.SetHp(hp));
        public void SetArmor(string equipmentId, float durability) => Raise(_status.SetArmor(Gear(equipmentId, durability)));
        public void SetHelmet(string equipmentId, float durability) => Raise(_status.SetHelmet(Gear(equipmentId, durability)));

        public void SetHeldWeapon(string weaponId) =>
            Raise(_status.SetHeldWeapon(string.IsNullOrEmpty(weaponId) ? BareHands() : Weapon(weaponId)));

        public void SetAmmo(int inMagazine, int reserve) => Raise(_status.SetAmmo(inMagazine, reserve));
        public void SetZoneCountdown(float secondsRemaining) => Raise(_status.SetZoneCountdown(secondsRemaining));
        public void SetAliveCount(int alive, int total) => Raise(_status.SetAliveCount(alive, total));
        public void SetPose(float x, float y, float rotationDegrees) => _status.SetPose(x, y, rotationDegrees);
        public void Reset() => Raise(_status.Reset(_balance.Match.HeroMaxHp, BareHands()));

        private GearStatus Gear(string equipmentId, float durability) => string.IsNullOrEmpty(equipmentId)
            ? default
            : new GearStatus(equipmentId, _labels.DisplayName(equipmentId), durability, _labels.MaxDurability(equipmentId));

        private HeldWeaponStatus Weapon(string weaponId) =>
            new HeldWeaponStatus(weaponId, _labels.DisplayName(weaponId), _labels.Weapon(weaponId));

        private HeldWeaponStatus BareHands() => Weapon(HudStateDefaults.BareHandsId);

        private void Raise(HudStateChange change)
        {
            if (change != HudStateChange.None) Changed?.Invoke(change);
        }

        private void OnNetworkStateChanged(NetworkStateChanged change)
        {
            if (change.Current == NetworkState.Disconnected || change.Current == NetworkState.Failed) Reset();
        }
    }
}
