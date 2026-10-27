using System.Collections.Generic;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Audio;
using JungleMaze.App.Audio.Mocks;
using JungleMaze.App.Flow;
using JungleMaze.App.Hud.State;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Audio;
using JungleMaze.Domain.Hud;
using JungleMaze.Domain.Player;
using JungleMaze.Infrastructure.Config;
using JungleMaze.Tests.Hud;
using NUnit.Framework;

namespace JungleMaze.Tests.Audio
{
    public sealed class SfxRouterTests
    {
        private EventBus _bus;
        private BalanceConfigProvider _balance;
        private HudStateStore _hud;
        private InventoryStateStore _inventory;
        private FakeAudioService _audio;
        private FakeAudioEventSource _source;
        private MusicPlan _music;
        private SfxRouter _router;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _balance = StoreTestUtil.Balance();
            _hud = new HudStateStore(_bus, _balance);
            _inventory = new InventoryStateStore(_bus, _balance);
            _hud.Initialize();
            _inventory.Initialize();
            _audio = new FakeAudioService();
            _source = new FakeAudioEventSource();
            _music = new MusicPlan();
            _router = new SfxRouter(_audio, _hud, _inventory, _bus, _balance, new IAudioEventSource[] { _source }, _music);
            _router.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _router.Dispose();
            _hud.Dispose();
            _inventory.Dispose();
        }

        private void Advance(float seconds)
        {
            _router.Tick(seconds);
            _hud.Tick(seconds);
            _inventory.Tick(seconds);
        }

        [Test]
        public void MapsEveryAvailableEvent()
        {
            _hud.NotifyPickup("CROSSBOW", 1);
            Assert.AreEqual(new[] { SfxId.PickupWeapon }, _audio.Ids().ToArray());
            _audio.Cues.Clear();

            _hud.NotifyPickup("GOD_LEAF", 2);
            _hud.NotifyPickup("AMMO_ROUND", 12);
            Assert.AreEqual(new[] { SfxId.PickupItem, SfxId.PickupItem }, _audio.Ids().ToArray(), "consumable and ammo");
            _audio.Cues.Clear();

            _hud.NotifyHitDealt(HitTargetKind.Hero);
            _hud.NotifyHitDealt(HitTargetKind.Other);
            Assert.AreEqual(new[] { SfxId.HitMarker }, _audio.Ids().ToArray(), "Q6: only Hero hits");
            _audio.Cues.Clear();

            _hud.NotifyDamageTaken(false, 0f);
            Assert.AreEqual(new[] { SfxId.HurtLocal }, _audio.Ids().ToArray());
            _audio.Cues.Clear();

            _hud.SetDanger(7, DangerKind.FusedBomb, 1.5f, true, 3f, 4f);
            Assert.AreEqual(1, _audio.Cues.Count);
            Assert.AreEqual(SfxId.BombFuse, _audio.Cues[0].Id);
            Assert.IsTrue(_audio.Cues[0].HasPosition);
            Assert.AreEqual(3f, _audio.Cues[0].X);
            Assert.AreEqual(4f, _audio.Cues[0].Y);
            _audio.Cues.Clear();
            for (var i = 0; i < 100; i++) Advance(0.02f);
            Assert.AreEqual(new[] { SfxId.Explosion }, _audio.Ids().ToArray(), "fuse expired");
            Assert.AreEqual(3f, _audio.Cues[0].X);
            _audio.Cues.Clear();

            _hud.SetDanger(8, DangerKind.FusedBomb, 1.5f, true, 1f, 1f);
            _hud.ClearDanger(8);
            Advance(2f);
            Assert.AreEqual(new[] { SfxId.BombFuse }, _audio.Ids().ToArray(), "removed bomb does not explode");
            _audio.Cues.Clear();

            _inventory.BeginUse(4, "GOD_LEAF", 2f, 2f, true);
            _inventory.CancelUse("GOD_LEAF");
            _inventory.BeginUse(4, "ADRENALINE", 1f, 1f, true);
            _inventory.CompleteUse("ADRENALINE");
            Assert.AreEqual(new[] { SfxId.UseHeal, SfxId.UseCancel, SfxId.UseDrink }, _audio.Ids().ToArray(), "Q8: no sound on complete");
            _audio.Cues.Clear();

            _hud.SetArmor("WOODEN_ARMOR", 50f);
            Advance(1f);
            _hud.SetHelmet("WOODEN_HELMET", 40f);
            Advance(1f);
            _hud.SetArmor("ARMOR", 80f);
            Assert.AreEqual(new[] { SfxId.WearArmor, SfxId.WearArmor, SfxId.WearArmor }, _audio.Ids().ToArray());
            _audio.Cues.Clear();

            Advance(1f);
            _hud.NotifyDamageTaken(true, 90f);
            _hud.SetArmor("ARMOR", 60f);
            Assert.AreEqual(new[] { SfxId.HurtLocal, SfxId.ArmorAbsorb }, _audio.Ids().ToArray(), "Q14: both play");
            _audio.Cues.Clear();

            Advance(1f);
            _hud.SetHelmet("WOODEN_HELMET", 30f);
            Advance(0.05f);
            _hud.NotifyDamageTaken(false, 0f);
            Assert.AreEqual(new[] { SfxId.HurtLocal, SfxId.ArmorAbsorb }, _audio.Ids().ToArray(), "drop just before the damage");
            _audio.Cues.Clear();

            _bus.Publish(new AppScreenChanged(AppScreen.InMatch, AppScreen.Results, null));
            Assert.AreEqual(new[] { SfxId.MatchEnd }, _audio.Ids().ToArray());
            Assert.AreEqual(AppScreen.Results, _music.Screen);
        }

