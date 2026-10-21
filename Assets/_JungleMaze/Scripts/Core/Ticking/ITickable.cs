namespace JungleMaze.Core.Ticking
{
    /// <summary>Service thuần C# cần chạy mỗi frame; TickRunner gọi thay cho việc mỗi service có Update riêng.</summary>
    public interface ITickable
    {
        void Tick(float deltaTime);
    }

    /// <summary>Nơi đăng ký ITickable; Infrastructure cung cấp bản chạy theo Update của Unity.</summary>
    public interface ITickScheduler
    {
        void Add(ITickable tickable);
        void Remove(ITickable tickable);
    }
}
