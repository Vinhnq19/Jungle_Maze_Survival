using System;
using System.Collections.Generic;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Player;
using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using JungleMaze.Core.Events;
using JungleMaze.Core.Ticking;
using JungleMaze.Domain.Network;
using JungleMaze.Domain.Player;

namespace JungleMaze.App.Match
{
    /// <summary>
    /// Authoritative match binding: phase, snapshot, zone, result. Movement input is predicted locally by
    /// <see cref="LocalPrediction"/> (MOV-02) and reconciled with each snapshot; no local damage or client-owned timer.
    /// </summary>
    public sealed class MatchSession : IInitializable, ITickable, IDisposable
    {
        private readonly INetworkClient _network;
        private readonly IPacketSender _sender;
        private readonly IPacketReceiver _receiver;
        private readonly IEventBus _bus;
        private readonly PlayerRegistry _players;
        private readonly Action<NetworkStateChanged> _onNetwork;
        private readonly Action<MatchPhasePacket> _onPhase;
        private readonly Action<WorldSnapshotPacket> _onSnapshot;
        private readonly Action<ZoneStatePacket> _onZone;
        private readonly Action<ResumeTokenPacket> _onWelcome;
        private readonly Action<MatchResultPacket> _onResult;
        private readonly Action<MatchRosterPacket> _onRoster;
        private readonly Action<MatchEntityEventPacket> _onEntity;
        private PlayerIntent _intent;
        private float _sendElapsed, _tickFraction;
        private ushort _inputSequence;
        private bool _hasSnapshot, _helloSent;
        private readonly Queue<Guid> _retiredMatches = new Queue<Guid>();
        private uint _lastSnapshotTick;

        public MatchSession(INetworkClient network, IPacketSender sender, IPacketReceiver receiver,
            IEventBus bus, PlayerRegistry players, LocalPrediction prediction = null)
        {
            _network = network; _sender = sender; _receiver = receiver; _bus = bus; _players = players;
            Prediction = prediction ?? new LocalPrediction();
            _onNetwork = NetworkChanged; _onPhase = PhaseChanged; _onSnapshot = Snapshot;
            _onZone = p => { if (p.MatchId == MatchId && (!_hasZone || unchecked((int)(p.Tick - Zone.Tick)) >= 0)) { Zone = p; _hasZone = true; } };
            _onWelcome = p => { if (p.MatchId == MatchId) { OwnNetId = p.NetId; ResumeProof = p.ResumeToken; } };
            _onResult = p => { if (p.MatchId == MatchId) { Result = p; HasResult = true; } };
            _onRoster = p => { if (p.MatchId == MatchId) Roster = p.Players ?? Array.Empty<MatchParticipantState>(); };
            _onEntity = p => { if (p.MatchId == MatchId && p.Kind == MatchEntityEvent.Despawn) RemovePlayer(p.NetId); };
        }

        public event Action<MatchPhasePacket> PhaseUpdated;
        private bool _hasZone;
        public Guid MatchId { get; private set; }
        public MatchPhase Phase { get; private set; }
        public uint ServerTick { get; private set; }
        public MatchPhasePacket PhaseState { get; private set; }
        public ZoneStatePacket Zone { get; private set; }
        public bool HasZone => _hasZone;
        public bool HasResult { get; private set; }
        public MatchResultPacket Result { get; private set; }
        public MatchParticipantState[] Roster { get; private set; } = Array.Empty<MatchParticipantState>();
        public byte OwnNetId { get; private set; } = 255;
        public Guid ResumeProof { get; private set; }
        public int Snapshots { get; private set; }
        public int IgnoredPackets { get; private set; }
        public int InputsSent { get; private set; }
        public double ActiveInputSeconds { get; private set; }
        public int InputCatchupDrops { get; private set; }
        public bool DeferHello { get; set; }
        public bool IdentityTicketSent { get; private set; }
        public bool Connected => _network.State == NetworkState.Joined;
        public float CountdownSeconds => Math.Max(0, unchecked((int)(PhaseState.PhaseEndTick - ServerTick))) / 30f;
        public byte OwnHp => OwnNetId != 255 && _players.TryGet(OwnNetId, out var p) && p.HasState ? p.Current.Hp : (byte)0;
        public bool CanResume => !Connected && Phase == MatchPhase.InGame && ResumeProof != Guid.Empty;
        public PlayerRegistry Players => _players;
        /// <summary>MOV-02: dự đoán nhân vật của mình; cần SetWorld (map đã kiểm hash) trước khi hoạt động.</summary>
        public LocalPrediction Prediction { get; }

