using System;
using System.Collections.Generic;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Ports;
using JungleMaze.Domain.Balance;

namespace JungleMaze.App.Combat.Mocks
{
    /// <summary>
    /// In-process stand-in for the combat server, used by the WPN-01 demo and tests until the GameServer transport is
    /// connected. It keeps its own ammo per peer, answers requests only when <see cref="Flush"/> runs (so tests control
    /// latency), and sends every result to all peers, like the room-wide server broadcast. Not authoritative for
    /// anything beyond ammo and reload timing; no damage, HP, stun or loot.
    /// </summary>
    public sealed class FakeCombatBackend
    {
        private readonly IBalanceConfigProvider _balance;
        private readonly List<FakeCombatPeer> _peers = new List<FakeCombatPeer>();
        private readonly Queue<QueuedRequest> _inbox = new Queue<QueuedRequest>();

        public FakeCombatBackend(IBalanceConfigProvider balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public uint Tick { get; private set; }

        /// <summary>Optional override for the attack decision. Null uses the default ammo/weapon rules.</summary>
        public Func<FakeCombatPeer, AttackRequestPacket, CombatOutcome> AttackOverride { get; set; }

        public FakeCombatPeer AddPeer(byte netId)
        {
            var peer = new FakeCombatPeer(this, netId, _balance);
            _peers.Add(peer);
            return peer;
        }

        /// <summary>Number of requests waiting for the next <see cref="Flush"/>.</summary>
        public int Pending => _inbox.Count;

        internal void EnqueueAttack(FakeCombatPeer peer, AttackRequestPacket packet) =>
            _inbox.Enqueue(new QueuedRequest { Peer = peer, IsReload = false, Attack = packet });

        internal void EnqueueReload(FakeCombatPeer peer, ReloadRequestPacket packet) =>
            _inbox.Enqueue(new QueuedRequest { Peer = peer, IsReload = true, Reload = packet });

        /// <summary>Processes queued requests in arrival order, then sends each result to every peer.</summary>
        public void Flush()
        {
            while (_inbox.Count > 0)
            {
                var request = _inbox.Dequeue();
                if (request.IsReload) HandleReload(request.Peer, request.Reload);
                else HandleAttack(request.Peer, request.Attack);
            }
        }

        /// <summary>Advances server ticks; reloads whose ReloadEndTick passed fill the magazine from reserve.</summary>
        public void AdvanceTicks(uint ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                Tick++;
                for (var p = 0; p < _peers.Count; p++) _peers[p].CompleteDueReloads(Tick);
            }
        }

        private void HandleAttack(FakeCombatPeer peer, AttackRequestPacket packet)
        {
            var outcome = AttackOverride != null ? AttackOverride(peer, packet) : peer.DecideAttack(packet);
            var result = new AttackResultPacket
            {
                RequestSeq = packet.RequestSeq,
                Weapon = packet.Weapon,
                Outcome = outcome,
                Tick = Tick,
                Magazine = (byte)peer.MagazineOf(packet.Weapon),
                NetId = peer.NetId
            };
            for (var i = 0; i < _peers.Count; i++) _peers[i].RaiseAttack(result);
        }

        private void HandleReload(FakeCombatPeer peer, ReloadRequestPacket packet)
        {
            var outcome = peer.DecideReload(packet, Tick, out var reloadEndTick);
            var result = new ReloadResultPacket
            {
                RequestSeq = packet.RequestSeq,
                Weapon = packet.Weapon,
                Outcome = outcome,
                Tick = Tick,
                ReloadEndTick = reloadEndTick,
                Magazine = (byte)peer.MagazineOf(packet.Weapon),
                NetId = peer.NetId
            };
            for (var i = 0; i < _peers.Count; i++) _peers[i].RaiseReload(result);
        }

        private struct QueuedRequest
        {
            public FakeCombatPeer Peer;
            public bool IsReload;
            public AttackRequestPacket Attack;
            public ReloadRequestPacket Reload;
        }
    }

    /// <summary>One player's link to <see cref="FakeCombatBackend"/>: the client sends through it and receives every room result.</summary>
    public sealed class FakeCombatPeer : ICombatRequestSink, ICombatResultSource
    {
        private readonly FakeCombatBackend _server;
        private readonly int[] _magazine = new int[CombatWeaponIds.Count];
        private readonly int[] _reserve = new int[CombatWeaponIds.Count];
        private readonly uint[] _reloadEndTick = new uint[CombatWeaponIds.Count];
        private readonly bool[] _reloading = new bool[CombatWeaponIds.Count];
        private readonly WeaponDefinition[] _definitions = new WeaponDefinition[CombatWeaponIds.Count];

