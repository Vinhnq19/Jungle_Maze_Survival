using System;
using System.Collections.Generic;
using JungleMaze.Core.Events;
using JungleMaze.Core.Logging;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace JungleMaze.Tests.Core
{
    public sealed class EventBusTests
    {
        private readonly struct Ping { public readonly int Value; public Ping(int value) => Value = value; }

        private sealed class CaptureLog : ILog
        {
            public readonly List<string> Errors = new List<string>();
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) => Errors.Add(message);
        }

        [Test]
        public void Publish_DeliversToAllSubscribers()
        {
            var bus = new EventBus();
            var sum = 0;
            bus.Subscribe<Ping>(e => sum += e.Value);
            bus.Subscribe<Ping>(e => sum += e.Value * 10);
            bus.Publish(new Ping(2));
            Assert.AreEqual(22, sum);
        }

        [Test]
        public void SubscribeTwice_InvokedOnce_UnsubscribeStops()
        {
            var bus = new EventBus();
            var calls = 0;
            Action<Ping> handler = _ => calls++;
            bus.Subscribe(handler);
            bus.Subscribe(handler);
            bus.Publish(new Ping(1));
            bus.Unsubscribe(handler);
            bus.Publish(new Ping(1));
            Assert.AreEqual(1, calls);
            Assert.AreEqual(0, bus.HandlerCount<Ping>());
        }

        [Test]
        public void UnsubscribeDuringPublish_IsSafe()
        {
            var bus = new EventBus();
            var calls = new List<string>();
            Action<Ping> second = _ => calls.Add("second");
            Action<Ping> first = null;
            first = _ => { calls.Add("first"); bus.Unsubscribe(first); bus.Unsubscribe(second); };
            bus.Subscribe(first);
            bus.Subscribe(second);
            bus.Publish(new Ping(1));
            bus.Publish(new Ping(1));
            CollectionAssert.AreEqual(new[] { "first" }, calls);
        }

        [Test]
        public void HandlerException_IsLogged_OtherHandlersStillRun()
        {
            var log = new CaptureLog();
            var bus = new EventBus(log);
            var reached = false;
            bus.Subscribe<Ping>(_ => throw new InvalidOperationException("boom"));
            bus.Subscribe<Ping>(_ => reached = true);
            bus.Publish(new Ping(1));
            Assert.IsTrue(reached);
            Assert.AreEqual(1, log.Errors.Count);
        }

        [Test]
        public void Publish_StructEvent_DoesNotAllocate()
        {
            var bus = new EventBus();
            var sum = 0;
            bus.Subscribe<Ping>(e => sum += e.Value);
            bus.Publish(new Ping(0)); // warm-up dictionary/JIT
            Assert.That(() =>
            {
                for (var i = 0; i < 1000; i++) bus.Publish(new Ping(i));
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