        public void Initialize()
        {
            _bus.Subscribe(_onNetwork);
            _receiver.Subscribe(_onPhase); _receiver.Subscribe(_onSnapshot); _receiver.Subscribe(_onZone);
            _receiver.Subscribe(_onWelcome); _receiver.Subscribe(_onResult); _receiver.Subscribe(_onRoster); _receiver.Subscribe(_onEntity);
        }
        public void Dispose()
        {
            _bus.Unsubscribe(_onNetwork);
            _receiver.Unsubscribe(_onPhase); _receiver.Unsubscribe(_onSnapshot); _receiver.Unsubscribe(_onZone);
            _receiver.Unsubscribe(_onWelcome); _receiver.Unsubscribe(_onResult); _receiver.Unsubscribe(_onRoster); _receiver.Unsubscribe(_onEntity);
        }
        public void SetIntent(in PlayerIntent intent) => _intent = intent;
        public void Tick(float deltaTime)
        {
            Prediction.Advance(deltaTime);
            if (!Connected) return;
            if (!_helloSent) Hello();
            _tickFraction += deltaTime * 30f;
            var ticks = (uint)_tickFraction;
            ServerTick += ticks; _tickFraction -= ticks;
            if (Phase != MatchPhase.InGame || OwnNetId == 255 || OwnHp == 0) { _sendElapsed = 0; return; }
            ActiveInputSeconds += deltaTime;
            _sendElapsed += deltaTime;
            var sent = 0;
            while (_sendElapsed >= 1f / 30 && sent++ < 3)
            {
                _sendElapsed -= 1f / 30;
                // Seq 0 is reserved for "server has processed nothing yet" (see LocalPrediction ack handling).
                if (++_inputSequence == 0) _inputSequence = 1;
                var input = new PlayerInputPacket { Tick = ServerTick, InputSeq = _inputSequence };
                input.SetMove(_intent.MoveX, _intent.MoveY); input.SetAimDegrees(_intent.AimDegrees);
                // Demo sends movement/aim only; this task does not implement combat actions.
                var scoped = new MatchInputPacket { MatchId = MatchId, Input = input };
                if (_sender.Send(ref scoped, PacketDelivery.Unreliable)) { InputsSent++; Prediction.RecordInput(input); }
            }
            if (_sendElapsed > .1f) { _sendElapsed = 0; InputCatchupDrops++; }
        }
        private void Hello()
        {
            if (DeferHello) return;
            var hello = new ClientHelloPacket { ProtocolVersion = MatchProtocol.Version, Ready = true };
            _helloSent = _sender.Send(ref hello, PacketDelivery.ReliableOrdered);
        }
        private void NetworkChanged(NetworkStateChanged change)
        {
            if (change.Current == NetworkState.Connecting) IdentityTicketSent = false;
            if (change.Current == NetworkState.Joined) { _helloSent = false; Hello(); }
            if (change.Current == NetworkState.Failed || change.Current == NetworkState.Disconnected)
            {
                // Retain only the short-lived resume proof; scene state is repopulated by the server.
                ClearPlayers(); _intent = default; _sendElapsed = 0; _hasSnapshot = false; _helloSent = false; Prediction.Reset();
            }
        }
        private void PhaseChanged(MatchPhasePacket packet)
        {
            if (!Connected) return;
            if (packet.MatchId != MatchId)
            {
                if (_retiredMatches.Contains(packet.MatchId)) { IgnoredPackets++; return; }
                if (MatchId != Guid.Empty) { _retiredMatches.Enqueue(MatchId); if (_retiredMatches.Count > 8) _retiredMatches.Dequeue(); }
                ClearPlayers(); MatchId = packet.MatchId; _hasSnapshot = false; HasResult = false; _hasZone = false;
                Roster = Array.Empty<MatchParticipantState>(); OwnNetId = 255; ResumeProof = Guid.Empty; _inputSequence = 0;
                Prediction.Reset();
            }
            Phase = packet.Phase; PhaseState = packet; ServerTick = packet.ServerTick; _tickFraction = 0;
            PhaseUpdated?.Invoke(packet);
        }
        /// <summary>The scene view calls this only after the gameplay scene and matching map data are ready.</summary>
        public void ConfirmLoaded()
        {
            if (!Connected || Phase != MatchPhase.Loading) return;
            var ack = new LoadAckPacket { MatchId = MatchId, Ready = true };
            _sender.Send(ref ack, PacketDelivery.ReliableOrdered);
        }
        private void Snapshot(WorldSnapshotPacket packet)
        {
            if (packet.MatchId != MatchId || (_hasSnapshot && unchecked((int)(packet.ServerTick - _lastSnapshotTick)) <= 0))
            { IgnoredPackets++; return; }
            _hasSnapshot = true; _lastSnapshotTick = packet.ServerTick; ServerTick = packet.ServerTick; _tickFraction = 0; Snapshots++;
            var ack = new SnapshotAckPacket { MatchId = MatchId, AckTick = packet.ServerTick };
            _sender.Send(ref ack, PacketDelivery.Unreliable);
            ushort seen = 0;
            for (var i = 0; i < packet.PlayerCount; i++)
            {
                var state = packet.GetPlayer(i); seen |= (ushort)(1 << state.NetId);
                var model = new PlayerSnapshot { Tick = packet.ServerTick, X = state.X, Y = state.Y,
                    RotationDegrees = state.RotationDegrees, Hp = state.Hp, IsDead = state.IsDead,
                    LastProcessedInputSeq = state.LastProcessedInputSeq };
                if (state.NetId == OwnNetId) Prediction.Reconcile(model);
                if (_players.GetOrAdd(state.NetId).Apply(model)) _bus.Publish(new PlayerStateUpdated(state.NetId));
            }
            for (var i = _players.ActiveIds.Count - 1; i >= 0; i--)
            { var id = _players.ActiveIds[i]; if ((seen & (1 << id)) == 0) RemovePlayer(id); }
        }
        private void RemovePlayer(byte id)
        { if (_players.Remove(id)) _bus.Publish(new PlayerRemoved(id)); }
        private void ClearPlayers()
        { _players.Clear(id => _bus.Publish(new PlayerRemoved(id))); }
        public void Replay()
        {
            if (!Connected || Phase != MatchPhase.Ending) return;
            var ack = new ReplayAckPacket { MatchId = MatchId };
            _sender.Send(ref ack, PacketDelivery.ReliableOrdered);
        }
        public void Leave()
        {
            if (Connected && MatchId != Guid.Empty)
            { var packet = new PlayerLeavePacket { MatchId = MatchId }; _sender.Send(ref packet, PacketDelivery.ReliableOrdered); }
            ResumeProof = Guid.Empty;
        }
        public void AttachIdentity(string ticket)
        {
            if (!Connected || string.IsNullOrEmpty(ticket)) return;
            var packet = new AttachIdentityPacket { Ticket = ticket };
            IdentityTicketSent = _sender.Send(ref packet, PacketDelivery.ReliableOrdered);
        }
        public void AnnounceReady()
        { DeferHello = false; if (Connected && !_helloSent) Hello(); }
    }
}
