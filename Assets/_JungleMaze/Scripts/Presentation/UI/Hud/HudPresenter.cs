using System;
using JungleMaze.App.Hud;
using JungleMaze.App.Hud.State;
using JungleMaze.Domain.Hud;

namespace JungleMaze.Presentation.UI.Hud
{
    /// <summary>
    /// HUD presenter (MVP), plain C#. Bound only to the three read ports (HUD, inventory, effects): it knows no event
    /// bus, config or writers. Reacts to their <c>Changed</c> events, interpolates bars and pushes display values to
    /// the four views. It only clamps values for display; it never computes HP, ammo, durability, the zone clock or
    /// effect expiry. Numbers go through preallocated char buffers, so ticking without changes allocates nothing (AC19).
    /// </summary>
    public sealed class HudPresenter : IDisposable
    {
        public const string ArmorEmptyLabel = "Giáp";
        public const string HelmetEmptyLabel = "Mũ";
        public const string AdrenalineLabel = "Adrenaline";

        private readonly IHudState _hud;
        private readonly IInventoryState _inventory;
        private readonly IEffectState _effects;
        private readonly HudTuning _tuning;
        private readonly IVitalsView _vitals;
        private readonly IQuickbarView _quickbar;
        private readonly IMinimapView _minimap;
        private readonly IMatchStatusView _match;
        private readonly Action<HudStateChange> _onHud;
        private readonly Action<InventoryChange> _onInventory;
        private readonly Action<EffectChange> _onEffect;
        private readonly MinimapProjection _projection;

        private readonly SmoothedBar _hpBar, _armorBar, _helmetBar;
        private readonly DamageTrail _trail;
        private readonly AdrenalineGauge _adrenaline = new AdrenalineGauge();

        private readonly char[] _hpText = new char[HudFormat.BufferSize];
        private readonly char[] _rowText = new char[HudFormat.BufferSize];
        private readonly char[] _magazineText = new char[HudFormat.BufferSize];
        private readonly char[] _reserveText = new char[HudFormat.BufferSize];
        private readonly char[] _countText = new char[HudFormat.BufferSize];
        private readonly char[] _matchText = new char[HudFormat.BufferSize];

        private int _maxHp;
        private bool _snap, _adrenalineShownActive, _adrenalineShownTimed, _markerVisible, _markerKnown, _disposed;
        private int _adrenalineShownSeconds = -1;
        private float _adrenalineShownFill = -1f;
        private float _markerX, _markerY, _markerHeading;

        public HudPresenter(IHudState hud, IInventoryState inventory, IEffectState effects, HudTuning tuning,
            IVitalsView vitals, IQuickbarView quickbar, IMinimapView minimap, IMatchStatusView matchStatus)
        {
            _hud = hud ?? throw new ArgumentNullException(nameof(hud));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _effects = effects ?? throw new ArgumentNullException(nameof(effects));
            _vitals = vitals ?? throw new ArgumentNullException(nameof(vitals));
            _quickbar = quickbar ?? throw new ArgumentNullException(nameof(quickbar));
            _minimap = minimap ?? throw new ArgumentNullException(nameof(minimap));
            _match = matchStatus ?? throw new ArgumentNullException(nameof(matchStatus));
            _tuning = tuning;
            _projection = new MinimapProjection(_hud.MapSizeCells, _minimap.SizePixels);
            _hpBar = new SmoothedBar(tuning.BarTweenSeconds);
            _armorBar = new SmoothedBar(tuning.BarTweenSeconds);
            _helmetBar = new SmoothedBar(tuning.BarTweenSeconds);
            _trail = new DamageTrail(tuning.TrailHoldSeconds, tuning.TrailRetractSeconds);
            _onHud = ApplyHud;
            _onInventory = ApplyInventory;
            _onEffect = OnEffectChanged;

            // First draw snaps: no tween from an arbitrary prefab value.
            _snap = true;
            ApplyHud(HudStateChange.All);
            ApplyInventory(InventoryChange.All);
            _snap = false;
            PushAdrenaline(true);
            TickMinimap();

            _hud.Changed += _onHud;
            _inventory.Changed += _onInventory;
            _effects.Changed += _onEffect;
        }

        public int MaxHp => _maxHp;

