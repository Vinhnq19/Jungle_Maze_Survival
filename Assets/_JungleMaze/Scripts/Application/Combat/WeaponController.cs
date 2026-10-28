using System;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Hud.State;
using JungleMaze.App.Ports;
using JungleMaze.Domain.Balance;

namespace JungleMaze.App.Combat
{
    /// <summary>What a <see cref="WeaponController"/> reports to presentation. Struct payload, no allocation.</summary>
    public enum CombatEventKind : byte
    {
        AttackPredicted = 0,
        AttackConfirmed = 1,
        AttackRejected = 2,
        ReloadRequested = 3,
        ReloadStarted = 4,
        ReloadFinished = 5,
        ReloadRejected = 6
    }

    public readonly struct CombatEvent
    {
        public CombatEvent(CombatEventKind kind, CombatWeaponId weapon, ushort requestSeq, float aimDegrees, CombatOutcome outcome)
        {
            Kind = kind;
            Weapon = weapon;
            RequestSeq = requestSeq;
            AimDegrees = aimDegrees;
            Outcome = outcome;
        }

        public CombatEventKind Kind { get; }
        public CombatWeaponId Weapon { get; }
        public ushort RequestSeq { get; }
        public float AimDegrees { get; }
        /// <summary>Set for rejections and sync results; <see cref="CombatOutcome.Accepted"/> otherwise.</summary>
        public CombatOutcome Outcome { get; }
    }

    /// <summary>
    /// Client-side weapon input and state for WPN-01: selection, aim, cadence, magazine and reload timing.
    /// Attack cadence and the magazine are predicted locally and rolled back if the server rejects the request.
    /// A reload only starts after the server accepts it. Damage, HP, stun and loot are never decided here.
    /// </summary>
    public sealed class WeaponController
    {
        private const int MaxPending = 32;

        private struct PendingAttack
        {
            public ushort Seq;
            public CombatWeaponId Weapon;
            public bool SpentAmmo;
        }

        private readonly ICombatRequestSink _sink;
        private readonly IHudStateWriter _hud;
        private readonly CombatWeaponSlot[] _slots = new CombatWeaponSlot[CombatWeaponIds.Count];
        private readonly PendingAttack[] _pending = new PendingAttack[MaxPending];
        private int _pendingCount;
        private ushort _nextSeq = 1;
        private CombatWeaponId _selected = CombatWeaponId.Hand;

        private bool _reloadRequested;
        private ushort _reloadSeq;
        private CombatWeaponId _reloadWeapon;
        private bool _reloading;
        private float _reloadRemaining;

        public WeaponController(IBalanceConfigProvider balance, ICombatRequestSink sink, IHudStateWriter hud)
        {
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _hud = hud ?? throw new ArgumentNullException(nameof(hud));
            for (var i = 0; i < CombatWeaponIds.Count; i++)
            {
                var weapon = (CombatWeaponId)i;
                balance.TryGetWeapon(CombatWeaponIds.ToJsonId(weapon), out var definition);
                _slots[i] = new CombatWeaponSlot(weapon, definition);
            }
            PublishSelection();
        }

        /// <summary>Raised synchronously on the main thread.</summary>
        public event Action<CombatEvent> Changed;

        public CombatWeaponId Selected => _selected;
        public bool IsReloading => _reloading;
        public bool IsReloadPending => _reloadRequested;
        public float ReloadRemainingSeconds => _reloadRemaining;
        public int PendingCount => _pendingCount;
        public CombatWeaponSlot Slot(CombatWeaponId weapon) => _slots[(int)weapon];
        public CombatWeaponSlot SelectedSlot => _slots[(int)_selected];

        /// <summary>Returns false if the weapon is unknown to balance or not one of the client weapons.</summary>
        public bool Select(CombatWeaponId weapon)
        {
            if (!CombatWeaponIds.IsKnown(weapon) || !_slots[(int)weapon].Available) return false;
            if (_selected == weapon) return true;
            _selected = weapon;
            PublishSelection();
            return true;
        }

        /// <summary>Sends an attack with the selected weapon. Returns false if cadence, magazine, reload or queue block it.</summary>
        public bool TryAttack(float aimDegrees)
        {
            var slot = SelectedSlot;
            if (!slot.Available || _reloading || _reloadRequested || slot.CooldownRemaining > 0f) return false;
            if (slot.IsRanged && slot.Magazine <= 0) return false;
            if (_pendingCount >= MaxPending) return false;

            var seq = NextSeq();
            var aim = CombatShotMath.EncodeAim(aimDegrees);
            _sink.SendAttack(new AttackRequestPacket { RequestSeq = seq, Weapon = _selected, Aim = aim });

            var spent = slot.IsRanged;
            slot.CooldownRemaining = slot.CadenceSeconds;
            if (spent) slot.Magazine--;
            _pending[_pendingCount++] = new PendingAttack { Seq = seq, Weapon = _selected, SpentAmmo = spent };
            PublishAmmoIfSelected(slot);
            Raise(CombatEventKind.AttackPredicted, _selected, seq, CombatShotMath.DecodeAim(aim), CombatOutcome.Accepted);
            return true;
        }

