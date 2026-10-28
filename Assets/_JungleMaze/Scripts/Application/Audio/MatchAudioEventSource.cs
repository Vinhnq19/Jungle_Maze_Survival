using System;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Match;
using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using JungleMaze.Core.Ticking;
using JungleMaze.Domain.Audio;

namespace JungleMaze.App.Audio
{
    /// <summary>Read-only view of the match state the audio adapter needs; lets tests fake the match without a network.</summary>
    public interface IMatchAudioFeed
    {
        /// <summary>255 = unknown.</summary>
        byte OwnNetId { get; }
        bool TryGetZone(out bool inWarning, out byte phaseIndex);
    }

    /// <summary><see cref="IMatchAudioFeed"/> over the live <see cref="MatchSession"/>.</summary>
    public sealed class MatchSessionAudioFeed : IMatchAudioFeed
    {
        private readonly MatchSession _match;

        private readonly OfflineMatchSession _offline;
        public MatchSessionAudioFeed(MatchSession match, OfflineMatchSession offline = null)
        { _match = match; _offline = offline; if (match == null && offline == null) throw new ArgumentNullException(nameof(match)); }

        public byte OwnNetId => _offline?.IsActive == true ? (byte)0 : _match.OwnNetId;

        public bool TryGetZone(out bool inWarning, out byte phaseIndex)
        {
            if (_offline?.IsActive == true)
            { inWarning = _offline.Zone.InWarning; phaseIndex = _offline.Zone.PhaseIndex; return true; }
            var has = _match != null && _match.HasZone;
            var zone = _match != null ? _match.Zone : default;
            inWarning = has && zone.InWarning;
            phaseIndex = has ? zone.PhaseIndex : (byte)0;
            return has;
        }
    }

    /// <summary>
    /// Turns network state that has no client event yet into keyed cues, without changing MatchSession or GameFlow:
    /// <c>EventPacket.PlayerDied</c> → <see cref="SfxId.Eliminated"/> (2D for the local player, at the victim's pose
    /// otherwise) and the zone warning rising edge → <see cref="SfxId.ZoneBell3"/>, once per <c>PhaseIndex</c>.
    /// PlayerHit/ItemPickedUp/MatchEnded are deliberately not mapped: those sounds come from the HUD ports or
    /// AppScreenChanged, and mapping both would play them twice (T-8).
    /// </summary>
    public sealed class MatchAudioEventSource : IAudioEventSource, IInitializable, ITickable, IDisposable
    {
        public const byte KindPlayerDied = 1;
        public const byte KindZoneWarning = 2;
        private const int NoPhase = -1;

        private readonly IPacketReceiver _receiver;
        private readonly IMatchAudioFeed _feed;
        private readonly IPlayerPoseSource _poses;
        private readonly Action<EventPacket> _onEvent;
        private bool _wasWarning;
        private int _belledPhase = NoPhase;
        private bool _subscribed;
        private readonly OfflineMatchSession _offline;
        private bool _wasOfflineDead;

        public MatchAudioEventSource(IPacketReceiver receiver, IMatchAudioFeed feed, IPlayerPoseSource poses, OfflineMatchSession offline = null)
        {
            _offline = offline;
            _receiver = receiver;
            _feed = feed;
            _poses = poses;
            _onEvent = OnEvent;
        }

        public event Action<AudioCue> CueRaised;

        public void Initialize()
        {
            if (_subscribed || _receiver == null) return;
            _receiver.Subscribe(_onEvent);
            _subscribed = true;
        }

        public void Dispose()
        {
            if (!_subscribed) return;
            _receiver.Unsubscribe(_onEvent);
            _subscribed = false;
        }

        public void Tick(float deltaTime)
        {
            var dead = _offline?.IsActive == true && _offline.IsDead;
            if (dead && !_wasOfflineDead) CueRaised?.Invoke(AudioCue.Global(SfxId.Eliminated));
            _wasOfflineDead = dead;
            if (_feed == null) return;
            if (!_feed.TryGetZone(out var warning, out var phase))
            {
                // No zone (between matches): the next match may reuse phase numbers.
                _wasWarning = false;
                _belledPhase = NoPhase;
                return;
            }
            if (warning && !_wasWarning && phase != _belledPhase)
            {
                _belledPhase = phase;
                CueRaised?.Invoke(AudioCue.Global(SfxId.ZoneBell3, 0, SfxDeduper.Key(KindZoneWarning, phase)));
            }
            _wasWarning = warning;
        }

        /// <summary>Handles one server event; public so tests can feed packets without a network.</summary>
        public void OnEvent(EventPacket packet)
        {
            if (packet.EventType != GameEventType.PlayerDied || packet.TargetNetId == EventPacket.NoTarget) return;
            var key = SfxDeduper.Key(KindPlayerDied, packet.Tick, packet.SourceNetId, packet.TargetNetId);
            var target = packet.TargetNetId;
            if (_feed != null && target == _feed.OwnNetId)
            {
                CueRaised?.Invoke(AudioCue.Global(SfxId.Eliminated, target, key));
                return;
            }
            if (_poses != null && _poses.TryGetPose(target, out var pose))
                CueRaised?.Invoke(AudioCue.At(SfxId.Eliminated, pose.X, pose.Y, target, key));
        }
    }
}
