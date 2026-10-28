using System;
using System.Collections.Generic;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Hud.State;
using JungleMaze.App.Match;
using JungleMaze.App.Flow;
using JungleMaze.App.MatchUi;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Hud;
using JungleMaze.Domain.MatchUi;
using JungleMaze.Domain.Network;
using JungleMaze.Presentation.UI;
using JungleMaze.Tests.MatchUi;
using NUnit.Framework;
using UnityEngine;

namespace JungleMaze.Tests.Hud
{
    public sealed class MatchHudBindingTests
    {
        private LiveMatchHarness _harness;
        private EventBus _bus;
        private HudStateStore _store;
        private MatchHudBinding _binding;
        private readonly List<DamageTaken> _damageEvents = new List<DamageTaken>();

        [SetUp]
        public void SetUp()
        {
            _harness = new LiveMatchHarness();
            _bus = new EventBus();
            _store = new HudStateStore(_bus, StoreTestUtil.Balance());
            _store.Initialize();
            _damageEvents.Clear();
            _store.DamageTaken += d => _damageEvents.Add(d);
            _binding = new MatchHudBinding(_harness.Match, _store);
        }

        [TearDown]
        public void TearDown()
        {
            _binding?.Dispose();
            _store?.Dispose();
            _harness?.Dispose();
        }

        [Test]
        public void InitialNoSnapshot_AvoidsWritingZero_RetainsHudDefaults()
        {
            // Initial state before any snapshots or InGame state
            _binding.Tick(0.1f);

            Assert.AreEqual(100, _store.Hp, "Should retain MaxHp defaults instead of writing 0");
            Assert.IsFalse(_store.MatchStatus.HasAlive, "Alive count should not be populated before snapshots");
            Assert.IsFalse(_store.MatchStatus.HasZoneCountdown, "Zone countdown should not be populated before zone packet");
            Assert.AreEqual(0, _damageEvents.Count, "No damage events before snapshots");
        }

        [Test]
        public void OwnSnapshot_PopulatesHp_DoesNotReportDamageOnInit()
        {
            _harness.Start(ownNetId: 0, count: 2);
            _binding.Tick(0.1f);

            Assert.AreEqual(100, _store.Hp);
            Assert.AreEqual(0, _damageEvents.Count, "Initial snapshot must not report damage");
        }

        [Test]
        public void HpDecrease_ReportsDamageTakenExactlyOnce_DedupesOnUnchanged()
        {
            _harness.Start(ownNetId: 0, count: 2);
            _binding.Tick(0.1f);
            Assert.AreEqual(0, _damageEvents.Count);

            // First damage taken: 100 -> 75
            _harness.Tick += 3;
            var packet = new WorldSnapshotPacket { MatchId = _harness.MatchId, ServerTick = _harness.Tick, PlayerCount = 2 };
            packet.SetPlayer(0, new PlayerStatePacket { Tick = _harness.Tick, NetId = 0, X = 5, Y = 10, Hp = 75 });
            packet.SetPlayer(1, new PlayerStatePacket { Tick = _harness.Tick, NetId = 1, X = 8, Y = 10, Hp = 100 });
            _harness.Network.Push(packet);

            _binding.Tick(0.1f);
            Assert.AreEqual(75, _store.Hp);
            Assert.AreEqual(1, _damageEvents.Count, "Damage taken should fire exactly once on decrease");

            // Tick again with unchanged HP: no duplicate event
            _binding.Tick(0.1f);
            Assert.AreEqual(1, _damageEvents.Count, "No duplicate damage report on unchanged HP tick");

            // Another snapshot at higher tick with same HP: still no duplicate event
            _harness.Tick += 3;
            var packet2 = new WorldSnapshotPacket { MatchId = _harness.MatchId, ServerTick = _harness.Tick, PlayerCount = 2 };
            packet2.SetPlayer(0, new PlayerStatePacket { Tick = _harness.Tick, NetId = 0, X = 5, Y = 10, Hp = 75 });
            packet2.SetPlayer(1, new PlayerStatePacket { Tick = _harness.Tick, NetId = 1, X = 8, Y = 10, Hp = 100 });
            _harness.Network.Push(packet2);

            _binding.Tick(0.1f);
            Assert.AreEqual(1, _damageEvents.Count, "No duplicate damage report across snapshots with identical HP");

            // Second real damage decrease: 75 -> 40
            _harness.Tick += 3;
            var packet3 = new WorldSnapshotPacket { MatchId = _harness.MatchId, ServerTick = _harness.Tick, PlayerCount = 2 };
            packet3.SetPlayer(0, new PlayerStatePacket { Tick = _harness.Tick, NetId = 0, X = 5, Y = 10, Hp = 40 });
            packet3.SetPlayer(1, new PlayerStatePacket { Tick = _harness.Tick, NetId = 1, X = 8, Y = 10, Hp = 100 });
            _harness.Network.Push(packet3);

            _binding.Tick(0.1f);
            Assert.AreEqual(40, _store.Hp);
            Assert.AreEqual(2, _damageEvents.Count, "Second real HP decrease fires damage event");
        }

