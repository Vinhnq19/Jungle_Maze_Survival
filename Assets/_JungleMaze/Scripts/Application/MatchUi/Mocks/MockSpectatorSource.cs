#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using JungleMaze.Domain.MatchUi;

namespace JungleMaze.App.MatchUi.Mocks
{
    /// <summary>
    /// Dev-only <see cref="ISpectatorSource"/> with known positions: up to 9 other players, each can move, be
    /// eliminated or the whole match can end. Used by the F7 panel and the camera/list tests. Not in Release builds.
    /// </summary>
    public sealed class MockSpectatorSource : ISpectatorSource
    {
        public const int MaxPlayers = 10;
        private static readonly string[] Names = { "Bạn", "Vinh", "Hào", "Vũ", "Lan", "Minh", "Thảo", "Khoa", "Ngọc", "Tùng" };

        private readonly SpectatorEntry[] _entries = new SpectatorEntry[MaxPlayers];
        private readonly float[] _x = new float[MaxPlayers];
        private readonly float[] _y = new float[MaxPlayers];
        private readonly bool[] _alive = new bool[MaxPlayers];
        private readonly List<SpectatorEntry> _list = new List<SpectatorEntry>(MaxPlayers);

        public byte LocalNetId { get; private set; }
        public int StartCount { get; private set; }
        public int AliveCount => _list.Count;
        public bool MatchEnded { get; private set; }
        public event Action<byte> PlayerEliminated;
        public event Action Changed;

        /// <summary><paramref name="aliveOthers"/> players alive besides the local one, spread over the 50×50 map.</summary>
        public void Populate(int aliveOthers, byte localNetId = 0, int startCount = MaxPlayers, string weapon = "Súng nước")
        {
            Array.Clear(_alive, 0, _alive.Length);
            LocalNetId = localNetId;
            StartCount = startCount;
            MatchEnded = false;
            var placed = 0;
            for (byte id = 0; id < MaxPlayers && placed < aliveOthers; id++)
            {
                if (id == localNetId) continue;
                _entries[id] = new SpectatorEntry(id, Names[id], true, (byte)(100 - id * 7), 100, id % 2 == 0 ? weapon : null);
                _x[id] = 6f + (id % 4) * 11f;
                _y[id] = 8f + (id / 4) * 14f;
                _alive[id] = true;
                placed++;
            }
            Rebuild();
            Changed?.Invoke();
        }

        public void SetPosition(byte netId, float x, float y)
        {
            if (netId >= MaxPlayers) return;
            _x[netId] = x;
            _y[netId] = y;
        }

        public void SetHp(byte netId, byte hp)
        {
            if (netId >= MaxPlayers || !_alive[netId]) return;
            var e = _entries[netId];
            _entries[netId] = new SpectatorEntry(netId, e.Name, e.HasHp, hp, e.MaxHp, e.WeaponName);
            Rebuild();
            Changed?.Invoke();
        }

        public void Eliminate(byte netId)
        {
            if (netId >= MaxPlayers || !_alive[netId]) return;
            _alive[netId] = false;
            Rebuild();
            PlayerEliminated?.Invoke(netId);
            Changed?.Invoke();
        }

        public void EndMatch()
        {
            MatchEnded = true;
            Changed?.Invoke();
        }

        public void Reset()
        {
            Array.Clear(_alive, 0, _alive.Length);
            StartCount = 0;
            MatchEnded = false;
            _list.Clear();
        }

        public SpectatorEntry GetAlive(int index) => _list[index];

        public bool TryGetEntry(byte netId, out SpectatorEntry entry)
        {
            entry = netId < MaxPlayers && _alive[netId] ? _entries[netId] : default;
            return netId < MaxPlayers && _alive[netId];
        }

        public bool TryGetPosition(byte netId, out float x, out float y)
        {
            var ok = netId < MaxPlayers && _alive[netId];
            x = ok ? _x[netId] : 0f;
            y = ok ? _y[netId] : 0f;
            return ok;
        }

        private void Rebuild()
        {
            _list.Clear();
            for (byte id = 0; id < MaxPlayers; id++) if (_alive[id]) _list.Add(_entries[id]);
        }
    }
}
#endif
