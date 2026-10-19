using System;
using System.Collections.Generic;

namespace JungleMaze.Domain.Player
{
    /// <summary>Tra người chơi theo NetId (byte) bằng mảng cố định: không cấp phát khi cập nhật mỗi tick.</summary>
    public sealed class PlayerRegistry
    {
        private readonly PlayerModel[] _players = new PlayerModel[256];
        private readonly List<byte> _active = new List<byte>(16);

        public int Count => _active.Count;
        public IReadOnlyList<byte> ActiveIds => _active;

        public bool TryGet(byte netId, out PlayerModel player)
        {
            player = _players[netId];
            return player != null;
        }

        public PlayerModel GetOrAdd(byte netId)
        {
            var player = _players[netId];
            if (player != null) return player;
            player = new PlayerModel(netId);
            _players[netId] = player;
            _active.Add(netId);
            return player;
        }

        public bool Remove(byte netId)
        {
            if (_players[netId] == null) return false;
            _players[netId] = null;
            _active.Remove(netId);
            return true;
        }

        /// <summary>Gọi onRemoved cho từng người chơi trước khi xóa (để view tự dọn).</summary>
        public void Clear(Action<byte> onRemoved = null)
        {
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var id = _active[i];
                _players[id] = null;
                onRemoved?.Invoke(id);
            }
            _active.Clear();
        }
    }
}
