using System;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Match;
using JungleMaze.Core.Ticking;

namespace JungleMaze.App.Hud.State
{
    /// <summary>
    /// Read-only binding from authoritative MatchSession to IHudStateWriter.
    /// Drives own HP from validated snapshots, alive/total count from roster/snapshots,
    /// and zone countdown from authoritative zone ticks.
    /// Safely disposes and resets on match change, disconnect, or non-InGame state.
    /// </summary>
    public sealed class MatchHudBinding : ITickable, IDisposable
    {
        private readonly MatchSession _session;
        private readonly IHudStateWriter _hud;

        private Guid _lastMatchId;
        private bool _wasConnected;
        private bool _disposed;
        private bool _wasInGame;
        private int? _lastKnownHp;
        private int _startTotal;

        public MatchHudBinding(MatchSession session, IHudStateWriter hud)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _hud = hud ?? throw new ArgumentNullException(nameof(hud));
        }

        public void Tick(float deltaTime)
        {
            if (_disposed) return;
            if (!_session.Connected)
            {
                if (_wasConnected || _wasInGame)
                {
                    _wasConnected = false;
                    ResetState(true);
                }
                return;
            }
            _wasConnected = true;

            if (_session.MatchId != _lastMatchId)
            {
                _lastMatchId = _session.MatchId;
                ResetState(true);
            }

            if (_session.Phase != MatchPhase.InGame)
            {
                if (_wasInGame)
                {
                    ResetState(true);
                }
                return;
            }

            _wasInGame = true;

            UpdateHp();
            UpdateAliveCount();
            UpdateZoneCountdown();
            // Note: Hero pose is already written to IHudStateWriter by LocalHeroPoseReporter; no duplicate writer added here.
        }

        private void UpdateHp()
        {
            // Populate own HP only from validated snapshots; avoid writing zero before own snapshot exists.
            if (_session.OwnNetId == 255 ||
                !_session.Players.TryGet(_session.OwnNetId, out var ownPlayer) ||
                !ownPlayer.HasState)
            {
                return;
            }

            var currentHp = ownPlayer.Current.Hp;
            if (!_lastKnownHp.HasValue)
            {
                // Initialising snapshot: set HP without reporting damage
                _lastKnownHp = currentHp;
                _hud.SetHp(currentHp);
            }
            else if (currentHp < _lastKnownHp.Value)
            {
                // Real HP decrease observed: notify damage taken exactly once per change
                _lastKnownHp = currentHp;
                _hud.SetHp(currentHp);
                _hud.NotifyDamageTaken(false, 0f);
            }
            else if (currentHp > _lastKnownHp.Value)
            {
                // Healing: update HP without reporting damage
                _lastKnownHp = currentHp;
                _hud.SetHp(currentHp);
            }
        }

        private void UpdateAliveCount()
        {
            // Authoritative total from initial roster or active snapshot count.
            var roster = _session.Roster;
            if (roster != null && roster.Length > _startTotal)
            {
                _startTotal = roster.Length;
            }
            else if (_startTotal == 0 && _session.Players.ActiveIds.Count > 0)
            {
                _startTotal = _session.Players.ActiveIds.Count;
            }

            // Note: Current snapshots contain all participants; future FOW (fog of war) may hide unspotted enemies,
            // at which point alive count will depend on authoritative server counter.
            var aliveCount = 0;
            var activeIds = _session.Players.ActiveIds;
            for (var i = 0; i < activeIds.Count; i++)
            {
                if (_session.Players.TryGet(activeIds[i], out var player) &&
                    player.HasState &&
                    !player.Current.IsDead &&
                    player.Current.Hp > 0)
                {
                    aliveCount++;
                }
            }

            if (_startTotal > 0 || _session.Snapshots > 0)
            {
                var total = Math.Max(_startTotal, aliveCount);
                if (total > 0)
                {
                    _hud.SetAliveCount(aliveCount, total);
                }
            }
        }

        private void UpdateZoneCountdown()
        {
            if (!_session.HasZone || _session.Zone.MatchId != _session.MatchId)
            {
                return;
            }

            var zone = _session.Zone;
            var currentTick = _session.ServerTick;
            var secondsRemaining = 0f;

            if (unchecked((int)(zone.ShrinkStartTick - currentTick)) > 0)
            {
                secondsRemaining = unchecked((int)(zone.ShrinkStartTick - currentTick)) / 30f;
            }
            else if (unchecked((int)(zone.ShrinkEndTick - currentTick)) > 0)
            {
                secondsRemaining = unchecked((int)(zone.ShrinkEndTick - currentTick)) / 30f;
            }
            else
            {
                // Expired / r0
                secondsRemaining = 0f;
            }

            if (secondsRemaining < 0f) secondsRemaining = 0f;
            _hud.SetZoneCountdown(secondsRemaining);
        }

        private void ResetState(bool resetHud)
        {
            _lastKnownHp = null;
            _startTotal = 0;
            _wasInGame = false;
            if (resetHud)
            {
                _hud.Reset();
            }
        }

        public void Dispose()
        {
            _disposed = true;
            ResetState(false);
            _lastMatchId = Guid.Empty;
            _wasConnected = false;
        }
    }
}
