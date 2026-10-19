using UnityEngine;

namespace JungleMaze.App.Ports
{
    /// <summary>
    /// Pool prefab Unity: mỗi prefab một pool. Component IPoolable trên prefab được gọi OnSpawned/OnDespawned.
    /// Object mới tạo được inject ([Inject]) một lần trước lần Spawn đầu tiên.
    /// </summary>
    public interface IPoolService
    {
        T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component;
        /// <summary>Trả object về pool; object không đến từ pool sẽ bị Destroy kèm cảnh báo.</summary>
        void Despawn(Component instance);
        /// <summary>Tự Despawn sau delaySeconds (VFX, đạn), không cần coroutine riêng.</summary>
        void Despawn(Component instance, float delaySeconds);
        void Prewarm<T>(T prefab, int count) where T : Component;
        int CountActive(Component prefab);
        int CountInactive(Component prefab);
    }
}