        /// <summary>Advances bar tweens and the damage trail, redraws the adrenaline row and follows the Hero pose.</summary>
        public void Tick(float deltaSeconds)
        {
            if (_disposed) return;
            var hpChanged = _hpBar.Tick(deltaSeconds);
            if (_trail.Tick(deltaSeconds)) hpChanged = true;
            if (hpChanged) PushHpBar();
            if (_armorBar.Tick(deltaSeconds)) _vitals.SetRowFill(VitalsRow.Armor, _armorBar.Value);
            if (_helmetBar.Tick(deltaSeconds)) _vitals.SetRowFill(VitalsRow.Helmet, _helmetBar.Value);
            PushAdrenaline(false);
            TickMinimap();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _hud.Changed -= _onHud;
            _inventory.Changed -= _onInventory;
            _effects.Changed -= _onEffect;
        }

        private void ApplyHud(HudStateChange changes)
        {
            if ((changes & (HudStateChange.Hp | HudStateChange.MaxHp)) != 0) ApplyHp((changes & HudStateChange.MaxHp) != 0);
            if ((changes & HudStateChange.Armor) != 0) ApplyGear(VitalsRow.Armor, _hud.Armor, ArmorEmptyLabel, _armorBar);
            if ((changes & HudStateChange.Helmet) != 0) ApplyGear(VitalsRow.Helmet, _hud.Helmet, HelmetEmptyLabel, _helmetBar);
            if ((changes & (HudStateChange.HeldWeapon | HudStateChange.Ammo)) != 0) ApplyWeaponStrip();
            if ((changes & HudStateChange.ZoneCountdown) != 0) ApplyZone();
            if ((changes & HudStateChange.AliveCount) != 0) ApplyAlive();
        }

        private void ApplyInventory(InventoryChange changes)
        {
            if (changes == InventoryChange.None) return;
            var active = _inventory.ActiveSlot;
            var weaponSlots = _inventory.WeaponSlotCount;
            var total = weaponSlots + _inventory.ConsumableSlotCount;
            for (var i = 0; i < total; i++)
            {
                var slot = _inventory.GetSlot(i);
                // The selected slot stays Selected even when empty: the amber frame shows where the player is.
                var visual = i == active ? SlotVisual.Selected : slot.IsEmpty ? SlotVisual.Empty : SlotVisual.Default;
                var countLength = !slot.IsEmpty && i >= weaponSlots ? HudFormat.Count(_countText, slot.Count) : 0;
                _quickbar.SetSlot(i, slot.ItemId, slot.DisplayName ?? "", _countText, countLength, visual);
            }
        }

        private void OnEffectChanged(EffectChange change)
        {
            if (change.Kind == EffectKind.Adrenaline) PushAdrenaline(true);
        }

        private void ApplyHp(bool maxChanged)
        {
            var max = _hud.MaxHp > 0 ? _hud.MaxHp : 1;
            if (maxChanged || max != _maxHp)
            {
                _maxHp = max;
                _vitals.SetHpMax(_hpText, HudFormat.SlashInteger(_hpText, max));
            }
            var hp = Clamp(_hud.Hp, 0, max);
            var low = _tuning.LowHpRatio > 0f && hp <= _tuning.LowHpRatio * max;
            _vitals.SetHpNumber(_hpText, HudFormat.Integer(_hpText, hp), low);
            var target = hp / (float)max;
            if (_snap)
            {
                _hpBar.Snap(target);
                _trail.Snap(target);
            }
            else
            {
                var shownBefore = _hpBar.Value;
                _hpBar.SetTarget(target);
                if (_tuning.DamageTrailEnabled) _trail.OnTargetChanged(shownBefore, target);
                else _trail.Snap(target);
            }
            PushHpBar();
        }

        private void PushHpBar()
        {
            var fill = _hpBar.Value;
            var trail = _trail.IsActive && _trail.Value > fill ? _trail.Value : fill;
            _vitals.SetHpBar(fill, trail);
        }

        private void ApplyGear(VitalsRow row, GearStatus gear, string emptyLabel, SmoothedBar bar)
        {
            float fill;
            if (gear.IsWorn)
            {
                var max = gear.MaxDurability;
                var durability = gear.Durability < 0f ? 0f : gear.Durability;
                if (max > 0f && durability > max) durability = max;
                fill = max > 0f ? durability / max : 0f;
                _vitals.SetRowLabel(row, gear.DisplayName ?? gear.ItemId, TextTone.Normal);
                _vitals.SetRowValue(row, _rowText, HudFormat.Integer(_rowText, HudFormat.CeilToInt(durability)), TextTone.Normal);
            }
            else
            {
                fill = 0f;
                _vitals.SetRowLabel(row, emptyLabel, TextTone.Faint);
                _vitals.SetRowValue(row, _rowText, 0, TextTone.Faint);
            }
            if (_snap) bar.Snap(fill);
            else bar.SetTarget(fill);
            _vitals.SetRowFill(row, bar.Value);
        }