        [Test]
        public void LeavingMatch_StopsAllBeforeMatchEnd()
        {
            _bus.Publish(new AppScreenChanged(AppScreen.MainMenu, AppScreen.Connecting, null));
            _bus.Publish(new AppScreenChanged(AppScreen.Connecting, AppScreen.InRoom, null));
            _bus.Publish(new AppScreenChanged(AppScreen.InRoom, AppScreen.InMatch, null));
            Assert.AreEqual(0, _audio.Stops.Count, "entering screens stops nothing");
            _bus.Publish(new AppScreenChanged(AppScreen.InMatch, AppScreen.Results, null));
            Assert.AreEqual(1, _audio.Stops.Count);
            Assert.AreEqual((SfxId.None, 0), _audio.Stops[0], "StopAll");
            Assert.AreEqual(0, _audio.StopAt[0], "stopped before MatchEnd was played");
            Assert.AreEqual(new[] { SfxId.MatchEnd }, _audio.Ids().ToArray());

            _bus.Publish(new AppScreenChanged(AppScreen.Results, AppScreen.InMatch, null));
            _bus.Publish(new AppScreenChanged(AppScreen.InMatch, AppScreen.MainMenu, null));
            Assert.AreEqual(2, _audio.Stops.Count, "disconnecting mid-match also stops");
        }

        [Test]
        public void BombExplosionOrRemoval_StopsItsOwnFuse()
        {
            _hud.SetDanger(7, DangerKind.FusedBomb, 1.5f, true, 3f, 4f);
            _hud.SetDanger(9, DangerKind.FusedBomb, 5f, true, 1f, 1f);
            var fuse7 = _audio.Cues[0].SourceKey;
            for (var i = 0; i < 100; i++) Advance(0.02f);
            Assert.AreEqual(1, _audio.Stops.Count);
            Assert.AreEqual((SfxId.BombFuse, fuse7), _audio.Stops[0]);
            Assert.AreEqual(2, _audio.StopAt[0], "fuse stopped before the explosion played");
            Assert.AreEqual(SfxId.Explosion, _audio.Cues[2].Id);
            Assert.AreEqual(fuse7, _audio.Cues[2].SourceKey);

            var fuse9 = _audio.Cues[1].SourceKey;
            _hud.ClearDanger(9);
            Assert.AreEqual((SfxId.BombFuse, fuse9), _audio.Stops[1], "a removed bomb's fuse stops too");
            Assert.AreEqual(3, _audio.Cues.Count, "no explosion for a removed bomb");
        }

        [Test]
        public void ResetAndInitialState_Silent()
        {
            _bus.Publish(new AppScreenChanged(AppScreen.InRoom, AppScreen.InMatch, null));
            _hud.SetArmor("WOODEN_ARMOR", 50f);
            _hud.SetHelmet("WOODEN_HELMET", 40f);
            _hud.NotifyPickup("CROSSBOW", 1);
            Assert.AreEqual(0, _audio.Cues.Count, "settle window after entering the match");

            Advance(1f);
            _hud.Reset();
            _hud.SetArmor("ARMOR", 80f);
            Assert.AreEqual(0, _audio.Cues.Count, "settle window after a store reset");
            Advance(1f);
            _hud.SetArmor("ARMOR", 80f);
            Assert.AreEqual(0, _audio.Cues.Count, "same piece again is not a change");
        }

