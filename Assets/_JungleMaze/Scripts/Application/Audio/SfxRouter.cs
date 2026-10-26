using System;
using System.Collections.Generic;
using JungleMaze.App.Flow;
using JungleMaze.App.Hud.State;
using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using JungleMaze.Core.Events;
using JungleMaze.Core.Ticking;
using JungleMaze.Domain.Audio;
using JungleMaze.Domain.Balance;
using JungleMaze.Domain.Hud;

namespace JungleMaze.App.Audio
{
    /// <summary>
    /// Maps game events that already exist (HUD/inventory ports, AppScreenChanged, every <see cref="IAudioEventSource"/>)
    /// to SFX ids (spec table 2.1, AC-A14). It only listens: it has no write port and does not know WPN/ITM/DRG code.
    /// Keyed cues are played once per key (<see cref="SfxDeduper"/>, 2 s); unkeyed HUD events with the same payload
    /// within 150 ms count as one (G-6). After entering a match or a store reset there is a settle window (T-3) in
    /// which gear and pickup changes only update the remembered state and make no sound (AC-A15).
    /// </summary>
    public sealed class SfxRouter : IInitializable, ITickable, IDisposable
    {
        public const float UnkeyedWindowSeconds = 0.15f;
        public const float ArmorAbsorbWindowSeconds = 0.1f;
        public const float DefaultSettleSeconds = 0.5f;
        private const int MaxBombs = 16;

        private enum HudKind : byte { Pickup = 1, Hit = 2, Damage = 3 }

        private readonly IAudioService _audio;
        private readonly IHudState _hud;
        private readonly IInventoryState _inventory;
        private readonly IEventBus _bus;
        private readonly IBalanceConfigProvider _balance;
        private readonly IReadOnlyList<IAudioEventSource> _sources;
        private readonly MusicPlan _music;
        private readonly float _settleSeconds;
        private readonly SfxDeduper _keyed = new SfxDeduper();
        private readonly SfxDeduper _unkeyed = new SfxDeduper(UnkeyedWindowSeconds, 32);

        private readonly Action<HudStateChange> _onHudChanged;
        private readonly Action<ItemPickup> _onPickup;
        private readonly Action<HitDealt> _onHit;
        private readonly Action<DamageTaken> _onDamage;
        private readonly Action<DangerChange> _onDanger;
        private readonly Action<ItemUseStatus> _onUse;
        private readonly Action<AppScreenChanged> _onScreen;
        private readonly Action<AudioCue> _onCue;

        private readonly int[] _bombIds = new int[MaxBombs];
        private readonly bool[] _bombUsed = new bool[MaxBombs];
        private readonly bool[] _bombHasSource = new bool[MaxBombs];
        private readonly float[] _bombX = new float[MaxBombs];
        private readonly float[] _bombY = new float[MaxBombs];

        private GearStatus _armor, _helmet;
        private float _now;
        private float _settleUntil = float.NegativeInfinity;
        private float _lastDamage = float.NegativeInfinity;
        private float _lastGearDrop = float.NegativeInfinity;
        private bool _initialized;

        public SfxRouter(IAudioService audio, IHudState hud, IInventoryState inventory, IEventBus bus,
            IBalanceConfigProvider balance, IReadOnlyList<IAudioEventSource> sources = null, MusicPlan music = null,
            float settleSeconds = DefaultSettleSeconds)
        {
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _hud = hud;
            _inventory = inventory;
            _bus = bus;
            _balance = balance;
            _sources = sources ?? Array.Empty<IAudioEventSource>();
            _music = music;
            _settleSeconds = settleSeconds;
            _onHudChanged = OnHudChanged;
            _onPickup = OnPickup;
            _onHit = OnHit;
            _onDamage = OnDamage;
            _onDanger = OnDanger;
            _onUse = OnUse;
            _onScreen = OnScreen;
            _onCue = OnCue;
        }