        /// <summary>Reads the Adrenaline effect every frame; the store owns the countdown and expiry.</summary>
        private void PushAdrenaline(bool force)
        {
            var active = _effects.TryGet(EffectKind.Adrenaline, out var effect);
            var timed = active && effect.IsTimed;
            if (timed) _adrenaline.Set(effect.DurationSeconds, effect.RemainingSeconds);
            var fill = !active ? 0f : timed ? _adrenaline.Fill : 1f;

            if (force || active != _adrenalineShownActive)
                _vitals.SetRowLabel(VitalsRow.Adrenaline, AdrenalineLabel, active ? TextTone.Highlight : TextTone.Faint);
            if (timed)
            {
                // G-13: "0s" only when the effect is over; a sliver of time left still reads "1s".
                var shownSeconds = effect.RemainingSeconds > 1f ? effect.RemainingSeconds : 1f;
                var seconds = HudFormat.CeilToInt(shownSeconds);
                if (force || !_adrenalineShownTimed || seconds != _adrenalineShownSeconds)
                {
                    _vitals.SetRowValue(VitalsRow.Adrenaline, _rowText,
                        HudFormat.Seconds(_rowText, shownSeconds), TextTone.Highlight);
                    _adrenalineShownSeconds = seconds;
                }
            }
            else if (force || _adrenalineShownTimed)
            {
                _vitals.SetRowValue(VitalsRow.Adrenaline, _rowText, 0, TextTone.Faint);
                _adrenalineShownSeconds = -1;
            }
            _adrenalineShownActive = active;
            _adrenalineShownTimed = timed;
            if (force || fill != _adrenalineShownFill)
            {
                _adrenalineShownFill = fill;
                _vitals.SetRowFill(VitalsRow.Adrenaline, fill);
            }
        }

        private void ApplyWeaponStrip()
        {
            var held = _hud.HeldWeapon;
            _quickbar.SetWeaponName(held.DisplayName ?? held.ItemId ?? "");
            var weapon = held.Definition;
            if (!WeaponStripRules.ShowAmmo(weapon))
            {
                _quickbar.SetAmmo(false, _magazineText, 0, _reserveText, 0);
                _quickbar.SetReloadHint(false);
                return;
            }
            var ammo = _hud.Ammo;
            var magazine = Clamp(ammo.InMagazine, 0, weapon.Stats.MagazineSize.Value);
            var reserve = ammo.Reserve < 0 ? 0 : ammo.Reserve;
            _quickbar.SetAmmo(true, _magazineText, HudFormat.Integer(_magazineText, magazine),
                _reserveText, HudFormat.SlashSpaceInteger(_reserveText, reserve));
            _quickbar.SetReloadHint(WeaponStripRules.ShowReloadHint(weapon, magazine, reserve));
        }

        private void ApplyZone()
        {
            var info = _hud.MatchStatus;
            var length = info.HasZoneCountdown
                ? HudFormat.MinutesSeconds(_matchText, info.ZoneSecondsRemaining)
                : HudFormat.ClockPlaceholder(_matchText);
            _match.SetZoneCountdown(_matchText, length);
        }

        private void ApplyAlive()
        {
            var info = _hud.MatchStatus;
            int length;
            if (info.HasAlive)
            {
                var total = info.Total < 0 ? 0 : info.Total;
                length = HudFormat.Fraction(_matchText, Clamp(info.Alive, 0, total), total);
            }
            else length = HudFormat.FractionPlaceholder(_matchText);
            _match.SetAlive(_matchText, length);
        }

        private void TickMinimap()
        {
            if (!_hud.HasPose)
            {
                if (_markerVisible || !_markerKnown)
                {
                    _markerVisible = false;
                    _markerKnown = true;
                    _minimap.SetMarker(false, 0f, 0f, 0f);
                }
                return;
            }
            var pose = _hud.Pose;
            _projection.Project(pose.X, pose.Y, out var x, out var y);
            var heading = MinimapProjection.HeadingDegrees(pose.RotationDegrees);
            if (_markerVisible && x == _markerX && y == _markerY && heading == _markerHeading) return;
            _markerVisible = true;
            _markerKnown = true;
            _markerX = x;
            _markerY = y;
            _markerHeading = heading;
            _minimap.SetMarker(true, x, y, heading);
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }
}
