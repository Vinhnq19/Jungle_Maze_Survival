using System;

namespace JungleMaze.Core.DI
{
    /// <summary>Chỉ đọc service; Factory/SceneContext nhận interface này thay vì cả registry.</summary>
    public interface IServiceResolver
    {
        TService Resolve<TService>() where TService : class;
        bool TryResolve<TService>(out TService service) where TService : class;
        object Resolve(Type serviceType);
        bool IsRegistered(Type serviceType);
    }

    /// <summary>Service cần chạy logic sau khi mọi dependency đã đăng ký xong.</summary>
    public interface IInitializable
    {
        void Initialize();
    }
}