        [Test]
        public void HpHeal_UpdatesHpWithoutDamageNotification()
        {
            _harness.Start(ownNetId: 0, count: 2);
            _binding.Tick(0.1f);

            // Damage down to 50
            _harness.Tick += 3;
            var packet = new WorldSnapshotPacket { MatchId = _harness.MatchId, ServerTick = _harness.Tick, PlayerCount = 2 };
            packet.SetPlayer(0, new PlayerStatePacket { Tick = _harness.Tick, NetId = 0, X = 5, Y = 10, Hp = 50 });
            packet.SetPlayer(1, new PlayerStatePacket { Tick = _harness.Tick, NetId = 1, X = 8, Y = 10, Hp = 100 });
            _harness.Network.Push(packet);
            _binding.Tick(0.1f);
            Assert.AreEqual(50, _store.Hp);
            Assert.AreEqual(1, _damageEvents.Count);

            // Healing up to 90
            _harness.Tick += 3;
            var healPacket = new WorldSnapshotPacket { MatchId = _harness.MatchId, ServerTick = _harness.Tick, PlayerCount = 2 };
            healPacket.SetPlayer(0, new PlayerStatePacket { Tick = _harness.Tick, NetId = 0, X = 5, Y = 10, Hp = 90 });
            healPacket.SetPlayer(1, new PlayerStatePacket { Tick = _harness.Tick, NetId = 1, X = 8, Y = 10, Hp = 100 });
            _harness.Network.Push(healPacket);
            _binding.Tick(0.1f);

            Assert.AreEqual(90, _store.Hp);
            Assert.AreEqual(1, _damageEvents.Count, "Healing must not report damage");
        }

        [Test]
        public void AliveCount_PopulatesFromRosterAndSnapshots_ExcludesDeadPlayers()
        {
            _harness.Start(ownNetId: 0, count: 3);

            // 3 players started; NetId 2 is dead
            _harness.Snapshot(new byte[] { 0, 1 }, new byte[] { 2 });
            _binding.Tick(0.1f);

            Assert.IsTrue(_store.MatchStatus.HasAlive);
            Assert.AreEqual(2, _store.MatchStatus.Alive);
            Assert.AreEqual(3, _store.MatchStatus.Total);

            // NetId 1 also dies
            _harness.Snapshot(new byte[] { 0 }, new byte[] { 1, 2 });
            _binding.Tick(0.1f);

            Assert.AreEqual(1, _store.MatchStatus.Alive);
            Assert.AreEqual(3, _store.MatchStatus.Total);
        }

        [Test]
        public void ZoneCountdown_CalculatesAuthoritativeCountdown_AndClampsExpired()
        {
            _harness.Start(ownNetId: 0, count: 2);

            // Warning / Waiting phase: shrink starts in 600 ticks (20s) and ends in 1500 ticks (50s)
            var startTick = _harness.Tick + 600;
            var endTick = _harness.Tick + 1500;
            _harness.Network.Push(new ZoneStatePacket
            {
                MatchId = _harness.MatchId,
                Tick = _harness.Tick,
                ShrinkStartTick = startTick,
                ShrinkEndTick = endTick,
                CurrentRadius = 25f,
                TargetRadius = 15f
            });

            _binding.Tick(0.1f);
            Assert.IsTrue(_store.MatchStatus.HasZoneCountdown);
            Assert.AreEqual(20f, _store.MatchStatus.ZoneSecondsRemaining, 0.1f, "Countdown to shrink start");

            // Advance session tick into active shrink: 300 ticks before endTick (10s remaining)
            for (var i = 0; i < 1200; i++)
            {
                _harness.Match.Tick(1f / 30f);
            }
            _binding.Tick(0.1f);
            Assert.AreEqual(10f, _store.MatchStatus.ZoneSecondsRemaining, 0.2f, "Countdown to shrink end");

            // Advance past endTick: expired / r0 countdown clamped to 0
            for (var i = 0; i < 400; i++)
            {
                _harness.Match.Tick(1f / 30f);
            }
            _binding.Tick(0.1f);
            Assert.AreEqual(0f, _store.MatchStatus.ZoneSecondsRemaining, "Expired countdown clamped to 0");
        }

