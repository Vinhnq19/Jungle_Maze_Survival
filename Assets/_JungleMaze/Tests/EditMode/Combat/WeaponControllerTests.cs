using System.Collections.Generic;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Combat;
using JungleMaze.App.Combat.Mocks;
using JungleMaze.App.Hud.State;
using JungleMaze.App.Ports;
using JungleMaze.Domain.Balance;
using JungleMaze.Domain.Hud;
using JungleMaze.Infrastructure.Config;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace JungleMaze.Tests.Combat
{
    public sealed class WeaponControllerTests
    {
        private const string TestWeapons = @"[
  { ""id"": ""HAND"", ""displayName"": ""Hand"", ""category"": ""Melee"", ""damage"": 8, ""cooldownSeconds"": 0.5, ""range"": 1, ""arcDegrees"": 90 },
  { ""id"": ""RUBBER_GUN"", ""displayName"": ""Rubber"", ""category"": ""Ranged"", ""damage"": 15, ""range"": 6, ""fireRatePerSecond"": 3, ""projectileSpeed"": 18, ""magazineSize"": 2, ""reserveAmmo"": 3, ""reloadSeconds"": 2 },
  { ""id"": ""SHOTGUN"", ""displayName"": ""Shotgun"", ""category"": ""Ranged"", ""damage"": 9, ""pellets"": 5, ""range"": 4, ""fireRatePerSecond"": 0.8, ""projectileSpeed"": 24, ""magazineSize"": 2, ""reserveAmmo"": 4 }
]";

        private sealed class StaticBalance : IBalanceConfigProvider
        {
            private readonly Dictionary<string, WeaponDefinition> _byId = new Dictionary<string, WeaponDefinition>();

            public StaticBalance(string weaponsJson)
            {
                Assert.IsTrue(BalanceConfigJson.TryParseWeapons(weaponsJson, "test", out var list, out var error), error);
                Weapons = list;
                foreach (var weapon in list) _byId[weapon.Id] = weapon;
            }

            public MatchConfigData Match => null;
            public IReadOnlyList<WeaponDefinition> Weapons { get; }
            public IReadOnlyList<EquipmentDefinition> Equipments => new List<EquipmentDefinition>();
            public bool TryGetWeapon(string id, out WeaponDefinition weapon) => _byId.TryGetValue(id ?? "", out weapon);
            public bool TryGetEquipment(string id, out EquipmentDefinition equipment) { equipment = null; return false; }
            public string LastError => null;
        }

        private sealed class RecordingHud : IHudStateWriter
        {
            public string HeldWeapon;
            public int AmmoInMagazine = -1;
            public int AmmoReserve = -1;

            public void SetHeldWeapon(string weaponId) => HeldWeapon = weaponId;
            public void SetAmmo(int inMagazine, int reserve) { AmmoInMagazine = inMagazine; AmmoReserve = reserve; }
            public void SetHp(int hp) { }
            public void NotifyPickup(string itemId, int count) { }
            public void NotifyHitDealt(HitTargetKind target) { }
            public void NotifyDamageTaken(bool hasDirection, float directionDegrees) { }
            public void SetDanger(int id, DangerKind kind, float remainingSeconds, bool hasSource, float sourceX, float sourceY) { }
            public void ClearDanger(int id) { }
            public void SetArmor(string equipmentId, float durability) { }
            public void SetHelmet(string equipmentId, float durability) { }
            public void SetZoneCountdown(float secondsRemaining) { }
            public void SetAliveCount(int alive, int total) { }
            public void SetPose(float x, float y, float rotationDegrees) { }
            public void Reset() { }
        }

        private sealed class EventLog
        {
            public readonly List<CombatEvent> Events = new List<CombatEvent>();
            public CombatOutcome LastOutcome;
            public void Record(CombatEvent e) { Events.Add(e); LastOutcome = e.Outcome; }
            public bool Has(CombatEventKind kind)
            {
                foreach (var e in Events) if (e.Kind == kind) return true;
                return false;
            }
        }

        private sealed class RecordingSink : ICombatRequestSink
        {
            private readonly ICombatRequestSink _inner;
            public readonly List<AttackRequestPacket> Attacks = new List<AttackRequestPacket>();
            public readonly List<ReloadRequestPacket> Reloads = new List<ReloadRequestPacket>();

            public RecordingSink(ICombatRequestSink inner) => _inner = inner;

            public void SendAttack(AttackRequestPacket packet)
            {
                Attacks.Add(packet);
                _inner.SendAttack(packet);
            }

            public void SendReload(ReloadRequestPacket packet)
            {
                Reloads.Add(packet);
                _inner.SendReload(packet);
            }
        }

        private sealed class Rig
        {
            public readonly FakeCombatBackend Server;
            public readonly FakeCombatPeer Peer;
            public readonly RecordingSink Sink;
            public readonly WeaponController Controller;
            public readonly RecordingHud Hud = new RecordingHud();
            public readonly EventLog Events = new EventLog();

            public Rig(string weaponsJson, byte netId = 1)
            {
                var balance = new StaticBalance(weaponsJson);
                Server = new FakeCombatBackend(balance);
                Peer = Server.AddPeer(netId);
                Sink = new RecordingSink(Peer);
                Controller = new WeaponController(balance, Sink, Hud, netId);
                Controller.Changed += Events.Record;
                Peer.AttackResult += Controller.OnAttackResult;
                Peer.ReloadResult += Controller.OnReloadResult;
            }

            public List<AttackRequestPacket> SentAttacks => Sink.Attacks;
        }

        [Test]
        public void TryAttack_SendsIncreasingSequenceAndEncodedAim()
        {
            var rig = new Rig(TestWeapons);
            Assert.IsTrue(rig.Controller.Select(CombatWeaponId.Hand));

            Assert.IsTrue(rig.Controller.TryAttack(90f));
            rig.Server.Flush();

            Assert.AreEqual(1, rig.SentAttacks.Count);
            Assert.AreEqual((ushort)1, rig.SentAttacks[0].RequestSeq);
            Assert.AreEqual(CombatWeaponId.Hand, rig.SentAttacks[0].Weapon);
            Assert.AreEqual((ushort)16384, rig.SentAttacks[0].Aim);
        }

        [Test]
        public void TryAttack_BlockedByCooldownUntilTickPasses()
        {
            var rig = new Rig(TestWeapons);
            rig.Controller.Select(CombatWeaponId.Hand);

            Assert.IsTrue(rig.Controller.TryAttack(0f));
            Assert.IsFalse(rig.Controller.TryAttack(0f));
            rig.Controller.Tick(0.5f);
            Assert.IsTrue(rig.Controller.TryAttack(0f));
            rig.Server.Flush();
            Assert.AreEqual((ushort)2, rig.SentAttacks[1].RequestSeq);
        }

        [Test]
        public void Cadence_ComesFromFireRateConfig()
        {
            var rig = new Rig(TestWeapons);
            rig.Controller.Select(CombatWeaponId.RubberGun);
            Assert.AreEqual(1f / 3f, rig.Controller.SelectedSlot.CadenceSeconds, 0.0001f);

            Assert.IsTrue(rig.Controller.TryAttack(0f));
            rig.Controller.Tick(0.34f);
            Assert.IsTrue(rig.Controller.TryAttack(0f));
        }

        [Test]
        public void Cadence_ChangesWhenConfigChangesWithoutCodeChange()
        {
            var faster = TestWeapons.Replace("\"fireRatePerSecond\": 3", "\"fireRatePerSecond\": 6");
            var rig = new Rig(faster);
            rig.Controller.Select(CombatWeaponId.RubberGun);
            Assert.AreEqual(1f / 6f, rig.Controller.SelectedSlot.CadenceSeconds, 0.0001f);
        }

        [Test]
        public void RejectedAttack_RollsBackPredictedMagazine()
        {
            var rig = new Rig(TestWeapons);
            rig.Controller.Select(CombatWeaponId.RubberGun);
            rig.Server.AttackOverride = (p, packet) => CombatOutcome.NoAmmo;

            Assert.IsTrue(rig.Controller.TryAttack(0f));
            Assert.AreEqual(1, rig.Controller.SelectedSlot.Magazine);
            rig.Server.Flush();

            Assert.AreEqual(0, rig.Controller.PendingCount);
            Assert.IsTrue(rig.Events.Has(CombatEventKind.AttackRejected));
            Assert.AreEqual(CombatOutcome.NoAmmo, rig.Events.LastOutcome);
            Assert.AreEqual(2, rig.Controller.SelectedSlot.Magazine);
        }

        [Test]
        public void Shotgun_FiresFivePelletsAcrossTwentyFiveDegrees()
        {
            var rig = new Rig(TestWeapons);
            rig.Controller.Select(CombatWeaponId.Shotgun);
            var slot = rig.Controller.SelectedSlot;
            Assert.AreEqual(5, slot.Pellets);
            Assert.AreEqual(25f, slot.SpreadDegrees);

            var angles = new float[8];
            var count = CombatShotMath.PelletAngles(0f, slot.Pellets, slot.SpreadDegrees, angles);
            Assert.AreEqual(5, count);
            Assert.AreEqual(-12.5f, angles[0], 0.001f);
            Assert.AreEqual(0f, angles[2], 0.001f);
            Assert.AreEqual(12.5f, angles[4], 0.001f);
        }

        [Test]
        public void Reload_PredictsTimerThenWaitsForServerConfirmation()
        {
            var rig = new Rig(TestWeapons);
            rig.Controller.Select(CombatWeaponId.RubberGun);
            rig.Controller.TryAttack(0f);
            rig.Controller.Tick(1f);
            rig.Controller.TryAttack(0f);
            rig.Server.Flush();
            rig.Controller.Tick(1f);
            Assert.AreEqual(0, rig.Controller.SelectedSlot.Magazine);

            Assert.IsTrue(rig.Controller.TryReload());
            Assert.IsTrue(rig.Controller.IsReloadPending);
            Assert.IsTrue(rig.Controller.IsReloading, "the timer is predicted from the config seconds");
            Assert.IsFalse(rig.Controller.IsReloadConfirmed);
            Assert.IsFalse(rig.Controller.TryAttack(0f));

            rig.Server.Flush();
            Assert.IsFalse(rig.Controller.IsReloadPending);
            Assert.IsTrue(rig.Controller.IsReloadConfirmed);
            Assert.AreEqual(2f, rig.Controller.ReloadRemainingSeconds, 0.0001f);
            Assert.IsTrue(rig.Events.Has(CombatEventKind.ReloadStarted));
        }

        [Test]
        public void Reload_ReconcilesTimerToServerReloadEndTick()
        {
            var rig = new Rig(TestWeapons);
            rig.Controller.Select(CombatWeaponId.RubberGun);
            rig.Controller.TryAttack(0f);
            rig.Controller.TryAttack(0f);
            rig.Server.Flush();
            rig.Controller.TryReload();
            rig.Controller.Tick(0.5f);

            rig.Server.Flush();
            Assert.AreEqual(1.5f, rig.Controller.ReloadRemainingSeconds, 0.0001f);
        }

        [Test]
        public void Reload_FinishesAfterConfiguredSecondsAndFillsFromReserve()
        {
            var rig = new Rig(TestWeapons);
            rig.Controller.Select(CombatWeaponId.RubberGun);
            rig.Controller.TryAttack(0f);
            rig.Controller.Tick(1f);
            rig.Controller.TryAttack(0f);
            rig.Server.Flush();
            rig.Controller.Tick(1f);
            rig.Controller.TryReload();
            rig.Server.Flush();

            rig.Controller.Tick(1f);
            Assert.IsTrue(rig.Controller.IsReloading);
            rig.Controller.Tick(1f);
            Assert.IsFalse(rig.Controller.IsReloading);
            Assert.AreEqual(2, rig.Controller.SelectedSlot.Magazine);
            Assert.AreEqual(1, rig.Controller.SelectedSlot.Reserve);
            Assert.IsTrue(rig.Events.Has(CombatEventKind.ReloadFinished));
        }

        [Test]
        public void Reload_ServerSideMagazineMatchesAfterReloadEndTick()
        {
            var rig = new Rig(TestWeapons);
            rig.Controller.Select(CombatWeaponId.RubberGun);
            rig.Controller.TryAttack(0f);
            rig.Controller.Tick(1f);
            rig.Controller.TryAttack(0f);
            rig.Server.Flush();
            rig.Controller.Tick(1f);
            rig.Controller.TryReload();
            rig.Server.Flush();
            Assert.AreEqual(0, rig.Peer.MagazineOf(CombatWeaponId.RubberGun));

            rig.Server.AdvanceTicks(60);
            Assert.AreEqual(2, rig.Peer.MagazineOf(CombatWeaponId.RubberGun));
            Assert.AreEqual(1, rig.Peer.ReserveOf(CombatWeaponId.RubberGun));
        }

        [Test]
        public void Reload_RejectedResultCancelsPredictedTimer()
        {
            var rig = new Rig(TestWeapons);
            rig.Controller.Select(CombatWeaponId.RubberGun);
            rig.Controller.TryAttack(0f);
            rig.Controller.Tick(1f);
            rig.Controller.TryAttack(0f);
            rig.Server.Flush();
            rig.Controller.Tick(1f);

            Assert.IsTrue(rig.Controller.TryReload());
            rig.Controller.OnReloadResult(new ReloadResultPacket
            {
                RequestSeq = rig.Sink.Reloads[0].RequestSeq, Weapon = CombatWeaponId.RubberGun, NetId = 1,
                Outcome = CombatOutcome.NoReserve, Magazine = 0
            });

            Assert.IsFalse(rig.Controller.IsReloadPending);
            Assert.IsFalse(rig.Controller.IsReloading);
            Assert.IsTrue(rig.Events.Has(CombatEventKind.ReloadRejected));
            Assert.AreEqual(CombatOutcome.NoReserve, rig.Events.LastOutcome);
        }

        [Test]
        public void Select_PushesHeldWeaponAndAmmoToHud()
        {
            var rig = new Rig(TestWeapons);
            rig.Controller.Select(CombatWeaponId.RubberGun);
            Assert.AreEqual("RUBBER_GUN", rig.Hud.HeldWeapon);
            Assert.AreEqual(2, rig.Hud.AmmoInMagazine);
            Assert.AreEqual(3, rig.Hud.AmmoReserve);

            rig.Controller.Select(CombatWeaponId.Hand);
            Assert.AreEqual("HAND", rig.Hud.HeldWeapon);
            Assert.AreEqual(0, rig.Hud.AmmoInMagazine);
        }

        [Test]
        public void Select_RejectsWeaponMissingFromBalance()
        {
            var rig = new Rig(TestWeapons);
            Assert.IsFalse(rig.Controller.Select(CombatWeaponId.Axe));
            Assert.IsFalse(rig.Controller.Slot(CombatWeaponId.Axe).Available);
            Assert.AreNotEqual(CombatWeaponId.Axe, rig.Controller.Selected);
        }

        [Test]
        public void Tick_DoesNotAllocateInSteadyState()
        {
            var rig = new Rig(TestWeapons);
            rig.Controller.Select(CombatWeaponId.RubberGun);
            rig.Controller.TryAttack(0f);
            rig.Controller.Tick(0.01f);
            Assert.That(() => rig.Controller.Tick(0.016f), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void TwoPeers_SeeRoomWideAttackAndReloadOnlyAfterServerConfirms()
        {
            var balance = new StaticBalance(TestWeapons);
            var server = new FakeCombatBackend(balance);
            var peerA = server.AddPeer(1);
            var peerB = server.AddPeer(2);
            var a = new WeaponController(balance, peerA, new RecordingHud(), 1);
            var b = new WeaponController(balance, peerB, new RecordingHud(), 2);
            peerA.AttackResult += a.OnAttackResult;
            peerA.ReloadResult += a.OnReloadResult;
            peerB.AttackResult += b.OnAttackResult;
            peerB.ReloadResult += b.OnReloadResult;

            var aEvents = new List<CombatEvent>();
            var bEvents = new List<CombatEvent>();
            a.Changed += aEvents.Add;
            b.Changed += bEvents.Add;

            a.Select(CombatWeaponId.RubberGun);
            a.TryAttack(45f);
            Assert.AreEqual(0, bEvents.Count, "B must not see A's attack before the server answers");
            Assert.AreEqual(1, a.PendingCount);

            server.Flush();
            Assert.AreEqual(0, a.PendingCount);
            Assert.AreEqual(CombatEventKind.AttackConfirmed, aEvents[aEvents.Count - 1].Kind);
            Assert.AreEqual(1, bEvents.Count);
            Assert.AreEqual(CombatEventKind.RemoteAttackConfirmed, bEvents[0].Kind);
            Assert.AreEqual((byte)1, bEvents[0].NetId);
            Assert.AreEqual(CombatWeaponId.RubberGun, bEvents[0].Weapon);

            a.Tick(1f);
            a.TryAttack(45f);
            server.Flush();
            a.Tick(1f);
            a.TryReload();
            Assert.AreEqual(2, bEvents.Count, "B must not see A's reload request before the server accepts it");
            server.Flush();
            Assert.AreEqual(3, bEvents.Count);
            Assert.AreEqual(CombatEventKind.RemoteReloadStarted, bEvents[2].Kind);
            Assert.AreEqual((byte)1, bEvents[2].NetId);
            Assert.AreEqual(CombatWeaponId.RubberGun, bEvents[2].Weapon);
            Assert.IsTrue(a.IsReloading);
            Assert.IsFalse(b.IsReloading, "B's own state is independent of A's reload");
        }
    }
}
