using System;
using System.Collections.Generic;
using JungleMaze.App.Audio;
using JungleMaze.App.Flow;
using JungleMaze.App.MatchUi;
using JungleMaze.App.MatchUi.Mocks;
using JungleMaze.App.Player;
using JungleMaze.App.Ports;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Audio;
using JungleMaze.Domain.Player;
using JungleMaze.Presentation.UI;
using JungleMaze.Presentation.UI.MatchOverlay;
using JungleMaze.Tests.App;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JungleMaze.Tests.MatchUi
{
    /// <summary>AC4 (EditMode part): the temporary N5 guard over public API only.</summary>
    public sealed class MatchMenuInputGuardTests
    {
        private EventBus _bus;
        private MatchMenuController _menu;
        private GameObject _host;
        private MatchDemoView _demo;
        private PlayerInputService _offline;
        private List<PlayerIntent> _intents;
        private MatchMenuInputGuard _guard;

        private sealed class Poses : IPlayerPoseSource
        {
            public bool TryGetPose(byte netId, out PlayerPose pose)
            {
                pose = new PlayerPose { X = 3, Y = 4, RotationDegrees = 135f };
                return netId == 2;
            }
        }

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _menu = new MatchMenuController(_bus, () => 0d, () => Guid.Empty, AppScreen.InMatch);
            _host = new GameObject("demo");
            _demo = _host.AddComponent<MatchDemoView>();
            _offline = new PlayerInputService(new FakeNetwork(_bus)) { Enabled = true };
            _intents = new List<PlayerIntent>();
            _guard = new MatchMenuInputGuard(_menu, _intents.Add, () => 2, new Poses(), _offline, () => _demo, () => _menu.InMatch);
        }

        [TearDown]
        public void TearDown()
        {
            _guard.Dispose();
            _menu.Dispose();
            Object.DestroyImmediate(_host);
        }

        [Test]
        public void MenuOpen_NeutralIntent_KeepsAim_DemoStopsWriting()
        {
            _menu.Open();
            Assert.IsTrue(_guard.Engaged);
            Assert.IsTrue(_demo.UseExternalIntent, "the demo stops writing intent");
            Assert.AreEqual(1, _intents.Count);
            var intent = _intents[0];
            Assert.AreEqual(0f, intent.MoveX);
            Assert.AreEqual(0f, intent.MoveY);
            Assert.IsFalse(intent.Attack);
            Assert.IsFalse(intent.Dash || intent.Interact || intent.Reload || intent.UseItem);
            Assert.AreEqual(135f, intent.AimDegrees, "aim stays on the current facing");
            Assert.IsFalse(_offline.Enabled, "offline sender paused");
        }

        [Test]
        public void MenuClose_RestoresAtOnce()
        {
            _menu.Open();
            _menu.Close();
            Assert.IsFalse(_guard.Engaged);
            Assert.IsFalse(_demo.UseExternalIntent, "input works again from the next frame");
            Assert.IsTrue(_offline.Enabled);
        }

        [Test]
        public void KeepsAnExternalRunnerFlag()
        {
            _demo.UseExternalIntent = true;
            _menu.Open();
            _menu.Close();
            Assert.IsTrue(_demo.UseExternalIntent, "an acceptance runner keeps control");
        }

        [Test]
        public void LeavingTheMatch_DoesNotReenableOfflineSender()
        {
            _menu.Open();
            _bus.Publish(new AppScreenChanged(AppScreen.InMatch, AppScreen.Results, null));
            Assert.IsFalse(_guard.Engaged);
            Assert.IsFalse(_offline.Enabled, "InMatchState owns the flag outside a match");
            Assert.IsFalse(_demo.UseExternalIntent);
        }
    }
}