        internal FakeCombatPeer(FakeCombatBackend server, byte netId, IBalanceConfigProvider balance)
        {
            _server = server;
            NetId = netId;
            for (var i = 0; i < CombatWeaponIds.Count; i++)
            {
                balance.TryGetWeapon(CombatWeaponIds.ToJsonId((CombatWeaponId)i), out var definition);
                _definitions[i] = definition;
                if (definition == null) continue;
                _magazine[i] = definition.Stats.MagazineSize ?? 0;
                _reserve[i] = definition.Stats.ReserveAmmo ?? 0;
            }
        }

        public byte NetId { get; }

        public event Action<AttackResultPacket> AttackResult;
        public event Action<ReloadResultPacket> ReloadResult;

        public void SendAttack(AttackRequestPacket packet) => _server.EnqueueAttack(this, packet);
        public void SendReload(ReloadRequestPacket packet) => _server.EnqueueReload(this, packet);

        public int MagazineOf(CombatWeaponId weapon) => _magazine[(int)weapon];
        public int ReserveOf(CombatWeaponId weapon) => _reserve[(int)weapon];

        /// <summary>Offline inventory preview only: do not grant the fake backend's default loadout.</summary>
        public void SynchronizeEquipment(CombatWeaponId weapon, int magazine, int reserve, bool equipmentChanged)
        {
            if (!CombatWeaponIds.IsKnown(weapon)) throw new ArgumentOutOfRangeException(nameof(weapon));
            if (equipmentChanged) Array.Clear(_reloading, 0, _reloading.Length);
            var index = (int)weapon;
            _magazine[index] = Math.Max(0, Math.Min(_definitions[index]?.Stats.MagazineSize ?? 0, magazine));
            _reserve[index] = Math.Max(0, reserve);
        }

        internal CombatOutcome DecideAttack(AttackRequestPacket packet)
        {
            if (!CombatWeaponIds.IsKnown(packet.Weapon)) return CombatOutcome.InvalidWeapon;
            var definition = _definitions[(int)packet.Weapon];
            if (definition == null) return CombatOutcome.InvalidWeapon;
            if (definition.Category != WeaponCategory.Ranged) return CombatOutcome.Accepted;
            var index = (int)packet.Weapon;
            if (_magazine[index] <= 0) return CombatOutcome.NoAmmo;
            _magazine[index]--;
            return CombatOutcome.Accepted;
        }

        internal CombatOutcome DecideReload(ReloadRequestPacket packet, uint tick, out uint reloadEndTick)
        {
            reloadEndTick = 0;
            if (!CombatWeaponIds.IsKnown(packet.Weapon)) return CombatOutcome.InvalidWeapon;
            var index = (int)packet.Weapon;
            var definition = _definitions[index];
            if (definition == null || definition.Category != WeaponCategory.Ranged) return CombatOutcome.InvalidWeapon;
            if (_reloading[index]) return CombatOutcome.Reloading;
            if (_magazine[index] >= (definition.Stats.MagazineSize ?? 0)) return CombatOutcome.MagazineFull;
            if (_reserve[index] <= 0) return CombatOutcome.NoReserve;

            var seconds = definition.Stats.ReloadSeconds ?? 0f;
            reloadEndTick = tick + (uint)Math.Ceiling(seconds * CombatShotMath.TickRate);
            _reloading[index] = true;
            _reloadEndTick[index] = reloadEndTick;
            return CombatOutcome.Accepted;
        }

        internal void CompleteDueReloads(uint tick)
        {
            for (var i = 0; i < _reloading.Length; i++)
            {
                if (!_reloading[i] || tick < _reloadEndTick[i]) continue;
                _reloading[i] = false;
                var definition = _definitions[i];
                if (definition == null) continue;
                var take = Math.Min((definition.Stats.MagazineSize ?? 0) - _magazine[i], _reserve[i]);
                if (take <= 0) continue;
                _magazine[i] += take;
                _reserve[i] -= take;
            }
        }

        internal void RaiseAttack(AttackResultPacket packet) => AttackResult?.Invoke(packet);
        internal void RaiseReload(ReloadResultPacket packet) => ReloadResult?.Invoke(packet);
    }
}
