using System;
using System.Collections.Generic;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Match;
using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using JungleMaze.Core.Ticking;
using JungleMaze.Domain.MatchUi;

namespace JungleMaze.App.MatchUi
{
    /// <summary>
    /// <see cref="ISpectatorSource"/> over the live match, read only. Alive = in the start roster, not the local
    /// player, not eliminated or despawned, and the latest snapshot has HP &gt; 0 and not dead. An entity event removes
    /// the row at once; the per-tick check catches the rest (AC27). Positions are the MOV-03 interpolated poses.
    /// The list is rebuilt in place (≤ 9 rows, cached names), so a tick does not allocate.
    /// </summary>
    public sealed class MatchSessionSpectatorSource : ISpectatorSource, IInitializable, ITickable, IDisposable
    {
        private readonly MatchSession _match;
        private readonly IPacketReceiver _receiver;
        private readonly IPlayerPoseSource _poses;
        private readonly byte _maxHp;
        private readonly MatchStartRoster _start = new MatchStartRoster();
        private readonly List<SpectatorEntry> _alive = new List<SpectatorEntry>(MatchStartRoster.MaxPlayers);
        private readonly Action<MatchEntityEventPacket> _onEntity;
        private readonly Action<EventPacket> _onEvent;
        private ushort _gone, _aliveMask;
        private int _hpSignature;
        private bool _subscribed, _endSignal;

        public MatchSessionSpectatorSource(MatchSession match, IPacketReceiver receiver, IPlayerPoseSource poses, int maxHp)
        {
            _match = match ?? throw new ArgumentNullException(nameof(match));
            _receiver = receiver;
            _poses = poses;
            _maxHp = (byte)Math.Max(0, Math.Min(255, maxHp));
            _onEntity = OnEntity;
            _onEvent = OnEvent;
        }

        public byte LocalNetId => _match.OwnNetId;
        public int StartCount => _start.Count;
        public int AliveCount => _alive.Count;
        public bool MatchEnded { get; private set; }
        public event Action<byte> PlayerEliminated;
        public event Action Changed;

        public SpectatorEntry GetAlive(int index) => _alive[index];

        public bool TryGetEntry(byte netId, out SpectatorEntry entry)
        {
            for (var i = 0; i < _alive.Count; i++)
                if (_alive[i].NetId == netId) { entry = _alive[i]; return true; }
            entry = default;
            return false;
        }

        public bool TryGetPosition(byte netId, out float x, out float y)
        {
            if (_poses != null && _poses.TryGetPose(netId, out var pose)) { x = pose.X; y = pose.Y; return true; }
            x = y = 0f;
            return false;
        }

        public void Initialize()
        {
            if (_subscribed || _receiver == null) return;
            _receiver.Subscribe(_onEntity);
            _receiver.Subscribe(_onEvent);
            _subscribed = true;
        }

        public void Dispose()
        {
            if (!_subscribed) return;
            _receiver.Unsubscribe(_onEntity);
            _receiver.Unsubscribe(_onEvent);
            _subscribed = false;
        }

        public void Tick(float deltaTime) => Refresh();

        /// <summary>Handles one entity event; public so tests can feed packets without a network.</summary>
        public void OnEntity(MatchEntityEventPacket packet)
        {
            if (packet.MatchId != _match.MatchId || packet.NetId >= MatchStartRoster.MaxPlayers) return;
            if (packet.Kind != MatchEntityEvent.Eliminated && packet.Kind != MatchEntityEvent.Despawn) return;
            _gone |= (ushort)(1 << packet.NetId);
            Refresh();
        }

        public void OnEvent(EventPacket packet)
        {
            if (packet.EventType == GameEventType.MatchEnded && _match.Phase == MatchPhase.InGame) _endSignal = true;
            else if (packet.EventType == GameEventType.PlayerDied && packet.TargetNetId < MatchStartRoster.MaxPlayers)
                _gone |= (ushort)(1 << packet.TargetNetId);
            else return;
            Refresh();
        }

        private void Refresh()
        {
            if (_start.Update(_match))
            {
                _gone = 0;
                _aliveMask = 0;
                _hpSignature = 0;
                _endSignal = false;
                MatchEnded = false;
            }
            var ended = _endSignal || _match.Phase == MatchPhase.Ending || _match.HasResult;
            ushort mask = 0;
            var hpSignature = 0;
            _alive.Clear();
            for (byte id = 0; id < MatchStartRoster.MaxPlayers; id++)
            {
                if (id == _match.OwnNetId || !_start.Contains(id) || (_gone & (1 << id)) != 0) continue;
                if (!_match.Players.TryGet(id, out var player) || !player.HasState) continue;
                var state = player.Current;
                if (state.IsDead || state.Hp == 0) continue;
                mask |= (ushort)(1 << id);
                hpSignature = hpSignature * 31 + state.Hp;
                _alive.Add(new SpectatorEntry(id, _start.NameOf(id), _maxHp > 0, state.Hp, _maxHp, null));
            }
            var removed = (ushort)(_aliveMask & ~mask);
            var changed = mask != _aliveMask || hpSignature != _hpSignature || ended != MatchEnded;
            _aliveMask = mask;
            _hpSignature = hpSignature;
            MatchEnded = ended;
            for (byte id = 0; id < MatchStartRoster.MaxPlayers; id++)
                if ((removed & (1 << id)) != 0) PlayerEliminated?.Invoke(id);
            if (changed) Changed?.Invoke();
        }
    }
}
