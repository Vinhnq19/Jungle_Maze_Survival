using System;
using System.Linq;
using JungleMaze.App.Flow;
using JungleMaze.App.MatchUi;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Network;
using JungleMaze.Tests.App;
using NUnit.Framework;

namespace JungleMaze.Tests.MatchUi
{
    public sealed class MatchMenuControllerTests
    {
        private static readonly Guid Match = new Guid("6f1c0b9e-1111-4a2b-9c3d-000000000003");
        private EventBus _bus;
        private FakeClock _clock;
        private FakeSettingsStore _store;
        private PlayerSettingsService _settings;
        private MatchMenuController _menu;
        private Recorder<LeaveMatchRequested> _leaves;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _clock = new FakeClock();
            _store = new FakeSettingsStore();
            _settings = new PlayerSettingsService(_store, _bus);
            _menu = new MatchMenuController(_bus, _clock.Read, () => Match, AppScreen.InMatch, _settings);
            _leaves = new Recorder<LeaveMatchRequested>();
            _bus.Subscribe(_leaves.Handler);
        }

        [TearDown]
        public void TearDown() => _menu.Dispose();

        private void Screen(AppScreen from, AppScreen to) => _bus.Publish(new AppScreenChanged(from, to, null));

        private void Advance(double seconds)
        {
            _clock.Advance(seconds);
            _menu.Tick((float)seconds);
        }

        // AC4 (controller side): open blocks gameplay input, close releases it at once.
        [Test]
        public void OpenBlocksGameplayInput_CloseReleasesIt()
        {
            var changes = new Recorder<bool>();
            _menu.OpenChanged += changes.Handler;
            Assert.IsFalse(_menu.BlocksGameplayInput);
            Assert.IsTrue(_menu.PressEscape());
            Assert.IsTrue(_menu.IsOpen);
            Assert.IsTrue(_menu.BlocksGameplayInput);
            Assert.IsTrue(_menu.PressEscape());
            Assert.IsFalse(_menu.BlocksGameplayInput);
            CollectionAssert.AreEqual(new[] { true, false }, changes.Items);
            _menu.Open();
            _menu.Close();
            Assert.IsFalse(_menu.IsOpen);
        }

        // AC5
        [TestCase(AppScreen.Boot)]
        [TestCase(AppScreen.MainMenu)]
        [TestCase(AppScreen.Connecting)]
        [TestCase(AppScreen.InRoom)]
        [TestCase(AppScreen.Results)]
        public void EscOnlyInMatch(AppScreen screen)
        {
            var menu = new MatchMenuController(_bus, _clock.Read, () => Match, screen);
            Assert.IsFalse(menu.PressEscape());
            menu.Open();
            Assert.IsFalse(menu.IsOpen);
            Assert.IsFalse(menu.BlocksGameplayInput);
            menu.Dispose();
        }

        // AC5
        [Test]
        public void ClosesOnScreenChange()
        {
            _menu.Open();
            _menu.RequestLeave();
            Screen(AppScreen.InMatch, AppScreen.Results);
            Assert.IsFalse(_menu.IsOpen);
            Assert.IsFalse(_menu.IsConfirmOpen);
            Assert.IsFalse(_menu.PressEscape());
            Screen(AppScreen.Results, AppScreen.InMatch);
            Assert.IsTrue(_menu.PressEscape(), "works again in the next match");
        }

        // AC6
        [Test]
        public void EscClosesConfirmFirst()
        {
            _menu.PressEscape();
            _menu.RequestLeave();
            Assert.IsTrue(_menu.IsConfirmOpen);
            _menu.PressEscape();
            Assert.IsFalse(_menu.IsConfirmOpen, "first ESC closes the confirm box");
            Assert.IsTrue(_menu.IsOpen, "menu still open");
            _menu.PressEscape();
            Assert.IsFalse(_menu.IsOpen, "second ESC closes the menu");
            Assert.AreEqual(0, _leaves.Count);
        }

        // AC15 (logic): "Ở lại" closes the box without a request.
        [Test]
        public void Stay_DoesNotPublish()
        {
            _menu.Open();
            _menu.RequestLeave();
            _menu.CancelLeave();
            Assert.IsFalse(_menu.IsConfirmOpen);
            Assert.IsTrue(_menu.IsOpen);
            Assert.AreEqual(0, _leaves.Count);
        }

