using System;
using JungleMaze.App.Ports;
using JungleMaze.Core.Factory;
using UnityEngine;

namespace JungleMaze.Infrastructure.Pooling
{
    /// <summary>
    /// Factory dùng chung: lấy instance từ pool (đã inject). Factory theo tính năng (ProjectileFactory...)
    /// nhận class này qua constructor rồi cấu hình thêm cho product.
    /// </summary>
    public sealed class PooledPrefabFactory<T> : IFactory<SpawnPose, T>, IFactory<T> where T : Component
    {
        private readonly IPoolService _pool;
        private readonly T _prefab;

        public PooledPrefabFactory(IPoolService pool, T prefab, int prewarm = 0)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            if (prewarm > 0) _pool.Prewarm(_prefab, prewarm);
        }

        public T Prefab => _prefab;

        public T Create(in SpawnPose pose) => _pool.Spawn(_prefab, pose.Position, pose.Rotation, pose.Parent);

        public T Create() => _pool.Spawn(_prefab, Vector3.zero, Quaternion.identity);

        public void Release(T instance) => _pool.Despawn(instance);
    }
}
