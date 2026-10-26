using JungleMaze.App.Audio;
using JungleMaze.App.Hud.State;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Audio;
using JungleMaze.Tests.Hud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace JungleMaze.Tests.Audio
{
    public sealed class SfxPlaybackServiceTests
    {
        private FakeAudioOutput _output;
        private HudStateStore _hud;
        private FakeAudioSettings _settings;
        private MusicPlan _music;
        private SfxPlaybackService _service;

        [SetUp]
        public void SetUp()
        {
            _output = new FakeAudioOutput();
            _hud = new HudStateStore(new EventBus(), StoreTestUtil.Balance());
            _hud.SetPose(0f, 0f, 0f);
            _settings = new FakeAudioSettings();
            _music = new MusicPlan();
            _service = new SfxPlaybackService(AudioSpec.Rules(), _output, _hud, _settings, _music);
        }

        [Test]
        public void EachOfThe26_OneVoiceOnItsBus()
        {
            foreach (var row in AudioSpec.Table)
            {
                _output.FinishAll();
                var before = _output.Plays.Count;
                _service.PlaySfx(row.Id, 1f, 0f, 77);
                Assert.AreEqual(before + 1, _output.Plays.Count, row.Id.ToString());
                var call = _output.Plays[_output.Plays.Count - 1];
                Assert.AreEqual(row.Id, call.Id);
                Assert.AreEqual(row.Bus, call.Bus, row.Id.ToString());
            }
        }

        [Test]
        public void CooldownAndMaxInstances_PerGroup()
        {
            // AC-A6: 10 plays from one source inside the cooldown → 1; 10 different sources at once → ≤ max.
            foreach (var id in new[] { SfxId.FootstepGrass, SfxId.ShootRanged, SfxId.Explosion, SfxId.PickupItem, SfxId.HitMarker })
            {
                var row = AudioSpec.RowOf(id);
                _output.FinishAll();
                _service.Tick(10f);
                var before = _output.CountPlays(id);
                var step = System.Math.Min(0.1f, row.Cooldown) / 12f;
                for (var i = 0; i < 10; i++) { _service.Play(AudioCue.Global(id, 5)); _service.Tick(step); }
                Assert.AreEqual(before + 1, _output.CountPlays(id), $"{id}: one source");

                _output.FinishAll();
                _service.Tick(10f);
                for (var s = 100; s < 110; s++) _service.Play(AudioCue.Global(id, s));
                Assert.LessOrEqual(_output.PlayingOf(id), row.Max, $"{id}: max instances");
                Assert.AreEqual(System.Math.Min(10, row.Max), _output.PlayingOf(id), $"{id}: fills up to max");

                _output.FinishAll();
                _service.Tick(row.Cooldown + 0.001f);
                var again = _output.CountPlays(id);
                _service.Play(AudioCue.Global(id, 5));
                Assert.AreEqual(again + 1, _output.CountPlays(id), $"{id}: plays again after cooldown");
            }
        }

        [Test]
        public void HitMarker_FiveIn60ms_OnePlay()
        {
            for (var i = 0; i < 5; i++) { _service.PlaySfx(SfxId.HitMarker); _service.Tick(0.012f); }
            Assert.AreEqual(1, _output.CountPlays(SfxId.HitMarker));
        }

        [Test]
        public void DistanceFalloff()
        {
            _service.PlaySfx(SfxId.ShootRanged, 0f, 0f, 1);
            Assert.AreEqual(0.8f * 1f, _output.Plays[0].Gain, 1e-4f);
            _service.PlaySfx(SfxId.ShootRanged, 6f, 0f, 2);
            Assert.AreEqual(0.8f * 0.5f, _output.Plays[1].Gain, 0.05f * 0.8f);
            _service.PlaySfx(SfxId.ShootRanged, 12f, 0f, 3);
            _service.PlaySfx(SfxId.ShootRanged, 20f, 0f, 4);
            Assert.AreEqual(2, _output.Plays.Count, "at or beyond 12 cells no voice");
            _service.PlaySfx(SfxId.HurtLocal, 40f, 0f, 5);
            Assert.AreEqual(3, _output.Plays.Count);
            Assert.AreEqual(0.8f, _output.Plays[2].Gain, 1e-4f, "2D ignores distance");
        }

        [Test]
        public void NoPose_PositionalSilent_2DPlays()
        {
            var hud = new HudStateStore(new EventBus(), StoreTestUtil.Balance());
            var service = new SfxPlaybackService(AudioSpec.Rules(), _output, hud);
            Assert.IsFalse(hud.HasPose);
            service.PlaySfx(SfxId.ShootRanged, 0f, 0f, 1);
            Assert.AreEqual(0, _output.Plays.Count);
            service.PlaySfx(SfxId.HurtLocal);
            Assert.AreEqual(1, _output.Plays.Count);
        }

        [Test]
        public void PoolBound_100Requests_NeverMoreThan16_CriticalWins()
        {
            // 16 Low voices: footsteps (max 6 + 6), reload (2), bush (2).
            foreach (var id in new[] { SfxId.FootstepGrass, SfxId.FootstepSwamp, SfxId.Reload, SfxId.BushEnter })
                for (var i = 0; i < 6; i++) _service.Play(AudioCue.Global(id, i));
            Assert.AreEqual(16, _output.PlayingCount);
            var plays = _output.Plays.Count;
            _service.PlaySfx(SfxId.HitMarker);
            Assert.AreEqual(plays + 1, _output.Plays.Count, "Critical plays at once");
            Assert.AreEqual(1, _output.Stops, "exactly one Low voice cut");
            Assert.AreEqual(16, _output.PlayingCount);

            for (var i = 0; i < 100; i++) _service.Play(AudioCue.Global((SfxId)(1 + i % 26), i));
            Assert.LessOrEqual(_output.PlayingCount, 16);
            Assert.AreEqual(1, _output.PlayingOf(SfxId.HitMarker), "the Critical voice was never cut");
        }

        [Test]
        public void SetVolume_DecibelsClampedAndSaved()
        {
            _service.SetVolume(AudioBus.Master, 0.5f);
            Assert.AreEqual(-6.02f, _output.BusDb[(int)AudioBus.Master], 0.1f);
            Assert.AreEqual(0.5f, _service.GetVolume(AudioBus.Master), 0.001f);
            Assert.AreEqual(0.5f, _settings.Values[AudioBus.Master], 0.001f);
            _service.SetVolume(AudioBus.Sfx, 2f);
            Assert.AreEqual(1f, _service.GetVolume(AudioBus.Sfx));
            Assert.AreEqual(0f, _output.BusDb[(int)AudioBus.Sfx], 0.01f);
            _service.SetVolume(AudioBus.Music, -1f);
            Assert.AreEqual(-80f, _output.BusDb[(int)AudioBus.Music], 0.001f);

            var restored = new SfxPlaybackService(AudioSpec.Rules(), new FakeAudioOutput(), _hud, _settings);
            Assert.AreEqual(0.5f, restored.GetVolume(AudioBus.Master), 0.001f, "loaded from the settings store");
        }

        [Test]
        public void MusicOffByDefault_ToggleSaved()
        {
            Assert.IsFalse(_service.MusicEnabled);
            Assert.IsFalse(_music.Enabled);
            _service.MusicEnabled = true;
            Assert.IsTrue(_music.Enabled);
            Assert.AreEqual(true, _settings.Music);
        }

        [Test]
        public void ZoneBell_ThreePlaysOnAlert_DucksMusicBetweenBells()
        {
            _service.MusicEnabled = true;
            _music.SetScreen(JungleMaze.App.Flow.AppScreen.InMatch);
            _service.PlaySfx(SfxId.ZoneBell3);
            var starts = new System.Collections.Generic.List<float> { _service.Now };
            var count = 1;
            for (var frame = 0; frame < 180; frame++)
            {
                if (frame == 30) _output.FinishAll(); // first bell ended before the next one: duck must hold.
                _service.Tick(1f / 60f);
                if (frame == 40) Assert.IsTrue(_service.AlertActive, "pending bell keeps the alert active");
                var c = _output.CountPlays(SfxId.ZoneBell3);
                if (c > count) { starts.Add(_service.Now); count = c; }
            }
            Assert.AreEqual(3, count);
            Assert.AreEqual(0.8f, starts[1] - starts[0], 0.05f);
            Assert.AreEqual(0.8f, starts[2] - starts[1], 0.05f);
            foreach (var p in _output.Plays) Assert.AreEqual(AudioBus.Alert, p.Bus);
            Assert.LessOrEqual(_output.DuckDb, -8.99f);
        }

        [Test]
        public void Settings_WrittenOnceAfterDebounce_NotPerCall()
        {
            for (var i = 0; i < 10; i++) { _service.SetVolume(AudioBus.Sfx, i / 10f); _service.Tick(0.03f); }
            Assert.AreEqual(0, _settings.Flushes, "dragging a slider does not write on every value");
            _service.Tick(SfxPlaybackService.SaveDebounceSeconds);
            Assert.AreEqual(1, _settings.Flushes);
            _service.Tick(2f);
            Assert.AreEqual(1, _settings.Flushes, "nothing pending, no write");
            _service.MusicEnabled = true;
            _service.Tick(0.6f);
            Assert.AreEqual(2, _settings.Flushes);
            _service.SetVolume(AudioBus.Master, 0.3f);
            _service.FlushSettings();
            Assert.AreEqual(3, _settings.Flushes, "pause/quit flushes immediately");
            _service.FlushSettings();
            _service.Tick(1f);
            Assert.AreEqual(3, _settings.Flushes);
        }

        [Test]
        public void StopAll_StopsVoicesAndPendingBells()
        {
            _service.PlaySfx(SfxId.ZoneBell3);
            _service.Play(AudioCue.Global(SfxId.FootstepGrass, 1));
            Assert.AreEqual(2, _output.PlayingCount);
            _service.StopAll();
            Assert.AreEqual(0, _output.PlayingCount);
            Assert.AreEqual(0, _service.ActiveVoices);
            for (var i = 0; i < 120; i++) _service.Tick(1f / 60f);
            Assert.AreEqual(1, _output.CountPlays(SfxId.ZoneBell3), "pending bells cancelled");
            Assert.IsFalse(_service.AlertActive);
        }

        [Test]
        public void Stop_OnlyThatIdAndSource()
        {
            _service.Play(AudioCue.Global(SfxId.BombFuse, 1));
            _service.Play(AudioCue.Global(SfxId.BombFuse, 2));
            _service.Play(AudioCue.Global(SfxId.Explosion, 1));
            _service.Stop(SfxId.BombFuse, 1);
            Assert.AreEqual(1, _output.PlayingOf(SfxId.BombFuse));
            Assert.AreEqual(1, _output.PlayingOf(SfxId.Explosion));
            Assert.AreEqual(1, _service.CountActive(SfxId.BombFuse));
        }

        [Test]
        public void MusicDuck_SentOnlyWhenItChanges()
        {
            Assert.AreEqual(1, _output.DuckCalls, "initial 0 dB");
            for (var i = 0; i < 120; i++) _service.Tick(1f / 60f);
            Assert.AreEqual(1, _output.DuckCalls, "no alert, no calls");
            _service.PlaySfx(SfxId.DragonRoar);
            for (var i = 0; i < 30; i++) _service.Tick(1f / 60f);
            Assert.AreEqual(-9f, _output.DuckDb, 1e-3f);
            var whileHeld = _output.DuckCalls;
            for (var i = 0; i < 30; i++) _service.Tick(1f / 60f);
            Assert.AreEqual(whileHeld, _output.DuckCalls, "held at -9 dB, no calls");
            _output.FinishAll();
            for (var i = 0; i < 120; i++) _service.Tick(1f / 60f);
            Assert.AreEqual(0f, _output.DuckDb, 1e-3f);
            var released = _output.DuckCalls;
            for (var i = 0; i < 60; i++) _service.Tick(1f / 60f);
            Assert.AreEqual(released, _output.DuckCalls);
        }

        [Test]
        public void MissingClip_WarnOnce_NoThrow()
        {
            var service = new SfxPlaybackService(AudioSpec.Rules(id => id == SfxId.Throw ? 0 : 1), _output, _hud,
                null, null, new UnityLogForTests());
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("No clip for SFX 'Throw'"));
            Assert.DoesNotThrow(() => { for (var i = 0; i < 5; i++) { service.PlaySfx(SfxId.Throw); service.Tick(1f); } });
            service.PlaySfx(SfxId.Reload);
            Assert.AreEqual(1, _output.Plays.Count);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void PlaySfx100_NoGcAlloc()
        {
            for (var i = 0; i < 200; i++) { _service.Play(AudioCue.Global((SfxId)(1 + i % 26), i)); _service.Tick(0.016f); }
            _output.Plays.Clear();
            Assert.That(() =>
            {
                for (var i = 0; i < 100; i++)
                {
                    _service.Play(AudioCue.Global((SfxId)(1 + i % 26), i));
                    _service.Tick(0.016f);
                }
            }, Is.Not.AllocatingGCMemory());
        }

        private sealed class UnityLogForTests : JungleMaze.Core.Logging.ILog
        {
            public void Info(string message) => Debug.Log(message);
            public void Warn(string message) => Debug.LogWarning(message);
            public void Error(string message) => Debug.LogError(message);
        }
    }
}
