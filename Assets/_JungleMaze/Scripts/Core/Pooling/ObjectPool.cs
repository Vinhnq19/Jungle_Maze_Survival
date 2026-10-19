using System;
using System.Collections.Generic;

namespace JungleMaze.Core.Pooling
{
    /// <summary>Object được pool gọi khi lấy ra/trả về; reset trạng thái tại đây thay vì trong Awake/OnDestroy.</summary>
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }

    public interface IObjectPool<T> where T : class
    {
        int CountActive { get; }
        int CountInactive { get; }
        T Get();
        void Release(T item);
    }

    /// <summary>
    /// Pool thuần C# cho object không phải GameObject (buffer, command, model).
    /// Prefab Unity dùng IPoolService ở Application, implement ở Infrastructure.
    /// </summary>
    public sealed class ObjectPool<T> : IObjectPool<T> where T : class
    {
        private readonly Stack<T> _inactive;
        private readonly HashSet<T> _released;
        private readonly Func<T> _create;
        private readonly Action<T> _onGet, _onRelease;
        private readonly int _maxSize;

        public ObjectPool(Func<T> create, Action<T> onGet = null, Action<T> onRelease = null,
            int prewarm = 0, int maxSize = 1024)
        {
            if (maxSize < 1) throw new ArgumentOutOfRangeException(nameof(maxSize));
            if (prewarm < 0 || prewarm > maxSize) throw new ArgumentOutOfRangeException(nameof(prewarm));
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _onGet = onGet;
            _onRelease = onRelease;
            _maxSize = maxSize;
            _inactive = new Stack<T>(prewarm);
            _released = new HashSet<T>();
            for (var i = 0; i < prewarm; i++)
            {
                var item = _create();
                _inactive.Push(item);
                _released.Add(item);
            }
        }

        public int CountActive { get; private set; }
        public int CountInactive => _inactive.Count;

        public T Get()
        {
            T item;
            if (_inactive.Count > 0) { item = _inactive.Pop(); _released.Remove(item); }
            else item = _create();
            CountActive++;
            _onGet?.Invoke(item);
            (item as IPoolable)?.OnSpawned();
            return item;
        }

        public void Release(T item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            // Trả hai lần sẽ khiến hai người cùng dùng một object: báo lỗi sớm.
            if (!_released.Add(item)) throw new InvalidOperationException($"{typeof(T).Name} đã được trả về pool.");
            CountActive--;
            (item as IPoolable)?.OnDespawned();
            _onRelease?.Invoke(item);
            if (_inactive.Count < _maxSize) _inactive.Push(item);
            else _released.Remove(item);
        }
    }
}