        public bool IsSettling => _now < _settleUntil;

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            if (_hud != null)
            {
                _armor = _hud.Armor;
                _helmet = _hud.Helmet;
                _hud.Changed += _onHudChanged;
                _hud.ItemPicked += _onPickup;
                _hud.HitDealt += _onHit;
                _hud.DamageTaken += _onDamage;
                _hud.DangerChanged += _onDanger;
            }
            if (_inventory != null) _inventory.UseChanged += _onUse;
            _bus?.Subscribe(_onScreen);
            for (var i = 0; i < _sources.Count; i++) if (_sources[i] != null) _sources[i].CueRaised += _onCue;
        }

        public void Dispose()
        {
            if (!_initialized) return;
            _initialized = false;
            if (_hud != null)
            {
                _hud.Changed -= _onHudChanged;
                _hud.ItemPicked -= _onPickup;
                _hud.HitDealt -= _onHit;
                _hud.DamageTaken -= _onDamage;
                _hud.DangerChanged -= _onDanger;
            }
            if (_inventory != null) _inventory.UseChanged -= _onUse;
            _bus?.Unsubscribe(_onScreen);
            for (var i = 0; i < _sources.Count; i++) if (_sources[i] != null) _sources[i].CueRaised -= _onCue;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime > 0f) _now += deltaTime;
        }

        private void OnCue(AudioCue cue)
        {
            if (cue.DedupeKey != 0 && _keyed.IsDuplicate(cue.DedupeKey, _now)) return;
            _audio.Play(cue);
        }

        private void OnPickup(ItemPickup pickup)
        {
            if (IsSettling || string.IsNullOrEmpty(pickup.ItemId)) return;
            var hash = pickup.ItemId.GetHashCode();
            if (IsRepeat(HudKind.Pickup, (uint)hash)) return;
            if (_balance != null && _balance.TryGetWeapon(pickup.ItemId, out _))
            {
                _audio.Play(AudioCue.Global(SfxId.PickupWeapon, hash));
                return;
            }
            // Helmet/armor pickups are heard as WearArmor when the worn piece changes.
            if (_balance != null && _balance.TryGetEquipment(pickup.ItemId, out var equipment)
                && (equipment.Type == EquipmentType.Helmet || equipment.Type == EquipmentType.Armor)) return;
            _audio.Play(AudioCue.Global(SfxId.PickupItem, hash));
        }

        private void OnHit(HitDealt hit)
        {
            if (hit.Target != HitTargetKind.Hero || IsRepeat(HudKind.Hit, 0)) return;
            _audio.Play(AudioCue.Global(SfxId.HitMarker));
        }

        private void OnDamage(DamageTaken damage)
        {
            if (IsRepeat(HudKind.Damage, 0)) return;
            _audio.Play(AudioCue.Global(SfxId.HurtLocal));
            if (!IsSettling && _now - _lastGearDrop <= ArmorAbsorbWindowSeconds)
            {
                _lastGearDrop = float.NegativeInfinity;
                _audio.Play(AudioCue.Global(SfxId.ArmorAbsorb));
                return;
            }
            _lastDamage = _now;
        }

        private void OnHudChanged(HudStateChange change)
        {
            if (_hud == null) return;
            // A store reset raises every flag at once: remember the cleared gear silently and settle.
            if (change == HudStateChange.All)
            {
                _armor = _hud.Armor;
                _helmet = _hud.Helmet;
                _settleUntil = _now + _settleSeconds;
                return;
            }
            if ((change & HudStateChange.Armor) != 0) OnGear(ref _armor, _hud.Armor);
            if ((change & HudStateChange.Helmet) != 0) OnGear(ref _helmet, _hud.Helmet);
        }

        private void OnGear(ref GearStatus remembered, GearStatus current)
        {
            var old = remembered;
            remembered = current;
            if (IsSettling) return;
            if (current.IsWorn && current.ItemId != old.ItemId)
            {
                _audio.Play(AudioCue.Global(SfxId.WearArmor));
                return;
            }
            if (!current.IsWorn || current.ItemId != old.ItemId || !(current.Durability < old.Durability)) return;
            if (_now - _lastDamage <= ArmorAbsorbWindowSeconds)
            {
                _lastDamage = float.NegativeInfinity;
                _audio.Play(AudioCue.Global(SfxId.ArmorAbsorb));
            }
            else _lastGearDrop = _now;
        }

        private void OnDanger(DangerChange change)
        {
            switch (change.Type)
            {
                case DangerChangeType.Added:
                    if (!TryFindDanger(change.Id, out var warning) || warning.Kind != DangerKind.FusedBomb) return;
                    var slot = RememberBomb(change.Id, warning);
                    PlayAtBomb(SfxId.BombFuse, slot, change.Id);
                    return;
                case DangerChangeType.Expired:
                    var expired = FindBomb(change.Id);
                    if (expired < 0) return;
                    PlayAtBomb(SfxId.Explosion, expired, change.Id);
                    _bombUsed[expired] = false;
                    return;
                case DangerChangeType.Removed:
                    var removed = FindBomb(change.Id);
                    if (removed >= 0) _bombUsed[removed] = false;
                    return;
            }
        }

        private void OnUse(ItemUseStatus use)
        {
            if (use.Phase == ItemUsePhase.Cancelled)
            {
                _audio.Play(AudioCue.Global(SfxId.UseCancel));
                return;
            }
            if (use.Phase != ItemUsePhase.Using || _balance == null
                || !_balance.TryGetEquipment(use.ItemId ?? string.Empty, out var item)) return;
            if (item.Type == EquipmentType.Healing) _audio.Play(AudioCue.Global(SfxId.UseHeal));
            else if (item.Type == EquipmentType.Support || item.Type == EquipmentType.Airdrop) _audio.Play(AudioCue.Global(SfxId.UseDrink));
        }

        private void OnScreen(AppScreenChanged change)
        {
            _music?.SetScreen(change.Current);
            if (change.Current == AppScreen.InMatch && change.Previous != AppScreen.InMatch)
            {
                if (_hud != null) { _armor = _hud.Armor; _helmet = _hud.Helmet; }
                _settleUntil = _now + _settleSeconds;
            }
            if (change.Current == AppScreen.Results && change.Previous != AppScreen.Results)
                _audio.Play(AudioCue.Global(SfxId.MatchEnd));
        }

        private bool IsRepeat(HudKind kind, uint payload) => _unkeyed.IsDuplicate(SfxDeduper.Key((byte)kind, payload), _now);

        private bool TryFindDanger(int id, out DangerWarning warning)
        {
            for (var i = 0; i < _hud.DangerCount; i++)
            {
                warning = _hud.GetDangerAt(i);
                if (warning.Id == id) return true;
            }
            warning = default;
            return false;
        }

        private int RememberBomb(int id, in DangerWarning warning)
        {
            var slot = FindBomb(id);
            for (var i = 0; slot < 0 && i < MaxBombs; i++) if (!_bombUsed[i]) slot = i;
            if (slot < 0) slot = 0;
            _bombUsed[slot] = true;
            _bombIds[slot] = id;
            _bombHasSource[slot] = warning.HasSource;
            _bombX[slot] = warning.SourceX;
            _bombY[slot] = warning.SourceY;
            return slot;
        }

        private int FindBomb(int id)
        {
            for (var i = 0; i < MaxBombs; i++) if (_bombUsed[i] && _bombIds[i] == id) return i;
            return -1;
        }

        private void PlayAtBomb(SfxId id, int slot, int dangerId)
        {
            var source = unchecked(dangerId + 1);
            _audio.Play(_bombHasSource[slot]
                ? AudioCue.At(id, _bombX[slot], _bombY[slot], source)
                : AudioCue.Global(id, source));
        }
    }
}
