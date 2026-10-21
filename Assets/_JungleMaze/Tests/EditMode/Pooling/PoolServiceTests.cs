using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using JungleMaze.Core.Events;
using JungleMaze.Core.Pooling;
using JungleMaze.App.Pooling;
using JungleMaze.Infrastructure.Pooling;
using JungleMaze.Infrastructure.Ticking;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JungleMaze.Tests.Pooling
{
    public sealed class PoolServiceTests
    {
        /// <summary>Component mẫu: nhận dependency qua [Inject] và đếm callback pool.
        /// ExecuteAlways để Unity gọi Awake cả trong EditMode test.</summary>
        [ExecuteAlways]
        private sealed class Bullet : MonoBehaviour, IPoolable
        {
            public IEventBus Bus;
            public int Injected, Spawned, Despawned, AwakeCalls;
            public bool HadBusInAwake;

            [Inject]
            public void Construct(IEventBus bus) { Bus = bus; Injected++; }

            private void Awake() { AwakeCalls++; HadBusInAwake = Bus != null; }
            public void OnSpawned() => Spawned++;
            public void OnDespawned() => Despawned++;
        }

        private GameObject _prefabGo;
        private Bullet _prefab;
        private PoolService _pool;
        private EventBus _bus;

        [SetUp]
        public void SetUp()
        {
            _prefabGo = new GameObject("BulletPrefab");
            _prefabGo.SetActive(false); // prefab "asset" giả: không Awake ngoài scene
            _prefab = _prefabGo.AddComponent<Bullet>();
            var registry = new ServiceRegistry();
            _bus = new EventBus();
            registry.Register<IEventBus>(_bus);
            _pool = new PoolService(new Injector(registry), null);
        }

        [TearDown]
        public void TearDown()
        {
            _pool.Dispose();
            Object.DestroyImmediate(_prefabGo);
            foreach (var leftover in Object.FindObjectsByType<Bullet>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(leftover.gameObject);
        }

        [Test]
        public void Spawn_InjectsBeforeAwake_ThenReuses()
        {
            var first = _pool.Spawn(_prefab, new Vector3(1, 2, 3), Quaternion.identity);
            Assert.IsTrue(first.gameObject.activeSelf);
            Assert.AreSame(_bus, first.Bus);
            Assert.IsTrue(first.HadBusInAwake, "Dependency phải có trước Awake");
            Assert.AreEqual(new Vector3(1, 2, 3), first.transform.position);
            Assert.AreEqual(1, _pool.CountActive(_prefab));

            _pool.Despawn(first);
            Assert.IsFalse(first.gameObject.activeSelf);
            Assert.AreEqual(1, _pool.CountInactive(_prefab));

            var second = _pool.Spawn(_prefab, Vector3.zero, Quaternion.identity);
            Assert.AreSame(first, second);
            Assert.AreEqual(1, second.Injected, "Inject chỉ một lần cho mỗi instance");
            Assert.AreEqual(1, second.AwakeCalls);
            Assert.AreEqual(2, second.Spawned);
            Assert.AreEqual(1, second.Despawned);
        }

        [Test]
        public void Prewarm_CreatesInactiveInstances()
        {
            _pool.Prewarm(_prefab, 5);
            Assert.AreEqual(5, _pool.CountInactive(_prefab));
            Assert.AreEqual(0, _pool.CountActive(_prefab));
        }

        [Test]
        public void DoubleDespawn_LogsError_DoesNotDuplicate()
        {
            var pool = new PoolService(null, new JungleMaze.Infrastructure.Logging.UnityLog());
            try
            {
                var bullet = pool.Spawn(_prefab, Vector3.zero, Quaternion.identity);
                pool.Despawn(bullet);
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("hai lần"));
                pool.Despawn(bullet);
                Assert.AreEqual(1, pool.CountInactive(_prefab));
            }
            finally { pool.Dispose(); }
        }

        [Test]
        public void DelayedDespawn_ReturnsAfterDelay_IgnoresStaleRespawn()
        {
            var bullet = _pool.Spawn(_prefab, Vector3.zero, Quaternion.identity);
            _pool.Despawn(bullet, 0.5f);
            _pool.Tick(0.3f);
            Assert.IsTrue(bullet.gameObject.activeSelf);
            _pool.Tick(0.3f);
            Assert.IsFalse(bullet.gameObject.activeSelf);

            // Hẹn despawn rồi trả sớm + spawn lại: lịch cũ không được trả nhầm lượt mới.
            var again = _pool.Spawn(_prefab, Vector3.zero, Quaternion.identity);
            _pool.Despawn(again, 0.5f);
            _pool.Despawn(again);
            var third = _pool.Spawn(_prefab, Vector3.zero, Quaternion.identity);
            _pool.Tick(1f);
            Assert.IsTrue(third.gameObject.activeSelf);
        }

        [Test]
        public void Factory_SpawnsAtPose()
        {
            var factory = new PooledPrefabFactory<Bullet>(_pool, _prefab, prewarm: 2);
            Assert.AreEqual(2, _pool.CountInactive(_prefab));
            var bullet = factory.Create(SpawnPose.At(new Vector3(5, 0, 0)));
            Assert.AreEqual(5f, bullet.transform.position.x);
            factory.Release(bullet);
            Assert.AreEqual(0, _pool.CountActive(_prefab));
        }

        [Test]
        public void TickRunner_RemoveDuringTick_AppliesNextFrame()
        {
            var go = new GameObject("TickRunner");
            try
            {
                var runner = go.AddComponent<TickRunner>();
                var counter = new SelfRemovingTickable(runner);
                runner.Add(counter);
                runner.TickAll(0.1f);
                runner.TickAll(0.1f);
                Assert.AreEqual(1, counter.Calls);
                Assert.AreEqual(0, runner.Count);
            }
            finally { Object.DestroyImmediate(go); }
        }

        private sealed class SelfRemovingTickable : JungleMaze.Core.Ticking.ITickable
        {
            private readonly TickRunner _runner;
            public int Calls;
            public SelfRemovingTickable(TickRunner runner) => _runner = runner;
            public void Tick(float deltaTime) { Calls++; _runner.Remove(this); }
        }
    }
}