        [Test]
        public void TakeOff_Silent()
        {
            _hud.SetArmor("WOODEN_ARMOR", 50f);
            _audio.Cues.Clear();
            Advance(1f);
            _hud.SetArmor(null, 0f);
            Assert.AreEqual(0, _audio.Cues.Count);
        }

        [Test]
        public void DurabilityDropWithoutDamage_Silent()
        {
            _hud.SetArmor("WOODEN_ARMOR", 50f);
            _audio.Cues.Clear();
            Advance(1f);
            _hud.SetArmor("WOODEN_ARMOR", 40f);
            Advance(0.5f);
            _hud.NotifyDamageTaken(false, 0f);
            Assert.AreEqual(new[] { SfxId.HurtLocal }, _audio.Ids().ToArray(), "drop 0.5 s before damage is not absorb");
        }

        [Test]
        public void HudEventsWithinCooldown_OnePlay()
        {
            _hud.NotifyHitDealt(HitTargetKind.Hero);
            Advance(0.1f);
            _hud.NotifyHitDealt(HitTargetKind.Hero);
            _hud.NotifyPickup("GOD_LEAF", 1);
            Advance(0.1f);
            _hud.NotifyPickup("GOD_LEAF", 1);
            _hud.NotifyDamageTaken(false, 0f);
            Advance(0.05f);
            _hud.NotifyDamageTaken(false, 0f);
            Assert.AreEqual(1, _audio.Count(SfxId.HitMarker));
            Assert.AreEqual(1, _audio.Count(SfxId.PickupItem));
            Assert.AreEqual(1, _audio.Count(SfxId.HurtLocal));
            Advance(0.2f);
            _hud.NotifyHitDealt(HitTargetKind.Hero);
            Assert.AreEqual(2, _audio.Count(SfxId.HitMarker), "a new hit after the window plays");
        }

        [Test]
        public void DifferentPickupIds_BothPlay()
        {
            _hud.NotifyPickup("GOD_LEAF", 1);
            _hud.NotifyPickup("SPIRIT_TEAR", 1);
            Assert.AreEqual(2, _audio.Count(SfxId.PickupItem));
            Assert.AreNotEqual(_audio.Cues[0].SourceKey, _audio.Cues[1].SourceKey, "separate cooldown sources");
        }

        [Test]
        public void KeyedCues_PlayOncePerKey()
        {
            var cue = AudioCue.Global(SfxId.Eliminated, 3, SfxDeduper.Key(1, 500, 2, 3));
            for (var i = 0; i < 5; i++) { _source.Raise(cue); Advance(0.03f); }
            Advance(1f);
            _source.Raise(cue);
            Assert.AreEqual(1, _audio.Count(SfxId.Eliminated));
            _source.Raise(AudioCue.Global(SfxId.Eliminated, 3, SfxDeduper.Key(1, 501, 2, 3)));
            Assert.AreEqual(2, _audio.Count(SfxId.Eliminated), "other tick plays");
        }

        [Test]
        public void SecondSourceImplementation_SameOutput()
        {
            var mock = new MockAudioEventSource();
            var audioB = new FakeAudioService();
            var routerB = new SfxRouter(audioB, null, null, null, null, new IAudioEventSource[] { mock });
            routerB.Initialize();
            var cues = new List<AudioCue>();
            mock.CueRaised += cues.Add;
            mock.RaiseAllUnsourced();
            mock.RepeatServerEvent();
            foreach (var cue in cues) _source.Raise(cue);
            CollectionAssert.AreEqual(_audio.Ids(), audioB.Ids());
            routerB.Dispose();
        }

        [Test]
        public void MockRaisesAll12Unsourced()
        {
            var mock = new MockAudioEventSource();
            var audio = new FakeAudioService();
            var router = new SfxRouter(audio, null, null, null, null, new IAudioEventSource[] { mock });
            router.Initialize();
            mock.RaiseAllUnsourced();
            CollectionAssert.AreEqual(MockAudioEventSource.Unsourced, audio.Ids());
            Assert.AreEqual(12, MockAudioEventSource.Unsourced.Length);
            CollectionAssert.AreEquivalent(new[]
            {
                SfxId.FootstepGrass, SfxId.FootstepSwamp, SfxId.BushEnter, SfxId.MeleeSwing, SfxId.MeleeHit, SfxId.ShootRanged,
                SfxId.ShootCrossbow, SfxId.Throw, SfxId.Reload, SfxId.EmptyClick, SfxId.TrapTriggered, SfxId.DragonRoar
            }, MockAudioEventSource.Unsourced);
            router.Dispose();
        }

