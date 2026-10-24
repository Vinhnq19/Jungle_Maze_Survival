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
    /// Default HUD state: holds what data owners write and raises <see cref="Changed"/> only on real changes.
    /// Names, durability maxima and weapon definitions are resolved from balance JSON at write time, so the HUD
    /// never needs the config provider. Resets to G-7 on disconnect.
    /// </summary>
    public sealed class HudStateStore : IHudState, IHudStateWriter, IInitializable, IDisposable, ITickable
    {
        private readonly HudStatus _status = new HudStatus();
        private readonly IEventBus _bus;
        private readonly IBalanceConfigProvider _balance;
        private readonly HudItemLabels _labels;
        private readonly Action<NetworkStateChanged> _onNetwork;
        private readonly DangerList _dangers = new DangerList();
        private readonly Action<int> _onDangerExpired;
        private readonly ILog _log;
        private bool _warnedCapacity;

        public HudStateStore(IEventBus bus, IBalanceConfigProvider balance, ILog log = null)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _labels = new HudItemLabels(balance, log);
            _onNetwork = OnNetworkStateChanged;
            _onDangerExpired = id => DangerChanged?.Invoke(new DangerChange(id, DangerChangeType.Expired));
            _log = log ?? NullLog.Instance;
            if (!balance.TryGetWeapon(HudStateDefaults.BareHandsId, out _))
                (log ?? NullLog.Instance).Warn($"[HUD] weapons.json has no '{HudStateDefaults.BareHandsId}'; the held weapon shows the raw id.");
            _status.Reset(balance.Match.HeroMaxHp, BareHands());
        }

        public event Action<HudStateChange> Changed;
        public event Action<ItemPickup> ItemPicked;
        public event Action<HitDealt> HitDealt;
        public event Action<DamageTaken> DamageTaken;
        public event Action<DangerChange> DangerChanged;
        public int DangerCount => _dangers.Count;
        public DangerWarning GetDangerAt(int index) => _dangers.GetAt(index);
        public void Tick(float dt) => _dangers.Tick(dt, _onDangerExpired);
        public void NotifyPickup(string itemId, int count)
        {
            if (!string.IsNullOrEmpty(itemId) && count > 0) ItemPicked?.Invoke(new ItemPickup(itemId, _labels.DisplayName(itemId), count));
        }
        public void NotifyHitDealt(HitTargetKind target) => HitDealt?.Invoke(new HitDealt(target));
        public void NotifyDamageTaken(bool hasDirection, float directionDegrees) => DamageTaken?.Invoke(new DamageTaken(hasDirection && !float.IsNaN(directionDegrees) && !float.IsInfinity(directionDegrees), directionDegrees));
        public void SetDanger(int id, DangerKind kind, float remainingSeconds, bool hasSource, float sourceX, float sourceY)
        {
            if (kind != DangerKind.FusedBomb && kind != DangerKind.Trap) return;
            if (kind == DangerKind.FusedBomb && (!(remainingSeconds > 0f) || float.IsInfinity(remainingSeconds))) return;
            hasSource &= !float.IsNaN(sourceX) && !float.IsNaN(sourceY) && !float.IsInfinity(sourceX) && !float.IsInfinity(sourceY);
            var total = kind == DangerKind.Trap ? 0f : remainingSeconds;
            if (_dangers.TryGet(id, out var old) && old.Kind == kind) total = Math.Max(total, old.TotalSeconds);
            var warning = new DangerWarning(id, kind, total, kind == DangerKind.Trap ? 0f : remainingSeconds, hasSource, sourceX, sourceY);
            if (_dangers.Apply(warning, out var change)) DangerChanged?.Invoke(new DangerChange(id, change));
            else if (!_warnedCapacity) { _warnedCapacity = true; _log.Warn("[HUD] Warning capacity reached; ignoring additional dangers."); }
        }
        public void ClearDanger(int id) { if (_dangers.Remove(id)) DangerChanged?.Invoke(new DangerChange(id, DangerChangeType.Removed)); }

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
        public void Reset()
        {
            while (_dangers.Count > 0) ClearDanger(_dangers.GetAt(_dangers.Count - 1).Id);
            _warnedCapacity = false;
            Raise(_status.Reset(_balance.Match.HeroMaxHp, BareHands()));
        }

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