        /// <summary>Sends a reload for the selected ranged weapon. The reload timer starts only after the server accepts it.</summary>
        public bool TryReload()
        {
            var slot = SelectedSlot;
            if (!slot.Available || !slot.IsRanged || _reloading || _reloadRequested) return false;
            if (slot.Magazine >= slot.MagazineSize || slot.Reserve <= 0) return false;

            var seq = NextSeq();
            _sink.SendReload(new ReloadRequestPacket { RequestSeq = seq, Weapon = _selected });
            _reloadRequested = true;
            _reloadSeq = seq;
            _reloadWeapon = _selected;
            Raise(CombatEventKind.ReloadRequested, _selected, seq, 0f, CombatOutcome.Accepted);
            return true;
        }

        /// <summary>Applies a server answer to an attack. Unknown or already-handled sequence numbers are ignored.</summary>
        public void OnAttackResult(AttackResultPacket result)
        {
            var index = FindPending(result.RequestSeq);
            if (index < 0) return;
            var pending = _pending[index];
            RemovePendingAt(index);
            if (!CombatWeaponIds.IsKnown(pending.Weapon)) return;

            var slot = _slots[(int)pending.Weapon];
            if (result.Outcome == CombatOutcome.Accepted)
            {
                Raise(CombatEventKind.AttackConfirmed, pending.Weapon, result.RequestSeq, 0f, CombatOutcome.Accepted);
            }
            else
            {
                if (pending.SpentAmmo && slot.MagazineSize > 0 && slot.Magazine < slot.MagazineSize) slot.Magazine++;
                Raise(CombatEventKind.AttackRejected, pending.Weapon, result.RequestSeq, 0f, result.Outcome);
            }
            if (slot.IsRanged && !HasPendingFor(pending.Weapon)) SyncMagazine(slot, result.Magazine);
            PublishAmmoIfSelected(slot);
        }

        /// <summary>Applies a server answer to the reload request. Stale sequence numbers are ignored.</summary>
        public void OnReloadResult(ReloadResultPacket result)
        {
            if (!_reloadRequested || result.RequestSeq != _reloadSeq || result.Weapon != _reloadWeapon) return;
            _reloadRequested = false;
            var slot = _slots[(int)_reloadWeapon];
            if (!HasPendingFor(_reloadWeapon)) SyncMagazine(slot, result.Magazine);

            if (result.Outcome == CombatOutcome.Accepted)
            {
                _reloading = true;
                _reloadRemaining = slot.ReloadSeconds;
                Raise(CombatEventKind.ReloadStarted, _reloadWeapon, result.RequestSeq, 0f, CombatOutcome.Accepted);
            }
            else
            {
                Raise(CombatEventKind.ReloadRejected, _reloadWeapon, result.RequestSeq, 0f, result.Outcome);
            }
            PublishAmmoIfSelected(slot);
        }

        /// <summary>Advances cooldowns and the reload timer. Completing a reload fills the magazine from reserve.</summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            for (var i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot.CooldownRemaining > 0f) slot.CooldownRemaining = Math.Max(0f, slot.CooldownRemaining - deltaTime);
            }
            if (!_reloading) return;

            _reloadRemaining -= deltaTime;
            if (_reloadRemaining > 0f) return;

            _reloading = false;
            _reloadRemaining = 0f;
            var slot = _slots[(int)_reloadWeapon];
            var take = Math.Min(slot.MagazineSize - slot.Magazine, slot.Reserve);
            if (take > 0)
            {
                slot.Magazine += take;
                slot.Reserve -= take;
            }
            Raise(CombatEventKind.ReloadFinished, _reloadWeapon, _reloadSeq, 0f, CombatOutcome.Accepted);
            PublishAmmoIfSelected(slot);
        }

        private void SyncMagazine(CombatWeaponSlot slot, byte serverMagazine)
        {
            slot.Magazine = Math.Min(serverMagazine, slot.MagazineSize);
        }

        private bool HasPendingFor(CombatWeaponId weapon)
        {
            for (var i = 0; i < _pendingCount; i++)
                if (_pending[i].Weapon == weapon) return true;
            return false;
        }

        private int FindPending(ushort seq)
        {
            for (var i = 0; i < _pendingCount; i++)
                if (_pending[i].Seq == seq) return i;
            return -1;
        }

        private void RemovePendingAt(int index)
        {
            _pendingCount--;
            for (var i = index; i < _pendingCount; i++) _pending[i] = _pending[i + 1];
            _pending[_pendingCount] = default;
        }

        private ushort NextSeq()
        {
            var seq = _nextSeq;
            _nextSeq = unchecked((ushort)(_nextSeq + 1));
            if (_nextSeq == 0) _nextSeq = 1;
            return seq;
        }

        private void PublishSelection()
        {
            _hud.SetHeldWeapon(CombatWeaponIds.ToJsonId(_selected));
            PublishAmmoIfSelected(SelectedSlot);
        }

        private void PublishAmmoIfSelected(CombatWeaponSlot slot)
        {
            if (slot != SelectedSlot) return;
            if (slot.IsRanged) _hud.SetAmmo(slot.Magazine, slot.Reserve);
            else _hud.SetAmmo(0, 0);
        }

        private void Raise(CombatEventKind kind, CombatWeaponId weapon, ushort seq, float aimDegrees, CombatOutcome outcome)
        {
            Changed?.Invoke(new CombatEvent(kind, weapon, seq, aimDegrees, outcome));
        }
    }
}
