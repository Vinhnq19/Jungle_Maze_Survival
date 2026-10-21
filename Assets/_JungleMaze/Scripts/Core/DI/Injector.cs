using System;
using System.Collections.Generic;
using System.Reflection;

namespace JungleMaze.Core.DI
{
    /// <summary>Đánh dấu method nhận dependency (method injection) cho object Unity tự tạo, như MonoBehaviour.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class InjectAttribute : Attribute { }

    /// <summary>
    /// Gọi mọi method [Inject] của target, mỗi tham số lấy từ resolver theo kiểu.
    /// Reflection chỉ chạy lúc tạo object (scene load/pool tạo mới), không chạy mỗi lần Spawn.
    /// </summary>
    public sealed class Injector
    {
        private static readonly Dictionary<Type, MethodInfo[]> MethodCache = new Dictionary<Type, MethodInfo[]>();
        private readonly IServiceResolver _resolver;

        public Injector(IServiceResolver resolver) =>
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

        public void Inject(object target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            foreach (var method in GetInjectMethods(target.GetType()))
            {
                var parameters = method.GetParameters();
                var args = new object[parameters.Length];
                for (var i = 0; i < parameters.Length; i++)
                {
                    var type = parameters[i].ParameterType;
                    if (!_resolver.IsRegistered(type))
                        throw new InvalidOperationException(
                            $"{target.GetType().Name}.{method.Name} cần {type.Name} nhưng Bootstrap chưa đăng ký.");
                    args[i] = _resolver.Resolve(type);
                }
                method.Invoke(target, args);
            }
        }

        private static MethodInfo[] GetInjectMethods(Type type)
        {
            if (MethodCache.TryGetValue(type, out var cached)) return cached;
            var result = new List<MethodInfo>();
            // Duyệt từ lớp cha xuống để base class được inject trước lớp con.
            var chain = new Stack<Type>();
            for (var t = type; t != null && t != typeof(object); t = t.BaseType) chain.Push(t);
            while (chain.Count > 0)
            {
                var current = chain.Pop();
                foreach (var method in current.GetMethods(BindingFlags.Instance | BindingFlags.Public
                             | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (method.IsDefined(typeof(InjectAttribute), false)) result.Add(method);
            }
            var methods = result.ToArray();
            MethodCache[type] = methods;
            return methods;
        }
    }
}
