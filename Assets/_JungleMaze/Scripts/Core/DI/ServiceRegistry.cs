using System;
using System.Collections.Generic;
using JungleMaze.Core.Logging;

namespace JungleMaze.Core.DI
{
    /// <summary>
    /// Danh bạ service của composition root. Chỉ Bootstrap, Factory và SceneContext gọi Resolve;
    /// code gameplay nhận dependency qua constructor hoặc method [Inject].
    /// </summary>
    public sealed class ServiceRegistry : IServiceResolver, IDisposable
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        // Một instance có thể đăng ký dưới nhiều interface; vòng đời chỉ chạy một lần, theo thứ tự đăng ký.
        private readonly List<object> _lifecycleOrder = new List<object>();
        private readonly HashSet<object> _lifecycleSet = new HashSet<object>(ReferenceEqualityComparer.Instance);
        private readonly ILog _log;
        private bool _initialized, _disposed;

        public ServiceRegistry(ILog log = null) => _log = log ?? NullLog.Instance;

        public void Register<TService>(TService instance) where TService : class
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ServiceRegistry));
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (_services.ContainsKey(typeof(TService)))
                throw new InvalidOperationException($"Service {typeof(TService).Name} đã được đăng ký.");
            _services.Add(typeof(TService), instance);
            if (_lifecycleSet.Add(instance))
            {
                _lifecycleOrder.Add(instance);
                // Service đăng ký muộn (sau InitializeAll) vẫn được khởi tạo ngay để không bị bỏ sót.
                if (_initialized && instance is IInitializable initializable) initializable.Initialize();
            }
        }

        public TService Resolve<TService>() where TService : class
        {
            if (TryResolve(out TService service)) return service;
            throw new InvalidOperationException($"Chưa đăng ký service {typeof(TService).Name} trong Bootstrap.");
        }

        public bool TryResolve<TService>(out TService service) where TService : class
        {
            if (_services.TryGetValue(typeof(TService), out var value)) { service = (TService)value; return true; }
            service = null;
            return false;
        }

        public object Resolve(Type serviceType)
        {
            if (_services.TryGetValue(serviceType, out var value)) return value;
            throw new InvalidOperationException($"Chưa đăng ký service {serviceType.Name} trong Bootstrap.");
        }

        public bool IsRegistered(Type serviceType) => _services.ContainsKey(serviceType);

        /// <summary>Gọi Initialize theo đúng thứ tự Register; lỗi một service dừng boot để không chạy nửa vời.</summary>
        public void InitializeAll()
        {
            if (_initialized) return;
            _initialized = true;
            for (var i = 0; i < _lifecycleOrder.Count; i++)
                if (_lifecycleOrder[i] is IInitializable initializable) initializable.Initialize();
        }

        /// <summary>Dispose ngược thứ tự đăng ký: service phụ thuộc được dọn trước dependency của nó.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (var i = _lifecycleOrder.Count - 1; i >= 0; i--)
            {
                if (!(_lifecycleOrder[i] is IDisposable disposable)) continue;
                try { disposable.Dispose(); }
                catch (Exception e) { _log.Error($"Dispose {_lifecycleOrder[i].GetType().Name} lỗi: {e}"); }
            }
            _services.Clear();
            _lifecycleOrder.Clear();
            _lifecycleSet.Clear();
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
