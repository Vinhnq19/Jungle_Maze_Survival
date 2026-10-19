using JungleMaze.Core.Pooling;
using UnityEngine;

namespace JungleMaze.Infrastructure.Pooling
{
    /// <summary>
    /// Gắn tự động lên instance do PoolService tạo: biết pool gốc (Despawn không tra dictionary)
    /// và cache các IPoolable để Spawn/Despawn không gọi GetComponents mỗi lần.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PooledObject : MonoBehaviour
    {
        internal PoolService Owner;
        internal int PrefabId;
        internal IPoolable[] Poolables;
        /// <summary>Tăng mỗi lần Spawn; Despawn trễ của lượt cũ không được trả nhầm object đã spawn lại.</summary>
        internal int SpawnVersion;
        internal bool IsSpawned;

        internal void NotifySpawned()
        {
            for (var i = 0; i < Poolables.Length; i++) Poolables[i].OnSpawned();
        }

        internal void NotifyDespawned()
        {
            for (var i = 0; i < Poolables.Length; i++) Poolables[i].OnDespawned();
        }

        // Instance bị Destroy cùng scene (vẫn đang dùng) phải được gỡ khỏi bộ đếm của pool.
        private void OnDestroy() => Owner?.OnInstanceDestroyed(this);
    }
}
