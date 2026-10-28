using System;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Match;
using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using JungleMaze.Core.Ticking;
using JungleMaze.Domain.MatchUi;

namespace JungleMaze.App.MatchUi
{
    /// <summary>
    /// <see cref="IEliminationSource"/> over the live match, read only (same approach as AUD-01's audio adapter).
    /// The local player is out when the server sends <c>MatchEntityEventPacket{Eliminated}</c> for our NetId (or our
    /// snapshot says dead). Rank and survival time come from the roster when it arrives (TR5). Kills stay hidden
    /// because the server always sends 0 (T-6); the killer is known only if <c>PlayerDied</c> names one (R1).
    /// </summary>
    public sealed class MatchSessionEliminationSource : IEliminationSource, IInitializable, ITickable, IDisposable
    {
        private const float TicksPerSecond = GameplayProtocol.TickRate;

        private readonly MatchSession _match;
        private readonly IPacketReceiver _receiver;
        private readonly MatchStartRoster _start = new MatchStartRoster();
        private readonly Action<MatchEntityEventPacket> _onEntity;
        private readonly Action<EventPacket> _onEvent;
        private readonly Action<MatchEliminationEventPacket> _onElimination;
        private byte _killer = EliminationInfo.NoNetId;
        private EliminationCause _cause = EliminationCause.Unknown;
        private uint _authoritativeTick;
        private bool _subscribed;

        public MatchSessionEliminationSource(MatchSession match, IPacketReceiver receiver)
        {
            _match = match ?? throw new ArgumentNullException(nameof(match));
            _receiver = receiver;
            _onEntity = OnEntity;
            _onEvent = OnEvent;
            _onElimination = OnElimination;
        }

        public bool IsLocalEliminated { get; private set; }
        public EliminationInfo Current { get; private set; }
        public event Action<EliminationInfo> LocalEliminated;
        public event Action<EliminationInfo> Updated;

        public void Initialize()
        {
            if (_subscribed || _receiver == null) return;
            _receiver.Subscribe(_onEntity);
            _receiver.Subscribe(_onEvent);
            _receiver.Subscribe(_onElimination);
            _subscribed = true;
        }

        public void Dispose()
        {
            if (!_subscribed) return;
            _receiver.Unsubscribe(_onEntity);
            _receiver.Unsubscribe(_onEvent);
            _receiver.Unsubscribe(_onElimination);
            _subscribed = false;
        }

        public void Tick(float deltaTime)
        {
            if (_start.Update(_match)) Reset();
            if (!IsLocalEliminated)
            {
                var own = _match.OwnNetId;
                if (own != EliminationInfo.NoNetId && _match.Phase == MatchPhase.InGame
                    && _match.Players.TryGet(own, out var player) && player.HasState && player.Current.IsDead)
                    Eliminate();
                return;
            }
            if (!Current.HasRank && TryRank(out var rank, out var survival))
            {
                Current = Current.WithRank(rank, (byte)_start.Count, survival);
                Updated?.Invoke(Current);
            }
        }

        /// <summary>Handles one entity event; public so tests can feed packets without a network.</summary>
        public void OnEntity(MatchEntityEventPacket packet)
        {
            if (packet.MatchId != _match.MatchId || packet.Kind != MatchEntityEvent.Eliminated) return;
            if (packet.NetId == _match.OwnNetId) Eliminate();
        }

        public void OnEvent(EventPacket packet)
        {
            if (packet.EventType != GameEventType.PlayerDied || packet.TargetNetId != _match.OwnNetId) return;
            if (packet.SourceNetId == EventPacket.NoTarget || packet.SourceNetId == packet.TargetNetId) return;
            _killer = packet.SourceNetId;
            if (!IsLocalEliminated || Current.HasKiller) return;
            Current = Build(_authoritativeTick != 0 ? _authoritativeTick : Current.EliminationTick);
            Updated?.Invoke(Current);
        }

        public void OnElimination(MatchEliminationEventPacket packet)
        {
            if (packet.MatchId != _match.MatchId || packet.VictimNetId != _match.OwnNetId) return;
            _authoritativeTick = packet.Tick;
            _cause = (EliminationCause)packet.Cause;
            if (packet.KillerNetId != EliminationInfo.NoNetId && packet.KillerNetId != packet.VictimNetId)
            {
                _killer = packet.KillerNetId;
            }
            if (!IsLocalEliminated)
            {
                Eliminate(packet.Tick);
            }
            else
            {
                Current = Build(_authoritativeTick != 0 ? _authoritativeTick : Current.EliminationTick);
                Updated?.Invoke(Current);
            }
        }

        private void Eliminate(uint? tickOverride = null)
        {
            if (IsLocalEliminated || _match.MatchId == Guid.Empty) return;
            _start.Update(_match);
            IsLocalEliminated = true;
            var tick = tickOverride ?? (_authoritativeTick != 0 ? _authoritativeTick : _match.ServerTick);
            Current = Build(tick);
            LocalEliminated?.Invoke(Current);
        }

        private EliminationInfo Build(uint tick)
        {
            var hasRank = TryRank(out var rank, out var survival);
            var cause = _cause != EliminationCause.Unknown ? _cause : (_killer != EliminationInfo.NoNetId ? EliminationCause.Player : EliminationCause.Unknown);
            var killerName = _killer != EliminationInfo.NoNetId ? _start.NameOf(_killer) : null;
            return new EliminationInfo(_match.MatchId, tick, hasRank ? rank : (byte)0, hasRank ? (byte)_start.Count : (byte)0,
                cause, _killer, killerName, null, float.NaN, hasRank ? survival : float.NaN, -1);
        }

        private bool TryRank(out byte rank, out float survivalSeconds)
        {
            rank = 0;
            survivalSeconds = float.NaN;
            if (_start.Count == 0 || !MatchStartRoster.TryFind(_match, _match.OwnNetId, out var me) || me.Rank == 0) return false;
            rank = me.Rank;
            survivalSeconds = me.SurvivalTicks / TicksPerSecond;
            return true;
        }

        private void Reset()
        {
            IsLocalEliminated = false;
            Current = default;
            _killer = EliminationInfo.NoNetId;
            _cause = EliminationCause.Unknown;
            _authoritativeTick = 0;
        }
    }
}