        [Test]
        public void NothingWithoutCalls_600Ticks()
        {
            var mock = new MockAudioEventSource();
            var audio = new FakeAudioService();
            var router = new SfxRouter(audio, _hud, _inventory, _bus, _balance, new IAudioEventSource[] { mock });
            router.Initialize();
            for (var i = 0; i < 600; i++) { mock.Tick(1f / 60f); router.Tick(1f / 60f); _hud.Tick(1f / 60f); }
            Assert.AreEqual(0, audio.Cues.Count);
            router.Dispose();
        }

        [Test]
        public void Dispose_UnsubscribesAll()
        {
            Assert.AreEqual(1, _bus.HandlerCount<AppScreenChanged>());
            Assert.AreEqual(1, _source.HandlerCount);
            _router.Dispose();
            Assert.AreEqual(0, _bus.HandlerCount<AppScreenChanged>());
            Assert.AreEqual(0, _source.HandlerCount);
            _hud.NotifyHitDealt(HitTargetKind.Hero);
            _inventory.BeginUse(4, "GOD_LEAF", 2f, 2f, true);
            Assert.AreEqual(0, _audio.Cues.Count);
        }
    }

    public sealed class MatchAudioEventSourceTests
    {
        private FakePacketReceiver _receiver;
        private FakeMatchFeed _feed;
        private FakePoseSource _poses;
        private MatchAudioEventSource _source;
        private readonly List<AudioCue> _cues = new List<AudioCue>();

        [SetUp]
        public void SetUp()
        {
            _receiver = new FakePacketReceiver();
            _feed = new FakeMatchFeed { OwnNetId = 2 };
            _poses = new FakePoseSource();
            _source = new MatchAudioEventSource(_receiver, _feed, _poses);
            _source.Initialize();
            _cues.Clear();
            _source.CueRaised += _cues.Add;
        }

        [TearDown]
        public void TearDown() => _source.Dispose();

        private static EventPacket Died(uint tick, byte target) =>
            new EventPacket { Tick = tick, EventType = GameEventType.PlayerDied, SourceNetId = 1, TargetNetId = target };

        [Test]
        public void PlayerDied_KeyedCue()
        {
            _poses.Poses[5] = new PlayerPose { X = 4f, Y = 2f };
            _receiver.Deliver(Died(300, 5));
            _receiver.Deliver(Died(300, 2));
            _receiver.Deliver(Died(300, 9));
            _receiver.Deliver(new EventPacket { Tick = 300, EventType = GameEventType.PlayerHit, TargetNetId = 5 });
            Assert.AreEqual(2, _cues.Count, "unknown victim pose is skipped; hits are not mapped (T-8)");
            Assert.IsTrue(_cues[0].HasPosition);
            Assert.AreEqual(4f, _cues[0].X);
            Assert.IsFalse(_cues[1].HasPosition, "own elimination is 2D");
            Assert.AreNotEqual(0UL, _cues[0].DedupeKey);
            Assert.AreNotEqual(_cues[0].DedupeKey, _cues[1].DedupeKey);
            _receiver.Deliver(Died(300, 5));
            Assert.AreEqual(_cues[0].DedupeKey, _cues[2].DedupeKey, "same event, same key");
        }

        [Test]
        public void WarningRisingEdge_OncePerPhase()
        {
            _feed.HasZone = true;
            _feed.Phase = 0;
            for (var i = 0; i < 30; i++) _source.Tick(1f / 30f);
            Assert.AreEqual(0, _cues.Count);

            _feed.InWarning = true;
            for (var i = 0; i < 90; i++) _source.Tick(1f / 30f);
            Assert.AreEqual(1, _cues.Count, "30 Hz repeats while in warning do not ring again");
            Assert.AreEqual(SfxId.ZoneBell3, _cues[0].Id);

            _feed.InWarning = false;
            _source.Tick(1f / 30f);
            _feed.InWarning = true;
            _source.Tick(1f / 30f);
            Assert.AreEqual(1, _cues.Count, "same phase again within 2 s");

            _feed.InWarning = false;
            _feed.Phase = 1;
            _source.Tick(1f / 30f);
            _feed.InWarning = true;
            _source.Tick(1f / 30f);
            Assert.AreEqual(2, _cues.Count, "new phase rings");
            Assert.AreNotEqual(_cues[0].DedupeKey, _cues[1].DedupeKey);
        }

        [Test]
        public void Dispose_Unsubscribes()
        {
            Assert.AreEqual(1, _receiver.HandlerCount);
            _source.Dispose();
            Assert.AreEqual(0, _receiver.HandlerCount);
        }
    }

    public sealed class MusicPlanTests
    {
        [Test]
        public void ScreenMapping()
        {
            Assert.AreEqual(MusicTrack.WaitingRoom, MusicPlan.TrackFor(AppScreen.MainMenu));
            Assert.AreEqual(MusicTrack.WaitingRoom, MusicPlan.TrackFor(AppScreen.InRoom));
            Assert.AreEqual(MusicTrack.WaitingRoom, MusicPlan.TrackFor(AppScreen.Results));
            Assert.AreEqual(MusicTrack.Loading, MusicPlan.TrackFor(AppScreen.Connecting));
            Assert.AreEqual(MusicTrack.Match, MusicPlan.TrackFor(AppScreen.InMatch));
            Assert.AreEqual(MusicTrack.None, MusicPlan.TrackFor(AppScreen.Boot));
        }

        [Test]
        public void OffByDefault()
        {
            var plan = new MusicPlan();
            plan.SetScreen(AppScreen.MainMenu);
            for (var i = 0; i < 60; i++) plan.Tick(1f / 60f);
            Assert.AreEqual(0, plan.AudibleSources);
            plan.SetEnabled(true);
            Assert.AreEqual(MusicTrack.WaitingRoom, plan.TrackA);
        }

        [Test]
        public void Crossfade1s_AtMostTwo()
        {
            var plan = new MusicPlan();
            plan.SetEnabled(true);
            plan.SetScreen(AppScreen.InRoom);
            for (var i = 0; i < 120; i++) plan.Tick(1f / 60f);
            Assert.AreEqual(1f, plan.WeightA);
            plan.SetScreen(AppScreen.InMatch);
            var t = 0f;
            while (plan.TrackB != MusicTrack.None && t < 3f)
            {
                plan.Tick(1f / 60f);
                t += 1f / 60f;
                Assert.LessOrEqual(plan.AudibleSources, 2);
            }
            Assert.AreEqual(1f, t, 0.1f);
            Assert.AreEqual(MusicTrack.Match, plan.TrackA);
            Assert.AreEqual(1f, plan.WeightA, 0.02f);
            Assert.AreEqual(1, plan.AudibleSources);
        }

        [Test]
        public void SameTrack_NoRestart()
        {
            var plan = new MusicPlan();
            plan.SetEnabled(true);
            plan.SetScreen(AppScreen.MainMenu);
            for (var i = 0; i < 120; i++) plan.Tick(1f / 60f);
            plan.SetScreen(AppScreen.InRoom);
            Assert.AreEqual(MusicTrack.WaitingRoom, plan.TrackA);
            Assert.AreEqual(1f, plan.WeightA, "same track keeps playing");
            Assert.AreEqual(MusicTrack.None, plan.TrackB);

            plan.SetScreen(AppScreen.InMatch);
            plan.Tick(0.3f);
            plan.SetScreen(AppScreen.Results);
            Assert.AreEqual(MusicTrack.WaitingRoom, plan.TrackA, "back to the fading track: swapped, not restarted");
            Assert.AreEqual(0.7f, plan.WeightA, 0.02f);
        }

        [Test]
        public void DisableFadesOutIn04()
        {
            var plan = new MusicPlan();
            plan.SetEnabled(true);
            plan.SetScreen(AppScreen.InMatch);
            for (var i = 0; i < 120; i++) plan.Tick(1f / 60f);
            plan.SetEnabled(false);
            var t = 0f;
            while (plan.AudibleSources > 0 && t < 2f) { plan.Tick(1f / 60f); t += 1f / 60f; }
            Assert.AreEqual(0.4f, t, 0.05f);
            Assert.LessOrEqual(t, 0.5f);
        }

        [Test]
        public void DuckMinus9In02_ReleaseIn1()
        {
            var plan = new MusicPlan();
            plan.SetAlertActive(true);
            var t = 0f;
            while (plan.DuckDb > -9f && t < 1f) { plan.Tick(1f / 60f); t += 1f / 60f; }
            Assert.LessOrEqual(t, 0.2f + 1e-3f);
            plan.SetAlertActive(false);
            t = 0f;
            while (plan.DuckDb < 0f && t < 3f) { plan.Tick(1f / 60f); t += 1f / 60f; }
            Assert.LessOrEqual(t, 1f + 1e-3f);
            Assert.AreEqual(0f, plan.DuckDb);
        }
    }
}
