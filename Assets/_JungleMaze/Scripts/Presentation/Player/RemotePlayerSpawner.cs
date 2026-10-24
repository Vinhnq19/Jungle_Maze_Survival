using System;
using JungleMaze.App.Pooling;
using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Player;
using UnityEngine;

namespace JungleMaze.Presentation.Player
{
    /// <summary>
    /// Mẫu đủ các pattern: nhận dependency qua [Inject], nghe EventBus (Observer), tạo view bằng Factory lấy từ Pool,
    /// đọc Domain model. Đặt trong scene Game, gán prefab PlayerView.
    /// </summary>
    public sealed class RemotePlayerSpawner : MonoBehaviour
    {
        [SerializeField] private PlayerView playerPrefab;
        [SerializeField] private int prewarm = 10;

        private readonly PlayerView[] _views = new PlayerView[256];
        private IEventBus _bus;
        private PlayerRegistry _players;
        private PooledPrefabFactory<PlayerView> _factory;
        private Action<PlayerStateUpdated> _onUpdated;
        private Action<PlayerRemoved> _onRemoved;

        [Inject]
        public void Construct(IEventBus bus, IPoolService pool, PlayerRegistry players)
        {
            _bus = bus;
            _players = players;
            if (playerPrefab != null) _factory = new PooledPrefabFactory<PlayerView>(pool, playerPrefab, prewarm);
            _onUpdated = OnPlayerUpdated;
            _onRemoved = OnPlayerRemoved;
            if (isActiveAndEnabled) Subscribe(); // inject xảy ra sau OnEnable khi scene load
        }

        public PlayerView GetView(byte id) => _views[id];

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            if (_bus == null) return;
            _bus.Unsubscribe(_onUpdated);
            _bus.Unsubscribe(_onRemoved);
            for (var i = 0; i < _views.Length; i++)
            {
                if (_views[i] == null) continue;
                _factory?.Release(_views[i]);
                _views[i] = null;
            }
        }

        private void Subscribe()
        {
            if (_bus == null) return;
            _bus.Subscribe(_onUpdated);
            _bus.Subscribe(_onRemoved);
            // Người chơi đã có trước khi scene load (ví dụ snapshot đến sớm) cũng được hiện.
            for (var i = 0; i < _players.ActiveIds.Count; i++) OnPlayerUpdated(new PlayerStateUpdated(_players.ActiveIds[i]));
        }

        private void OnPlayerUpdated(PlayerStateUpdated evt)
        {
            if (_factory == null || _views[evt.NetId] != null || !_players.TryGet(evt.NetId, out var model)) return;
            var view = _factory.Create(new SpawnPose(Vector3.zero, Quaternion.identity, transform));
            view.Bind(model);
            _views[evt.NetId] = view;
        }

        private void OnPlayerRemoved(PlayerRemoved evt)
        {
            var view = _views[evt.NetId];
            if (view == null) return;
            _factory.Release(view);
            _views[evt.NetId] = null;
        }
    }
}
