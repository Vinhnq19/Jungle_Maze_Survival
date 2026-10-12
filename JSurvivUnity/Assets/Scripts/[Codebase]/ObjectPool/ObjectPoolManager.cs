using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _Codebase_.ObjectPool
{
    public class ObjectPoolManager
    {
        private readonly Dictionary<GameObject, ObjectPool> pools = new();

        private ObjectPoolManager()
        {
        }

        public static ObjectPoolManager I { get; } = new();

        public ObjectPool this[GameObject prefab] => this.GetOrAddPool(prefab);

        public ObjectPool GetOrAddPool(GameObject prefab)
        {
            if (this.pools.TryGetValue(prefab, out var pool))
            {
                return pool;
            }

            pool = new(prefab);

            this.pools.Add(prefab, pool);

            return pool;
        }

        public bool Free(GameObject obj)
        {
            return this.pools.Any(pool => pool.Value.Free(obj));
        }
    }
}