using System;
using System.Collections.Generic;
using JungleMaze.Core.DI;
using NUnit.Framework;

namespace JungleMaze.Tests.Core
{
    public sealed class ServiceRegistryTests
    {
        private interface IFoo { }
        private interface IBar { }

        private sealed class Service : IFoo, IBar, IInitializable, IDisposable
        {
            private readonly List<string> _log;
            private readonly string _name;
            public Service(List<string> log, string name) { _log = log; _name = name; }
            public void Initialize() => _log.Add("init:" + _name);
            public void Dispose() => _log.Add("dispose:" + _name);
        }

        private sealed class Consumer
        {
            public IFoo Foo;
            public IBar Bar;
            [Inject] private void Construct(IFoo foo, IBar bar) { Foo = foo; Bar = bar; }
        }

        [Test]
        public void Resolve_ReturnsRegisteredInstance()
        {
            var registry = new ServiceRegistry();
            var service = new Service(new List<string>(), "a");
            registry.Register<IFoo>(service);
            Assert.AreSame(service, registry.Resolve<IFoo>());
            Assert.IsFalse(registry.TryResolve<IBar>(out _));
        }

        [Test]
        public void Register_SameTypeTwice_Throws()
        {
            var registry = new ServiceRegistry();
            registry.Register<IFoo>(new Service(new List<string>(), "a"));
            Assert.Throws<InvalidOperationException>(() => registry.Register<IFoo>(new Service(new List<string>(), "b")));
        }

        [Test]
        public void Resolve_Missing_ThrowsWithServiceName()
        {
            var error = Assert.Throws<InvalidOperationException>(() => new ServiceRegistry().Resolve<IFoo>());
            StringAssert.Contains(nameof(IFoo), error.Message);
        }

        [Test]
        public void Lifecycle_InitInOrder_DisposeReverse_OncePerInstance()
        {
            var log = new List<string>();
            var registry = new ServiceRegistry();
            var a = new Service(log, "a");
            registry.Register<IFoo>(a);
            registry.Register<IBar>(a); // cùng instance dưới hai interface
            registry.Register(new Service(log, "b"));
            registry.InitializeAll();
            registry.Dispose();
            CollectionAssert.AreEqual(new[] { "init:a", "init:b", "dispose:b", "dispose:a" }, log);
        }

        [Test]
        public void RegisterAfterInitialize_InitializesImmediately()
        {
            var log = new List<string>();
            var registry = new ServiceRegistry();
            registry.InitializeAll();
            registry.Register<IFoo>(new Service(log, "late"));
            CollectionAssert.AreEqual(new[] { "init:late" }, log);
        }

        [Test]
        public void Injector_CallsInjectMethodWithResolvedServices()
        {
            var registry = new ServiceRegistry();
            var service = new Service(new List<string>(), "a");
            registry.Register<IFoo>(service);
            registry.Register<IBar>(service);
            var consumer = new Consumer();
            new Injector(registry).Inject(consumer);
            Assert.AreSame(service, consumer.Foo);
            Assert.AreSame(service, consumer.Bar);
        }

        [Test]
        public void Injector_MissingDependency_ThrowsClearMessage()
        {
            var registry = new ServiceRegistry();
            registry.Register<IFoo>(new Service(new List<string>(), "a"));
            var error = Assert.Throws<InvalidOperationException>(() => new Injector(registry).Inject(new Consumer()));
            StringAssert.Contains(nameof(IBar), error.Message);
        }
    }
}
