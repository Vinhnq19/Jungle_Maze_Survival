using System;
using JungleMaze.Domain.MatchUi;

namespace JungleMaze.App.MatchUi
{
    /// <summary>
    /// Who the spectator is watching. Order is the source's NetId order, so next/previous wrap the same way every
    /// time (AC23). When the watched player is eliminated the next one in that order takes over in the same call
    /// (G-4, AC26). "Kẻ đã hạ bạn" works only while the killer is still alive.
    /// </summary>
    public sealed class SpectatorTargets
    {
        public const byte None = EliminationInfo.NoNetId;

        private ISpectatorSource _source;

        public SpectatorTargets(ISpectatorSource source) => _source = source;

        public byte Current { get; private set; } = None;
        public bool HasTarget => Current != None;

        /// <summary>Name of the current target, kept so "&lt;name&gt; đã bị loại" still works after it left the list.</summary>
        public string CurrentName { get; private set; }

        public byte KillerNetId { get; set; } = None;

        public bool CanJumpToKiller => KillerNetId != None && KillerNetId != Current && _source != null
            && _source.TryGetEntry(KillerNetId, out _);

        /// <summary>netId, automatic (true when the previous target was eliminated).</summary>
        public event Action<byte, bool> TargetChanged;

        public void SetSource(ISpectatorSource source)
        {
            _source = source;
            Clear();
        }

        /// <summary>Starts on <paramref name="preferred"/> if alive, else the first alive player.</summary>
        public bool Begin(byte preferred = None)
        {
            if (_source == null || _source.AliveCount == 0) { Set(None, false); return false; }
            if (preferred != None && _source.TryGetEntry(preferred, out _)) Set(preferred, false);
            else Set(_source.GetAlive(0).NetId, false);
            return true;
        }

        public void Next() => Step(+1);
        public void Previous() => Step(-1);

        public bool Select(byte netId)
        {
            if (_source == null || !_source.TryGetEntry(netId, out _)) return false;
            if (netId != Current) Set(netId, false);
            return true;
        }

        public bool JumpToKiller() => CanJumpToKiller && Select(KillerNetId);

        /// <summary>Called when a player left the alive list. Returns true if it was the watched one.</summary>
        public bool OnPlayerEliminated(byte netId)
        {
            if (netId != Current) return false;
            var next = FirstAfter(netId);
            Set(next, true);
            return true;
        }

        public void Clear()
        {
            Current = None;
            CurrentName = null;
            KillerNetId = None;
        }

        private void Step(int direction)
        {
            if (_source == null || _source.AliveCount == 0) return;
            var count = _source.AliveCount;
            var index = IndexOf(Current);
            if (index < 0) { Set(_source.GetAlive(direction > 0 ? 0 : count - 1).NetId, false); return; }
            if (count == 1) return;
            var target = ((index + direction) % count + count) % count;
            Set(_source.GetAlive(target).NetId, false);
        }

        /// <summary>First alive NetId greater than <paramref name="netId"/>, wrapping to the lowest.</summary>
        private byte FirstAfter(byte netId)
        {
            if (_source == null || _source.AliveCount == 0) return None;
            for (var i = 0; i < _source.AliveCount; i++)
            {
                var id = _source.GetAlive(i).NetId;
                if (id > netId) return id;
            }
            return _source.GetAlive(0).NetId;
        }

        private int IndexOf(byte netId)
        {
            if (netId == None) return -1;
            for (var i = 0; i < _source.AliveCount; i++) if (_source.GetAlive(i).NetId == netId) return i;
            return -1;
        }

        private void Set(byte netId, bool automatic)
        {
            Current = netId;
            CurrentName = netId != None && _source != null && _source.TryGetEntry(netId, out var entry) ? entry.Name : null;
            TargetChanged?.Invoke(netId, automatic);
        }
    }
}