        // AC16
        [Test]
        public void ConfirmFiveTimes_OneRequest_UnlockAfter5s()
        {
            _menu.Open();
            _menu.RequestLeave();
            for (var i = 0; i < 5; i++)
            {
                _menu.ConfirmLeave();
                _menu.RequestLeave();
                Advance(0.04);
            }
            Assert.AreEqual(1, _leaves.Count, "five clicks in 200 ms, one request");
            Assert.AreEqual(Match, _leaves.Items[0].MatchId);
            Assert.AreEqual(LeaveMatchSource.MatchMenu, _leaves.Items[0].Source);
            Assert.IsTrue(_menu.IsLeaveLocked);
            Assert.IsFalse(_menu.IsConfirmOpen, "locked: the confirm box cannot reopen");
            Assert.IsFalse(_menu.LeaveWithoutConfirm(LeaveMatchSource.Spectator));

            Advance(4.7);
            Assert.IsTrue(_menu.IsLeaveLocked, "still locked before 5 s");
            Assert.IsFalse(_menu.ShowRetryNotice);
            Advance(0.2);
            Assert.IsFalse(_menu.IsLeaveLocked, "unlocked after 5 s");
            Assert.IsTrue(_menu.ShowRetryNotice, "Chưa thoát được, thử lại");

            _menu.RequestLeave();
            _menu.ConfirmLeave();
            Assert.AreEqual(2, _leaves.Count, "retry publishes again");
            Assert.IsFalse(_menu.ShowRetryNotice);
        }

        [Test]
        public void ScreenChangeUnlocks()
        {
            Assert.IsTrue(_menu.LeaveWithoutConfirm(LeaveMatchSource.EliminatedScreen));
            Assert.IsTrue(_menu.IsLeaveLocked);
            Screen(AppScreen.InMatch, AppScreen.MainMenu);
            Assert.IsFalse(_menu.IsLeaveLocked);
            Advance(10);
            Assert.IsFalse(_menu.ShowRetryNotice);
        }

        // AC12: closing the menu forces the pending settings write.
        [Test]
        public void Close_FlushesSettings()
        {
            _menu.Open();
            _settings.SetMouseSensitivity(1.6f);
            Assert.AreEqual(0, _store.Saves);
            _menu.Close();
            Assert.AreEqual(1, _store.Saves);
        }
    }

    /// <summary>AC17: the leave request has no side effect of its own.</summary>
    public sealed class LeaveMatchContractTests
    {
        [Test]
        public void NoListener_OneEvent_NoNetworkCall_ScreenUnchanged()
        {
            var bus = new EventBus();
            var network = new FakeNetwork(bus);
            network.SetState(NetworkState.Joined, DisconnectReason.None);
            var screens = new Recorder<AppScreenChanged>();
            var leaves = new Recorder<LeaveMatchRequested>();
            bus.Subscribe(screens.Handler);
            bus.Subscribe(leaves.Handler);
            var menu = new MatchMenuController(bus, () => 0d, () => Guid.Empty, AppScreen.InMatch);

            Assert.IsTrue(menu.LeaveWithoutConfirm(LeaveMatchSource.Spectator));
            Assert.AreEqual(1, leaves.Count);
            Assert.AreEqual(NetworkState.Joined, network.State, "UI-03 does not disconnect");
            Assert.AreEqual(0, network.Sent, "UI-03 sends no packet itself");
            Assert.AreEqual(0, screens.Count, "UI-03 does not change AppScreen");
            menu.Dispose();
        }

        [Test]
        public void Controller_HasNoNetworkOrFlowDependency()
        {
            var forbidden = new[] { "INetworkClient", "IPacketSender", "GameFlow", "MatchSession", "ConnectionMenuPresenter" };
            foreach (var ctor in typeof(MatchMenuController).GetConstructors())
            foreach (var p in ctor.GetParameters())
                Assert.IsFalse(forbidden.Contains(p.ParameterType.Name), p.ParameterType.Name);
            foreach (var f in typeof(MatchMenuController).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
                Assert.IsFalse(forbidden.Contains(f.FieldType.Name), f.FieldType.Name);
        }
    }
}