        [Test]
        public void MatchResetAndDisconnect_ResetsHudAndState_NextMatchInitialisesCleanly()
        {
            _harness.Start(ownNetId: 0, count: 2);
            _binding.Tick(0.1f);
            Assert.IsTrue(_store.MatchStatus.HasAlive);

            // Disconnect
            _harness.Network.SetState(NetworkState.Disconnected, DisconnectReason.None);
            _binding.Tick(0.1f);

            Assert.AreEqual(100, _store.Hp, "HUD HP reset to default max");
            Assert.IsFalse(_store.MatchStatus.HasAlive, "Alive count reset");
            Assert.IsFalse(_store.MatchStatus.HasZoneCountdown, "Zone countdown reset");

            // Start a new match with a new MatchId
            var match2Id = Guid.NewGuid();
            _harness.Network.SetState(NetworkState.Joined, DisconnectReason.None);
            _harness.Network.Push(new MatchPhasePacket { MatchId = match2Id, Phase = MatchPhase.InGame, ServerTick = 500, MaxPlayers = 10, MinPlayers = 1, PlayerCount = 2 });
            _harness.Network.Push(new ResumeTokenPacket { MatchId = match2Id, NetId = 0, ResumeToken = Guid.NewGuid() });

            var players = new MatchParticipantState[]
            {
                new MatchParticipantState { NetId = 0, DisplayName = "P0" },
                new MatchParticipantState { NetId = 1, DisplayName = "P1" }
            };
            _harness.Network.Push(new MatchRosterPacket { MatchId = match2Id, Players = players });

            var snap = new WorldSnapshotPacket { MatchId = match2Id, ServerTick = 503, PlayerCount = 2 };
            snap.SetPlayer(0, new PlayerStatePacket { Tick = 503, NetId = 0, X = 5, Y = 10, Hp = 80 });
            snap.SetPlayer(1, new PlayerStatePacket { Tick = 503, NetId = 1, X = 8, Y = 10, Hp = 100 });
            _harness.Network.Push(snap);

            _binding.Tick(0.1f);
            Assert.AreEqual(80, _store.Hp);
            Assert.AreEqual(0, _damageEvents.Count, "First snapshot of new match must not report damage");
        }

        [Test]
        public void SafeDisposal_CanDisposeSafelyWithoutExceptions()
        {
            Assert.DoesNotThrow(() => _binding.Dispose());
            Assert.DoesNotThrow(() => _binding.Dispose(), "Double dispose must be safe");
            Assert.DoesNotThrow(() => _binding.Tick(0.1f), "Tick after dispose must not throw");
        }

        [Test]
        public void MatchDemoView_ShouldHideWorldLabels_WhenMenuOrOverlayOpen()
        {
            var host = new GameObject("demoViewTest");
            var demo = host.AddComponent<MatchDemoView>();
            var menu = new MatchMenuController(_bus, () => 0d, () => _harness.MatchId, AppScreen.InMatch);
            var spectator = new MatchSessionSpectatorSource(_harness.Match, _harness.Network, null, 100);
            var elimination = new MatchSessionEliminationSource(_harness.Match, _harness.Network);
            var flow = new MatchOverlayFlow(_bus, () => 0d, elimination, spectator, AppScreen.InMatch);

            try
            {
                demo.BindOverlay(menu, flow);
                Assert.IsFalse(demo.ShouldHideWorldLabels, "World labels visible during ordinary gameplay");

                // ESC menu opened
                menu.Open();
                Assert.IsTrue(demo.ShouldHideWorldLabels, "World labels hidden while ESC menu is open");

                menu.Close();
                Assert.IsFalse(demo.ShouldHideWorldLabels, "World labels visible when menu closes");

                // Spectating phase
                _harness.Start(ownNetId: 0, count: 2);
                spectator.Tick(0f);
                _harness.Snapshot(new byte[] { 1 }, new byte[] { 0 });
                spectator.Tick(0f);
                elimination.Tick(0f);
                Assert.AreEqual(MatchOverlayPhase.Eliminated, flow.Phase);
                flow.Spectate();
                Assert.AreEqual(MatchOverlayPhase.Spectating, flow.Phase);
                Assert.IsTrue(demo.ShouldHideWorldLabels, "World labels hidden while spectating");
            }
            finally
            {
                menu.Dispose();
                spectator.Dispose();
                elimination.Dispose();
                flow.Dispose();
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
