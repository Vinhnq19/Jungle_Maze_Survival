using System;
using System.Collections.Generic;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Flow;
using JungleMaze.App.Match;
using JungleMaze.App.Ports;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Lobby;
using JungleMaze.Domain.Network;

namespace JungleMaze.App.Lobby.Adapters
{
    /// <summary>
    /// N1 from <see cref="MatchSession"/>: read only. <c>ServerTick</c> is the session's server-anchored tick; UI-02
    /// never advances it (AC21). Slots are rebuilt only when the roster array changes and keep NetId/session keys (AC11).
    /// </summary>
    public sealed class MatchSessionLobbySource : ILobbySource
    {
        private readonly MatchSession _session;
        private readonly Func<string> _roomCode;
        private readonly Func<string> _loadError;
        private readonly List<LobbySlot> _slots = new List<LobbySlot>(10);
        private MatchParticipantState[] _builtFrom;
        private byte _builtFor = 255;

        public MatchSessionLobbySource(MatchSession session, Func<string> roomCode, Func<string> loadError = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _roomCode = roomCode ?? (() => "");
            _loadError = loadError ?? (() => null);
        }

        public bool HasPhase => _session.MatchId != Guid.Empty;
        public Guid MatchId => _session.MatchId;
        public MatchPhase Phase => _session.Phase;
        public int PlayerCount => _session.PhaseState.PlayerCount;
        public int MinPlayers => _session.PhaseState.MinPlayers;
        public int MaxPlayers => _session.PhaseState.MaxPlayers > 0 ? _session.PhaseState.MaxPlayers : LobbyLimits.PlayersPerRoom;
        public uint PhaseEndTick => _session.PhaseState.PhaseEndTick;
        public uint ServerTick => _session.ServerTick;
        public byte OwnNetId => _session.OwnNetId;
        public string RoomCode => _roomCode() ?? "";
        public bool ReadySupported => false; // the roster has no ready flag yet (M2)
        public string LoadError => _loadError();

        public IReadOnlyList<LobbySlot> Slots
        {
            get
            {
                var roster = _session.Roster;
                if (!ReferenceEquals(roster, _builtFrom) || _builtFor != _session.OwnNetId)
                {
                    _builtFrom = roster;
                    _builtFor = _session.OwnNetId;
                    _slots.Clear();
                    foreach (var p in roster)
                        _slots.Add(new LobbySlot(p.NetId, p.PlayerSessionId, p.DisplayName, p.NetId == _session.OwnNetId));
                }
                return _slots;
            }
        }
    }

    /// <summary>N2 from <see cref="MatchSession.Result"/>; one view object per match id.</summary>
    public sealed class MatchSessionResultSource : IMatchResultSource
    {
        private readonly MatchSession _session;
        private MatchResultView _view;

        public MatchSessionResultSource(MatchSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public bool HasResult => _session.HasResult;

        public MatchResultView Current
        {
            get
            {
                if (!_session.HasResult) return null;
                var r = _session.Result;
                if (_view == null || _view.MatchId != r.MatchId || _view.OwnNetId != _session.OwnNetId)
                    _view = new MatchResultView
                    {
                        MatchId = r.MatchId, Reason = r.Reason, WinnerNetId = r.WinnerNetId,
                        ParticipantCount = r.ParticipantCount, OwnNetId = _session.OwnNetId,
                        Participants = r.Participants ?? Array.Empty<MatchParticipantState>()
                    };
                return _view;
            }
        }
    }

    /// <summary>
    /// N3 from <see cref="INetworkClient"/>. A failure before any transport (login: <see cref="ConnectionError"/>; bad
    /// endpoint: <see cref="ReportLocalFailure"/>) reads as <c>Failed</c> until the next state change, so the join form
    /// shows it instead of waiting for the 20 s budget.
    /// </summary>
    public sealed class NetworkConnectionStateSource : IConnectionStateSource, IDisposable
    {
        private readonly INetworkClient _network;
        private readonly Func<bool> _canResume;
        private readonly IEventBus _bus;
        private readonly Action<ConnectionError> _onError;
        private readonly Action<NetworkStateChanged> _onState;
        private DisconnectReason _local;

        public NetworkConnectionStateSource(INetworkClient network, Func<bool> canResume, IEventBus bus)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _canResume = canResume ?? (() => false);
            _bus = bus;
            _onError = _ => _local = DisconnectReason.LoginFailed;
            _onState = _ => _local = DisconnectReason.None;
            _bus?.Subscribe(_onError);
            _bus?.Subscribe(_onState);
        }

        public NetworkState State => _local != DisconnectReason.None ? NetworkState.Failed : _network.State;
        public DisconnectReason LastReason => _local != DisconnectReason.None ? _local : _network.LastReason;
        public string Step => string.IsNullOrEmpty(_network.Stats.Transport) ? "Auto" : _network.Stats.Transport;
        public int RttMs => _network.Stats.LastRttMs > 0 ? (int)Math.Round(_network.Stats.LastRttMs) : -1;
        public bool CanResume => _canResume();

        /// <summary>Called before a new join so an old failure does not count.</summary>
        public void ClearLoginFailure() => _local = DisconnectReason.None;

        /// <summary>The join was refused locally (invalid endpoint) and nothing will connect.</summary>
        public void ReportLocalFailure() => _local = DisconnectReason.InvalidConfig;

        public void Dispose()
        {
            _bus?.Unsubscribe(_onError);
            _bus?.Unsubscribe(_onState);
        }
    }

    /// <summary>
    /// N4 through existing public APIs only: the form presenter's Join/Disconnect (T-2; Disconnect already sends
    /// PlayerLeave first), <see cref="MatchSession.Replay"/>/<see cref="MatchSession.AnnounceReady"/> and
    /// <see cref="IMatchConnection.Resume"/>. LoadAck is never sent from here (AC27).
    /// </summary>
    public sealed class SessionLobbyCommands : ILobbyCommands
    {
        private readonly Action<string, string> _join;
        private readonly Action _disconnect;
        private readonly MatchSession _session;
        private readonly IMatchConnection _connection;

        public SessionLobbyCommands(Action<string, string> join, Action disconnect, MatchSession session, IMatchConnection connection)
        {
            _join = join ?? throw new ArgumentNullException(nameof(join));
            _disconnect = disconnect ?? throw new ArgumentNullException(nameof(disconnect));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _connection = connection;
        }

        public void Join(string roomCode, string displayName) => _join(roomCode, displayName);
        public void Cancel() => _disconnect();
        public void Leave() => _disconnect();
        public void Ready() => _session.AnnounceReady();
        public void Replay() => _session.Replay();

        public void Resume()
        {
            if (_connection != null && _session.ResumeProof != Guid.Empty) _connection.Resume(_session.ResumeProof);
        }
    }
}
