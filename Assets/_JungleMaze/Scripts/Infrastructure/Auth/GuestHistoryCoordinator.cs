using System;
using System.Threading.Tasks;
using System.Threading;
using JungleMaze.App.Match;
using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Network;

namespace JungleMaze.Infrastructure.Auth
{
    /// <summary>Prepare optional identity before announcing gameplay capability. UDP join and server tick never await this work.</summary>
    public sealed class GuestHistoryCoordinator : IInitializable, IDisposable
    {
        private readonly IConfigProvider _config;
        private readonly IMatchConnection _connection;
        private readonly IPlayerHistory _history;
        private readonly MatchSession _match;
        private readonly IEventBus _bus;
        private readonly Action<NetworkStateChanged> _onNetwork;
        private Task<bool> _warming;
        private string _warmingUrl;
        private int _generation;
        private bool _disposed;

        public GuestHistoryCoordinator(IConfigProvider config, IMatchConnection connection, IPlayerHistory history,
            MatchSession match, IEventBus bus)
        {
            _config = config; _connection = connection; _history = history; _match = match; _bus = bus;
            _onNetwork = OnNetwork;
        }
        public void Initialize() { _bus.Subscribe(_onNetwork); Warm(_config.Current.ApiBaseUrl); }
        public void Dispose() { _disposed = true; _generation++; _bus.Unsubscribe(_onNetwork); }
        private void Warm(string url)
        {
            if (_warming != null && _warmingUrl == url && (!_warming.IsCompleted || _warming.Status == TaskStatus.RanToCompletion && _warming.Result)) return;
            _warmingUrl = url; _warming = _history.EnsureGuestAsync(url);
        }
        private void OnNetwork(NetworkStateChanged change)
        {
            if (change.Current == NetworkState.Connecting) { _generation++; _match.DeferHello = true; Warm(_connection.LastRequest.Config.ApiBaseUrl); }
            if (change.Current == NetworkState.Joined) Bind(_generation, _connection.AttemptId);
            if (change.Current == NetworkState.Disconnected || change.Current == NetworkState.Failed) _generation++;
        }
        private async void Bind(int generation, Guid attempt)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            try
            {
                if (_warming != null)
                {
                    var completed = await Task.WhenAny(_warming, Task.Delay(Timeout.Infinite, deadline.Token));
                    if (completed != _warming) return;
                    await _warming;
                }
                if (_disposed || generation != _generation || !_match.Connected) return;
                var request = _connection.LastRequest;
                var ticket = await _history.TicketAsync(attempt, request.RoomCode, request.AccessToken, deadline.Token);
                if (!_disposed && generation == _generation && _match.Connected) _match.AttachIdentity(ticket);
            }
            catch (Exception e) when (!(e is OutOfMemoryException)) { /* Optional history never turns into a gameplay exception. */ }
            finally
            {
                // AttachIdentity and Hello share reliable channel 1, so the server binds before freezing roster.
                // Timeout/API failure always releases capability announcement for anonymous RoomCode play.
                if (!_disposed && generation == _generation && _match.Connected) _match.AnnounceReady();
            }
        }
    }
}
