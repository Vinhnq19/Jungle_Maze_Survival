using JungleMaze.Core.DI;
using UnityEngine;
using UnityEngine.Profiling;

namespace JungleMaze.Infrastructure.Network
{
    /// <summary>
    /// Vỏ MonoBehaviour của NetworkClient: poll mạng trong Update, chạy trước gameplay (execution order sớm),
    /// gửi disconnect khi thoát game. Logic nằm trong NetworkClient để test được không cần scene.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class NetworkManager : MonoBehaviour
    {
        private NetworkClient _client;

        public NetworkClient Client => _client;

        [Inject]
        public void Construct(NetworkClient client) => _client = client;

        private void Update()
        {
            if (_client == null) return;
            // Marker để đo GC Alloc của vòng mạng trong Profiler (tiêu chí 0 B của CLI-02).
            Profiler.BeginSample("Network.Poll");
            _client.Tick(Time.unscaledDeltaTime);
            Profiler.EndSample();
        }

        private void OnApplicationQuit() => _client?.Dispose();
        private void OnDestroy() => _client?.Dispose();
    }
}
