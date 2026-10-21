using System;
using JungleMaze.Core.Pooling;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace JungleMaze.Tests.Core
{
    public sealed class ObjectPoolTests
    {
        private sealed class Item : IPoolable
        {
            public int Spawned, Despawned;
            public void OnSpawned() => Spawned++;
            public void OnDespawned() => Despawned++;
        }

        [Test]
        public void GetRelease_ReusesInstance_AndCallsPoolable()
        {
            var created = 0;
            var pool = new ObjectPool<Item>(() => { created++; return new Item(); });
            var first = pool.Get();
            pool.Release(first);
            var second = pool.Get();
            Assert.AreSame(first, second);
            Assert.AreEqual(1, created);
            Assert.AreEqual(2, second.Spawned);
            Assert.AreEqual(1, second.Despawned);
            Assert.AreEqual(1, pool.CountActive);
        }

        [Test]
        public void Prewarm_CreatesInactiveItems()
        {
            var pool = new ObjectPool<Item>(() => new Item(), prewarm: 5);
            Assert.AreEqual(5, pool.CountInactive);
            Assert.AreEqual(0, pool.CountActive);
        }

        [Test]
        public void DoubleRelease_Throws()
        {
            var pool = new ObjectPool<Item>(() => new Item());
            var item = pool.Get();
            pool.Release(item);
            Assert.Throws<InvalidOperationException>(() => pool.Release(item));
        }

        [Test]
        public void GetRelease_AfterWarmup_DoesNotAllocate()
        {
            var pool = new ObjectPool<Item>(() => new Item(), prewarm: 16);
            var items = new Item[16];
            Assert.That(() =>
            {
                for (var round = 0; round < 100; round++)
                {
                    for (var i = 0; i < items.Length; i++) items[i] = pool.Get();
                    for (var i = 0; i < items.Length; i++) pool.Release(items[i]);
                }
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
