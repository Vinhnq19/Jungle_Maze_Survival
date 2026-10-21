using System;
using System.Collections.Generic;
using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using JungleMaze.Core.Logging;
using JungleMaze.Core.Pooling;
using JungleMaze.Core.Ticking;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JungleMaze.Infrastructure.Pooling
{
    /// <summary>
    /// Pool prefab theo instance ID của prefab. Instance mới được tạo dưới một root inactive nên Awake chưa chạy
    /// cho tới lần Spawn đầu, và dependency [Inject] đã có trước Awake/OnEnable.
    /// </summary>
    public sealed class PoolService : IPoolService, ITickable, IDisposable
    {
        private sealed class Pool
        {
            public GameObject Prefab;
            public readonly Stack<PooledObject> Inactive = new Stack<PooledObject>();
            public int Active;
        }

        private struct DelayedDespawn
        {
            public PooledObject Instance;
            public int SpawnVersion;
            public float Remaining;
        }

        private readonly Dictionary<int, Pool> _pools = new Dictionary<int, Pool>();
        private readonly List<DelayedDespawn> _delayed = new List<DelayedDespawn>();
        private readonly Injector _injector;
        private readonly ILog _log;
        private readonly Transform _root;
        private bool _disposed;

        /// <param name="injector">Có thể null nếu prefab không có method [Inject].</param>
        public PoolService(Injector injector, ILog log, Transform parentForRoot = null)
        {
            _injector = injector;
            _log = log ?? NullLog.Instance;
            var root = new GameObject("[Pools]");
            root.SetActive(false); // inactive: instance mới chưa Awake cho tới khi được Spawn ra ngoài
            if (parentForRoot != null) root.transform.SetParent(parentForRoot, false);
            _root = root.transform;
        }

        public T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            var pool = GetPool(prefab.gameObject);
            var instance = pool.Inactive.Count > 0 ? pool.Inactive.Pop() : Create(pool);
            var transform = instance.transform;
            transform.SetParent(parent, false);
            transform.SetPositionAndRotation(position, rotation);
            instance.IsSpawned = true;
            instance.SpawnVersion++;
            pool.Active++;
            instance.gameObject.SetActive(true);
            instance.NotifySpawned();
            return instance.GetComponent<T>();
        }

        public void Despawn(Component instance)
        {
            if (instance == null) return;
            var pooled = instance.GetComponent<PooledObject>();
            if (pooled == null || pooled.Owner != this)
            {
                _log.Warn($"[Pool] {instance.name} không đến từ pool, Destroy thay vì Despawn.");
                DestroyObject(instance.gameObject);
                return;
            }
            if (!pooled.IsSpawned)
            {
                _log.Error($"[Pool] {instance.name} bị Despawn hai lần.");
                return;
            }
            pooled.IsSpawned = false;
            pooled.NotifyDespawned();
            pooled.gameObject.SetActive(false);
            pooled.transform.SetParent(_root, false);
            if (_pools.TryGetValue(pooled.PrefabId, out var pool))
            {
                pool.Active--;
                pool.Inactive.Push(pooled);
            }
        }

        public void Despawn(Component instance, float delaySeconds)
        {
            if (instance == null) return;
            var pooled = instance.GetComponent<PooledObject>();
            if (delaySeconds <= 0f || pooled == null) { Despawn(instance); return; }
            _delayed.Add(new DelayedDespawn { Instance = pooled, SpawnVersion = pooled.SpawnVersion, Remaining = delaySeconds });
        }

        public void Prewarm<T>(T prefab, int count) where T : Component
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            var pool = GetPool(prefab.gameObject);
            for (var i = pool.Inactive.Count + pool.Active; i < count; i++) pool.Inactive.Push(Create(pool));
        }

        public int CountActive(Component prefab) =>
            prefab != null && _pools.TryGetValue(prefab.gameObject.GetInstanceID(), out var pool) ? pool.Active : 0;

        public int CountInactive(Component prefab) =>
            prefab != null && _pools.TryGetValue(prefab.gameObject.GetInstanceID(), out var pool) ? pool.Inactive.Count : 0;

        public void Tick(float deltaTime)
        {
            // Duyệt ngược để xóa tại chỗ không cấp phát.
            for (var i = _delayed.Count - 1; i >= 0; i--)
            {
                var entry = _delayed[i];
                entry.Remaining -= deltaTime;
                if (entry.Remaining > 0f) { _delayed[i] = entry; continue; }
                _delayed.RemoveAt(i);
                // Bỏ qua nếu object đã bị trả/spawn lại cho lượt khác hoặc đã bị Destroy.
                if (entry.Instance != null && entry.Instance.IsSpawned && entry.Instance.SpawnVersion == entry.SpawnVersion)
                    Despawn(entry.Instance);
            }
        }

        internal void OnInstanceDestroyed(PooledObject instance)
        {
            if (_disposed || !_pools.TryGetValue(instance.PrefabId, out var pool)) return;
            if (instance.IsSpawned) pool.Active--;
            // Instance inactive chỉ bị Destroy khi root bị hủy (Dispose), lúc đó pool đã bị xóa.
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _delayed.Clear();
            _pools.Clear();
            if (_root != null) DestroyObject(_root.gameObject);
        }

        private Pool GetPool(GameObject prefab)
        {
            var id = prefab.GetInstanceID();
            if (_pools.TryGetValue(id, out var pool)) return pool;
            pool = new Pool { Prefab = prefab };
            _pools.Add(id, pool);
            return pool;
        }

        private PooledObject Create(Pool pool)
        {
            var go = Object.Instantiate(pool.Prefab, _root, false);
            go.name = pool.Prefab.name;
            // Không dùng ?? với UnityEngine.Object: GetComponent trong Editor có thể trả "fake null".
            if (!go.TryGetComponent(out PooledObject pooled)) pooled = go.AddComponent<PooledObject>();
            pooled.Owner = this;
            pooled.PrefabId = pool.Prefab.GetInstanceID();
            pooled.Poolables = go.GetComponentsInChildren<IPoolable>(true);
            if (_injector != null)
            {
                // Inject một lần cho mọi MonoBehaviour có [Inject], trước khi object được kích hoạt.
                foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true)) _injector.Inject(behaviour);
            }
            go.SetActive(false);
            return pooled;
        }

        private static void DestroyObject(GameObject go)
        {
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }
    }
}
